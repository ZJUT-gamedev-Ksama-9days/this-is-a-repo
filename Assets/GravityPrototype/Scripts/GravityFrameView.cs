using UnityEngine;

namespace GravityPrototype
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GravityRegion2D))]
    public sealed class GravityFrameView : MonoBehaviour
    {
        [SerializeField] private LineRenderer outline;
        [SerializeField] private LineRenderer arrow;
        [SerializeField] private SpriteRenderer fill;

        private GravityRegion2D region;
        private float visibility = 1f;
        private bool cachedColors;
        private Color outlineColor;
        private Color arrowColor;
        private Color fillColor;

        public void SetVisibility(float value)
        {
            if (!cachedColors)
            {
                if (outline != null) outlineColor = outline.startColor;
                if (arrow != null) arrowColor = arrow.startColor;
                if (fill != null) fillColor = fill.color;
                cachedColors = true;
            }
            visibility = Mathf.Clamp01(value);
            if (outline != null)
            {
                Color c = outlineColor; c.a *= visibility;
                outline.startColor = outline.endColor = c;
                outline.enabled = visibility > 0f;
            }
            if (arrow != null)
            {
                Color c = arrowColor; c.a *= visibility;
                arrow.startColor = arrow.endColor = c;
            }
            if (fill != null)
            {
                Color c = fillColor; c.a *= visibility;
                fill.color = c;
                fill.enabled = visibility > 0f;
            }
        }

        private void LateUpdate()
        {
            if (region == null)
            {
                region = GetComponent<GravityRegion2D>();
            }

            Vector2 half = region.Size * 0.5f;
            if (outline != null)
            {
                outline.SetPosition(0, new Vector3(-half.x, -half.y, 0f));
                outline.SetPosition(1, new Vector3(-half.x, half.y, 0f));
                outline.SetPosition(2, new Vector3(half.x, half.y, 0f));
                outline.SetPosition(3, new Vector3(half.x, -half.y, 0f));
            }

            if (fill != null)
            {
                fill.transform.localScale = new Vector3(region.Size.x, region.Size.y, 1f);
            }

            if (arrow == null)
            {
                return;
            }

            Vector2 acceleration = region.Acceleration;
            arrow.enabled = visibility > 0f && acceleration.sqrMagnitude > 0f;
            Vector3 direction = transform.InverseTransformDirection(acceleration.normalized);
            Vector3 side = new Vector3(-direction.y, direction.x, 0f);
            float length = Mathf.Min(region.Size.x, region.Size.y) * 0.28f;
            Vector3 tail = -direction * length * 0.5f;
            Vector3 tip = direction * length * 0.5f;
            arrow.SetPosition(0, tail);
            arrow.SetPosition(1, tip);
            arrow.SetPosition(2, tip - direction * length * 0.35f + side * length * 0.25f);
            arrow.SetPosition(3, tip);
            arrow.SetPosition(4, tip - direction * length * 0.35f - side * length * 0.25f);
        }
    }
}
