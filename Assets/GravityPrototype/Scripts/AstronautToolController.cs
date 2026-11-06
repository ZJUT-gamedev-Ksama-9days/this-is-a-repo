using UnityEngine;

namespace GravityPrototype
{
    public enum AstronautTool { GravityDevice, MechanicalArm, EmptyHands }

    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController2D), typeof(PlayerHealth))]
    public sealed class AstronautToolController : MonoBehaviour
    {
        [SerializeField] private GravityFrameDrag2D gravityFrame;
        [SerializeField] private MechanicalArm2D mechanicalArm;
        [SerializeField] private GameObject gravityDeviceVisual;
        [SerializeField] private GameObject mechanicalArmVisual;
        [SerializeField, Min(0f)] private float fieldHoldSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float fieldDisappearSeconds = 3f;
        [SerializeField] private bool readKeyboardInput = true;

        private PlayerHealth health;
        private GravityRegion2D region;
        private GravityFrameView frameView;
        private float stowedAt = float.NegativeInfinity;
        private bool wasDead;
        public bool InputBlocked { get; set; }

        public AstronautTool Equipped { get; private set; } = AstronautTool.GravityDevice;
        public float FieldVisibility { get; private set; } = 1f;
        public bool IsOperational => isActiveAndEnabled && health != null && !health.IsDead;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            if (gravityFrame != null)
            {
                region = gravityFrame.GetComponent<GravityRegion2D>();
                frameView = gravityFrame.GetComponent<GravityFrameView>();
            }
        }

        private void Start() => ResetTools();

        private void Update()
        {
            bool dead = health != null && health.IsDead;
            if (dead && !wasDead)
            {
                if (Equipped == AstronautTool.GravityDevice) stowedAt = Time.time;
                if (gravityFrame != null) gravityFrame.SetCanDrag(false);
                if (mechanicalArm != null) mechanicalArm.Release();
            }
            wasDead = dead;
            if (readKeyboardInput && IsOperational && !InputBlocked)
            {
                if (Input.GetKeyDown(KeyCode.Q)) Cycle(-1);
                if (Input.GetKeyDown(KeyCode.E)) Cycle(1);
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Equip(AstronautTool.GravityDevice);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Equip(AstronautTool.MechanicalArm);
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Equip(AstronautTool.EmptyHands);
            }
            UpdateField();
            UpdateVisuals();
        }

        public void Cycle(int direction)
        {
            Equip((AstronautTool)(((int)Equipped + direction % 3 + 3) % 3));
        }

        public bool Equip(AstronautTool tool)
        {
            if (!IsOperational || InputBlocked || (int)tool < 0 || (int)tool > 2) return false;
            if (Equipped == tool) return true;
            if (Equipped == AstronautTool.GravityDevice) stowedAt = Time.time;
            if (mechanicalArm != null) mechanicalArm.Release();
            Equipped = tool;
            if (gravityFrame != null) gravityFrame.SetCanDrag(tool == AstronautTool.GravityDevice);
            UpdateField();
            UpdateVisuals();
            return true;
        }

        private void UpdateField()
        {
            if (region == null) return;
            if (IsOperational && Equipped == AstronautTool.GravityDevice)
            {
                FieldVisibility = 1f;
            }
            else
            {
                float elapsed = Time.time - stowedAt;
                FieldVisibility = 1f - Mathf.Clamp01((elapsed - fieldHoldSeconds)
                    / Mathf.Max(0.01f, fieldDisappearSeconds - fieldHoldSeconds));
            }
            region.SetInfluence(FieldVisibility);
            if (frameView != null) frameView.SetVisibility(FieldVisibility);
        }

        private void UpdateVisuals()
        {
            if (gravityDeviceVisual != null)
                gravityDeviceVisual.SetActive(IsOperational && Equipped == AstronautTool.GravityDevice);
            if (mechanicalArmVisual != null)
                mechanicalArmVisual.SetActive(IsOperational && Equipped == AstronautTool.MechanicalArm);
        }

        public void ResetTools()
        {
            Equipped = AstronautTool.GravityDevice;
            stowedAt = float.NegativeInfinity;
            wasDead = false;
            if (mechanicalArm != null) mechanicalArm.Release();
            if (gravityFrame != null) gravityFrame.SetCanDrag(true);
            UpdateField();
            UpdateVisuals();
        }

        private void OnDisable()
        {
            if (mechanicalArm != null) mechanicalArm.Release();
            if (gravityFrame != null) gravityFrame.SetCanDrag(false);
            if (region != null) region.SetInfluence(0f);
            if (frameView != null) frameView.SetVisibility(0f);
        }

        private void OnValidate()
        {
            fieldHoldSeconds = Mathf.Max(0f, fieldHoldSeconds);
            fieldDisappearSeconds = Mathf.Max(fieldHoldSeconds + 0.01f, fieldDisappearSeconds);
        }
    }
}
