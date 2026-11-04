using System;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// A pushable pad that launches a contacting player along a configurable world-space direction.
    /// Keep its Rigidbody2D dynamic so the player can shove the pad into position with real contacts;
    /// use <see cref="BouncePadMouseDrag2D"/> when the pad also has to be placed with the mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GravityBouncePad2D : MonoBehaviour
    {
        [SerializeField, Tooltip("世界空间弹射方向；会被归一化。")]
        private Vector2 launchDirection = Vector2.up;
        [SerializeField, Min(0f), Tooltip("弹射速度，米/秒。")]
        private float launchSpeed = 12.5f;
        [SerializeField, Range(0f, 1f), Tooltip("弹射时保留的水平速度比例，用来改变运动轨迹。")]
        private float horizontalInherit = 1f;
        [SerializeField, Min(0f), Tooltip("同一角色两次弹射的最短间隔，秒。")]
        private float bounceCooldown = 0.15f;

        private Rigidbody2D body;
        private Vector2 homePosition;
        private float homeRotation;
        private float nextBounceTime;

        public event Action<GravityBouncePad2D, PlayerController2D> Bounced;

        public Rigidbody2D Body => body;
        public Vector2 LaunchVelocity =>
            (launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : Vector2.up) * launchSpeed;
        public bool CanBePushedByPlayer => body != null && body.bodyType == RigidbodyType2D.Dynamic;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            homePosition = body.position;
            homeRotation = body.rotation;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController2D player = collision.collider.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                TryBounce(player);
            }
        }

        /// <summary>
        /// Launches the player if the pad is off cooldown. Public so scripted levels, tests and
        /// non-collision triggers can reuse the same launch math.
        /// </summary>
        public bool TryBounce(PlayerController2D player)
        {
            if (player == null || Time.time < nextBounceTime)
            {
                return false;
            }

            Rigidbody2D targetBody = player.GetComponent<Rigidbody2D>();
            if (targetBody == null || !targetBody.simulated)
            {
                return false;
            }

            Vector2 direction = launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : Vector2.up;
            Vector2 inherited = Vector2.right * (targetBody.linearVelocity.x * horizontalInherit);
            player.ApplyLaunch(inherited + direction * launchSpeed);
            nextBounceTime = Time.time + bounceCooldown;
            Bounced?.Invoke(this, player);
            return true;
        }

        /// <summary>Restores the authored position and velocity, used by in-place level resets.</summary>
        public void ReturnToHome()
        {
            if (body == null)
            {
                return;
            }

            body.position = homePosition;
            body.rotation = homeRotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            // Clear interpolation history so the pad is visibly back home on the next frame.
            body.transform.SetPositionAndRotation(homePosition, Quaternion.Euler(0f, 0f, homeRotation));
            body.WakeUp();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 1f, 0.65f, 0.9f);
            Gizmos.DrawRay(transform.position, (Vector3)LaunchVelocity);
        }
    }
}
