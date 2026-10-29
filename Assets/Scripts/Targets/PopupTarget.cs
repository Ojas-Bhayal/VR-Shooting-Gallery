using System.Collections;
using UnityEngine;
using VRShootingGallery.Difficulty;
using VRShootingGallery.Scoring;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Popup target that emerges, stays active for a duration defined by DifficultyData,
    /// and retracts if not hit or upon taking a hit.
    /// Satisfies FR-05 (PopupTarget).
    /// </summary>
    public class PopupTarget : TargetBase
    {
        [Header("Popup Settings")]
        [Tooltip("Transform representing the target arm/plate that pivots.")]
        [SerializeField] private Transform pivotTransform;

        [Tooltip("Default duration target stays up before auto-retracting (seconds).")]
        [SerializeField] private float defaultPopupDuration = 3.0f;

        [Tooltip("Speed of the pop up/down rotation animation.")]
        [SerializeField] private float animationSpeed = 8.0f;

        private float _activeDuration;
        private Coroutine _popupLifecycleCoroutine;
        private bool _isPoppedUp;

        private readonly Quaternion _upRotation = Quaternion.identity;
        private readonly Quaternion _downRotation = Quaternion.Euler(90f, 0f, 0f);

        protected override void Awake()
        {
            base.Awake();
            if (pivotTransform == null) pivotTransform = transform;
            _activeDuration = defaultPopupDuration;
            pivotTransform.localRotation = _downRotation;
        }

        public override void ApplyDifficulty(DifficultyData difficulty)
        {
            base.ApplyDifficulty(difficulty);
            if (difficulty != null)
            {
                _activeDuration = difficulty.PopupDuration;
            }
        }

        /// <summary>
        /// Initiates the popup sequence.
        /// </summary>
        public void PopUp()
        {
            if (_isPoppedUp) return;

            ActivateTarget();
            if (_popupLifecycleCoroutine != null) StopCoroutine(_popupLifecycleCoroutine);
            _popupLifecycleCoroutine = StartCoroutine(PopupSequence());
        }

        private IEnumerator PopupSequence()
        {
            _isPoppedUp = true;

            // Animate up
            yield return AnimateRotation(_upRotation);

            // Wait for configured difficulty duration
            float timer = 0f;
            while (timer < _activeDuration && !_isHit)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Animate down
            yield return AnimateRotation(_downRotation);

            _isPoppedUp = false;
            DeactivateTarget();
        }

        private IEnumerator AnimateRotation(Quaternion targetRot)
        {
            while (Quaternion.Angle(pivotTransform.localRotation, targetRot) > 0.5f)
            {
                pivotTransform.localRotation = Quaternion.Slerp(pivotTransform.localRotation, targetRot, Time.deltaTime * animationSpeed);
                yield return null;
            }
            pivotTransform.localRotation = targetRot;
        }

        public override void OnHit(HitInfo hitInfo)
        {
            if (_isHit || !_isActive) return;

            base.OnHit(hitInfo);

            // Immediately animate down upon hit
            if (_popupLifecycleCoroutine != null) StopCoroutine(_popupLifecycleCoroutine);
            _popupLifecycleCoroutine = StartCoroutine(HitAndRetract());
        }

        private IEnumerator HitAndRetract()
        {
            yield return AnimateRotation(_downRotation);
            _isPoppedUp = false;
            DeactivateTarget();
        }
    }
}
