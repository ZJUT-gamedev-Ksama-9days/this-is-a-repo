using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// Optional mouse placement for a bounce pad. The left button stays reserved for gravity frames,
    /// so pads use the right button by default. While dragging, the body turns kinematic and follows
    /// the pointer; on release it becomes dynamic again so the player can still push it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GravityBouncePad2D), typeof(Rigidbody2D))]
    public sealed class BouncePadMouseDrag2D : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField, Tooltip("鼠标按键：0 左键，1 右键；默认右键，避免与重力方框拖动冲突。")]
        private int mouseButton = 1;
        [SerializeField, Tooltip("使用无旋转的正交相机，把弹窗保持在画面内。")]
        private bool constrainToCamera = true;

        private MechanicalArm2D arm;
        private Collider2D padCollider;
        private Rigidbody2D body;
        private Vector3 pointerOffset;
        private Vector3 target;
        private bool dragging;
        private RigidbodyType2D previousBodyType;

        private void Awake()
        {
            arm = FindFirstObjectByType<MechanicalArm2D>();
            padCollider = GetComponent<Collider2D>();
            body = GetComponent<Rigidbody2D>();
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) { EndDrag(); return; }
            if (arm != null && arm.CanUse)
            {
                EndDrag();
                return;
            }
            if (inputCamera == null || padCollider == null)
            {
                return;
            }

            if (dragging && !Input.GetMouseButton(mouseButton))
            {
                EndDrag();
            }

            if (!TryGetWorldPoint(Input.mousePosition, out Vector3 pointer))
            {
                return;
            }

            if (!dragging && Input.GetMouseButtonDown(mouseButton) && padCollider.OverlapPoint(pointer))
            {
                BeginDrag(pointer);
            }

            if (!dragging)
            {
                return;
            }

            target = pointer + pointerOffset;
            target.z = transform.position.z;
            if (constrainToCamera && inputCamera.orthographic)
            {
                target = ClampToCamera(target);
            }
        }

        private void FixedUpdate()
        {
            if (dragging && body != null)
            {
                body.MovePosition(target);
            }
        }

        private void BeginDrag(Vector3 pointer)
        {
            dragging = true;
            pointerOffset = transform.position - pointer;
            previousBodyType = body.bodyType;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        private void EndDrag()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            if (body != null)
            {
                body.bodyType = previousBodyType;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
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

        private Vector3 ClampToCamera(Vector3 desired)
        {
            float depth = inputCamera.transform.InverseTransformPoint(transform.position).z;
            Vector3 minimum = inputCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 maximum = inputCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Vector3 half = padCollider.bounds.extents;
            desired.x = maximum.x - minimum.x >= half.x * 2f
                ? Mathf.Clamp(desired.x, minimum.x + half.x, maximum.x - half.x)
                : (minimum.x + maximum.x) * 0.5f;
            desired.y = maximum.y - minimum.y >= half.y * 2f
                ? Mathf.Clamp(desired.y, minimum.y + half.y, maximum.y - half.y)
                : (minimum.y + maximum.y) * 0.5f;
            return desired;
        }

        public void CancelDrag()
        {
            EndDrag();
        }

        private void OnDisable()
        {
            EndDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                EndDrag();
            }
        }
    }
}
