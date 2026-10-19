using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GWindows;

/// <summary>Works out where "Show in Explorer" should go for an item: the real install location, not the shortcut file.</summary>
public static class Locator
{
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern int GetPackagesByPackageFamily(string family, ref uint count, IntPtr[]? fullNames, ref uint bufLen, char[]? buf);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern int GetPackagePathByFullName(string fullName, ref uint len, StringBuilder? path);

    [DllImport("msi", CharSet = CharSet.Unicode)] static extern int MsiGetShortcutTarget(string lnk, StringBuilder product, StringBuilder feature, StringBuilder component);
    [DllImport("msi", CharSet = CharSet.Unicode)] static extern int MsiGetComponentPath(string product, string component, StringBuilder path, ref uint len);

    /// <summary>Windows Installer ("advertised") shortcuts store an installer reference instead of a path; this asks MSI for the real executable.</summary>
    static string? MsiTarget(string lnk)
    {
        StringBuilder prod = new(40), feat = new(40), comp = new(40);
        if (MsiGetShortcutTarget(lnk, prod, feat, comp) != 0) return null;   // needs an STA thread (the UI thread is)
        var sb = new StringBuilder(520); uint len = (uint)sb.Capacity;
        return MsiGetComponentPath(prod.ToString(), comp.ToString(), sb, ref len) > 0 ? sb.ToString() : null;   // > 0 = INSTALLSTATE_LOCAL or similar
    }

    /// <summary>Folder of an installed Store/system package from its family name ("Microsoft.WindowsCalculator_8wekyb3d8bbwe"), or null.</summary>
    public static string? PackageFolder(string family)
    {
        uint count = 0, buf = 0;
        GetPackagesByPackageFamily(family, ref count, null, ref buf, null);   // 122 (insufficient buffer) just reports the sizes
        if (count == 0) return null;
        var names = new IntPtr[count]; var chars = new char[buf];
        if (GetPackagesByPackageFamily(family, ref count, names, ref buf, chars) != 0) return null;
        var full = Marshal.PtrToStringUni(names[0]);
        if (full is null) return null;
        uint len = 0; GetPackagePathByFullName(full, ref len, null);
        if (len == 0) return null;
        var sb = new StringBuilder((int)len);
        return GetPackagePathByFullName(full, ref len, sb) == 0 ? sb.ToString() : null;
    }

    /// <summary>What to do for "Show in Explorer": either select a file (Select) or open a folder (Open). Never both.</summary>
    public readonly record struct Target(string? Select, string? Open);

    public static Target Resolve(string path)
    {
        if (Directory.Exists(path)) return new(null, path);
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return new(path, null);

        var (target, args) = ShellLink.Read(path);
        if (target is null || target.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) target = MsiTarget(path) ?? target;
        // Store / system apps: the shortcut launches explorer with shell:AppsFolder\<PackageFamilyName>!<AppId>
        if (args is not null && args.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
        {
            var aumid = args[@"shell:AppsFolder\".Length..];
            var family = aumid.Split('!')[0];
            if (family.Contains('_') && PackageFolder(family) is { } dir) return new(null, dir);
        }
        // Special shell folders (Recycle Bin, ...): open them
        if (args is not null && args.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return new(null, args);
        // Steam games and other URI launchers: the shortcut's icon points at the game's executable
        if (target is not null && Path.GetFileName(target).Equals("steam.exe", StringComparison.OrdinalIgnoreCase) && args is not null && args.StartsWith("steam://", StringComparison.OrdinalIgnoreCase)
            && ShellLink.IconPath(path) is { } icon && File.Exists(icon)) return new(icon, null);
        // Regular shortcut: select what it points to
        if (target is not null && (File.Exists(target) || Directory.Exists(target))) return Directory.Exists(target) ? new(null, target) : new(target, null);
        // Shortcuts without a resolvable target (some vendor tiles): fall back to the shortcut itself
        return new(path, null);
    }
}
