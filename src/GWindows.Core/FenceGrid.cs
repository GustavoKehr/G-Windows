namespace GWindows.Core;

/// <summary>Adaptive column grid for aligned fences. The number of columns follows the screen width, and the columns share the
/// whole width (so no empty strip is left at the side); fences stack top-down inside their column. All sizes are physical pixels
/// scaled by the monitor's DPI factor <c>s</c> (1.0 = 100 %).</summary>
public static class FenceGrid
{
    public const int FenceW = 450, Gap = 20, Margin = 40, MinW = 380;

    static int M(double s) => (int)Math.Round(Margin * s);
    static int G(double s) => (int)Math.Round(Gap * s);

    /// <summary>Number of columns: as many as fit when each is about <see cref="FenceW"/> wide (the nearest whole number, so a nearly-free
    /// extra column is used and the columns just get slightly narrower).</summary>
    public static int Columns(int areaW, double s = 1)
    {
        var n = Math.Max(1, (int)Math.Round((areaW - 2.0 * M(s) + G(s)) / (FenceW * s + G(s))));
        while (n > 1 && (areaW - 2 * M(s) - (n - 1) * G(s)) / (double)n < MinW * s) n--;   // never squeeze below ~4 icons per row
        return n;
    }

    public static int ColumnWidth(int areaW, double s = 1)
    {
        var n = Columns(areaW, s);
        return Math.Max((int)(160 * s), (areaW - 2 * M(s) - (n - 1) * G(s)) / n);
    }

    static int X0(int right, int areaW, double s) => right - M(s) - (Columns(areaW, s) * ColumnWidth(areaW, s) + (Columns(areaW, s) - 1) * G(s));

    /// <summary>Column whose slot is nearest to a fence centered at centerX.</summary>
    public static int NearestColumn(int right, int areaW, int centerX, double s = 1)
    {
        var cw = ColumnWidth(areaW, s);
        var c = (int)Math.Round((centerX - X0(right, areaW, s) - cw / 2.0) / (cw + G(s)));
        return Math.Clamp(c, 0, Columns(areaW, s) - 1);
    }

    /// <summary>Slots for fences in list order. A fence goes to its preferred column; if that would run past the bottom
    /// it moves to the column with the most room, so nothing ends up off screen while another column is free.</summary>
    public static List<(int x, int y, int w, int col)> Layout(int left, int top, int right, int bottom, IReadOnlyList<(int col, int h)> fences, double s = 1)
    {
        var areaW = right - left; var cols = Columns(areaW, s); var cw = ColumnWidth(areaW, s); var x0 = X0(right, areaW, s);
        var y = new int[cols]; Array.Fill(y, top + M(s));
        var result = new List<(int, int, int, int)>();
        foreach (var (pref, h) in fences)
        {
            var c = Math.Clamp(pref, 0, cols - 1);
            if (y[c] + h > bottom - M(s))
                for (var i = 0; i < cols; i++) if (y[i] < y[c] && y[i] + h <= bottom - M(s)) { c = i; }
            result.Add((x0 + c * (cw + G(s)), y[c], cw, c));
            y[c] += h + G(s);
        }
        return result;
    }
}