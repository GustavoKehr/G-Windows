using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using GWindows.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace GWindows;

/// <summary>On/off switch for the fences, hidden at the top-center of the desktop: the pointer reaching the top edge slides it in.
/// The window is a near-invisible (alpha 1) hit zone at the bottom of the z-order, so it only exists over the bare desktop.</summary>
sealed class TogglePill : Window
{
    const int W = 320, H = 52; // DIP
    readonly Animator _anim = new();
    readonly Spring _reveal, _knob;
    readonly TranslateTransform _slide = new(0, -44);
    readonly Border _pill = new() { CornerRadius = new(0, 0, 16, 16), Opacity = 0, Height = 36, Width = 130, VerticalAlignment = VerticalAlignment.Top };
    readonly Ellipse _dot = new() { Width = 14, Height = 14, Fill = Brushes.White };
    readonly Border _track = new() { Width = 34, Height = 18, CornerRadius = new(9) };
    readonly SolidColorBrush _trackBrush = new(Color.FromArgb(0x55, 255, 255, 255));
    readonly System.Windows.Threading.DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly Action _onToggle;
    bool _on;

    readonly System.Windows.Forms.Screen _screen;

    public TogglePill(System.Windows.Forms.Screen screen, bool on, Action onToggle)
    {
        _screen = screen; _on = on; _onToggle = onToggle;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        Width = W; Height = H;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // alpha 1: invisible but receives the mouse

        _reveal = _anim.Add(0, v => { _slide.Y = -44 + 44 * v; _pill.Opacity = Math.Clamp(v, 0, 1); }, Motion.SoftK, Motion.SoftC);
        _knob = _anim.Add(on ? 1 : 0, v =>
        {
            _dot.Margin = new(2 + 16 * v, 2, 0, 2);
            var a = (byte)(0x55 + 0xAA * Math.Clamp(v, 0, 1) * 0.6);
            _trackBrush.Color = Color.FromArgb(a, (byte)(255 - 120 * Math.Clamp(v, 0, 1)), 255, (byte)(255 - 120 * Math.Clamp(v, 0, 1)));
        }, Motion.SnapK, Motion.SnapC);

        _track.Background = _trackBrush; _dot.HorizontalAlignment = HorizontalAlignment.Left; _track.Child = _dot;
        _knob.Step(1); // place the knob for the initial state without animation
        var label = new TextBlock { Text = "G-Windows", Foreground = Brushes.White, FontFamily = new("Segoe UI Variable"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
        _pill.Child = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Children = { label, _track } };
        _pill.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x18, 0x18, 0x1C));
        _pill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)); _pill.BorderThickness = new(1, 0, 1, 1);
        _pill.RenderTransform = _slide; _pill.Cursor = Cursors.Hand;
        _pill.HorizontalAlignment = HorizontalAlignment.Center;
        Content = new Grid { Children = { _pill } };

        _hide.Tick += (_, _) => { _hide.Stop(); _anim.To(_reveal, 0); };
        MouseEnter += (_, _) => { _hide.Stop(); _anim.To(_reveal, 1); };
        MouseLeave += (_, _) => _hide.Start();
        _pill.MouseLeftButtonUp += (_, _) => _onToggle();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        Native.Attach(h);
        var s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var b = _screen.Bounds;
        var w = (int)(W * s);
        Native.Place(h, b.X + (b.Width - w) / 2, b.Y, w, (int)(H * s));
    }

    public void SetOn(bool on) { _on = on; _anim.To(_knob, on ? 1 : 0); }
}
