using System;
using UnityEngine;
using UnityEngine.XR;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.VR
{
    /// <summary>
    /// Throttled diagnostic logger and runtime telemetry for VR pose tracking,
    /// controller attachment, muzzle alignment, and shot-ray consistency.
    /// Development-only, low-overhead design satisfying Phase 3:
    /// - Uses cached runtime state ($O(1)$) from XRRuntimeDetector.
    /// - Logs immediately on meaningful state transitions without per-frame allocations.
    /// - Default snapshot interval: 5.0 seconds.
    /// - Console logging disabled by default (can be toggled in inspector/code).
    /// - Builds diagnostic string only when logging is enabled or snapshot requested.
    /// - Avoids per-frame GC allocations (compares references instead of creating strings).
    /// - Disabled by default in non-development player release builds.
    /// </summary>
    public class VRDiagnosticLogger : MonoBehaviour
    {
        public static VRDiagnosticLogger Instance { get; private set; }

        [Header("Telemetry Configuration")]
        [Tooltip("Seconds between regular diagnostic log snapshots (default 5s).")]
        [SerializeField] private float logInterval = 5.0f;

        [Tooltip("Whether to output formatted diagnostic logs to Unity Console.")]
        [SerializeField] private bool logToConsole = false;

        [Tooltip("Whether to render an on-screen debug GUI overlay in development.")]
        [SerializeField] private bool showOnScreenGUI = false;

        [Header("System References (Auto-resolved if null)")]
        [SerializeField] private VRControllerPreference controllerPreference;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private GameObject leftAnchor;
        [SerializeField] private GameObject rightAnchor;
        [SerializeField] private Transform leftControllerAnchor;
        [SerializeField] private Transform rightControllerAnchor;
        [SerializeField] private GameObject weaponMount;

        private float _lastLogTime = -999f;
        private VRActiveHand _lastHand = VRActiveHand.Right;
        private bool _lastXRActive = false;
        private WeaponController _lastWeapon = null;
        private Transform _lastMountParent = null;
        private string _lastDiagnosticSnapshot = "";

        public string LatestDiagnosticSnapshot => _lastDiagnosticSnapshot;
        public bool LogToConsole { get => logToConsole; set => logToConsole = value; }

        private void Awake()
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Low overhead: disable entirely in production release builds unless explicitly enabled
            enabled = false;
            return;
#endif
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            EmitDiagnosticSnapshot("Initial Startup Snapshot");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ResolveReferences()
        {
            if (controllerPreference == null) controllerPreference = FindAnyObjectByType<VRControllerPreference>();
            if (weaponManager == null) weaponManager = FindAnyObjectByType<WeaponManager>();

            if (controllerPreference != null)
            {
                if (leftControllerAnchor == null && controllerPreference.LeftControllerAnchor != null)
                    leftControllerAnchor = controllerPreference.LeftControllerAnchor.transform;
                if (rightControllerAnchor == null && controllerPreference.RightControllerAnchor != null)
                    rightControllerAnchor = controllerPreference.RightControllerAnchor.transform;
                if (weaponMount == null)
                    weaponMount = controllerPreference.WeaponMountGO;
            }

            if (leftControllerAnchor == null && leftAnchor != null) leftControllerAnchor = leftAnchor.transform;
            if (rightControllerAnchor == null && rightAnchor != null) rightControllerAnchor = rightAnchor.transform;

            if (leftControllerAnchor == null)
            {
                var left = transform.Find("LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor")?.transform;
                if (left != null) leftControllerAnchor = left;
            }

            if (rightControllerAnchor == null)
            {
                var right = transform.Find("RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor")?.transform;
                if (right != null) rightControllerAnchor = right;
            }

            if (weaponMount == null)
            {
                weaponMount = GameObject.Find("WeaponMount");
            }
        }

        private void Update()
        {
            // Zero-allocation state change detection: compare references instead of string allocations
            bool xrActive = XRRuntimeDetector.IsXRActive;
            VRActiveHand currentHand = controllerPreference != null ? controllerPreference.ActiveHand : VRActiveHand.Right;
            WeaponController currentWeapon = (weaponManager != null) ? weaponManager.ActiveWeapon : null;
            Transform currentParent = (weaponMount != null) ? weaponMount.transform.parent : null;

            bool stateChanged = (xrActive != _lastXRActive) ||
                                (currentHand != _lastHand) ||
                                (currentWeapon != _lastWeapon) ||
                                (currentParent != _lastMountParent);

            if (stateChanged)
            {
                _lastXRActive = xrActive;
                _lastHand = currentHand;
                _lastWeapon = currentWeapon;
                _lastMountParent = currentParent;

                EmitDiagnosticSnapshot("State Change Detected");
                return;
            }

            // Throttled periodic snapshot (default 5s)
            if (Time.unscaledTime - _lastLogTime >= logInterval)
            {
                EmitDiagnosticSnapshot("Periodic Snapshot");
            }
        }

        /// <summary>
        /// Captures and optionally logs full tracking, attachment, muzzle, and shot ray telemetry.
        /// Avoids building detailed string if logging is disabled and GUI is off.
        /// </summary>
        public void EmitDiagnosticSnapshot(string reason)
        {
            _lastLogTime = Time.unscaledTime;

            // If neither console logging nor GUI is requested, avoid expensive string generation
            if (!logToConsole && !showOnScreenGUI)
            {
                return;
            }

            ResolveReferences();

            // 1. XR Subsystem & Initialization (reads cached states with zero driver overhead)
            bool isXRActive = XRRuntimeDetector.IsXRActive;
            bool openXRInit = XRRuntimeDetector.IsOpenXRInitialized;
            string activeLoader = XRRuntimeDetector.ActiveLoaderName;
            bool xrDeviceActive = XRRuntimeDetector.IsXRDeviceActive;

            // 2. Hardware Device Validity
            InputDevice headDev = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            InputDevice leftDev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice rightDev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            bool headValid = headDev.isValid;
            bool leftValid = leftDev.isValid;
            bool rightValid = rightDev.isValid;

            // 3. Controller Tracking State (Distinguishing missing vs connected but non-tracking)
            string leftTrackingStatus = EvaluateTrackingStatus(leftDev, leftControllerAnchor);
            string rightTrackingStatus = EvaluateTrackingStatus(rightDev, rightControllerAnchor);

            // 4. Anchor World Poses
            Vector3 leftAnchorPos = leftControllerAnchor != null ? leftControllerAnchor.position : Vector3.zero;
            Vector3 leftAnchorRot = leftControllerAnchor != null ? leftControllerAnchor.eulerAngles : Vector3.zero;
            Vector3 rightAnchorPos = rightControllerAnchor != null ? rightControllerAnchor.position : Vector3.zero;
            Vector3 rightAnchorRot = rightControllerAnchor != null ? rightControllerAnchor.eulerAngles : Vector3.zero;

            // 5. Active Hand & Weapon Attachment
            VRActiveHand hand = controllerPreference != null ? controllerPreference.ActiveHand : VRActiveHand.Right;
            string mountParentName = (weaponMount != null && weaponMount.transform.parent != null)
                ? weaponMount.transform.parent.name : "None";

            // 6. Active Weapon Poses
            WeaponController activeWeapon = weaponManager != null ? weaponManager.ActiveWeapon : null;
            string weaponName = activeWeapon != null ? activeWeapon.WeaponName : "None";
            Vector3 weaponPos = activeWeapon != null ? activeWeapon.transform.position : Vector3.zero;
            Vector3 weaponRot = activeWeapon != null ? activeWeapon.transform.eulerAngles : Vector3.zero;

            // 7. Muzzle & Aim Ray Telemetry
            Vector3 muzzlePos = Vector3.zero;
            Vector3 muzzleFwd = Vector3.forward;
            Vector3 shotOrigin = Vector3.zero;
            Vector3 shotDir = Vector3.forward;
            Vector3 aimRayOrigin = Vector3.zero;
            Vector3 aimRayDir = Vector3.forward;
            float alignmentDot = 1.0f;
            float originDist = 0.0f;

            if (activeWeapon != null)
            {
                Transform muzzle = activeWeapon.MuzzlePoint;
                if (muzzle != null)
                {
                    muzzlePos = muzzle.position;
                    muzzleFwd = muzzle.forward;
                }

                WeaponController.WeaponShotRay shotRay = activeWeapon.CalculateShotRay();
                shotOrigin = shotRay.origin;
                shotDir = shotRay.direction;

                aimRayOrigin = shotOrigin;
                aimRayDir = shotDir;

                alignmentDot = Vector3.Dot(muzzleFwd, shotDir);
                originDist = Vector3.Distance(muzzlePos, shotOrigin);
            }

            // Build human-readable snapshot
            var sb = new System.Text.StringBuilder(512);
            sb.AppendLine($"[VR Diagnostics] === Snapshot ({reason}) @ Time: {Time.time:F2}s ===");
            sb.AppendLine($"  Runtime Mode: {(isXRActive ? "VR" : "PC Desktop")} | OpenXR Initialized: {openXRInit} | Active Loader: {activeLoader} | XR Device Active: {xrDeviceActive}");
            sb.AppendLine($"  Devices: Head={headValid} | Left={leftValid} ({leftTrackingStatus}) | Right={rightValid} ({rightTrackingStatus})");
            sb.AppendLine($"  Left Anchor: Pos={leftAnchorPos:F3}, Rot={leftAnchorRot:F1}");
            sb.AppendLine($"  Right Anchor: Pos={rightAnchorPos:F3}, Rot={rightAnchorRot:F1}");
            sb.AppendLine($"  Active Hand: {hand} | WeaponMount Parent: {mountParentName}");
            sb.AppendLine($"  Active Weapon: {weaponName} | Pos={weaponPos:F3}, Rot={weaponRot:F1}");
            sb.AppendLine($"  MuzzlePoint: Pos={muzzlePos:F3}, Fwd={muzzleFwd:F3}");
            sb.AppendLine($"  Authoritative Shot: Origin={shotOrigin:F3}, Dir={shotDir:F3}");
            sb.AppendLine($"  Visual Aim Ray: Origin={aimRayOrigin:F3}, Dir={aimRayDir:F3}");
            sb.AppendLine($"  Muzzle-to-Shot Alignment Dot: {alignmentDot:F4} | Origin Offset: {originDist:F4}m");

            _lastDiagnosticSnapshot = sb.ToString();

            if (logToConsole)
            {
                Debug.Log(_lastDiagnosticSnapshot);
            }
        }

        private string EvaluateTrackingStatus(InputDevice device, Transform anchor)
        {
            if (!device.isValid)
            {
                return "Device Missing / Disconnected";
            }

            bool hasTrackingState = device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState trackingState);
            if (hasTrackingState)
            {
                bool hasPos = (trackingState & InputTrackingState.Position) != 0;
                bool hasRot = (trackingState & InputTrackingState.Rotation) != 0;

                if (hasPos && hasRot) return "Connected & 6-DOF Tracking";
                if (hasRot) return "Connected & 3-DOF Rotation Only (Position Untracked)";
                if (hasPos) return "Connected & 3-DOF Position Only (Rotation Untracked)";
                return "Connected but NOT Tracking (TrackingState None)";
            }

            if (anchor != null)
            {
                return "Connected (Device Valid)";
            }

            return "Connected (No Anchor)";
        }

        private void OnGUI()
        {
            if (!showOnScreenGUI) return;

            GUI.color = Color.white;
            GUI.Box(new Rect(10, 10, 480, 260), "VR Runtime Diagnostics");
            GUI.Label(new Rect(20, 30, 460, 230), _lastDiagnosticSnapshot);
        }
    }
}
