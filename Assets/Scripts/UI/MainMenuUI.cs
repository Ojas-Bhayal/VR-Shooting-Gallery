using UnityEngine;
using UnityEngine.UI;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;
using VRShootingGallery.VR;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Controls the Main Menu UI panel satisfying FR-01.
    /// Provides options to start Practice, start Challenge, toggle difficulty, and Quit.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button quitButton;

        [Header("Difficulty Buttons / Toggles")]
        [SerializeField] private Button easyDifficultyButton;
        [SerializeField] private Button mediumDifficultyButton;
        [SerializeField] private Button hardDifficultyButton;
        [SerializeField] private Text currentDifficultyText;

        [Header("VR Controller Selection")]
        [SerializeField] private Button rightControllerButton;
        [SerializeField] private Button leftControllerButton;
        [SerializeField] private Text controllerSelectionText;

        [Header("Dependencies")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField] private VRControllerPreference controllerPreference;

        public Button RightControllerButton => rightControllerButton;
        public Button LeftControllerButton => leftControllerButton;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (difficultyManager == null) difficultyManager = FindObjectOfType<DifficultyManager>();
            if (controllerPreference == null) controllerPreference = FindObjectOfType<VRControllerPreference>();

            BindButtonCallbacks();
        }

        private void Start()
        {
            UpdateDifficultyDisplay();
            UpdateControllerDisplay();
        }

        public void BindButtonCallbacks()
        {
            if (rightControllerButton != null)
            {
                rightControllerButton.onClick.RemoveAllListeners();
                rightControllerButton.onClick.AddListener(() => SelectController(VRActiveHand.Right));
            }

            if (leftControllerButton != null)
            {
                leftControllerButton.onClick.RemoveAllListeners();
                leftControllerButton.onClick.AddListener(() => SelectController(VRActiveHand.Left));
            }
            if (practiceButton != null)
            {
                practiceButton.onClick.AddListener(OnPracticeClicked);
            }

            if (challengeButton != null)
            {
                challengeButton.onClick.AddListener(OnChallengeClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            if (easyDifficultyButton != null)
            {
                easyDifficultyButton.onClick.AddListener(() => SelectDifficulty(DifficultyLevel.Easy));
            }

            if (mediumDifficultyButton != null)
            {
                mediumDifficultyButton.onClick.AddListener(() => SelectDifficulty(DifficultyLevel.Medium));
            }

            if (hardDifficultyButton != null)
            {
                hardDifficultyButton.onClick.AddListener(() => SelectDifficulty(DifficultyLevel.Hard));
            }
        }

        private void OnPracticeClicked()
        {
            if (gameManager != null)
            {
                gameManager.StartPractice();
            }
        }

        private void OnChallengeClicked()
        {
            if (gameManager != null)
            {
                gameManager.StartChallenge();
            }
        }

        private void SelectDifficulty(DifficultyLevel level)
        {
            if (difficultyManager != null)
            {
                difficultyManager.SetDifficulty(level);
                UpdateDifficultyDisplay();
            }
        }

        private void UpdateDifficultyDisplay()
        {
            if (currentDifficultyText != null && difficultyManager != null)
            {
                currentDifficultyText.text = $"DIFFICULTY: {difficultyManager.CurrentLevel}";
            }
        }

        public void SelectController(VRActiveHand hand)
        {
            if (controllerPreference != null)
            {
                controllerPreference.SetActiveHand(hand);
            }
            else if (VRControllerPreference.Instance != null)
            {
                VRControllerPreference.Instance.SetActiveHand(hand);
            }

            UpdateControllerDisplay();
        }

        public void UpdateControllerDisplay()
        {
            VRActiveHand currentHand = (controllerPreference != null)
                ? controllerPreference.ActiveHand
                : VRControllerPreference.CurrentHand;

            if (controllerSelectionText != null)
            {
                controllerSelectionText.text = $"VR CONTROLLER: {(currentHand == VRActiveHand.Right ? "RIGHT" : "LEFT")}";
            }

            Color activeColor = new Color(0.15f, 0.70f, 0.35f, 1f);   // Bright highlight green
            Color inactiveColor = new Color(0.25f, 0.28f, 0.35f, 1f); // Darker subdued gray

            if (rightControllerButton != null)
            {
                Image img = rightControllerButton.GetComponent<Image>();
                if (img != null) img.color = (currentHand == VRActiveHand.Right) ? activeColor : inactiveColor;
            }

            if (leftControllerButton != null)
            {
                Image img = leftControllerButton.GetComponent<Image>();
                if (img != null) img.color = (currentHand == VRActiveHand.Left) ? activeColor : inactiveColor;
            }
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
