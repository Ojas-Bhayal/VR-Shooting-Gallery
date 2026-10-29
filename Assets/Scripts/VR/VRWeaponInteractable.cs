using UnityEngine;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.VR
{
    /// <summary>
    /// Connects VR controller events (from XRGrabInteractable or custom VR input) to WeaponController.
    /// Exposes UnityEvent target methods for clean Inspector wiring without hard package coupling.
    /// </summary>
    [RequireComponent(typeof(WeaponController))]
    public class VRWeaponInteractable : MonoBehaviour
    {
        [Header("Weapon Reference")]
        [SerializeField] private WeaponController weaponController;

        [Header("Haptic Feedback (Optional)")]
        [SerializeField] private bool enableHaptics = true;
        [SerializeField] private float hapticAmplitude = 0.5f;
        [SerializeField] private float hapticDuration = 0.1f;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;

        private void Awake()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
            }

            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (_grabInteractable == null)
            {
                _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            }

            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.AddListener(HandleSelectEntered);
                _grabInteractable.selectExited.AddListener(HandleSelectExited);
                _grabInteractable.activated.AddListener(HandleActivated);
            }
        }

        private void OnDisable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                _grabInteractable.selectExited.RemoveListener(HandleSelectExited);
                _grabInteractable.activated.RemoveListener(HandleActivated);
            }
        }

        private void HandleSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            if (VRControllerPreference.Instance != null && !VRControllerPreference.Instance.IsInteractorAllowed(args.interactorObject))
            {
                return;
            }
            OnVRGrabbed();
        }

        private void HandleSelectExited(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args) => OnVRReleased();

        private void HandleActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs args)
        {
            if (VRControllerPreference.Instance != null && !VRControllerPreference.Instance.IsInteractorAllowed(args.interactorObject))
            {
                return;
            }
            OnVRTriggerFired();
        }

        /// <summary>
        /// Call this from XRGrabInteractable 'activated' event (trigger pull).
        /// </summary>
        public void OnVRTriggerFired()
        {
            if (weaponController != null)
            {
                weaponController.Fire();
            }
        }

        /// <summary>
        /// Call this from XRGrabInteractable 'selectEntered' event (grip grab).
        /// </summary>
        public void OnVRGrabbed()
        {
            if (weaponController != null)
            {
                weaponController.OnGrabbed();
            }
        }

        /// <summary>
        /// Call this from XRGrabInteractable 'selectExited' event (grip release).
        /// </summary>
        public void OnVRReleased()
        {
            if (weaponController != null)
            {
                weaponController.OnReleased();
            }
        }

        /// <summary>
        /// Call this from secondary controller button or magazine proximity socket.
        /// </summary>
        public void OnVRReload()
        {
            if (weaponController != null)
            {
                weaponController.Reload();
            }
        }
    }
}
