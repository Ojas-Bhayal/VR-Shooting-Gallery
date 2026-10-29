using System;
using System.Collections;
using UnityEngine;
using VRShootingGallery.Difficulty;
using VRShootingGallery.Scoring;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Abstract base class for all target variants (Static, Moving, Popup).
    /// Handles hit detection, audio/visual reactions, and lifecycle events.
    /// </summary>
    public abstract class TargetBase : MonoBehaviour
    {
        [Header("Target Base Configuration")]
        [SerializeField] private int basePoints = 10;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private Renderer targetRenderer;

        [Header("Visual Feedback")]
        [Tooltip("Optional hit flash color.")]
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private ParticleSystem hitParticle;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip bullseyeSound;

        // State tracking
        protected bool _isHit;
        protected bool _isActive = true;
        private Color _originalColor;
        private Coroutine _flashCoroutine;

        // Events
        public event Action<TargetBase, HitInfo> OnHitEvent;
        public event Action<TargetBase> OnDespawnEvent;

        public bool IsHit => _isHit;
        public bool IsActive => _isActive;
        public int BasePoints => basePoints;
        public Collider TargetCollider => targetCollider != null ? targetCollider : (targetCollider = GetComponent<Collider>());
        public Vector3 TargetCenter => transform.position + Vector3.up * (1.2f * transform.localScale.y);

        protected virtual void Awake()
        {
            if (targetCollider == null) targetCollider = GetComponent<Collider>();
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (targetRenderer != null && targetRenderer.material != null)
            {
                _originalColor = targetRenderer.material.color;
            }
        }

        /// <summary>
        /// Applies difficulty settings (scale, speed, timing) to this target.
        /// </summary>
        public virtual void ApplyDifficulty(DifficultyData difficulty)
        {
            if (difficulty != null)
            {
                transform.localScale = Vector3.one * difficulty.TargetScale;
            }
        }

        /// <summary>
        /// Called when weapon shot collides with this target.
        /// Subclasses implement custom behavior (dropping, popping down, resetting).
        /// </summary>
        public virtual void OnHit(HitInfo hitInfo)
        {
            if (_isHit || !_isActive) return;

            _isHit = true;

            PlayHitFeedback(hitInfo);
            NotifyScoreManager(hitInfo);

            OnHitEvent?.Invoke(this, hitInfo);
        }

        protected virtual void PlayHitFeedback(HitInfo hitInfo)
        {
            // Audio feedback
            if (audioSource != null)
            {
                AudioClip clipToPlay = (hitInfo.HitType == HitType.Bullseye && bullseyeSound != null)
                    ? bullseyeSound
                    : hitSound;

                if (clipToPlay != null)
                {
                    audioSource.PlayOneShot(clipToPlay);
                }
            }

            // Visual particle feedback
            if (hitParticle != null)
            {
                hitParticle.transform.position = hitInfo.Point;
                hitParticle.Play();
            }

            // Material flash feedback
            if (targetRenderer != null && gameObject.activeInHierarchy)
            {
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(FlashRoutine());
            }
        }

        private IEnumerator FlashRoutine()
        {
            if (targetRenderer != null && targetRenderer.material != null)
            {
                targetRenderer.material.color = hitFlashColor;
                yield return new WaitForSeconds(flashDuration);
                targetRenderer.material.color = _originalColor;
            }
        }

        protected virtual void NotifyScoreManager(HitInfo hitInfo)
        {
            // If ScoreManager exists in scene, register hit
            var scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager != null)
            {
                scoreManager.RegisterHit(hitInfo.HitType, hitInfo.BasePoints);
            }
        }

        /// <summary>
        /// Activates the target for shooting.
        /// </summary>
        public virtual void ActivateTarget()
        {
            _isActive = true;
            _isHit = false;
            if (targetCollider != null) targetCollider.enabled = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Deactivates the target.
        /// </summary>
        public virtual void DeactivateTarget()
        {
            _isActive = false;
            if (targetCollider != null) targetCollider.enabled = false;
            OnDespawnEvent?.Invoke(this);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Resets target state without disabling it.
        /// </summary>
        public virtual void ResetTarget()
        {
            _isHit = false;
            _isActive = true;
            if (targetCollider != null) targetCollider.enabled = true;
            if (targetRenderer != null && targetRenderer.material != null)
            {
                targetRenderer.material.color = _originalColor;
            }
        }
    }
}
