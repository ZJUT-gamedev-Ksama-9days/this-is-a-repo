using UnityEngine;

namespace GravityPrototype
{
    public enum BallMaterialKind { Balloon, Wood, Iron }

    [CreateAssetMenu(menuName = "Gravity Prototype/Ball Physics Profile", fileName = "BallPhysicsProfile")]
    public sealed class BallPhysicsProfile : ScriptableObject
    {
        [SerializeField] private BallMaterialKind kind = BallMaterialKind.Wood;
        [SerializeField, Min(0.01f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float gravityScale = 1f;
        [SerializeField, Min(0f)] private float linearDamping;
        [SerializeField, Min(0f)] private float angularDamping = 0.05f;
        [SerializeField] private PhysicsMaterial2D contactMaterial;
        [SerializeField] private Color color = new Color(1f, 0.72f, 0.25f);
        [SerializeField, HideInInspector] private float impactDamageMultiplier = 1f; // Legacy prototype data; impulse damage ignores it.

        [Header("SI sphere model: 1 Unity unit = 1 metre")]
        [SerializeField] private bool useReferencePhysics;
        [SerializeField, Min(0.01f)] private float diameterMeters = 0.84f;
        [SerializeField, Min(0.001f), Tooltip("木/铁球壳的材料密度；气球内部气体密度，kg/m³。")]
        private float densityKgPerCubicMeter = 600f;
        [SerializeField, Min(0f), Tooltip("气球外皮和打结的总质量，kg；不含内部空气。")]
        private float envelopeMassKg = 0.015f;
        [SerializeField, Min(0f)] private float airDensityKgPerCubicMeter = 1.225f;
        [SerializeField, Min(0f), Tooltip("常数球体阻力系数近似，不适用于全部流速。")]
        private float dragCoefficient = 0.47f;
        [SerializeField, Min(0f), Tooltip("空心球壳厚度，米；木壳参考 0.002，铁壳参考 0.0003。")]
        private float shellThicknessMeters = 0.002f;
        [SerializeField, Min(0f), Tooltip("木/铁空心球内部封闭空气的密度，kg/m³。")]
        private float internalGasDensityKgPerCubicMeter = 1.225f;

        public BallMaterialKind Kind => kind;
        public bool UseReferencePhysics => useReferencePhysics;
        public float DiameterMeters => diameterMeters;
        public float RadiusMeters => diameterMeters * 0.5f;
        public float Volume => 4f / 3f * Mathf.PI * RadiusMeters * RadiusMeters * RadiusMeters;
        public float FrontalArea => Mathf.PI * RadiusMeters * RadiusMeters;
        public float Density => densityKgPerCubicMeter;
        public float AirDensity => airDensityKgPerCubicMeter;
        public float DragCoefficient => dragCoefficient;
        public float ShellThicknessMeters => shellThicknessMeters;
        public float InnerRadius => Mathf.Max(0f, RadiusMeters - shellThicknessMeters);
        public float InnerVolume => 4f / 3f * Mathf.PI * InnerRadius * InnerRadius * InnerRadius;
        public float ShellVolume => 4f / 3f * Mathf.PI * (RadiusMeters - InnerRadius)
            * (RadiusMeters * RadiusMeters + RadiusMeters * InnerRadius + InnerRadius * InnerRadius);
        public float ShellMass => kind == BallMaterialKind.Balloon ? envelopeMassKg : densityKgPerCubicMeter * ShellVolume;
        public float GasMass => kind == BallMaterialKind.Balloon ? densityKgPerCubicMeter * Volume
            : internalGasDensityKgPerCubicMeter * InnerVolume;
        public float Mass => useReferencePhysics ? Mathf.Max(0.0001f, ShellMass + GasMass) : mass;
        public float GravityScale => useReferencePhysics ? 1f : gravityScale;
        public float LinearDamping => useReferencePhysics ? 0f : linearDamping;
        public float AngularDamping => useReferencePhysics ? 0f : angularDamping;
        public float MomentOfInertia
        {
            get
            {
                float r = RadiusMeters;
                if (kind == BallMaterialKind.Balloon)
                    return (0.4f * GasMass + 2f / 3f * envelopeMassKg) * r * r;
                float i = InnerRadius;
                // Factored (R^5-r^5)/(R^3-r^3) avoids subtracting nearly equal powers for thin shells.
                float ratio = (r*r*r*r + r*r*r*i + r*r*i*i + r*i*i*i + i*i*i*i)
                    / Mathf.Max(r*r + r*i + i*i, 0.00000001f);
                return 0.4f * ShellMass * ratio + 0.4f * GasMass * i * i;
            }
        }

        public Vector2 CalculateAirForce(Vector2 velocity, Vector2 localGravity)
        {
            if (!useReferencePhysics) return Vector2.zero;
            Vector2 buoyancy = -localGravity * (airDensityKgPerCubicMeter * Volume);
            Vector2 drag = -0.5f * airDensityKgPerCubicMeter * dragCoefficient * FrontalArea * velocity.magnitude * velocity;
            return buoyancy + drag;
        }
        public PhysicsMaterial2D ContactMaterial => contactMaterial;
        public Color Color => color;
        public float ImpactDamageMultiplier => impactDamageMultiplier;

        private void OnValidate()
        {
            mass = Mathf.Max(0.01f, mass);
            gravityScale = Mathf.Max(0f, gravityScale);
            linearDamping = Mathf.Max(0f, linearDamping);
            angularDamping = Mathf.Max(0f, angularDamping);
            impactDamageMultiplier = Mathf.Max(0f, impactDamageMultiplier);
            diameterMeters = Mathf.Max(0.01f, diameterMeters);
            densityKgPerCubicMeter = Mathf.Max(0.001f, densityKgPerCubicMeter);
            envelopeMassKg = Mathf.Max(0f, envelopeMassKg);
            airDensityKgPerCubicMeter = Mathf.Max(0f, airDensityKgPerCubicMeter);
            dragCoefficient = Mathf.Max(0f, dragCoefficient);
            shellThicknessMeters = Mathf.Clamp(shellThicknessMeters, 0f, RadiusMeters);
            internalGasDensityKgPerCubicMeter = Mathf.Max(0f, internalGasDensityKgPerCubicMeter);
        }
    }
}
