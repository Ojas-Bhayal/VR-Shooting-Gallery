using System;
using UnityEngine;

namespace VRShootingGallery.Scoring
{
    /// <summary>
    /// Tracks shots, hits, accuracy, score, and combo multipliers.
    /// Fully satisfies FR-06 and GAME_DESIGN.md scoring guidelines.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [Header("Combo Multipliers")]
        [Tooltip("Multiplier applied when combo is between 1 and 4.")]
        [SerializeField] private float tier1Multiplier = 1.0f;

        [Tooltip("Multiplier applied when combo is between 5 and 9.")]
        [SerializeField] private float tier2Multiplier = 1.25f;

        [Tooltip("Multiplier applied when combo is 10 or greater.")]
        [SerializeField] private float tier3Multiplier = 1.5f;

        // Internal tracking state
        private int _score;
        private int _shotsFired;
        private int _shotsHit;
        private int _currentCombo;
        private int _bestCombo;

        // Events for UI and external feedback
        public event Action<int, int> OnScoreChanged;               // (newScore, addedPoints)
        public event Action<float> OnAccuracyChanged;               // (accuracyPercentage)
        public event Action<int, float> OnComboChanged;             // (currentCombo, currentMultiplier)
        public event Action<int, int> OnShotRecorded;               // (shotsFired, shotsHit)
        public event Action OnStatsReset;

        // Public properties
        public int Score => _score;
        public int ShotsFired => _shotsFired;
        public int ShotsHit => _shotsHit;
        public int CurrentCombo => _currentCombo;
        public int BestCombo => _bestCombo;

        /// <summary>
        /// Accuracy calculated according to FR-06:
        /// accuracy = shotsHit / shotsFired * 100.
        /// If shotsFired is zero, accuracy is 0%.
        /// </summary>
        public float Accuracy
        {
            get
            {
                if (_shotsFired <= 0) return 0.0f;
                return ((float)_shotsHit / _shotsFired) * 100.0f;
            }
        }

        public float CurrentMultiplier => CalculateMultiplier(_currentCombo);

        /// <summary>
        /// Records that a shot was fired.
        /// Called each time a weapon discharges.
        /// </summary>
        public void RegisterShot()
        {
            _shotsFired++;
            OnShotRecorded?.Invoke(_shotsFired, _shotsHit);
            OnAccuracyChanged?.Invoke(Accuracy);
        }

        /// <summary>
        /// Records a successful hit on a target and calculates combo and score additions.
        /// </summary>
        /// <param name="hitType">The zone/type of hit scored.</param>
        /// <param name="customPoints">Optional override for point value.</param>
        public void RegisterHit(HitType hitType, int customPoints = 0)
        {
            if (hitType == HitType.Miss)
            {
                RegisterMiss();
                return;
            }

            _shotsHit++;
            _currentCombo++;
            if (_currentCombo > _bestCombo)
            {
                _bestCombo = _currentCombo;
            }

            float multiplier = CalculateMultiplier(_currentCombo);
            int basePoints = customPoints > 0 ? customPoints : (int)hitType;
            int pointsEarned = Mathf.RoundToInt(basePoints * multiplier);

            _score += pointsEarned;

            OnScoreChanged?.Invoke(_score, pointsEarned);
            OnComboChanged?.Invoke(_currentCombo, multiplier);
            OnShotRecorded?.Invoke(_shotsFired, _shotsHit);
            OnAccuracyChanged?.Invoke(Accuracy);
        }

        /// <summary>
        /// Records a miss, resetting the combo streak.
        /// </summary>
        public void RegisterMiss()
        {
            if (_currentCombo > 0)
            {
                _currentCombo = 0;
                OnComboChanged?.Invoke(_currentCombo, 1.0f);
            }
            OnAccuracyChanged?.Invoke(Accuracy);
        }

        /// <summary>
        /// Resets all scoring metrics to zero.
        /// </summary>
        public void ResetStats()
        {
            _score = 0;
            _shotsFired = 0;
            _shotsHit = 0;
            _currentCombo = 0;
            _bestCombo = 0;

            OnStatsReset?.Invoke();
            OnScoreChanged?.Invoke(0, 0);
            OnAccuracyChanged?.Invoke(0.0f);
            OnComboChanged?.Invoke(0, 1.0f);
            OnShotRecorded?.Invoke(0, 0);
        }

        private float CalculateMultiplier(int combo)
        {
            if (combo >= 10) return tier3Multiplier;
            if (combo >= 5) return tier2Multiplier;
            return tier1Multiplier;
        }
    }
}
