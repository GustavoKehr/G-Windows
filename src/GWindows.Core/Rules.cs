

namespace GWindows.Core;

public static class Rules
{
    /// <summary>Rule = patterns separated by ';' or ',' with * and ? wildcards (e.g. "*.pdf; invoice*"), matched against the file name.</summary>
    public static bool Matches(string rule, string path)
    {
        var name = Path.GetFileName(path);
        foreach (var pat in rule.Split([';', ','],StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Glob(pat, name)) return true;
        return false;
    }

    /// <summary>Iterative wildcard match (* and ?, case-insensitive) in O(n·m) worst case: no regex, so no catastrophic backtracking.</summary>
    static bool Glob(string pat, string s)
    {
        int p = 0, i = 0, star = -1, mark = 0;
        while (i < s.Length)
        {
            if (p < pat.Length && (pat[p] == '?' || char.ToUpperInvariant(pat[p]) == char.ToUpperInvariant(s[i]))) { p++; i++; }
            else if (p < pat.Length && pat[p] == '*') { star = p++; mark = i; }
            else if (star >= 0) { p = star + 1; i = ++mark; }
            else return false;
        }
        while (p < pat.Length && pat[p] == '*') p++;
        return p == pat.Length;
    }

    /// <summary>First fence (in order) whose rule matches and that doesn't already hold the path; null if none or already placed.</summary>
    public static FenceData? Target(IReadOnlyList<FenceData> fences, string path) =>
        fences.Any(f => f.Items.Contains(path)) ? null : fences.FirstOrDefault(f => f.Rule.Length > 0 && Matches(f.Rule, path));
}
