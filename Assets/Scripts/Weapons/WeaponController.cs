using System;
using UnityEngine;
using VRShootingGallery.Scoring;
using VRShootingGallery.Targets;
using VRShootingGallery.VR;

namespace VRShootingGallery.Weapons
{
    /// <summary>
    /// Base controller for virtual weapons (Pistol and Rifle).
    /// Handles trigger firing, raycasting, audio/visual muzzle feedback, and hit registration.
    /// Can be activated via XR Grab Interactable events or PC input fallback.
    /// </summary>
    [RequireComponent(typeof(WeaponAmmo))]
    public class WeaponController : MonoBehaviour
    {
        [Header("Weapon Identity")]
        [SerializeField] private WeaponType weaponType = WeaponType.Pistol;
        [SerializeField] private string weaponName = "Pistol";

        [Header("Ballistics & Tuning")]
        [Tooltip("Minimum time in seconds between consecutive shots.")]
        [SerializeField] private float fireRate = 0.2f;

        [Tooltip("Maximum raycast travel distance in meters.")]
        [SerializeField] private float maxRange = 100.0f;

        [Tooltip("Transform representing the barrel tip where raycasts originate.")]
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("Physics layers that can be hit by raycasts.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fireSound;
        [SerializeField] private AudioClip emptySound;
        [SerializeField] private AudioClip reloadSound;

        [Header("Visual Feedback")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private GameObject bulletImpactPrefab;
        [SerializeField] private LineRenderer tracerLine;
        [SerializeField] private float tracerDuration = 0.05f;

        [Header("Component References")]
        [SerializeField] private WeaponAmmo weaponAmmo;

        // Runtime state
        private float _lastFireTime = -999f;
        private bool _isGrabbed;
        private ScoreManager _scoreManager;

        public event Action OnWeaponFired;
        public event Action<RaycastHit> OnWeaponHit;

        public WeaponType WeaponType => weaponType;
        public string WeaponName => weaponName;
        public WeaponAmmo Ammo => weaponAmmo;
        public bool IsGrabbed => _isGrabbed;

        private void Awake()
        {
            if (weaponAmmo == null) weaponAmmo = GetComponent<WeaponAmmo>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (muzzlePoint == null) muzzlePoint = transform;

            _scoreManager = FindObjectOfType<ScoreManager>();
        }

        private void Start()
        {
            if (weaponAmmo != null)
            {
                weaponAmmo.OnReloadStarted += HandleReloadStarted;
            }
        }

        private void OnDestroy()
        {
            if (weaponAmmo != null)
            {
                weaponAmmo.OnReloadStarted -= HandleReloadStarted;
            }
        }

        private void HandleReloadStarted()
        {
            if (audioSource != null && reloadSound != null)
            {
                audioSource.PlayOneShot(reloadSound);
            }
        }

        /// <summary>
        /// Fires the weapon if cooldown has elapsed and ammunition is available.
        /// Can be wired directly to XR Interaction Toolkit's "activated" UnityEvent.
        /// </summary>
        public bool Fire()
        {
            if (Time.time < _lastFireTime + fireRate)
            {
                return false;
            }

            _lastFireTime = Time.time;

            if (weaponAmmo == null || !weaponAmmo.ConsumeRound())
            {
                PlayEmptyFeedback();
                return false;
            }

            // Register shot count with ScoreManager
            if (_scoreManager == null) _scoreManager = FindObjectOfType<ScoreManager>();
            if (_scoreManager != null)
            {
                _scoreManager.RegisterShot();
            }

            // Discharge raycast and feedback
            PerformRaycast();
            PlayMuzzleFeedback();

            OnWeaponFired?.Invoke();
            return true;
        }

        public Transform MuzzlePoint => muzzlePoint != null ? muzzlePoint : transform;
        public Transform WeaponAimTransform => muzzlePoint != null ? muzzlePoint : transform;

        /// <summary>
        /// Shared physical shot ray data containing exact origin and direction.
        /// </summary>
        public struct WeaponShotRay
        {
            public Vector3 origin;
            public Vector3 direction;

            public WeaponShotRay(Vector3 origin, Vector3 direction)
            {
                this.origin = origin;
                this.direction = direction;
            }

            public Ray ToRay() => new Ray(origin, direction);
            public Vector3 GetPoint(float distance) => origin + direction * distance;
        }

        /// <summary>
        /// Returns the single authoritative shot ray origin and direction:
        /// - In VR: originates from active weapon's physical muzzle point and points along muzzle forward.
        /// - On PC: converges from muzzle point to camera-center aim point.
        /// Guaranteed identical across physics firing raycast, visual aiming ray, and bullet tracers.
        /// </summary>
        public WeaponShotRay CalculateShotRay()
        {
            Transform muzzle = MuzzlePoint;
            Vector3 origin = muzzle.position;
            Camera cam = Camera.main;
            bool isVR = XRRuntimeDetector.IsXRActive || _isGrabbed;

            if (!isVR && cam != null)
            {
                Ray camRay = new Ray(cam.transform.position, cam.transform.forward);
                Vector3 aimPoint;
                if (Physics.Raycast(camRay, out RaycastHit camHit, maxRange, hitLayers))
                {
                    aimPoint = camHit.point;
                }
                else
                {
                    aimPoint = camRay.origin + camRay.direction * maxRange;
                }
                Vector3 dir = (aimPoint - origin).normalized;
                return new WeaponShotRay(origin, dir);
            }

            return new WeaponShotRay(origin, muzzle.forward);
        }

        /// <summary>
        /// Returns the weapon aim direction calculated via CalculateShotRay().
        /// </summary>
        public Vector3 GetAimDirection()
        {
            return CalculateShotRay().direction;
        }

        private void PerformRaycast()
        {
            WeaponShotRay shotRay = CalculateShotRay();
            Vector3 origin = shotRay.origin;
            Vector3 direction = shotRay.direction;
            Vector3 endPoint = shotRay.GetPoint(maxRange);

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, hitLayers))
            {
                endPoint = hit.point;
                ProcessHit(hit);
                OnWeaponHit?.Invoke(hit);
            }
            else
            {
                // Raycast went into empty air — register a miss
                if (_scoreManager != null)
                {
                    _scoreManager.RegisterMiss();
                }
            }

            ShowTracer(origin, endPoint);
        }

        private void ProcessHit(RaycastHit hit)
        {
            // Check for specific concentric ring zone first (Bullseye, Center, Normal)
            TargetZone targetZone = hit.collider.GetComponent<TargetZone>() ?? hit.collider.GetComponentInParent<TargetZone>();
            TargetBase target = hit.collider.GetComponent<TargetBase>() ?? hit.collider.GetComponentInParent<TargetBase>();

            if (targetZone != null)
            {
                HitInfo hitInfo = targetZone.CreateHitInfo(hit.point, hit.normal);
                if (target != null)
                {
                    target.OnHit(hitInfo);
                }
                else if (_scoreManager != null)
                {
                    _scoreManager.RegisterHit(hitInfo.HitType, hitInfo.BasePoints);
                }
            }
            else if (target != null)
            {
                HitInfo hitInfo = new HitInfo(HitType.Normal, hit.point, hit.normal, target.gameObject, target.BasePoints);
                target.OnHit(hitInfo);
            }
            else
            {
                // Hit scenery or non-target collider — register miss
                if (_scoreManager != null)
                {
                    _scoreManager.RegisterMiss();
                }

                SpawnImpactEffect(hit);
            }
        }

        private void SpawnImpactEffect(RaycastHit hit)
        {
            if (bulletImpactPrefab != null)
            {
                GameObject impact = Instantiate(bulletImpactPrefab, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 2.0f);
            }
        }

        private void PlayMuzzleFeedback()
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.Play();
            }

            if (audioSource != null && fireSound != null)
            {
                audioSource.PlayOneShot(fireSound);
            }
        }

        private void PlayEmptyFeedback()
        {
            if (audioSource != null && emptySound != null)
            {
                audioSource.PlayOneShot(emptySound);
            }
        }

        private void ShowTracer(Vector3 start, Vector3 end)
        {
            if (tracerLine != null)
            {
                StartCoroutine(TracerRoutine(start, end));
            }
        }

        private System.Collections.IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
            tracerLine.enabled = true;
            tracerLine.SetPosition(0, start);
            tracerLine.SetPosition(1, end);

            yield return new WaitForSeconds(tracerDuration);

            tracerLine.enabled = false;
        }

        /// <summary>
        /// Requests a reload on this weapon.
        /// </summary>
        public void Reload()
        {
            if (weaponAmmo != null)
            {
                weaponAmmo.Reload();
            }
        }

        /// <summary>
        /// Called when weapon is grabbed in VR.
        /// </summary>
        public void OnGrabbed()
        {
            _isGrabbed = true;
        }

        /// <summary>
        /// Called when weapon is released in VR.
        /// </summary>
        public void OnReleased()
        {
            _isGrabbed = false;
        }
    }
}
