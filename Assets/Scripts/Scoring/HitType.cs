namespace VRShootingGallery.Scoring
{
    /// <summary>
    /// Type of hit scored on a target, with corresponding default base points.
    /// As defined in GAME_DESIGN.md:
    /// Normal: +10, Center: +25, Bullseye: +50, Miss: 0
    /// </summary>
    public enum HitType
    {
        Miss = 0,
        Normal = 10,
        Center = 25,
        Bullseye = 50
    }
}
