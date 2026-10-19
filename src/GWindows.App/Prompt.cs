using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace GWindows;

static class Prompt
{
    /// <summary>Small modal text input in the fence look (dark translucent surface, rounded field, fade/slide-in); null if cancelled.</summary>
    public static string? Ask(string title, string initial = "")
    {
        var box = new TextBox { Text = initial, MinWidth = 360, Style = (Style)Application.Current.FindResource("FenceTextBox"), FontSize = 14, Margin = new(0, 8, 0, 6) };
        var label = new TextBlock { Text = title, Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 255, 255, 255)), FontSize = 12, FontFamily = new("Segoe UI Variable") };
        var hint = new TextBlock { Text = L.T("Enter confirmar   ·   Esc cancelar"), Foreground = new SolidColorBrush(Color.FromArgb(0x66, 255, 255, 255)), FontSize = 10.5, FontFamily = new("Segoe UI Variable") };
        var slide = new TranslateTransform(0, -10);
        var surface = new Border
        {
            CornerRadius = new(14), Padding = new(18, 14, 18, 12), Margin = new(18), Opacity = 0, RenderTransform = slide, BorderThickness = new(1),
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x18, 0x1A, 0x20)), BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)),
            Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 3, Opacity = 0.5, Color = Colors.Black },
            Child = new StackPanel { Children = { label, box, hint } },
        };
        var w = new Window
        {
            Content = surface, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false,
        };
        string? result = null;
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { result = box.Text; w.Close(); }
            else if (e.Key == Key.Escape) w.Close();
        };
        w.Loaded += (_, _) =>
        {
            box.Focus(); box.SelectAll();
            surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
        w.ShowDialog();
        return result;
    }
}