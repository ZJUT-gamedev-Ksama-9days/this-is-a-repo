using UnityEngine;

namespace GravityPrototype
{
    [RequireComponent(typeof(PlayerController2D)), DisallowMultipleComponent]
    public sealed class CharacterPixelAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private Sprite standing;
        [SerializeField] private Sprite floatFrame;
        [SerializeField] private Sprite walkA;
        [SerializeField] private Sprite walkB;
        [SerializeField] private Sprite jump;
        [SerializeField] private Sprite death;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private Sprite[] floatFrames;
        [SerializeField] private Sprite[] deathFrames;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.14f;
        [SerializeField, Min(0f)] private float floatBobPixels = 1.5f;
        [SerializeField, Min(0.01f)] private float floatBobSeconds = 0.9f;

        private PlayerController2D player;
        private PlayerHealth health;
        private Rigidbody2D body;
        private Vector3 artBasePosition;
        private float frameTime;
        public string CurrentState { get; private set; } = "Standing";

        private void Awake()
        {
            player = GetComponent<PlayerController2D>();
            health = GetComponent<PlayerHealth>();
            body = GetComponent<Rigidbody2D>();
            if (artwork == null)
                artwork = transform.Find("Visuals/Pixel Astronaut")?.GetComponent<SpriteRenderer>();
            if (artwork != null) artBasePosition = artwork.transform.localPosition;
        }

        private void LateUpdate()
        {
            if (player == null || artwork == null) return;
            var gravity = GetComponent<GravityBody2D>();
            Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
            bool floating = !player.HasGravity || (gravity != null && gravity.CurrentRegion != null
                && Vector2.Dot(velocity, player.UpAxis) <= 0.15f);
            Tick(health != null && health.IsDead, player.IsGrounded, floating,
                player.HorizontalInput, velocity, player.UpAxis, Time.deltaTime);
        }

        public void Tick(bool dead, bool grounded, bool floating, float input, Vector2 velocity, Vector2 up, float deltaTime)
        {
            if (artwork == null) return;
            string state = dead ? "Death" : grounded
                ? (Mathf.Abs(input) > 0.01f || Mathf.Abs(Vector2.Dot(velocity, new Vector2(up.y, -up.x))) > 0.15f ? "Walking" : "Standing")
                : floating ? "Floating" : "Jumping";
            if (state != CurrentState) { CurrentState = state; frameTime = 0f; }
            else frameTime += Mathf.Max(0f, deltaTime);
            Sprite[] frames = state == "Death" ? deathFrames : state == "Walking" ? walkFrames
                : state == "Floating" ? floatFrames : state == "Jumping" ? jumpFrames : idleFrames;
            Sprite fallback = state == "Death" ? death : state == "Walking" ? walkA
                : state == "Floating" ? floatFrame : state == "Jumping" ? jump : standing;
            int count = frames != null ? frames.Length : 0;
            int index = Mathf.FloorToInt(frameTime / Mathf.Max(0.01f, frameSeconds));
            if (count > 0) index = state == "Death" ? Mathf.Min(index, count - 1) : index % count;
            artwork.sprite = Frame(frames, fallback, index);
            float bob = state == "Floating" ? Mathf.Round(Mathf.Sin(frameTime * 2f * Mathf.PI / floatBobSeconds) * floatBobPixels) : 0f;
            artwork.transform.localPosition = artBasePosition + Vector3.up * (bob / PixelMetrics.PixelsPerUnit);
        }

        public void SetSprites(Sprite standingSprite, Sprite floatSprite, Sprite walkASprite,
            Sprite walkBSprite, Sprite jumpSprite, Sprite deathSprite)
        {
            standing=standingSprite;floatFrame=floatSprite;walkA=walkASprite;walkB=walkBSprite;jump=jumpSprite;death=deathSprite;
        }
        public void SetSequences(Sprite[] idle,Sprite[] walk,Sprite[] jumpSequence,Sprite[] floating,Sprite[] dying)
        {
            idleFrames=idle;walkFrames=walk;jumpFrames=jumpSequence;floatFrames=floating;deathFrames=dying;
        }
        private static Sprite Frame(Sprite[] frames,Sprite fallback,int index=0)
            => frames!=null&&frames.Length>0?frames[Mathf.Clamp(index,0,frames.Length-1)]:fallback;
    }
}
