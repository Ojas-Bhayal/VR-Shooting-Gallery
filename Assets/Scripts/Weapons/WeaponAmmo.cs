using System;
using UnityEngine;

namespace VRShootingGallery.Weapons
{
    /// <summary>
    /// Manages magazine capacity, current ammunition, and reloading behavior.
    /// Modular component referenced by WeaponController.
    /// </summary>
    public class WeaponAmmo : MonoBehaviour
    {
        [Header("Ammunition Configuration")]
        [Tooltip("Maximum rounds held in a single magazine.")]
        [SerializeField] private int magazineCapacity = 12;

        [Tooltip("Time required to complete reload action in seconds.")]
        [SerializeField] private float reloadDuration = 1.2f;

        private int _currentAmmo;
        private bool _isReloading;

        public event Action<int, int> OnAmmoChanged; // (currentAmmo, magazineCapacity)
        public event Action OnOutOfAmmo;
        public event Action OnReloadStarted;
        public event Action OnReloadCompleted;

        public int CurrentAmmo => _currentAmmo;
        public int MagazineCapacity => magazineCapacity;
        public bool IsReloading => _isReloading;
        public bool IsFull => _currentAmmo >= magazineCapacity;

        private void Awake()
        {
            _currentAmmo = magazineCapacity;
        }

        private void Start()
        {
            OnAmmoChanged?.Invoke(_currentAmmo, magazineCapacity);
        }

        /// <summary>
        /// Checks if the weapon has rounds available and is not currently reloading.
        /// </summary>
        public bool CanFire()
        {
            return !_isReloading && _currentAmmo > 0;
        }

        /// <summary>
        /// Consumes one bullet from the current magazine.
        /// </summary>
        public bool ConsumeRound()
        {
            if (!CanFire())
            {
                if (_currentAmmo <= 0)
                {
                    OnOutOfAmmo?.Invoke();
                }
                return false;
            }

            _currentAmmo--;
            OnAmmoChanged?.Invoke(_currentAmmo, magazineCapacity);

            if (_currentAmmo <= 0)
            {
                OnOutOfAmmo?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Initiates the reload cycle.
        /// </summary>
        public void Reload()
        {
            if (_isReloading || IsFull) return;

            StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            _isReloading = true;
            OnReloadStarted?.Invoke();

            yield return new WaitForSeconds(reloadDuration);

            _currentAmmo = magazineCapacity;
            _isReloading = false;

            OnAmmoChanged?.Invoke(_currentAmmo, magazineCapacity);
            OnReloadCompleted?.Invoke();
        }

        /// <summary>
        /// Instantly fills the magazine to capacity.
        /// </summary>
        public void InstantRefill()
        {
            _isReloading = false;
            _currentAmmo = magazineCapacity;
            OnAmmoChanged?.Invoke(_currentAmmo, magazineCapacity);
        }
    }
}
