using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRShootingGallery.VR;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Manages platform-aware EventSystem input modules to eliminate input module conflicts:
    /// - When VR / XR is active: XRUIInputModule is enabled, InputSystemUIInputModule & StandaloneInputModule are disabled.
    /// - When PC / XR is inactive: InputSystemUIInputModule is enabled, XRUIInputModule & StandaloneInputModule are disabled.
    /// Guarantees exactly one active UI input module at runtime and exactly one EventSystem in the scene.
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public class EventSystemPlatformAdapter : MonoBehaviour
    {
        [Header("UI Input Modules")]
        [SerializeField] private XRUIInputModule xrInputModule;
        [SerializeField] private InputSystemUIInputModule inputSystemModule;
        [SerializeField] private StandaloneInputModule standaloneModule;

        private bool? _currentModeIsXR = null;

        public XRUIInputModule XRInputModule => xrInputModule;
        public InputSystemUIInputModule InputSystemModule => inputSystemModule;
        public StandaloneInputModule StandaloneModule => standaloneModule;

        private void Awake()
        {
            ValidateSingleEventSystem();
            ResolveReferences();
            ApplyPlatformConfiguration();
        }

        private void Start()
        {
            ApplyPlatformConfiguration();
            XRRuntimeDetector.OnXRModeChanged += HandleXRModeChanged;
        }

        private void OnDestroy()
        {
            XRRuntimeDetector.OnXRModeChanged -= HandleXRModeChanged;
        }

        private void HandleXRModeChanged(bool isXR)
        {
            SetPlatformMode(isXR);
        }

        /// <summary>
        /// Ensures there is strictly only one EventSystem GameObject in the active scene.
        /// </summary>
        public void ValidateSingleEventSystem()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (eventSystems.Length > 1)
            {
                Debug.LogWarning($"[EventSystemPlatformAdapter] Found {eventSystems.Length} EventSystems in scene. Keeping {gameObject.name} and disabling duplicates.");
                for (int i = 0; i < eventSystems.Length; i++)
                {
                    if (eventSystems[i].gameObject != gameObject)
                    {
                        eventSystems[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Locates and caches required input module components on this EventSystem.
        /// </summary>
        public void ResolveReferences()
        {
            if (xrInputModule == null) xrInputModule = GetComponent<XRUIInputModule>();
            if (inputSystemModule == null) inputSystemModule = GetComponent<InputSystemUIInputModule>();
            if (standaloneModule == null) standaloneModule = GetComponent<StandaloneInputModule>();
        }

        /// <summary>
        /// Detects active XR device via centralized XRRuntimeDetector and applies the appropriate single active input module configuration.
        /// </summary>
        public void ApplyPlatformConfiguration()
        {
            ResolveReferences();
            bool isXR = XRRuntimeDetector.IsXRActive;
            SetPlatformMode(isXR);
        }

        /// <summary>
        /// Configures the active UI input module for the specified platform mode.
        /// Ensures exactly one UI input module is enabled at all times.
        /// </summary>
        /// <param name="isXR">True if XR/VR interaction is active; false for PC desktop mouse interaction.</param>
        public void SetPlatformMode(bool isXR)
        {
            ResolveReferences();

            // StandaloneInputModule is legacy and must ALWAYS remain disabled
            if (standaloneModule != null)
            {
                standaloneModule.enabled = false;
            }

            bool modeChanged = !_currentModeIsXR.HasValue || (_currentModeIsXR.Value != isXR);
            _currentModeIsXR = isXR;

            if (isXR)
            {
                // In VR: disable InputSystemUIInputModule first, then enable XRUIInputModule
                if (inputSystemModule != null)
                {
                    inputSystemModule.enabled = false;
                }

                if (xrInputModule != null)
                {
                    xrInputModule.enabled = true;
                    xrInputModule.enableXRInput = true;
                    xrInputModule.enableMouseInput = true;
                }

                if (modeChanged)
                {
                    Debug.Log("[EventSystemPlatformAdapter] Mode changed: VR");
                    Debug.Log("[EventSystemPlatformAdapter] Active UI Module: XRUIInputModule (VR Mode).");
                }
            }
            else
            {
                // On PC: disable XRUIInputModule first, then enable InputSystemUIInputModule
                if (xrInputModule != null)
                {
                    xrInputModule.enabled = false;
                }

                if (inputSystemModule != null)
                {
                    inputSystemModule.enabled = true;
                }

                if (modeChanged)
                {
                    Debug.Log("[EventSystemPlatformAdapter] Mode changed: PC");
                    Debug.Log("[EventSystemPlatformAdapter] Active UI Module: InputSystemUIInputModule (PC Mode).");
                }
            }

            // Runtime verification: Never allow both modules to be simultaneously active
            if (EnabledModuleCount != 1)
            {
                Debug.LogError($"[EventSystemPlatformAdapter] Module exclusivity violation! Exactly 1 module must be active, found: {EnabledModuleCount}");
            }
        }

        /// <summary>
        /// Count of currently enabled UI input modules on this EventSystem.
        /// Must be exactly 1 at all times.
        /// </summary>
        public int EnabledModuleCount
        {
            get
            {
                ResolveReferences();
                int count = 0;
                if (xrInputModule != null && xrInputModule.enabled) count++;
                if (inputSystemModule != null && inputSystemModule.enabled) count++;
                if (standaloneModule != null && standaloneModule.enabled) count++;
                return count;
            }
        }
    }
}
