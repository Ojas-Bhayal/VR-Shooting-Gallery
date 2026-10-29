using UnityEngine;

namespace VRShootingGallery.Difficulty
{
    /// <summary>
    /// ScriptableObject defining tuning parameters for a specific difficulty level.
    /// Follows serialized configuration pattern to avoid hard-coded values.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyData_", menuName = "VR Shooting Gallery/Difficulty Data")]
    public class DifficultyData : ScriptableObject
    {
        [Header("Difficulty Identification")]
        [SerializeField] private DifficultyLevel level = DifficultyLevel.Medium;
        [SerializeField] private string displayName = "Medium";

        [Header("Target Scaling")]
        [Tooltip("Scale multiplier applied to spawned targets.")]
        [SerializeField] private float targetScale = 1.0f;

        [Header("Target Movement")]
        [Tooltip("Movement speed for moving targets (units/sec).")]
        [SerializeField] private float targetMoveSpeed = 2.0f;

        [Header("Popup Target Timing")]
        [Tooltip("How long popup targets stay visible before lowering (seconds).")]
        [SerializeField] private float popupDuration = 3.0f;

        [Header("Spawning Parameters")]
        [Tooltip("Time interval between spawning targets (seconds).")]
        [SerializeField] private float spawnInterval = 2.5f;

        [Tooltip("Maximum number of concurrently active targets in the range.")]
        [SerializeField] private int maxActiveTargets = 15;

        [Header("Scoring")]
        [Tooltip("Optional score multiplier for this difficulty setting.")]
        [SerializeField] private float scoreMultiplier = 1.0f;

        // Public getters for read-only access
        public DifficultyLevel Level => level;
        public string DisplayName => displayName;
        public float TargetScale => targetScale;
        public float TargetMoveSpeed => targetMoveSpeed;
        public float PopupDuration => popupDuration;
        public float SpawnInterval => spawnInterval;
        public int MaxActiveTargets => maxActiveTargets;
        public float ScoreMultiplier => scoreMultiplier;

        /// <summary>
        /// Initializes runtime data properties (useful for fallbacks and tests).
        /// </summary>
        public void Initialize(DifficultyLevel newLevel, string newDisplayName, float newTargetScale, float newTargetMoveSpeed, float newPopupDuration, float newSpawnInterval, int newMaxActiveTargets, float newScoreMultiplier)
        {
            level = newLevel;
            displayName = newDisplayName;
            targetScale = newTargetScale;
            targetMoveSpeed = newTargetMoveSpeed;
            popupDuration = newPopupDuration;
            spawnInterval = newSpawnInterval;
            maxActiveTargets = newMaxActiveTargets;
            scoreMultiplier = newScoreMultiplier;
        }
    }
}
