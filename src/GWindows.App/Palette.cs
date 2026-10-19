using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace GWindows;

/// <summary>One palette row: a title, a dimmer subtitle (the path, or "Command"), an optional file whose icon is shown, and what to run.</summary>
sealed record Entry(string Title, string Sub, string? Path, Action Run);

/// <summary>Ctrl+Space: one box to open any item from the fences/desktop or run a command. Same look as the fences:
/// dark translucent surface, 14 px corners, hairline border, fade + 12 px slide on open (base token, 220 ms).</summary>
static class Palette
{
    static Window? _w;
    static readonly Dictionary<string, ImageSource?> Icons = new(StringComparer.OrdinalIgnoreCase);
    const int MaxRows = 8;

    public static Entry Item(string path) =>
        new(Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n ? n : path, path, path,
            () => { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception) { } });

    static ImageSource? IconOf(string path) => Icons.TryGetValue(path, out var i) ? i : Icons[path] = FenceWindow.IconOf(path);

    static readonly SolidColorBrush Dim = new(Color.FromArgb(0x99, 255, 255, 255));

    static UIElement Row(Entry e)
    {
        var grid = new Grid { ColumnDefinitions = { new() { Width = new GridLength(34) }, new() } };
        UIElement icon = e.Path is not null && IconOf(e.Path) is { } src
            ? new System.Windows.Controls.Image { Source = src, Width = 26, Height = 26, HorizontalAlignment = HorizontalAlignment.Left }
            : new Border   // commands get a small rounded glyph tile instead of a file icon
            {
                Width = 26, Height = 26, CornerRadius = new(7), HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0x7A, 0xA7, 0xFF)),
                Child = new TextBlock { Text = "›", Foreground = Brushes.White, FontSize = 17, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, -2, 0, 0) },
            };
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = e.Title, Foreground = Brushes.White, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = e.Sub, Foreground = Dim, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 1, 0, 0) },
            },
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(icon); grid.Children.Add(text);
        return grid;
    }

    public static void Show(IEnumerable<Entry> entries)
    {
        if (_w != null) { _w.Close(); return; }
        var all = entries.ToList();

        var box = new TextBox
        {
            FontSize = 17, FontFamily = new("Segoe UI Variable"), Foreground = Brushes.White, CaretBrush = Brushes.White, Background = Brushes.Transparent,
            BorderThickness = new(0), VerticalContentAlignment = VerticalAlignment.Center, Padding = new(0, 4, 0, 4),
            SelectionBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x7A, 0xA7, 0xFF)),
        };
        var hint = new TextBlock { Text = L.T("Buscar itens e comandos..."), Foreground = new SolidColorBrush(Color.FromArgb(0x66, 255, 255, 255)), FontSize = 17, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        var glass = new TextBlock { Text = "", FontFamily = new("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 12, 0) };
        var searchRow = new Grid { Margin = new(16, 10, 16, 8), ColumnDefinitions = { new() { Width = GridLength.Auto }, new() } };
        Grid.SetColumn(box, 1); Grid.SetColumn(hint, 1);
        searchRow.Children.Add(glass); searchRow.Children.Add(box); searchRow.Children.Add(hint);

        var list = new ListBox { Style = (Style)Application.Current.FindResource("PaletteList"), MaxHeight = MaxRows * 48 + 4, Margin = new(8, 0, 8, 4) };
        var divider = new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x26, 255, 255, 255)), Margin = new(0, 0, 0, 6) };
        var footer = new TextBlock { Text = L.T("↑↓ navegar   ·   Enter abrir   ·   Esc fechar"), Foreground = new SolidColorBrush(Color.FromArgb(0x66, 255, 255, 255)), FontSize = 10.5, Margin = new(18, 4, 18, 10) };

        var slide = new TranslateTransform(0, -12);
        var surface = new Border
        {
            CornerRadius = new(14), Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x18, 0x1A, 0x20)), BorderThickness = new(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)), Margin = new(18), Opacity = 0, RenderTransform = slide,
            Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 3, Opacity = 0.5, Color = Colors.Black },
            Child = new StackPanel { Children = { searchRow, divider, list, footer } },
        };
        TextElement.SetFontFamily(surface, new FontFamily("Segoe UI Variable"));

        var w = _w = new Window
        {
            Content = surface, Width = 660, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual,
        };
        var wa = SystemParameters.WorkArea;
        w.Left = wa.Left + (wa.Width - w.Width) / 2; w.Top = wa.Top + wa.Height * 0.14;

        void Filter()
        {
            var q = box.Text.Trim();
            hint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            IEnumerable<Entry> res = q.Length == 0
                ? all
                : all.Select(e => (e, s: e.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ? 1 : e.Sub.Contains(q, StringComparison.OrdinalIgnoreCase) ? 2 : 9))
                      .Where(x => x.s < 9).OrderBy(x => x.s).ThenBy(x => x.e.Title, StringComparer.OrdinalIgnoreCase).Select(x => x.e);
            list.Items.Clear();
            foreach (var e in res.Take(60)) list.Items.Add(new ListBoxItem { Content = Row(e), Tag = e });   // icons are only fetched for what is built
            list.Visibility = divider.Visibility = list.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (list.Items.Count > 0) { list.SelectedIndex = 0; list.ScrollIntoView(list.SelectedItem); }
        }
        void Run() { if (list.SelectedItem is ListBoxItem { Tag: Entry e }) { w.Close(); e.Run(); } }
        void Move(int d) { if (list.Items.Count == 0) return; list.SelectedIndex = Math.Clamp(list.SelectedIndex + d, 0, list.Items.Count - 1); list.ScrollIntoView(list.SelectedItem); }

        box.TextChanged += (_, _) => Filter();
        box.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape: w.Close(); break;
                case Key.Enter: Run(); break;
                case Key.Down: Move(1); e.Handled = true; break;
                case Key.Up: Move(-1); e.Handled = true; break;
                case Key.PageDown: Move(MaxRows); e.Handled = true; break;
                case Key.PageUp: Move(-MaxRows); e.Handled = true; break;
            }
        };
        list.PreviewMouseLeftButtonUp += (_, e) =>   // one click runs, like everywhere else
        {
            if (ItemsControl.ContainerFromElement(list, (DependencyObject)e.OriginalSource) is ListBoxItem it) { list.SelectedItem = it; Run(); }
        };
        bool active = false;
        w.Activated += (_, _) => active = true;
        w.Deactivated += (_, _) => { if (active) w.Close(); };
        w.Closed += (_, _) => _w = null;
        w.Loaded += (_, _) =>
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        };
        Filter(); w.Show(); w.Activate(); box.Focus();
    }
}
