using System.Collections;
using UnityEngine;
using VRShootingGallery.Scoring;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Static target that remains stationary and resets/respawns after a short delay upon being hit.
    /// Fully satisfies FR-05 (StaticTarget).
    /// </summary>
    public class StaticTarget : TargetBase
    {
        [Header("Static Target Settings")]
        [Tooltip("Delay in seconds before the target resets after being hit.")]
        [SerializeField] private float respawnDelay = 2.0f;

        [Tooltip("Optional animator or pivot for knockdown / reset animation.")]
        [SerializeField] private Transform targetModel;

        private Coroutine _respawnCoroutine;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;

        protected override void Awake()
        {
            base.Awake();
            if (targetModel != null)
            {
                _initialLocalPosition = targetModel.localPosition;
                _initialLocalRotation = targetModel.localRotation;
            }
        }

        public override void OnHit(HitInfo hitInfo)
        {
            if (_isHit || !_isActive) return;

            base.OnHit(hitInfo);

            if (_respawnCoroutine != null) StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = StartCoroutine(KnockdownAndRespawn());
        }

        private IEnumerator KnockdownAndRespawn()
        {
            // Knock target down (tilt backwards 90 degrees if model transform is provided)
            if (targetModel != null)
            {
                targetModel.localRotation = _initialLocalRotation * Quaternion.Euler(90f, 0f, 0f);
            }

            yield return new WaitForSeconds(respawnDelay);

            // Reset back up
            ResetTarget();
        }

        public override void ResetTarget()
        {
            base.ResetTarget();

            if (targetModel != null)
            {
                targetModel.localPosition = _initialLocalPosition;
                targetModel.localRotation = _initialLocalRotation;
            }
        }
    }
}
