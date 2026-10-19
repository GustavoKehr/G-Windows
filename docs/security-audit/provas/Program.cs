// Programas de comprovação da auditoria (A-01 a A-04). Rodar: dotnet run -c Release --project docs/security-audit/provas
// Não faz parte da solução do produto; usa o carregador e as regras reais (GWindows.Core).
using System.Diagnostics;
using GWindows.Core;

var tmp = Path.Combine(Path.GetTempPath(), "audit_layout.json");
File.WriteAllText(tmp, "{\"Fences\":null}");
var l = LayoutStore.Load(tmp); Console.WriteLine($"[A-02] Fences:null aceito -> Layout.Fences is null: {l.Fences is null}");
File.WriteAllText(tmp, "{\"Fences\":[{\"Items\":null,\"Name\":null,\"Excluded\":null}]}");
l = LayoutStore.Load(tmp); Console.WriteLine($"[A-02] Items/Name/Excluded nulos aceitos: {l.Fences[0].Items is null}/{l.Fences[0].Name is null}/{l.Fences[0].Excluded is null}");
File.WriteAllText(tmp, "{\"Fences\":[{\"W\":2147483647,\"H\":-5,\"X\":2147483647,\"Portal\":\"\\\\\\\\attacker.example\\\\share\",\"Items\":[\"\\\\\\\\attacker.example\\\\share\\\\x.exe\"]}]}");
l = LayoutStore.Load(tmp); var f = l.Fences[0];
Console.WriteLine($"[A-01/A-02] sem validação: W={f.W} H={f.H} X={f.X} Portal={f.Portal} Items[0]={f.Items[0]}");
try { Rules.Target(new List<FenceData> { new FenceData { Items = null, Rule = "*" } }, "C:\\x\\a.txt"); }
catch (Exception e) { Console.WriteLine($"[A-02] Rules.Target com Items=null lança {e.GetType().Name}"); }

foreach (var n in new[] { 20, 24, 26, 28, 30 })
{
    var sw = Stopwatch.StartNew();
    var t = Task.Run(() => Rules.Matches("*a*a*a*a*a*a*a*a*c", "C:\\x\\" + new string('a', n) + "b"));
    if (t.Wait(15000)) Console.WriteLine($"[A-03] ReDoS n={n}: {sw.ElapsedMilliseconds} ms"); else { Console.WriteLine($"[A-03] n={n}: >15000 ms"); break; }
}

var p = Path.Combine(Path.GetTempPath(), "audit_oneline.txt");
using (var fs = File.Create(p)) { var buf = new byte[1 << 20]; Array.Fill(buf, (byte)'a'); for (int i = 0; i < 300; i++) fs.Write(buf); }
var proc = Process.GetCurrentProcess(); var sw2 = Stopwatch.StartNew();
var s = string.Join("\n", File.ReadLines(p).Take(6).Select(x => x.Length > 80 ? x[..80] : x)) + "\n\n" + p;   // expressão de FenceWindow.cs:378
proc.Refresh(); Console.WriteLine($"[A-04] arquivo de 300 MB em linha única: {sw2.ElapsedMilliseconds} ms, pico de working set {proc.PeakWorkingSet64 / 1048576} MB");
File.Delete(p);
