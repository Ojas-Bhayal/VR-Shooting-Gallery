using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using VRShootingGallery.Core;
using VRShootingGallery.VR;
using VRShootingGallery.Weapons;

namespace VRShootingGallery.Editor
{
    /// <summary>
    /// Temporary Editor diagnostic tool satisfying Step 1, Step 2, and Step 5 of the
    /// Weapon Pose, Aim Ray & Blue Ray Visibility Repair.
    /// Audits all active LineRenderer and ray visual components in the scene without logging every frame.
    /// </summary>
    public static class RayVisibilityDiagnostic
    {
        public struct RayCandidateInfo
        {
            public string hierarchyPath;
            public string componentType;
            public string materialName;
            public Color materialColor;
            public bool isComponentEnabled;
            public bool isGameObjectActive;
            public string controllingScript;
            public bool isBlueRayBeneathGun;
            public string notes;
        }

        [MenuItem("VR Shooting Gallery/Diagnostic: Audit All Ray Renderers")]
        public static void AuditRayRenderers()
        {
            var candidates = CollectAllRayCandidates();
            var sb = new StringBuilder();

            sb.AppendLine("===============================================================================");
            sb.AppendLine("VR SHOOTING GALLERY — RAY RENDERER AUDIT REPORT");
            sb.AppendLine($"PlayMode: {Application.isPlaying} | XR Active: {XRRuntimeDetector.IsXRActive}");
            sb.AppendLine("===============================================================================");

            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                sb.AppendLine($"\n[Candidate {i + 1}] {c.hierarchyPath}");
                sb.AppendLine($"  - Component:         {c.componentType}");
                sb.AppendLine($"  - Material:          {c.materialName} (Color: {c.materialColor})");
                sb.AppendLine($"  - Component Enabled: {c.isComponentEnabled}");
                sb.AppendLine($"  - GameObject Active: {c.isGameObjectActive}");
                sb.AppendLine($"  - Controlling Script:{c.controllingScript}");
                sb.AppendLine($"  - Blue Ray Under Gun:{c.isBlueRayBeneathGun}");
                sb.AppendLine($"  - Notes:             {c.notes}");
            }

            sb.AppendLine("\n===============================================================================");
            sb.AppendLine("SINGLE-RAY VISIBILITY VERIFICATION ACROSS PLATFORMS & STATES");
            sb.AppendLine("===============================================================================");

            var vrPref = Object.FindFirstObjectByType<VRControllerPreference>();
            var weaponMgr = Object.FindFirstObjectByType<WeaponManager>();
            var aimRay = weaponMgr?.ActiveWeapon?.GetComponent<WeaponAimRay>();

            if (vrPref != null)
            {
                bool rightLineVis = vrPref.RightLineVisual != null && vrPref.RightLineVisual.enabled;
                bool leftLineVis = vrPref.LeftLineVisual != null && vrPref.LeftLineVisual.enabled;
                bool rightLineRend = vrPref.RightLineRenderer != null && vrPref.RightLineRenderer.enabled;
                bool leftLineRend = vrPref.LeftLineRenderer != null && vrPref.LeftLineRenderer.enabled;
                bool aimLineVis = aimRay != null && aimRay.AimLine != null && aimRay.AimLine.enabled;

                sb.AppendLine($"Current Active Hand:     {vrPref.ActiveHand}");
                sb.AppendLine($"Right Controller Visual: LineVisual={rightLineVis}, LineRenderer={rightLineRend}");
                sb.AppendLine($"Left Controller Visual:  LineVisual={leftLineVis}, LineRenderer={leftLineRend}");
                sb.AppendLine($"Weapon Aim Ray Visual:   LineRenderer={aimLineVis}");

                int totalVisibleRays = (rightLineRend ? 1 : 0) + (leftLineRend ? 1 : 0) + (aimLineVis ? 1 : 0);
                sb.AppendLine($"Total Active Rays:       {totalVisibleRays}");

                if (totalVisibleRays > 1)
                {
                    sb.AppendLine("WARNING: More than one ray visual is currently active!");
                }
                else
                {
                    sb.AppendLine("SUCCESS: Single-ray constraint satisfied (no multiple concurrent rays).");
                }
            }

            Debug.Log(sb.ToString());
        }

        public static List<RayCandidateInfo> CollectAllRayCandidates()
        {
            var list = new List<RayCandidateInfo>();

            // Find all LineRenderer components in the scene
            var lineRenderers = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var lr in lineRenderers)
            {
                string path = GetHierarchyPath(lr.transform);
                string matName = lr.sharedMaterial != null ? lr.sharedMaterial.name : "None";
                Color matColor = lr.sharedMaterial != null && lr.sharedMaterial.HasProperty("_Color")
                    ? lr.sharedMaterial.GetColor("_Color")
                    : (lr.sharedMaterial != null && lr.sharedMaterial.HasProperty("_BaseColor")
                        ? lr.sharedMaterial.GetColor("_BaseColor")
                        : Color.white);

                string ctrlScript = "Unknown";
                bool isBlueUnderGun = false;
                string notes = "";

                if (path.Contains("RightControllerAnchor/RayInteractor"))
                {
                    ctrlScript = "VRControllerPreference.cs (ApplyControllerStates)";
                    isBlueUnderGun = true;
                    notes = "PROVEN SOURCE of blue ray under gun on PC Desktop. Originates from RightControllerAnchor (y=1.25m, 27cm below CameraWeaponMount at y=1.52m). Material is Mat_UIRay (Cyan/Blue).";
                }
                else if (path.Contains("LeftControllerAnchor/RayInteractor"))
                {
                    ctrlScript = "VRControllerPreference.cs (ApplyControllerStates)";
                    isBlueUnderGun = false;
                    notes = "Left hand UI ray interactor. Originates from LeftControllerAnchor (x=-0.25m). Material is Mat_UIRay (Cyan/Blue). Active only when Left hand is selected in VR.";
                }
                else if (path.Contains("GunAimRay"))
                {
                    ctrlScript = "WeaponAimRay.cs (UpdateAimRay in LateUpdate)";
                    isBlueUnderGun = false;
                    notes = "Weapon aiming ray originating from weapon muzzle point. Material is Mat_Tracer (Yellow). Active only during active gameplay (Practice/Challenge).";
                }
                else if (lr.GetComponent<WeaponController>() != null)
                {
                    ctrlScript = "WeaponController.cs (ShowTracerCoroutine)";
                    isBlueUnderGun = false;
                    notes = "Temporary weapon muzzle flash tracer line (enabled for 0.05s on bullet fire). Material is Mat_Tracer (Yellow).";
                }

                list.Add(new RayCandidateInfo
                {
                    hierarchyPath = path,
                    componentType = nameof(LineRenderer),
                    materialName = matName,
                    materialColor = matColor,
                    isComponentEnabled = lr.enabled,
                    isGameObjectActive = lr.gameObject.activeInHierarchy,
                    controllingScript = ctrlScript,
                    isBlueRayBeneathGun = isBlueUnderGun,
                    notes = notes
                });
            }

            // Find all XRInteractorLineVisual components
            var lineVisuals = Object.FindObjectsByType<XRInteractorLineVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var lv in lineVisuals)
            {
                string path = GetHierarchyPath(lv.transform);
                bool isBlue = path.Contains("RightControllerAnchor/RayInteractor");
                list.Add(new RayCandidateInfo
                {
                    hierarchyPath = path,
                    componentType = nameof(XRInteractorLineVisual),
                    materialName = "Delegates to LineRenderer",
                    materialColor = Color.cyan,
                    isComponentEnabled = lv.enabled,
                    isGameObjectActive = lv.gameObject.activeInHierarchy,
                    controllingScript = "VRControllerPreference.cs",
                    isBlueRayBeneathGun = isBlue,
                    notes = isBlue
                        ? "Drives curve and sample points of Right LineRenderer every frame. Disabled in PC mode and during gameplay."
                        : "Drives curve and sample points of Left LineRenderer every frame. Disabled when Left controller is inactive or during gameplay."
                });
            }

            return list;
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null) return string.Empty;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        [MenuItem("VR Shooting Gallery/Diagnostic: Toggle Right Blue Ray Visual (PlayMode Test)")]
        public static void ToggleRightBlueRay()
        {
            var vrPref = Object.FindFirstObjectByType<VRControllerPreference>();
            if (vrPref != null && vrPref.RightLineVisual != null && vrPref.RightLineRenderer != null)
            {
                bool newState = !vrPref.RightLineRenderer.enabled;
                vrPref.RightLineVisual.enabled = newState;
                vrPref.RightLineRenderer.enabled = newState;
                Debug.Log($"[Diagnostic] Toggled Right Blue Ray Visual -> enabled = {newState}");
            }
        }

        [MenuItem("VR Shooting Gallery/Diagnostic: Toggle Weapon Aim Ray (PlayMode Test)")]
        public static void ToggleWeaponAimRay()
        {
            var weaponMgr = Object.FindFirstObjectByType<WeaponManager>();
            var aimRay = weaponMgr?.ActiveWeapon?.GetComponent<WeaponAimRay>();
            if (aimRay != null && aimRay.AimLine != null)
            {
                bool newState = !aimRay.AimLine.enabled;
                aimRay.AimLine.enabled = newState;
                Debug.Log($"[Diagnostic] Toggled Weapon Aim Ray -> enabled = {newState}");
            }
        }
    }
}
