using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Weapons
{
    /// <summary>
    /// Manages currently available weapons and handles switching for PC fallback or VR tables.
    /// Does not handle scoring logic.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Header("Available Weapons")]
        [SerializeField] private WeaponController pistol;
        [SerializeField] private WeaponController rifle;

        [Header("Initial Selection")]
        [SerializeField] private WeaponType defaultWeaponType = WeaponType.Pistol;

        private WeaponController _activeWeapon;
        private readonly List<WeaponController> _weapons = new List<WeaponController>();

        public event Action<WeaponController> OnWeaponSwitched;

        public WeaponController ActiveWeapon => _activeWeapon;
        public WeaponController Pistol => pistol;
        public WeaponController Rifle => rifle;

        private void Awake()
        {
            if (pistol != null) _weapons.Add(pistol);
            if (rifle != null) _weapons.Add(rifle);

            EquipWeapon(defaultWeaponType);
        }

        /// <summary>
        /// Equips the specified weapon type, activating its GameObject and deactivating others.
        /// </summary>
        public void EquipWeapon(WeaponType type)
        {
            WeaponController target = type == WeaponType.Pistol ? pistol : rifle;

            if (target == null && _weapons.Count > 0)
            {
                target = _weapons[0];
            }

            if (target == null) return;

            _activeWeapon = target;

            // Update active states
            if (pistol != null) pistol.gameObject.SetActive(pistol == _activeWeapon);
            if (rifle != null) rifle.gameObject.SetActive(rifle == _activeWeapon);

            OnWeaponSwitched?.Invoke(_activeWeapon);
        }

        /// <summary>
        /// Toggles between Pistol and Rifle.
        /// </summary>
        public void ToggleWeapon()
        {
            if (_activeWeapon == pistol && rifle != null)
            {
                EquipWeapon(WeaponType.Rifle);
            }
            else if (pistol != null)
            {
                EquipWeapon(WeaponType.Pistol);
            }
        }

        /// <summary>
        /// Instantly refills ammunition on all weapons.
        /// </summary>
        public void ResetAllWeaponsAmmo()
        {
            if (pistol != null && pistol.Ammo != null)
            {
                pistol.Ammo.InstantRefill();
            }
            if (rifle != null && rifle.Ammo != null)
            {
                rifle.Ammo.InstantRefill();
            }
        }
    }
}
