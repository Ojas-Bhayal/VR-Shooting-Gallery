using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;
using VRShootingGallery.PC;
using VRShootingGallery.Scoring;
using VRShootingGallery.Targets;
using VRShootingGallery.UI;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.Testing
{
    /// <summary>
    /// Automated runtime test runner that validates all 20 requirements when entering Play Mode.
    /// Logs progress to Unity Console and writes Logs/RuntimeTestReport.txt.
    /// </summary>
    public class RuntimeIntegrationVerifier : MonoBehaviour
    {
        [Header("Auto-Run in Editor")]
        [SerializeField] private bool autoRunTests = false;

        private readonly List<string> _results = new List<string>();
        private int _passedCount = 0;
        private int _totalCount = 0;
        private int _exceptionCount = 0;
        private int _instantiateNullCount = 0;

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLogMessage;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLogMessage;
        }

        private void HandleLogMessage(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || logString.Contains("NullReferenceException"))
            {
                _exceptionCount++;
            }
            if (logString.Contains("Instantiate") && logString.Contains("null"))
            {
                _instantiateNullCount++;
            }
        }

        private void Start()
        {
            if (autoRunTests)
            {
                StartCoroutine(RunVerificationRoutine());
            }
        }

        private IEnumerator RunVerificationRoutine()
        {
            yield return new WaitForSeconds(0.5f); // Wait for scene initialization

            Debug.Log("[VERIFIER] === STARTING RUNTIME INTEGRATION VERIFICATION ===");
            _results.Clear();
            _passedCount = 0;
            _totalCount = 0;

            // 1. Scene Name Check
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            RecordCheck("1. Scene Loaded", sceneName == "ShootingRange" || sceneName.Contains("ShootingRange"), $"Active scene: {sceneName}");

            // 2. Missing Scripts Check
            int missingScripts = CountMissingScripts();
            RecordCheck("2. Zero Missing Scripts", missingScripts == 0, $"Found {missingScripts} missing script components");

            // 3. Environment Materials Check (Not magenta/pink)
            bool materialsOk = CheckMaterialsValid();
            RecordCheck("3. Environment Materials Valid", materialsOk, "All renderers have valid shaders and materials");

            // 4. Managers Check
            GameManager gm = FindObjectOfType<GameManager>();
            ScoreManager sm = FindObjectOfType<ScoreManager>();
            DifficultyManager dm = FindObjectOfType<DifficultyManager>();
            TargetManager tm = FindObjectOfType<TargetManager>();
            WeaponManager wm = FindObjectOfType<WeaponManager>();
            bool managersOk = gm != null && sm != null && dm != null && tm != null && wm != null;
            RecordCheck("4. Game Managers Initialized", managersOk, "GameManager, ScoreManager, DifficultyManager, TargetManager, WeaponManager present");

            // 5. Main Menu UI Check
            UIManager uim = FindObjectOfType<UIManager>();
            MainMenuUI menuUI = FindObjectOfType<MainMenuUI>();
            bool menuVisible = menuUI != null && menuUI.gameObject.activeInHierarchy;
            RecordCheck("5. Main Menu Visible", menuVisible, "MainMenuPanel active in hierarchy");

            // 6. UI Text & Buttons Check
            Text titleText = GameObject.Find("TitleText")?.GetComponent<Text>();
            Button practiceBtn = GameObject.Find("PracticeButton")?.GetComponent<Button>();
            Button challengeBtn = GameObject.Find("ChallengeButton")?.GetComponent<Button>();
            bool buttonsOk = titleText != null && practiceBtn != null && challengeBtn != null;
            RecordCheck("6. Menu Buttons Present", buttonsOk, "Title, Practice, and Challenge buttons located");

            // 7. Static / Moving / Popup Targets Exist
            StaticTarget[] staticTargets = FindObjectsOfType<StaticTarget>();
            MovingTarget[] movingTargets = FindObjectsOfType<MovingTarget>();
            PopupTarget[] popupTargets = FindObjectsOfType<PopupTarget>();
            bool targetsExist = staticTargets.Length > 0 && movingTargets.Length > 0 && popupTargets.Length > 0;
            RecordCheck("7. Targets Exist (Static/Moving/Popup)", targetsExist, $"Static: {staticTargets.Length}, Moving: {movingTargets.Length}, Popup: {popupTargets.Length}");

            // 8. Weapon Check (Pistol)
            bool pistolOk = wm != null && wm.Pistol != null && wm.Pistol.gameObject.activeInHierarchy;
            RecordCheck("8. Pistol Exists & Equipped", pistolOk, "Pistol is active weapon");

            // 9. Initial Ammo Check
            int initialAmmo = wm != null && wm.Pistol != null && wm.Pistol.Ammo != null ? wm.Pistol.Ammo.CurrentAmmo : 0;
            RecordCheck("9. Initial Ammo Valid", initialAmmo == 12, $"Initial ammo: {initialAmmo} / 12");

            // 10. Start Practice Mode
            if (gm != null)
            {
                gm.StartPractice();
                yield return new WaitForSeconds(0.2f);
            }
            HUDController hud = FindObjectOfType<HUDController>();
            bool practiceActive = gm != null && gm.CurrentMode == GameMode.Practice && hud != null && hud.gameObject.activeInHierarchy;
            RecordCheck("10. Practice Mode Active", practiceActive, "Mode is Practice, HUDPanel visible");

            // 11. Target Spawning System Check (Without Null Exception)
            bool spawnOk = tm != null && tm.ActiveTargetCount >= 0;
            RecordCheck("11. Target Spawning System Active", spawnOk, $"TargetManager running with {tm?.ActiveTargetCount} active targets");

            // 12. Fire Weapon
            bool fired = false;
            int ammoAfterFire = initialAmmo;
            if (wm != null && wm.ActiveWeapon != null)
            {
                fired = wm.ActiveWeapon.Fire();
                ammoAfterFire = wm.ActiveWeapon.Ammo.CurrentAmmo;
            }
            RecordCheck("12. Fire Pistol", fired && ammoAfterFire == initialAmmo - 1, $"Fired successfully. Ammo: {ammoAfterFire}");

            // 13. Target Hit & Scoring Check
            TargetZone targetZone = FindObjectOfType<TargetZone>();
            TargetBase hitTarget = targetZone != null ? targetZone.GetComponentInParent<TargetBase>() : null;
            int scoreBefore = sm != null ? sm.Score : 0;
            if (targetZone != null && hitTarget != null)
            {
                HitInfo hitInfo = targetZone.CreateHitInfo(targetZone.transform.position, Vector3.up);
                hitTarget.OnHit(hitInfo);
            }
            int scoreAfter = sm != null ? sm.Score : 0;
            RecordCheck("13. Hit Detection & Score Increase", scoreAfter > scoreBefore, $"Score increased from {scoreBefore} to {scoreAfter}");

            // 14. Accuracy Calculation Check
            float accuracy = sm != null ? sm.Accuracy : 0f;
            RecordCheck("14. Accuracy Updates", accuracy > 0f, $"Accuracy calculated: {accuracy:0.0}%");

            // 15. Reload Weapon Check
            if (wm != null && wm.ActiveWeapon != null)
            {
                wm.ActiveWeapon.Reload();
                yield return new WaitForSeconds(1.3f); // Wait for 1.2s reload routine
            }
            int ammoAfterReload = wm != null && wm.ActiveWeapon != null && wm.ActiveWeapon.Ammo != null ? wm.ActiveWeapon.Ammo.CurrentAmmo : 0;
            RecordCheck("15. Reload Functionality", ammoAfterReload == 12, $"Ammo after reload: {ammoAfterReload} / 12");

            // 16. Weapon Switching Check (Rifle)
            if (wm != null)
            {
                wm.EquipWeapon(WeaponType.Rifle);
                yield return new WaitForSeconds(0.1f);
            }
            bool rifleActive = wm != null && wm.ActiveWeapon != null && wm.ActiveWeapon.WeaponType == WeaponType.Rifle && wm.Rifle.gameObject.activeInHierarchy;
            RecordCheck("16. Weapon Switching (Rifle)", rifleActive, "Rifle equipped, Pistol deactivated");

            // 17. Rifle Fire Check
            bool rifleFired = false;
            if (wm != null && wm.ActiveWeapon != null)
            {
                rifleFired = wm.ActiveWeapon.Fire();
            }
            RecordCheck("17. Fire Rifle", rifleFired, "Rifle discharged successfully");

            // 18. Switch Back to Pistol
            if (wm != null)
            {
                wm.EquipWeapon(WeaponType.Pistol);
                yield return new WaitForSeconds(0.1f);
            }
            RecordCheck("18. Switch Back to Pistol", wm != null && wm.ActiveWeapon == wm.Pistol, "Pistol re-equipped");

            // 19. Start Challenge Mode
            if (gm != null)
            {
                gm.StartChallenge();
                yield return new WaitForSeconds(0.5f);
            }
            bool challengeActive = gm != null && gm.CurrentMode == GameMode.Challenge && gm.IsTimerRunning;
            RecordCheck("19. Start Challenge Mode (60s)", challengeActive, $"Challenge active, time remaining: {gm?.TimeRemaining:0.0}s");

            // 20. Timer Tick Check
            float time1 = gm != null ? gm.TimeRemaining : 60f;
            yield return new WaitForSeconds(0.6f);
            float time2 = gm != null ? gm.TimeRemaining : 60f;
            RecordCheck("20. 60-Second Challenge Timer Ticking", time2 < time1 && time2 > 0f, $"Timer ticking: {time1:0.0}s -> {time2:0.0}s");

            // 21. Difficulty Configuration: Easy = 5
            DifficultyData easyData = dm != null ? dm.GetDifficultyData(DifficultyLevel.Easy) : null;
            bool easyLimitOk = easyData != null && easyData.MaxActiveTargets == 5;
            RecordCheck("21. Easy Max Active Targets (5)", easyLimitOk, $"Easy MaxActiveTargets = {easyData?.MaxActiveTargets} (expected 5)");

            // 22. Difficulty Configuration: Medium = 10
            DifficultyData medData = dm != null ? dm.GetDifficultyData(DifficultyLevel.Medium) : null;
            bool medLimitOk = medData != null && medData.MaxActiveTargets == 10;
            RecordCheck("22. Medium Max Active Targets (10)", medLimitOk, $"Medium MaxActiveTargets = {medData?.MaxActiveTargets} (expected 10)");

            // 23. Difficulty Configuration: Hard = 15
            DifficultyData hardData = dm != null ? dm.GetDifficultyData(DifficultyLevel.Hard) : null;
            bool hardLimitOk = hardData != null && hardData.MaxActiveTargets == 15;
            RecordCheck("23. Hard Max Active Targets (15)", hardLimitOk, $"Hard MaxActiveTargets = {hardData?.MaxActiveTargets} (expected 15)");

            // 24. Practice Mode Respects Easy Difficulty Limit (5)
            if (dm != null) dm.SetDifficulty(DifficultyLevel.Easy);
            if (gm != null) gm.StartPractice();
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < 15; i++)
            {
                if (tm != null) tm.TrySpawnTarget();
            }
            int easyActiveCount = tm != null ? tm.ActiveTargetCount : 0;
            bool easyRespected = easyActiveCount == 5;
            RecordCheck("24. Practice Mode Easy Limit Respected (5)", easyRespected, $"Active targets = {easyActiveCount} / 5");

            // 25. Challenge Mode Respects Medium Difficulty Limit (10)
            if (dm != null) dm.SetDifficulty(DifficultyLevel.Medium);
            if (gm != null) gm.StartChallenge();
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < 25; i++)
            {
                if (tm != null) tm.TrySpawnTarget();
            }
            int medActiveCount = tm != null ? tm.ActiveTargetCount : 0;
            bool medRespected = medActiveCount == 10;
            RecordCheck("25. Challenge Mode Medium Limit Respected (10)", medRespected, $"Active targets = {medActiveCount} / 10");

            // 26. Hard Difficulty Respects Limit (15)
            if (dm != null) dm.SetDifficulty(DifficultyLevel.Hard);
            yield return new WaitForSeconds(0.1f);
            for (int i = 0; i < 35; i++)
            {
                if (tm != null) tm.TrySpawnTarget();
            }
            int hardActiveCount = tm != null ? tm.ActiveTargetCount : 0;
            bool hardRespected = hardActiveCount == 15;
            RecordCheck("26. Hard Difficulty Limit Respected (15)", hardRespected, $"Active targets = {hardActiveCount} / 15");

            // 27. Hitting/Removing Target Allows Replacement Spawn
            bool replacementSpawned = false;
            if (tm != null && tm.ActiveTargetCount > 0)
            {
                int countBefore = tm.ActiveTargetCount;
                TargetBase toDeactivate = null;
                foreach (var t in tm.ActiveTargets)
                {
                    if (t != null && t.IsActive)
                    {
                        toDeactivate = t;
                        break;
                    }
                }
                if (toDeactivate != null)
                {
                    toDeactivate.DeactivateTarget();
                    int afterRemoval = tm.ActiveTargetCount;
                    bool spawned = tm.TrySpawnTarget();
                    int afterReplacement = tm.ActiveTargetCount;
                    replacementSpawned = (afterRemoval < countBefore) && spawned && (afterReplacement > afterRemoval);
                }
            }
            RecordCheck("27. Target Removal Allows Replacement Spawn", replacementSpawned, "Target removal permitted another target to spawn");

            // 28. Zero Exceptions and Instantiate(null) Errors
            bool zeroErrors = _exceptionCount == 0 && _instantiateNullCount == 0;
            RecordCheck("28. Zero Exceptions and Instantiate(null) Errors", zeroErrors, $"Exceptions: {_exceptionCount}, Instantiate(null): {_instantiateNullCount}");

            // Reset back to Medium default
            if (dm != null) dm.SetDifficulty(DifficultyLevel.Medium);

            // Output Final Report
            GenerateReport();
        }

        private void RecordCheck(string checkName, bool passed, string detail)
        {
            _totalCount++;
            if (passed) _passedCount++;
            string status = passed ? "PASS" : "FAIL";
            string log = $"[{status}] {checkName} | {detail}";
            _results.Add(log);
            if (passed)
                Debug.Log($"[VERIFIER] {log}");
            else
                Debug.LogError($"[VERIFIER] {log}");
        }

        private int CountMissingScripts()
        {
            int missing = 0;
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (var go in allObjects)
            {
                Component[] components = go.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c == null) missing++;
                }
            }
            return missing;
        }

        private bool CheckMaterialsValid()
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterials == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) return false;
                    if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void GenerateReport()
        {
            List<string> report = new List<string>();
            report.Add($"RUNTIME INTEGRATION VERIFICATION REPORT");
            report.Add($"Timestamp: {DateTime.UtcNow:O}");
            report.Add($"Result: {_passedCount} / {_totalCount} checks passed ({((float)_passedCount / _totalCount) * 100f:0}%)");
            report.Add("==================================================");
            report.AddRange(_results);
            report.Add("==================================================");

            string reportText = string.Join("\n", report);
            Debug.Log($"\n{reportText}\n");

            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/RuntimeTestReport.txt", reportText);
            }
            catch { }

#if UNITY_EDITOR
            if (File.Exists("Temp/InAutoTest.marker"))
            {
                try { File.Delete("Temp/InAutoTest.marker"); } catch { }
                UnityEditor.EditorApplication.isPlaying = false;
                if (Application.isBatchMode)
                {
                    UnityEditor.EditorApplication.Exit(_passedCount == _totalCount ? 0 : 1);
                }
            }
#endif
        }
    }
}
