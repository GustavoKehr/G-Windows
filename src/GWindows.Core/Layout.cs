using System.Text.Json;

namespace GWindows.Core;

/// <summary>A fence is a view: Items are references (paths), never copies.</summary>
public sealed class FenceData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nova cerca";
    // physical px, screen coords
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int W { get; set; } = 360;
    public int H { get; set; } = 260;
    public bool Rolled { get; set; }
    public bool Free { get; set; }        // false: snapped to the adaptive grid; true: stays wherever it is dropped
    public int Col { get; set; } = -1;    // preferred grid column (-1: derive from position)
    public string Monitor { get; set; } = "";   // device name of the monitor whose grid it belongs to
    public string Rule { get; set; } = "";
    public string Tint { get; set; } = "";
    public string Portal { get; set; } = "";   // folder mirrored by this fence
    public string Smart { get; set; } = "";    // recent | large | stale
    public string Kind { get; set; } = "";     // "" | clock | notes (widgets)
    public string Notes { get; set; } = "";
    public List<string> Excluded { get; set; } = [];   // items hidden from portal/smart fences
    public List<string> Items { get; set; } = [];
}

public sealed class LayoutFile
{
    public int Version { get; set; } = 1;
    public bool HideIcons { get; set; }
    public string Language { get; set; } = "";   // "pt" | "en" | "" (follow Windows)
    public bool ShowFences { get; set; }   // fences start hidden; the top-edge switch reveals them
    public bool DblClickToggle { get; set; }
    public string Profile { get; set; } = "Padrão";
    public Dictionary<string, List<FenceData>> Profiles { get; set; } = [];
    public List<FenceData> Fences { get; set; } = [];
}

public static class LayoutStore
{
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>True when layout.json exists but neither it nor the .bak could be used (the caller must not overwrite it blindly).</summary>
    public static bool LastLoadFailed { get; private set; }

    /// <summary>Reads layout.json (then .bak, then defaults). Any read/parse failure falls through to the next source, and the result is always normalized.</summary>
    public static LayoutFile Load(string path) => Load(path, 3, 150);

    /// <summary>Right after logon the file can be locked for a while (antivirus, sync clients, disk not settled): callers at startup wait longer.</summary>
    public static LayoutFile Load(string path, int attempts, int delayMs)
    {
        LastLoadFailed = false;
        foreach (var p in new[] { path, path + ".bak" })
        {
            for (var attempt = 0; attempt < attempts; attempt++)   // a locked file (antivirus, a crashing process) is often released within moments
            {
                try
                {
                    if (!File.Exists(p) || new FileInfo(p).Length > MaxBytes) break;
                    if (JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(p)) is { } l) return Normalize(l);
                    break;
                }
                catch (IOException) { Thread.Sleep(delayMs); }
                catch (UnauthorizedAccessException) { Thread.Sleep(delayMs); }
                catch (Exception) { break; }   // corrupt JSON / hostile content: try the next source
            }
        }
        LastLoadFailed = File.Exists(path);
        return new();
    }

    /// <summary>Makes untrusted/hand-edited layout data safe to use: no nulls, bounded numbers, counts and text sizes.
    /// Paths are deliberately left alone (network paths are allowed).</summary>
    public static LayoutFile Normalize(LayoutFile l)
    {
        l.Language = l.Language is "pt" or "en" ? l.Language : "";
        l.Profile = Text(l.Profile, 200, "Padrão");
        l.Fences = Fences(l.Fences);
        var profiles = new Dictionary<string, List<FenceData>>();
        foreach (var (k, v) in (l.Profiles ?? []).Take(100)) profiles[Text(k, 200, "perfil")] = Fences(v);   // indexer: truncated duplicate names just overwrite
        l.Profiles = profiles;
        return l;
    }

    static string Text(string? s, int max, string fallback = "") => s is null ? fallback : s.Length > max ? s[..max] : s;
    static List<string> Paths(List<string>? l) => (l ?? []).Where(s => !string.IsNullOrEmpty(s)).Select(s => Text(s, 1000)).Take(5000).ToList();

    static List<FenceData> Fences(List<FenceData>? list) => (list ?? []).Where(f => f is not null).Take(200).Select(f =>
    {
        f.Id = Text(f.Id, 64, Guid.NewGuid().ToString("N"));
        f.Name = Text(f.Name, 200, "Cerca");
        f.X = Math.Clamp(f.X, -30000, 30000); f.Y = Math.Clamp(f.Y, -30000, 30000);
        f.W = Math.Clamp(f.W, 160, 3000); f.H = Math.Clamp(f.H, 60, 4000);
        f.Col = Math.Clamp(f.Col, -1, 50);
        f.Monitor = Text(f.Monitor, 100); f.Rule = Text(f.Rule, 500); f.Tint = Text(f.Tint, 30);
        f.Portal = Text(f.Portal, 1000); f.Smart = Text(f.Smart, 30); f.Kind = Text(f.Kind, 30);
        f.Notes = Text(f.Notes, 200_000);
        f.Items = Paths(f.Items); f.Excluded = Paths(f.Excluded);
        return f;
    }).ToList();

    /// <summary>Atomic: write temp, then replace (keeping the previous valid file as .bak).</summary>
    public static void Save(string path, LayoutFile layout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(layout, Opt));
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
        else File.Move(tmp, path);
    }
}




