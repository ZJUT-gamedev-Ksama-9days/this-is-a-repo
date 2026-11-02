using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Prototype-only reset and debug instructions, not a level/flow system.</summary>
    public sealed class PhysicsSandboxController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D ball;
        [SerializeField] private GravityFrameDrag2D frame;
        [SerializeField] private PlayerController2D player;

        private Vector2 ballStartPosition;
        private float ballStartRotation;
        private Vector3 frameStartPosition;
        private GravityBody2D gravityBody;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;

        private void Awake()
        {
            if (ball != null)
            {
                ballStartPosition = ball.position;
                ballStartRotation = ball.rotation;
                gravityBody = ball.GetComponent<GravityBody2D>();
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
            }

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

            if (frame != null)
            {
                frame.CancelDrag();
                frame.transform.position = frameStartPosition;
            }
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = Color.white;
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                textStyle.normal.textColor = new Color(0.65f, 0.88f, 0.95f);
            }

            GUI.Box(new Rect(16f, 16f, Mathf.Min(750f, Screen.width - 32f), 150f), GUIContent.none);
            GUI.Label(new Rect(32f, 26f, 700f, 32f), "LOCAL GRAVITY / PHYSICS SANDBOX", titleStyle);
            GUI.Label(new Rect(32f, 64f, 700f, 25f), "A / D or arrows: move     Space / W / Up: jump", textStyle);
            GUI.Label(new Rect(32f, 94f, 700f, 25f), "LMB: drag frame     R: reset all     Walk into the ball to push", textStyle);
            string state = gravityBody != null && gravityBody.CurrentRegion != null
                ? "Ball: LOCAL gravity (arrow) / velocity is preserved"
                : "Ball: WORLD gravity (down) / move frame over the ball";
            GUI.Label(new Rect(32f, 124f, 700f, 25f), state, textStyle);
        }
    }
}
