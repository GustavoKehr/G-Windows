using System.Runtime.InteropServices;

namespace GWindows;

/// <summary>The real Explorer context menu (IContextMenu) for a path, with one extra entry on top.
/// NOTE: no IContextMenu2/3 message forwarding, so owner-drawn submenus (Send to, Open with) may show empty.</summary>
static class ShellMenu
{
    [ComImport, Guid("000214e6-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellFolder
    {
        void ParseDisplayName(); void EnumObjects(); void BindToObject(); void BindToStorage(); void CompareIDs(); void CreateViewObject(); void GetAttributesOf();
        [PreserveSig] int GetUIObjectOf(IntPtr hwnd, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rsv, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    }

    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint pos, uint idFirst, uint idLast, uint flags);
        [PreserveSig] int InvokeCommand(ref INVOKE info);
        void GetCommandString();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INVOKE { public int cbSize, mask; public IntPtr hwnd, verb; public string? parms, dir; public int show, hotkey; public IntPtr icon; }

    [DllImport("shell32", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string n, IntPtr bc, out IntPtr pidl, uint a, out uint oa);
    [DllImport("shell32")] static extern int SHBindToParent(IntPtr pidl, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellFolder f, out IntPtr last);
    [DllImport("ole32")] static extern void CoTaskMemFree(IntPtr p);
    [DllImport("user32")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32")] static extern bool DestroyMenu(IntPtr m);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern bool InsertMenu(IntPtr m, uint pos, uint flags, UIntPtr id, string? text);
    [DllImport("user32")] static extern uint TrackPopupMenuEx(IntPtr m, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);
    [DllImport("user32")] static extern bool SetForegroundWindow(IntPtr h);

    /// <summary>Returns true if the extra entry was chosen.</summary>
    public static bool Show(IntPtr owner, string path, int x, int y, string extra)
    {
        if (SHParseDisplayName(path, 0, out var pidl, 0, out _) != 0) return false;
        IntPtr menu = 0; object? cm = null; IShellFolder? folder = null;
        try
        {
            var iid = typeof(IShellFolder).GUID;
            if (SHBindToParent(pidl, ref iid, out folder, out var last) != 0) return false;
            var iidCm = typeof(IContextMenu).GUID;
            if (folder.GetUIObjectOf(owner, 1, [last], ref iidCm, 0, out cm) != 0) return false;
            var ctx = (IContextMenu)cm;
            menu = CreatePopupMenu();
            ctx.QueryContextMenu(menu, 0, 2, 0x7FFF, 0);
            if (extra.Length > 0)
            {
                InsertMenu(menu, 0, 0xC00 /*BYPOSITION|SEPARATOR*/, 0, null);
                InsertMenu(menu, 0, 0x400 /*BYPOSITION*/, 1, extra);
            }
            SetForegroundWindow(owner);
            var id = TrackPopupMenuEx(menu, 0x0100 | 0x0002 /*RETURNCMD|RIGHTBUTTON*/, x, y, owner, 0);
            if (id == 1) return true;
            if (id >= 2)
            {
                var info = new INVOKE { cbSize = Marshal.SizeOf<INVOKE>(), hwnd = owner, verb = (IntPtr)(id - 2), show = 1 };
                ctx.InvokeCommand(ref info);
            }
            return false;
        }
        finally
        {
            if (menu != 0) DestroyMenu(menu);
            if (cm != null) Marshal.ReleaseComObject(cm);
            if (folder != null) Marshal.ReleaseComObject(folder);
            CoTaskMemFree(pidl);
        }
    }
}

