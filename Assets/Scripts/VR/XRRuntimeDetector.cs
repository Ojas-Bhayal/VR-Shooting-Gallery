using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace VRShootingGallery.VR
{
    /// <summary>
    /// Centralized, high-performance XR runtime detector for Unity 6.6 / OpenXR.
    /// Distinguishes between:
    /// - PC Desktop mode (no active headset/controller)
    /// - VR mode (active OpenXR runtime with real VR display/headset active)
    /// 
    /// Performance guarantees:
    /// - Zero synchronous loader initialization calls in property getters (no InitializeLoaderSync).
    /// - Property getters (IsXRActive, IsOpenXRInitialized, IsXRDeviceActive) return cached state ($O(1)$).
    /// - Zero allocations in device/subsystem checks (reusable static lists).
    /// - Controlled single initialization lifecycle via Unity XR Management.
    /// - Event-driven updates on device connect/disconnect, plus low-frequency heartbeat refresh (1.5s).
    /// - Failure does not cause frame-by-frame retries.
    /// </summary>
    public class XRRuntimeDetector : MonoBehaviour
    {
        private static XRRuntimeDetector _instance;
        public static XRRuntimeDetector Instance => _instance;

        public static event Action<bool> OnXRModeChanged;

        private static bool? _simulatedXRState = null;

        // Cached runtime states (accessed via static getters with zero overhead)
        private static bool _cachedIsXRActive = false;
        private static bool _cachedIsOpenXRInitialized = false;
        private static bool _cachedIsXRDeviceActive = false;
        private static string _cachedActiveLoaderName = "None";

        private static bool _initializationAttempted = false;
        private static readonly List<XRDisplaySubsystem> s_DisplaySubsystems = new List<XRDisplaySubsystem>(4);

        private bool _lastReportedVRMode = false;
        private bool _hasInitialized = false;
        private float _lastRefreshTime = -999f;
        private const float LowFrequencyRefreshInterval = 1.5f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            RefreshCachedXRState();
            EvaluateAndLogRuntimeState(true);
        }

        private void Start()
        {
            var generalSettings = XRGeneralSettings.Instance;
            var manager = generalSettings != null ? generalSettings.Manager : null;

            if (manager != null)
            {
                if (manager.isInitializationComplete && manager.activeLoader != null)
                {
                    try
                    {
                        manager.StartSubsystems();
                    }
                    catch (Exception) { }
                    RefreshCachedXRState();
                    EvaluateAndLogRuntimeState(false);
                }
                else if (!_initializationAttempted && Application.isPlaying)
                {
                    _initializationAttempted = true;
                    StartCoroutine(InitializeXROnceRoutine(manager));
                }
            }

            InputDevices.deviceConnected += HandleDeviceChanged;
            InputDevices.deviceDisconnected += HandleDeviceChanged;
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= HandleDeviceChanged;
            InputDevices.deviceDisconnected -= HandleDeviceChanged;
            if (_instance == this) _instance = null;
        }

        private IEnumerator InitializeXROnceRoutine(XRManagerSettings manager)
        {
            if (manager == null) yield break;

            if (!manager.isInitializationComplete)
            {
                yield return manager.InitializeLoader();
            }

            if (manager.isInitializationComplete && manager.activeLoader != null)
            {
                try
                {
                    manager.StartSubsystems();
                }
                catch (Exception) { }
            }

            RefreshCachedXRState();
            EvaluateAndLogRuntimeState(true);
        }

        private void Update()
        {
            // Low-frequency heartbeat refresh (every 1.5s) to detect runtime changes without per-frame polling
            if (Time.unscaledTime - _lastRefreshTime >= LowFrequencyRefreshInterval)
            {
                _lastRefreshTime = Time.unscaledTime;
                bool previousMode = _cachedIsXRActive;
                RefreshCachedXRState();

                if (_cachedIsXRActive != previousMode)
                {
                    EvaluateAndLogRuntimeState(false);
                }
            }
        }

        private void HandleDeviceChanged(InputDevice device)
        {
            RefreshCachedXRState();
            EvaluateAndLogRuntimeState(false);
        }

        /// <summary>
        /// Centralized check whether a real VR headset/device is currently active.
        /// Returns inexpensive cached state ($O(1)$) with zero allocations.
        /// </summary>
        public static bool IsXRActive
        {
            get
            {
                if (_simulatedXRState.HasValue)
                {
                    return _simulatedXRState.Value;
                }
                return _cachedIsXRActive;
            }
        }

        /// <summary>
        /// Verifies whether the OpenXR / XR Management system has completed initialization with an active loader.
        /// Returns cached status; never initiates blocking synchronous initialization in the getter.
        /// </summary>
        public static bool IsOpenXRInitialized
        {
            get
            {
                if (_simulatedXRState.HasValue)
                {
                    return _simulatedXRState.Value;
                }

                // If not cached as initialized, check manager state once without triggering loader initialization
                if (!_cachedIsOpenXRInitialized)
                {
                    var generalSettings = XRGeneralSettings.Instance;
                    if (generalSettings != null && generalSettings.Manager != null)
                    {
                        var manager = generalSettings.Manager;
                        if (manager.isInitializationComplete && manager.activeLoader != null)
                        {
                            _cachedIsOpenXRInitialized = true;
                            _cachedActiveLoaderName = manager.activeLoader.name;
                        }
                    }
                }

                return _cachedIsOpenXRInitialized;
            }
        }

        /// <summary>
        /// Returns the name of the active XR loader, or "None".
        /// </summary>
        public static string ActiveLoaderName => _cachedActiveLoaderName;

        /// <summary>
        /// Verifies whether an actual XR device (HMD / display subsystem / VR controller) is actively running.
        /// Returns cached status with zero per-frame cost.
        /// </summary>
        public static bool IsXRDeviceActive => _cachedIsXRDeviceActive;

        /// <summary>
        /// Queries the underlying XR management and input subsystems without allocations.
        /// Called only on startup, on device connect/disconnect, or on throttled refresh.
        /// </summary>
        public static void RefreshCachedXRState()
        {
            var generalSettings = XRGeneralSettings.Instance;
            var manager = generalSettings != null ? generalSettings.Manager : null;

            if (manager != null && manager.isInitializationComplete && manager.activeLoader != null)
            {
                _cachedIsOpenXRInitialized = true;
                _cachedActiveLoaderName = manager.activeLoader.name;
            }
            else
            {
                _cachedIsOpenXRInitialized = false;
                _cachedActiveLoaderName = (manager != null && manager.activeLoader != null)
                    ? manager.activeLoader.name
                    : "None";
            }

            _cachedIsXRDeviceActive = CheckIsXRDeviceActiveInternal();
            _cachedIsXRActive = _cachedIsOpenXRInitialized && _cachedIsXRDeviceActive;
        }

        private static bool CheckIsXRDeviceActiveInternal()
        {
            if (XRSettings.isDeviceActive)
            {
                return true;
            }

            s_DisplaySubsystems.Clear();
            SubsystemManager.GetSubsystems(s_DisplaySubsystems);
            for (int i = 0; i < s_DisplaySubsystems.Count; i++)
            {
                if (s_DisplaySubsystems[i].running) return true;
            }

            var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (headDevice.isValid) return true;

            var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightDevice.isValid) return true;

            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftDevice.isValid) return true;

            return false;
        }

        /// <summary>
        /// Evaluates current state, notifies listeners if changed, and emits standard diagnostic logs.
        /// </summary>
        public void EvaluateAndLogRuntimeState(bool forceLog)
        {
            bool currentVRMode = IsXRActive;
            bool stateChanged = !_hasInitialized || (currentVRMode != _lastReportedVRMode);

            if (stateChanged || forceLog)
            {
                _lastReportedVRMode = currentVRMode;
                _hasInitialized = true;

                VRActiveHand currentHand = VRControllerPreference.Instance != null
                    ? VRControllerPreference.Instance.ActiveHand
                    : VRActiveHand.Right;

                Debug.Log($"[XR] Runtime mode: {(currentVRMode ? "VR" : "PC")}");
                Debug.Log($"[XR] OpenXR initialized: {IsOpenXRInitialized.ToString().ToLower()}");
                Debug.Log($"[XR] Active loader: {ActiveLoaderName}");
                Debug.Log($"[XR] XR device active: {IsXRDeviceActive.ToString().ToLower()}");
                Debug.Log($"[XR] Active controller hand: {currentHand}");

                OnXRModeChanged?.Invoke(currentVRMode);
            }
        }

        /// <summary>
        /// Allows testing / simulation of XR state in editor and test suites.
        /// Set to null to restore real hardware detection.
        /// </summary>
        public static void SetSimulatedXRState(bool? simulatedState)
        {
            _simulatedXRState = simulatedState;
            if (simulatedState.HasValue)
            {
                _cachedIsXRActive = simulatedState.Value;
                _cachedIsOpenXRInitialized = simulatedState.Value;
                _cachedIsXRDeviceActive = simulatedState.Value;
            }
            else
            {
                RefreshCachedXRState();
            }

            if (_instance != null)
            {
                _instance.EvaluateAndLogRuntimeState(true);
            }
            else
            {
                OnXRModeChanged?.Invoke(IsXRActive);
            }
        }
    }
}
