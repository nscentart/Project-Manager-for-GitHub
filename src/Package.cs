using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class Package {
    static string Fence(string s) {
        int max = 2;
        foreach (Match m in Regex.Matches(s ?? "", "`+")) max = Math.Max(max, m.Length);
        return new string('`', max + 1);
    }

    static string Data(string iso) {
        DateTime t;
        return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out t) ? t.ToLocalTime().ToString("dd/MM/yyyy") : "";
    }

    public static bool GoesToClipboard(RepoInfo r) { return r != null && !r.Private; }

    static string Clean(string t, ref int covered) {
        int n;
        string m = Screener.Mask(t ?? "", out n);
        covered += n;
        return Screener.MakeVisible(m);
    }

    public static string Text(Archive a, string repo, List<Entry> entries, List<RecurringError> errors) {
        int covered = 0;
        var sb = new StringBuilder();
        var repoErrors = errors.Where(g => g.Repo == repo).Take(20).ToList();
        if (repoErrors.Count > 0) {
            sb.AppendLine(Language.Fixed("package.errors_title"));
            sb.AppendLine();
            sb.AppendLine(Language.Fixed("package.errors_header"));
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var g in repoErrors)
                sb.AppendLine("| " + g.Reports.Count + " | " + g.Times + " | " + string.Join(", ", g.Releases.OrderBy(x => x)) + " | "
                              + (g.Last == DateTime.MinValue ? "" : g.Last.ToString("dd/MM/yyyy")) + " | `" + Clean(g.Signature, ref covered).Replace("`", "'").Replace("|", "/").Replace("\r", " ").Replace("\n", " ") + "` |");
            sb.AppendLine();
        }
        var suspicious = new HashSet<int>(entries.Where(v => v.E.Repo == repo && v.E.Type != "comment" && v.State == "quarantine").Select(v => v.E.Number));
        Func<Entry, bool> toLeave = v => v.E.Type == "comment" && suspicious.Contains(v.E.Number);
        int left = entries.Count(v => v.E.Repo == repo && v.State == "work" && toLeave(v));
        foreach (var cat in ItemSorter.Categories) {
            var categoryEntries = entries.Where(v => v.E.Repo == repo && v.State == "work" && v.Category == cat && !toLeave(v)).OrderBy(v => v.E.Number).ThenBy(v => v.E.Created).ToList();
            if (categoryEntries.Count == 0) continue;
            string categoryName = Language.Fixed("category." + cat.Replace(' ', '_'));
            sb.AppendLine("## " + char.ToUpper(categoryName[0]) + categoryName.Substring(1) + " (" + categoryEntries.Count + ")");
            sb.AppendLine();
            foreach (var v in categoryEntries) {
                string typeName = Language.Fixed("type." + v.E.Type);
                sb.AppendLine("### #" + v.E.Number + " " + typeName);
                sb.AppendLine();
                sb.AppendLine("- " + Language.Fixed("package.item", "type", typeName, "author", Screener.MakeVisible(v.E.Author ?? "").Replace("\r", " ").Replace("\n", " "), "date", Data(v.E.Created), "closed", v.E.State == "closed" ? Language.Fixed("package.closed") : "", "url", v.E.Url));
                sb.AppendLine();
                string body = Language.Fixed("package.title", "title", (ItemSorter.TitleOf(v.E, a) ?? "").Replace("\r", " ").Replace("\n", " ")) + "\n\n";
                if (v.D != null && !string.IsNullOrEmpty(v.D.Summary) && v.D.SummaryFingerprint == v.Fingerprint)
                    body = Language.Fixed("package.summary", "model", v.D.SummaryModel, "summary", v.D.Summary.Replace("\r", " ").Replace("\n", " ")) + "\n\n" + body;
                body = Clean(body + (v.E.Text ?? "").Replace("\r\n", "\n"), ref covered);
                string r = Fence(body);
                sb.AppendLine(r + "text");
                sb.AppendLine(body.TrimEnd());
                sb.AppendLine(r);
                sb.AppendLine();
            }
        }
        var head = new StringBuilder();
        head.AppendLine(Language.Fixed("package.heading", "repo", repo, "date", DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
        head.AppendLine();
        head.AppendLine(Language.Fixed("package.intro_1"));
        head.AppendLine(Language.Fixed("package.intro_2"));
        head.AppendLine(Language.Fixed("package.intro_3"));
        head.AppendLine(Language.Fixed("package.intro_4"));
        if (covered > 0) head.AppendLine(Language.Fixed("package.covered." + (covered == 1 ? "one" : "other"), "count", covered));
        if (left > 0) head.AppendLine(Language.Fixed("package.left." + (left == 1 ? "one" : "other"), "count", left));
        head.AppendLine();
        return head.ToString() + sb;
    }

    public static string FileName(string repo) { return Path.Combine(DataStore.Packages, repo.Replace('/', '_') + "_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".md"); }
    public static bool NameGenerated(string name) { return Regex.IsMatch(name, @"_\d{4}-\d{2}-\d{2}_\d{4}\.md$"); }

    public static string Write(string repo, string text) {
        Directory.CreateDirectory(DataStore.Packages);
        string f = FileName(repo);
        File.WriteAllText(f, text, new UTF8Encoding(false));
        return f;
    }
}
