using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class TextExport {
    const string Extension = ".txt";
    static readonly string Divider = new string('-', 60);

    static string Day(string iso) {
        DateTime t;
        return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out t) ? t.ToLocalTime().ToString("dd/MM/yyyy") : "";
    }

    static string Clean(string t, ref int covered) {
        int n;
        string m = Screener.Mask(t ?? "", out n);
        covered += n;
        return Screener.MakeVisible(m);
    }

    static string OneLine(string t) { return (t ?? "").Replace("\r", " ").Replace("\n", " "); }

    public static string Text(Archive a, string repo, List<Entry> entries, List<RecurringError> errors) {
        int covered = 0;
        var sb = new StringBuilder();
        var repoErrors = errors.Where(g => g.Repo == repo).Take(20).ToList();
        if (repoErrors.Count > 0) {
            sb.AppendLine(Language.T("export.errors_title"));
            sb.AppendLine(Divider);
            sb.AppendLine();
            foreach (var g in repoErrors) {
                sb.AppendLine(Language.T("export.error_counts", "reports", g.Reports.Count, "times", g.Times, "versions", string.Join(", ", g.Releases.OrderBy(x => x)),
                                         "last", g.Last == DateTime.MinValue ? "" : g.Last.ToString("dd/MM/yyyy")));
                sb.AppendLine(Language.T("export.error_line", "error", OneLine(Clean(g.Signature, ref covered))));
                sb.AppendLine();
            }
        }
        var suspicious = new HashSet<int>(entries.Where(v => v.E.Repo == repo && v.E.Type != "comment" && v.State == "quarantine").Select(v => v.E.Number));
        Func<Entry, bool> toLeave = v => v.E.Type == "comment" && suspicious.Contains(v.E.Number);
        int left = entries.Count(v => v.E.Repo == repo && v.State == "work" && toLeave(v));
        int shown = 0;
        foreach (var cat in ItemSorter.Categories) {
            var categoryEntries = entries.Where(v => v.E.Repo == repo && v.State == "work" && v.Category == cat && !toLeave(v)).OrderBy(v => v.E.Number).ThenBy(v => v.E.Created).ToList();
            foreach (var v in categoryEntries) {
                shown++;
                sb.AppendLine("#" + v.E.Number + " (" + ItemSorter.TypeName(v.E.Type) + ")");
                sb.AppendLine(Language.T("export.line_kind", "value", ItemSorter.CategoryName(cat)));
                sb.AppendLine(Language.T("export.line_title", "value", OneLine(Clean(ItemSorter.TitleOf(v.E, a), ref covered))));
                sb.AppendLine(Language.T("export.line_author", "value", OneLine(Screener.MakeVisible(v.E.Author ?? ""))));
                sb.AppendLine(Language.T("export.line_date", "value", Day(v.E.Created), "closed", v.E.State == "closed" ? Language.T("export.closed") : ""));
                sb.AppendLine(Language.T("export.line_link", "value", OneLine(Screener.MakeVisible(v.E.Url ?? ""))));
                sb.AppendLine();
                sb.AppendLine(Clean((v.E.Text ?? "").Replace("\r\n", "\n"), ref covered).TrimEnd());
                sb.AppendLine();
                sb.AppendLine(Divider);
                sb.AppendLine();
            }
        }
        var head = new StringBuilder();
        head.AppendLine(Language.T("export.heading", "repo", repo, "date", DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
        head.AppendLine();
        if (shown == 0 && repoErrors.Count == 0) head.AppendLine(Language.T("export.nothing"));
        if (covered > 0) head.AppendLine(Language.N("export.covered", covered));
        if (left > 0) head.AppendLine(Language.N("export.left", left));
        head.AppendLine();
        return head.ToString() + sb;
    }

    public static string FileName(string repo) { return Path.Combine(DataStore.Exports, repo.Replace('/', '_') + "_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + Extension); }
    public static bool NameGenerated(string name) { return Regex.IsMatch(name, @"_\d{4}-\d{2}-\d{2}_\d{4}\.txt$"); }

    public static string Write(string repo, string text) {
        Directory.CreateDirectory(DataStore.Exports);
        string f = FileName(repo);
        File.WriteAllText(f, text, new UTF8Encoding(false));
        return f;
    }
}
