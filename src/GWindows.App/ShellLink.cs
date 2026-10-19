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
        void GetArguments(); void SetArguments(); void GetHotkey(); void SetHotkey(); void GetShowCmd(); void SetShowCmd();
        void GetIconLocation(); void SetIconLocation(); void SetRelativePath(); void Resolve(); void SetPath();
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
}