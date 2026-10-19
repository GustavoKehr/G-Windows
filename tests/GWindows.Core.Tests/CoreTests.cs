using GWindows.Core;
using Xunit;

public class CoreTests
{
    [Fact]
    public void Spring_converges_and_settles()
    {
        var s = new Spring(1) { Target = 1.08 };
        for (var i = 0; i < 120 && !s.Settled; i++) s.Step(1.0 / 60);
        Assert.True(s.Settled);
        Assert.Equal(1.08, s.Value);
    }

    [Fact]
    public void Spring_retarget_keeps_velocity()
    {
        var s = new Spring(1) { Target = 1.08 };
        s.Step(0.05);
        var v = s.Velocity;
        s.Target = 1; // interrupt
        Assert.Equal(v, s.Velocity);
        Assert.NotEqual(0, v);
    }

    [Fact]
    public void Store_roundtrip_and_backup_fallback()
    {
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "layout.json");
        var l = new LayoutFile { Fences = [new FenceData { Name = "A", Items = ["C:/x.txt"] }] };
        LayoutStore.Save(p, l);
        LayoutStore.Save(p, l); // creates .bak
        Assert.Equal("A", LayoutStore.Load(p).Fences[0].Name);
        File.WriteAllText(p, "{corrupt");
        Assert.Equal("A", LayoutStore.Load(p).Fences[0].Name);
    }

    [Fact]
    public void Rules_match_and_pick_first_unplaced_fence()
    {
        Assert.True(Rules.Matches("*.pdf; invoice?", "C:/d/A.PDF"));
        Assert.True(Rules.Matches("*.pdf; invoice?", "C:/d/invoice1"));
        Assert.False(Rules.Matches("*.pdf", "C:/d/a.pdf.exe"));
        var a = new FenceData { Rule = "*.pdf" }; var b = new FenceData { Rule = "*.*" };
        Assert.Same(a, Rules.Target([a, b], "C:/d/x.pdf"));
        Assert.Same(b, Rules.Target([a, b], "C:/d/x.txt"));
        a.Items.Add("C:/d/x.pdf");
        Assert.Null(Rules.Target([a, b], "C:/d/x.pdf"));
    }

    [Fact]
    public void Smart_recent_and_stale()
    {
        var d = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        var old = Path.Combine(d, "old.txt"); var fresh = Path.Combine(d, "fresh.txt");
        File.WriteAllText(old, "x"); File.WriteAllText(fresh, "x");
        File.SetLastWriteTime(old, DateTime.Now.AddDays(-200)); File.SetLastAccessTime(old, DateTime.Now.AddDays(-200));
        Assert.Equal([fresh, old], Smart.Resolve("recent", [old, fresh], DateTime.Now));
        Assert.Equal([old], Smart.Resolve("stale", [old, fresh], DateTime.Now));
        Assert.Empty(Smart.Resolve("large", [old, fresh], DateTime.Now));
    }

    [Fact]
    public void Grid_adapts_to_screen_width_and_stacks()
    {
        Assert.Equal(7, FenceGrid.Columns(3440)); Assert.Equal(3, FenceGrid.Columns(1920)); Assert.Equal(1, FenceGrid.Columns(700));
        Assert.Equal(6, FenceGrid.NearestColumn(3440, 3440, 3440 - 40 - 225)); // right-most slot
        var l = FenceGrid.Layout(0, 0, 1920, 1080, [(0, 300), (0, 300), (2, 100)]);
        Assert.Equal(l[0].x, l[1].x); Assert.True(l[1].y > l[0].y);   // same column stacks downward
        Assert.True(l[2].x > l[0].x);                                   // right column is further right
        var narrow = FenceGrid.Layout(0, 0, 400, 600, [(5, 100)]);
        Assert.Equal(0, narrow[0].col); Assert.Equal(320, narrow[0].w);  // one column, width shrinks to fit
        var full = FenceGrid.Layout(0, 0, 1920, 500, [(0, 300), (0, 300)]);   // 2nd would run off screen -> another column
        Assert.NotEqual(full[0].col, full[1].col);
    }

    [Fact]
    public void Rules_pathological_pattern_is_fast()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(Rules.Matches("*a*a*a*a*a*a*a*a*c", "C:/x/" + new string('a', 250) + "b"));
        Assert.True(sw.ElapsedMilliseconds < 50);
        Assert.True(Rules.Matches("*.PDF;inv?ice*", "C:/x/Invoice-1.txt"));
    }

    [Fact]
    public void Load_normalizes_hostile_layout_and_keeps_network_paths()
    {
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(p, "{\"Fences\":[{\"Items\":null,\"Name\":null,\"W\":2147483647,\"H\":-5,\"X\":2147483647,\"Portal\":\"\\\\\\\\srv\\\\share\"},null,{\"Items\":[\"\\\\\\\\srv\\\\share\\\\a.txt\",\"\",null]}],\"Profiles\":{\"a\":null}}");
        var l = LayoutStore.Load(p);
        Assert.Equal(2, l.Fences.Count);
        Assert.NotNull(l.Fences[0].Items); Assert.Equal("Cerca", l.Fences[0].Name);
        Assert.Equal(3000, l.Fences[0].W); Assert.Equal(60, l.Fences[0].H); Assert.Equal(30000, l.Fences[0].X);
        Assert.Equal("\\\\srv\\share", l.Fences[0].Portal);                    // UNC kept on purpose
        Assert.Equal(["\\\\srv\\share\\a.txt"], l.Fences[1].Items);
        Assert.Empty(l.Profiles["a"]);
        File.WriteAllText(p, "{\"Fences\":null}"); Assert.NotNull(LayoutStore.Load(p).Fences);
        File.WriteAllText(p, "not json"); Assert.Empty(LayoutStore.Load(p).Fences);
    }

    [Fact]
    public void Load_waits_for_a_locked_file_and_flags_total_failure()
    {
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(p, "{\"Fences\":[{\"Name\":\"A\"}]}");
        var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None);   // exclusive lock, like an antivirus scan at logon
        var t = Task.Run(() => { Thread.Sleep(600); fs.Dispose(); });
        var l = LayoutStore.Load(p, 20, 100);
        t.Wait();
        Assert.Equal("A", l.Fences[0].Name); Assert.False(LayoutStore.LastLoadFailed);

        using var fs2 = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.Empty(LayoutStore.Load(p, 2, 50).Fences); Assert.True(LayoutStore.LastLoadFailed);   // still locked: reported, not silently replaced
    }
}