using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GWindows.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace GWindows;

/// <summary>One borderless window per fence. Built in code: a XAML file would only add indirection.</summary>
public sealed class FenceWindow : Window
{
    readonly FenceData _d;
    readonly Animator _anim = new();
    readonly Border _frame = new()
    {
        CornerRadius = new(12), BorderThickness = new(1), Opacity = Motion.RestOpacity,
        Background = new SolidColorBrush(Color.FromArgb(0x99, 0x18, 0x18, 0x1C)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
    };
    readonly Border _titleBar = new() { Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
    readonly TextBlock _title = new() { Foreground = Brushes.White, FontFamily = new("Segoe UI Variable"), FontSize = 13, Margin = new(12, 8, 12, 8) };
    readonly WrapPanel _panel = new() { Margin = new(4) };
    readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    System.Windows.Threading.DispatcherTimer? _clock;
    FileSystemWatcher? _portalWatch;
    readonly Spring _opacity;
    IntPtr _hwnd;
    bool _hover;
    static readonly (string, uint)[] Tints = [("Escuro", 0x9918181C), ("Azul", 0x99102040), ("Verde", 0x99103020), ("Roxo", 0x99281040), ("Vermelho", 0x99401418)];

    void ApplyTint()
    {
        var argb = Tints.FirstOrDefault(t => t.Item1 == _d.Tint, Tints[0]).Item2;
        _frame.Background = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }

    Spring? _fade;
    bool _wantShow = true;

    /// <summary>Spring fade in/out of the whole fence; hidden windows are removed from the screen after the fade.</summary>
    public void Fade(bool on)
    {
        _wantShow = on;
        _fade ??= _anim.Add(Opacity, v => Opacity = Math.Clamp(v, 0, 1));
        if (on) { Visibility = Visibility.Visible; Apply(); }
        _anim.To(_fade, on ? 1 : 0);
        if (!on) { var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) }; t.Tick += (_, _) => { t.Stop(); if (!_wantShow) Visibility = Visibility.Hidden; }; t.Start(); }
    }

    public void Hide_() { _wantShow = false; Opacity = 0; Visibility = Visibility.Hidden; _fade = _anim.Add(0, v => Opacity = Math.Clamp(v, 0, 1)); }

    int _px, _py;            // where the window is drawn right now (differs from _d.X/_d.Y while sliding to a slot)
    Spring? _sx, _sy;

    /// <summary>Snapped fences size their height to the content: 5 items per row (84 px each), 1 to 5 rows, scrolling beyond that.</summary>
    public void FitHeight()
    {
        if (_d.Free || _d.Kind.Length > 0) return;
        var n = ItemsOf().Count();
        _d.H = Math.Clamp((int)Math.Ceiling(n / 5.0), n == 0 && Manual ? 2 : 1, 5) * 84 + 58;
    }

    /// <summary>Rebuild menus and texts after a language switch.</summary>
    public void Relocalize() { ContextMenu = Menu(null); Rebuild(); }

    public void Reposition() { _px = _d.X; _py = _d.Y; Apply(); }

    /// <summary>Slide to a grid slot with the spring-snappy token (or jump when not animating).</summary>
    public void MoveTo(int x, int y, int w, bool animate)
    {
        (_d.X, _d.Y, _d.W) = (x, y, w);
        if (!animate || _hwnd == 0 || !IsVisible) { Reposition(); return; }
        _sx ??= _anim.Add(_px, v => { _px = (int)Math.Round(v); Apply(); }, Motion.SnapK, Motion.SnapC);
        _sy ??= _anim.Add(_py, v => { _py = (int)Math.Round(v); Apply(); }, Motion.SnapK, Motion.SnapC);
        _sx.Reset(_px); _sy.Reset(_py);
        _anim.To(_sx, x); _anim.To(_sy, y);
        Apply();
    }
    public FenceData Data => _d;
    public void Refresh() => Rebuild();
    readonly System.Windows.Threading.DispatcherTimer _rollTimer = new() { Interval = TimeSpan.FromMilliseconds(Motion.RollupDelayMs) };

    /// <summary>Rolled-up fences collapse to the title bar; hover expands, leaving collapses after 600 ms.</summary>
    void Apply()
    {
        var h = _d.Rolled && !_hover ? (int)(36 * VisualTreeHelper.GetDpi(this).DpiScaleY) : _d.H;
        Native.Place(_hwnd, _px, _py, _d.W, h);
    }

    public FenceWindow(FenceData d)
    {
        _d = d;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; Width = 10; Height = 10;
        _opacity = _anim.Add(Motion.RestOpacity, v => _frame.Opacity = Math.Clamp(v, 0, 1));
        _title.Text = d.Name;
        _titleBar.Child = _title;

        var grip = new System.Windows.Shapes.Rectangle
        {
            Width = 16, Height = 16, Fill = Brushes.Transparent, Cursor = Cursors.SizeNWSE,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
        };
        _scroll.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = Application.Current.FindResource("FenceScrollBar");
        var scroll = _scroll;
        var grid = new Grid { RowDefinitions = { new() { Height = GridLength.Auto }, new() } };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(_titleBar); grid.Children.Add(scroll);
        _frame.Child = grid;
        var root = new Grid(); root.Children.Add(_frame); root.Children.Add(grip);
        Content = root;
        AllowDrop = true;

        _rollTimer.Tick += (_, _) => { _rollTimer.Stop(); Apply(); };
        MouseEnter += (_, _) => { _rollTimer.Stop(); _hover = true; Apply(); _anim.To(_opacity, 1); };
        MouseLeave += (_, _) => { _hover = false; _rollTimer.Start(); _anim.To(_opacity, Motion.RestOpacity); };
        Drop += OnDrop;
        Drag(_titleBar, true, (dx, dy, x0, y0, w, h) => (x0 + dx, y0 + dy, w, h));
        Drag(grip, false, (dx, dy, x0, y0, w, h) => (x0, y0, _d.Free ? Math.Max(160, w + dx) : w, _d.Free ? Math.Max(120, h + dy) : h));   // snapped fences keep the grid size
        _titleBar.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Rename(); };
        MouseLeftButtonDown += (_, _) => Activate();   // fences never take focus on their own; clicking one lets Ctrl+V reach it
        PreviewKeyDown += (_, e) => { if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && !(Keyboard.FocusedElement is TextBox)) { PasteFromClipboard(); e.Handled = true; } };
        ContextMenu = Menu(null);
        ApplyTint();
        Rebuild();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _px = _d.X; _py = _d.Y;
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.Attach(_hwnd);
        Apply();
    }

    // Move/resize by manual capture: Window.Left/Top assume top-level coordinates, which a child window is not.
    void Drag(UIElement el, bool isMove, Func<int, int, int, int, int, int, (int x, int y, int w, int h)> calc)
    {
        Native.POINT start = default; (int x, int y, int w, int h) o = default;
        el.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount > 1) return;
            Native.GetCursorPos(out start); o = (_d.X, _d.Y, _d.W, _d.H); el.CaptureMouse();
        };
        el.MouseMove += (_, _) =>
        {
            if (!el.IsMouseCaptured) return;
            Native.GetCursorPos(out var p);
            (_d.X, _d.Y, _d.W, _d.H) = calc(p.X - start.X, p.Y - start.Y, o.x, o.y, o.w, o.h);
            _px = _d.X; _py = _d.Y;
            Apply();
        };
        el.MouseLeftButtonUp += (_, _) => { if (el.IsMouseCaptured) { el.ReleaseMouseCapture(); App.AfterDrag(this, isMove); } };
    }

    /// <summary>Inline title editor. It commits what was typed when focus is lost for any reason: clicking elsewhere in the fence,
    /// on the desktop or in another app (the window deactivates), Enter, or Esc (which restores the old name instead).</summary>
    void Rename()
    {
        var box = new TextBox { Text = _d.Name, Margin = new(8, 4, 8, 4), Style = (Style)Application.Current.FindResource("FenceTextBox") };
        var finished = false;
        void Done()
        {
            if (finished) return;
            finished = true;
            PreviewMouseDown -= OutsideClick; Deactivated -= OnDeactivated;
            _d.Name = box.Text; _title.Text = _d.Name; _titleBar.Child = _title; App.SaveSoon();
        }
        void OutsideClick(object s, MouseButtonEventArgs e) { if (!box.IsMouseOver) Done(); }   // a click anywhere else in this fence
        void OnDeactivated(object? s, EventArgs e) => Done();                                    // a click on the desktop or another window
        box.LostFocus += (_, _) => Done();
        box.KeyDown += (_, e) => { if (e.Key == Key.Escape) box.Text = _d.Name; if (e.Key is Key.Enter or Key.Escape) Done(); };
        PreviewMouseDown += OutsideClick; Deactivated += OnDeactivated;
        _titleBar.Child = box; Activate(); box.Focus(); box.SelectAll();
    }
    ContextMenu Menu(string? path)
    {
        var m = new ContextMenu();
        void Add(string h, Action a) { var i = new MenuItem { Header = h }; i.Click += (_, _) => a(); m.Items.Add(i); }
        if (path != null)
        {
            Add(L.T("Abrir"), () => Launch(path));
            Add(L.T("Mostrar no Explorer"), () => ShowInExplorer(path));
            Add(L.T("Propriedades"), () => Native.Properties(path));
            Add(L.T("Mais opções do Windows..."), () => { Native.GetCursorPos(out var p); ShellMenu.Show(_hwnd, path, p.X, p.Y, ""); });
            Add(L.T("Remover da cerca"), () =>
            {
                if (Manual) _d.Items.Remove(path); else _d.Excluded.Add(path);   // portal/smart content comes from outside: just hide it here
                Rebuild(); App.SaveSoon();
            });
        }
        else
        {
            Add(_d.Rolled ? L.T("Desenrolar") : L.T("Enrolar (recolher na barra de título)"), () => { _d.Rolled = !_d.Rolled; ContextMenu = Menu(null); Apply(); App.SaveSoon(); });
            Add(L.T("Ordenar por nome"), () => Sort(p => Path.GetFileName(p).ToLowerInvariant()));
            Add(L.T("Ordenar por tipo"), () => Sort(p => Path.GetExtension(p).ToLowerInvariant()));
            Add(L.T("Ordenar por data"), () => Sort(p => File.GetLastWriteTime(p)));
            Add(L.T("Ordenar por tamanho"), () => Sort(p => File.Exists(p) ? new FileInfo(p).Length : 0L));
            Add(L.T("Regra de captura automática (ex.: *.pdf; *.docx)..."), () =>
            {
                var r = Prompt.Ask(L.T("Regra desta cerca (vazio = sem regra)"), _d.Rule);
                if (r != null) { _d.Rule = r.Trim(); App.SaveSoon(); }
            });
            foreach (var (name, argb) in Tints) Add(L.T("Cor: ") + L.T(name), () => { _d.Tint = name; ApplyTint(); App.SaveSoon(); });
            if (Manual) Add(L.T("Colar caminho(s) da área de transferência (Ctrl+V)"), () => PasteFromClipboard());
            Add(L.T("Tipo: manual"), () => SetKind("", "", ""));
            Add(L.T("Tipo: portal de pasta..."), () => { if (Prompt.Ask(L.T("Caminho da pasta"), _d.Portal) is { Length: > 0 } p) SetKind(p, "", ""); });
            Add(L.T("Tipo: inteligente - recentes"), () => SetKind("", "recent", ""));
            Add(L.T("Tipo: inteligente - pastas mais acessadas"), () => SetKind("", "folders", ""));
            Add(L.T("Tipo: inteligente - arquivos grandes"), () => SetKind("", "large", ""));
            Add(L.T("Tipo: inteligente - não tocados há 90 dias"), () => SetKind("", "stale", ""));
            Add(L.T("Tipo: widget relógio"), () => SetKind("", "", "clock"));
            Add(L.T("Tipo: widget notas"), () => SetKind("", "", "notes"));
            Add(_d.Free ? L.T("Alinhar à grade") : L.T("Mobilidade livre (soltar fora da grade)"), () => { _d.Free = !_d.Free; if (!_d.Free) { _d.Col = -1; _d.Monitor = ""; } ContextMenu = Menu(null); App.Arrange(true); App.SaveSoon(); });
            Add(L.T("Nova cerca"), App.NewFence);
            Add(L.T("Excluir cerca (arquivos não são tocados)"), () => { App.Layout.Fences.Remove(_d); App.Wins.Remove(this); App.SaveSoon(); Close(); });
            Add(L.T("Sair"), () => Application.Current.Shutdown());
        }
        // Popups are owned by a bottom-of-z-order window, so lift them above every normal window.
        m.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(m) is not HwndSource s) return;
            Native.KeepTopmost(s);
            Native.PopupAtCursor(s.Handle, (int)(8 * VisualTreeHelper.GetDpi(m).DpiScaleX));
        };
        return m;
    }

    static void Raise(Visual v) { if (PresentationSource.FromVisual(v) is HwndSource s) Native.KeepTopmost(s); }

    /// <summary>One-shot sort; order then stays manual.</summary>
    void Sort(Func<string, IComparable> key) { _d.Items = _d.Items.OrderBy(key).ToList(); Rebuild(); App.SaveSoon(); }

    /// <summary>Adds the existing paths found in the clipboard text (one per line, quotes allowed) as references; returns how many were new.</summary>
    int PasteFromClipboard()
    {
        if (!Manual || !Clipboard.ContainsText()) return 0;
        var n = 0;
        foreach (var raw in Clipboard.GetText().Split(["\r\n", "\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = raw.Trim('"').TrimEnd('\\');
            if (p.Length == 2 && p[1] == ':') p += "\\";
            if ((Directory.Exists(p) || File.Exists(p)) && !_d.Items.Contains(p)) { _d.Items.Add(p); n++; }
        }
        if (n > 0) { Rebuild(); App.SaveSoon(); }
        return n;
    }

    void SetKind(string portal, string smart, string kind)
    {
        (_d.Portal, _d.Smart, _d.Kind) = (portal, smart, kind);
        // the title follows the type; "manual" keeps whatever name the fence has
        var title = smart switch { "recent" => L.T("Arquivos Recentes"), "large" => L.T("Maiores Arquivos"), "stale" => L.T("Não Tocados (90 dias)"), "folders" => L.T("Pastas Mais Acessadas"), _ => kind switch { "clock" => L.T("Relógio"), "notes" => L.T("Notas"), _ => portal.Length > 0 ? (Path.GetFileName(portal.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : portal) : null } };
        if (title != null) { _d.Name = title; _title.Text = title; }
        Rebuild(); App.SaveSoon();
    }

    /// <summary>Opens Explorer at the item's real location (the install folder or target of a shortcut), selecting the file when there is one.</summary>
    static void ShowInExplorer(string path)
    {
        var t = Locator.Resolve(path);
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        try
        {
            var psi = new ProcessStartInfo(explorer);
            if (t.Select is not null) psi.Arguments = $"/select,\"{t.Select.Replace("\"", "")}\"";   // one pre-quoted string: Explorer does not parse /select,"x" split into two arguments
            else if (t.Open is not null) psi.Arguments = t.Open.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? t.Open : $"\"{t.Open.Replace("\"", "")}\"";
            else return;
            Process.Start(psi);
        }
        catch (Exception) { }
    }

    static void Launch(string p) { try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); } catch (Exception) { } }

    void OnDrop(object s, DragEventArgs e)
    {
        if (!Manual || e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files) if (!_d.Items.Contains(f)) _d.Items.Add(f);
        // moved between fences: drop the reference from the source (the file itself never moves)
        if (e.Data.GetData("gwindows/src") is string src)
            foreach (var w in App.Wins.Where(w => w != this && w._d.Id == src)) { foreach (var f in files) w._d.Items.Remove(f); w.Rebuild(); }
        Rebuild(); App.SaveSoon();
    }

    bool Manual => _d.Portal.Length == 0 && _d.Smart.Length == 0 && _d.Kind.Length == 0;

    IEnumerable<string> ItemsOf()
    {
        if (_d.Portal.Length > 0) return Directory.Exists(_d.Portal) ? Directory.EnumerateFileSystemEntries(_d.Portal).Where(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) && !_d.Excluded.Contains(p)) : [];
        if (_d.Smart == "folders")
        {
            if (App.FoldersCache is null && !App.FoldersLoading)   // Recent shortcuts are slow to resolve: compute off the UI thread, then redraw
                { App.FoldersLoading = true; App.FrequentFoldersAsync().ContinueWith(t => { App.FoldersCache = t.Result; Dispatcher.InvokeAsync(Rebuild); }); }
            return (App.FoldersCache ?? []).Where(p => !_d.Excluded.Contains(p));
        }
        if (_d.Smart.Length > 0) return GWindows.Core.Smart.Resolve(_d.Smart, App.SmartSources(), DateTime.Now).Where(p => !_d.Excluded.Contains(p)).ToList();
        return _d.Items.Where(p => File.Exists(p) || Directory.Exists(p));
    }

    void Rebuild()
    {
        _clock?.Stop(); _clock = null; _portalWatch?.Dispose(); _portalWatch = null;
        _scroll.Content = _panel;
        _panel.Children.Clear();
        if (_d.Kind == "clock")
        {
            var tb = new TextBlock { Foreground = Brushes.White, FontSize = 36, FontFamily = new("Segoe UI Variable"), Margin = new(16), HorizontalAlignment = HorizontalAlignment.Center };
            void Tick() => tb.Text = DateTime.Now.ToString("HH:mm:ss\ndddd, d MMM", L.Culture);
            Tick(); _scroll.Content = tb;
            _clock = new() { Interval = TimeSpan.FromSeconds(1) }; _clock.Tick += (_, _) => { if (IsVisible) Tick(); }; _clock.Start();
        }
        else if (_d.Kind == "notes")
        {
            var box = new TextBox { Text = _d.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new(0), Margin = new(8), CaretBrush = Brushes.White };
            box.TextChanged += (_, _) => { _d.Notes = box.Text; App.SaveSoon(); };
            box.GotFocus += (_, _) => Activate();
            _scroll.Content = box;
        }
        else
        {
            foreach (var path in ItemsOf()) _panel.Children.Add(MakeItem(path));
            if (_panel.Children.Count == 0 && Manual)
                _panel.Children.Add(new TextBlock { Text = L.T("Arraste pastas ou arquivos para cá,\nou cole o caminho (Ctrl+V)."), Foreground = new SolidColorBrush(Color.FromArgb(0x99, 255, 255, 255)), FontSize = 12, Margin = new(8, 12, 8, 0), TextWrapping = TextWrapping.Wrap });
            if (_d.Portal.Length > 0 && Directory.Exists(_d.Portal))
            {
                _portalWatch = new(_d.Portal) { EnableRaisingEvents = true };
                void R(object? s, EventArgs e) => Dispatcher.InvokeAsync(Rebuild);
                _portalWatch.Created += R; _portalWatch.Deleted += R; _portalWatch.Renamed += R;
            }
        }
        if (_hwnd != 0) App.Arrange(true);   // item count changed: heights and stacking follow
    }

    UIElement MakeItem(string path)
    {
        var scale = new ScaleTransform(1, 1);
        var hl = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
        var name = Path.GetFileNameWithoutExtension(path);
        var label = new TextBlock
        {
            Text = name.Length > 0 ? name : path, Foreground = Brushes.White, FontSize = 11, Width = 70, MaxHeight = 30,
            TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap,
        };
        var item = new Border
        {
            Width = 80, Height = 80, Margin = new(2), CornerRadius = new(8), Background = hl,
            RenderTransform = scale, RenderTransformOrigin = new(0.5, 0.5), ContextMenu = Menu(path),
            Child = new StackPanel
            {
                Margin = new(0, 6, 0, 0),
                Children = { new System.Windows.Controls.Image { Width = 40, Height = 40, Source = IconOf(path) }, label },
            },
        };

        var sc = _anim.Add(1, v => scale.ScaleX = scale.ScaleY = v, Motion.SnapK, Motion.SnapC);                    // spring-snappy
        var glow = _anim.Add(0, v => hl.Color = Color.FromArgb((byte)(Math.Clamp(v, 0, 1) * 40), 255, 255, 255));   // spring-soft
        item.MouseEnter += (_, _) => { _anim.To(sc, Motion.HoverScale); _anim.To(glow, 1); };
        item.MouseLeave += (_, _) => { _anim.To(sc, 1); _anim.To(glow, 0); };
        var tip = new ToolTip(); item.ToolTip = tip;
        tip.Opened += (_, _) => { tip.Content = Preview(path); Raise(tip); };
        System.Windows.Point? down = null;
        item.MouseLeftButtonDown += (_, e) =>
        {
            down = e.GetPosition(item); _anim.To(sc, Motion.PressScale);
        };
        item.MouseLeftButtonUp += (_, _) =>
        {
            var click = down != null; down = null; _anim.To(sc, Motion.HoverScale);
            if (click) Launch(path);   // one click opens; a drag (down cleared on move) does not
        };
        item.MouseMove += (_, e) =>
        {
            if (down is null || e.LeftButton != MouseButtonState.Pressed) return;
            if ((e.GetPosition(item) - down.Value).Length < 6) return;
            down = null;
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            data.SetData("gwindows/src", _d.Id);
            DragDrop.DoDragDrop(item, data, DragDropEffects.Link | DragDropEffects.Move);
            _anim.To(sc, 1);
        };
        return item;
    }

    /// <summary>First lines of a text file reading at most 4 KB, so a huge single-line file cannot blow up memory.</summary>
    static string ReadHead(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        var buf = new char[4096]; var n = sr.Read(buf, 0, buf.Length);
        return string.Join("\n", new string(buf, 0, n).Split('\n').Take(6).Select(l => (l.Length > 80 ? l[..80] : l).TrimEnd('\r')));
    }

    /// <summary>Hover preview: image thumbnail or first lines of a text file; otherwise just the path.</summary>
    static object Preview(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp")
            {
                if (new FileInfo(path).Length > 30 * 1024 * 1024) return path;   // never decode huge images on hover
                var bi = new BitmapImage(); bi.BeginInit(); bi.UriSource = new Uri(path); bi.DecodePixelWidth = 240; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit();
                return new StackPanel { Children = { new System.Windows.Controls.Image { Source = bi, MaxWidth = 240 }, new TextBlock { Text = path, FontSize = 10 } } };
            }
            if (ext is ".txt" or ".md" or ".log" or ".json" or ".cs" or ".csv" or ".xml")
                return ReadHead(path) + "\n\n" + path;
        }
        catch (Exception) { }
        return path;
    }

    internal static ImageSource? IconOf(string path)
    {
        try
        {
            if (Directory.Exists(path)) return Native.ShellIcon(path);
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && path.StartsWith(App.ShortcutDir, StringComparison.OrdinalIgnoreCase))
            {
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                string args = sh.CreateShortcut(path).Arguments;
                if (args.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && Native.ItemImage(args) is { } img) return img;   // Store apps (shell:AppsFolder\...) and shell folders (shell:RecycleBinFolder)
            }
            using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path);
            return ic is null ? null : Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch (Exception) { return null; }
    }
}





