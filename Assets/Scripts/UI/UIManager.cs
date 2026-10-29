using UnityEngine;
using VRShootingGallery.Core;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Coordinates visibility of UI panels based on current GameState.
    /// Manages MainMenuUI, HUDController, ResultsUI, and Pause overlays.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("Controllers")]
        [SerializeField] private ResultsUI resultsUI;
        [SerializeField] private HUDController hudController;

        [Header("Dependencies")]
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private VRUIAdapter vrUIAdapter;

        private void Awake()
        {
            if (gameStateManager == null) gameStateManager = FindObjectOfType<GameStateManager>();
            if (vrUIAdapter == null) vrUIAdapter = GetComponent<VRUIAdapter>();
            if (resultsUI == null && resultsPanel != null) resultsUI = resultsPanel.GetComponent<ResultsUI>();
            if (hudController == null && hudPanel != null) hudController = hudPanel.GetComponent<HUDController>();
        }

        private void Start()
        {
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged += HandleGameStateChanged;
                UpdatePanelsForState(gameStateManager.CurrentState);
            }
            else
            {
                UpdatePanelsForState(GameState.MainMenu);
            }
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandleGameStateChanged(GameState newState, GameState previousState)
        {
            UpdatePanelsForState(newState);
        }

        public void UpdatePanelsForState(GameState state)
        {
            bool isMenu = state == GameState.MainMenu;
            bool isGameplay = state == GameState.Practice || state == GameState.Challenge;
            bool isResults = state == GameState.Results;
            bool isPaused = state == GameState.Paused;

            if (mainMenuPanel != null) mainMenuPanel.SetActive(isMenu);
            if (hudPanel != null) hudPanel.SetActive(isGameplay);
            if (resultsPanel != null)
            {
                resultsPanel.SetActive(isResults);
                if (isResults && resultsUI != null)
                {
                    resultsUI.DisplayResults();
                }
            }
            if (pausePanel != null) pausePanel.SetActive(isPaused);
            if (isMenu || isPaused || isResults)
            {
                if (vrUIAdapter != null) vrUIAdapter.PositionInFrontOfHeadset();
            }
        }
    }
}
