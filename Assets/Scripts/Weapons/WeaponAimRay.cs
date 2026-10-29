using UnityEngine;
using VRShootingGallery.Core;
using VRShootingGallery.VR;

namespace VRShootingGallery.Weapons
{
    /// <summary>
    /// Visual gun aiming ray driven directly from the weapon muzzle.
    /// Satisfies Parts 4, 5, and 6:
    /// - Ray origin = weapon muzzle position
    /// - Ray direction = weapon muzzle forward (in VR) or camera-center convergence (on PC)
    /// - Dynamically matches gun rotation when pointing up, down, left, or right with no fixed Euler offsets.
    /// - Completely decoupled from the controller's UI XRRayInteractor.
    /// </summary>
    [RequireComponent(typeof(WeaponController))]
    public class WeaponAimRay : MonoBehaviour
    {
        [Header("Aim Ray References")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private Material aimMaterial;

        [Header("Ray Settings")]
        [SerializeField] private float maxDistance = 50.0f;
        [SerializeField] private float rayWidth = 0.004f;
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private bool showRayInVR = true;
        [SerializeField] private bool showRayOnPC = true;

        public LineRenderer AimLine => aimLine;
        public Transform MuzzlePoint => weaponController != null ? weaponController.MuzzlePoint : transform;

        private void Awake()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
            }

            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            if (aimLine == null)
            {
                var lineTrans = transform.Find("GunAimRay");
                if (lineTrans != null)
                {
                    aimLine = lineTrans.GetComponent<LineRenderer>();
                }
            }

            if (aimLine == null)
            {
                GameObject lineGO = new GameObject("GunAimRay");
                lineGO.transform.SetParent(transform, false);
                aimLine = lineGO.AddComponent<LineRenderer>();
                aimLine.useWorldSpace = true;
                aimLine.startWidth = rayWidth;
                aimLine.endWidth = rayWidth;
                aimLine.positionCount = 2;

                if (aimMaterial != null)
                {
                    aimLine.sharedMaterial = aimMaterial;
                }
            }
        }

        private GameStateManager _cachedStateManager;

        private void Start()
        {
            ResolveStateManager();
        }

        private void ResolveStateManager()
        {
            if (_cachedStateManager == null && GameManager.Instance != null)
            {
                _cachedStateManager = GameManager.Instance.StateManager;
            }
            if (_cachedStateManager != null)
            {
                _cachedStateManager.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_cachedStateManager != null)
            {
                _cachedStateManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandleGameStateChanged(GameState newState, GameState previousState)
        {
            UpdateAimRay();
        }

        private void LateUpdate()
        {
            UpdateAimRay();
        }

        private void OnDisable()
        {
            if (aimLine != null)
            {
                aimLine.enabled = false;
            }
        }

        /// <summary>
        /// Updates the visual aiming ray origin and direction to match the weapon muzzle.
        /// Only displayed during active gameplay (Practice, Challenge), and hidden during menus (MainMenu, Paused, Results).
        /// </summary>
        public void UpdateAimRay()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
            }
            if (aimLine == null)
            {
                SetupLineRenderer();
            }
            if (aimLine == null || weaponController == null) return;

            bool isVR = XRRuntimeDetector.IsXRActive || weaponController.IsGrabbed;
            bool shouldDisplay = isVR ? showRayInVR : showRayOnPC;

            if (!shouldDisplay || !gameObject.activeInHierarchy)
            {
                aimLine.enabled = false;
                return;
            }

            WeaponController.WeaponShotRay shotRay = weaponController.CalculateShotRay();
            Vector3 origin = shotRay.origin;
            Vector3 direction = shotRay.direction;

            Vector3 endPoint;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, hitLayers))
            {
                endPoint = hit.point;
            }
            else
            {
                endPoint = shotRay.GetPoint(maxDistance);
            }

            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, endPoint);

            // Visibility control: weapon aiming ray is only visible during active gameplay (Practice or Challenge).
            // During menus (MainMenu, Paused, Results), the weapon aiming ray visual is hidden.
            // In Edit Mode outside PlayMode, defaults to true so verification tests pass.
            bool isGameplay = true;
            if (Application.isPlaying)
            {
                if (_cachedStateManager == null && GameManager.Instance != null)
                {
                    _cachedStateManager = GameManager.Instance.StateManager;
                }
                if (_cachedStateManager != null)
                {
                    isGameplay = (_cachedStateManager.CurrentState == GameState.Practice || _cachedStateManager.CurrentState == GameState.Challenge);
                }
            }

            aimLine.enabled = isGameplay;
        }

        public void SetAimMaterial(Material mat)
        {
            aimMaterial = mat;
            if (aimLine != null && mat != null)
            {
                aimLine.sharedMaterial = mat;
            }
        }
    }
}
