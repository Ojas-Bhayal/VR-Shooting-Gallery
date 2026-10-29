using System;
using UnityEngine;

namespace VRShootingGallery.Core
{
    /// <summary>
    /// Manages state transitions and notifies subscribed systems.
    /// Cleanly separates state management from high-level flow logic.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        [Header("State Configuration")]
        [SerializeField] private GameState initialState = GameState.MainMenu;

        private GameState _currentState;
        private GameState _previousState;

        public event Action<GameState, GameState> OnGameStateChanged; // (newState, previousState)

        public GameState CurrentState => _currentState;
        public GameState PreviousState => _previousState;

        private void Awake()
        {
            _currentState = initialState;
            _previousState = initialState;
        }

        /// <summary>
        /// Transitions to a new game state and triggers state change notifications.
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (_currentState == newState) return;

            _previousState = _currentState;
            _currentState = newState;

            OnGameStateChanged?.Invoke(_currentState, _previousState);
        }
    }
}
