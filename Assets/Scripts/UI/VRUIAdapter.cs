using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRShootingGallery.Core;
using VRShootingGallery.VR;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Configures GameUI_Canvas presentation for seamless VR and PC compatibility:
    /// - In VR: Converts the canvas to a comfortable World Space presentation with TrackedDeviceGraphicRaycaster.
    ///   When a major UI panel (Main Menu, Pause, Results) opens, places it 1.5–2.0 meters in front of the player's
    ///   headset, oriented toward the player (without permanently following the headset).
    /// - On PC: Maintains pixel-accurate Screen Space Overlay presentation with GraphicRaycaster and unit scale (1,1,1).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class VRUIAdapter : MonoBehaviour
    {
        [Header("VR Presentation Settings")]
        [SerializeField] private float vrDistance = 1.8f;
        [SerializeField] private Vector3 vrScale = new Vector3(0.002f, 0.002f, 0.002f);
        [SerializeField] private float minHeight = 1.3f;

        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private TrackedDeviceGraphicRaycaster trackedRaycaster;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GameStateManager gameStateManager;

        private bool _isVRConfigured = false;
        public bool IsVRConfigured => _isVRConfigured;
        public float VRDistance => vrDistance;

        private void Awake()
        {
            if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();
            if (graphicRaycaster == null) graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (trackedRaycaster == null) trackedRaycaster = GetComponent<TrackedDeviceGraphicRaycaster>();
            if (trackedRaycaster == null) trackedRaycaster = gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (gameStateManager == null) gameStateManager = FindObjectOfType<GameStateManager>();

            if (XRRuntimeDetector.IsXRActive)
            {
                ConfigureForVR(targetCamera);
            }
            else
            {
                ConfigureForPC();
            }
        }

        private void Start()
        {
            if (gameStateManager == null) gameStateManager = FindObjectOfType<GameStateManager>();
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged += HandleGameStateChanged;
            }

            XRRuntimeDetector.OnXRModeChanged += HandleXRModeChanged;
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged -= HandleGameStateChanged;
            }

            XRRuntimeDetector.OnXRModeChanged -= HandleXRModeChanged;
        }

        private void HandleXRModeChanged(bool isVR)
        {
            if (isVR)
            {
                ConfigureForVR(targetCamera != null ? targetCamera : Camera.main);
            }
            else
            {
                ConfigureForPC();
            }
        }

        private void HandleGameStateChanged(GameState newState, GameState previousState)
        {
            // When a major UI panel opens in VR, position it cleanly in front of the player
            if (newState == GameState.MainMenu || newState == GameState.Paused || newState == GameState.Results)
            {
                if (_isVRConfigured)
                {
                    PositionInFrontOfHeadset(vrDistance);
                }
            }
        }

        /// <summary>
        /// Configures the canvas for VR World Space with TrackedDeviceGraphicRaycaster,
        /// comfortable scale, and initial positioning in front of the headset.
        /// </summary>
        public void ConfigureForVR(Camera eventCamera = null)
        {
            if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();
            if (eventCamera != null) targetCamera = eventCamera;
            if (targetCamera == null) targetCamera = Camera.main;

            targetCanvas.renderMode = RenderMode.WorldSpace;
            targetCanvas.worldCamera = targetCamera;

            transform.localScale = vrScale;

            if (trackedRaycaster == null) trackedRaycaster = GetComponent<TrackedDeviceGraphicRaycaster>();
            if (trackedRaycaster == null) trackedRaycaster = gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            trackedRaycaster.enabled = true;

            _isVRConfigured = true;

            PositionInFrontOfHeadset(vrDistance);
            Debug.Log("[VRUIAdapter] Configured GameUI_Canvas for VR World Space.");
        }

        /// <summary>
        /// Positions the canvas at the specified distance (1.5-2.0m) directly in front of the player's
        /// headset and orients it toward the player.
        /// This is called upon panel activation and does NOT continuously follow the head every frame.
        /// </summary>
        /// <param name="distance">Distance in meters (defaults to vrDistance: 1.8m).</param>
        public void PositionInFrontOfHeadset(float distance = 1.8f)
        {
            if (!_isVRConfigured) return;

            Camera headCam = targetCamera != null ? targetCamera : Camera.main;
            if (headCam == null) return;

            Vector3 headPos = headCam.transform.position;
            Vector3 forward = headCam.transform.forward;
            forward.y = 0f; // Level forward direction to avoid uncomfortable pitch

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 targetPos = headPos + forward * distance;
            targetPos.y = Mathf.Max(headPos.y - 0.1f, minHeight);

            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Debug.Log($"[VRUIAdapter] Positioned canvas in front of headset: pos={targetPos}, rot={transform.rotation.eulerAngles}");
        }

        /// <summary>
        /// Configures the canvas for PC Screen Space Overlay with unit scale.
        /// </summary>
        public void ConfigureForPC()
        {
            if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();

            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.localScale = Vector3.one;

            if (graphicRaycaster == null) graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (graphicRaycaster != null) graphicRaycaster.enabled = true;

            _isVRConfigured = false;
            Debug.Log("[VRUIAdapter] Configured GameUI_Canvas for PC Screen Space Overlay.");
        }
    }
}
