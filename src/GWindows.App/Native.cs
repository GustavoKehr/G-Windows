using System.Runtime.InteropServices;

namespace GWindows;

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [DllImport("user32", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cx, uint f);
    [DllImport("user32")] public static extern bool GetCursorPos(out POINT p);

    [DllImport("user32", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr after, string cls, string? title);
    [DllImport("user32")] static extern bool ShowWindow(IntPtr h, int cmd);
    const uint NOACTIVATE = 0x0010, SHOWWINDOW = 0x0040;

    /// <summary>Top-level window pushed to the bottom of the z-order: above the wallpaper/icons, below every app window.
    /// NOTE: reparenting into Progman/WorkerW rendered nothing on Win11 here; Win+D hides fences. Revisit per Windows build.</summary>
    public static void Attach(IntPtr hwnd)
    {
        const int GWL_EXSTYLE = -20; const long TOOLWINDOW = 0x80; // hide from Alt+Tab
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() | TOOLWINDOW));
    }

    /// <summary>Position in physical screen pixels.</summary>
    public static void Place(IntPtr hwnd, int x, int y, int w, int h) =>
        SetWindowPos(hwnd, 1 /*HWND_BOTTOM*/, x, y, w, h, NOACTIVATE | SHOWWINDOW);

    /// <summary>Show/hide the native desktop icon list (SysListView32). Must be restored on exit.</summary>
    public static void SetNativeIcons(bool visible)
    {
        foreach (var cls in new[] { "Progman", "WorkerW" })
            for (IntPtr top = 0; (top = FindWindowEx(0, top, cls, null)) != 0;)
            {
                var view = FindWindowEx(top, 0, "SHELLDLL_DefView", null);
                var list = view == 0 ? 0 : FindWindowEx(view, 0, "SysListView32", null);
                if (list != 0) ShowWindow(list, visible ? 5 : 0);
            }
    }

    delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [DllImport("user32")] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr mod, uint tid);
    [DllImport("user32")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("user32")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32")] static extern uint GetDoubleClickTime();
    [DllImport("user32")] static extern int GetSystemMetrics(int i);

    static IntPtr _hook; static HookProc? _proc;

    /// <summary>Low-level mouse hook that only looks at left-button-down positions on the empty desktop; never keys or other windows' content.
    /// iconsHidden: with the icon list visible an icon's own double-click would also toggle, so only Progman/WorkerW count then.</summary>
    public static void WatchDesktopDoubleClick(Action toggle, Func<bool> iconsHidden)
    {
        if (_hook != 0) return;
        long lastT = 0; POINT last = default;
        _proc = (code, w, l) =>
        {
            if (code >= 0 && w == 0x201)
            {
                var pt = Marshal.PtrToStructure<POINT>(l);
                var sb = new System.Text.StringBuilder(64);
                GetClassName(WindowFromPoint(pt), sb, 64);
                var c = sb.ToString();
                var desktop = c is "Progman" or "WorkerW" || (iconsHidden() && c is "SysListView32" or "SHELLDLL_DefView");
                var now = Environment.TickCount64; var d = GetSystemMetrics(36) / 2;
                if (desktop && now - lastT <= GetDoubleClickTime() && Math.Abs(pt.X - last.X) <= d && Math.Abs(pt.Y - last.Y) <= d)
                { lastT = 0; toggle(); }
                else if (desktop) { lastT = now; last = pt; }
                else lastT = 0;
            }
            return CallNextHookEx(_hook, code, w, l);
        };
        _hook = SetWindowsHookEx(14, _proc, 0, 0);
    }

    public static void StopWatching() { if (_hook != 0) UnhookWindowsHookEx(_hook); _hook = 0; }

    [DllImport("user32")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);

    /// <summary>Thread-level global hotkey (mods: 1=Alt 2=Ctrl 4=Shift 8=Win). Fails silently if another app owns the combination.</summary>
    public static void Hotkey(int id, uint mods, uint vk, Action onPress)
    {
        RegisterHotKey(0, id, mods | 0x4000 /*NOREPEAT*/, vk);
        System.Windows.Interop.ComponentDispatcher.ThreadPreprocessMessage += (ref System.Windows.Interop.MSG m, ref bool handled) =>
        {
            if (m.message == 0x0312 && (int)m.wParam == id) { onPress(); handled = true; }
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attr; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string name; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string type; }
    [DllImport("shell32", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string p, uint a, ref SHFILEINFO i, uint s, uint f);
    [DllImport("user32")] static extern bool DestroyIcon(IntPtr h);

    /// <summary>Explorer's own icon for a path (used for folders); caller gets a frozen bitmap, the HICON is released.</summary>
    public static System.Windows.Media.ImageSource? ShellIcon(string path)
    {
        var i = new SHFILEINFO();
        if (SHGetFileInfo(path, 0, ref i, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 /*ICON*/) == 0 || i.hIcon == 0) return null;
        try
        {
            var s = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(i.hIcon, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            s.Freeze(); return s;
        }
        finally { DestroyIcon(i.hIcon); }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(long size, int flags, out IntPtr hbm); }
    [StructLayout(LayoutKind.Sequential)] struct BITMAP { public int type, w, h, stride; public short planes, bpp; public IntPtr bits; }
    [DllImport("shell32", CharSet = CharSet.Unicode, PreserveSig = false)] static extern void SHCreateItemFromParsingName(string p, IntPtr bc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory f);
    [StructLayout(LayoutKind.Sequential)] struct BIH { public int size, width, height; public short planes, bpp; public int comp, sizeImage, xppm, yppm, used, important; }
    [StructLayout(LayoutKind.Sequential)] struct DIBSECTION { public BITMAP bm; public BIH bih; public int f1, f2, f3; public IntPtr section; public int offset; }
    [DllImport("gdi32")] static extern int GetObject(IntPtr h, int n, out DIBSECTION b);
    [DllImport("gdi32")] static extern bool DeleteObject(IntPtr h);

    /// <summary>Real tile icon (with transparency) of any shell item, e.g. "shell:AppsFolder\&lt;AUMID&gt;" for Store apps.</summary>
    public static System.Windows.Media.ImageSource? ItemImage(string parsingName, int px = 48)
    {
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(parsingName, 0, ref iid, out var f);
            if (f.GetImage((long)px | (long)px << 32, 0x4 /*ICONONLY: no light backing plate*/, out var hbm) != 0 || hbm == 0) return null;
            try
            {
                GetObject(hbm, Marshal.SizeOf<DIBSECTION>(), out var ds);
                var bm = ds.bm;
                System.Windows.Media.Imaging.BitmapSource s = System.Windows.Media.Imaging.BitmapSource.Create(bm.w, bm.h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, bm.bits, bm.stride * bm.h, bm.stride);
                // positive biHeight = bottom-up DIB: rows are stored last-first, so flip it
                if (ds.bih.height > 0) s = new System.Windows.Media.Imaging.TransformedBitmap(s, new System.Windows.Media.ScaleTransform(1, -1));
                s.Freeze(); return s;
            }
            finally { DeleteObject(hbm); }
        }
        catch (Exception) { return null; }
    }

    /// <summary>Fence windows sit at the bottom of the z-order, and so would their owned popups: lift a popup above every normal window.</summary>
    public static void Topmost(IntPtr hwnd) => SetWindowPos(hwnd, -1 /*HWND_TOPMOST*/, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /*NOSIZE|NOMOVE|NOACTIVATE*/);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [DllImport("user32")] static extern bool GetWindowRect(IntPtr h, out RECT r);

    /// <summary>Open a popup at the pointer, to the right and below it, kept inside the monitor and above all windows
    /// (WPF mirrors menus to the left when Windows is set to left-handed menus, and offers no switch for it).</summary>
    public static void PopupAtCursor(IntPtr hwnd, int inset)
    {
        GetCursorPos(out var c); GetWindowRect(hwnd, out var r);
        int w = r.R - r.L, h = r.B - r.T;
        var wa = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(c.X, c.Y)).WorkingArea;
        int x = Math.Clamp(c.X - inset, wa.Left, Math.Max(wa.Left, wa.Right - w));
        int y = c.Y - inset + h > wa.Bottom ? Math.Max(wa.Top, c.Y + inset - h) : c.Y - inset;
        SetWindowPos(hwnd, -1, x, y, 0, 0, 0x0001 | 0x0010 /*NOSIZE|NOACTIVATE*/);
    }

    [DllImport("shell32", CharSet = CharSet.Unicode)] static extern bool SHObjectProperties(IntPtr hwnd, uint type, string obj, string? page);

    /// <summary>Explorer's Properties dialog for a file or folder.</summary>
    public static bool Properties(string path) => SHObjectProperties(0, 2 /*SHOP_FILEPATH*/, path, null);

    [StructLayout(LayoutKind.Sequential)] struct WINDOWPOS { public IntPtr hwnd, after; public int x, y, cx, cy; public uint flags; }

    /// <summary>WPF re-applies its own z-order every time it shows or moves a popup, undoing a one-off SetWindowPos.
    /// This hook rewrites every z-order change of the popup to "topmost", so menus and tooltips stay above all fences and apps.</summary>
    public static void KeepTopmost(System.Windows.Interop.HwndSource s)
    {
        s.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg == 0x0046 /*WM_WINDOWPOSCHANGING*/)
            {
                var p = Marshal.PtrToStructure<WINDOWPOS>(l);
                p.after = -1; p.flags &= ~0x0004u /*clear SWP_NOZORDER*/;
                Marshal.StructureToPtr(p, l, false);
            }
            return IntPtr.Zero;
        });
        Topmost(s.Handle);
    }

    [DllImport("shell32")] static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    [DllImport("ole32")] static extern void CoTaskMemFree(IntPtr p);

    /// <summary>Path of a Windows known folder (e.g. Downloads, which .NET's SpecialFolder lacks), even if the user relocated it.</summary>
    public static string? KnownFolder(Guid id)
    {
        if (SHGetKnownFolderPath(ref id, 0, 0, out var p) != 0) return null;
        try { return Marshal.PtrToStringUni(p); } finally { CoTaskMemFree(p); }
    }

    [DllImport("user32")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("shcore")] static extern int GetDpiForMonitor(IntPtr mon, int type, out uint x, out uint y);

    /// <summary>DPI scale factor (1.0 = 96 dpi) of the monitor that contains the middle of <paramref name="bounds"/>.</summary>
    public static double ScaleOf(System.Drawing.Rectangle bounds)
    {
        try
        {
            var mon = MonitorFromPoint(new POINT { X = bounds.X + bounds.Width / 2, Y = bounds.Y + bounds.Height / 2 }, 2 /*MONITOR_DEFAULTTONEAREST*/);
            return GetDpiForMonitor(mon, 0 /*MDT_EFFECTIVE_DPI*/, out var dx, out _) == 0 && dx > 0 ? dx / 96.0 : 1.0;
        }
        catch (Exception) { return 1.0; }
    }
}