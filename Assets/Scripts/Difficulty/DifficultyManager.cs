using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Difficulty
{
    /// <summary>
    /// Manages active difficulty settings and notifies subscribers upon change.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private DifficultyLevel defaultDifficulty = DifficultyLevel.Medium;
        [SerializeField] private List<DifficultyData> availableDifficulties = new List<DifficultyData>();

        private DifficultyData _currentDifficultyData;

        public event Action<DifficultyData> OnDifficultyChanged;

        public DifficultyData CurrentDifficulty => _currentDifficultyData;
        public DifficultyLevel CurrentLevel => _currentDifficultyData != null ? _currentDifficultyData.Level : defaultDifficulty;

        private void Awake()
        {
            InitializeDifficulty();
        }

        private void InitializeDifficulty()
        {
            SetDifficulty(defaultDifficulty);
        }

        /// <summary>
        /// Sets the difficulty to the requested level.
        /// </summary>
        public void SetDifficulty(DifficultyLevel level)
        {
            DifficultyData match = availableDifficulties.Find(d => d != null && d.Level == level);

            if (match != null)
            {
                _currentDifficultyData = match;
            }
            else
            {
                // Fallback runtime configuration if no ScriptableObject asset was assigned
                _currentDifficultyData = CreateFallbackDifficulty(level);
            }

            OnDifficultyChanged?.Invoke(_currentDifficultyData);
        }

        /// <summary>
        /// Generates sensible fallback values based on FR-07 if ScriptableObject assets are missing.
        /// </summary>
        private DifficultyData CreateFallbackDifficulty(DifficultyLevel level)
        {
            var fallback = ScriptableObject.CreateInstance<DifficultyData>();
            
            // Sensible fallback configuration if ScriptableObject assets are missing:
            // Easy: Large targets, slow movement, long popup, lower spawn rate, max 5 active
            // Medium: Medium targets, moderate movement, moderate popup, moderate spawn rate, max 10 active
            // Hard: Small targets, fast movement, short popup, higher spawn rate, max 15 active
            switch (level)
            {
                case DifficultyLevel.Easy:
                    fallback.Initialize(DifficultyLevel.Easy, "Easy", 1.3f, 1.2f, 4.0f, 3.0f, 5, 1.0f);
                    break;
                case DifficultyLevel.Hard:
                    fallback.Initialize(DifficultyLevel.Hard, "Hard", 0.75f, 3.5f, 1.8f, 1.5f, 15, 1.5f);
                    break;
                case DifficultyLevel.Medium:
                default:
                    fallback.Initialize(DifficultyLevel.Medium, "Medium", 1.0f, 2.0f, 3.0f, 2.2f, 10, 1.25f);
                    break;
            }
            return fallback;
        }

        /// <summary>
        /// Retrieves the DifficultyData matching the given level.
        /// </summary>
        public DifficultyData GetDifficultyData(DifficultyLevel level)
        {
            return availableDifficulties.Find(d => d != null && d.Level == level) ?? _currentDifficultyData;
        }
    }
}
