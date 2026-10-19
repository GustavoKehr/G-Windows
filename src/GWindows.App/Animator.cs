using System.Windows.Media;
using GWindows.Core;

namespace GWindows;

/// <summary>Drives springs from the render loop; subscribes only while something is moving (no frames at rest).</summary>
sealed class Animator
{
    readonly List<(Spring s, Action<double> apply)> _items = [];
    long _last;
    bool _on;

    public Spring Add(double start, Action<double> apply, double k = Motion.SoftK, double c = Motion.SoftC)
    {
        var s = new Spring(start, k, c);
        _items.Add((s, apply));
        return s;
    }

    public void To(Spring s, double target)
    {
        s.Target = target;
        if (_on) return;
        _on = true; _last = Environment.TickCount64;
        CompositionTarget.Rendering += Tick;
    }

    void Tick(object? _, EventArgs __)
    {
        var now = Environment.TickCount64;
        var dt = (now - _last) / 1000.0; _last = now;
        var busy = false;
        foreach (var (s, apply) in _items)
        {
            if (s.Settled) continue;
            s.Step(dt); apply(s.Value); busy |= !s.Settled;
        }
        if (busy) return;
        CompositionTarget.Rendering -= Tick; _on = false;
    }
}

