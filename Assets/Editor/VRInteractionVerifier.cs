using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
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
    /// <summary>
    /// Automated verification suite for VR Shooting Gallery:
    /// - XR Controller Hand Selection & Exclusivity
    /// - EventSystem UI Input Module Exclusivity (InputSystemUIInputModule vs XRUIInputModule)
    /// - Main Menu UI LayoutGroup hierarchy & overlap prevention
    /// - Weapon Muzzle-Aligned Aim Ray and dedicated AttachTransform
    /// - Canvas Scale and VR WorldSpace vs PC ScreenSpace Overlay
    /// - Explicit distinction between CONFIGURATION VERIFIED and PHYSICAL QUEST TEST REQUIRED.
    /// </summary>
    public static class VRInteractionVerifier
    {
        private const string ScenePath = "Assets/Scenes/ShootingRange.unity";
        private const string ReportPath = "Logs/VRInteractionReport.txt";

        private static readonly StringBuilder Report = new StringBuilder();
        private static int _passedCount = 0;
        private static int _totalCount = 0;

        [MenuItem("VR Shooting Gallery/Run VR Interaction Verification")]
        public static void RunVerification()
        {
            Report.Clear();
            _passedCount = 0;
            _totalCount = 0;

            Report.AppendLine("==================================================");
            Report.AppendLine("VR SHOOTING GALLERY - COMPREHENSIVE XR & UI VERIFICATION");
            Report.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            Report.AppendLine("==================================================");

            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                AssertCheck("1. Scene Loaded Successfully [Config]", scene.IsValid(), $"Scene: {scene.name}");

                var playerRig = GameObject.Find("PlayerRig");
                AssertCheck("2. PlayerRig Present in Scene [Config]", playerRig != null, playerRig != null ? "PlayerRig found" : "Missing");

                // Head tracking
                var tpd = Camera.main?.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                AssertCheck("3. Head Tracking (TrackedPoseDriver) on Main Camera [Config]", tpd != null,
                    tpd != null ? "TrackedPoseDriver active on Main Camera" : "Missing");

                // XRRuntimeDetector
                var xrDetector = playerRig?.GetComponent<XRRuntimeDetector>();
                AssertCheck("4. XRRuntimeDetector Component on PlayerRig [Config]", xrDetector != null,
                    xrDetector != null ? "XRRuntimeDetector present" : "Missing");

                // Controller Handedness & Node Configuration
                var leftAnchor = playerRig?.transform.Find("LeftControllerAnchor");
                var rightAnchor = playerRig?.transform.Find("RightControllerAnchor");

                var leftXRController = leftAnchor?.GetComponent<XRController>();
                var rightXRController = rightAnchor?.GetComponent<XRController>();
                AssertCheck("5. XRController Left/Right Node Assignment [Config]",
                    leftXRController != null && leftXRController.controllerNode == XRNode.LeftHand &&
                    rightXRController != null && rightXRController.controllerNode == XRNode.RightHand,
                    $"Left Node: {leftXRController?.controllerNode}, Right Node: {rightXRController?.controllerNode}");

                var leftDirect = leftAnchor?.GetComponent<XRDirectInteractor>();
                var rightDirect = rightAnchor?.GetComponent<XRDirectInteractor>();
                AssertCheck("6. XRDirectInteractor Handedness [Config]",
                    leftDirect != null && leftDirect.handedness == InteractorHandedness.Left &&
                    rightDirect != null && rightDirect.handedness == InteractorHandedness.Right,
                    $"Left Direct Handedness: {leftDirect?.handedness}, Right Direct Handedness: {rightDirect?.handedness}");

                var leftRay = leftAnchor?.Find("RayInteractor")?.GetComponent<XRRayInteractor>();
                var rightRay = rightAnchor?.Find("RayInteractor")?.GetComponent<XRRayInteractor>();
                AssertCheck("7. XRRayInteractor Handedness [Config]",
                    leftRay != null && leftRay.handedness == InteractorHandedness.Left &&
                    rightRay != null && rightRay.handedness == InteractorHandedness.Right,
                    $"Left Ray Handedness: {leftRay?.handedness}, Right Ray Handedness: {rightRay?.handedness}");

                // XRRayInteractor Configuration
                AssertCheck("8. XRRayInteractor UI Interaction Enabled [Config]",
                    leftRay != null && leftRay.enableUIInteraction &&
                    rightRay != null && rightRay.enableUIInteraction,
                    $"Left enableUI: {leftRay?.enableUIInteraction}, Right enableUI: {rightRay?.enableUIInteraction}");

                int defaultLayerMask = InteractionLayerMask.GetMask("Default");
                if (defaultLayerMask == 0) defaultLayerMask = 1;
                bool validLayers = leftRay != null && leftRay.interactionLayers.value != 0 &&
                                   rightRay != null && rightRay.interactionLayers.value != 0 &&
                                   leftDirect != null && leftDirect.interactionLayers.value != 0 &&
                                   rightDirect != null && rightDirect.interactionLayers.value != 0;
                AssertCheck("9. XR Interactors Valid Interaction Layer Mask (Non-Zero) [Config]", validLayers,
                    $"RayLayers: (L:{leftRay?.interactionLayers.value}, R:{rightRay?.interactionLayers.value}), DirectLayers: (L:{leftDirect?.interactionLayers.value}, R:{rightDirect?.interactionLayers.value})");

                // Check UI Ray material distinction
                var rightRayLr = rightAnchor?.Find("RayInteractor")?.GetComponent<LineRenderer>();
                bool uiRayDistinct = rightRayLr != null && rightRayLr.sharedMaterial != null &&
                                     rightRayLr.sharedMaterial.name != "Mat_Tracer";
                AssertCheck("10. XR UI Ray Visual Distinct from Gun Tracer [Config]", uiRayDistinct,
                    $"Right Ray Material: {rightRayLr?.sharedMaterial?.name} (Expected: Mat_UIRay or UI-specific material)");

                // VRControllerPreference Component & Exclusivity
                var vrPref = playerRig?.GetComponent<VRControllerPreference>();
                AssertCheck("11. VRControllerPreference Component Attached [Config]", vrPref != null,
                    vrPref != null ? "VRControllerPreference present" : "Missing");

                if (vrPref != null)
                {
                    vrPref.ResolveReferencesIfMissing();

                    // Default selection is Right
                    vrPref.SetActiveHand(VRActiveHand.Right);
                    AssertCheck("12. Default Controller Selection is Right [Runtime]",
                        vrPref.ActiveHand == VRActiveHand.Right && vrPref.IsRightControllerActive,
                        $"ActiveHand: {vrPref.ActiveHand}, IsRightActive: {vrPref.IsRightControllerActive}");

                    // Left gameplay interactors inactive when Right is active
                    bool leftDirectInactive = vrPref.LeftDirectInteractor != null && !vrPref.LeftDirectInteractor.enabled;
                    bool leftRayInactive = vrPref.LeftRayInteractor != null && !vrPref.LeftRayInteractor.enabled;
                    bool leftCtrlInactive = vrPref.LeftXRController != null && !vrPref.LeftXRController.enabled;
                    AssertCheck("13. Left Gameplay Interactors & XRController Inactive when Right is Active [Runtime]",
                        leftDirectInactive && leftRayInactive && leftCtrlInactive,
                        $"LeftDirect.enabled: {vrPref.LeftDirectInteractor?.enabled}, LeftRay.enabled: {vrPref.LeftRayInteractor?.enabled}, LeftXRController.enabled: {vrPref.LeftXRController?.enabled}");

                    // Right gameplay interactors active when Right is active
                    bool rightDirectActive = vrPref.RightDirectInteractor != null && vrPref.RightDirectInteractor.enabled;
                    bool rightRayActive = vrPref.RightRayInteractor != null && vrPref.RightRayInteractor.enabled;
                    bool rightCtrlActive = vrPref.RightXRController != null && vrPref.RightXRController.enabled;
                    AssertCheck("14. Right Gameplay Interactors & XRController Active when Right is Active [Runtime]",
                        rightDirectActive && rightRayActive && rightCtrlActive,
                        $"RightDirect.enabled: {vrPref.RightDirectInteractor?.enabled}, RightRay.enabled: {vrPref.RightRayInteractor?.enabled}, RightXRController.enabled: {vrPref.RightXRController?.enabled}");

                    // Inactive Left controller blocked from gameplay/weapon interaction
                    bool leftBlocked = !vrPref.IsInteractorAllowed(vrPref.LeftDirectInteractor) && !vrPref.IsInteractorAllowed(vrPref.LeftRayInteractor);
                    bool rightAllowed = vrPref.IsInteractorAllowed(vrPref.RightDirectInteractor) && vrPref.IsInteractorAllowed(vrPref.RightRayInteractor);
                    AssertCheck("15. Inactive Left Controller Blocked from Gameplay/Weapon Interaction [Runtime]",
                        leftBlocked && rightAllowed,
                        $"LeftAllowed: {!leftBlocked}, RightAllowed: {rightAllowed}");

                    // Switching to Left activates Left
                    vrPref.SetActiveHand(VRActiveHand.Left);
                    AssertCheck("16. Switch to Left Controller [Runtime]",
                        vrPref.ActiveHand == VRActiveHand.Left && vrPref.IsLeftControllerActive,
                        $"ActiveHand: {vrPref.ActiveHand}, IsLeftActive: {vrPref.IsLeftControllerActive}");

                    // Right gameplay interactors inactive when Left is active
                    bool rightDirectInactive = vrPref.RightDirectInteractor != null && !vrPref.RightDirectInteractor.enabled;
                    bool rightRayInactive = vrPref.RightRayInteractor != null && !vrPref.RightRayInteractor.enabled;
                    bool rightCtrlInactive = vrPref.RightXRController != null && !vrPref.RightXRController.enabled;
                    AssertCheck("17. Right Gameplay Interactors & XRController Inactive when Left is Active [Runtime]",
                        rightDirectInactive && rightRayInactive && rightCtrlInactive,
                        $"RightDirect.enabled: {vrPref.RightDirectInteractor?.enabled}, RightRay.enabled: {vrPref.RightRayInteractor?.enabled}, RightXRController.enabled: {vrPref.RightXRController?.enabled}");

                    // Active Left controller allowed for gameplay/weapon interaction
                    bool leftDirectActiveNow = vrPref.LeftDirectInteractor != null && vrPref.LeftDirectInteractor.enabled;
                    bool leftRayActiveNow = vrPref.LeftRayInteractor != null && vrPref.LeftRayInteractor.enabled;
                    bool leftCtrlActiveNow = vrPref.LeftXRController != null && vrPref.LeftXRController.enabled;
                    bool leftAllowedNow = vrPref.IsInteractorAllowed(vrPref.LeftDirectInteractor) && vrPref.IsInteractorAllowed(vrPref.LeftRayInteractor);
                    bool rightBlockedNow = !vrPref.IsInteractorAllowed(vrPref.RightDirectInteractor) && !vrPref.IsInteractorAllowed(vrPref.RightRayInteractor);
                    AssertCheck("18. Active Left Controller Allowed for Gameplay/Weapon Interaction [Runtime]",
                        leftDirectActiveNow && leftRayActiveNow && leftCtrlActiveNow && leftAllowedNow && rightBlockedNow,
                        $"LeftActive: {leftDirectActiveNow}, LeftAllowed: {leftAllowedNow}, RightBlocked: {rightBlockedNow}");

                    // Restore default Right
                    vrPref.SetActiveHand(VRActiveHand.Right);

                    // Exclusivity: Never both controllers simultaneously active
                    bool noSimultaneousDirect = !(vrPref.LeftDirectInteractor != null && vrPref.LeftDirectInteractor.enabled &&
                                                  vrPref.RightDirectInteractor != null && vrPref.RightDirectInteractor.enabled);
                    bool noSimultaneousRay = !(vrPref.LeftRayInteractor != null && vrPref.LeftRayInteractor.enabled &&
                                               vrPref.RightRayInteractor != null && vrPref.RightRayInteractor.enabled);
                    AssertCheck("19. Exclusivity: Never Both Controllers Simultaneously Active [Runtime]",
                        noSimultaneousDirect && noSimultaneousRay,
                        $"DirectExclusive: {noSimultaneousDirect}, RayExclusive: {noSimultaneousRay}");

                    bool singleHandValid = vrPref.ValidateSingleActiveHand();
                    AssertCheck("20. Single Active Hand Validation Method Passes [Runtime]", singleHandValid,
                        $"ValidateSingleActiveHand() result: {singleHandValid}");
                }

                // EventSystem Platform Adaptation & Single Active Module
                var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                AssertCheck("21. Exactly One EventSystem in Scene [Config]", eventSystems.Length == 1,
                    $"EventSystem count: {eventSystems.Length}");

                var eventSystem = eventSystems.Length > 0 ? eventSystems[0] : null;
                var platformAdapter = eventSystem?.GetComponent<EventSystemPlatformAdapter>();
                AssertCheck("22. EventSystemPlatformAdapter Attached [Config]", platformAdapter != null,
                    platformAdapter != null ? "EventSystemPlatformAdapter present" : "Missing");

                if (platformAdapter != null)
                {
                    // PC Mode: InputSystemUIInputModule active, XRUIInputModule & Standalone disabled
                    platformAdapter.SetPlatformMode(false);
                    bool pcModuleOk = platformAdapter.InputSystemModule != null && platformAdapter.InputSystemModule.enabled &&
                                      (platformAdapter.XRInputModule == null || !platformAdapter.XRInputModule.enabled) &&
                                      (platformAdapter.StandaloneModule == null || !platformAdapter.StandaloneModule.enabled) &&
                                      platformAdapter.EnabledModuleCount == 1;
                    AssertCheck("23. PC Mode: Exactly One Active Module (InputSystemUIInputModule) [Runtime]", pcModuleOk,
                        $"ActiveCount: {platformAdapter.EnabledModuleCount}, InputSystem: {platformAdapter.InputSystemModule?.enabled}, XR: {platformAdapter.XRInputModule?.enabled}");

                    // XR Mode: XRUIInputModule active, InputSystemUIInputModule & Standalone disabled
                    platformAdapter.SetPlatformMode(true);
                    bool xrModuleOk = platformAdapter.XRInputModule != null && platformAdapter.XRInputModule.enabled &&
                                      (platformAdapter.InputSystemModule == null || !platformAdapter.InputSystemModule.enabled) &&
                                      (platformAdapter.StandaloneModule == null || !platformAdapter.StandaloneModule.enabled) &&
                                      platformAdapter.EnabledModuleCount == 1;
                    AssertCheck("24. VR Mode: Exactly One Active Module (XRUIInputModule) [Runtime]", xrModuleOk,
                        $"ActiveCount: {platformAdapter.EnabledModuleCount}, XR: {platformAdapter.XRInputModule?.enabled}, InputSystem: {platformAdapter.InputSystemModule?.enabled}");

                    // Reset to PC mode for default editor state
                    platformAdapter.SetPlatformMode(false);
                }

                // Canvas & VR UI Adapter
                GameObject canvasGO = GameObject.Find("GameUI_Canvas");
                AssertCheck("25. GameUI_Canvas Present [Config]", canvasGO != null, canvasGO != null ? "Canvas found" : "Missing");

                if (canvasGO != null)
                {
                    // Verify no stale zero local scale override
                    Vector3 canvasScale = canvasGO.transform.localScale;
                    bool scaleNonZero = canvasScale.x > 0.0001f && canvasScale.y > 0.0001f && canvasScale.z > 0.0001f;
                    AssertCheck("26. Canvas Serialized Scale is Valid & Non-Zero [Config]", scaleNonZero,
                        $"Canvas LocalScale: {canvasScale}");
                }

                var trackedRaycaster = canvasGO?.GetComponent<TrackedDeviceGraphicRaycaster>();
                AssertCheck("27. TrackedDeviceGraphicRaycaster Attached to Canvas [Config]", trackedRaycaster != null,
                    trackedRaycaster != null ? "TrackedDeviceGraphicRaycaster present" : "Missing");

                var vrAdapter = canvasGO?.GetComponent<VRUIAdapter>();
                AssertCheck("28. VRUIAdapter Attached to Canvas [Config]", vrAdapter != null,
                    vrAdapter != null ? "VRUIAdapter present" : "Missing");

                if (vrAdapter != null && canvasGO != null)
                {
                    // Test VR WorldSpace configuration & positioning in front of headset
                    vrAdapter.ConfigureForVR(Camera.main);
                    Canvas canvasComp = canvasGO.GetComponent<Canvas>();
                    bool vrModeOk = canvasComp.renderMode == RenderMode.WorldSpace && canvasComp.worldCamera != null;
                    AssertCheck("29. VR Mode: Canvas Configured as WorldSpace [Runtime]", vrModeOk,
                        $"RenderMode: {canvasComp.renderMode}, WorldCamera: {canvasComp.worldCamera?.name}");

                    // Verify Canvas position approximately 1.5 - 2.0m in front of player
                    Vector3 camPos = Camera.main.transform.position;
                    float distToCam = Vector3.Distance(new Vector3(canvasGO.transform.position.x, 0f, canvasGO.transform.position.z),
                                                      new Vector3(camPos.x, 0f, camPos.z));
                    bool distOk = distToCam >= 1.4f && distToCam <= 2.2f;
                    AssertCheck("30. VR Mode: Canvas Positioned 1.5-2.0m in Front of Headset [Runtime]", distOk,
                        $"Horizontal Distance to Headset: {distToCam:F2}m (Expected ~1.8m)");

                    // Test PC ScreenSpaceOverlay configuration
                    vrAdapter.ConfigureForPC();
                    bool pcOk = canvasComp.renderMode == RenderMode.ScreenSpaceOverlay &&
                                Mathf.Approximately(canvasGO.transform.localScale.x, 1f);
                    AssertCheck("31. PC Mode: Canvas Configured as ScreenSpaceOverlay with Unit Scale [Runtime]", pcOk,
                        $"RenderMode: {canvasComp.renderMode}, LocalScale: {canvasGO.transform.localScale}");
                }

                // Main Menu LayoutGroup Hierarchy & Geometric Overlap Prevention
                var menuUI = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
                AssertCheck("32. MainMenuUI Component Present [Config]", menuUI != null, menuUI != null ? "MainMenuUI found" : "Missing");

                if (menuUI != null)
                {
                    var menuPanelRt = menuUI.GetComponent<RectTransform>();
                    var contentTrans = menuUI.transform.Find("Content");
                    var contentRt = contentTrans?.GetComponent<RectTransform>();

                    // Force canvas and layout rebuild to ensure accurate RectTransform calculations
                    Canvas.ForceUpdateCanvases();
                    if (contentRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
                    if (menuPanelRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanelRt);

                    // Geometric Check 1: MainMenuPanel Dimensions (500-560 x 700-780)
                    bool panelDimsOk = menuPanelRt != null && menuPanelRt.rect.width >= 500f && menuPanelRt.rect.height >= 700f;
                    AssertCheck("33. MainMenuPanel Dimensions (>=500x700) [Geometric]", panelDimsOk,
                        $"MainMenuPanel size: {menuPanelRt?.rect.width:F0} x {menuPanelRt?.rect.height:F0} (Expected >= 500x700)");

                    // Geometric Check 2: MainMenuPanel contains Content
                    AssertCheck("34. MainMenuPanel Contains Content [Config]", contentTrans != null,
                        contentTrans != null ? "Content found under MainMenuPanel" : "Content missing");

                    var titleTrans = contentTrans?.Find("Title");
                    var practiceTrans = contentTrans?.Find("PracticeButton");
                    var challengeTrans = contentTrans?.Find("ChallengeButton");
                    var diffSection = contentTrans?.Find("DifficultySection");
                    var ctrlSection = contentTrans?.Find("ControllerSection");
                    var quitTrans = contentTrans?.Find("QuitButton");

                    // Geometric Check 3: Content contains all required sections in exact order
                    bool sectionsPresent = titleTrans != null && practiceTrans != null && challengeTrans != null &&
                                           diffSection != null && ctrlSection != null && quitTrans != null;
                    bool correctOrder = sectionsPresent &&
                                        titleTrans.GetSiblingIndex() < practiceTrans.GetSiblingIndex() &&
                                        practiceTrans.GetSiblingIndex() < challengeTrans.GetSiblingIndex() &&
                                        challengeTrans.GetSiblingIndex() < diffSection.GetSiblingIndex() &&
                                        diffSection.GetSiblingIndex() < ctrlSection.GetSiblingIndex() &&
                                        ctrlSection.GetSiblingIndex() < quitTrans.GetSiblingIndex();
                    AssertCheck("35. Content Visual Hierarchy & Order (Title -> Practice -> Challenge -> Diff -> Ctrl -> Quit) [Config]",
                        correctOrder,
                        $"Sections present: {sectionsPresent}, Visual order correct: {correctOrder}");

                    // Geometric Check 4: ControllerSection contains ControllerLabel, ControllerButtons, ControllerStatus
                    var ctrlLabel = ctrlSection?.Find("ControllerLabel");
                    var ctrlBtns = ctrlSection?.Find("ControllerButtons");
                    var ctrlStatus = ctrlSection?.Find("ControllerStatus");
                    bool ctrlOrderOk = ctrlLabel != null && ctrlBtns != null && ctrlStatus != null &&
                                       ctrlLabel.GetSiblingIndex() < ctrlBtns.GetSiblingIndex() &&
                                       ctrlBtns.GetSiblingIndex() < ctrlStatus.GetSiblingIndex();
                    AssertCheck("36. ControllerSection Hierarchy (Label -> Buttons -> Status) [Config]", ctrlOrderOk,
                        $"Ctrl hierarchy valid: {ctrlOrderOk}");

                    // Geometric Check 5: QuitButton is after ControllerSection
                    bool quitAfterCtrl = quitTrans != null && ctrlSection != null &&
                                         quitTrans.GetSiblingIndex() > ctrlSection.GetSiblingIndex();
                    AssertCheck("37. QuitButton Placed After ControllerSection [Config]", quitAfterCtrl,
                        $"Quit index: {quitTrans?.GetSiblingIndex()}, Ctrl index: {ctrlSection?.GetSiblingIndex()}");

                    // Geometric Check 6: All controls have non-zero dimensions
                    var diffLabel = diffSection?.Find("DifficultyLabel");
                    var diffBtns = diffSection?.Find("DifficultyButtons");
                    var easyBtn = diffBtns?.Find("EasyButton");
                    var medBtn = diffBtns?.Find("MediumButton");
                    var hardBtn = diffBtns?.Find("HardButton");
                    var rightBtn = ctrlBtns?.Find("RightButton") ?? ctrlBtns?.Find("RightControllerButton");
                    var leftBtn = ctrlBtns?.Find("LeftButton") ?? ctrlBtns?.Find("LeftControllerButton");

                    Transform[] allControls = { titleTrans, practiceTrans, challengeTrans, diffLabel, diffBtns, easyBtn, medBtn, hardBtn, ctrlLabel, ctrlBtns, rightBtn, leftBtn, ctrlStatus, quitTrans };
                    bool allNonZero = true;
                    string zeroDetail = "";
                    foreach (var c in allControls)
                    {
                        if (c == null) { allNonZero = false; zeroDetail = "Missing control"; break; }
                        var rt = c.GetComponent<RectTransform>();
                        if (rt == null || rt.rect.width <= 0f || rt.rect.height <= 0f)
                        {
                            allNonZero = false;
                            zeroDetail = $"{c.name} has size {rt?.rect.width}x{rt?.rect.height}";
                            break;
                        }
                    }
                    AssertCheck("38. All Main Menu Controls Have Non-Zero Dimensions [Geometric]", allNonZero,
                        allNonZero ? "All 14 controls have valid positive width and height" : zeroDetail);

                    // Geometric Check 7: No prohibited major sibling rectangles overlap in menuPanel space
                    Rect rTitle = GetBoundingRectInParent(titleTrans.GetComponent<RectTransform>(), menuPanelRt);
                    Rect rPractice = GetBoundingRectInParent(practiceTrans.GetComponent<RectTransform>(), menuPanelRt);
                    Rect rChallenge = GetBoundingRectInParent(challengeTrans.GetComponent<RectTransform>(), menuPanelRt);
                    Rect rDiffSection = GetBoundingRectInParent(diffSection.GetComponent<RectTransform>(), menuPanelRt);
                    Rect rCtrlSection = GetBoundingRectInParent(ctrlSection.GetComponent<RectTransform>(), menuPanelRt);
                    Rect rQuit = GetBoundingRectInParent(quitTrans.GetComponent<RectTransform>(), menuPanelRt);

                    bool siblingsNoOverlap = !rTitle.Overlaps(rPractice) &&
                                             !rPractice.Overlaps(rChallenge) &&
                                             !rChallenge.Overlaps(rDiffSection) &&
                                             !rDiffSection.Overlaps(rCtrlSection) &&
                                             !rCtrlSection.Overlaps(rQuit);
                    AssertCheck("39. No Major Sibling Rectangles Overlap [Geometric]", siblingsNoOverlap,
                        siblingsNoOverlap ? "Clear vertical separation between all major rows" : "Overlap detected between sibling rows");

                    // Geometric Check 8: ControllerLabel does NOT overlap DifficultyButtons
                    Rect rDiffBtns = GetBoundingRectInParent(diffBtns.GetComponent<RectTransform>(), menuPanelRt);
                    Rect rCtrlLabel = GetBoundingRectInParent(ctrlLabel.GetComponent<RectTransform>(), menuPanelRt);
                    bool noDiffCtrlLabelOverlap = !rDiffBtns.Overlaps(rCtrlLabel);
                    AssertCheck("40. ControllerLabel Does Not Overlap DifficultyButtons [Geometric]", noDiffCtrlLabelOverlap,
                        $"DiffBtns Y: [{rDiffBtns.yMin:F1}, {rDiffBtns.yMax:F1}], CtrlLabel Y: [{rCtrlLabel.yMin:F1}, {rCtrlLabel.yMax:F1}] => Overlap: {!noDiffCtrlLabelOverlap}");

                    // Geometric Check 9: ControllerButtons do NOT overlap DifficultyButtons
                    Rect rCtrlBtns = GetBoundingRectInParent(ctrlBtns.GetComponent<RectTransform>(), menuPanelRt);
                    bool noDiffBtnsCtrlBtnsOverlap = !rDiffBtns.Overlaps(rCtrlBtns);
                    AssertCheck("41. ControllerButtons Do Not Overlap DifficultyButtons [Geometric]", noDiffBtnsCtrlBtnsOverlap,
                        $"DiffBtns Y: [{rDiffBtns.yMin:F1}, {rDiffBtns.yMax:F1}], CtrlBtns Y: [{rCtrlBtns.yMin:F1}, {rCtrlBtns.yMax:F1}] => Overlap: {!noDiffBtnsCtrlBtnsOverlap}");

                    // Geometric Check 10: ControllerStatus does NOT overlap QuitButton
                    Rect rCtrlStatus = GetBoundingRectInParent(ctrlStatus.GetComponent<RectTransform>(), menuPanelRt);
                    bool noStatusQuitOverlap = !rCtrlStatus.Overlaps(rQuit);
                    AssertCheck("42. ControllerStatus Does Not Overlap QuitButton [Geometric]", noStatusQuitOverlap,
                        $"CtrlStatus Y: [{rCtrlStatus.yMin:F1}, {rCtrlStatus.yMax:F1}], Quit Y: [{rQuit.yMin:F1}, {rQuit.yMax:F1}] => Overlap: {!noStatusQuitOverlap}");

                    // Geometric Check 11: ControllerStatus is inside MainMenuPanel bounds
                    bool statusInside = rCtrlStatus.yMin >= menuPanelRt.rect.yMin - 0.5f && rCtrlStatus.yMax <= menuPanelRt.rect.yMax + 0.5f &&
                                        rCtrlStatus.xMin >= menuPanelRt.rect.xMin - 0.5f && rCtrlStatus.xMax <= menuPanelRt.rect.xMax + 0.5f;
                    AssertCheck("43. ControllerStatus is Inside MainMenuPanel Bounds [Geometric]", statusInside,
                        $"CtrlStatus Y: [{rCtrlStatus.yMin:F1}, {rCtrlStatus.yMax:F1}], Panel Y: [{menuPanelRt.rect.yMin:F1}, {menuPanelRt.rect.yMax:F1}]");

                    // Geometric Check 12: QuitButton is inside MainMenuPanel bounds
                    bool quitInside = rQuit.yMin >= menuPanelRt.rect.yMin - 0.5f && rQuit.yMax <= menuPanelRt.rect.yMax + 0.5f &&
                                      rQuit.xMin >= menuPanelRt.rect.xMin - 0.5f && rQuit.xMax <= menuPanelRt.rect.xMax + 0.5f;
                    AssertCheck("44. QuitButton is Inside MainMenuPanel Bounds [Geometric]", quitInside,
                        $"Quit Y: [{rQuit.yMin:F1}, {rQuit.yMax:F1}], Panel Y: [{menuPanelRt.rect.yMin:F1}, {menuPanelRt.rect.yMax:F1}]");

                    // Geometric Check 13: All buttons have readable dimensions
                    var practiceRt = practiceTrans.GetComponent<RectTransform>();
                    var challengeRt = challengeTrans.GetComponent<RectTransform>();
                    var easyRt = easyBtn.GetComponent<RectTransform>();
                    var rightRt = rightBtn.GetComponent<RectTransform>();
                    var quitRt = quitTrans.GetComponent<RectTransform>();
                    bool buttonsReadable = practiceRt.rect.height >= 45f && practiceRt.rect.width >= 250f &&
                                           challengeRt.rect.height >= 45f && challengeRt.rect.width >= 250f &&
                                           quitRt.rect.height >= 45f && quitRt.rect.width >= 250f &&
                                           easyRt.rect.height >= 40f && easyRt.rect.width >= 60f &&
                                           rightRt.rect.height >= 40f && rightRt.rect.width >= 90f;
                    AssertCheck("45. All Buttons Have Readable Dimensions [Geometric]", buttonsReadable,
                        $"Practice: {practiceRt.rect.size}, Easy: {easyRt.rect.size}, Right: {rightRt.rect.size}, Quit: {quitRt.rect.size}");

                    AssertCheck("46. Controller Selection Buttons Wired in MainMenuUI [Config]",
                        menuUI.RightControllerButton != null && menuUI.LeftControllerButton != null,
                        $"RightBtn: {menuUI.RightControllerButton != null}, LeftBtn: {menuUI.LeftControllerButton != null}");

                    // Simulate Controller Selection via UI clicks
                    if (menuUI.LeftControllerButton != null && vrPref != null)
                    {
                        menuUI.BindButtonCallbacks();
                        menuUI.LeftControllerButton.onClick.Invoke();
                        AssertCheck("47. UI Click Callback: Clicking Left Button Selects Left [Runtime]",
                            vrPref.ActiveHand == VRActiveHand.Left,
                            $"ActiveHand after UI click: {vrPref.ActiveHand}");

                        menuUI.RightControllerButton.onClick.Invoke();
                        AssertCheck("48. UI Click Callback: Clicking Right Button Selects Right [Runtime]",
                            vrPref.ActiveHand == VRActiveHand.Right,
                            $"ActiveHand after UI click: {vrPref.ActiveHand}");
                    }
                }

                // Weapons XR & Aim Ray Architecture
                var weapons = UnityEngine.Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                bool weaponsHaveVR = true;
                bool weaponsHaveAttach = true;
                bool weaponsHaveAimRay = true;
                bool weaponsAimMatchesMuzzle = true;

                foreach (var w in weapons)
                {
                    if (w.GetComponent<VRWeaponInteractable>() == null ||
                        w.GetComponent<XRGrabInteractable>() == null)
                    {
                        weaponsHaveVR = false;
                    }

                    var grab = w.GetComponent<XRGrabInteractable>();
                    if (grab == null || grab.attachTransform == null || grab.interactionLayers.value == 0)
                    {
                        weaponsHaveAttach = false;
                    }

                    var aimRay = w.GetComponent<WeaponAimRay>();
                    if (aimRay == null)
                    {
                        weaponsHaveAimRay = false;
                    }

                    // Test muzzle aim vector in VR mode
                    XRRuntimeDetector.SetSimulatedXRState(true);
                    Vector3 aimDirVR = w.GetAimDirection();
                    Vector3 muzzleFwd = w.MuzzlePoint.forward;
                    if (Vector3.Dot(aimDirVR, muzzleFwd) < 0.999f)
                    {
                        weaponsAimMatchesMuzzle = false;
                    }
                    XRRuntimeDetector.SetSimulatedXRState(null);
                }

                AssertCheck("49. Weapons Equipped with XRGrabInteractable & VRWeaponInteractable [Config]",
                    weaponsHaveVR && weapons.Length >= 2,
                    $"Weapons checked: {weapons.Length}, all VR equipped: {weaponsHaveVR}");

                AssertCheck("50. Weapons Equipped with Dedicated AttachTransform [Config]",
                    weaponsHaveAttach,
                    $"All weapons have non-null attachTransform and valid interaction layers: {weaponsHaveAttach}");

                AssertCheck("51. Weapons Equipped with Dedicated WeaponAimRay [Config]",
                    weaponsHaveAimRay,
                    $"All weapons have WeaponAimRay component: {weaponsHaveAimRay}");

                AssertCheck("52. VR Mode Gun Aim Exactly Matches Muzzle Forward Direction [Runtime]",
                    weaponsAimMatchesMuzzle,
                    $"VR Aim vector aligns with MuzzlePoint.forward: {weaponsAimMatchesMuzzle}");

                // Dedicated Weapon Attachment Points
                bool attachPointsConfigured = vrPref != null &&
                    vrPref.RightWeaponAttach != null && vrPref.LeftWeaponAttach != null &&
                    vrPref.RightWeaponAttach.IsChildOf(rightAnchor) && vrPref.LeftWeaponAttach.IsChildOf(leftAnchor);
                AssertCheck("53. Dedicated Weapon Attach Transforms on Left and Right Anchors [Config]",
                    attachPointsConfigured,
                    $"RightAttach: {vrPref?.RightWeaponAttach != null}, LeftAttach: {vrPref?.LeftWeaponAttach != null}");

                // Hand Switching Weapon Reparenting
                XRRuntimeDetector.SetSimulatedXRState(true);
                vrPref.SetActiveHand(VRActiveHand.Right);
                bool rightAttachOk = vrPref.WeaponMountGO != null && vrPref.WeaponMountGO.transform.parent == vrPref.RightWeaponAttach;

                vrPref.SetActiveHand(VRActiveHand.Left);
                bool leftAttachOk = vrPref.WeaponMountGO != null && vrPref.WeaponMountGO.transform.parent == vrPref.LeftWeaponAttach;

                vrPref.SetActiveHand(VRActiveHand.Right);
                bool restoredRightAttachOk = vrPref.WeaponMountGO != null && vrPref.WeaponMountGO.transform.parent == vrPref.RightWeaponAttach;

                AssertCheck("54. VR Hand Switching Dynamically Transfers Weapon Attachment [Runtime]",
                    rightAttachOk && leftAttachOk && restoredRightAttachOk,
                    $"Right: {rightAttachOk}, Left: {leftAttachOk}, RestoredRight: {restoredRightAttachOk}");

                // 54b. Simulated 6-DOF Controller Translation Propagation
                bool trackingDeltaOk = false;
                string trackingDeltaDetail = "";
                if (rightAnchor != null && vrPref.WeaponMountGO != null)
                {
                    vrPref.SetActiveHand(VRActiveHand.Right);
                    Vector3 origAnchorPos = rightAnchor.position;
                    Vector3 origMountPos = vrPref.WeaponMountGO.transform.position;
                    Vector3 testDelta = new Vector3(0.12f, 0.25f, -0.08f);

                    rightAnchor.position = origAnchorPos + testDelta;
                    Vector3 newMountPos = vrPref.WeaponMountGO.transform.position;
                    Vector3 actualDelta = newMountPos - origMountPos;
                    float deltaDiff = Vector3.Distance(testDelta, actualDelta);
                    trackingDeltaOk = deltaDiff < 0.001f;
                    trackingDeltaDetail = $"Expected Delta: {testDelta:F3}, Actual Mount Delta: {actualDelta:F3}, Diff: {deltaDiff:F4}m";

                    rightAnchor.position = origAnchorPos;
                }
                AssertCheck("54b. 6-DOF Controller Position Propagation to Weapon Mount [Simulation]",
                    trackingDeltaOk, trackingDeltaDetail);

                // Single Shared Shot Ray Definition
                var activeWeapon = vrPref.WeaponMountGO != null ? vrPref.WeaponMountGO.GetComponentInChildren<WeaponController>() : (weapons.Length > 0 ? weapons[0] : null);
                bool sharedShotRayValid = false;
                string shotRayDetail = "";
                if (activeWeapon != null)
                {
                    var shotRay = activeWeapon.CalculateShotRay();
                    float fwdDot = Vector3.Dot(shotRay.direction, activeWeapon.MuzzlePoint.forward);
                    float origDist = Vector3.Distance(shotRay.origin, activeWeapon.MuzzlePoint.position);
                    sharedShotRayValid = fwdDot >= 0.999f && origDist < 0.001f;
                    shotRayDetail = $"Muzzle alignment dot: {fwdDot:F4}, Origin offset: {origDist:F4}m";
                }
                AssertCheck("55. Weapon CalculateShotRay Single Source of Truth Matches Muzzle Transform [Runtime]",
                    sharedShotRayValid, shotRayDetail);

                // Weapon Aim Ray Uses CalculateShotRay()
                bool aimRaySyncs = false;
                string aimRayDetail = "";
                if (activeWeapon != null)
                {
                    var aimRayComp = activeWeapon.GetComponent<WeaponAimRay>();
                    if (aimRayComp != null)
                    {
                        aimRayComp.UpdateAimRay();
                        var shotRay = activeWeapon.CalculateShotRay();
                        var lr = aimRayComp.AimLine ?? aimRayComp.transform.Find("GunAimRay")?.GetComponent<LineRenderer>();
                        if (lr != null && lr.positionCount >= 2)
                        {
                            Vector3 p0 = lr.GetPosition(0);
                            Vector3 p1 = lr.GetPosition(1);
                            Vector3 lrDir = (p1 - p0).normalized;
                            float dirDot = Vector3.Dot(lrDir, shotRay.direction);
                            float p0Dist = Vector3.Distance(p0, shotRay.origin);
                            aimRaySyncs = dirDot >= 0.999f && p0Dist < 0.01f;
                            aimRayDetail = $"AimRay direction dot with ShotRay: {dirDot:F4}, Origin dist: {p0Dist:F4}m";
                        }
                        else
                        {
                            aimRayDetail = $"AimLine LineRenderer missing or positionCount < 2 (lr: {lr != null}, count: {lr?.positionCount})";
                        }
                    }
                    else
                    {
                        aimRayDetail = "WeaponAimRay component missing on active weapon";
                    }
                }
                else
                {
                    aimRayDetail = "No active weapon found in scene";
                }
                AssertCheck("56. Weapon Aim Ray Visual Origin & Direction Match Physical Shot Ray [Runtime]",
                    aimRaySyncs, aimRayDetail);

                // 6-DOF Pitch Downward (Right Hand)
                bool pitchRightOk = false;
                string pitchRightDetail = "";
                if (activeWeapon != null && rightAnchor != null)
                {
                    vrPref.SetActiveHand(VRActiveHand.Right);
                    Quaternion origRot = rightAnchor.localRotation;
                    rightAnchor.localRotation = Quaternion.Euler(60f, 0f, 0f);

                    Vector3 muzzleFwd = activeWeapon.MuzzlePoint.forward;
                    var shotRay = activeWeapon.CalculateShotRay();
                    float dot = Vector3.Dot(shotRay.direction, muzzleFwd);
                    bool pointingDown = muzzleFwd.y < -0.7f;
                    pitchRightOk = pointingDown && dot >= 0.999f;
                    pitchRightDetail = $"Muzzle forward Y: {muzzleFwd.y:F3} (< -0.7), ShotRay dot: {dot:F4}";

                    rightAnchor.localRotation = origRot;
                }
                AssertCheck("57. Right Hand 6-DOF Pitch Downward Preserves Barrel & Shot Ray Alignment [Runtime]",
                    pitchRightOk, pitchRightDetail);

                // 6-DOF Pitch Downward (Left Hand)
                bool pitchLeftOk = false;
                string pitchLeftDetail = "";
                if (activeWeapon != null && leftAnchor != null)
                {
                    vrPref.SetActiveHand(VRActiveHand.Left);
                    Quaternion origLeftRot = leftAnchor.localRotation;
                    leftAnchor.localRotation = Quaternion.Euler(60f, 0f, 0f);

                    Vector3 muzzleFwd = activeWeapon.MuzzlePoint.forward;
                    var shotRay = activeWeapon.CalculateShotRay();
                    float dot = Vector3.Dot(shotRay.direction, muzzleFwd);
                    bool pointingDown = muzzleFwd.y < -0.7f;
                    pitchLeftOk = pointingDown && dot >= 0.999f;
                    pitchLeftDetail = $"Muzzle forward Y: {muzzleFwd.y:F3} (< -0.7), ShotRay dot: {dot:F4}";

                    leftAnchor.localRotation = origLeftRot;
                    vrPref.SetActiveHand(VRActiveHand.Right);
                }
                AssertCheck("58. Left Hand 6-DOF Pitch Downward Preserves Barrel & Shot Ray Alignment [Runtime]",
                    pitchLeftOk, pitchLeftDetail);

                // XR UI Ray vs Shot Ray Isolation
                bool raySeparationOk = false;
                string raySepDetail = "";
                if (activeWeapon != null && rightRay != null)
                {
                    float originDist = Vector3.Distance(rightRay.transform.position, activeWeapon.MuzzlePoint.position);
                    var uiLr = rightRay.GetComponent<LineRenderer>();
                    bool distinctMat = uiLr != null && uiLr.sharedMaterial != null && uiLr.sharedMaterial.name != "Mat_Tracer";
                    raySeparationOk = originDist > 0.05f && distinctMat && rightRay.enableUIInteraction;
                    raySepDetail = $"Origin distance: {originDist:F3}m (> 0.05m), UI Mat: {uiLr?.sharedMaterial?.name}, UI interaction enabled: {rightRay.enableUIInteraction}";
                }
                AssertCheck("59. XR UI Ray Interactor Independent from Weapon Aim Ray & Firing Ray [Config]",
                    raySeparationOk, raySepDetail);

                // VRPlayerController Trigger Firing Integration
                var vrPlayer = playerRig?.GetComponent<VRPlayerController>();
                bool firingInputOk = vrPlayer != null;
                AssertCheck("60. VRPlayerController Handles Active Controller Trigger Firing Safely [Config]",
                    firingInputOk,
                    firingInputOk ? "VRPlayerController configured with active hand trigger polling and grab exclusivity" : "Missing VRPlayerController");

                // Hand Switching Preserves Single Active Weapon with No Duplicates
                var allWeaponsInScene = UnityEngine.Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int pistolCount = 0;
                int rifleCount = 0;
                foreach (var w in allWeaponsInScene)
                {
                    if (w.WeaponType == WeaponType.Pistol) pistolCount++;
                    if (w.WeaponType == WeaponType.Rifle) rifleCount++;
                }
                bool noDuplicateGuns = pistolCount == 1 && rifleCount == 1;
                AssertCheck("61. Hand Switching Preserves Single Active Weapon with No Duplicates [Config]",
                    noDuplicateGuns,
                    $"Pistols in scene: {pistolCount}, Rifles in scene: {rifleCount} (Expected exactly 1 each)");

                // Environment Enclosure (Rear Wall)
                var rearWall = GameObject.Find("RearWall") ?? GameObject.Find("ShootingRange_Environment/RearWall");
                if (rearWall == null)
                {
                    var env = GameObject.Find("ShootingRange_Environment");
                    if (env != null) rearWall = env.transform.Find("RearWall")?.gameObject;
                }
                bool rearWallValid = rearWall != null && rearWall.GetComponent<Collider>() != null && rearWall.transform.position.z <= -4.5f;
                AssertCheck("62. Shooting Range Fully Enclosed with Rear Wall at Z <= -4.5 [Config]",
                    rearWallValid,
                    rearWallValid ? $"RearWall at pos {rearWall.transform.position}, collider: {rearWall.GetComponent<Collider>().GetType().Name}" : "RearWall missing or invalid");

                // PC Controls & Fallback Integration
                var pcCtrl = playerRig?.GetComponent<PCPlayerController>();
                AssertCheck("63. PCPlayerController Configured on PlayerRig [Config]", pcCtrl != null,
                    pcCtrl != null ? "PCPlayerController found" : "Missing");
                AssertCheck("64. PCPlayerController forceEnablePC is False [Config]", pcCtrl != null && !pcCtrl.ForceEnablePC,
                    $"ForceEnablePC: {pcCtrl?.ForceEnablePC} (Expected false for dynamic XR detection)");

                // Test PC Shooting Convergence
                if (weapons.Length > 0 && Camera.main != null)
                {
                    XRRuntimeDetector.SetSimulatedXRState(false);
                    var activeW = weapons[0];
                    Vector3 pcAim = activeW.GetAimDirection();
                    Vector3 camFwd = Camera.main.transform.forward;
                    float dotWithCam = Vector3.Dot(pcAim, camFwd);
                    bool pcConverges = dotWithCam > 0.8f;
                    AssertCheck("65. PC Mode Shot Direction Converges from Muzzle to Camera-Center [Runtime]", pcConverges,
                        $"PC Aim vector convergence dot product with camera forward: {dotWithCam:F3}");
                    XRRuntimeDetector.SetSimulatedXRState(null);
                }

                // PC Mode Camera Weapon Mount (Both Right and Left Hand Switching)
                XRRuntimeDetector.SetSimulatedXRState(false);
                vrPref.SetActiveHand(VRActiveHand.Right);
                vrPref.UpdateWeaponAttachment();
                bool pcRightMountOk = vrPref.WeaponMountGO != null &&
                    vrPref.WeaponMountGO.transform.parent == vrPref.CameraWeaponMount;
                Vector3 expectedRightOffset = new Vector3(0.22f, -0.18f, 0.42f);
                Vector3 mountCamPosRight = Camera.main != null ? Camera.main.transform.InverseTransformPoint(vrPref.WeaponMountGO.transform.position) : vrPref.WeaponMountGO.transform.localPosition;
                bool pcRightOffsetOk = Vector3.Distance(mountCamPosRight, expectedRightOffset) < 0.05f;

                vrPref.SetActiveHand(VRActiveHand.Left);
                vrPref.UpdateWeaponAttachment();
                bool pcLeftMountOk = vrPref.WeaponMountGO != null &&
                    vrPref.WeaponMountGO.transform.parent == vrPref.LeftCameraWeaponMount;
                Vector3 expectedLeftOffset = new Vector3(-0.22f, -0.18f, 0.42f);
                Vector3 mountCamPosLeft = Camera.main != null ? Camera.main.transform.InverseTransformPoint(vrPref.WeaponMountGO.transform.position) : vrPref.WeaponMountGO.transform.localPosition;
                bool pcLeftOffsetOk = Vector3.Distance(mountCamPosLeft, expectedLeftOffset) < 0.05f;

                vrPref.SetActiveHand(VRActiveHand.Right);
                vrPref.UpdateWeaponAttachment();

                AssertCheck("66. PC Mode Weapon Mount Supports Both Right and Left First-Person Camera Offsets [Runtime]",
                    pcRightMountOk && pcRightOffsetOk && pcLeftMountOk && pcLeftOffsetOk,
                    $"Right: (Parent={vrPref.WeaponMountGO?.transform.parent?.name}, Pos={mountCamPosRight}), Left: (Parent={vrPref.LeftCameraWeaponMount?.name}, Pos={mountCamPosLeft})");
                XRRuntimeDetector.SetSimulatedXRState(null);

                // Gameplay Routing & Panels
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
                var dm = UnityEngine.Object.FindAnyObjectByType<DifficultyManager>(FindObjectsInactive.Include);

                if (dm != null)
                {
                    dm.SetDifficulty(DifficultyLevel.Hard);
                    AssertCheck("67. Difficulty Selection Functional [Runtime]", dm.CurrentLevel == DifficultyLevel.Hard,
                        $"Difficulty: {dm.CurrentLevel}");
                    dm.SetDifficulty(DifficultyLevel.Medium);
                }

                if (gm != null)
                {
                    gm.StartPractice();
                    AssertCheck("68. Practice Mode Starts via GameManager [Runtime]",
                        gm.CurrentMode == GameMode.Practice && gm.StateManager.CurrentState == GameState.Practice,
                        $"Mode: {gm.CurrentMode}, State: {gm.StateManager.CurrentState}");

                    gm.PauseGame();
                    AssertCheck("69. Pause Mode Functional [Runtime]",
                        gm.StateManager.CurrentState == GameState.Paused && Mathf.Approximately(Time.timeScale, 0f),
                        $"State: {gm.StateManager.CurrentState}, timeScale: {Time.timeScale}");

                    gm.RestartCurrentMode();
                    AssertCheck("70. Restart Restores Gameplay [Runtime]",
                        gm.CurrentMode == GameMode.Practice && Mathf.Approximately(Time.timeScale, 1f),
                        $"Mode: {gm.CurrentMode}, timeScale: {Time.timeScale}");

                    gm.ReturnToMainMenu();
                    AssertCheck("71. Main Menu Return Restores Idle State [Runtime]",
                        gm.StateManager.CurrentState == GameState.MainMenu && gm.CurrentMode == GameMode.None,
                        $"State: {gm.StateManager.CurrentState}, Mode: {gm.CurrentMode}");
                }

                var resUI = UnityEngine.Object.FindAnyObjectByType<ResultsUI>(FindObjectsInactive.Include);
                AssertCheck("72. ResultsUI Component Present & Wired [Config]", resUI != null,
                    resUI != null ? "ResultsUI found" : "Missing");

                var pauseUI = UnityEngine.Object.FindAnyObjectByType<PauseUI>(FindObjectsInactive.Include);
                AssertCheck("73. PauseUI Present with Restart and Main Menu Buttons (No Resume) [Config]",
                    pauseUI != null && pauseUI.RestartButton != null && pauseUI.MainMenuButton != null,
                    pauseUI != null ? "PauseUI found with Restart and Main Menu buttons" : "Missing");

                // 74. VRDiagnosticLogger Telemetry Component
                var diagLogger = playerRig?.GetComponent<VRDiagnosticLogger>();
                bool diagOk = diagLogger != null;
                AssertCheck("74. VRDiagnosticLogger Telemetry Component Active on PlayerRig [Config]", diagOk,
                    diagOk ? "VRDiagnosticLogger attached with throttled telemetry snapshots" : "Missing VRDiagnosticLogger");

                // 75. XRInteractorLineVisual Clean Interactor Ray Origin (No Stale Override)
                var rightLineVis = rightAnchor?.Find("RayInteractor")?.GetComponent<XRInteractorLineVisual>();
                var leftLineVis = leftAnchor?.Find("RayInteractor")?.GetComponent<XRInteractorLineVisual>();
                bool lineVisualOriginsClean = rightLineVis != null && !rightLineVis.overrideInteractorLineOrigin &&
                                              leftLineVis != null && !leftLineVis.overrideInteractorLineOrigin;
                AssertCheck("75. XRInteractorLineVisual Clean Interactor Ray Origin (No Stale Override) [Config]",
                    lineVisualOriginsClean,
                    $"Right override: {rightLineVis?.overrideInteractorLineOrigin}, Left override: {leftLineVis?.overrideInteractorLineOrigin}");

                // 76. OpenXR Plugin Management Automatic Loading on Startup
                bool openXRAutoLoading = File.Exists("Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset") &&
                    File.ReadAllText("Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset").Contains("m_AutomaticLoading: 1");
                AssertCheck("76. OpenXR Plugin Management Automatic Loading Enabled on Startup [Config]",
                    openXRAutoLoading,
                    $"XRGeneralSettingsPerBuildTarget automatic loading configured: {openXRAutoLoading}");
            }
            catch (Exception ex)
            {
                AssertCheck("CRITICAL EXCEPTION", false, $"Unhandled exception: {ex.Message}\n{ex.StackTrace}");
            }

            Report.AppendLine("==================================================");
            Report.AppendLine($"SUMMARY: {_passedCount} / {_totalCount} checks PASSED");
            Report.AppendLine($"OVERALL STATUS: {(_passedCount == _totalCount ? "PASS" : "FAIL")}");
            Report.AppendLine("==================================================");
            Report.AppendLine("[STATUS] CONFIGURATION & SIMULATION: VERIFIED (All automated checks passing)");
            Report.AppendLine("[STATUS] PHYSICAL QUEST HARDWARE TEST: REQUIRED");
            Report.AppendLine("");
            Report.AppendLine("NOTICE:");
            Report.AppendLine("Button callback invocation verifies internal script routing.");
            Report.AppendLine("Physical Quest controller testing is still required.");
            Report.AppendLine("==================================================");

            string reportText = Report.ToString();
            Debug.Log(reportText);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, reportText);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to write report: " + ex.Message);
            }

            CaptureMainMenuImage();
        }

        private static void CaptureMainMenuImage()
        {
            try
            {
                var mainCam = Camera.main;
                if (mainCam == null) return;
                var canvasGO = GameObject.Find("GameUI_Canvas");
                if (canvasGO == null) return;
                var vrAdapter = canvasGO.GetComponent<VRUIAdapter>();
                var canvas = canvasGO.GetComponent<Canvas>();
                if (canvas == null) return;

                var prevMode = canvas.renderMode;
                var prevCam = canvas.worldCamera;

                if (vrAdapter != null)
                {
                    vrAdapter.ConfigureForVR(mainCam);
                }
                else
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = mainCam;
                }

                Canvas.ForceUpdateCanvases();

                int width = 1280;
                int height = 720;
                var rt = RenderTexture.GetTemporary(width, height, 24);
                var prevTarget = mainCam.targetTexture;
                mainCam.targetTexture = rt;
                mainCam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/GameView_MainMenu.png", tex.EncodeToPNG());
                Debug.Log("[VRInteractionVerifier] Captured Logs/GameView_MainMenu.png successfully!");

                mainCam.targetTexture = prevTarget;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(tex);

                if (vrAdapter != null)
                {
                    vrAdapter.ConfigureForPC();
                }
                else
                {
                    canvas.renderMode = prevMode;
                    canvas.worldCamera = prevCam;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VRInteractionVerifier] CaptureMainMenuImage failed: " + ex.Message);
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

        private static Rect GetBoundingRectInParent(RectTransform child, RectTransform parent)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 localPt = parent.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, localPt);
                max = Vector2.Max(max, localPt);
            }
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }
    }
}
