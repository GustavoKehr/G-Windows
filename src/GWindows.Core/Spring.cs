namespace GWindows.Core;

/// <summary>Damped spring (mass 1), fixed-step semi-implicit Euler. Retargeting keeps velocity, so animations are interruptible.</summary>
public sealed class Spring(double value, double stiffness = 220, double damping = 22)
{
    const double Dt = 1.0 / 240;
    public double Value { get; private set; } = value;
    public double Velocity { get; private set; }
    public double Target { get; set; } = value;
    public bool Settled => Math.Abs(Value - Target) < 1e-3 && Math.Abs(Velocity) < 1e-3;

    public void Reset(double v) { Value = Target = v; Velocity = 0; }

    public void Step(double seconds)
    {
        for (var t = Math.Min(seconds, 0.1); t > 0; t -= Dt)
        {
            Velocity += (-stiffness * (Value - Target) - damping * Velocity) * Dt;
            Value += Velocity * Dt;
        }
        if (Settled) { Value = Target; Velocity = 0; }
    }
}
