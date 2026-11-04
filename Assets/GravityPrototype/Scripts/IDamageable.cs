namespace GravityPrototype
{
    /// <summary>Anything a hazard can drain over time; returns false when the hit was ignored.</summary>
    public interface IDamageable
    {
        bool IsDead { get; }

        bool TakeDamage(float amount);
    }
}
