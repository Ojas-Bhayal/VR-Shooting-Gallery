using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using VRShootingGallery.Core;
using VRShootingGallery.Weapons;
using VRShootingGallery.VR;
using VRShootingGallery.UI;
using VRShootingGallery.PC;
using VRShootingGallery.Difficulty;

namespace VRShootingGallery.Editor
{
    public static class ShootingGalleryIntegrationFixer
    {
        private const string ScenePath = "Assets/Scenes/ShootingRange.unity";
        private const string BulletImpactPrefabPath = "Assets/Prefabs/VFX/BulletImpact.prefab";
        private const string PistolPrefabPath = "Assets/Prefabs/Weapons/Pistol.prefab";
        private const string RiflePrefabPath = "Assets/Prefabs/Weapons/Rifle.prefab";

        [MenuItem("VR Shooting Gallery/Run Final Integration Fixer")]
        public static void RunFullFix()
        {
            Debug.Log("[IntegrationFixer] Starting full project repair & VR integration...");

            CleanBuildSettingsAndBackups();
            ConfigureXRSettings();
            GameObject bulletImpact = SetupBulletImpactVFX();
            SetupAudioSpatialBlend();
            SetupWeaponsXRInteractable(bulletImpact);
            SetupSceneAndPlayerRig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IntegrationFixer] Final integration completed successfully!");
        }

        public static void CleanBuildSettingsAndBackups()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[IntegrationFixer] EditorBuildSettings updated: only ShootingRange.unity included.");

            string backupPath = "Assets/Scenes/ShootingRange.unity.backup";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
                Debug.Log("[IntegrationFixer] Removed ShootingRange.unity.backup.");
            }
        }

        public static void ConfigureXRSettings()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget buildTargetSettings) || buildTargetSettings == null)
            {
                var assets = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (assets.Length > 0)
                {
                    string p = AssetDatabase.GUIDToAssetPath(assets[0]);
                    buildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(p);
                    if (buildTargetSettings != null)
                    {
                        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
                    }
                }
            }

            var xrGeneralSettings = buildTargetSettings != null ? buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Standalone) : null;
            if (xrGeneralSettings == null)
            {
                xrGeneralSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                if (buildTargetSettings != null)
                {
                    buildTargetSettings.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, xrGeneralSettings);
                }
            }

            var xrManagerSettings = xrGeneralSettings.Manager;
            if (xrManagerSettings == null)
            {
                xrManagerSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
                xrGeneralSettings.Manager = xrManagerSettings;
            }

            XRPackageMetadataStore.AssignLoader(xrManagerSettings, typeof(OpenXRLoader).FullName, BuildTargetGroup.Standalone);
            xrGeneralSettings.InitManagerOnStart = true;
            EditorUtility.SetDirty(xrGeneralSettings);
            EditorUtility.SetDirty(xrManagerSettings);
            Debug.Log("[IntegrationFixer] Assigned OpenXRLoader for Standalone build target.");

            var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (openXRSettings == null)
            {
                openXRSettings = ScriptableObject.CreateInstance<OpenXRSettings>();
            }

            var features = openXRSettings.GetFeatures();
            foreach (var f in features)
            {
                if (f is OculusTouchControllerProfile ||
                    f is MetaQuestTouchProControllerProfile ||
                    f is EyeGazeInteraction)
                {
                    f.enabled = true;
                    EditorUtility.SetDirty(f);
                    Debug.Log($"[IntegrationFixer] Enabled OpenXR feature: {f.name}");
                }
            }
            EditorUtility.SetDirty(openXRSettings);
        }

        public static GameObject SetupBulletImpactVFX()
        {
            if (!Directory.Exists("Assets/Prefabs/VFX"))
            {
                Directory.CreateDirectory("Assets/Prefabs/VFX");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletImpactPrefabPath);
            if (prefab == null)
            {
                GameObject tempGO = new GameObject("BulletImpact");
                ParticleSystem ps = tempGO.AddComponent<ParticleSystem>();

                var main = ps.main;
                main.duration = 0.2f;
                main.loop = false;
                main.startLifetime = 0.25f;
                main.startSpeed = 5.0f;
                main.startSize = 0.06f;
                main.startColor = new Color(1.0f, 0.7f, 0.2f, 1.0f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 20;

                var emission = ps.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 12) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.04f;

                prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, BulletImpactPrefabPath);
                UnityEngine.Object.DestroyImmediate(tempGO);
                Debug.Log("[IntegrationFixer] Created BulletImpact.prefab successfully.");
            }

            return prefab;
        }

        public static void SetupAudioSpatialBlend()
        {
            string[] targetPrefabs = new string[]
            {
                "Assets/Prefabs/Targets/StaticTarget.prefab",
                "Assets/Prefabs/Targets/MovingTarget.prefab",
                "Assets/Prefabs/Targets/PopupTarget.prefab"
            };

            foreach (var path in targetPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root != null)
                {
                    var audioSources = root.GetComponentsInChildren<AudioSource>(true);
                    foreach (var a in audioSources)
                    {
                        a.spatialBlend = 1.0f;
                        a.minDistance = 1.0f;
                        a.maxDistance = 25.0f;
                        a.rolloffMode = AudioRolloffMode.Logarithmic;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                    Debug.Log($"[IntegrationFixer] Configured 3D spatial audio on {path}");
                }
            }
        }

        public static void SetupWeaponsXRInteractable(GameObject bulletImpact)
        {
            string[] weapons = new string[] { PistolPrefabPath, RiflePrefabPath };
            Material tracerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Tracer.mat");
            int defaultInteractionLayer = InteractionLayerMask.GetMask("Default");
            if (defaultInteractionLayer == 0) defaultInteractionLayer = 1;

            foreach (var path in weapons)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root != null)
                {
                    bool isPistol = path.Contains("Pistol");

                    // Ensure Rigidbody for XR grab interaction
                    var rb = root.GetComponent<Rigidbody>();
                    if (rb == null) rb = root.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;

                    // Dedicated AttachTransform for predictable grip and barrel alignment
                    Transform attachTrans = root.transform.Find("AttachTransform");
                    if (attachTrans == null)
                    {
                        GameObject attachGO = new GameObject("AttachTransform");
                        attachTrans = attachGO.transform;
                        attachTrans.SetParent(root.transform, false);
                    }

                    if (isPistol)
                    {
                        attachTrans.localPosition = new Vector3(0f, -0.05f, 0.02f);
                        attachTrans.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        attachTrans.localPosition = new Vector3(0f, -0.04f, 0.05f);
                        attachTrans.localRotation = Quaternion.identity;
                    }

                    // Ensure XRGrabInteractable
                    var grab = root.GetComponent<XRGrabInteractable>();
                    if (grab == null) grab = root.AddComponent<XRGrabInteractable>();
                    grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                    grab.throwOnDetach = false;
                    grab.attachTransform = attachTrans;
                    grab.interactionLayers = defaultInteractionLayer;

                    // Ensure WeaponController has bulletImpactPrefab and MuzzlePoint
                    var weaponCtrl = root.GetComponent<WeaponController>();
                    if (weaponCtrl != null)
                    {
                        var so = new SerializedObject(weaponCtrl);
                        if (bulletImpact != null)
                        {
                            var impactProp = so.FindProperty("bulletImpactPrefab");
                            if (impactProp != null) impactProp.objectReferenceValue = bulletImpact;
                        }
                        var muzzleProp = so.FindProperty("muzzlePoint");
                        if (muzzleProp != null && muzzleProp.objectReferenceValue == null)
                        {
                            var m = root.transform.Find("MuzzlePoint");
                            if (m != null) muzzleProp.objectReferenceValue = m;
                        }
                        so.ApplyModifiedProperties();
                    }

                    // Ensure VRWeaponInteractable exists
                    var vrInteractable = root.GetComponent<VRWeaponInteractable>();
                    if (vrInteractable == null)
                    {
                        root.AddComponent<VRWeaponInteractable>();
                    }

                    // Ensure WeaponAimRay exists and has tracer material
                    var aimRay = root.GetComponent<WeaponAimRay>();
                    if (aimRay == null) aimRay = root.AddComponent<WeaponAimRay>();
                    if (tracerMat != null) aimRay.SetAimMaterial(tracerMat);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                    Debug.Log($"[IntegrationFixer] Configured AttachTransform, XRGrabInteractable, and WeaponAimRay on {path}");
                }
            }
        }

        public static void SetupSceneAndPlayerRig()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Ensure XRInteractionManager exists
            var interactionManager = UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>();
            if (interactionManager == null)
            {
                var managersGO = GameObject.Find("--- GAME MANAGERS ---");
                GameObject imGO = new GameObject("XR Interaction Manager");
                interactionManager = imGO.AddComponent<XRInteractionManager>();
                if (managersGO != null) imGO.transform.SetParent(managersGO.transform);
                Debug.Log("[IntegrationFixer] Created XRInteractionManager in scene.");
            }

            // 2. Configure EventSystem for VR UI & PC UI
            ConfigureEventSystem();

            // 3. Configure Canvas & VR UI Adapter
            ConfigureCanvasAndUI();

            // 4. Configure PlayerRig, Controller Anchors, and VRControllerPreference
            ConfigurePlayerRigAndControllers();

            // 5. Configure scene weapons under WeaponMount
            ConfigureSceneWeapons();

            // 6. Configure environment rear wall and bounds
            ConfigureEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntegrationFixer] Scene saved successfully.");
        }

        public static void ConfigureEventSystem()
        {
            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem eventSystem = null;

            if (eventSystems.Length > 0)
            {
                eventSystem = eventSystems[0];
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(eventSystems[i].gameObject);
                }
            }
            else
            {
                GameObject esGO = new GameObject("EventSystem");
                eventSystem = esGO.AddComponent<EventSystem>();
            }

            var inputSystemUI = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemUI == null) inputSystemUI = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            var xrUIModule = eventSystem.GetComponent<XRUIInputModule>();
            if (xrUIModule == null) xrUIModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
            xrUIModule.enableXRInput = true;
            xrUIModule.enableMouseInput = true;

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null) standalone.enabled = false;

            var adapter = eventSystem.GetComponent<EventSystemPlatformAdapter>();
            if (adapter == null) adapter = eventSystem.gameObject.AddComponent<EventSystemPlatformAdapter>();
            adapter.ResolveReferences();

            SerializedObject soAdapter = new SerializedObject(adapter);
            soAdapter.FindProperty("xrInputModule").objectReferenceValue = xrUIModule;
            soAdapter.FindProperty("inputSystemModule").objectReferenceValue = inputSystemUI;
            soAdapter.FindProperty("standaloneModule").objectReferenceValue = standalone;
            soAdapter.ApplyModifiedProperties();

            // Default PC/Editor initial state
            if (standalone != null) standalone.enabled = false;
            xrUIModule.enabled = false;
            inputSystemUI.enabled = true;

            Debug.Log("[IntegrationFixer] Configured single EventSystem with EventSystemPlatformAdapter.");
        }

        public static void ConfigureCanvasAndUI()
        {
            // Configure in Scene
            GameObject canvasGO = GameObject.Find("GameUI_Canvas");
            if (canvasGO != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(canvasGO))
                {
                    PrefabUtility.UnpackPrefabInstance(canvasGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                SetupCanvasVRUI(canvasGO);
            }

            // Also configure in Prefab
            string uiPrefabPath = "Assets/Prefabs/UI/GameUI_Canvas.prefab";
            if (File.Exists(uiPrefabPath))
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(uiPrefabPath);
                if (prefabRoot != null)
                {
                    SetupCanvasVRUI(prefabRoot);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, uiPrefabPath);
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    Debug.Log("[IntegrationFixer] Configured GameUI_Canvas.prefab with clean layout & VR UI components.");
                }
            }
        }

        private static void SetupCanvasVRUI(GameObject canvasGO)
        {
            // Ensure Canvas transform scale is unit scale (1, 1, 1), eliminating stale 0 overrides
            canvasGO.transform.localScale = Vector3.one;

            var trackedRaycaster = canvasGO.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (trackedRaycaster == null) trackedRaycaster = canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();
            trackedRaycaster.enabled = true;

            var vrAdapter = canvasGO.GetComponent<VRUIAdapter>();
            if (vrAdapter == null) vrAdapter = canvasGO.AddComponent<VRUIAdapter>();

            Transform menuPanelTrans = canvasGO.transform.Find("MainMenuPanel");
            if (menuPanelTrans != null)
            {
                RebuildMainMenuLayout(menuPanelTrans.gameObject);
            }
        }

        public static void RebuildMainMenuLayout(GameObject menuPanelGO)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();

            // 1. Configure MainMenuPanel RectTransform as the centered card (540 x 740 px)
            RectTransform menuPanelRt = menuPanelGO.GetComponent<RectTransform>();
            menuPanelRt.anchorMin = new Vector2(0.5f, 0.5f);
            menuPanelRt.anchorMax = new Vector2(0.5f, 0.5f);
            menuPanelRt.pivot = new Vector2(0.5f, 0.5f);
            menuPanelRt.anchoredPosition = Vector2.zero;
            menuPanelRt.sizeDelta = new Vector2(540f, 740f);

            // Dark card background covering the entire menu (Title through Quit)
            Image panelImg = menuPanelGO.GetComponent<Image>();
            if (panelImg == null) panelImg = menuPanelGO.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

            // 2. Clean up ALL existing children of menuPanelGO to avoid stale/orphaned controls
            for (int i = menuPanelGO.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(menuPanelGO.transform.GetChild(i).gameObject);
            }

            // 3. Create Content container inset inside MainMenuPanel (padding 25 on all sides)
            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(menuPanelGO.transform, false);
            RectTransform contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 0.5f);
            contentRt.offsetMin = new Vector2(25f, 25f);
            contentRt.offsetMax = new Vector2(-25f, -25f);

            VerticalLayoutGroup contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(0, 0, 15, 15);
            contentVlg.spacing = 16f;
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            // 4. Child 1: Title (55 px)
            Text titleTxt = CreateLabel("Title", contentGO.transform, "VR SHOOTING GALLERY", font, 28, Color.white, 55f, FontStyle.Bold);

            // 5. Child 2: Practice Button (55 px)
            Button practiceBtn = CreateLayoutButton("PracticeButton", contentGO.transform, "PRACTICE MODE", font, new Color(0.20f, 0.50f, 0.80f, 1f), 20, 55f);

            // 6. Child 3: Challenge Button (55 px)
            Button challengeBtn = CreateLayoutButton("ChallengeButton", contentGO.transform, "START CHALLENGE (60s)", font, new Color(0.15f, 0.70f, 0.35f, 1f), 20, 55f);

            // 7. Child 4: Difficulty Section (88 px total: 30 label + 8 spacing + 50 buttons)
            GameObject diffSection = new GameObject("DifficultySection", typeof(RectTransform));
            diffSection.transform.SetParent(contentGO.transform, false);
            RectTransform diffRt = diffSection.GetComponent<RectTransform>();
            diffRt.sizeDelta = new Vector2(0f, 88f);
            LayoutElement diffSectionLe = diffSection.AddComponent<LayoutElement>();
            diffSectionLe.minHeight = 88f;
            diffSectionLe.preferredHeight = 88f;
            diffSectionLe.flexibleHeight = 0f;

            VerticalLayoutGroup diffVlg = diffSection.AddComponent<VerticalLayoutGroup>();
            diffVlg.padding = new RectOffset(0, 0, 0, 0);
            diffVlg.spacing = 8f;
            diffVlg.childAlignment = TextAnchor.UpperCenter;
            diffVlg.childControlWidth = true;
            diffVlg.childControlHeight = true;
            diffVlg.childForceExpandWidth = true;
            diffVlg.childForceExpandHeight = false;

            Text diffLabelTxt = CreateLabel("DifficultyLabel", diffSection.transform, "DIFFICULTY: Medium", font, 18, Color.yellow, 30f, FontStyle.Bold);

            GameObject diffButtonsGO = new GameObject("DifficultyButtons", typeof(RectTransform));
            diffButtonsGO.transform.SetParent(diffSection.transform, false);
            RectTransform diffBtnsRt = diffButtonsGO.GetComponent<RectTransform>();
            diffBtnsRt.sizeDelta = new Vector2(0f, 50f);
            LayoutElement diffBtnsLe = diffButtonsGO.AddComponent<LayoutElement>();
            diffBtnsLe.minHeight = 50f;
            diffBtnsLe.preferredHeight = 50f;
            diffBtnsLe.flexibleHeight = 0f;

            HorizontalLayoutGroup diffHlg = diffButtonsGO.AddComponent<HorizontalLayoutGroup>();
            diffHlg.padding = new RectOffset(0, 0, 0, 0);
            diffHlg.spacing = 10f;
            diffHlg.childControlWidth = true;
            diffHlg.childControlHeight = true;
            diffHlg.childForceExpandWidth = true;
            diffHlg.childForceExpandHeight = true;

            Button easyBtn = CreateRowButton("EasyButton", diffButtonsGO.transform, "EASY", font, new Color(0.25f, 0.60f, 0.30f, 1f), 16, 80f, 50f);
            Button medBtn = CreateRowButton("MediumButton", diffButtonsGO.transform, "MEDIUM", font, new Color(0.80f, 0.55f, 0.15f, 1f), 16, 80f, 50f);
            Button hardBtn = CreateRowButton("HardButton", diffButtonsGO.transform, "HARD", font, new Color(0.75f, 0.25f, 0.25f, 1f), 16, 80f, 50f);

            // 8. Child 5: Controller Section (126 px total: 30 label + 8 spacing + 50 buttons + 8 spacing + 30 status)
            GameObject ctrlSection = new GameObject("ControllerSection", typeof(RectTransform));
            ctrlSection.transform.SetParent(contentGO.transform, false);
            RectTransform ctrlRt = ctrlSection.GetComponent<RectTransform>();
            ctrlRt.sizeDelta = new Vector2(0f, 126f);
            LayoutElement ctrlSectionLe = ctrlSection.AddComponent<LayoutElement>();
            ctrlSectionLe.minHeight = 126f;
            ctrlSectionLe.preferredHeight = 126f;
            ctrlSectionLe.flexibleHeight = 0f;

            VerticalLayoutGroup ctrlVlg = ctrlSection.AddComponent<VerticalLayoutGroup>();
            ctrlVlg.padding = new RectOffset(0, 0, 0, 0);
            ctrlVlg.spacing = 8f;
            ctrlVlg.childAlignment = TextAnchor.UpperCenter;
            ctrlVlg.childControlWidth = true;
            ctrlVlg.childControlHeight = true;
            ctrlVlg.childForceExpandWidth = true;
            ctrlVlg.childForceExpandHeight = false;

            Text ctrlLabelTxt = CreateLabel("ControllerLabel", ctrlSection.transform, "VR CONTROLLER", font, 18, Color.white, 30f, FontStyle.Bold);

            GameObject ctrlButtonsGO = new GameObject("ControllerButtons", typeof(RectTransform));
            ctrlButtonsGO.transform.SetParent(ctrlSection.transform, false);
            RectTransform ctrlBtnsRt = ctrlButtonsGO.GetComponent<RectTransform>();
            ctrlBtnsRt.sizeDelta = new Vector2(0f, 50f);
            LayoutElement ctrlBtnsLe = ctrlButtonsGO.AddComponent<LayoutElement>();
            ctrlBtnsLe.minHeight = 50f;
            ctrlBtnsLe.preferredHeight = 50f;
            ctrlBtnsLe.flexibleHeight = 0f;

            HorizontalLayoutGroup ctrlHlg = ctrlButtonsGO.AddComponent<HorizontalLayoutGroup>();
            ctrlHlg.padding = new RectOffset(0, 0, 0, 0);
            ctrlHlg.spacing = 12f;
            ctrlHlg.childControlWidth = true;
            ctrlHlg.childControlHeight = true;
            ctrlHlg.childForceExpandWidth = true;
            ctrlHlg.childForceExpandHeight = true;

            Button rightBtn = CreateRowButton("RightButton", ctrlButtonsGO.transform, "RIGHT", font, new Color(0.15f, 0.70f, 0.35f, 1f), 16, 120f, 50f);
            Button leftBtn = CreateRowButton("LeftButton", ctrlButtonsGO.transform, "LEFT", font, new Color(0.25f, 0.28f, 0.35f, 1f), 16, 120f, 50f);

            Text ctrlStatusTxt = CreateLabel("ControllerStatus", ctrlSection.transform, "VR CONTROLLER: RIGHT", font, 15, Color.cyan, 30f, FontStyle.Normal);

            // 9. Child 6: Quit Button (50 px)
            Button quitBtn = CreateLayoutButton("QuitButton", contentGO.transform, "QUIT", font, new Color(0.70f, 0.20f, 0.20f, 1f), 20, 50f);

            // Force layout rebuild across the hierarchy
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(diffBtnsRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(diffRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ctrlBtnsRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ctrlRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanelRt);

            // Wire into MainMenuUI
            MainMenuUI menuUI = menuPanelGO.GetComponent<MainMenuUI>();
            if (menuUI == null) menuUI = menuPanelGO.AddComponent<MainMenuUI>();

            SerializedObject soMenu = new SerializedObject(menuUI);
            soMenu.FindProperty("practiceButton").objectReferenceValue = practiceBtn;
            soMenu.FindProperty("challengeButton").objectReferenceValue = challengeBtn;
            soMenu.FindProperty("quitButton").objectReferenceValue = quitBtn;
            soMenu.FindProperty("easyDifficultyButton").objectReferenceValue = easyBtn;
            soMenu.FindProperty("mediumDifficultyButton").objectReferenceValue = medBtn;
            soMenu.FindProperty("hardDifficultyButton").objectReferenceValue = hardBtn;
            soMenu.FindProperty("currentDifficultyText").objectReferenceValue = diffLabelTxt;
            soMenu.FindProperty("rightControllerButton").objectReferenceValue = rightBtn;
            soMenu.FindProperty("leftControllerButton").objectReferenceValue = leftBtn;
            soMenu.FindProperty("controllerSelectionText").objectReferenceValue = ctrlStatusTxt;

            var ctrlPref = UnityEngine.Object.FindAnyObjectByType<VRControllerPreference>();
            if (ctrlPref != null) soMenu.FindProperty("controllerPreference").objectReferenceValue = ctrlPref;
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null) soMenu.FindProperty("gameManager").objectReferenceValue = gm;
            var dm = UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();
            if (dm != null) soMenu.FindProperty("difficultyManager").objectReferenceValue = dm;

            soMenu.ApplyModifiedProperties();
            menuUI.BindButtonCallbacks();
            menuUI.UpdateControllerDisplay();

            Debug.Log("[IntegrationFixer] Rebuilt Main Menu layout: 540x740 card, structured Content, zero overlap.");
        }

        private static Text CreateLabel(string name, Transform parent, string text, Font font, int fontSize, Color color, float height, FontStyle style = FontStyle.Normal)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;

            Text txt = obj.AddComponent<Text>();
            txt.text = text;
            if (font != null) txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            return txt;
        }

        private static Button CreateLayoutButton(string name, Transform parent, string label, Font font, Color color, int fontSize, float height)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;

            Image img = obj.AddComponent<Image>();
            img.color = color;

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
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            return btn;
        }

        private static Button CreateRowButton(string name, Transform parent, string label, Font font, Color color, int fontSize, float minWidth, float height = 50f)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(minWidth, height);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minWidth = minWidth;
            le.preferredWidth = minWidth;
            le.flexibleWidth = 1f;
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;

            Image img = obj.AddComponent<Image>();
            img.color = color;

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
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            return btn;
        }

        public static void ConfigurePlayerRigAndControllers()
        {
            var playerRig = GameObject.Find("PlayerRig");
            if (playerRig == null) return;

            // Ensure XRRuntimeDetector is present on PlayerRig
            var xrDetector = playerRig.GetComponent<XRRuntimeDetector>();
            if (xrDetector == null) xrDetector = playerRig.AddComponent<XRRuntimeDetector>();

            var xrOrigin = playerRig.GetComponent<XROrigin>();
            if (xrOrigin == null) xrOrigin = playerRig.AddComponent<XROrigin>();

            var mainCam = playerRig.GetComponentInChildren<Camera>();
            if (mainCam != null)
            {
                xrOrigin.Camera = mainCam;
                xrOrigin.Origin = playerRig;
                xrOrigin.CameraFloorOffsetObject = playerRig;
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

                var tpd = mainCam.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                if (tpd == null)
                {
                    tpd = mainCam.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                }
            }

            Material uiRayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_UIRay.mat");
            int defaultInteractionLayer = InteractionLayerMask.GetMask("Default");
            if (defaultInteractionLayer == 0) defaultInteractionLayer = 1;

            // Left Anchor
            var leftAnchor = playerRig.transform.Find("LeftControllerAnchor");
            Transform leftAttachTrans = null;
            if (leftAnchor != null)
            {
                var leftController = leftAnchor.GetComponent<XRController>();
                if (leftController == null) leftController = leftAnchor.gameObject.AddComponent<XRController>();
                leftController.controllerNode = XRNode.LeftHand;

                var sc = leftAnchor.GetComponent<SphereCollider>();
                if (sc == null) sc = leftAnchor.gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.15f;

                var direct = leftAnchor.GetComponent<XRDirectInteractor>();
                if (direct == null) direct = leftAnchor.gameObject.AddComponent<XRDirectInteractor>();
                direct.handedness = InteractorHandedness.Left;
                direct.interactionLayers = defaultInteractionLayer;

                SetupRayInteractorOnAnchor(leftAnchor, uiRayMat, InteractorHandedness.Left, defaultInteractionLayer);

                leftAttachTrans = leftAnchor.Find("LeftWeaponAttach");
                if (leftAttachTrans == null)
                {
                    var go = new GameObject("LeftWeaponAttach");
                    go.transform.SetParent(leftAnchor, false);
                    go.transform.localPosition = new Vector3(0f, -0.04f, 0.08f);
                    go.transform.localRotation = Quaternion.identity;
                    leftAttachTrans = go.transform;
                }
            }

            // Right Anchor
            var rightAnchor = playerRig.transform.Find("RightControllerAnchor");
            Transform rightAttachTrans = null;
            if (rightAnchor != null)
            {
                var rightController = rightAnchor.GetComponent<XRController>();
                if (rightController == null) rightController = rightAnchor.gameObject.AddComponent<XRController>();
                rightController.controllerNode = XRNode.RightHand;

                var sc = rightAnchor.GetComponent<SphereCollider>();
                if (sc == null) sc = rightAnchor.gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.15f;

                var direct = rightAnchor.GetComponent<XRDirectInteractor>();
                if (direct == null) direct = rightAnchor.gameObject.AddComponent<XRDirectInteractor>();
                direct.handedness = InteractorHandedness.Right;
                direct.interactionLayers = defaultInteractionLayer;

                SetupRayInteractorOnAnchor(rightAnchor, uiRayMat, InteractorHandedness.Right, defaultInteractionLayer);

                rightAttachTrans = rightAnchor.Find("RightWeaponAttach");
                if (rightAttachTrans == null)
                {
                    var go = new GameObject("RightWeaponAttach");
                    go.transform.SetParent(rightAnchor, false);
                    go.transform.localPosition = new Vector3(0f, -0.04f, 0.08f);
                    go.transform.localRotation = Quaternion.identity;
                    rightAttachTrans = go.transform;
                }
            }

            Transform camAttachTrans = mainCam != null ? mainCam.transform.Find("CameraWeaponMount") : null;
            if (camAttachTrans == null && mainCam != null)
            {
                var go = new GameObject("CameraWeaponMount");
                go.transform.SetParent(mainCam.transform, false);
                go.transform.localPosition = new Vector3(0.22f, -0.18f, 0.42f);
                go.transform.localRotation = Quaternion.identity;
                camAttachTrans = go.transform;
            }

            var weaponMount = GameObject.Find("WeaponMount");
            var weaponManager = playerRig.GetComponent<WeaponManager>() ?? UnityEngine.Object.FindAnyObjectByType<WeaponManager>();

            // VRControllerPreference on PlayerRig
            var ctrlPref = playerRig.GetComponent<VRControllerPreference>();
            if (ctrlPref == null) ctrlPref = playerRig.AddComponent<VRControllerPreference>();
            ctrlPref.ResolveReferencesIfMissing();

            SerializedObject soCtrlPref = new SerializedObject(ctrlPref);
            if (rightAttachTrans != null) soCtrlPref.FindProperty("rightWeaponAttach").objectReferenceValue = rightAttachTrans;
            if (leftAttachTrans != null) soCtrlPref.FindProperty("leftWeaponAttach").objectReferenceValue = leftAttachTrans;
            if (camAttachTrans != null) soCtrlPref.FindProperty("cameraWeaponMount").objectReferenceValue = camAttachTrans;
            if (weaponMount != null) soCtrlPref.FindProperty("weaponMount").objectReferenceValue = weaponMount;
            if (weaponManager != null) soCtrlPref.FindProperty("weaponManager").objectReferenceValue = weaponManager;
            soCtrlPref.ApplyModifiedProperties();

            ctrlPref.SetActiveHand(VRActiveHand.Right);
            ctrlPref.UpdateWeaponAttachment();
            ctrlPref.ValidateSingleActiveHand();

            // Ensure VRPlayerController on PlayerRig
            var vrPlayerCtrl = playerRig.GetComponent<VRPlayerController>();
            if (vrPlayerCtrl == null) vrPlayerCtrl = playerRig.AddComponent<VRPlayerController>();
            SerializedObject soVRPlayer = new SerializedObject(vrPlayerCtrl);
            var propPref = soVRPlayer.FindProperty("controllerPreference");
            if (propPref != null) propPref.objectReferenceValue = ctrlPref;
            soVRPlayer.ApplyModifiedProperties();

            // Ensure PCPlayerController forceEnablePC is false
            var pcCtrl = playerRig.GetComponent<PCPlayerController>();
            if (pcCtrl != null)
            {
                pcCtrl.ForceEnablePC = false;
            }

            // Wire MainMenuUI controllerPreference
            var menuUI = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            if (menuUI != null)
            {
                SerializedObject soMenu = new SerializedObject(menuUI);
                var propMenuPref = soMenu.FindProperty("controllerPreference");
                if (propMenuPref != null) propMenuPref.objectReferenceValue = ctrlPref;
                soMenu.ApplyModifiedProperties();
                menuUI.UpdateControllerDisplay();
            }

            Debug.Log("[IntegrationFixer] Configured PlayerRig with XRRuntimeDetector, VRControllerPreference, single active Right controller, and UI Ray Interactors.");
        }

        private static XRRayInteractor SetupRayInteractorOnAnchor(Transform anchor, Material uiRayMat, InteractorHandedness handedness, int layerMask)
        {
            Transform rayTrans = anchor.Find("RayInteractor");
            GameObject rayGO;
            if (rayTrans == null)
            {
                rayGO = new GameObject("RayInteractor");
                rayGO.transform.SetParent(anchor, false);
                rayGO.transform.localPosition = Vector3.zero;
                rayGO.transform.localRotation = Quaternion.identity;
            }
            else
            {
                rayGO = rayTrans.gameObject;
            }

            var rayInteractor = rayGO.GetComponent<XRRayInteractor>();
            if (rayInteractor == null) rayInteractor = rayGO.AddComponent<XRRayInteractor>();
            rayInteractor.enableUIInteraction = true;
            rayInteractor.maxRaycastDistance = 10f;
            rayInteractor.raycastMask = ~0;
            rayInteractor.interactionLayers = layerMask;
            rayInteractor.handedness = handedness;

            var lr = rayGO.GetComponent<LineRenderer>();
            if (lr == null) lr = rayGO.AddComponent<LineRenderer>();
            lr.startWidth = 0.005f;
            lr.endWidth = 0.005f;
            if (uiRayMat != null) lr.sharedMaterial = uiRayMat;

            var lineVisual = rayGO.GetComponent<XRInteractorLineVisual>();
            if (lineVisual == null) lineVisual = rayGO.AddComponent<XRInteractorLineVisual>();
            lineVisual.lineWidth = 0.005f;
            lineVisual.lineLength = 10f;

            return rayInteractor;
        }

        public static void ConfigureSceneWeapons()
        {
            var weapons = UnityEngine.Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Material tracerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Tracer.mat");
            int defaultInteractionLayer = InteractionLayerMask.GetMask("Default");
            if (defaultInteractionLayer == 0) defaultInteractionLayer = 1;

            foreach (var w in weapons)
            {
                bool isPistol = w.WeaponType == WeaponType.Pistol || w.name.Contains("Pistol");

                Transform attachTrans = w.transform.Find("AttachTransform");
                if (attachTrans == null)
                {
                    GameObject attachGO = new GameObject("AttachTransform");
                    attachTrans = attachGO.transform;
                    attachTrans.SetParent(w.transform, false);
                }

                if (isPistol)
                {
                    attachTrans.localPosition = new Vector3(0f, -0.05f, 0.02f);
                    attachTrans.localRotation = Quaternion.identity;
                }
                else
                {
                    attachTrans.localPosition = new Vector3(0f, -0.04f, 0.05f);
                    attachTrans.localRotation = Quaternion.identity;
                }

                var grab = w.GetComponent<XRGrabInteractable>();
                if (grab != null)
                {
                    grab.attachTransform = attachTrans;
                    grab.interactionLayers = defaultInteractionLayer;
                }

                var aimRay = w.GetComponent<WeaponAimRay>();
                if (aimRay == null) aimRay = w.gameObject.AddComponent<WeaponAimRay>();
                if (tracerMat != null) aimRay.SetAimMaterial(tracerMat);
            }

            Debug.Log($"[IntegrationFixer] Configured AttachTransform and WeaponAimRay on {weapons.Length} scene weapons.");
        }

        public static void ConfigureEnvironment()
        {
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Wall.mat");
            string envPrefabPath = "Assets/Prefabs/Environment/ShootingRange_Environment.prefab";

            // Update in prefab
            if (File.Exists(envPrefabPath))
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(envPrefabPath);
                if (prefabRoot != null)
                {
                    Transform rearWallTrans = prefabRoot.transform.Find("RearWall");
                    if (rearWallTrans == null)
                    {
                        GameObject rearWallGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rearWallGO.name = "RearWall";
                        rearWallGO.transform.SetParent(prefabRoot.transform, false);
                        rearWallGO.transform.localPosition = new Vector3(0f, 3f, -5f);
                        rearWallGO.transform.localScale = new Vector3(20f, 6.2f, 0.5f);
                        if (wallMat != null) rearWallGO.GetComponent<Renderer>().sharedMaterial = wallMat;
                        Debug.Log("[IntegrationFixer] Added RearWall to ShootingRange_Environment.prefab.");
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, envPrefabPath);
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            // Update in scene
            GameObject envSceneGO = GameObject.Find("ShootingRange_Environment");
            if (envSceneGO != null)
            {
                Transform rearWallSceneTrans = envSceneGO.transform.Find("RearWall");
                if (rearWallSceneTrans == null)
                {
                    GameObject rearWallGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rearWallGO.name = "RearWall";
                    rearWallGO.transform.SetParent(envSceneGO.transform, false);
                    rearWallGO.transform.localPosition = new Vector3(0f, 3f, -5f);
                    rearWallGO.transform.localScale = new Vector3(20f, 6.2f, 0.5f);
                    if (wallMat != null) rearWallGO.GetComponent<Renderer>().sharedMaterial = wallMat;
                    Debug.Log("[IntegrationFixer] Added RearWall to ShootingRange_Environment in scene.");
                }
            }
        }
    }
}
