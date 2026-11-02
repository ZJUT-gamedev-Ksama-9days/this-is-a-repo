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
            arrow.enabled = acceleration.sqrMagnitude > 0f;
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
