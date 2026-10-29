using UnityEngine;
using UnityEngine.UI;
using VRShootingGallery.Core;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Controls the centered Pause Menu UI.
    /// Provides RESTART and MAIN MENU actions.
    /// Exiting the pause state is only possible via these buttons (no Escape resume).
    /// </summary>
    public class PauseUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Dependencies")]
        [SerializeField] private GameManager gameManager;

        public Button RestartButton => restartButton;
        public Button MainMenuButton => mainMenuButton;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            BindButtonCallbacks();
        }

        private void BindButtonCallbacks()
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
