using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GWindows;

/// <summary>Ctrl+Space: one box to open any item from the fences/desktop or run a command.</summary>
static class Palette
{
    static Window? _w;

    public static void Show(IEnumerable<(string label, Action run)> entries)
    {
        if (_w != null) { _w.Close(); return; }
        var all = entries.ToList();
        var box = new TextBox { FontSize = 18, Margin = new(8) };
        var list = new ListBox { MaxHeight = 360, Margin = new(8, 0, 8, 8), BorderThickness = new(0) };
        var panel = new StackPanel { Children = { box, list } };
        var w = _w = new Window
        {
            Content = panel, Width = 560, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Topmost = true, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        void Filter()
        {
            list.Items.Clear();
            foreach (var e in all.Where(e => e.label.Contains(box.Text, StringComparison.OrdinalIgnoreCase)).Take(12)) list.Items.Add(new ListBoxItem { Content = e.label, Tag = e.run });
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }
        void Run() { if (list.SelectedItem is ListBoxItem { Tag: Action a }) { w.Close(); a(); } }
        box.TextChanged += (_, _) => Filter();
        box.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape: w.Close(); break;
                case Key.Enter: Run(); break;
                case Key.Down: list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1); e.Handled = true; break;
                case Key.Up: list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1); e.Handled = true; break;
            }
        };
        list.MouseDoubleClick += (_, _) => Run();
        bool active = false;
        w.Activated += (_, _) => active = true;
        w.Deactivated += (_, _) => { if (active) w.Close(); };
        w.Closed += (_, _) => _w = null;
        Filter(); w.Show(); w.Activate(); box.Focus();
    }

    public static (string, Action) Item(string path) =>
        (Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n ? $"{n}   -{path}" : path,
         () => { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception) { } });
}

