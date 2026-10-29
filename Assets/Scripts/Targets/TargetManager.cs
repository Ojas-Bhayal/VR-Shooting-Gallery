using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRShootingGallery.Core;
using VRShootingGallery.Difficulty;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Coordinates target spawning, lifetime management, active target limits, and cleanup.
    /// Drives target difficulty progression using DifficultyManager configuration.
    /// Enforces target separation, angular fairness, and line-of-sight checks to prevent overlapping or occluded targets.
    /// </summary>
    public class TargetManager : MonoBehaviour
    {
        [Header("Target Prefabs / Pool")]
        [Tooltip("Prefabs for target types to spawn (StaticTarget, MovingTarget, PopupTarget).")]
        [SerializeField] private List<TargetBase> targetPrefabs = new List<TargetBase>();

        [Header("Spawn Points")]
        [Tooltip("Transforms in the range where targets can appear.")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        [Header("Scene Pre-Placed Targets")]
        [Tooltip("Optional scene targets to manage.")]
        [SerializeField] private List<TargetBase> sceneTargets = new List<TargetBase>();

        [Header("Target Separation & Fairness")]
        [Tooltip("Base minimum 3D distance between any two active targets (Unity units).")]
        [SerializeField] private float minimumTargetSeparation = 1.5f;

        [Tooltip("Separation setting for Easy difficulty (approx 1.75m).")]
        [SerializeField] private float easySeparation = 1.75f;

        [Tooltip("Separation setting for Medium difficulty (approx 1.5m).")]
        [SerializeField] private float mediumSeparation = 1.42f;

        [Tooltip("Separation setting for Hard difficulty (approx 1.25m).")]
        [SerializeField] private float hardSeparation = 1.25f;

        [Tooltip("Minimum angular separation between targets from player viewpoint (degrees).")]
        [SerializeField] private float minimumAngularSeparation = 3.5f;

        [Tooltip("Angular separation setting for Easy difficulty (degrees).")]
        [SerializeField] private float easyAngularSeparation = 4.5f;

        [Tooltip("Angular separation setting for Medium difficulty (degrees).")]
        [SerializeField] private float mediumAngularSeparation = 3.0f;

        [Tooltip("Angular separation setting for Hard difficulty (degrees).")]
        [SerializeField] private float hardAngularSeparation = 2.6f;

        [Tooltip("Target center height offset above spawn point (matches target rings center).")]
        [SerializeField] private float targetCenterHeightOffset = 1.2f;

        [Tooltip("Maximum candidate spawn points to evaluate per spawn attempt.")]
        [SerializeField] private int maxSpawnCandidatesToCheck = 36;

        [Header("Dependencies")]
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField] private GameManager gameManager;

        // Active tracking
        private readonly List<TargetBase> _activeTargets = new List<TargetBase>();
        private readonly HashSet<Transform> _temporarilyBlockedSpawnPoints = new HashSet<Transform>();
        private readonly List<TargetBase> _validPrefabsBuffer = new List<TargetBase>();
        private readonly List<Transform> _validSpawnPointsBuffer = new List<Transform>();
        private readonly List<Transform> _candidatePointsBuffer = new List<Transform>();
        private readonly List<TargetBase> _prefabsToTryBuffer = new List<TargetBase>();
        private Coroutine _spawnLoopCoroutine;
        private Coroutine _runtimeFairnessCoroutine;
        private DifficultyData _cachedDifficulty;
        private Camera _cachedMainCamera;

        public IReadOnlyList<TargetBase> ActiveTargets => _activeTargets;
        public List<Transform> SpawnPoints => spawnPoints;
        public float MinimumTargetSeparation => GetCurrentMinimumSeparation();

        public int ActiveTargetCount
        {
            get
            {
                _activeTargets.RemoveAll(t => t == null || !t.IsActive);
                return _activeTargets.Count;
            }
        }

        private void Awake()
        {
            if (difficultyManager == null) difficultyManager = FindObjectOfType<DifficultyManager>();
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

            // Clean sceneTargets list
            sceneTargets.RemoveAll(t => t == null);

            if (difficultyManager != null && _cachedDifficulty == null)
            {
                _cachedDifficulty = difficultyManager.CurrentDifficulty;
            }
        }

        private void Start()
        {
            if (difficultyManager != null)
            {
                difficultyManager.OnDifficultyChanged += OnDifficultyChanged;
                OnDifficultyChanged(difficultyManager.CurrentDifficulty);
            }

            if (gameManager != null)
            {
                gameManager.OnModeStarted += HandleModeStarted;
                gameManager.OnModeEnded += HandleModeEnded;
            }
        }

        private void OnDestroy()
        {
            if (difficultyManager != null)
            {
                difficultyManager.OnDifficultyChanged -= OnDifficultyChanged;
            }

            if (gameManager != null)
            {
                gameManager.OnModeStarted -= HandleModeStarted;
                gameManager.OnModeEnded -= HandleModeEnded;
            }
        }

        public DifficultyData GetActiveDifficulty()
        {
            if (difficultyManager == null)
            {
                difficultyManager = FindObjectOfType<DifficultyManager>();
            }
            if (difficultyManager != null && difficultyManager.CurrentDifficulty != null)
            {
                return difficultyManager.CurrentDifficulty;
            }
            return _cachedDifficulty;
        }

        private void OnDifficultyChanged(DifficultyData newDifficulty)
        {
            _cachedDifficulty = newDifficulty;

            // Update all currently active targets
            var diff = GetActiveDifficulty();
            foreach (var target in _activeTargets)
            {
                if (target != null) target.ApplyDifficulty(diff);
            }
            foreach (var target in sceneTargets)
            {
                if (target != null) target.ApplyDifficulty(diff);
            }
        }

        private void HandleModeStarted(GameMode mode)
        {
            ResetAllTargets();

            if (mode == GameMode.Challenge || mode == GameMode.Practice)
            {
                StartSpawning();
            }
        }

        private void HandleModeEnded(GameMode mode)
        {
            StopSpawning();
            ResetAllTargets();
        }

        public void StartSpawning()
        {
            StopSpawning();
            PopulateInitialTargets();
            _spawnLoopCoroutine = StartCoroutine(SpawnLoop());
            _runtimeFairnessCoroutine = StartCoroutine(RuntimeFairnessRoutine());
        }

        public void StopSpawning()
        {
            if (_spawnLoopCoroutine != null)
            {
                StopCoroutine(_spawnLoopCoroutine);
                _spawnLoopCoroutine = null;
            }

            if (_runtimeFairnessCoroutine != null)
            {
                StopCoroutine(_runtimeFairnessCoroutine);
                _runtimeFairnessCoroutine = null;
            }

            _temporarilyBlockedSpawnPoints.Clear();
        }

        /// <summary>
        /// Lightweight runtime validation (checks ~6.6 Hz) to detect moving targets crossing
        /// lines-of-sight to other spawn points, preventing replacement spawns directly behind moving targets.
        /// </summary>
        private IEnumerator RuntimeFairnessRoutine()
        {
            WaitForSeconds waitInterval = new WaitForSeconds(0.15f);

            while (true)
            {
                yield return waitInterval;

                _temporarilyBlockedSpawnPoints.Clear();
                if (_activeTargets.Count == 0) continue;

                // Optimization: check if any moving targets are active before doing projection math
                bool anyMoving = false;
                for (int i = 0; i < _activeTargets.Count; i++)
                {
                    if (_activeTargets[i] is MovingTarget mt && mt.IsActive)
                    {
                        anyMoving = true;
                        break;
                    }
                }
                if (!anyMoving) continue;

                Vector3 playerEye = GetPlayerEyePosition();

                for (int i = 0; i < _activeTargets.Count; i++)
                {
                    var active = _activeTargets[i];
                    if (active == null || !active.IsActive) continue;

                    if (active is MovingTarget movingTarget)
                    {
                        Vector3 movingCenter = movingTarget.TargetCenter;
                        float movingRadius = 0.55f * movingTarget.transform.localScale.x;

                        for (int spIdx = 0; spIdx < spawnPoints.Count; spIdx++)
                        {
                            Transform sp = spawnPoints[spIdx];
                            if (sp == null) continue;

                            Vector3 spCenter = sp.position + Vector3.up * targetCenterHeightOffset;
                            Vector3 toSp = spCenter - playerEye;
                            float spDist = toSp.magnitude;
                            if (spDist < 0.001f) continue;
                            Vector3 spDir = toSp / spDist;

                            Vector3 toMoving = movingCenter - playerEye;
                            float proj = Vector3.Dot(toMoving, spDir);
                            if (proj > 1.0f && proj < spDist - 0.5f)
                            {
                                Vector3 perp = toMoving - spDir * proj;
                                if (perp.magnitude < movingRadius + 0.6f)
                                {
                                    _temporarilyBlockedSpawnPoints.Add(sp);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Populates active targets up to the difficulty limit upon mode start.
        /// </summary>
        public void PopulateInitialTargets()
        {
            var diff = GetActiveDifficulty();
            int maxActive = diff != null ? diff.MaxActiveTargets : 10;
            int maxAttempts = maxActive * 25;
            int attempts = 0;
            while (ActiveTargetCount < maxActive && attempts < maxAttempts)
            {
                attempts++;
                TrySpawnTarget();
            }
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                var diff = GetActiveDifficulty();
                float interval = diff != null ? diff.SpawnInterval : 2.2f;
                yield return new WaitForSeconds(interval);

                TrySpawnTarget();
            }
        }

        /// <summary>
        /// Attempts to spawn a replacement target if the current active target count is below the difficulty limit.
        /// Returns true if a target was spawned or activated.
        /// </summary>
        public bool TrySpawnTarget()
        {
            var diff = GetActiveDifficulty();
            int maxActive = diff != null ? diff.MaxActiveTargets : 10;

            // Clean nulls or inactive targets before checking limit
            _activeTargets.RemoveAll(t => t == null || !t.IsActive);

            if (_activeTargets.Count < maxActive)
            {
                return SpawnNextTarget();
            }

            return false;
        }

        private bool SpawnNextTarget()
        {
            var diff = GetActiveDifficulty();

            // If scene targets exist and have inactive popup targets, try activating one if valid
            foreach (var sceneTarget in sceneTargets)
            {
                if (sceneTarget is PopupTarget popup && !popup.IsActive)
                {
                    if (IsValidSpawnLocation(popup.transform.position, popup))
                    {
                        popup.ApplyDifficulty(diff);
                        popup.PopUp();
                        popup.OnDespawnEvent -= OnTargetDespawned;
                        popup.OnDespawnEvent += OnTargetDespawned;
                        _activeTargets.Add(popup);
                        return true;
                    }
                }
            }

            // Spawn from prefabs at a validated spawn point
            _validPrefabsBuffer.Clear();
            for (int i = 0; i < targetPrefabs.Count; i++)
            {
                if (targetPrefabs[i] != null) _validPrefabsBuffer.Add(targetPrefabs[i]);
            }

            _validSpawnPointsBuffer.Clear();
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                var sp = spawnPoints[i];
                if (sp != null && !_temporarilyBlockedSpawnPoints.Contains(sp))
                {
                    _validSpawnPointsBuffer.Add(sp);
                }
            }
            if (_validSpawnPointsBuffer.Count == 0)
            {
                // Fallback to all non-null points if every candidate happens to be temporarily blocked
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    var sp = spawnPoints[i];
                    if (sp != null) _validSpawnPointsBuffer.Add(sp);
                }
            }

            if (_validPrefabsBuffer.Count == 0 || _validSpawnPointsBuffer.Count == 0)
            {
                return false;
            }

            // Shuffle candidate spawn points
            _candidatePointsBuffer.Clear();
            _candidatePointsBuffer.AddRange(_validSpawnPointsBuffer);
            for (int i = 0; i < _candidatePointsBuffer.Count; i++)
            {
                int r = Random.Range(i, _candidatePointsBuffer.Count);
                var temp = _candidatePointsBuffer[i];
                _candidatePointsBuffer[i] = _candidatePointsBuffer[r];
                _candidatePointsBuffer[r] = temp;
            }

            Transform selectedPoint = null;
            TargetBase selectedPrefab = null;

            int activeMovingCount = 0;
            for (int i = 0; i < _activeTargets.Count; i++)
            {
                var a = _activeTargets[i];
                if (a is MovingTarget && a.IsActive) activeMovingCount++;
            }

            int candidatesChecked = 0;
            for (int c = 0; c < _candidatePointsBuffer.Count; c++)
            {
                Transform point = _candidatePointsBuffer[c];
                if (point == null) continue;

                // Try each available prefab (fallback to static/popup if moving target corridor is blocked)
                _prefabsToTryBuffer.Clear();
                _prefabsToTryBuffer.AddRange(_validPrefabsBuffer);
                for (int p = 0; p < _prefabsToTryBuffer.Count; p++)
                {
                    int r = Random.Range(p, _prefabsToTryBuffer.Count);
                    var temp = _prefabsToTryBuffer[p];
                    _prefabsToTryBuffer[p] = _prefabsToTryBuffer[r];
                    _prefabsToTryBuffer[r] = temp;
                }

                for (int p = 0; p < _prefabsToTryBuffer.Count; p++)
                {
                    var prefab = _prefabsToTryBuffer[p];
                    if (prefab == null) continue;
                    if (prefab is MovingTarget && activeMovingCount >= 2)
                    {
                        continue;
                    }

                    if (IsValidSpawnLocation(point.position, prefab))
                    {
                        selectedPoint = point;
                        selectedPrefab = prefab;
                        break;
                    }
                }

                if (selectedPoint != null) break;

                candidatesChecked++;
                if (candidatesChecked >= maxSpawnCandidatesToCheck)
                {
                    break;
                }
            }

            // If no valid spawn point found, safely skip without error
            if (selectedPoint == null || selectedPrefab == null)
            {
                return false;
            }

            // Instantiate valid target
            TargetBase instance = Instantiate(selectedPrefab, selectedPoint.position, selectedPoint.rotation);
            if (instance == null) return false;

            instance.ApplyDifficulty(diff);
            instance.ActivateTarget();
            Physics.SyncTransforms();

            instance.OnDespawnEvent += OnTargetDespawned;
            _activeTargets.Add(instance);

            if (instance is PopupTarget popupInstance)
            {
                popupInstance.PopUp();
            }

            return true;
        }

        /// <summary>
        /// Validates whether a candidate spawn position and target prefab are fair, separated, and free of occlusion.
        /// </summary>
        public bool IsValidSpawnLocation(Vector3 candidatePosition, TargetBase candidatePrefab)
        {
            Vector3 playerEye = GetPlayerEyePosition();
            var diff = GetActiveDifficulty();
            float candScale = diff != null ? diff.TargetScale : 1.0f;
            Vector3 candidateCenter = candidatePosition + Vector3.up * (targetCenterHeightOffset * candScale);

            // 1. Environment distance from player table
            float distFromPlayer = Vector3.Distance(playerEye, candidateCenter);
            if (distFromPlayer < 4.0f)
            {
                return false;
            }

            // 2. Moving target wall bounds check (walls at X = +-10)
            bool isCandMoving = candidatePrefab is MovingTarget;
            float candTravelDist = 3.0f;
            if (isCandMoving)
            {
                if (Mathf.Abs(candidatePosition.x) > 5.5f)
                {
                    return false;
                }
            }

            float currentMinSeparation = GetCurrentMinimumSeparation();
            float currentMinAngle = GetCurrentMinimumAngularSeparation();

            Vector3 toCandidate = candidateCenter - playerEye;
            float candDist = toCandidate.magnitude;
            if (candDist < 0.001f) return false;
            Vector3 candDir = toCandidate / candDist;

            // 3. Line-of-sight Raycast Check (Permanent environment obstacles & targets in front)
            RaycastHit hit;
            if (Physics.Raycast(playerEye, candDir, out hit, candDist - 0.35f, ~0, QueryTriggerInteraction.Ignore))
            {
                var hitTarget = hit.collider.GetComponentInParent<TargetBase>();
                if (hitTarget != null && _activeTargets.Contains(hitTarget))
                {
                    // Directly blocked by an existing active target in front of candidate
                    return false;
                }

                if (hitTarget == null)
                {
                    // Completely blocked by wall, divider, or shooting counter
                    return false;
                }
            }

            // 4. Compare against all currently active targets
            foreach (var active in _activeTargets)
            {
                if (active == null || !active.IsActive) continue;

                Vector3 activeCenter = active.TargetCenter;

                // 4a. 3D World Separation Check
                float worldDist = Vector3.Distance(candidateCenter, activeCenter);
                if (worldDist < currentMinSeparation)
                {
                    return false;
                }

                // 4b. Moving Target Corridor Collision Check
                if (active is MovingTarget activeMoving)
                {
                    Vector3 moveStart = activeMoving.StartPosition;
                    float moveTravel = activeMoving.TravelDistance;
                    if (Mathf.Abs(candidatePosition.z - moveStart.z) < currentMinSeparation &&
                        candidatePosition.x >= (moveStart.x - moveTravel - currentMinSeparation * 0.5f) &&
                        candidatePosition.x <= (moveStart.x + moveTravel + currentMinSeparation * 0.5f))
                    {
                        return false;
                    }
                }

                if (isCandMoving)
                {
                    if (Mathf.Abs(active.transform.position.z - candidatePosition.z) < currentMinSeparation &&
                        active.transform.position.x >= (candidatePosition.x - candTravelDist - currentMinSeparation * 0.5f) &&
                        active.transform.position.x <= (candidatePosition.x + candTravelDist + currentMinSeparation * 0.5f))
                    {
                        return false;
                    }
                }

                // 4c. Line-of-Sight Cylinder / Perpendicular Clearance Check from Player Eye
                Vector3 toActive = activeCenter - playerEye;
                float activeDist = toActive.magnitude;
                if (activeDist < 0.001f) continue;
                Vector3 activeDir = toActive / activeDist;

                float minClearance = GetCurrentMinimumClearance();
                float candRadius = Mathf.Max(0.55f * candScale, minClearance);
                float activeRadius = Mathf.Max(0.55f * active.transform.localScale.x, minClearance);

                // Check if active target blocks candidate ray
                float projToActive = Vector3.Dot(toActive, candDir);
                if (projToActive > 0f && projToActive < candDist)
                {
                    Vector3 perp = toActive - candDir * projToActive;
                    if (perp.magnitude < activeRadius)
                    {
                        return false; // Active target intercepts candidate's sightline
                    }
                }

                // Check if candidate blocks active target ray
                float projCand = Vector3.Dot(toCandidate, activeDir);
                if (projCand > 0f && projCand < activeDist)
                {
                    Vector3 perpCand = toCandidate - activeDir * projCand;
                    if (perpCand.magnitude < candRadius)
                    {
                        return false; // Candidate intercepts active target's sightline
                    }
                }

                // 4d. Angular Separation Check from Player's Viewpoint
                float angle = Vector3.Angle(candDir, activeDir);
                if (angle < currentMinAngle)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 GetPlayerEyePosition()
        {
            if (_cachedMainCamera == null)
            {
                _cachedMainCamera = Camera.main;
                if (_cachedMainCamera == null) _cachedMainCamera = FindObjectOfType<Camera>();
            }

            if (_cachedMainCamera != null)
            {
                return _cachedMainCamera.transform.position;
            }

            return new Vector3(0f, 1.7f, 0f);
        }

        private float GetCurrentMinimumSeparation()
        {
            var diff = GetActiveDifficulty();
            if (diff != null)
            {
                switch (diff.Level)
                {
                    case DifficultyLevel.Easy: return easySeparation;
                    case DifficultyLevel.Medium: return mediumSeparation;
                    case DifficultyLevel.Hard: return hardSeparation;
                }
            }
            return minimumTargetSeparation;
        }

        private float GetCurrentMinimumAngularSeparation()
        {
            var diff = GetActiveDifficulty();
            if (diff != null)
            {
                switch (diff.Level)
                {
                    case DifficultyLevel.Easy: return easyAngularSeparation;
                    case DifficultyLevel.Medium: return mediumAngularSeparation;
                    case DifficultyLevel.Hard: return hardAngularSeparation;
                }
            }
            return minimumAngularSeparation;
        }

        private float GetCurrentMinimumClearance()
        {
            var diff = GetActiveDifficulty();
            if (diff != null)
            {
                switch (diff.Level)
                {
                    case DifficultyLevel.Easy: return 0.72f;
                    case DifficultyLevel.Medium: return 0.60f;
                    case DifficultyLevel.Hard: return 0.50f;
                }
            }
            return 0.55f;
        }

        private void OnTargetDespawned(TargetBase target)
        {
            if (target != null)
            {
                target.OnDespawnEvent -= OnTargetDespawned;
                _activeTargets.Remove(target);
                if (!sceneTargets.Contains(target))
                {
                    Destroy(target.gameObject);
                }

                // Attempt to spawn replacement immediately if currently in active gameplay
                if (gameManager != null && (gameManager.CurrentMode == GameMode.Practice || gameManager.CurrentMode == GameMode.Challenge))
                {
                    TrySpawnTarget();
                }
            }
        }

        /// <summary>
        /// Resets or clears all targets across the range.
        /// </summary>
        public void ResetAllTargets()
        {
            // Reset pre-placed targets
            foreach (var target in sceneTargets)
            {
                if (target != null)
                {
                    target.ResetTarget();
                }
            }

            // Destroy dynamically spawned targets
            for (int i = _activeTargets.Count - 1; i >= 0; i--)
            {
                var target = _activeTargets[i];
                if (target != null && !sceneTargets.Contains(target))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(target.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(target.gameObject);
                    }
                }
            }
            _activeTargets.Clear();
            Physics.SyncTransforms();
        }
    }
}
