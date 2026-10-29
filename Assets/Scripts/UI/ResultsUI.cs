using UnityEngine;
using UnityEngine.UI;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;
using VRShootingGallery.Scoring;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Displays post-challenge summary telemetry satisfying FR-08.
    /// Shows Final Score, Accuracy %, Shots Fired, Hits, Best Combo, and Difficulty.
    /// </summary>
    public class ResultsUI : MonoBehaviour
    {
        [Header("Telemetry Labels")]
        [SerializeField] private Text finalScoreText;
        [SerializeField] private Text accuracyText;
        [SerializeField] private Text shotsFiredText;
        [SerializeField] private Text shotsHitText;
        [SerializeField] private Text bestComboText;
        [SerializeField] private Text difficultyText;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Dependencies")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultyManager difficultyManager;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (scoreManager == null) scoreManager = FindObjectOfType<ScoreManager>();
            if (difficultyManager == null) difficultyManager = FindObjectOfType<DifficultyManager>();

            BindButtons();
        }

        private void BindButtons()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
        }

        /// <summary>
        /// Populates all results labels with final round metrics.
        /// </summary>
        public void DisplayResults()
        {
            if (scoreManager != null)
            {
                if (finalScoreText != null) finalScoreText.text = $"FINAL SCORE: {scoreManager.Score}";
                if (accuracyText != null) accuracyText.text = $"ACCURACY: {scoreManager.Accuracy:0.0}%";
                if (shotsFiredText != null) shotsFiredText.text = $"SHOTS FIRED: {scoreManager.ShotsFired}";
                if (shotsHitText != null) shotsHitText.text = $"HITS: {scoreManager.ShotsHit}";
                if (bestComboText != null) bestComboText.text = $"BEST COMBO: x{scoreManager.BestCombo}";
            }

            if (difficultyManager != null && difficultyText != null)
            {
                difficultyText.text = $"DIFFICULTY: {difficultyManager.CurrentLevel}";
            }
        }

        private void OnRestartClicked()
        {
            if (gameManager != null)
            {
                gameManager.RestartCurrentMode();
            }
        }

        private void OnMainMenuClicked()
        {
            if (gameManager != null)
            {
                gameManager.ReturnToMainMenu();
            }
        }
    }
}
