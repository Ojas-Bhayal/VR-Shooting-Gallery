using System;
using UnityEngine;
using VRShootingGallery.Difficulty;
using VRShootingGallery.Scoring;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.Core
{
    /// <summary>
    /// Coordinates high-level game flow, mode execution, and the 60-second challenge timer.
    /// Acts as the central coordinator without holding specific weapon or target logic.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Sub-Managers")]
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultyManager difficultyManager;

        [Header("Challenge Settings")]
        [Tooltip("Standard challenge duration in seconds (FR-03 requires exactly 60 seconds).")]
        [SerializeField] private float challengeDuration = 60.0f;

        // Runtime mode and timer state
        private GameMode _currentMode = GameMode.None;
        private float _timeRemaining;
        private bool _isTimerRunning;

        // Events
        public event Action<GameMode> OnModeStarted;
        public event Action<GameMode> OnModeEnded;
        public event Action<float> OnTimerTick;                 // Remaining seconds
        public event Action OnChallengeTimeExpired;

        // Public getters
        public GameMode CurrentMode => _currentMode;
        public float TimeRemaining => _timeRemaining;
        public float ChallengeDuration => challengeDuration;
        public bool IsTimerRunning => _isTimerRunning;

        public GameStateManager StateManager => gameStateManager;
        public ScoreManager ScoreManager => scoreManager;
        public DifficultyManager DifficultyManager => difficultyManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Time.timeScale = 1.0f;
            ResolveManagerDependencies();
        }

        private void Start()
        {
            Time.timeScale = 1.0f;
            if (gameStateManager != null && gameStateManager.CurrentState != GameState.MainMenu)
            {
                gameStateManager.ChangeState(GameState.MainMenu);
            }
        }

        private void ResolveManagerDependencies()
        {
            if (gameStateManager == null) gameStateManager = GetComponent<GameStateManager>() ?? FindObjectOfType<GameStateManager>();
            if (scoreManager == null) scoreManager = GetComponent<ScoreManager>() ?? FindObjectOfType<ScoreManager>();
            if (difficultyManager == null) difficultyManager = GetComponent<DifficultyManager>() ?? FindObjectOfType<DifficultyManager>();
        }

        private void Update()
        {
            if (_isTimerRunning && _currentMode == GameMode.Challenge)
            {
                UpdateChallengeTimer();
            }
        }

        private void UpdateChallengeTimer()
        {
            _timeRemaining -= Time.deltaTime;

            if (_timeRemaining <= 0.0f)
            {
                _timeRemaining = 0.0f;
                _isTimerRunning = false;
                OnTimerTick?.Invoke(_timeRemaining);
                EndChallenge();
            }
            else
            {
                OnTimerTick?.Invoke(_timeRemaining);
            }
        }

        /// <summary>
        /// Starts Practice mode (untimed, free shooting, score tracked).
        /// </summary>
        public void StartPractice()
        {
            Time.timeScale = 1.0f;
            _currentMode = GameMode.Practice;
            _isTimerRunning = false;
            _timeRemaining = 0.0f;

            if (scoreManager != null)
            {
                scoreManager.ResetStats();
            }

            var weaponManager = FindObjectOfType<WeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.ResetAllWeaponsAmmo();
            }

            if (gameStateManager != null)
            {
                gameStateManager.ChangeState(GameState.Practice);
            }

            OnModeStarted?.Invoke(_currentMode);
        }

        /// <summary>
        /// Starts 60-second Challenge mode.
        /// </summary>
        public void StartChallenge()
        {
            Time.timeScale = 1.0f;
            _currentMode = GameMode.Challenge;
            _timeRemaining = challengeDuration;
            _isTimerRunning = true;

            if (scoreManager != null)
            {
                scoreManager.ResetStats();
            }

            var weaponManager = FindObjectOfType<WeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.ResetAllWeaponsAmmo();
            }

            if (gameStateManager != null)
            {
                gameStateManager.ChangeState(GameState.Challenge);
            }

            OnModeStarted?.Invoke(_currentMode);
            OnTimerTick?.Invoke(_timeRemaining);
        }

        /// <summary>
        /// Ends the current challenge and transitions to results state.
        /// </summary>
        public void EndChallenge()
        {
            _isTimerRunning = false;

            OnChallengeTimeExpired?.Invoke();
            OnModeEnded?.Invoke(_currentMode);

            if (gameStateManager != null)
            {
                gameStateManager.ChangeState(GameState.Results);
            }
        }

        /// <summary>
        /// Restarts the currently active mode with clean stats and full ammo.
        /// Always restores Time.timeScale before restarting.
        /// </summary>
        public void RestartCurrentMode()
        {
            Time.timeScale = 1.0f;

            var weaponManager = FindObjectOfType<WeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.ResetAllWeaponsAmmo();
            }

            if (_currentMode == GameMode.Challenge)
            {
                StartChallenge();
            }
            else if (_currentMode == GameMode.Practice)
            {
                StartPractice();
            }
            else
            {
                ReturnToMainMenu();
            }
        }

        /// <summary>
        /// Returns to Main Menu and halts active modes.
        /// Restores Time.timeScale = 1.0f.
        /// </summary>
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1.0f;
            _isTimerRunning = false;
            GameMode previousMode = _currentMode;
            _currentMode = GameMode.None;

            if (previousMode != GameMode.None)
            {
                OnModeEnded?.Invoke(previousMode);
            }

            // Halt target spawning and clear active targets
            var targetManager = FindObjectOfType<VRShootingGallery.Targets.TargetManager>();
            if (targetManager != null)
            {
                targetManager.StopSpawning();
                targetManager.ResetAllTargets();
            }

            if (gameStateManager != null)
            {
                gameStateManager.ChangeState(GameState.MainMenu);
            }
        }

        /// <summary>
        /// Pauses the game if actively in Practice or Challenge.
        /// Note: There is NO Escape-key resume behavior.
        /// </summary>
        public void PauseGame()
        {
            if (gameStateManager == null) return;

            if (gameStateManager.CurrentState == GameState.Practice || gameStateManager.CurrentState == GameState.Challenge)
            {
                Time.timeScale = 0.0f;
                gameStateManager.ChangeState(GameState.Paused);
            }
        }

        /// <summary>
        /// Single-direction pause method. Calling this while paused will DO NOTHING.
        /// </summary>
        public void TogglePause()
        {
            PauseGame();
        }
    }
}
