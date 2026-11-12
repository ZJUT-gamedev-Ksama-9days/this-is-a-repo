using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityPrototype
{
    /// <summary>
    /// 电路门（设计文档 2.3.6.3）：相当于电线的连接开关，不做角色交互。
    /// 小球落入本门后，门附近的电路接通（贴图换成通电的蓝色版本），门扇对角色不再阻挡，
    /// 小球仍会停在门里。门没有开闭动画，打开状态只用亮度表示。
    /// 断电状态由 LevelController.ResetLevelInPlace 统一恢复。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CircuitGate2D : MonoBehaviour
    {
        [SerializeField, Tooltip("本门控制的电路贴图；留空则不切换贴图。")]
        private Tilemap circuit;
        [SerializeField, Tooltip("断电直路贴图。")]
        private TileBase emptyStraight;
        [SerializeField, Tooltip("通电直路贴图。")]
        private TileBase poweredStraight;
        [SerializeField, Tooltip("断电弯角贴图。")]
        private TileBase emptyCorner;
        [SerializeField, Tooltip("通电弯角贴图。")]
        private TileBase poweredCorner;
        [SerializeField, Tooltip("只接受特定小球时指定；留空表示任何小球落入都算。")]
        private Rigidbody2D ball;
        [SerializeField, Tooltip("门扇碰撞体；接通后对角色不再阻挡，小球仍会停在里面。留空时使用本对象的碰撞体。")]
        private Collider2D[] leaves;
        [SerializeField, Range(0f, 0.6f), Tooltip("小球底部进入门顶该距离内即视为落入，米。")]
        private float dropTolerance = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("接通后门扇的透明度，用来替代还没做的美术开闭帧。")]
        private float openAlpha = 0.35f;

        private Collider2D[] playerColliders;
        private SpriteRenderer[] visuals;
        private Color[] visualColors;

        public bool IsConnected { get; private set; }
        public Rigidbody2D Ball => ball;

        private void Awake()
        {
            if (leaves == null || leaves.Length == 0)
            {
                leaves = new[] { GetComponent<Collider2D>() };
            }

            if (ball != null && ball.GetComponent<PlayerController2D>() != null)
            {
                ball = null;
            }

            visuals = GetComponentsInChildren<SpriteRenderer>(true);
            visualColors = new Color[visuals.Length];
            for (int i = 0; i < visuals.Length; i++) visualColors[i] = visuals[i].color;

            Apply();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsConnected || !IsBall(collision.collider)) return;
            if (!IsDroppedIn(collision.collider)) return;
            Connect();
        }

        private bool IsBall(Collider2D collider)
        {
            Rigidbody2D body = collider.attachedRigidbody;
            if (body == null || body.GetComponent<PlayerController2D>() != null) return false;
            return ball != null ? body == ball : body.GetComponent<BallMaterial2D>() != null;
        }

        /// <summary>只认从门口上方落到门里的球，擦到门侧不算落入。</summary>
        private bool IsDroppedIn(Collider2D ballCollider)
        {
            float top = float.NegativeInfinity;
            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] != null) top = Mathf.Max(top, leaves[i].bounds.max.y);
            }

            if (float.IsNegativeInfinity(top)) top = transform.position.y;
            return ballCollider.bounds.min.y >= top - dropTolerance;
        }

        /// <summary>接通本门附近的电路。公开给脚本关卡和测试调用。</summary>
        public void Connect()
        {
            if (IsConnected) return;
            IsConnected = true;
            Apply();
        }

        /// <summary>回到断电状态，供关卡原地重置使用。</summary>
        public void Disconnect()
        {
            if (!IsConnected) return;
            IsConnected = false;
            Apply();
        }

        private void Apply()
        {
            SwapTiles(IsConnected);
            SetPlayerPassThrough(IsConnected);
            SetLeafAlpha();
        }

        private void SetLeafAlpha()
        {
            if (visuals == null || visualColors == null) return;
            for (int i = 0; i < visuals.Length && i < visualColors.Length; i++)
            {
                if (visuals[i] == null) continue;
                Color color = visualColors[i];
                color.a = (IsConnected ? openAlpha : 1f) * visualColors[i].a;
                visuals[i].color = color;
            }
        }

        private void SwapTiles(bool powered)
        {
            if (circuit == null) return;
            if (powered)
            {
                if (emptyStraight != null && poweredStraight != null) circuit.SwapTile(emptyStraight, poweredStraight);
                if (emptyCorner != null && poweredCorner != null) circuit.SwapTile(emptyCorner, poweredCorner);
            }
            else
            {
                if (emptyStraight != null && poweredStraight != null) circuit.SwapTile(poweredStraight, emptyStraight);
                if (emptyCorner != null && poweredCorner != null) circuit.SwapTile(poweredCorner, emptyCorner);
            }
        }

        /// <summary>接通后只放行角色；小球还要留在门里充当开关。</summary>
        private void SetPlayerPassThrough(bool passThrough)
        {
            if (playerColliders == null)
            {
                var player = FindFirstObjectByType<PlayerController2D>();
                playerColliders = player != null && player.gameObject.scene == gameObject.scene
                    ? player.GetComponentsInChildren<Collider2D>(true)
                    : new Collider2D[0];
            }

            for (int i = 0; i < leaves.Length; i++)
            {
                Collider2D leaf = leaves[i];
                if (leaf == null) continue;
                for (int j = 0; j < playerColliders.Length; j++)
                {
                    if (playerColliders[j] != null) Physics2D.IgnoreCollision(playerColliders[j], leaf, passThrough);
                }
            }
        }

        private void OnDestroy()
        {
            if (playerColliders == null) return;
            SetPlayerPassThrough(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsConnected ? new Color(0.35f, 0.75f, 1f, 0.9f) : new Color(0.55f, 0.55f, 0.6f, 0.9f);
            Collider2D gate = GetComponent<Collider2D>();
            if (gate != null) Gizmos.DrawWireCube(gate.bounds.center, gate.bounds.size);
        }
    }
}
