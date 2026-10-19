using System.IO;
using System.Windows;
using System.Windows.Threading;
using GWindows.Core;

namespace GWindows;

public partial class App : Application
{
    public static readonly string LayoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "G-Windows", "layout.json");
    public static LayoutFile Layout = new();
    public static readonly List<FenceWindow> Wins = [];
    static DispatcherTimer? _debounce;
    static System.Windows.Forms.NotifyIcon? _tray;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static bool AutoStart
    {
        get { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("G-Windows") != null; }
        set
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true)!;
            if (value) k.SetValue("G-Windows", $"\"{Environment.ProcessPath}\" --startup"); else k.DeleteValue("G-Windows", false);
        }
    }

    static System.Windows.Forms.ToolStripMenuItem? _show;
    static readonly List<TogglePill> _pills = [];
    static Mutex? _single;

    static void WatchDblClick(bool on)
    {
        if (on) Native.WatchDesktopDoubleClick(() => _show!.Checked = !_show.Checked, () => Layout.HideIcons);
        else Native.StopWatching();
    }

    /// <summary>--perf: every 10 s append CPU % (of one core) and memory to %LocalAppData%\G-Windows\perf.log.</summary>
    static void Perf()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "G-Windows", "perf.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var p = System.Diagnostics.Process.GetCurrentProcess(); var cpu = p.TotalProcessorTime; var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) =>
        {
            p.Refresh(); var now = DateTime.UtcNow;
            var pct = (p.TotalProcessorTime - cpu).TotalSeconds / (now - t0).TotalSeconds * 100; cpu = p.TotalProcessorTime; t0 = now;
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss} cpu={pct:F2}% ws={p.WorkingSet64 >> 20}MB private={p.PrivateMemorySize64 >> 20}MB\n");
        };
        timer.Start();
    }

    static void Tray()
    {
        var m = new System.Windows.Forms.ContextMenuStrip { Renderer = new DarkRenderer(), ForeColor = System.Drawing.Color.White, BackColor = System.Drawing.Color.FromArgb(24, 26, 32), Font = new System.Drawing.Font("Segoe UI Variable Text", 9.5f) };
        m.Items.Add(L.T("Nova cerca"), null, (_, _) => NewFence());
        m.Items.Add(L.T("Alinhar todas as cercas à grade"), null, (_, _) => { foreach (var f in Layout.Fences) { f.Free = false; f.Col = -1; f.Monitor = ""; } Arrange(true); SaveSoon(); });
        ProfileMenu((System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Perfis")));
        var show = _show = (System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Mostrar cercas"), null);
        show.CheckOnClick = true; show.Checked = Layout.ShowFences;
        show.CheckedChanged += (_, _) =>
        {
            Layout.ShowFences = show.Checked;
            foreach (var w in Wins) w.Fade(show.Checked);
            foreach (var p in _pills) p.SetOn(show.Checked); SaveSoon();
        };
        var hide = (System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Ocultar ícones do desktop"), null);
        hide.CheckOnClick = true; hide.Checked = Layout.HideIcons;
        hide.CheckedChanged += (_, _) => { Layout.HideIcons = hide.Checked; Native.SetNativeIcons(!hide.Checked); SaveSoon(); };
        var dbl = (System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Duplo clique no desktop mostra/oculta"), null);
        dbl.CheckOnClick = true; dbl.Checked = Layout.DblClickToggle;
        dbl.CheckedChanged += (_, _) => { Layout.DblClickToggle = dbl.Checked; WatchDblClick(dbl.Checked); SaveSoon(); };
        var auto = (System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Iniciar com o Windows"), null);
        auto.CheckOnClick = true; auto.Checked = AutoStart;
        auto.CheckedChanged += (_, _) => AutoStart = auto.Checked;
        FileMenu(m);
        var lang = (System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Idioma") + " / Language");
        foreach (var (code, name) in new[] { ("pt", "Português"), ("en", "English") })
            ((System.Windows.Forms.ToolStripMenuItem)lang.DropDownItems.Add(name, null, (_, _) => SetLanguage(code))).Checked = L.Current == code;
        m.Items.Add(L.T("Sair"), null, (_, _) => Current.Shutdown());
        _tray = new() { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Text = "G-Windows", Visible = true, ContextMenuStrip = m };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // WPF mirrors menus to the left of the pointer when Windows is set to left-handed menus; always open them to the right.
        typeof(SystemParameters).GetField("_menuDropAlignment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, false);
        _single = new Mutex(true, "GWindows.SingleInstance", out var first);
        if (!first) { MessageBox.Show(L.T("O G-Windows já está em execução. Procure o ícone na bandeja do Windows."), "G-Windows", MessageBoxButton.OK, MessageBoxImage.Information); Shutdown(); return; }
        if (e.Args.Contains("--startup")) System.Threading.Thread.Sleep(4000);   // at logon, let the shell and disk settle first
        Layout = LayoutStore.Load(LayoutPath, 30, 500);   // waits up to ~15 s for a locked file
        if (LayoutStore.LastLoadFailed)
        {
            // Never overwrite a layout we could not read: run with defaults, but do not save in this session.
            _noSave = true;
            MessageBox.Show(L.T("Não foi possível ler o seu layout (o arquivo está intacto). O G-Windows abriu com o padrão e não vai salvar nesta sessão. Feche e abra o app novamente."), "G-Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        if (Layout.Language.Length > 0) L.Current = Layout.Language;
        if (Layout.Fences.Count == 0) Layout.Fences.Add(new FenceData { Name = "Desktop", Items = DesktopItems().Take(24).ToList() });
        foreach (var f in Layout.Fences) Fit(f);
        foreach (var f in Layout.Fences.ToList()) Open(f);
        Arrange(false);
        Watch();
        WatchSmartSources();
        Snapshot();
        Native.Hotkey(1, 2, 0x20, ShowPalette);   // Ctrl+Space
        Native.Hotkey(2, 3, 0x43, () => _show!.Checked = !_show.Checked);   // Ctrl+Alt+C
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.InvokeAsync(() => { PillsOnEveryScreen(); foreach (var w in Wins) { Fit(w.Data); w.Reposition(); } Arrange(false); SaveSoon(); });
        Native.SetNativeIcons(!Layout.HideIcons);
        Tray();
        PillsOnEveryScreen();
        if (Layout.DblClickToggle) WatchDblClick(true);
        if (e.Args.Contains("--perf")) Perf();
        Exit += (_, _) => { Native.StopWatching(); Native.SetNativeIcons(true); _tray?.Dispose(); SaveNow(); };
    }

    /// <summary>Keep a fence reachable after monitors change: if its title corner is on no screen, move it to the primary one.</summary>
    static void Fit(FenceData f)
    {
        var pt = new System.Drawing.Point(f.X + 20, f.Y + 10);
        if (System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.Contains(pt))) return;
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        f.X = wa.X + 100; f.Y = wa.Y + 100;
    }

    /// <summary>Rules: a new/renamed file on the desktop goes to the first fence whose rule matches (reference only, nothing moves).</summary>
    static void Watch()
    {
        foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }.Where(Directory.Exists))
        {
            var w = new FileSystemWatcher(dir) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, EnableRaisingEvents = true };
            void On(string p) => Current.Dispatcher.InvokeAsync(() =>
            {
                if (Rules.Target(Layout.Fences, p) is { } f) { f.Items.Add(p); Wins.FirstOrDefault(x => x.Data == f)?.Refresh(); SaveSoon(); }
                foreach (var x in Wins.Where(x => x.Data.Smart.Length > 0)) x.Refresh();
            });
            w.Created += (_, e) => On(e.FullPath);
            w.Renamed += (_, e) => On(e.FullPath);
        }
    }

    static DispatcherTimer? _smartTimer;

    static void WatchSmartSources()
    {
        foreach (var dir in new[] { Native.KnownFolder(DownloadsId), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)))
        {
            var w = new FileSystemWatcher(dir!) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, EnableRaisingEvents = true };
            FileSystemEventHandler on = (_, _) => Current.Dispatcher.InvokeAsync(() =>
            {
                _smartTimer ??= new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
                _smartTimer.Tick -= RefreshSmart; _smartTimer.Tick += RefreshSmart;
                _smartTimer.Stop(); _smartTimer.Start();   // many events in a row (a download) = one refresh
            });
            w.Created += on; w.Deleted += on; w.Renamed += (s, e) => on(s, e);
        }
    }

    static void RefreshSmart(object? s, EventArgs e) { _smartTimer!.Stop(); foreach (var w in Wins.Where(w => w.Data.Smart.Length > 0 && w.Data.Smart != "folders")) w.Refresh(); }

    public static void SetLanguage(string lang)
    {
        Layout.Language = L.Current = lang;
        foreach (var w in Wins) w.Relocalize();
        Current.Dispatcher.InvokeAsync(() => { _tray?.Dispose(); Tray(); });   // not from inside the tray menu that is being clicked
        SaveSoon();
    }

    static void SwitchProfile(string name)
    {
        if (name == Layout.Profile) return;
        Snapshot();
        Layout.Profiles[Layout.Profile] = Clone(Layout.Fences);
        Layout.Profile = name;
        Reload(Layout.Profiles.TryGetValue(name, out var l) ? Clone(l) : []);
    }

    static void Reload(List<FenceData> fences)
    {
        Layout.Fences = fences;
        foreach (var w in Wins.ToList()) w.Close();
        Wins.Clear();
        foreach (var f in Layout.Fences) { Fit(f); Open(f); }
        Arrange(false);
        SaveSoon();
    }

    static string SnapDir => Path.Combine(Path.GetDirectoryName(LayoutPath)!, "snapshots");

    /// <summary>Snapshot = copy of layout.json; kept 30 days, at most 50.</summary>
    static void Snapshot()
    {
        try
        {
            if (!File.Exists(LayoutPath)) return;
            Directory.CreateDirectory(SnapDir);
            File.Copy(LayoutPath, Path.Combine(SnapDir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), true);
            foreach (var f in new DirectoryInfo(SnapDir).GetFiles("*.json").OrderByDescending(f => f.Name).Select((f, i) => (f, i)).Where(x => x.i >= 50 || x.f.LastWriteTime < DateTime.Now.AddDays(-30))) f.f.Delete();
        }
        catch (IOException) { }
    }

    static void ShowPalette()
    {
        var items = Layout.Fences.SelectMany(f => f.Items).Concat(DesktopItems()).Distinct().Select(Palette.Item);
        var cmd = L.T("Comando");
        var cmds = new[] { new Entry(L.T("Nova cerca"), cmd, null, NewFence), new Entry(L.T("Alternar cercas"), cmd, null, () => _show!.Checked = !_show.Checked) }
            .Concat(Layout.Profiles.Keys.Append(Layout.Profile).Distinct().Select(n => new Entry(L.T("Perfil: ") + n, cmd, null, () => SwitchProfile(n))));
        Palette.Show(cmds.Concat(items));
    }

    static void FileMenu(System.Windows.Forms.ContextMenuStrip m)
    {
        var snaps = (System.Windows.Forms.ToolStripMenuItem)m.Items.Add(L.T("Restaurar snapshot"));
        snaps.DropDownOpening += (_, _) =>
        {
            snaps.DropDownItems.Clear();
            if (!Directory.Exists(SnapDir)) return;
            foreach (var f in new DirectoryInfo(SnapDir).GetFiles("*.json").OrderByDescending(f => f.Name).Take(15))
                snaps.DropDownItems.Add(f.Name[..^5], null, (_, _) => ImportFrom(f.FullName));
        };
        m.Items.Add(L.T("Exportar layout..."), null, (_, _) =>
        {
            using var d = new System.Windows.Forms.SaveFileDialog { Filter = "G-Windows layout|*.json", FileName = "g-windows-layout.json" };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) { SaveNow(); File.Copy(LayoutPath, d.FileName, true); }
        });
        m.Items.Add(L.T("Importar layout..."), null, (_, _) =>
        {
            using var d = new System.Windows.Forms.OpenFileDialog { Filter = "G-Windows layout|*.json" };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) ImportFrom(d.FileName);
        });
    }

    /// <summary>Loads a layout file (already normalized by LayoutStore). Network paths are allowed, but since Windows contacts those servers as soon as an item is drawn, the user confirms first.</summary>
    static void ImportFrom(string file)
    {
        var fences = LayoutStore.Load(file).Fences;
        var net = fences.SelectMany(f => f.Items.Append(f.Portal)).Count(p => p.StartsWith(@"\\", StringComparison.Ordinal));
        if (net > 0 && MessageBox.Show(string.Format(L.T("Este layout contém {0} caminho(s) de rede (\\\\servidor\\...). Ao carregar, o Windows se conecta a esses servidores. Continuar?"), net),
                "G-Windows", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Snapshot(); Reload(fences);
    }

    static List<FenceData> Clone(List<FenceData> l) => System.Text.Json.JsonSerializer.Deserialize<List<FenceData>>(System.Text.Json.JsonSerializer.Serialize(l))!;

    static void ProfileMenu(System.Windows.Forms.ToolStripMenuItem root)
    {
        root.DropDownOpening += (_, _) =>
        {
            root.DropDownItems.Clear();
            root.DropDownItems.Add(L.T("Salvar perfil atual como..."), null, (_, _) =>
            {
                if (Prompt.Ask(L.T("Nome do perfil")) is { Length: > 0 } n) { Layout.Profiles[n] = Clone(Layout.Fences); Layout.Profile = n; SaveSoon(); }
            });
            root.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());
            foreach (var n in Layout.Profiles.Keys.Append(Layout.Profile).Distinct().Order())
            {
                var item = (System.Windows.Forms.ToolStripMenuItem)root.DropDownItems.Add(n, null, (_, _) => SwitchProfile(n));
                item.Checked = n == Layout.Profile;
            }
        };
        root.DropDownItems.Add("-");
    }

    /// <summary>One switch at the top-center of each monitor, rebuilt when monitors change.</summary>
    static void PillsOnEveryScreen()
    {
        foreach (var p in _pills) p.Close();
        _pills.Clear();
        foreach (var s in System.Windows.Forms.Screen.AllScreens)
        {
            var p = new TogglePill(s, Layout.ShowFences, () => _show!.Checked = !_show.Checked);
            _pills.Add(p); p.Show();
        }
    }

    /// <summary>Monitor whose grid a fence belongs to: the remembered one if still connected, else the one under its center, else primary.</summary>
    static System.Windows.Forms.Screen ScreenOf(FenceData f)
    {
        var all = System.Windows.Forms.Screen.AllScreens;
        var byName = all.FirstOrDefault(s => s.DeviceName == f.Monitor);
        return byName ?? System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(f.X + f.W / 2, f.Y + f.H / 2));
    }

    /// <summary>Put every non-free fence in its grid slot, per monitor. Fences without a column take the one nearest to where they are.</summary>
    public static void Arrange(bool animate)
    {
        foreach (var g in Wins.Where(w => !w.Data.Free).GroupBy(w => ScreenOf(w.Data).DeviceName).ToList())
        {
            var s = System.Windows.Forms.Screen.AllScreens.First(x => x.DeviceName == g.Key); var a = s.WorkingArea;
            var list = g.OrderBy(w => Layout.Fences.IndexOf(w.Data)).ToList();   // stacking order = list order
            foreach (var w in list) w.FitHeight();
            foreach (var w in list)
            {
                w.Data.Monitor = s.DeviceName;
                if (w.Data.Col < 0) w.Data.Col = FenceGrid.NearestColumn(a.Right, a.Width, w.Data.X + w.Data.W / 2);
            }
            var slots = FenceGrid.Layout(a.Left, a.Top, a.Right, a.Bottom, list.Select(w => (w.Data.Col, w.Data.H)).ToList());
            for (var i = 0; i < list.Count; i++) { list[i].Data.Col = slots[i].col; list[i].MoveTo(slots[i].x, slots[i].y, slots[i].w, animate); }
        }
    }

    /// <summary>After a drag ends: a moved snapped fence is re-inserted at the column/row it was dropped on, then everything re-aligns.</summary>
    public static void AfterDrag(FenceWindow w, bool moved)
    {
        var d = w.Data;
        if (moved && !d.Free)
        {
            var s = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(d.X + d.W / 2, d.Y + d.H / 2)); var a = s.WorkingArea;
            d.Monitor = s.DeviceName; d.Col = FenceGrid.NearestColumn(a.Right, a.Width, d.X + d.W / 2);
            // list order = stacking order inside a column: go before the first fence in that column that sits lower than the drop point
            Layout.Fences.Remove(d);
            var before = Layout.Fences.FirstOrDefault(f => !f.Free && f.Monitor == d.Monitor && f.Col == d.Col && f.Y + f.H / 2 > d.Y + d.H / 2);
            Layout.Fences.Insert(before is null ? Layout.Fences.Count : Layout.Fences.IndexOf(before), d);
        }
        if (!d.Free) Arrange(true);
        SaveSoon();
    }

    static void Open(FenceData f) { var w = new FenceWindow(f); Wins.Add(w); w.Show(); if (!Layout.ShowFences) w.Hide_(); }

    public static void NewFence()
    {
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var f = new FenceData { X = wa.Right - 500, Y = wa.Top + 60 };   // lands in the right-most column; Arrange sets the slot
        _show!.Checked = true;
        Layout.Fences.Add(f); Open(f); Arrange(true); SaveSoon();
    }

    /// <summary>Layout is saved 500 ms after the last change.</summary>
    public static void SaveSoon()
    {
        if (_debounce is null)
        {
            _debounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
            _debounce.Tick += (_, _) => { _debounce.Stop(); SaveNow(); };
        }
        _debounce.Stop(); _debounce.Start();
    }

    static bool _noSave;

    static void SaveNow() { if (!_noSave) LayoutStore.Save(LayoutPath, Layout); }

    /// <summary>Most used folders, ranked by how many Recent entries (folder shortcuts or files inside) point at them. Windows keeps no true access counter.</summary>
    public static List<string>? FoldersCache;
    public static bool FoldersLoading;
    public static readonly string ShortcutDir = Path.Combine(Path.GetDirectoryName(LayoutPath)!, "shortcuts");

    /// <summary>WScript.Shell is an STA COM object: run the scan on its own STA thread.</summary>
    public static Task<List<string>> FrequentFoldersAsync()
    {
        var tcs = new TaskCompletionSource<List<string>>();
        var th = new Thread(() => tcs.SetResult(FrequentFolders())) { IsBackground = true };
        th.SetApartmentState(ApartmentState.STA); th.Start();
        return tcs.Task;
    }

    public static List<string> FrequentFolders()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            foreach (var f in new DirectoryInfo(recent).GetFiles("*.lnk").OrderByDescending(f => f.LastWriteTime).Take(600))
            {
                try   // one unreadable shortcut must not stop the scan
                {
                    var target = ShellLink.Target(f.FullName);
                    if (string.IsNullOrEmpty(target)) continue;
                    var dir = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
                    if (dir is { Length: > 3 } && Directory.Exists(dir)) counts[dir] = counts.GetValueOrDefault(dir) + 1;
                }
                catch (Exception) { }
            }
        }
        catch (Exception) { }
        var skip = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.Recent) };
        return counts.Where(c => !skip.Any(s => c.Key.StartsWith(s, StringComparison.OrdinalIgnoreCase))).OrderByDescending(c => c.Value).Take(15).Select(c => c.Key).ToList();
    }

    static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>Folders the smart fences look at: Desktop (user + public), Downloads and Documents; top-level entries only, no hidden/system ones.</summary>
    public static List<string> SmartSources() =>
        new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Native.KnownFolder(DownloadsId), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(d => { try { return Directory.EnumerateFileSystemEntries(d!).ToList(); } catch (Exception) { return []; } })
            .Where(p => { try { return !File.GetAttributes(p).HasFlag(FileAttributes.Hidden) && !File.GetAttributes(p).HasFlag(FileAttributes.System); } catch (Exception) { return false; } })
            .ToList();

    public static IEnumerable<string> DesktopItems() =>
        new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFileSystemEntries(d))
            .Where(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase));
}








