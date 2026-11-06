using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static partial class Program {
    static int RunRemoveTests(string[] a) {
        if (!a[3].Contains(Installer.TestMark) || !a[4].StartsWith(Installer.TestMark)) return 2;
        Installer.Remove(new Locations { Folder = a[1], MenuStart = a[2], Key = a[3] }, a[5] == "si", a[4]);
        return 0;
    }

    static int RunRepoTests(string folder, string result) {
        var k = Writer.Prepare(folder, null);
        var sb = new StringBuilder();
        foreach (var r in k.Findings) sb.AppendLine("FAIL " + r);
        foreach (var r in k.Rejected) sb.AppendLine("FAIL " + r);
        string withoutPath = "Process.Start(" + "\"explorer";
        var bare = Directory.GetFiles(Path.Combine(folder, "src"), "*.cs", SearchOption.AllDirectories).Where(x => File.ReadAllText(x).Contains(withoutPath)).ToList();
        foreach (var x in bare) sb.AppendLine("FAIL " + Path.GetFileName(x) + ": calls explorer without the full path");
        sb.AppendLine(k.File.Count + " files checked, " + (k.Findings.Count + k.Rejected.Count + bare.Count) + " stopped");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return k.Blocked || k.File.Count == 0 || bare.Count > 0 ? 1 : 0;
    }

    static int RunScreenerTests(string folder, string result) {
        var sb = new StringBuilder();
        int wrong = 0, tot = 0;
        var trusted = DataStore.DefaultTrustedDomains;
        foreach (var f in Directory.GetFiles(folder, "*.txt").OrderBy(x => x)) {
            var lines = File.ReadAllText(f, Encoding.UTF8).Replace("\r\n", "\n").Split(new[] { '\n' }, 2);
            var before = lines[0].Trim().Split(' ');
            bool expected = before.Length > 1 && before[1] == "suspect";
            string rule = before.Length > 2 ? before[2] : null;
            var reasons = Screener.Examine(rule != null ? "" : Path.GetFileNameWithoutExtension(f), lines.Length > 1 ? lines[1] : "", trusted);
            bool ok = (reasons.Count > 0) == expected && (rule == null || reasons.Any(x => x.Code == rule));
            tot++; if (!ok) wrong++;
            sb.AppendLine((ok ? "OK      " : "FAIL ") + Path.GetFileName(f) + " (expected " + (expected ? "suspect" : "clean") + ")");
            foreach (var m in reasons) sb.AppendLine("          " + m.Code + ": " + m);
        }
        sb.AppendLine(tot - wrong + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return wrong == 0 && tot > 0 ? 0 : 1;
    }
}
