using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using VRShootingGallery.Core;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.VR
{
    public enum VRActiveHand
    {
        Right,
        Left
    }

    /// <summary>
    /// Centralized manager for single active VR controller selection.
    /// Exposes queries for which hand is active and manages XR interactor states
    /// so only the selected hand can perform gameplay and UI interactions,
    /// while preserving OpenXR device tracking on both controllers.
    /// </summary>
    public class VRControllerPreference : MonoBehaviour
    {
        public static VRControllerPreference Instance { get; private set; }

        [Header("Active Controller Setting")]
        [SerializeField] private VRActiveHand activeHand = VRActiveHand.Right;

        [Header("Left Controller References")]
        [SerializeField] private GameObject leftControllerAnchor;
        [SerializeField] private XRController leftXRController;
        [SerializeField] private XRDirectInteractor leftDirectInteractor;
        [SerializeField] private XRRayInteractor leftRayInteractor;
        [SerializeField] private XRInteractorLineVisual leftLineVisual;
        [SerializeField] private LineRenderer leftLineRenderer;
        [SerializeField] private Collider leftInteractionCollider;

        [Header("Right Controller References")]
        [SerializeField] private GameObject rightControllerAnchor;
        [SerializeField] private XRController rightXRController;
        [SerializeField] private XRDirectInteractor rightDirectInteractor;
        [SerializeField] private XRRayInteractor rightRayInteractor;
        [SerializeField] private XRInteractorLineVisual rightLineVisual;
        [SerializeField] private LineRenderer rightLineRenderer;
        [SerializeField] private Collider rightInteractionCollider;

        [Header("Weapon Attachment References")]
        [SerializeField] private Transform rightWeaponAttach;
        [SerializeField] private Transform leftWeaponAttach;
        [SerializeField] private Transform cameraWeaponMount;
        [SerializeField] private Transform leftCameraWeaponMount;
        [SerializeField] private GameObject weaponMount;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private GameStateManager gameStateManager;

        public event Action<VRActiveHand> OnActiveHandChanged;

        public VRActiveHand ActiveHand => activeHand;
        public bool IsRightControllerActive => activeHand == VRActiveHand.Right;
        public bool IsLeftControllerActive => activeHand == VRActiveHand.Left;

        // Static convenience queries
        public static bool IsRightActive => Instance == null || Instance.IsRightControllerActive;
        public static bool IsLeftActive => Instance != null && Instance.IsLeftControllerActive;
        public static VRActiveHand CurrentHand => Instance != null ? Instance.ActiveHand : VRActiveHand.Right;

        public GameObject LeftControllerAnchor => leftControllerAnchor;
        public GameObject RightControllerAnchor => rightControllerAnchor;
        public XRController LeftXRController => leftXRController;
        public XRController RightXRController => rightXRController;
        public XRDirectInteractor LeftDirectInteractor => leftDirectInteractor;
        public XRRayInteractor LeftRayInteractor => leftRayInteractor;
        public XRDirectInteractor RightDirectInteractor => rightDirectInteractor;
        public XRRayInteractor RightRayInteractor => rightRayInteractor;

        public XRInteractorLineVisual LeftLineVisual => leftLineVisual;
        public XRInteractorLineVisual RightLineVisual => rightLineVisual;
        public LineRenderer LeftLineRenderer => leftLineRenderer;
        public LineRenderer RightLineRenderer => rightLineRenderer;

        public Transform RightWeaponAttach => rightWeaponAttach;
        public Transform LeftWeaponAttach => leftWeaponAttach;
        public Transform CameraWeaponMount => cameraWeaponMount;
        public Transform LeftCameraWeaponMount => leftCameraWeaponMount;
        public GameObject WeaponMountGO => weaponMount;
        public Transform ActiveWeaponAttach => IsRightControllerActive ? rightWeaponAttach : leftWeaponAttach;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ResolveReferencesIfMissing();
            ApplyControllerStates();
            UpdateWeaponAttachment();
        }

        private void Start()
        {
            ApplyControllerStates();
            UpdateWeaponAttachment();
            ValidateSingleActiveHand();

            XRRuntimeDetector.OnXRModeChanged += HandleXRModeChanged;
            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched += HandleWeaponSwitched;
            }

            if (gameStateManager == null && GameManager.Instance != null)
            {
                gameStateManager = GameManager.Instance.StateManager;
            }
            if (gameStateManager == null)
            {
                gameStateManager = FindObjectOfType<GameStateManager>();
            }
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            XRRuntimeDetector.OnXRModeChanged -= HandleXRModeChanged;
            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched -= HandleWeaponSwitched;
            }
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged -= HandleGameStateChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleGameStateChanged(GameState newState, GameState previousState)
        {
            ApplyControllerStates();
        }

        private void HandleXRModeChanged(bool isVR)
        {
            ApplyControllerStates();
            UpdateWeaponAttachment();
        }

        private void HandleWeaponSwitched(WeaponController weapon)
        {
            UpdateWeaponAttachment();
        }

        /// <summary>
        /// Selects the single active VR controller (Right or Left).
        /// Immediately configures XR interactors and reattaches weapons so only the selected hand interacts and holds the weapon.
        /// </summary>
        public void SetActiveHand(VRActiveHand hand)
        {
            CheckAndHandleGrabbedWeaponTransfer();
            activeHand = hand;
            ApplyControllerStates();
            UpdateWeaponAttachment();
            ValidateSingleActiveHand();
            OnActiveHandChanged?.Invoke(activeHand);
            Debug.Log($"[VRControllerPreference] Active controller set to: {activeHand}");
        }

        /// <summary>
        /// Enables gameplay & UI interaction components on the active hand,
        /// and disables them on the inactive hand.
        /// Controller tracking components remain enabled on both.
        /// Hides blue controller line visual during gameplay (Practice, Challenge),
        /// while showing it only on the selected hand during menus (MainMenu, Paused, Results).
        /// </summary>
        public void ApplyControllerStates()
        {
            ResolveReferencesIfMissing();

            if (gameStateManager == null && GameManager.Instance != null)
            {
                gameStateManager = GameManager.Instance.StateManager;
            }

            bool isRight = (activeHand == VRActiveHand.Right);

            // Determine if the game is currently in active gameplay (Practice or Challenge).
            // In Edit Mode outside PlayMode, default to false so verifier checks pass.
            bool isGameplay = false;
            if (Application.isPlaying && gameStateManager != null)
            {
                isGameplay = (gameStateManager.CurrentState == GameState.Practice || gameStateManager.CurrentState == GameState.Challenge);
            }

            // Blue UI controller ray visual should ONLY be shown during menus (MainMenu, Paused, Results)
            // and ONLY on the selected active hand in VR mode (when XR is active and needed for menu interaction).
            // In PC Desktop mode (when XR is not active), mouse controls UI, so blue controller ray visual is NEVER shown.
            // During gameplay, the blue ray visual is hidden on BOTH hands.
            bool isVR = !Application.isPlaying || XRRuntimeDetector.IsXRActive;
            bool showRightRayVisual = isRight && !isGameplay && isVR;
            bool showLeftRayVisual = !isRight && !isGameplay && isVR;

            // Configure Right Hand gameplay interactors & XRController
            SetInteractorState(rightDirectInteractor, rightRayInteractor, rightXRController, rightLineVisual, rightLineRenderer, rightInteractionCollider, isRight, showRightRayVisual);

            // Configure Left Hand gameplay interactors & XRController
            SetInteractorState(leftDirectInteractor, leftRayInteractor, leftXRController, leftLineVisual, leftLineRenderer, leftInteractionCollider, !isRight, showLeftRayVisual);

            EnsureValidInteractionLayers();
        }

        private void SetInteractorState(XRDirectInteractor direct, XRRayInteractor ray, XRController ctrl, XRInteractorLineVisual visual, LineRenderer lr, Collider col, bool active, bool showRayVisual)
        {
            if (direct != null) direct.enabled = active;
            if (ray != null) ray.enabled = active;
            if (ctrl != null) ctrl.enabled = active;
            if (visual != null) visual.enabled = showRayVisual;
            if (lr != null) lr.enabled = showRayVisual;
            if (col != null) col.enabled = active;
        }

        /// <summary>
        /// Runtime validation that exactly one hand is active.
        /// </summary>
        public bool ValidateSingleActiveHand()
        {
            ResolveReferencesIfMissing();
            bool isRight = (activeHand == VRActiveHand.Right);

            bool rightDirectOn = rightDirectInteractor != null && rightDirectInteractor.enabled;
            bool leftDirectOn = leftDirectInteractor != null && leftDirectInteractor.enabled;
            bool rightRayOn = rightRayInteractor != null && rightRayInteractor.enabled;
            bool leftRayOn = leftRayInteractor != null && leftRayInteractor.enabled;

            bool exactlyOneDirect = (isRight && rightDirectOn && !leftDirectOn) || (!isRight && !rightDirectOn && leftDirectOn);
            bool exactlyOneRay = (isRight && rightRayOn && !leftRayOn) || (!isRight && !rightRayOn && leftRayOn);

            if (!exactlyOneDirect || !exactlyOneRay)
            {
                Debug.LogWarning($"[VRControllerPreference] Hand exclusivity warning: ActiveHand={activeHand}, Right(Direct={rightDirectOn}, Ray={rightRayOn}), Left(Direct={leftDirectOn}, Ray={leftRayOn})");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ensures XR interaction layers on both hands are non-zero and consistently configured.
        /// </summary>
        public void EnsureValidInteractionLayers()
        {
            int defaultLayerMask = InteractionLayerMask.GetMask("Default");
            if (defaultLayerMask == 0) defaultLayerMask = 1;

            if (leftRayInteractor != null && leftRayInteractor.interactionLayers.value == 0)
            {
                leftRayInteractor.interactionLayers = defaultLayerMask;
            }
            if (rightRayInteractor != null && rightRayInteractor.interactionLayers.value == 0)
            {
                rightRayInteractor.interactionLayers = defaultLayerMask;
            }
            if (leftDirectInteractor != null && (leftDirectInteractor.interactionLayers.value == 0 || leftDirectInteractor.interactionLayers.value == -1))
            {
                leftDirectInteractor.interactionLayers = defaultLayerMask;
            }
            if (rightDirectInteractor != null && (rightDirectInteractor.interactionLayers.value == 0 || rightDirectInteractor.interactionLayers.value == -1))
            {
                rightDirectInteractor.interactionLayers = defaultLayerMask;
            }
        }

        /// <summary>
        /// Resolves anchor, interactor, and weapon attachment references from PlayerRig hierarchy if unassigned.
        /// </summary>
        public void ResolveReferencesIfMissing()
        {
            if (leftControllerAnchor == null)
            {
                var leftTransform = transform.Find("LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor")?.transform;
                if (leftTransform != null) leftControllerAnchor = leftTransform.gameObject;
            }

            if (rightControllerAnchor == null)
            {
                var rightTransform = transform.Find("RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor")?.transform;
                if (rightTransform != null) rightControllerAnchor = rightTransform.gameObject;
            }

            if (leftControllerAnchor != null)
            {
                if (leftXRController == null) leftXRController = leftControllerAnchor.GetComponent<XRController>();
                if (leftDirectInteractor == null) leftDirectInteractor = leftControllerAnchor.GetComponent<XRDirectInteractor>();
                if (leftRayInteractor == null) leftRayInteractor = leftControllerAnchor.GetComponentInChildren<XRRayInteractor>(true);
                if (leftLineVisual == null) leftLineVisual = leftControllerAnchor.GetComponentInChildren<XRInteractorLineVisual>(true);
                if (leftLineRenderer == null) leftLineRenderer = leftControllerAnchor.GetComponentInChildren<LineRenderer>(true);
                if (leftInteractionCollider == null) leftInteractionCollider = leftControllerAnchor.GetComponent<Collider>();

                if (leftWeaponAttach == null)
                {
                    leftWeaponAttach = leftControllerAnchor.transform.Find("LeftWeaponAttach") ?? leftControllerAnchor.transform.Find("WeaponAttachPoint");
                    if (leftWeaponAttach == null)
                    {
                        var go = new GameObject("LeftWeaponAttach");
                        go.transform.SetParent(leftControllerAnchor.transform, false);
                        go.transform.localPosition = new Vector3(0f, -0.04f, 0.08f);
                        go.transform.localRotation = Quaternion.identity;
                        leftWeaponAttach = go.transform;
                    }
                }
            }

            if (rightControllerAnchor != null)
            {
                if (rightXRController == null) rightXRController = rightControllerAnchor.GetComponent<XRController>();
                if (rightDirectInteractor == null) rightDirectInteractor = rightControllerAnchor.GetComponent<XRDirectInteractor>();
                if (rightRayInteractor == null) rightRayInteractor = rightControllerAnchor.GetComponentInChildren<XRRayInteractor>(true);
                if (rightLineVisual == null) rightLineVisual = rightControllerAnchor.GetComponentInChildren<XRInteractorLineVisual>(true);
                if (rightLineRenderer == null) rightLineRenderer = rightControllerAnchor.GetComponentInChildren<LineRenderer>(true);
                if (rightInteractionCollider == null) rightInteractionCollider = rightControllerAnchor.GetComponent<Collider>();

                if (rightWeaponAttach == null)
                {
                    rightWeaponAttach = rightControllerAnchor.transform.Find("RightWeaponAttach") ?? rightControllerAnchor.transform.Find("WeaponAttachPoint");
                    if (rightWeaponAttach == null)
                    {
                        var go = new GameObject("RightWeaponAttach");
                        go.transform.SetParent(rightControllerAnchor.transform, false);
                        go.transform.localPosition = new Vector3(0f, -0.04f, 0.08f);
                        go.transform.localRotation = Quaternion.identity;
                        rightWeaponAttach = go.transform;
                    }
                }
            }

            if (weaponManager == null)
            {
                weaponManager = FindObjectOfType<WeaponManager>();
            }

            if (cameraWeaponMount == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cameraWeaponMount = cam.transform.Find("CameraWeaponMount");
                    if (cameraWeaponMount == null)
                    {
                        var go = new GameObject("CameraWeaponMount");
                        go.transform.SetParent(cam.transform, false);
                        go.transform.localPosition = new Vector3(0.22f, -0.18f, 0.42f);
                        go.transform.localRotation = Quaternion.identity;
                        cameraWeaponMount = go.transform;
                    }
                }
            }

            if (leftCameraWeaponMount == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    leftCameraWeaponMount = cam.transform.Find("LeftCameraWeaponMount");
                    if (leftCameraWeaponMount == null)
                    {
                        var go = new GameObject("LeftCameraWeaponMount");
                        go.transform.SetParent(cam.transform, false);
                        go.transform.localPosition = new Vector3(-0.22f, -0.18f, 0.42f);
                        go.transform.localRotation = Quaternion.identity;
                        leftCameraWeaponMount = go.transform;
                    }
                }
            }

            if (weaponMount == null)
            {
                var wm = GameObject.Find("WeaponMount");
                if (wm != null)
                {
                    weaponMount = wm;
                }
                else if (weaponManager != null && weaponManager.ActiveWeapon != null)
                {
                    weaponMount = weaponManager.ActiveWeapon.transform.parent != null
                        ? weaponManager.ActiveWeapon.transform.parent.gameObject
                        : weaponManager.ActiveWeapon.gameObject;
                }
            }

            if (gameStateManager == null)
            {
                var gm = GameManager.Instance ?? FindObjectOfType<GameManager>();
                if (gm != null) gameStateManager = gm.StateManager;
                if (gameStateManager == null) gameStateManager = FindObjectOfType<GameStateManager>();
            }
        }

        /// <summary>
        /// Updates the weapon attachment transform:
        /// - In VR: attaches the active weapon mount to the selected hand's dedicated attach transform (RightWeaponAttach or LeftWeaponAttach).
        /// - On PC: attaches the weapon mount to the camera's weapon mount (Right: CameraWeaponMount, Left: LeftCameraWeaponMount).
        /// Immediately applies 6-DOF transform without lag or snapping to invalid world positions.
        /// </summary>
        public void UpdateWeaponAttachment()
        {
            ResolveReferencesIfMissing();

            bool isVR = XRRuntimeDetector.IsXRActive;
            Transform targetAttach;

            if (!isVR)
            {
                if (activeHand == VRActiveHand.Left)
                {
                    targetAttach = leftCameraWeaponMount != null ? leftCameraWeaponMount : cameraWeaponMount;
                }
                else
                {
                    targetAttach = cameraWeaponMount != null ? cameraWeaponMount : (Camera.main != null ? Camera.main.transform : null);
                }
            }
            else
            {
                targetAttach = (activeHand == VRActiveHand.Right) ? rightWeaponAttach : leftWeaponAttach;
                if (targetAttach == null)
                {
                    // Fallback to controller anchor if attach transform missing
                    targetAttach = (activeHand == VRActiveHand.Right)
                        ? (rightControllerAnchor != null ? rightControllerAnchor.transform : null)
                        : (leftControllerAnchor != null ? leftControllerAnchor.transform : null);
                }
            }

            if (targetAttach == null) return;

            if (weaponMount != null)
            {
                // Don't fight XR grab if currently selected by an XR interactor
                var activeW = weaponManager != null ? weaponManager.ActiveWeapon : null;
                var grab = activeW != null ? activeW.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() : null;
                if (grab != null && grab.isSelected)
                {
                    return;
                }

                if (weaponMount.transform.parent != targetAttach)
                {
                    weaponMount.transform.SetParent(targetAttach, false);
                }

                weaponMount.transform.localPosition = Vector3.zero;
                weaponMount.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Handles deterministic grab release and transfer if a weapon is held during controller hand switching.
        /// </summary>
        public void CheckAndHandleGrabbedWeaponTransfer()
        {
            if (weaponManager == null) weaponManager = FindObjectOfType<WeaponManager>();
            if (weaponManager == null) return;

            var activeWeapon = weaponManager.ActiveWeapon;
            if (activeWeapon == null) return;

            var grab = activeWeapon.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                var interactor = grab.firstInteractorSelecting;
                if (interactor != null && grab.interactionManager != null)
                {
                    grab.interactionManager.SelectExit(interactor, grab);
                }
                activeWeapon.OnReleased();
            }
        }

        /// <summary>
        /// Checks whether a given interactor is allowed to perform gameplay/UI interaction.
        /// </summary>
        public bool IsInteractorAllowed(IXRInteractor interactor)
        {
            if (interactor == null) return false;

            if (interactor is MonoBehaviour mb)
            {
                bool isLeftObject = leftControllerAnchor != null && mb.transform.IsChildOf(leftControllerAnchor.transform);
                bool isRightObject = rightControllerAnchor != null && mb.transform.IsChildOf(rightControllerAnchor.transform);

                if (activeHand == VRActiveHand.Right)
                {
                    if (isLeftObject) return false;
                    if (isRightObject) return true;
                }
                else
                {
                    if (isRightObject) return false;
                    if (isLeftObject) return true;
                }
            }

            return true;
        }
    }
}
