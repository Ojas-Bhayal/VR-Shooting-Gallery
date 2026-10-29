using UnityEngine;
using UnityEngine.UI;
using VRShootingGallery.Core;
using VRShootingGallery.Scoring;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Displays in-game HUD telemetry: Timer, Score, Accuracy, Combo, Ammo.
    /// Can be rendered as a VR World-Space Canvas or PC Screen-Space Overlay.
    /// Satisfies FR-06 and GAME_DESIGN.md HUD specifications.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("UI Text Displays")]
        [SerializeField] private Text timerText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text accuracyText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text ammoText;

        [Header("Dependencies")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private WeaponManager weaponManager;

        private WeaponAmmo _activeAmmo;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (scoreManager == null) scoreManager = FindObjectOfType<ScoreManager>();
            if (weaponManager == null) weaponManager = FindObjectOfType<WeaponManager>();
        }

        private void Start()
        {
            SubscribeToEvents();
            RefreshAllDisplays();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            if (gameManager != null)
            {
                gameManager.OnTimerTick += UpdateTimerDisplay;
                gameManager.OnModeStarted += HandleModeStarted;
            }

            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged += UpdateScoreDisplay;
                scoreManager.OnAccuracyChanged += UpdateAccuracyDisplay;
                scoreManager.OnComboChanged += UpdateComboDisplay;
                scoreManager.OnStatsReset += RefreshAllDisplays;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched += HandleWeaponSwitched;
                if (weaponManager.ActiveWeapon != null)
                {
                    HandleWeaponSwitched(weaponManager.ActiveWeapon);
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (gameManager != null)
            {
                gameManager.OnTimerTick -= UpdateTimerDisplay;
                gameManager.OnModeStarted -= HandleModeStarted;
            }

            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged -= UpdateScoreDisplay;
                scoreManager.OnAccuracyChanged -= UpdateAccuracyDisplay;
                scoreManager.OnComboChanged -= UpdateComboDisplay;
                scoreManager.OnStatsReset -= RefreshAllDisplays;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched -= HandleWeaponSwitched;
            }

            UnbindAmmoEvents();
        }

        private void HandleModeStarted(GameMode mode)
        {
            if (mode == GameMode.Practice)
            {
                if (timerText != null) timerText.text = "TIME: UNLIMITED";
            }
        }

        private void HandleWeaponSwitched(WeaponController weapon)
        {
            UnbindAmmoEvents();

            if (weapon != null && weapon.Ammo != null)
            {
                _activeAmmo = weapon.Ammo;
                _activeAmmo.OnAmmoChanged += UpdateAmmoDisplay;
                UpdateAmmoDisplay(_activeAmmo.CurrentAmmo, _activeAmmo.MagazineCapacity);
            }
        }

        private void UnbindAmmoEvents()
        {
            if (_activeAmmo != null)
            {
                _activeAmmo.OnAmmoChanged -= UpdateAmmoDisplay;
                _activeAmmo = null;
            }
        }

        public void UpdateTimerDisplay(float secondsRemaining)
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(secondsRemaining);
                timerText.text = $"TIME: {seconds:00}s";
            }
        }

        public void UpdateScoreDisplay(int currentScore, int addedPoints)
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {currentScore}";
            }
        }

        public void UpdateAccuracyDisplay(float accuracy)
        {
            if (accuracyText != null)
            {
                accuracyText.text = $"ACCURACY: {accuracy:0.0}%";
            }
        }

        public void UpdateComboDisplay(int combo, float multiplier)
        {
            if (comboText != null)
            {
                comboText.text = combo > 1 ? $"COMBO: x{combo} ({multiplier:0.00}x)" : "COMBO: --";
            }
        }

        public void UpdateAmmoDisplay(int current, int max)
        {
            if (ammoText != null)
            {
                ammoText.text = $"AMMO: {current} / {max}";
            }
        }

        public void RefreshAllDisplays()
        {
            if (scoreManager != null)
            {
                UpdateScoreDisplay(scoreManager.Score, 0);
                UpdateAccuracyDisplay(scoreManager.Accuracy);
                UpdateComboDisplay(scoreManager.CurrentCombo, scoreManager.CurrentMultiplier);
            }
            if (_activeAmmo != null)
            {
                UpdateAmmoDisplay(_activeAmmo.CurrentAmmo, _activeAmmo.MagazineCapacity);
            }
            if (gameManager != null && gameManager.CurrentMode == GameMode.Practice)
            {
                if (timerText != null) timerText.text = "TIME: UNLIMITED";
            }
        }
    }
}
