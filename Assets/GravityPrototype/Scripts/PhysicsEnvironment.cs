namespace GravityPrototype
{
    /// <summary>Chosen world setting: Earth-like air with 70 percent of Earth's gravity.</summary>
    public static class PhysicsEnvironment
    {
        public const float EarthGravity = 9.81f;
        public const float Gravity = EarthGravity * 0.7f;
        public const float AirDensity = 1.225f;
        public const float SphereDragCoefficient = 0.47f;
    }
}
