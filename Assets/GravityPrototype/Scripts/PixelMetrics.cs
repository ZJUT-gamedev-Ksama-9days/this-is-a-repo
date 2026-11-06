using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Authored pixel dimensions at 32 pixels per metre; physics uses SI world units.</summary>
    public static class PixelMetrics
    {
        public const int PixelsPerUnit = 32;
        public static readonly Vector2 PlayerSpriteSize = new Vector2(1f, 1.5f);
        public static readonly Vector2 PlayerColliderSize = new Vector2(0.5f, 0.875f);
        // Foot-aligned artwork: the capsule stays centred on the existing physics origin.
        public static readonly Vector2 PlayerArtworkOffset = new Vector2(0f, 10f / PixelsPerUnit);
    }
}
