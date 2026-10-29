using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;
using VRShootingGallery.PC;
using VRShootingGallery.Scoring;
using VRShootingGallery.Targets;
using VRShootingGallery.UI;
using VRShootingGallery.VR;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.Editor
{
    [InitializeOnLoad]
    public static class ShootingGallerySetup
    {
        private const string ScenePath = "Assets/Scenes/ShootingRange.unity";
        private const string RepairMarkerFile = "ProjectSettings/ShootingGalleryRepaired.marker";
        private const string TriggerTestFile = "Temp/RunPlayModeTest.trigger";
        private const string InTestMarkerFile = "Temp/InAutoTest.marker";
        private const string FixerTriggerFile = "Temp/RunFixer.trigger";
        private const string VerifyTriggerFile = "Temp/RunVerification.trigger";

        static ShootingGallerySetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                if (!File.Exists(RepairMarkerFile))
                {
                    Debug.Log("[VR Shooting Gallery] Corrupted asset repair marker not found. Running full asset repair and scene rebuild...");
                    RunFullRepair();
                }

                VerifyDifficultyLimits();

                if (File.Exists(TriggerTestFile) && !EditorApplication.isPlaying)
                {
                    try { File.Delete(TriggerTestFile); } catch { }
                    try { File.WriteAllText(InTestMarkerFile, "1"); } catch { }
                    Debug.Log("[VR Shooting Gallery] Triggering Automated Play Mode Verification...");
                    EditorApplication.EnterPlaymode();
                }
            };

            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && File.Exists("Temp/TakeScreenshotInPlayMode.marker"))
                {
                    try { File.Delete("Temp/TakeScreenshotInPlayMode.marker"); } catch { }
                    EditorApplication.delayCall += () =>
                    {
                        EditorApplication.delayCall += () =>
                        {
                            Directory.CreateDirectory("Logs");
                            ScreenCapture.CaptureScreenshot("Logs/GameView_MainMenu.png");
                            Debug.Log("[VR Shooting Gallery] Captured GameView_MainMenu.png in Play Mode!");
                            File.WriteAllText("Temp/ScreenshotDone.marker", "1");
                            EditorApplication.delayCall += () =>
                            {
                                EditorApplication.isPlaying = false;
                            };
                        };
                    };
                }
            };

            EditorApplication.update += CheckTriggers;
        }

        private static void CheckTriggers()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(FixerTriggerFile))
            {
                try { File.Delete(FixerTriggerFile); } catch { }
                Debug.Log("[VR Shooting Gallery] Triggering Full Integration Fixer via trigger file...");
                ShootingGalleryIntegrationFixer.RunFullFix();
            }

            if (File.Exists(VerifyTriggerFile))
            {
                try { File.Delete(VerifyTriggerFile); } catch { }
                Debug.Log("[VR Shooting Gallery] Triggering Verification via trigger file...");
                GameplayRepairVerifier.RunVerification();
                TargetFairnessVerifier.RunVerification();
                VRInteractionVerifier.RunVerification();
            }

            if (File.Exists("Temp/CaptureScreenshot.trigger"))
            {
                try { File.Delete("Temp/CaptureScreenshot.trigger"); } catch { }
                Debug.Log("[VR Shooting Gallery] Opening scene and taking screenshot in Game View...");
                File.WriteAllText("Temp/TakeScreenshotInPlayMode.marker", "1");
                EditorSceneManager.OpenScene("Assets/Scenes/ShootingRange.unity");
                EditorApplication.EnterPlaymode();
            }
        }

        [MenuItem("VR Shooting Gallery/Repair All Assets & Rebuild Scene")]
        public static void RunFullRepairMenu()
        {
            if (File.Exists(RepairMarkerFile))
            {
                File.Delete(RepairMarkerFile);
            }
            RunFullRepair();
        }

        [MenuItem("VR Shooting Gallery/Run Setup Integration")]
        public static void RunFullSetup()
        {
            RunFullRepair();
        }

        [MenuItem("VR Shooting Gallery/Verify Difficulty Limits")]
        public static void VerifyDifficultyLimits()
        {
            var easy = AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/DifficultyData_Easy.asset");
            var med = AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/DifficultyData_Medium.asset");
            var hard = AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/DifficultyData_Hard.asset");

            Debug.Log($"[VERIFICATION] Easy MaxActiveTargets: {easy?.MaxActiveTargets} (Expected: 5)");
            Debug.Log($"[VERIFICATION] Medium MaxActiveTargets: {med?.MaxActiveTargets} (Expected: 10)");
            Debug.Log($"[VERIFICATION] Hard MaxActiveTargets: {hard?.MaxActiveTargets} (Expected: 15)");

            bool ok = easy != null && easy.MaxActiveTargets == 5 &&
                      med != null && med.MaxActiveTargets == 10 &&
                      hard != null && hard.MaxActiveTargets == 15;

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== DIFFICULTY CONFIGURATION VERIFICATION ===");
            report.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            report.AppendLine($"Easy:   MaxActiveTargets = {easy?.MaxActiveTargets} (Expected: 5) => {(easy?.MaxActiveTargets == 5 ? "PASS" : "FAIL")}");
            report.AppendLine($"Medium: MaxActiveTargets = {med?.MaxActiveTargets} (Expected: 10) => {(med?.MaxActiveTargets == 10 ? "PASS" : "FAIL")}");
            report.AppendLine($"Hard:   MaxActiveTargets = {hard?.MaxActiveTargets} (Expected: 15) => {(hard?.MaxActiveTargets == 15 ? "PASS" : "FAIL")}");
            report.AppendLine($"Overall Status: {(ok ? "PASS" : "FAIL")}");

            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/DifficultyVerification.txt", report.ToString());
            }
            catch { }

            if (ok)
            {
                Debug.Log("[VERIFICATION] PASS: All difficulty limits verified successfully!");
            }
            else
            {
                Debug.LogError("[VERIFICATION] FAIL: Difficulty limits mismatch!");
            }
        }

        [MenuItem("VR Shooting Gallery/Run Automated Play Mode Tests")]
        public static void RunAutomatedPlayModeTests()
        {
            if (!File.Exists(RepairMarkerFile))
            {
                RunFullRepair();
            }

            Debug.Log("[VR Shooting Gallery] Opening ShootingRange scene and entering Play Mode...");
            EditorSceneManager.OpenScene(ScenePath);
            File.WriteAllText(InTestMarkerFile, "1");
            EditorApplication.EnterPlaymode();
        }

        public static void RunFullRepair()
        {
            Debug.Log("[VR Shooting Gallery] === STARTING ASSET REPAIR & SCENE REBUILD ===");

            EnsureDirectories();
            CleanCorruptedAssetsAndMetadata();
            GenerateAudioClips();
            GenerateMaterials();
            GenerateScriptableObjects();
            GeneratePrefabs();
            BuildScene();

            File.WriteAllText(RepairMarkerFile, DateTime.UtcNow.ToString("O"));

            VerifyGuidIntegrity();
            Debug.Log("[VR Shooting Gallery] === ASSET REPAIR & SCENE REBUILD COMPLETED SUCCESSFULLY ===");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = {
                "Assets/Audio",
                "Assets/Materials",
                "Assets/Prefabs",
                "Assets/Prefabs/Environment",
                "Assets/Prefabs/Targets",
                "Assets/Prefabs/UI",
                "Assets/Prefabs/Weapons",
                "Assets/Scenes",
                "Assets/ScriptableObjects"
            };

            foreach (var d in dirs)
            {
                if (!Directory.Exists(d))
                {
                    Directory.CreateDirectory(d);
                }
            }
        }

        #region 0. Clean Corrupted Placeholder Assets and Metadata
        private static void CleanCorruptedAssetsAndMetadata()
        {
            Debug.Log("[VR Shooting Gallery] Cleaning corrupted placeholder assets and metadata...");

            string[] assetsToDelete = {
                "Assets/Materials/Mat_Floor.mat",
                "Assets/Materials/Mat_Wall.mat",
                "Assets/Materials/Mat_Table.mat",
                "Assets/Materials/Mat_TargetOuter.mat",
                "Assets/Materials/Mat_TargetCenter.mat",
                "Assets/Materials/Mat_TargetBullseye.mat",
                "Assets/Materials/Mat_GunMetal.mat",
                "Assets/Materials/Mat_Tracer.mat",
                "Assets/Prefabs/Targets/StaticTarget.prefab",
                "Assets/Prefabs/Targets/MovingTarget.prefab",
                "Assets/Prefabs/Targets/PopupTarget.prefab",
                "Assets/Prefabs/Environment/ShootingRange_Environment.prefab",
                "Assets/Prefabs/Weapons/Pistol.prefab",
                "Assets/Prefabs/Weapons/Rifle.prefab",
                "Assets/Prefabs/UI/GameUI_Canvas.prefab"
            };

            foreach (var path in assetsToDelete)
            {
                AssetDatabase.DeleteAsset(path);
                string metaPath = path + ".meta";
                if (File.Exists(metaPath))
                {
                    try { File.Delete(metaPath); } catch { }
                }
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
            }

            // Replace placeholder GUIDs in audio and difficulty metadata with Unity-generated GUIDs
            string[] placeholderMetas = {
                "Assets/ScriptableObjects/DifficultyData_Easy.asset.meta",
                "Assets/ScriptableObjects/DifficultyData_Medium.asset.meta",
                "Assets/ScriptableObjects/DifficultyData_Hard.asset.meta",
                "Assets/Audio/gunshot_pistol.wav.meta",
                "Assets/Audio/gunshot_rifle.wav.meta",
                "Assets/Audio/gun_empty.wav.meta",
                "Assets/Audio/gun_reload.wav.meta",
                "Assets/Audio/target_hit.wav.meta",
                "Assets/Audio/target_bullseye.wav.meta",
                "Assets/Audio/ui_click.wav.meta"
            };

            foreach (var meta in placeholderMetas)
            {
                if (File.Exists(meta))
                {
                    string text = File.ReadAllText(meta);
                    var m = Regex.Match(text, @"guid:\s*([a-z0-9]{32})");
                    if (m.Success)
                    {
                        string g = m.Groups[1].Value;
                        if (g.StartsWith("a1f") || g.StartsWith("d1f") || g.StartsWith("m1f") || g.StartsWith("p1f"))
                        {
                            string newGuid = GUID.Generate().ToString();
                            text = Regex.Replace(text, @"guid:\s*[a-z0-9]{32}", "guid: " + newGuid);
                            File.WriteAllText(meta, text);
                        }
                    }
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        #endregion

        #region 1. Procedural Audio Generation
        private static void GenerateAudioClips()
        {
            GenerateWavFile("Assets/Audio/gunshot_pistol.wav", 0.22f, t =>
            {
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-18f * t);
                float thump = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-14f * t);
                return Mathf.Clamp((noise * 0.7f + thump * 0.6f), -1f, 1f);
            });

            GenerateWavFile("Assets/Audio/gunshot_rifle.wav", 0.32f, t =>
            {
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-12f * t);
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-10f * t);
                return Mathf.Clamp((noise * 0.8f + thump * 0.7f), -1f, 1f);
            });

            GenerateWavFile("Assets/Audio/gun_empty.wav", 0.06f, t =>
            {
                return Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-40f * t);
            });

            GenerateWavFile("Assets/Audio/gun_reload.wav", 0.35f, t =>
            {
                float click1 = t < 0.15f ? Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-30f * t) : 0f;
                float click2 = t >= 0.18f ? Mathf.Sin(2f * Mathf.PI * 1400f * (t - 0.18f)) * Mathf.Exp(-30f * (t - 0.18f)) : 0f;
                return Mathf.Clamp(click1 + click2, -1f, 1f);
            });

            GenerateWavFile("Assets/Audio/target_hit.wav", 0.15f, t =>
            {
                float tone = Mathf.Sin(2f * Mathf.PI * 480f * t) * Mathf.Exp(-22f * t);
                float snap = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-45f * t);
                return Mathf.Clamp(tone * 0.6f + snap * 0.5f, -1f, 1f);
            });

            GenerateWavFile("Assets/Audio/target_bullseye.wav", 0.38f, t =>
            {
                float chime1 = Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-9f * t);
                float chime2 = Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-12f * t);
                return Mathf.Clamp((chime1 * 0.7f + chime2 * 0.4f), -1f, 1f);
            });

            GenerateWavFile("Assets/Audio/ui_click.wav", 0.05f, t =>
            {
                return Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-35f * t);
            });

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void GenerateWavFile(string relativePath, float duration, Func<float, float> synth)
        {
            if (File.Exists(relativePath) && new FileInfo(relativePath).Length > 100) return;

            int sampleRate = 44100;
            int numSamples = Mathf.CeilToInt(sampleRate * duration);
            int subChunk2Size = numSamples * 2;
            int chunkSize = 36 + subChunk2Size;

            using (var fs = new FileStream(relativePath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(chunkSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(subChunk2Size);

                for (int i = 0; i < numSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float sample = Mathf.Clamp(synth(t), -1f, 1f);
                    short pcm = (short)(sample * 32767f);
                    bw.Write(pcm);
                }
            }

            AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceSynchronousImport);
        }
        #endregion

        #region 2. Materials Generation
        private static void GenerateMaterials()
        {
            Debug.Log("[VR Shooting Gallery] Generating URP Materials...");

            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit")
                               ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                               ?? Shader.Find("Standard");

            Shader urpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Unlit/Color")
                                 ?? urpLitShader;

            CreateMaterialAsset("Assets/Materials/Mat_Floor.mat", urpLitShader, new Color(0.22f, 0.24f, 0.28f));
            CreateMaterialAsset("Assets/Materials/Mat_Wall.mat", urpLitShader, new Color(0.38f, 0.40f, 0.44f));
            CreateMaterialAsset("Assets/Materials/Mat_Table.mat", urpLitShader, new Color(0.35f, 0.24f, 0.16f));
            CreateMaterialAsset("Assets/Materials/Mat_TargetOuter.mat", urpLitShader, new Color(0.92f, 0.92f, 0.92f));
            CreateMaterialAsset("Assets/Materials/Mat_TargetCenter.mat", urpLitShader, new Color(0.85f, 0.18f, 0.18f));
            CreateMaterialAsset("Assets/Materials/Mat_TargetBullseye.mat", urpLitShader, new Color(0.98f, 0.82f, 0.12f));
            CreateMaterialAsset("Assets/Materials/Mat_GunMetal.mat", urpLitShader, new Color(0.14f, 0.15f, 0.18f));
            CreateMaterialAsset("Assets/Materials/Mat_Tracer.mat", urpUnlitShader, new Color(1.0f, 0.85f, 0.2f));
            CreateMaterialAsset("Assets/Materials/Mat_UIRay.mat", urpUnlitShader, new Color(0.2f, 0.7f, 1.0f, 0.85f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateMaterialAsset(string path, Shader shader, Color color)
        {
            AssetDatabase.DeleteAsset(path);

            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            AssetDatabase.CreateAsset(mat, path);
        }
        #endregion

        #region 3. ScriptableObjects Generation
        private static void GenerateScriptableObjects()
        {
            Debug.Log("[VR Shooting Gallery] Generating Difficulty ScriptableObjects...");

            CreateDifficultyAsset("Assets/ScriptableObjects/DifficultyData_Easy.asset", DifficultyLevel.Easy, "Easy", 1.3f, 1.2f, 4.0f, 3.0f, 5, 1.0f);
            CreateDifficultyAsset("Assets/ScriptableObjects/DifficultyData_Medium.asset", DifficultyLevel.Medium, "Medium", 1.0f, 2.0f, 3.0f, 2.2f, 10, 1.25f);
            CreateDifficultyAsset("Assets/ScriptableObjects/DifficultyData_Hard.asset", DifficultyLevel.Hard, "Hard", 0.75f, 3.5f, 1.8f, 1.5f, 15, 1.5f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateDifficultyAsset(string path, DifficultyLevel level, string displayName, float scale, float speed, float popup, float spawnInterval, int maxActive, float multiplier)
        {
            DifficultyData data = AssetDatabase.LoadAssetAtPath<DifficultyData>(path);
            bool isNew = false;
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DifficultyData>();
                isNew = true;
            }

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("level").enumValueIndex = (int)level;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("targetScale").floatValue = scale;
            so.FindProperty("targetMoveSpeed").floatValue = speed;
            so.FindProperty("popupDuration").floatValue = popup;
            so.FindProperty("spawnInterval").floatValue = spawnInterval;
            so.FindProperty("maxActiveTargets").intValue = maxActive;
            so.FindProperty("scoreMultiplier").floatValue = multiplier;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
            {
                AssetDatabase.CreateAsset(data, path);
            }
            else
            {
                EditorUtility.SetDirty(data);
            }
        }
        #endregion

        #region 4. Prefabs Generation
        private static void GeneratePrefabs()
        {
            Debug.Log("[VR Shooting Gallery] Generating Prefabs...");

            AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/target_hit.wav");
            AudioClip bullseyeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/target_bullseye.wav");
            AudioClip pistolFireClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gunshot_pistol.wav");
            AudioClip rifleFireClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gunshot_rifle.wav");
            AudioClip emptyClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gun_empty.wav");
            AudioClip reloadClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gun_reload.wav");

            Material outerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TargetOuter.mat");
            Material centerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TargetCenter.mat");
            Material bullseyeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TargetBullseye.mat");
            Material gunMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_GunMetal.mat");
            Material tracerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Tracer.mat");

            CreateStaticTargetPrefab("Assets/Prefabs/Targets/StaticTarget.prefab", outerMat, centerMat, bullseyeMat, hitClip, bullseyeClip);
            CreateMovingTargetPrefab("Assets/Prefabs/Targets/MovingTarget.prefab", outerMat, centerMat, bullseyeMat, hitClip, bullseyeClip);
            CreatePopupTargetPrefab("Assets/Prefabs/Targets/PopupTarget.prefab", outerMat, centerMat, bullseyeMat, hitClip, bullseyeClip);
            CreateWeaponPrefab("Assets/Prefabs/Weapons/Pistol.prefab", WeaponType.Pistol, "Pistol", 0.2f, 100f, 12, 1.2f, gunMat, tracerMat, pistolFireClip, emptyClip, reloadClip);
            CreateWeaponPrefab("Assets/Prefabs/Weapons/Rifle.prefab", WeaponType.Rifle, "Rifle", 0.1f, 150f, 30, 2.0f, gunMat, tracerMat, rifleFireClip, emptyClip, reloadClip);
            CreateEnvironmentPrefab("Assets/Prefabs/Environment/ShootingRange_Environment.prefab");
            CreateUIPrefab("Assets/Prefabs/UI/GameUI_Canvas.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateStaticTargetPrefab(string path, Material outerMat, Material centerMat, Material bullseyeMat, AudioClip hitClip, AudioClip bullseyeClip)
        {
            AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject("StaticTarget");
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.2f, 0.08f);
            col.radius = 0.55f;
            col.height = 0.2f;
            col.direction = 2; // Z-axis

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "StandPole";
            pole.transform.SetParent(root.transform);
            pole.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 0.6f, 0.08f);
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());

            GameObject model = new GameObject("TargetModel");
            model.transform.SetParent(root.transform);
            model.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            BuildTargetRings(model, outerMat, centerMat, bullseyeMat);

            StaticTarget target = root.AddComponent<StaticTarget>();
            SerializedObject so = new SerializedObject(target);
            so.FindProperty("basePoints").intValue = 10;
            so.FindProperty("targetCollider").objectReferenceValue = col;
            so.FindProperty("targetRenderer").objectReferenceValue = model.transform.Find("OuterRing").GetComponent<Renderer>();
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("hitSound").objectReferenceValue = hitClip;
            so.FindProperty("bullseyeSound").objectReferenceValue = bullseyeClip;
            so.FindProperty("respawnDelay").floatValue = 2.0f;
            so.FindProperty("targetModel").objectReferenceValue = model.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateMovingTargetPrefab(string path, Material outerMat, Material centerMat, Material bullseyeMat, AudioClip hitClip, AudioClip bullseyeClip)
        {
            AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject("MovingTarget");
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.2f, 0.08f);
            col.radius = 0.55f;
            col.height = 0.2f;
            col.direction = 2; // Z-axis

            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "RailBar";
            rail.transform.SetParent(root.transform);
            rail.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            rail.transform.localScale = new Vector3(6.5f, 0.06f, 0.06f);
            UnityEngine.Object.DestroyImmediate(rail.GetComponent<Collider>());

            GameObject model = new GameObject("TargetModel");
            model.transform.SetParent(root.transform);
            model.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            BuildTargetRings(model, outerMat, centerMat, bullseyeMat);

            MovingTarget target = root.AddComponent<MovingTarget>();
            SerializedObject so = new SerializedObject(target);
            so.FindProperty("basePoints").intValue = 10;
            so.FindProperty("targetCollider").objectReferenceValue = col;
            so.FindProperty("targetRenderer").objectReferenceValue = model.transform.Find("OuterRing").GetComponent<Renderer>();
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("hitSound").objectReferenceValue = hitClip;
            so.FindProperty("bullseyeSound").objectReferenceValue = bullseyeClip;
            so.FindProperty("travelDistance").floatValue = 3.0f;
            so.FindProperty("defaultSpeed").floatValue = 2.0f;
            so.FindProperty("targetModel").objectReferenceValue = model.transform;
            so.FindProperty("respawnDelay").floatValue = 2.0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreatePopupTargetPrefab(string path, Material outerMat, Material centerMat, Material bullseyeMat, AudioClip hitClip, AudioClip bullseyeClip)
        {
            AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject("PopupTarget");
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;

            GameObject baseStand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseStand.name = "BaseStand";
            baseStand.transform.SetParent(root.transform);
            baseStand.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            baseStand.transform.localScale = new Vector3(0.8f, 0.5f, 0.6f);
            UnityEngine.Object.DestroyImmediate(baseStand.GetComponent<Collider>());

            GameObject pivot = new GameObject("PivotArm");
            pivot.transform.SetParent(root.transform);
            pivot.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            GameObject model = new GameObject("TargetPlate");
            model.transform.SetParent(pivot.transform);
            model.transform.localPosition = new Vector3(0f, 0.7f, 0f);

            BuildTargetRings(model, outerMat, centerMat, bullseyeMat);

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.2f, 0.08f);
            col.radius = 0.55f;
            col.height = 0.2f;
            col.direction = 2; // Z-axis

            PopupTarget target = root.AddComponent<PopupTarget>();
            SerializedObject so = new SerializedObject(target);
            so.FindProperty("basePoints").intValue = 10;
            so.FindProperty("targetCollider").objectReferenceValue = col;
            so.FindProperty("targetRenderer").objectReferenceValue = model.transform.Find("OuterRing").GetComponent<Renderer>();
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("hitSound").objectReferenceValue = hitClip;
            so.FindProperty("bullseyeSound").objectReferenceValue = bullseyeClip;
            so.FindProperty("pivotTransform").objectReferenceValue = pivot.transform;
            so.FindProperty("defaultPopupDuration").floatValue = 3.0f;
            so.FindProperty("animationSpeed").floatValue = 8.0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildTargetRings(GameObject parent, Material outerMat, Material centerMat, Material bullseyeMat)
        {
            // Outer Ring: Normal hit (+10)
            GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outer.name = "OuterRing";
            outer.transform.SetParent(parent.transform);
            outer.transform.localPosition = Vector3.zero;
            outer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            outer.transform.localScale = new Vector3(1.0f, 0.02f, 1.0f);
            outer.GetComponent<Renderer>().sharedMaterial = outerMat;
            TargetZone outerZone = outer.AddComponent<TargetZone>();
            SetTargetZone(outerZone, HitType.Normal, 10);

            // Center Ring: Center hit (+25)
            GameObject center = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            center.name = "CenterRing";
            center.transform.SetParent(parent.transform);
            center.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            center.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            center.transform.localScale = new Vector3(0.6f, 0.022f, 0.6f);
            center.GetComponent<Renderer>().sharedMaterial = centerMat;
            TargetZone centerZone = center.AddComponent<TargetZone>();
            SetTargetZone(centerZone, HitType.Center, 25);

            // Bullseye Ring: Bullseye hit (+50)
            GameObject bullseye = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bullseye.name = "BullseyeRing";
            bullseye.transform.SetParent(parent.transform);
            bullseye.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            bullseye.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bullseye.transform.localScale = new Vector3(0.24f, 0.025f, 0.24f);
            bullseye.GetComponent<Renderer>().sharedMaterial = bullseyeMat;
            TargetZone bullseyeZone = bullseye.AddComponent<TargetZone>();
            SetTargetZone(bullseyeZone, HitType.Bullseye, 50);
        }

        private static void SetTargetZone(TargetZone zone, HitType type, int points)
        {
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("hitType").enumValueIndex = (int)type;
            so.FindProperty("customPoints").intValue = points;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateWeaponPrefab(string path, WeaponType type, string weaponName, float fireRate, float maxRange, int magCap, float reloadDuration, Material gunMat, Material tracerMat, AudioClip fireClip, AudioClip emptyClip, AudioClip reloadClip)
        {
            AssetDatabase.DeleteAsset(path);

            GameObject root = new GameObject(weaponName);
            WeaponAmmo ammo = root.AddComponent<WeaponAmmo>();
            SerializedObject soAmmo = new SerializedObject(ammo);
            soAmmo.FindProperty("magazineCapacity").intValue = magCap;
            soAmmo.FindProperty("reloadDuration").floatValue = reloadDuration;
            soAmmo.ApplyModifiedPropertiesWithoutUndo();

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.5f;

            LineRenderer tracer = root.AddComponent<LineRenderer>();
            tracer.enabled = false;
            tracer.startWidth = 0.015f;
            tracer.endWidth = 0.01f;
            tracer.sharedMaterial = tracerMat;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = gunMat;

            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(root.transform);

            if (type == WeaponType.Pistol)
            {
                body.transform.localPosition = new Vector3(0f, 0f, 0.06f);
                body.transform.localScale = new Vector3(0.04f, 0.06f, 0.22f);

                GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                grip.name = "Grip";
                grip.transform.SetParent(root.transform);
                grip.transform.localPosition = new Vector3(0f, -0.08f, 0f);
                grip.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                grip.transform.localScale = new Vector3(0.035f, 0.12f, 0.05f);
                UnityEngine.Object.DestroyImmediate(grip.GetComponent<Collider>());
                grip.GetComponent<Renderer>().sharedMaterial = gunMat;

                muzzle.transform.localPosition = new Vector3(0f, 0.02f, 0.18f);
            }
            else
            {
                body.transform.localPosition = new Vector3(0f, 0.02f, 0.2f);
                body.transform.localScale = new Vector3(0.05f, 0.08f, 0.6f);

                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "Barrel";
                barrel.transform.SetParent(root.transform);
                barrel.transform.localPosition = new Vector3(0f, 0.03f, 0.55f);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                barrel.transform.localScale = new Vector3(0.025f, 0.2f, 0.025f);
                UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
                barrel.GetComponent<Renderer>().sharedMaterial = gunMat;

                GameObject stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stock.name = "Stock";
                stock.transform.SetParent(root.transform);
                stock.transform.localPosition = new Vector3(0f, -0.03f, -0.18f);
                stock.transform.localScale = new Vector3(0.04f, 0.09f, 0.25f);
                UnityEngine.Object.DestroyImmediate(stock.GetComponent<Collider>());
                stock.GetComponent<Renderer>().sharedMaterial = gunMat;

                muzzle.transform.localPosition = new Vector3(0f, 0.03f, 0.75f);
            }

            WeaponController weapon = root.AddComponent<WeaponController>();
            SerializedObject soWpn = new SerializedObject(weapon);
            soWpn.FindProperty("weaponType").enumValueIndex = (int)type;
            soWpn.FindProperty("weaponName").stringValue = weaponName;
            soWpn.FindProperty("fireRate").floatValue = fireRate;
            soWpn.FindProperty("maxRange").floatValue = maxRange;
            soWpn.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
            soWpn.FindProperty("audioSource").objectReferenceValue = audio;
            soWpn.FindProperty("fireSound").objectReferenceValue = fireClip;
            soWpn.FindProperty("emptySound").objectReferenceValue = emptyClip;
            soWpn.FindProperty("reloadSound").objectReferenceValue = reloadClip;
            soWpn.FindProperty("tracerLine").objectReferenceValue = tracer;
            soWpn.FindProperty("weaponAmmo").objectReferenceValue = ammo;
            soWpn.ApplyModifiedPropertiesWithoutUndo();

            // Dedicated AttachTransform for predictable grip and barrel alignment
            GameObject attachGO = new GameObject("AttachTransform");
            attachGO.transform.SetParent(root.transform, false);
            if (type == WeaponType.Pistol)
            {
                attachGO.transform.localPosition = new Vector3(0f, -0.05f, 0.02f);
                attachGO.transform.localRotation = Quaternion.identity;
            }
            else
            {
                attachGO.transform.localPosition = new Vector3(0f, -0.04f, 0.05f);
                attachGO.transform.localRotation = Quaternion.identity;
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var grab = root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.attachTransform = attachGO.transform;
            int defaultLayer = UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Default");
            if (defaultLayer == 0) defaultLayer = 1;
            grab.interactionLayers = defaultLayer;

            VRWeaponInteractable vrInteractable = root.AddComponent<VRWeaponInteractable>();
            SerializedObject soVR = new SerializedObject(vrInteractable);
            soVR.FindProperty("weaponController").objectReferenceValue = weapon;
            soVR.ApplyModifiedPropertiesWithoutUndo();

            var aimRay = root.AddComponent<WeaponAimRay>();
            if (tracerMat != null) aimRay.SetAimMaterial(tracerMat);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateEnvironmentPrefab(string path)
        {
            AssetDatabase.DeleteAsset(path);

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Floor.mat");
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Wall.mat");
            Material tableMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Table.mat");

            GameObject root = new GameObject("ShootingRange_Environment");

            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 10f);
            floor.transform.localScale = new Vector3(20f, 0.2f, 30f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Back Wall
            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "BackWall";
            backWall.transform.SetParent(root.transform);
            backWall.transform.localPosition = new Vector3(0f, 3f, 25f);
            backWall.transform.localScale = new Vector3(20f, 6.2f, 0.5f);
            backWall.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Left Wall
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.SetParent(root.transform);
            leftWall.transform.localPosition = new Vector3(-10f, 3f, 10f);
            leftWall.transform.localScale = new Vector3(0.5f, 6.2f, 30f);
            leftWall.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Right Wall
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.SetParent(root.transform);
            rightWall.transform.localPosition = new Vector3(10f, 3f, 10f);
            rightWall.transform.localScale = new Vector3(0.5f, 6.2f, 30f);
            rightWall.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Shooting Table
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "ShootingCounter";
            table.transform.SetParent(root.transform);
            table.transform.localPosition = new Vector3(0f, 0.45f, 1.2f);
            table.transform.localScale = new Vector3(6.0f, 0.9f, 0.8f);
            table.GetComponent<Renderer>().sharedMaterial = tableMat;

            // Lane Dividers
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject divider = GameObject.CreatePrimitive(PrimitiveType.Cube);
                divider.name = $"Divider_{(i < 0 ? "Left" : "Right")}";
                divider.transform.SetParent(root.transform);
                divider.transform.localPosition = new Vector3(i * 3.0f, 1.3f, 1.2f);
                divider.transform.localScale = new Vector3(0.1f, 1.8f, 1.6f);
                divider.GetComponent<Renderer>().sharedMaterial = wallMat;
            }

            // Rear Wall
            GameObject rearWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rearWall.name = "RearWall";
            rearWall.transform.SetParent(root.transform);
            rearWall.transform.localPosition = new Vector3(0f, 3f, -5f);
            rearWall.transform.localScale = new Vector3(20f, 6.2f, 0.5f);
            rearWall.GetComponent<Renderer>().sharedMaterial = wallMat;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateUIPrefab(string path)
        {
            AssetDatabase.DeleteAsset(path);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                     ?? Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();

            GameObject root = new GameObject("GameUI_Canvas");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            root.AddComponent<VRUIAdapter>();

            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.localScale = Vector3.one; // MUST BE (1, 1, 1)

            UIManager uiManager = root.AddComponent<UIManager>();

            // --- Main Menu Panel ---
            GameObject menuPanel = CreateUIPanel("MainMenuPanel", root.transform, new Color(0.06f, 0.08f, 0.10f, 0.85f));

            ShootingGalleryIntegrationFixer.RebuildMainMenuLayout(menuPanel);

            // --- HUD Panel ---
            GameObject hudPanel = CreateUIPanel("HUDPanel", root.transform, Color.clear);
            Text timerTxt = CreateUIText("TimerText", hudPanel.transform, "TIME: 60s", font, 28, new Vector2(0f, 210f), new Vector2(250f, 40f), Color.white);
            Text scoreTxt = CreateUIText("ScoreText", hudPanel.transform, "SCORE: 0", font, 24, new Vector2(-320f, 210f), new Vector2(220f, 36f), Color.green);
            Text accTxt = CreateUIText("AccuracyText", hudPanel.transform, "ACCURACY: 0.0%", font, 20, new Vector2(-320f, 175f), new Vector2(220f, 30f), Color.cyan);
            Text comboTxt = CreateUIText("ComboText", hudPanel.transform, "COMBO: --", font, 22, new Vector2(320f, 210f), new Vector2(240f, 36f), Color.yellow);
            Text ammoTxt = CreateUIText("AmmoText", hudPanel.transform, "AMMO: 12 / 12", font, 24, new Vector2(320f, -200f), new Vector2(220f, 36f), Color.white);

            HUDController hudController = hudPanel.AddComponent<HUDController>();
            SerializedObject soHud = new SerializedObject(hudController);
            soHud.FindProperty("timerText").objectReferenceValue = timerTxt;
            soHud.FindProperty("scoreText").objectReferenceValue = scoreTxt;
            soHud.FindProperty("accuracyText").objectReferenceValue = accTxt;
            soHud.FindProperty("comboText").objectReferenceValue = comboTxt;
            soHud.FindProperty("ammoText").objectReferenceValue = ammoTxt;
            soHud.ApplyModifiedPropertiesWithoutUndo();

            // --- Results Panel ---
            GameObject resultsPanel = CreateUIPanel("ResultsPanel", root.transform, new Color(0.12f, 0.14f, 0.18f, 0.95f));
            CreateUIText("ResultsHeader", resultsPanel.transform, "CHALLENGE RESULTS", font, 32, new Vector2(0f, 170f), new Vector2(400f, 50f), Color.yellow);
            Text resFinalScore = CreateUIText("FinalScore", resultsPanel.transform, "FINAL SCORE: 0", font, 26, new Vector2(0f, 110f), new Vector2(350f, 36f), Color.green);
            Text resAcc = CreateUIText("Accuracy", resultsPanel.transform, "ACCURACY: 0.0%", font, 22, new Vector2(0f, 65f), new Vector2(350f, 30f), Color.white);
            Text resShots = CreateUIText("ShotsFired", resultsPanel.transform, "SHOTS FIRED: 0", font, 20, new Vector2(0f, 25f), new Vector2(350f, 30f), Color.white);
            Text resHits = CreateUIText("Hits", resultsPanel.transform, "HITS: 0", font, 20, new Vector2(0f, -15f), new Vector2(350f, 30f), Color.white);
            Text resCombo = CreateUIText("BestCombo", resultsPanel.transform, "BEST COMBO: x0", font, 20, new Vector2(0f, -55f), new Vector2(350f, 30f), Color.white);
            Text resDiff = CreateUIText("Difficulty", resultsPanel.transform, "DIFFICULTY: Medium", font, 20, new Vector2(0f, -95f), new Vector2(350f, 30f), Color.yellow);

            Button resRestart = CreateUIButton("RestartButton", resultsPanel.transform, "PLAY AGAIN", font, new Vector2(-110f, -160f), new Vector2(180f, 44f), new Color(0.2f, 0.6f, 0.3f));
            Button resMenu = CreateUIButton("MainMenuButton", resultsPanel.transform, "MAIN MENU", font, new Vector2(110f, -160f), new Vector2(180f, 44f), new Color(0.3f, 0.4f, 0.5f));

            ResultsUI resultsUI = resultsPanel.AddComponent<ResultsUI>();
            SerializedObject soRes = new SerializedObject(resultsUI);
            soRes.FindProperty("finalScoreText").objectReferenceValue = resFinalScore;
            soRes.FindProperty("accuracyText").objectReferenceValue = resAcc;
            soRes.FindProperty("shotsFiredText").objectReferenceValue = resShots;
            soRes.FindProperty("shotsHitText").objectReferenceValue = resHits;
            soRes.FindProperty("bestComboText").objectReferenceValue = resCombo;
            soRes.FindProperty("difficultyText").objectReferenceValue = resDiff;
            soRes.FindProperty("restartButton").objectReferenceValue = resRestart;
            soRes.FindProperty("mainMenuButton").objectReferenceValue = resMenu;
            soRes.ApplyModifiedPropertiesWithoutUndo();

            // --- Pause Panel ---
            GameObject pausePanel = CreateUIPanel("PausePanel", root.transform, new Color(0.04f, 0.05f, 0.07f, 0.85f));

            // Centered Pause Card: 500 x 350
            GameObject pauseCard = new GameObject("PauseCard", typeof(RectTransform));
            pauseCard.transform.SetParent(pausePanel.transform, false);
            RectTransform pauseCardRt = pauseCard.GetComponent<RectTransform>();
            pauseCardRt.anchorMin = new Vector2(0.5f, 0.5f);
            pauseCardRt.anchorMax = new Vector2(0.5f, 0.5f);
            pauseCardRt.anchoredPosition = Vector2.zero;
            pauseCardRt.sizeDelta = new Vector2(500f, 350f);
            Image pauseCardImg = pauseCard.AddComponent<Image>();
            pauseCardImg.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

            CreateUIText("PauseTitle", pauseCard.transform, "GAME PAUSED", font, 36, new Vector2(0f, 90f), new Vector2(440f, 50f), Color.white);
            Button pauseRestartBtn = CreateUIButton("RestartButton", pauseCard.transform, "RESTART", font, new Vector2(0f, 10f), new Vector2(360f, 58f), new Color(0.2f, 0.55f, 0.35f), 22);
            Button pauseMenuBtn = CreateUIButton("MainMenuButton", pauseCard.transform, "MAIN MENU", font, new Vector2(0f, -70f), new Vector2(360f, 58f), new Color(0.25f, 0.4f, 0.6f), 22);

            PauseUI pauseUI = pausePanel.AddComponent<PauseUI>();
            SerializedObject soPause = new SerializedObject(pauseUI);
            soPause.FindProperty("restartButton").objectReferenceValue = pauseRestartBtn;
            soPause.FindProperty("mainMenuButton").objectReferenceValue = pauseMenuBtn;
            soPause.ApplyModifiedPropertiesWithoutUndo();

            // Wire UIManager
            SerializedObject soUiMgr = new SerializedObject(uiManager);
            soUiMgr.FindProperty("mainMenuPanel").objectReferenceValue = menuPanel;
            soUiMgr.FindProperty("hudPanel").objectReferenceValue = hudPanel;
            soUiMgr.FindProperty("resultsPanel").objectReferenceValue = resultsPanel;
            soUiMgr.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            soUiMgr.FindProperty("resultsUI").objectReferenceValue = resultsUI;
            soUiMgr.FindProperty("hudController").objectReferenceValue = hudController;
            soUiMgr.ApplyModifiedPropertiesWithoutUndo();

            menuPanel.SetActive(true);
            hudPanel.SetActive(false);
            resultsPanel.SetActive(false);
            pausePanel.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static GameObject CreateUIPanel(string name, Transform parent, Color bgColor)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (bgColor.a > 0f)
            {
                Image img = panel.AddComponent<Image>();
                img.color = bgColor;
            }
            return panel;
        }

        private static Text CreateUIText(string name, Transform parent, string content, Font font, int size, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Text txt = obj.AddComponent<Text>();
            txt.text = content;
            if (font != null) txt.font = font;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            return txt;
        }

        private static Button CreateUIButton(string name, Transform parent, string label, Font font, Vector2 anchoredPos, Vector2 sizeDelta, Color btnColor, int fontSize = 18)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = obj.AddComponent<Image>();
            img.color = btnColor;

            Button btn = obj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.text = label;
            if (font != null) txt.font = font;
            txt.fontSize = fontSize;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }
        #endregion

        #region 5. Scene Assembly
        private static void BuildScene()
        {
            Debug.Log("[VR Shooting Gallery] Rebuilding ShootingRange scene...");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.8f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Global Volume
            GameObject volumeObj = new GameObject("Global Volume");
            Volume volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            string[] volumeProfiles = AssetDatabase.FindAssets("t:VolumeProfile");
            if (volumeProfiles.Length > 0)
            {
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(volumeProfiles[0]));
            }

            // Environment
            GameObject envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/ShootingRange_Environment.prefab");
            GameObject envInstance = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
            envInstance.name = "ShootingRange_Environment";

            // Game Managers
            GameObject managersObj = new GameObject("--- GAME MANAGERS ---");
            GameStateManager gameStateMgr = managersObj.AddComponent<GameStateManager>();
            ScoreManager scoreMgr = managersObj.AddComponent<ScoreManager>();
            DifficultyManager diffMgr = managersObj.AddComponent<DifficultyManager>();
            GameManager gameMgr = managersObj.AddComponent<GameManager>();

            // Setup DifficultyManager
            SerializedObject soDiff = new SerializedObject(diffMgr);
            soDiff.FindProperty("defaultDifficulty").enumValueIndex = (int)DifficultyLevel.Medium;
            SerializedProperty diffList = soDiff.FindProperty("availableDifficulties");
            diffList.ClearArray();
            string[] diffPaths = {
                "Assets/ScriptableObjects/DifficultyData_Easy.asset",
                "Assets/ScriptableObjects/DifficultyData_Medium.asset",
                "Assets/ScriptableObjects/DifficultyData_Hard.asset"
            };
            for (int i = 0; i < diffPaths.Length; i++)
            {
                diffList.InsertArrayElementAtIndex(i);
                diffList.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<DifficultyData>(diffPaths[i]);
            }
            soDiff.ApplyModifiedPropertiesWithoutUndo();

            // Setup GameManager
            SerializedObject soGame = new SerializedObject(gameMgr);
            soGame.FindProperty("gameStateManager").objectReferenceValue = gameStateMgr;
            soGame.FindProperty("scoreManager").objectReferenceValue = scoreMgr;
            soGame.FindProperty("difficultyManager").objectReferenceValue = diffMgr;
            soGame.FindProperty("challengeDuration").floatValue = 60.0f;
            soGame.ApplyModifiedPropertiesWithoutUndo();

            // Setup TargetManager
            TargetManager targetMgr = managersObj.AddComponent<TargetManager>();

            // Target Prefabs
            GameObject staticPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Targets/StaticTarget.prefab");
            GameObject movingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Targets/MovingTarget.prefab");
            GameObject popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Targets/PopupTarget.prefab");

            // 28 Well-Distributed Tiered Spawn Points across the range (Clear lines of sight, >= 1.5m 3D separation)
            GameObject spawnPointsGroup = new GameObject("--- SPAWN POINTS ---");
            Vector3[] spawnPos = {
                // Tier 1 (Close Distance: Z = 6.0 - 7.5m, Y = 0.0m): 8 points
                new Vector3(-4.2f, 0.0f,  6.8f),
                new Vector3(-3.0f, 0.0f,  6.2f),
                new Vector3(-1.8f, 0.0f,  6.8f),
                new Vector3(-0.6f, 0.0f,  6.0f),
                new Vector3( 0.6f, 0.0f,  6.0f),
                new Vector3( 1.8f, 0.0f,  6.8f),
                new Vector3( 3.0f, 0.0f,  6.2f),
                new Vector3( 4.2f, 0.0f,  6.8f),

                // Tier 2 (Mid-Close Distance: Z = 9.5 - 11.5m, Y = 0.2m): 9 points
                new Vector3(-5.6f, 0.2f, 10.5f),
                new Vector3(-4.2f, 0.2f, 10.0f),
                new Vector3(-2.8f, 0.2f, 11.2f),
                new Vector3(-1.4f, 0.2f, 10.0f),
                new Vector3( 0.0f, 0.3f, 11.0f),
                new Vector3( 1.4f, 0.2f, 10.0f),
                new Vector3( 2.8f, 0.2f, 11.2f),
                new Vector3( 4.2f, 0.2f, 10.0f),
                new Vector3( 5.6f, 0.2f, 10.5f),

                // Tier 3 (Mid-Far Distance: Z = 13.5 - 15.5m, Y = 0.4m): 10 points
                new Vector3(-6.8f, 0.4f, 14.5f),
                new Vector3(-5.2f, 0.4f, 13.8f),
                new Vector3(-3.8f, 0.4f, 14.8f),
                new Vector3(-2.2f, 0.4f, 13.8f),
                new Vector3(-0.8f, 0.4f, 14.8f),
                new Vector3( 0.8f, 0.4f, 14.8f),
                new Vector3( 2.2f, 0.4f, 13.8f),
                new Vector3( 3.8f, 0.4f, 14.8f),
                new Vector3( 5.2f, 0.4f, 13.8f),
                new Vector3( 6.8f, 0.4f, 14.5f),

                // Tier 4 (Far Distance: Z = 18.0 - 21.0m, Y = 0.6m): 9 points
                new Vector3(-7.4f, 0.6f, 19.0f),
                new Vector3(-5.5f, 0.6f, 18.5f),
                new Vector3(-3.6f, 0.6f, 19.5f),
                new Vector3(-1.8f, 0.6f, 18.5f),
                new Vector3( 0.0f, 0.7f, 19.5f),
                new Vector3( 1.8f, 0.6f, 18.5f),
                new Vector3( 3.6f, 0.6f, 19.5f),
                new Vector3( 5.5f, 0.6f, 18.5f),
                new Vector3( 7.4f, 0.6f, 19.0f)
            };
            List<Transform> spawnPointTransforms = new List<Transform>();
            for (int i = 0; i < spawnPos.Length; i++)
            {
                GameObject sp = new GameObject($"SpawnPoint_{i + 1}");
                sp.transform.SetParent(spawnPointsGroup.transform);
                sp.transform.position = spawnPos[i];
                spawnPointTransforms.Add(sp.transform);
            }

            // Wire TargetManager
            SerializedObject soTargetMgr = new SerializedObject(targetMgr);
            soTargetMgr.FindProperty("difficultyManager").objectReferenceValue = diffMgr;
            soTargetMgr.FindProperty("gameManager").objectReferenceValue = gameMgr;

            SerializedProperty prefabsProp = soTargetMgr.FindProperty("targetPrefabs");
            prefabsProp.ClearArray();
            prefabsProp.InsertArrayElementAtIndex(0);
            prefabsProp.GetArrayElementAtIndex(0).objectReferenceValue = staticPrefab.GetComponent<TargetBase>();
            prefabsProp.InsertArrayElementAtIndex(1);
            prefabsProp.GetArrayElementAtIndex(1).objectReferenceValue = movingPrefab.GetComponent<TargetBase>();
            prefabsProp.InsertArrayElementAtIndex(2);
            prefabsProp.GetArrayElementAtIndex(2).objectReferenceValue = popupPrefab.GetComponent<TargetBase>();

            SerializedProperty spawnPointsProp = soTargetMgr.FindProperty("spawnPoints");
            spawnPointsProp.ClearArray();
            for (int i = 0; i < spawnPointTransforms.Count; i++)
            {
                spawnPointsProp.InsertArrayElementAtIndex(i);
                spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPointTransforms[i];
            }

            SerializedProperty sceneTargetsProp = soTargetMgr.FindProperty("sceneTargets");
            sceneTargetsProp.ClearArray();

            soTargetMgr.FindProperty("minimumTargetSeparation").floatValue = 1.5f;
            soTargetMgr.FindProperty("easySeparation").floatValue = 1.75f;
            soTargetMgr.FindProperty("mediumSeparation").floatValue = 1.5f;
            soTargetMgr.FindProperty("hardSeparation").floatValue = 1.25f;
            soTargetMgr.FindProperty("minimumAngularSeparation").floatValue = 3.5f;
            soTargetMgr.FindProperty("easyAngularSeparation").floatValue = 4.5f;
            soTargetMgr.FindProperty("mediumAngularSeparation").floatValue = 3.5f;
            soTargetMgr.FindProperty("hardAngularSeparation").floatValue = 2.6f;
            soTargetMgr.FindProperty("maxSpawnCandidatesToCheck").intValue = 36;

            soTargetMgr.ApplyModifiedPropertiesWithoutUndo();

            // Player Rig
            GameObject player = new GameObject("PlayerRig");
            player.transform.position = Vector3.zero;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            GameObject head = new GameObject("Main Camera");
            head.tag = "MainCamera";
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            Camera cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            head.AddComponent<AudioListener>();

            // VR Controller Anchors
            GameObject leftHand = new GameObject("LeftControllerAnchor");
            leftHand.transform.SetParent(player.transform);
            leftHand.transform.localPosition = new Vector3(-0.25f, 1.25f, 0.4f);

            GameObject rightHand = new GameObject("RightControllerAnchor");
            rightHand.transform.SetParent(player.transform);
            rightHand.transform.localPosition = new Vector3(0.25f, 1.25f, 0.4f);

            // Weapon Mount
            GameObject weaponMount = new GameObject("WeaponMount");
            weaponMount.transform.SetParent(head.transform);
            weaponMount.transform.localPosition = new Vector3(0.22f, -0.18f, 0.42f);

            GameObject pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Pistol.prefab");
            GameObject riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Rifle.prefab");

            GameObject pistolInstance = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, weaponMount.transform);
            pistolInstance.name = "Pistol";
            pistolInstance.transform.localPosition = Vector3.zero;
            pistolInstance.transform.localRotation = Quaternion.identity;

            GameObject rifleInstance = (GameObject)PrefabUtility.InstantiatePrefab(riflePrefab, weaponMount.transform);
            rifleInstance.name = "Rifle";
            rifleInstance.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            rifleInstance.transform.localRotation = Quaternion.identity;
            rifleInstance.SetActive(false);

            WeaponManager weaponMgr = player.AddComponent<WeaponManager>();
            SerializedObject soWpnMgr = new SerializedObject(weaponMgr);
            soWpnMgr.FindProperty("pistol").objectReferenceValue = pistolInstance.GetComponent<WeaponController>();
            soWpnMgr.FindProperty("rifle").objectReferenceValue = rifleInstance.GetComponent<WeaponController>();
            soWpnMgr.FindProperty("defaultWeaponType").enumValueIndex = (int)WeaponType.Pistol;
            soWpnMgr.ApplyModifiedPropertiesWithoutUndo();

            PCPlayerController pcCtrl = player.AddComponent<PCPlayerController>();
            SerializedObject soPC = new SerializedObject(pcCtrl);
            soPC.FindProperty("moveSpeed").floatValue = 4.0f;
            soPC.FindProperty("mouseSensitivity").floatValue = 2.0f;
            soPC.FindProperty("playerCamera").objectReferenceValue = head.transform;
            soPC.FindProperty("characterController").objectReferenceValue = cc;
            soPC.FindProperty("forceEnablePC").boolValue = false;
            soPC.FindProperty("weaponManager").objectReferenceValue = weaponMgr;
            soPC.FindProperty("gameManager").objectReferenceValue = gameMgr;
            soPC.ApplyModifiedPropertiesWithoutUndo();

            // UI Canvas
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameUI_Canvas.prefab");
            GameObject uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab);
            uiInstance.name = "GameUI_Canvas";
            uiInstance.GetComponent<RectTransform>().localScale = Vector3.one;

            // Wire UI Canvas Scene References
            UIManager uiMgr = uiInstance.GetComponent<UIManager>();
            if (uiMgr != null)
            {
                SerializedObject soUIMgr = new SerializedObject(uiMgr);
                soUIMgr.FindProperty("gameStateManager").objectReferenceValue = gameStateMgr;
                soUIMgr.ApplyModifiedPropertiesWithoutUndo();
            }

            MainMenuUI menuUI = uiInstance.GetComponentInChildren<MainMenuUI>(true);
            if (menuUI != null)
            {
                SerializedObject soMenuUI = new SerializedObject(menuUI);
                soMenuUI.FindProperty("gameManager").objectReferenceValue = gameMgr;
                soMenuUI.FindProperty("difficultyManager").objectReferenceValue = diffMgr;
                soMenuUI.ApplyModifiedPropertiesWithoutUndo();
            }

            HUDController hudCtrl = uiInstance.GetComponentInChildren<HUDController>(true);
            if (hudCtrl != null)
            {
                SerializedObject soHudCtrl = new SerializedObject(hudCtrl);
                soHudCtrl.FindProperty("gameManager").objectReferenceValue = gameMgr;
                soHudCtrl.FindProperty("scoreManager").objectReferenceValue = scoreMgr;
                soHudCtrl.FindProperty("weaponManager").objectReferenceValue = weaponMgr;
                soHudCtrl.ApplyModifiedPropertiesWithoutUndo();
            }

            ResultsUI resUI = uiInstance.GetComponentInChildren<ResultsUI>(true);
            if (resUI != null)
            {
                SerializedObject soResUI = new SerializedObject(resUI);
                soResUI.FindProperty("gameManager").objectReferenceValue = gameMgr;
                soResUI.FindProperty("scoreManager").objectReferenceValue = scoreMgr;
                soResUI.FindProperty("difficultyManager").objectReferenceValue = diffMgr;
                soResUI.ApplyModifiedPropertiesWithoutUndo();
            }

            PauseUI pauseUI = uiInstance.GetComponentInChildren<PauseUI>(true);
            if (pauseUI != null)
            {
                SerializedObject soPauseUI = new SerializedObject(pauseUI);
                soPauseUI.FindProperty("gameManager").objectReferenceValue = gameMgr;
                soPauseUI.ApplyModifiedPropertiesWithoutUndo();
            }

            // Event System with single active module adapter
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            var inputModule = eventSystemObj.AddComponent<InputSystemUIInputModule>();
            var xrModule = eventSystemObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            xrModule.enableXRInput = true;
            xrModule.enableMouseInput = true;
            xrModule.enabled = false; // Disabled by default for PC mode
            var adapter = eventSystemObj.AddComponent<EventSystemPlatformAdapter>();
            adapter.ResolveReferences();

            ShootingGalleryIntegrationFixer.ConfigurePlayerRigAndControllers();
            ShootingGalleryIntegrationFixer.ConfigureSceneWeapons();

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Register in EditorBuildSettings
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool hasShootingRange = false;
            foreach (var s in buildScenes)
            {
                if (s.path == ScenePath)
                {
                    hasShootingRange = true;
                    s.enabled = true;
                    break;
                }
            }
            if (!hasShootingRange)
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        #endregion

        #region 6. GUID Integrity Verification
        public static void VerifyGuidIntegrity()
        {
            string[] checkFiles = {
                "Assets/Materials/Mat_Floor.mat",
                "Assets/Materials/Mat_Wall.mat",
                "Assets/Materials/Mat_Table.mat",
                "Assets/Materials/Mat_TargetOuter.mat",
                "Assets/Materials/Mat_TargetCenter.mat",
                "Assets/Materials/Mat_TargetBullseye.mat",
                "Assets/Materials/Mat_GunMetal.mat",
                "Assets/Materials/Mat_Tracer.mat",
                "Assets/Prefabs/Targets/StaticTarget.prefab",
                "Assets/Prefabs/Targets/MovingTarget.prefab",
                "Assets/Prefabs/Targets/PopupTarget.prefab",
                "Assets/Prefabs/Environment/ShootingRange_Environment.prefab",
                "Assets/Prefabs/Weapons/Pistol.prefab",
                "Assets/Prefabs/Weapons/Rifle.prefab",
                "Assets/Prefabs/UI/GameUI_Canvas.prefab",
                "Assets/ScriptableObjects/DifficultyData_Easy.asset",
                "Assets/ScriptableObjects/DifficultyData_Medium.asset",
                "Assets/ScriptableObjects/DifficultyData_Hard.asset",
                "Assets/Audio/gunshot_pistol.wav",
                "Assets/Audio/gunshot_rifle.wav",
                "Assets/Audio/gun_empty.wav",
                "Assets/Audio/gun_reload.wav",
                "Assets/Audio/target_hit.wav",
                "Assets/Audio/target_bullseye.wav",
                "Assets/Audio/ui_click.wav"
            };

            List<string> reportLines = new List<string>();
            reportLines.Add($"GUID INTEGRITY REPORT - {DateTime.UtcNow:O}");
            reportLines.Add("==================================================");

            bool allValid = true;
            foreach (var relPath in checkFiles)
            {
                string metaPath = relPath + ".meta";
                if (!File.Exists(metaPath))
                {
                    reportLines.Add($"[MISSING META] {relPath}");
                    allValid = false;
                    continue;
                }

                string metaContent = File.ReadAllText(metaPath);
                var match = Regex.Match(metaContent, @"guid:\s*([a-fA-F0-9]{32})");
                if (match.Success)
                {
                    string guid = match.Groups[1].Value;
                    if (guid.StartsWith("m1f") || guid.StartsWith("p1f") || guid.StartsWith("a1f") || guid.StartsWith("d1f"))
                    {
                        reportLines.Add($"[PLACEHOLDER GUID DETECTED] {relPath} -> {guid}");
                        allValid = false;
                    }
                    else
                    {
                        reportLines.Add($"[VALID GUID] {relPath} -> {guid}");
                    }
                }
                else
                {
                    reportLines.Add($"[INVALID GUID FORMAT] {relPath}");
                    allValid = false;
                }
            }

            reportLines.Add("==================================================");
            reportLines.Add(allValid ? "ALL GUIDS VALID AND REPAIRED!" : "REPAIR INCOMPLETE - CORRUPTED GUIDS REMAIN!");

            string report = string.Join("\n", reportLines);
            Debug.Log(report);

            try
            {
                File.WriteAllText("Logs/GuidIntegrityReport.txt", report);
            }
            catch { }
        }
        #endregion
    }
}
