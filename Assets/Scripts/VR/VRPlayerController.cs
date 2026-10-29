using UnityEngine;
using UnityEngine.XR;
using VRShootingGallery.Core;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.VR
{
    /// <summary>
    /// Handles VR controller inputs for weapon reloading, switching, and pause menu.
    /// Operates cleanly alongside PCPlayerController without conflicts.
    /// Strictly active only when a real VR device is running via XRRuntimeDetector.
    /// </summary>
    public class VRPlayerController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private VRControllerPreference controllerPreference;

        private bool _prevSecondary;
        private bool _prevPrimary;
        private bool _prevMenuOrStick;
        private bool _prevTrigger;

        private void Awake()
        {
            if (weaponManager == null) weaponManager = FindObjectOfType<WeaponManager>();
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (controllerPreference == null) controllerPreference = FindObjectOfType<VRControllerPreference>();
        }

        private void Update()
        {
            // Only process VR controller inputs if an XR headset is actively running
            if (!XRRuntimeDetector.IsXRActive) return;

            GameState currentState = (gameManager != null && gameManager.StateManager != null)
                ? gameManager.StateManager.CurrentState
                : GameState.MainMenu;

            VRActiveHand currentHand = (controllerPreference != null)
                ? controllerPreference.ActiveHand
                : VRControllerPreference.CurrentHand;

            XRNode activeNode = (currentHand == VRActiveHand.Right) ? XRNode.RightHand : XRNode.LeftHand;
            InputDevice activeDevice = InputDevices.GetDeviceAtXRNode(activeNode);

            if (!activeDevice.isValid) return;

            // Handle Pause from active controller (Menu button or Thumbstick click)
            bool pauseInput = false;
            if (activeDevice.TryGetFeatureValue(CommonUsages.menuButton, out bool menuBtn) && menuBtn)
            {
                pauseInput = true;
            }
            else if (activeDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClick) && stickClick)
            {
                pauseInput = true;
            }

            if (pauseInput && !_prevMenuOrStick)
            {
                if (currentState == GameState.Practice || currentState == GameState.Challenge)
                {
                    if (gameManager != null)
                    {
                        gameManager.PauseGame();
                    }
                }
            }
            _prevMenuOrStick = pauseInput;

            // Active gameplay inputs: only from the active controller
            if (currentState == GameState.Practice || currentState == GameState.Challenge)
            {
                // Trigger Button or Axis: Fire weapon
                bool triggerPressed = false;
                if (activeDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerBtn) && triggerBtn)
                {
                    triggerPressed = true;
                }
                else if (activeDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal) && triggerVal > 0.5f)
                {
                    triggerPressed = true;
                }

                if (triggerPressed && !_prevTrigger)
                {
                    if (weaponManager != null && weaponManager.ActiveWeapon != null)
                    {
                        if (!weaponManager.ActiveWeapon.IsGrabbed)
                        {
                            weaponManager.ActiveWeapon.Fire();
                        }
                    }
                }
                _prevTrigger = triggerPressed;

                // Secondary Button (B on Right, Y on Left): Reload
                bool reloadPressed = false;
                if (activeDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary) && secondary)
                {
                    reloadPressed = true;
                }

                if (reloadPressed && !_prevSecondary)
                {
                    if (weaponManager != null && weaponManager.ActiveWeapon != null)
                    {
                        weaponManager.ActiveWeapon.Reload();
                    }
                }
                _prevSecondary = reloadPressed;

                // Primary Button (A on Right, X on Left): Toggle weapon
                bool switchPressed = false;
                if (activeDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) && primary)
                {
                    switchPressed = true;
                }

                if (switchPressed && !_prevPrimary)
                {
                    if (weaponManager != null)
                    {
                        weaponManager.ToggleWeapon();
                    }
                }
                _prevPrimary = switchPressed;
            }
        }
    }
}
