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
            Rect button = new Rect(16f, 16f, 180f, 38f);
            if (PixelUI.Button(button, showControlsMenu ? "关闭说明" : "操作说明"))
                showControlsMenu = !showControlsMenu;
            if (showControlsMenu)
                PixelUI.Controls(new Rect(16f, 64f, Mathf.Min(600f, Screen.width - 32f), 454f), false);
        }
    }
}
