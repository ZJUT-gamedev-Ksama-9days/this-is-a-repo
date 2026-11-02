using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    /// <summary>
    /// Opts a Rigidbody2D into local gravity, without changing global gravity or its authored gravityScale.
    /// Collision detection, contacts, friction and integration remain Unity Physics2D's responsibility.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GravityBody2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private Vector2 previousAcceleration;

        public GravityRegion2D CurrentRegion { get; private set; }
        public Vector2 CurrentAcceleration { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            CurrentAcceleration = Physics2D.gravity * body.gravityScale;
            previousAcceleration = CurrentAcceleration;
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
            {
                CurrentRegion = null;
                CurrentAcceleration = Vector2.zero;
                return;
            }

            GravityRegion2D region = GravityRegion2D.FindAtPoint(
                body.worldCenterOfMass, gameObject.scene.GetPhysicsScene2D());
            Vector2 nativeAcceleration = Physics2D.gravity * body.gravityScale;
            CurrentAcceleration = region != null
                ? region.Acceleration * body.gravityScale
                : nativeAcceleration;

            bool gravityChanged = region != CurrentRegion
                || (CurrentAcceleration - previousAcceleration).sqrMagnitude > 0.000001f;
            CurrentRegion = region;
            previousAcceleration = CurrentAcceleration;

            // A frame moved over a resting ball must wake it. An unchanged frame should allow sleep.
            if (gravityChanged)
            {
                body.WakeUp();
            }

            if (region == null || body.IsSleeping())
            {
                return;
            }

            // Unity already applies native gravity. Add only the difference to avoid double gravity.
            // F = m * a keeps acceleration independent of mass. Never rewrite velocity on transitions.
            Vector2 correction = CurrentAcceleration - nativeAcceleration;
            if (correction.sqrMagnitude > 0.000001f)
            {
                body.AddForce(correction * body.mass, ForceMode2D.Force);
            }
        }

        private void OnDisable()
        {
            CurrentRegion = null;
        }
    }
}
