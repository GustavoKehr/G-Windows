namespace GWindows.Core;

/// <summary>Adaptive column grid for aligned fences. Columns are anchored to the right edge of the work area;
/// the number of columns follows the screen width, and fences stack top-down inside their column.</summary>
public static class FenceGrid
{
    public const int FenceW = 450, Gap = 20, Margin = 40;

    public static int Columns(int areaW) => Math.Max(1, (areaW - 2 * Margin + Gap) / (FenceW + Gap));

    public static int ColumnWidth(int areaW) => Columns(areaW) == 1 ? Math.Min(FenceW, areaW - 2 * Margin) : FenceW;

    static int X0(int right, int areaW) => right - Margin - (Columns(areaW) * ColumnWidth(areaW) + (Columns(areaW) - 1) * Gap);

    /// <summary>Column whose slot is nearest to a fence centered at centerX.</summary>
    public static int NearestColumn(int right, int areaW, int centerX)
    {
        var cw = ColumnWidth(areaW);
        var c = (int)Math.Round((centerX - X0(right, areaW) - cw / 2.0) / (cw + Gap));
        return Math.Clamp(c, 0, Columns(areaW) - 1);
    }

    /// <summary>Slots for fences in list order. A fence goes to its preferred column; if that would run past the bottom
    /// it moves to the column with the most room, so nothing ends up off screen while another column is free.</summary>
    public static List<(int x, int y, int w, int col)> Layout(int left, int top, int right, int bottom, IReadOnlyList<(int col, int h)> fences)
    {
        var areaW = right - left; var cols = Columns(areaW); var cw = ColumnWidth(areaW); var x0 = X0(right, areaW);
        var y = new int[cols]; Array.Fill(y, top + Margin);
        var result = new List<(int, int, int, int)>();
        foreach (var (pref, h) in fences)
        {
            var c = Math.Clamp(pref, 0, cols - 1);
            if (y[c] + h > bottom - Margin)
                for (var i = 0; i < cols; i++) if (y[i] < y[c] && y[i] + h <= bottom - Margin) { c = i; }
            result.Add((x0 + c * (cw + Gap), y[c], cw, c));
            y[c] += h + Gap;
        }
        return result;
    }
}