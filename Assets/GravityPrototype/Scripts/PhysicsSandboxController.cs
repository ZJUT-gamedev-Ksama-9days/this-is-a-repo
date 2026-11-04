using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Prototype-only reset and debug instructions, not a level/flow system.</summary>
    public sealed class PhysicsSandboxController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D ball;
        [SerializeField] private GravityFrameDrag2D frame;
        [SerializeField] private PlayerController2D player;
        [SerializeField, Tooltip("供编辑器/测试选择球种的配置列表；数字键用于装置切换。切换只复位小球，不给玩家回血。")]
        private BallPhysicsProfile[] ballProfiles;
        [SerializeField, HideInInspector] private bool readBallSelectionInput = false; // Legacy; numeric keys now select devices.
        private BallMaterial2D ballMaterial;

        public BallPhysicsProfile CurrentBallProfile => ballMaterial != null ? ballMaterial.Profile : null;

        private Vector2 ballStartPosition;
        private float ballStartRotation;
        private Vector3 frameStartPosition;
        private GravityBody2D gravityBody;
        private bool showControlsMenu;

        private void Awake()
        {
            if (ball != null)
            {
                ballStartPosition = ball.position;
                ballStartRotation = ball.rotation;
                gravityBody = ball.GetComponent<GravityBody2D>();
                ballMaterial = ball.GetComponent<BallMaterial2D>();
            }

            if (frame != null)
            {
                frameStartPosition = frame.transform.position;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetSimulation();
            }
        }

        public void ResetSimulation()
        {
            if (player != null)
            {
                player.ResetToSpawn();
                AstronautToolController tools = player.GetComponent<AstronautToolController>();
                if (tools != null) tools.ResetTools();
            }

            ResetBall();

            if (frame != null)
            {
                frame.CancelDrag();
                frame.transform.position = frameStartPosition;
            }
        }

        public bool SelectBallType(int index)
        {
            if (ballMaterial == null || ballProfiles == null || index < 0 || index >= ballProfiles.Length
                || ballProfiles[index] == null) return false;
            if (player != null)
            {
                MechanicalArm2D arm = player.GetComponent<MechanicalArm2D>();
                if (arm != null) arm.Release();
            }
            ballMaterial.ApplyProfile(ballProfiles[index]);
            ResetBall();
            return true;
        }

        private void ResetBall()
        {
            if (ball != null)
            {
                ball.position = ballStartPosition;
                ball.rotation = ballStartRotation;
                ball.linearVelocity = Vector2.zero;
                ball.angularVelocity = 0f;
                // Clear interpolation history too, so reset is visible immediately.
                ball.transform.SetPositionAndRotation(ballStartPosition, Quaternion.Euler(0f, 0f, ballStartRotation));
                ball.WakeUp();
            }
        }

        private void OnGUI()
        {
            const float buttonWidth = 140f;
            const float buttonHeight = 34f;
            Rect button = new Rect(16f, 16f, buttonWidth, buttonHeight);
            if (GUI.Button(button, showControlsMenu ? "关闭操作说明" : "操作说明"))
                showControlsMenu = !showControlsMenu;

            if (!showControlsMenu) return;
            float width = Mathf.Min(390f, Screen.width - 32f);
            float height = 292f;
            Rect panel = new Rect(16f, 58f, width, height);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));
            GUILayout.Label("操作说明", GUI.skin.label);
            GUILayout.Space(8f);
            GUILayout.Label("A / D 或方向键：移动");
            GUILayout.Label("空格 / W / 上方向键：跳跃");
            GUILayout.Label("Q / E：上一个 / 下一个装置");
            GUILayout.Label("1：重力装置    2：机械臂    3：空手");
            GUILayout.Space(5f);
            GUILayout.Label("重力装置：按住鼠标左键拖动重力框");
            GUILayout.Label("机械臂：左键按住抓取并拖动，松开释放");
            GUILayout.Label("机械臂：按住鼠标右键推动物体");
            GUILayout.Label("身体直接推挤物体：仅有极轻微效果");
            GUILayout.Label("机械臂抓取/拖动：目标须位于重力场内");
            GUILayout.Space(5f);
            GUILayout.Label("收起重力装置后保留 1.5 秒，3 秒后完全消散");
            GUILayout.Label("R：重置位置、生命并重新装备重力装置");
            GUILayout.EndArea();
        }
    }
}
