using System.Collections;
using UnityEngine;
using VRShootingGallery.Difficulty;
using VRShootingGallery.Scoring;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Moving target that travels back and forth along a horizontal axis.
    /// Speed is dynamically tuned by DifficultyData.
    /// Satisfies FR-05 (MovingTarget).
    /// </summary>
    public class MovingTarget : TargetBase
    {
        [Header("Movement Path")]
        [Tooltip("Horizontal travel distance to the left and right of spawn point.")]
        [SerializeField] private float travelDistance = 3.0f;

        [Tooltip("Default base movement speed if not configured by DifficultyData.")]
        [SerializeField] private float defaultSpeed = 2.0f;

        [Tooltip("Optional knockdown/model transform for visual hit reaction.")]
        [SerializeField] private Transform targetModel;

        [Tooltip("Delay in seconds before target resets after hit.")]
        [SerializeField] private float respawnDelay = 2.5f;

        public float TravelDistance => travelDistance;
        public Vector3 StartPosition => _startPosition != Vector3.zero ? _startPosition : transform.position;

        private Vector3 _startPosition;
        private float _currentSpeed;
        private int _moveDirection = 1; // 1 = right, -1 = left
        private Coroutine _resetCoroutine;
        private Quaternion _initialModelRotation;

        protected override void Awake()
        {
            base.Awake();
            _startPosition = transform.position;
            _currentSpeed = defaultSpeed;

            if (targetModel != null)
            {
                _initialModelRotation = targetModel.localRotation;
            }
        }

        public override void ApplyDifficulty(DifficultyData difficulty)
        {
            base.ApplyDifficulty(difficulty);
            if (difficulty != null)
            {
                _currentSpeed = difficulty.TargetMoveSpeed;
            }
        }

        private void Update()
        {
            if (!_isActive || _isHit) return;

            MoveAlongTrack();
        }

        private void MoveAlongTrack()
        {
            // Move horizontally relative to right vector
            float step = _currentSpeed * Time.deltaTime * _moveDirection;
            transform.position += transform.right * step;

            // Check boundaries
            float currentOffset = Vector3.Dot(transform.position - _startPosition, transform.right);
            if (Mathf.Abs(currentOffset) >= travelDistance)
            {
                // Clamp and reverse
                transform.position = _startPosition + transform.right * (Mathf.Sign(currentOffset) * travelDistance);
                _moveDirection *= -1;
            }
        }

        public override void OnHit(HitInfo hitInfo)
        {
            if (_isHit || !_isActive) return;

            base.OnHit(hitInfo);

            if (_resetCoroutine != null) StopCoroutine(_resetCoroutine);
            _resetCoroutine = StartCoroutine(HitAndReset());
        }

        private IEnumerator HitAndReset()
        {
            if (targetModel != null)
            {
                targetModel.localRotation = _initialModelRotation * Quaternion.Euler(90f, 0f, 0f);
            }

            yield return new WaitForSeconds(respawnDelay);

            ResetTarget();
        }

        public override void ResetTarget()
        {
            base.ResetTarget();

            if (targetModel != null)
            {
                targetModel.localRotation = _initialModelRotation;
            }
        }
    }
}
