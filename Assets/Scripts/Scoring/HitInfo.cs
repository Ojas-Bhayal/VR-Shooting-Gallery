using UnityEngine;

namespace VRShootingGallery.Scoring
{
    /// <summary>
    /// Encapsulates all data relating to a shot collision.
    /// Passed to targets and scoring systems to decouple hit logic.
    /// </summary>
    public struct HitInfo
    {
        public HitType HitType { get; }
        public int BasePoints { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public GameObject TargetObject { get; }

        public HitInfo(HitType hitType, Vector3 point, Vector3 normal, GameObject targetObject, int customPoints = 0)
        {
            HitType = hitType;
            BasePoints = customPoints > 0 ? customPoints : (int)hitType;
            Point = point;
            Normal = normal;
            TargetObject = targetObject;
        }
    }
}
