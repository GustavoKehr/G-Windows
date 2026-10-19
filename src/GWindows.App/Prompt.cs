using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GWindows;

static class Prompt
{
    /// <summary>Tiny modal text input; null if cancelled.</summary>
    public static string? Ask(string title, string initial = "")
    {
        var box = new TextBox { Text = initial, Margin = new(12), MinWidth = 320 };
        var w = new Window { Title = title, Content = box, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, Topmost = true };
        string? result = null;
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { result = box.Text; w.Close(); }
            else if (e.Key == Key.Escape) w.Close();
        };
        w.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        w.ShowDialog();
        return result;
    }
}
