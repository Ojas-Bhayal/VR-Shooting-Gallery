using UnityEngine;
using VRShootingGallery.Scoring;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Attach to individual colliders or sub-parts of a target (e.g. Bullseye, Center ring, Outer ring)
    /// to award specific hit points and hit types.
    /// </summary>
    public class TargetZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [SerializeField] private HitType hitType = HitType.Normal;
        [Tooltip("Optional custom points override. If 0, uses default points defined by HitType.")]
        [SerializeField] private int customPoints = 0;

        public HitType HitType => hitType;
        public int Points => customPoints > 0 ? customPoints : (int)hitType;

        /// <summary>
        /// Creates a HitInfo struct representing a hit on this specific zone.
        /// </summary>
        public HitInfo CreateHitInfo(Vector3 hitPoint, Vector3 hitNormal)
        {
            return new HitInfo(hitType, hitPoint, hitNormal, gameObject, Points);
        }
    }
}
