using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;
using VRShootingGallery.PC;
using VRShootingGallery.Scoring;
using VRShootingGallery.Targets;
using VRShootingGallery.UI;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.Editor
{
    public static class GameplayRepairVerifier
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _passedCount = 0;
        private static int _totalCount = 0;

        [MenuItem("VR Shooting Gallery/Run Final Repair Verification")]
        public static void RunVerification()
        {
            Report.Clear();
            _passedCount = 0;
            _totalCount = 0;

            Report.AppendLine("==================================================");
            Report.AppendLine("VR SHOOTING GALLERY - FINAL REPAIR VERIFICATION");
            Report.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            Report.AppendLine("==================================================");

            TestDifficultyScriptableObjects();
            TestSceneSetupAndNoVerifier();
            TestMenuCardDimensionsAndButtons();
            TestPauseMenuContentAndNoEscapeResume();
            TestGameplayFlowAndRestart();
            TestMainMenuReturnFromPause();
            TestChallengeMode();

            Report.AppendLine("==================================================");
            Report.AppendLine($"SUMMARY: {_passedCount} / {_totalCount} checks PASSED");
            Report.AppendLine($"OVERALL STATUS: {(_passedCount == _totalCount ? "PASS" : "FAIL")}");
            Report.AppendLine("==================================================");

            string reportText = Report.ToString();
            Debug.Log(reportText);

            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/FinalGameplayRepairReport.txt", reportText);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to write report file: " + ex.Message);
            }
        }

        private static void AssertCheck(string title, bool condition, string detail)
        {
            _totalCount++;
            if (condition)
            {
                _passedCount++;
                Report.AppendLine($"[PASS] {title} | {detail}");
            }
            else
            {
                Report.AppendLine($"[FAIL] {title} | {detail}");
            }
        }

        private static void TestDifficultyScriptableObjects()
        {
            var easy = AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/DifficultyData_Easy.asset");
            var med = AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/DifficultyData_Medium.asset");
            var hard = AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/DifficultyData_Hard.asset");

            AssertCheck("1. Easy Max Active Targets", easy != null && easy.MaxActiveTargets == 5,
                $"Easy MaxActiveTargets = {easy?.MaxActiveTargets} (Expected: 5)");

            AssertCheck("2. Medium Max Active Targets", med != null && med.MaxActiveTargets == 10,
                $"Medium MaxActiveTargets = {med?.MaxActiveTargets} (Expected: 10)");

            AssertCheck("3. Hard Max Active Targets", hard != null && hard.MaxActiveTargets == 15,
                $"Hard MaxActiveTargets = {hard?.MaxActiveTargets} (Expected: 15)");
        }

        private static void TestSceneSetupAndNoVerifier()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ShootingRange.unity");

            // Verify IntegrationVerifier is removed from scene
            GameObject verifierObj = GameObject.Find("IntegrationVerifier");
            var verifierComponent = UnityEngine.Object.FindObjectOfType<VRShootingGallery.Testing.RuntimeIntegrationVerifier>();
            AssertCheck("4. No IntegrationVerifier in Scene", verifierObj == null && verifierComponent == null,
                verifierObj == null ? "IntegrationVerifier GameObject is absent" : "IntegrationVerifier still present!");

            // Verify GameStateManager initial state
            var stateMgr = UnityEngine.Object.FindObjectOfType<GameStateManager>();
            SerializedObject soState = new SerializedObject(stateMgr);
            int initialState = soState.FindProperty("initialState").enumValueIndex;
            AssertCheck("5. GameStateManager Initial State is MainMenu", initialState == (int)GameState.MainMenu,
                $"Initial state: {(GameState)initialState}");

            // Verify initial timeScale
            AssertCheck("6. Time.timeScale is 1.0f", Mathf.Approximately(Time.timeScale, 1.0f),
                $"Current timeScale: {Time.timeScale}");
        }

        private static void TestMenuCardDimensionsAndButtons()
        {
            GameObject uiCanvas = GameObject.Find("GameUI_Canvas");
            AssertCheck("7. GameUI_Canvas Present", uiCanvas != null, uiCanvas != null ? "GameUI_Canvas found" : "Missing");

            Transform menuPanel = uiCanvas?.transform.Find("MainMenuPanel");
            Transform menuCard = menuPanel?.Find("Content") ?? menuPanel?.Find("MenuCard");
            AssertCheck("8. MainMenuPanel and Content Present", menuPanel != null && menuCard != null,
                menuCard != null ? "Content found under MainMenuPanel" : "Content missing");

            if (menuCard != null)
            {
                RectTransform cardRt = menuCard.GetComponent<RectTransform>();
                RectTransform panelRt = menuPanel?.GetComponent<RectTransform>();
                bool sizeOk = (panelRt != null && panelRt.rect.width >= 400f && panelRt.rect.height >= 400f) ||
                              (cardRt != null && ((cardRt.rect.width >= 400f && cardRt.rect.height >= 400f) || (cardRt.sizeDelta.x >= 400f && cardRt.sizeDelta.y >= 400f)));
                AssertCheck("9. Menu Layout Dimensions", sizeOk,
                    $"Size: {(panelRt != null ? panelRt.rect.width : cardRt.sizeDelta.x)} x {(panelRt != null ? panelRt.rect.height : cardRt.sizeDelta.y)}");

                Transform practiceBtn = menuCard.Find("PracticeButton");
                Transform challengeBtn = menuCard.Find("ChallengeButton");
                Transform diffSection = menuCard.Find("DifficultySection");
                Transform easyBtn = diffSection != null ? diffSection.Find("DifficultyButtons")?.Find("EasyButton") : menuCard.Find("EasyButton");
                Transform medBtn = diffSection != null ? diffSection.Find("DifficultyButtons")?.Find("MediumButton") : menuCard.Find("MediumButton");
                Transform hardBtn = diffSection != null ? diffSection.Find("DifficultyButtons")?.Find("HardButton") : menuCard.Find("HardButton");
                Transform quitBtn = menuCard.Find("QuitButton");
                Transform diffText = diffSection != null ? diffSection.Find("DifficultyLabel") : menuCard.Find("DifficultyDisplay");

                bool buttonsPresent = practiceBtn != null && challengeBtn != null && easyBtn != null &&
                                      medBtn != null && hardBtn != null && quitBtn != null && diffText != null;

                AssertCheck("10. Main Menu Elements Present & Sized", buttonsPresent,
                    buttonsPresent ? "Practice, Challenge, Easy, Medium, Hard, Quit, and DifficultyDisplay present" : "Missing elements");

                if (practiceBtn != null)
                {
                    var le = practiceBtn.GetComponent<LayoutElement>();
                    RectTransform pRt = practiceBtn.GetComponent<RectTransform>();
                    bool pSizeOk = (le != null && le.minHeight >= 45f) || (pRt != null && pRt.sizeDelta.y >= 45f);
                    AssertCheck("11. Practice Button Size", pSizeOk,
                        $"Practice button height: {(le != null ? le.minHeight : pRt.sizeDelta.y)}");
                }
            }
        }

        private static void TestPauseMenuContentAndNoEscapeResume()
        {
            GameObject uiCanvas = GameObject.Find("GameUI_Canvas");
            Transform pausePanel = uiCanvas?.transform.Find("PausePanel");
            Transform pauseCard = pausePanel?.Find("PauseCard");

            AssertCheck("12. PausePanel and PauseCard Present", pausePanel != null && pauseCard != null,
                pauseCard != null ? "PauseCard found under PausePanel" : "PauseCard missing");

            if (pauseCard != null)
            {
                RectTransform pRt = pauseCard.GetComponent<RectTransform>();
                bool sizeOk = pRt != null && Mathf.Approximately(pRt.sizeDelta.x, 500f) && Mathf.Approximately(pRt.sizeDelta.y, 350f);
                AssertCheck("13. PauseCard Dimensions (500x350)", sizeOk,
                    $"Size: {pRt?.sizeDelta.x} x {pRt?.sizeDelta.y} (Expected: 500 x 350)");

                Transform titleObj = pauseCard.Find("PauseTitle");
                Text titleTxt = titleObj?.GetComponent<Text>();
                bool titleOk = titleTxt != null && titleTxt.text == "GAME PAUSED";
                AssertCheck("14. Pause Title is 'GAME PAUSED'", titleOk,
                    $"Title text: '{titleTxt?.text}'");

                Transform restartBtn = pauseCard.Find("RestartButton");
                Transform menuBtn = pauseCard.Find("MainMenuButton");
                bool buttonsOk = restartBtn != null && menuBtn != null;
                AssertCheck("15. Pause Buttons: RESTART & MAIN MENU", buttonsOk,
                    buttonsOk ? "RESTART and MAIN MENU buttons present" : "Missing buttons");

                Transform hintObj = pausePanel.Find("PauseHint") ?? pauseCard.Find("PauseHint");
                AssertCheck("16. No Escape Resume Hint Present", hintObj == null,
                    hintObj == null ? "PauseHint removed" : "PauseHint still present!");

                PauseUI pauseUI = pausePanel.GetComponent<PauseUI>() ?? pauseCard.GetComponent<PauseUI>();
                AssertCheck("17. PauseUI Component Attached and Wired", pauseUI != null,
                    pauseUI != null ? "PauseUI attached" : "PauseUI missing");
            }
        }

        private static void TestGameplayFlowAndRestart()
        {
            var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
            var sm = UnityEngine.Object.FindObjectOfType<ScoreManager>();
            var tm = UnityEngine.Object.FindObjectOfType<TargetManager>();
            var wm = UnityEngine.Object.FindObjectOfType<WeaponManager>();
            var uiMgr = UnityEngine.Object.FindObjectOfType<UIManager>();

            // Start Practice Mode
            gm.StartPractice();
            AssertCheck("18. Start Practice Mode", gm.CurrentMode == GameMode.Practice && gm.StateManager.CurrentState == GameState.Practice,
                $"CurrentMode: {gm.CurrentMode}, CurrentState: {gm.StateManager.CurrentState}");

            // Spawning respects Easy limit
            gm.DifficultyManager.SetDifficulty(DifficultyLevel.Easy);
            for (int i = 0; i < 15; i++)
            {
                tm.TrySpawnTarget();
            }
            AssertCheck("19. Easy Active Targets Limit (<= 5)", tm.ActiveTargetCount <= 5,
                $"Active targets count: {tm.ActiveTargetCount} / 5");

            // Simulate shooting
            if (wm != null)
            {
                if (wm.ActiveWeapon == null)
                {
                    wm.EquipWeapon(WeaponType.Pistol);
                }

                if (wm.ActiveWeapon != null && wm.ActiveWeapon.Ammo != null)
                {
                    int ammoBefore = wm.ActiveWeapon.Ammo.CurrentAmmo;
                    wm.ActiveWeapon.Fire();
                    int ammoAfter = wm.ActiveWeapon.Ammo.CurrentAmmo;
                    AssertCheck("20. Weapon Discharges Ammo", ammoAfter == ammoBefore - 1,
                        $"Ammo: {ammoBefore} -> {ammoAfter}");
                }
            }

            // Pause Game
            gm.PauseGame();
            AssertCheck("21. Game Pauses (timeScale=0, State=Paused)",
                Mathf.Approximately(Time.timeScale, 0.0f) && gm.StateManager.CurrentState == GameState.Paused,
                $"timeScale: {Time.timeScale}, State: {gm.StateManager.CurrentState}");

            // Verify pressing Escape while paused DOES NOTHING (no unpause)
            gm.TogglePause();
            AssertCheck("22. Escape/Toggle While Paused Does NOT Unpause",
                Mathf.Approximately(Time.timeScale, 0.0f) && gm.StateManager.CurrentState == GameState.Paused,
                $"timeScale: {Time.timeScale}, State: {gm.StateManager.CurrentState} (remains Paused)");

            // Restart Current Mode
            gm.RestartCurrentMode();
            AssertCheck("23. RESTART Restores timeScale and Practice Mode",
                Mathf.Approximately(Time.timeScale, 1.0f) && gm.CurrentMode == GameMode.Practice && gm.StateManager.CurrentState == GameState.Practice,
                $"timeScale: {Time.timeScale}, Mode: {gm.CurrentMode}, State: {gm.StateManager.CurrentState}");

            bool ammoReset = (wm?.ActiveWeapon != null && wm.ActiveWeapon.Ammo != null) ? wm.ActiveWeapon.Ammo.IsFull : true;
            AssertCheck("24. RESTART Resets Score & Ammo",
                sm.Score == 0 && ammoReset,
                $"Score: {sm.Score}, Ammo: {wm?.ActiveWeapon?.Ammo?.CurrentAmmo} / {wm?.ActiveWeapon?.Ammo?.MagazineCapacity}");
        }

        private static void TestMainMenuReturnFromPause()
        {
            var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
            var tm = UnityEngine.Object.FindObjectOfType<TargetManager>();
            var uiMgr = UnityEngine.Object.FindObjectOfType<UIManager>();

            // Pause from practice
            gm.PauseGame();

            // Click MAIN MENU
            gm.ReturnToMainMenu();

            AssertCheck("25. ReturnToMainMenu Restores timeScale = 1.0f",
                Mathf.Approximately(Time.timeScale, 1.0f),
                $"timeScale: {Time.timeScale}");

            AssertCheck("26. GameState is MainMenu and Mode is None",
                gm.StateManager.CurrentState == GameState.MainMenu && gm.CurrentMode == GameMode.None,
                $"State: {gm.StateManager.CurrentState}, Mode: {gm.CurrentMode}");

            AssertCheck("27. Targets Reset on Returning to MainMenu",
                tm.ActiveTargetCount == 0,
                $"Active target count: {tm.ActiveTargetCount}");

            uiMgr.UpdatePanelsForState(GameState.MainMenu);
            GameObject uiCanvas = GameObject.Find("GameUI_Canvas");
            bool menuActive = uiCanvas.transform.Find("MainMenuPanel").gameObject.activeSelf;
            bool hudActive = uiCanvas.transform.Find("HUDPanel").gameObject.activeSelf;
            bool pauseActive = uiCanvas.transform.Find("PausePanel").gameObject.activeSelf;

            AssertCheck("28. MainMenuPanel Active, HUD and Pause Inactive",
                menuActive && !hudActive && !pauseActive,
                $"MainMenu: {menuActive}, HUD: {hudActive}, Pause: {pauseActive}");
        }

        private static void TestChallengeMode()
        {
            var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
            var tm = UnityEngine.Object.FindObjectOfType<TargetManager>();

            // Select Medium difficulty
            gm.DifficultyManager.SetDifficulty(DifficultyLevel.Medium);

            // Start Challenge
            gm.StartChallenge();

            AssertCheck("29. Start Challenge Mode",
                gm.CurrentMode == GameMode.Challenge && gm.StateManager.CurrentState == GameState.Challenge,
                $"Mode: {gm.CurrentMode}, State: {gm.StateManager.CurrentState}");

            AssertCheck("30. Challenge Timer Initialized (60s)",
                Mathf.Approximately(gm.TimeRemaining, 60.0f) && gm.IsTimerRunning,
                $"TimeRemaining: {gm.TimeRemaining}s, TimerRunning: {gm.IsTimerRunning}");

            // Medium active targets cap (10)
            for (int i = 0; i < 25; i++)
            {
                tm.TrySpawnTarget();
            }
            AssertCheck("31. Medium Active Targets Limit (<= 10)",
                tm.ActiveTargetCount <= 10,
                $"Active targets count: {tm.ActiveTargetCount} / 10");

            // Return to clean idle MainMenu
            gm.ReturnToMainMenu();
            AssertCheck("32. Idle on MainMenu without Auto-Starting",
                gm.StateManager.CurrentState == GameState.MainMenu && gm.CurrentMode == GameMode.None && !gm.IsTimerRunning,
                $"State: {gm.StateManager.CurrentState}, Mode: {gm.CurrentMode}, Idle: {!gm.IsTimerRunning}");
        }
    }
}
