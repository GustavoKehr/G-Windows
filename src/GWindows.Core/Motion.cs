namespace GWindows.Core;

/// <summary>Single source of motion values. Spring constants: stiffness, damping.</summary>
public static class Motion
{
    public const double SoftK = 220, SoftC = 22;   // spring-soft: lift, drag, drop
    public const double SnapK = 380, SnapC = 28;   // spring-snappy: grid snap, reorder, item hover
    public const double HoverScale = 1.08, PressScale = 0.96, RestOpacity = 0.7;
    public const int RollupDelayMs = 600;
}
