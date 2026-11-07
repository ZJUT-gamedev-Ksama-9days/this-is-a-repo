using UnityEngine;

namespace GravityPrototype
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PixelBallView : MonoBehaviour
    {
        [SerializeField] private Sprite balloon;
        [SerializeField] private Sprite wood;
        [SerializeField] private Sprite iron;
        [SerializeField] private SpriteRenderer artwork;
        private BallMaterial2D material;
        private void LateUpdate()
        {
            if (material == null) material = GetComponent<BallMaterial2D>();
            if (artwork == null || material == null || material.Profile == null) return;
            switch (material.Profile.Kind)
            {
                case BallMaterialKind.Balloon: artwork.sprite = balloon; break;
                case BallMaterialKind.Iron: artwork.sprite = iron; break;
                default: artwork.sprite = wood; break;
            }
            if (artwork.sprite != null)
            {
                // Supplied 32px orb has a 22px opaque diameter; preserve the authored 0.84m ball size.
                float visibleFraction = artwork.sprite.name == "Orb" ? 22f / 32f : 1f;
                float diameter = artwork.sprite.bounds.size.x * visibleFraction;
                if (diameter > 0f) artwork.transform.localScale = Vector3.one / diameter;
            }
        }
    }
}
