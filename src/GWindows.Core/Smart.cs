namespace GWindows.Core;

/// <summary>Auto-filled fences: the content is computed from the desktop items, never stored.</summary>
public static class Smart
{
    public static IEnumerable<string> Resolve(string kind, IEnumerable<string> paths, DateTime now) => kind switch
    {
        "recent" => paths.OrderByDescending(File.GetLastWriteTime).Take(12),
        "large" => paths.Where(p => File.Exists(p) && new FileInfo(p).Length > 100_000_000).OrderByDescending(p => new FileInfo(p).Length),
        "stale" => paths.Where(p => File.GetLastAccessTime(p) < now.AddDays(-90)),
        _ => [],
    };
}
