using System.Runtime.InteropServices;
using System.Text;

namespace GWindows;

/// <summary>Reads a .lnk target through IShellLink (thread-agnostic, no WScript/dynamic).</summary>
static class ShellLink
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class CShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr fd, uint flags);
        void GetIDList(); void SetIDList(); void GetDescription(); void SetDescription(); void GetWorkingDirectory(); void SetWorkingDirectory();
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch); void SetArguments(); void GetHotkey(); void SetHotkey(); void GetShowCmd(); void SetShowCmd();
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int cch, out int index); void SetIconLocation(); void SetRelativePath(); void Resolve(); void SetPath();
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPersistFile
    {
        void GetClassID(out Guid id); [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string file, uint mode);
        void Save(); void SaveCompleted(); void GetCurFile();
    }

    public static string? Target(string lnk)
    {
        var link = new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnk, 0);
            var sb = new StringBuilder(520);
            ((IShellLinkW)link).GetPath(sb, sb.Capacity, 0, 0);
            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch (Exception) { return null; }
        finally { Marshal.ReleaseComObject(link); }
    }

    /// <summary>Target path and arguments of a shortcut (either may be null).</summary>
    public static (string? Target, string? Args) Read(string lnk)
    {
        var link = new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnk, 0);
            var p = new StringBuilder(520); var a = new StringBuilder(2048);
            ((IShellLinkW)link).GetPath(p, p.Capacity, 0, 0);
            ((IShellLinkW)link).GetArguments(a, a.Capacity);
            return (p.Length > 0 ? p.ToString() : null, a.Length > 0 ? a.ToString() : null);
        }
        catch (Exception) { return (null, null); }
        finally { Marshal.ReleaseComObject(link); }
    }

    /// <summary>The file the shortcut's icon comes from (environment variables expanded), or null.</summary>
    public static string? IconPath(string lnk)
    {
        var link = new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnk, 0);
            var sb = new StringBuilder(520);
            ((IShellLinkW)link).GetIconLocation(sb, sb.Capacity, out _);
            return sb.Length > 0 ? Environment.ExpandEnvironmentVariables(sb.ToString()) : null;
        }
        catch (Exception) { return null; }
        finally { Marshal.ReleaseComObject(link); }
    }
}