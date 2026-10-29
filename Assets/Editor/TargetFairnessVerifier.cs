using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;
using VRShootingGallery.Scoring;
using VRShootingGallery.Targets;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.Editor
{
    /// <summary>
    /// Automated test suite verifying target overlap prevention, occlusion avoidance,
    /// line-of-sight checks, target limits (5/10/15), target replacement, and gameplay preservation.
    /// </summary>
    public static class TargetFairnessVerifier
    {
        private const string ScenePath = "Assets/Scenes/ShootingRange.unity";
        private const string ReportPath = "Logs/TargetFairnessReport.txt";

        private static readonly List<string> _results = new List<string>();
        private static int _passedCount = 0;
        private static int _failedCount = 0;

        [MenuItem("VR Shooting Gallery/Run Target Fairness Verification")]
        public static void RunVerification()
        {
            _results.Clear();
            _passedCount = 0;
            _failedCount = 0;

            string logDir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            LogHeader("VR SHOOTING GALLERY - TARGET FAIRNESS & OCCLUSION VERIFICATION");

            try
            {
                // Open scene
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                AssertCheck("1. Scene Loaded Successfully", scene.IsValid(), $"Loaded: {scene.name}");

                // Find key managers
                var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
                var tm = UnityEngine.Object.FindObjectOfType<TargetManager>();
                var sm = UnityEngine.Object.FindObjectOfType<ScoreManager>();
                var wm = UnityEngine.Object.FindObjectOfType<WeaponManager>();
                var dm = UnityEngine.Object.FindObjectOfType<DifficultyManager>();

                AssertCheck("2. Required Managers Present", gm != null && tm != null && sm != null && wm != null && dm != null,
                    $"GM: {gm != null}, TM: {tm != null}, SM: {sm != null}, WM: {wm != null}, DM: {dm != null}");

                if (gm == null || tm == null || dm == null)
                {
                    SaveReport();
                    return;
                }

                // Verify Spawn Points Count
                AssertCheck("3. Spawn Points Count (>= 20)", tm.SpawnPoints.Count >= 20,
                    $"Spawn Points configured: {tm.SpawnPoints.Count}");

                // Test A: Easy Mode Target Placement & Fairness
                TestDifficultyPlacement(gm, tm, dm, DifficultyLevel.Easy, 5, 1.65f, 3.8f, "Easy Mode");

                // Test B: Medium Mode Target Placement & Fairness
                TestDifficultyPlacement(gm, tm, dm, DifficultyLevel.Medium, 10, 1.40f, 2.8f, "Medium Mode");

                // Test C: Hard Mode Target Placement & Fairness
                TestDifficultyPlacement(gm, tm, dm, DifficultyLevel.Hard, 15, 1.25f, 2.4f, "Hard Mode");

                // Test D: Target Types Preserved
                TestTargetTypesPreserved(tm);

                // Test E: Target Replacement Validation
                TestTargetReplacement(gm, tm, dm);

                // Test F: Repeated Game Restarts & Clean Return to Main Menu
                TestRepeatedGameplayAndMenu(gm, tm, sm, wm);

                // Test G: Shooting, Hit Detection & Raycasting Preservation
                TestShootingAndRaycasting(gm, tm, sm, wm);

                // Test H: Null Prefab and Over-Spawning Safety
                TestSpawningSafety(tm);
            }
            catch (Exception ex)
            {
                AssertCheck("CRITICAL EXCEPTION", false, $"Unhandled: {ex.Message}\n{ex.StackTrace}");
            }

            SaveReport();
        }

        private static void TestDifficultyPlacement(GameManager gm, TargetManager tm, DifficultyManager dm,
            DifficultyLevel level, int expectedCount, float minAllowedDist, float minAllowedAngle, string label)
        {
            gm.StartPractice();
            dm.SetDifficulty(level);
            tm.ResetAllTargets();
            tm.PopulateInitialTargets();

            // Verify count
            AssertCheck($"{label} Active Target Count ({expectedCount})", tm.ActiveTargetCount == expectedCount,
                $"Active: {tm.ActiveTargetCount} / {expectedCount}");

            // Verify Pairwise 3D Distance
            float minDist = float.MaxValue;
            string minDistPair = "";
            var activeList = tm.ActiveTargets;

            for (int i = 0; i < activeList.Count; i++)
            {
                for (int j = i + 1; j < activeList.Count; j++)
                {
                    if (activeList[i] == null || activeList[j] == null) continue;
                    float d = Vector3.Distance(activeList[i].transform.position, activeList[j].transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        minDistPair = $"{activeList[i].name} & {activeList[j].name}";
                    }
                }
            }

            AssertCheck($"{label} Minimum 3D Separation (>= {minAllowedDist:F2}m)", minDist >= minAllowedDist,
                $"Min Dist: {minDist:F2}m ({minDistPair})");

            // Verify Pairwise Angular Separation from Player Eye
            Vector3 playerEye = Camera.main != null ? Camera.main.transform.position : new Vector3(0f, 1.7f, 0f);
            float minAngle = float.MaxValue;
            string minAnglePair = "";

            for (int i = 0; i < activeList.Count; i++)
            {
                for (int j = i + 1; j < activeList.Count; j++)
                {
                    if (activeList[i] == null || activeList[j] == null) continue;
                    Vector3 v1 = activeList[i].TargetCenter - playerEye;
                    Vector3 v2 = activeList[j].TargetCenter - playerEye;
                    float a = Vector3.Angle(v1, v2);
                    if (a < minAngle)
                    {
                        minAngle = a;
                        minAnglePair = $"{activeList[i].name} & {activeList[j].name}";
                    }
                }
            }

            AssertCheck($"{label} Minimum Angular Separation (>= {minAllowedAngle:F1} deg)", minAngle >= minAllowedAngle,
                $"Min Angle: {minAngle:F2} deg ({minAnglePair})");

            // Verify Line-of-Sight Raycast for each target
            int unblockedCount = 0;
            foreach (var target in activeList)
            {
                if (target == null) continue;
                Vector3 targetCenter = target.TargetCenter;
                Vector3 dir = targetCenter - playerEye;
                float dist = dir.magnitude;

                RaycastHit hit;
                if (Physics.Raycast(playerEye, dir.normalized, out hit, dist - 0.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hitTarget = hit.collider.GetComponentInParent<TargetBase>();
                    if (hitTarget != null && hitTarget != target)
                    {
                        // Blocked by another target
                        Debug.LogWarning($"[{label} LOS Blocked] Target {target.name} at {target.transform.position} blocked by {hitTarget.name} at {hitTarget.transform.position} (collider: {hit.collider.name})");
                        continue;
                    }
                    if (hitTarget == null)
                    {
                        // Blocked by environment
                        Debug.LogWarning($"[{label} LOS Blocked] Target {target.name} at {target.transform.position} blocked by environment obstacle {hit.collider.name} at {hit.point}");
                        continue;
                    }
                }
                unblockedCount++;
            }

            AssertCheck($"{label} Line-of-Sight ({unblockedCount}/{expectedCount} Visible)", unblockedCount == expectedCount,
                $"Clear LOS: {unblockedCount} / {expectedCount}");
        }

        private static void TestTargetTypesPreserved(TargetManager tm)
        {
            // Verify TargetManager knows about all 3 types
            bool hasStatic = false;
            bool hasMoving = false;
            bool hasPopup = false;

            // Check prefabs or spawn multiple to see types
            var prefabs = tm.GetType().GetField("targetPrefabs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(tm) as List<TargetBase>;

            if (prefabs != null)
            {
                foreach (var p in prefabs)
                {
                    if (p is StaticTarget) hasStatic = true;
                    if (p is MovingTarget) hasMoving = true;
                    if (p is PopupTarget) hasPopup = true;
                }
            }

            AssertCheck("Target Types: StaticTarget Present", hasStatic, $"StaticTarget registered: {hasStatic}");
            AssertCheck("Target Types: MovingTarget Present", hasMoving, $"MovingTarget registered: {hasMoving}");
            AssertCheck("Target Types: PopupTarget Present", hasPopup, $"PopupTarget registered: {hasPopup}");
        }

        private static void TestTargetReplacement(GameManager gm, TargetManager tm, DifficultyManager dm)
        {
            dm.SetDifficulty(DifficultyLevel.Hard);
            tm.ResetAllTargets();
            tm.PopulateInitialTargets();

            int initialCount = tm.ActiveTargetCount;
            AssertCheck("Target Replacement: Initial Hard Count is 15", initialCount == 15, $"Count: {initialCount}");

            // Despawn one target
            var targetToDespawn = tm.ActiveTargets[0];
            targetToDespawn.DeactivateTarget();

            // When in active gameplay, TargetManager.OnTargetDespawned automatically removes old target and spawns replacement
            bool containsOld = false;
            foreach (var t in tm.ActiveTargets)
            {
                if (t == targetToDespawn)
                {
                    containsOld = true;
                    break;
                }
            }
            AssertCheck("Target Replacement: Despawned target removed from active list", !containsOld,
                $"Old target removed: {!containsOld}");

            int countAfterReplacement = tm.ActiveTargetCount;
            AssertCheck("Target Replacement: Replacement automatically spawned to maintain limit", countAfterReplacement == 15,
                $"Count after replacement: {countAfterReplacement}");

            AssertCheck("Target Replacement: TrySpawnTarget respects limit when at capacity", !tm.TrySpawnTarget(),
                "Correctly rejected over-spawn at capacity");

            // Verify the newly spawned target does not violate separation
            float minDist = float.MaxValue;
            var activeList = tm.ActiveTargets;
            for (int i = 0; i < activeList.Count; i++)
            {
                for (int j = i + 1; j < activeList.Count; j++)
                {
                    if (activeList[i] == null || activeList[j] == null) continue;
                    float d = Vector3.Distance(activeList[i].transform.position, activeList[j].transform.position);
                    if (d < minDist) minDist = d;
                }
            }
            AssertCheck("Target Replacement: New target maintains minimum separation (>= 1.25m)", minDist >= 1.25f,
                $"Min Separation after replacement: {minDist:F2}m");
        }

        private static void TestRepeatedGameplayAndMenu(GameManager gm, TargetManager tm, ScoreManager sm, WeaponManager wm)
        {
            // Restart Current Mode
            gm.RestartCurrentMode();
            AssertCheck("Restart: Mode is Practice & timeScale is 1.0f",
                Mathf.Approximately(Time.timeScale, 1.0f) && gm.CurrentMode == GameMode.Practice,
                $"timeScale: {Time.timeScale}, Mode: {gm.CurrentMode}");

            AssertCheck("Restart: Score is reset to 0", sm.Score == 0, $"Score: {sm.Score}");

            // Pause Game
            gm.PauseGame();
            AssertCheck("Pause: timeScale is 0 & state is Paused",
                Mathf.Approximately(Time.timeScale, 0.0f) && gm.StateManager.CurrentState == GameState.Paused,
                $"timeScale: {Time.timeScale}, State: {gm.StateManager.CurrentState}");

            // TogglePause while paused does NOT unpause
            gm.TogglePause();
            AssertCheck("Pause: Escape while paused DOES NOT unpause",
                Mathf.Approximately(Time.timeScale, 0.0f) && gm.StateManager.CurrentState == GameState.Paused,
                $"timeScale: {Time.timeScale}, State: {gm.StateManager.CurrentState}");

            // Return to Main Menu
            gm.ReturnToMainMenu();
            AssertCheck("Main Menu: timeScale is 1.0f & state is MainMenu",
                Mathf.Approximately(Time.timeScale, 1.0f) && gm.StateManager.CurrentState == GameState.MainMenu,
                $"timeScale: {Time.timeScale}, State: {gm.StateManager.CurrentState}");

            AssertCheck("Main Menu: Active Target Count is 0", tm.ActiveTargetCount == 0,
                $"Active targets on MainMenu: {tm.ActiveTargetCount}");
        }

        private static void TestShootingAndRaycasting(GameManager gm, TargetManager tm, ScoreManager sm, WeaponManager wm)
        {
            gm.StartPractice();
            tm.PopulateInitialTargets();

            if (wm != null)
            {
                if (wm.ActiveWeapon == null) wm.EquipWeapon(WeaponType.Pistol);

                if (wm.ActiveWeapon != null && wm.ActiveWeapon.Ammo != null)
                {
                    int ammoStart = wm.ActiveWeapon.Ammo.CurrentAmmo;
                    wm.ActiveWeapon.Fire();
                    int ammoAfter = wm.ActiveWeapon.Ammo.CurrentAmmo;

                    AssertCheck("Shooting: Pistol fire decrements ammo", ammoAfter == ammoStart - 1,
                        $"Ammo: {ammoStart} -> {ammoAfter}");

                    // Reload
                    wm.ActiveWeapon.Ammo.InstantRefill();
                    AssertCheck("Shooting: Weapon reload restores full magazine", wm.ActiveWeapon.Ammo.IsFull,
                        $"Current Ammo: {wm.ActiveWeapon.Ammo.CurrentAmmo} / {wm.ActiveWeapon.Ammo.MagazineCapacity}");
                }

                // Hit Detection and Score Simulation
                if (tm.ActiveTargets.Count > 0)
                {
                    var target = tm.ActiveTargets[0];
                    int scoreBefore = sm.Score;
                    HitInfo hit = new HitInfo(HitType.Center, target.transform.position, Vector3.forward, target.gameObject, 25);
                    target.OnHit(hit);
                    int scoreAfter = sm.Score;

                    AssertCheck("Scoring: Target OnHit registers score increase", scoreAfter > scoreBefore,
                        $"Score: {scoreBefore} -> {scoreAfter}");
                }
            }
        }

        private static void TestSpawningSafety(TargetManager tm)
        {
            // Call TrySpawnTarget repeatedly when at capacity
            bool anyFailedGracefully = true;
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    tm.TrySpawnTarget();
                }
                catch (Exception ex)
                {
                    anyFailedGracefully = false;
                    AssertCheck("Safety: Over-spawning throws no exceptions", false, $"Exception: {ex.Message}");
                    break;
                }
            }

            if (anyFailedGracefully)
            {
                AssertCheck("Safety: Over-spawning handled safely with 0 exceptions", true,
                    "30 consecutive spawn calls handled safely");
            }
        }

        private static void AssertCheck(string testName, bool condition, string detail)
        {
            if (condition)
            {
                _passedCount++;
                string line = $"[PASS] {testName} | {detail}";
                _results.Add(line);
                Debug.Log(line);
            }
            else
            {
                _failedCount++;
                string line = $"[FAIL] {testName} | {detail}";
                _results.Add(line);
                Debug.LogError(line);
            }
        }

        private static void LogHeader(string title)
        {
            _results.Add("==================================================");
            _results.Add(title);
            _results.Add($"Timestamp: {DateTime.UtcNow:O}");
            _results.Add("==================================================");
        }

        private static void SaveReport()
        {
            _results.Add("==================================================");
            _results.Add($"SUMMARY: {_passedCount} / {_passedCount + _failedCount} checks PASSED ({_failedCount} failed)");
            _results.Add($"OVERALL STATUS: {(_failedCount == 0 ? "PASS" : "FAIL")}");
            _results.Add("==================================================");

            File.WriteAllLines(ReportPath, _results);
            Debug.Log($"Target fairness report written to {ReportPath}. Overall: {(_failedCount == 0 ? "PASS" : "FAIL")}");
        }
    }
}
