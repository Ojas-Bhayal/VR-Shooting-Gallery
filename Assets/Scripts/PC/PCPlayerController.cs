using UnityEngine;
using VRShootingGallery.Core;
using VRShootingGallery.VR;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.PC
{
    /// <summary>
    /// PC keyboard and mouse fallback controller satisfying FR-09.
    /// Provides WASD movement, mouse look, Left Click to fire, R to reload,
    /// 1/2 for weapon switching, and Escape for pause.
    /// Gameplay input and cursor lock are strictly controlled by GameState.
    /// Automatically disabled when a real VR device is active.
    /// </summary>
    public class PCPlayerController : MonoBehaviour
    {
        [Header("Movement & Camera")]
        [SerializeField] private float moveSpeed = 4.0f;
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private CharacterController characterController;

        [Header("Control Settings")]
        [Tooltip("If true, PC controls will run regardless of XR device detection.")]
        [SerializeField] private bool forceEnablePC = false;

        [Header("System References")]
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private GameManager gameManager;

        private float _verticalRotation = 0.0f;
        private bool _isPCActive;

        public bool ForceEnablePC
        {
            get => forceEnablePC;
            set
            {
                forceEnablePC = value;
                DetermineControlMode();
            }
        }

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (weaponManager == null) weaponManager = FindObjectOfType<WeaponManager>();
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

            DetermineControlMode();
        }

        private void Start()
        {
            if (gameManager != null && gameManager.StateManager != null)
            {
                gameManager.StateManager.OnGameStateChanged += HandleGameStateChanged;
                UpdateStateControls(gameManager.StateManager.CurrentState);
            }
            else
            {
                UpdateCursorState(false);
            }

            XRRuntimeDetector.OnXRModeChanged += HandleXRModeChanged;
        }

        private void OnDestroy()
        {
            if (gameManager != null && gameManager.StateManager != null)
            {
                gameManager.StateManager.OnGameStateChanged -= HandleGameStateChanged;
            }

            XRRuntimeDetector.OnXRModeChanged -= HandleXRModeChanged;
        }

        private void HandleXRModeChanged(bool isVR)
        {
            DetermineControlMode();
        }

        private void DetermineControlMode()
        {
            bool isXRActive = XRRuntimeDetector.IsXRActive;
            _isPCActive = forceEnablePC || !isXRActive;
            enabled = _isPCActive;

            if (!_isPCActive)
            {
                UpdateCursorState(false);
            }
            else if (gameManager != null && gameManager.StateManager != null)
            {
                UpdateStateControls(gameManager.StateManager.CurrentState);
            }
        }

        private void HandleGameStateChanged(GameState newState, GameState previousState)
        {
            UpdateStateControls(newState);
        }

        private void UpdateStateControls(GameState state)
        {
            bool isGameplay = state == GameState.Practice || state == GameState.Challenge;
            UpdateCursorState(isGameplay);
        }

        private void UpdateCursorState(bool lockCursor)
        {
            if (!_isPCActive) return;

            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        private void Update()
        {
            if (!_isPCActive) return;

            GameState currentState = (gameManager != null && gameManager.StateManager != null)
                ? gameManager.StateManager.CurrentState
                : GameState.MainMenu;

            // Only process gameplay inputs during active Practice or Challenge
            if (currentState == GameState.Practice || currentState == GameState.Challenge)
            {
                HandleLook();
                HandleMovement();
                HandleWeapons();
                HandleMenu();
            }
            // In Paused, MainMenu, Results: gameplay controls and Escape toggle are disabled
        }

        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Horizontal rotation on player body
            transform.Rotate(Vector3.up * mouseX);

            // Vertical rotation on camera
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -80.0f, 80.0f);

            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(_verticalRotation, 0.0f, 0.0f);
            }
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal"); // A/D
            float vertical = Input.GetAxis("Vertical");     // W/S

            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            Vector3 movement = moveDirection * (moveSpeed * Time.deltaTime);

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(movement);
            }
            else
            {
                transform.position += movement;
            }
        }

        private void HandleWeapons()
        {
            if (weaponManager == null || weaponManager.ActiveWeapon == null) return;

            // Left Mouse Button: Fire
            if (Input.GetMouseButtonDown(0))
            {
                weaponManager.ActiveWeapon.Fire();
            }

            // R: Reload
            if (Input.GetKeyDown(KeyCode.R))
            {
                weaponManager.ActiveWeapon.Reload();
            }

            // 1: Equip Pistol
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                weaponManager.EquipWeapon(WeaponType.Pistol);
            }

            // 2: Equip Rifle
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                weaponManager.EquipWeapon(WeaponType.Rifle);
            }
        }

        private void HandleMenu()
        {
            // Escape: Pause (only valid during active Practice or Challenge)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (gameManager != null)
                {
                    gameManager.PauseGame();
                }
            }
        }

        public void SetControlActive(bool active)
        {
            _isPCActive = active;
            enabled = active;
            UpdateCursorState(active);
        }
    }
}
