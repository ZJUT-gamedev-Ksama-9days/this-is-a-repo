using UnityEngine;

namespace GravityPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GravityRegion2D))]
    public sealed class GravityFrameDrag2D : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField, Tooltip("使用无旋转的正交相机，将整个方框保持在画面内。")]
        private bool constrainToCamera = true;

        private GravityRegion2D region;
        private Vector3 pointerOffset;
        private bool dragging;

        private void Awake()
        {
            region = GetComponent<GravityRegion2D>();
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (inputCamera == null || !TryGetWorldPoint(Input.mousePosition, out Vector3 pointer))
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && region.Contains(pointer))
            {
                dragging = true;
                pointerOffset = transform.position - pointer;
            }

            if (!Input.GetMouseButton(0))
            {
                dragging = false;
            }

            if (!dragging)
            {
                return;
            }

            Vector3 target = pointer + pointerOffset;
            target.z = transform.position.z;
            if (constrainToCamera && inputCamera.orthographic)
            {
                target = ClampToCamera(target);
            }

            // This is an influence area, not a physical wall, so moving its Transform is intentional.
            transform.position = target;
        }

        private bool TryGetWorldPoint(Vector3 screenPoint, out Vector3 point)
        {
            Ray ray = inputCamera.ScreenPointToRay(screenPoint);
            Plane plane = new Plane(Vector3.forward, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private Vector3 ClampToCamera(Vector3 target)
        {
            float depth = inputCamera.transform.InverseTransformPoint(transform.position).z;
            Vector3 minimum = inputCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 maximum = inputCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Vector2 half = region.WorldHalfExtents;
            target.x = maximum.x - minimum.x >= half.x * 2f
                ? Mathf.Clamp(target.x, minimum.x + half.x, maximum.x - half.x)
                : (minimum.x + maximum.x) * 0.5f;
            target.y = maximum.y - minimum.y >= half.y * 2f
                ? Mathf.Clamp(target.y, minimum.y + half.y, maximum.y - half.y)
                : (minimum.y + maximum.y) * 0.5f;
            return target;
        }

        public void CancelDrag()
        {
            dragging = false;
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelDrag();
            }
        }
    }
}
