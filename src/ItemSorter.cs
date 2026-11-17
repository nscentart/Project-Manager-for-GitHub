using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static class ItemSorter {
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    public static readonly string[] Categories = { "log", "bug", "request", "question", "review", "pull request", "other" };

    static readonly Regex LogTitle = new Regex(@"^Log degli errori, (.+?) v?(\d+(?:\.\d+)+)", O);

    static readonly Dictionary<string, List<Reason>> AlreadySeen = new Dictionary<string, List<Reason>>();

    static List<Reason> Examine(WorkItem e, string fingerprint, IEnumerable<string> trusted) {
        string key = Language.Code + "\n" + fingerprint + "\n" + string.Join(",", trusted ?? new string[0]);
        lock (AlreadySeen) {
            List<Reason> m;
            if (AlreadySeen.TryGetValue(key, out m)) return m;
        }
        var newItems = Screener.Examine(e.Title, e.Text, trusted);
        lock (AlreadySeen) {
            if (AlreadySeen.Count > 5000) AlreadySeen.Clear();
            AlreadySeen[key] = newItems;
        }
        return newItems;
    }

    public static List<Entry> Entries(Archive a) {
        var decisions = new Dictionary<string, Decision>();
        foreach (var d in a.Decisions) decisions[d.Key] = d;
        var parents = new Dictionary<string, WorkItem>();
        foreach (var e in a.Items) if (e.Type != "comment") parents[e.Repo + "#" + e.Number] = e;
        var entries = new List<Entry>();
        foreach (var e in a.Items) {
            var v = new Entry { E = e, Fingerprint = DataStore.Fingerprint(e) };
            v.Reasons = Examine(e, v.Fingerprint, a.TrustedDomains);
            WorkItem parent;
            parents.TryGetValue(e.Repo + "#" + e.Number, out parent);
            v.Category = Category(e, parent);
            Decision d;
            decisions.TryGetValue(e.Key, out d);
            bool valid = d != null && d.Fingerprint == v.Fingerprint && !string.IsNullOrEmpty(d.State);
            v.D = d != null && d.Fingerprint == v.Fingerprint ? d : null;
            if (valid) v.State = d.State == "approved" ? "work" : d.State;
            else v.State = v.Reasons.Count > 0 ? "quarantine" : "work";
            entries.Add(v);
        }
        return entries;
    }

    public static string CategoryName(string c) { return Language.T("category." + c.Replace(' ', '_')); }
    public static string StateName(string s) { return Language.T("state." + s); }
    public static string TypeName(string t) { return Language.T("type." + t); }

    public static string TitleOf(WorkItem e, Archive a) {
        if (e.Type != "comment") return e.Title;
        var m = a.Items.FirstOrDefault(x => x.Type != "comment" && x.Repo == e.Repo && x.Number == e.Number);
        return Language.T("triage.comment_on", "part", (m == null ? "#" + e.Number : m.Title));
    }

    public static string Category(WorkItem e, WorkItem parent) {
        var rif = e.Type == "comment" && parent != null ? parent : e;
        if (rif.Type == "pull") return "pull request";
        string et = (rif.Labels ?? "").ToLowerInvariant();
        if (LogTitle.IsMatch(rif.Title ?? "") || et.Split(',').Contains("log")) return "log";
        if (Regex.IsMatch(et, @"\bbug\b")) return "bug";
        if (Regex.IsMatch(et, @"\b(enhancement|feature|richiesta)\b")) return "request";
        if (Regex.IsMatch(et, @"\b(question|domanda)\b")) return "question";
        string t = ((rif.Title ?? "") + "\n" + (e.Text ?? "")).ToLowerInvariant();
        if (Regex.IsMatch(t, @"\b(errore|error|crash\w*|bug|non funziona|non va|non parte|si blocca|bloccat\w*|si chiude|eccezione|exception|broken|doesn'?t work|not working|freez\w*|sbagliat\w*)\b")) return "bug";
        if (Regex.IsMatch(t, @"\b(vorrei|sarebbe bello|si potrebbe|potresti|aggiung\w*|richiesta|proposta|suggeriment\w*|feature|would be nice|please add|could you add|request)\b")) return "request";
        if (Regex.IsMatch(t, @"\?\s*$|\b(come si (fa|può|imposta|installa|usa|toglie|cambia)|come faccio|how do i|how to|is it possible|si può)\b")) return "question";
        if (Regex.IsMatch(t, @"\b(grazie|ottimo|bellissim\w*|fantastic\w*|perfett\w*|funziona benissimo|thanks|thank you|great|love it|awesome)\b")) return "review";
        return "other";
    }

    public static string ParseVersion(WorkItem e) {
        var m = LogTitle.Match(e.Title ?? "");
        if (m.Success) return m.Groups[2].Value;
        m = Regex.Match(e.Text ?? "", @"^\s*`*\s*\S+ v?(\d+(?:\.\d+)+)", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : "?";
    }

    public static string Signature(string line) {
        string s = line;
        s = Regex.Replace(s, @"[a-zA-Z]:\\[^\s:;,]+", Language.Fixed("signature.path"));
        s = Regex.Replace(s, "\"[^\"]*\"|«[^»]*»|'[^']{2,}'", "\"...\"");
        s = Regex.Replace(s, @"0x[0-9a-fA-F]+", "0x#");
        s = Regex.Replace(s, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", "<guid>");
        s = Regex.Replace(s, @"\d+([.,]\d+)*", "#");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 160 ? s.Substring(0, 160) : s;
    }

    static readonly Regex LogLine = new Regex(@"^\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}) (.+?)\s*$", RegexOptions.Multiline);

    public static List<RecurringError> Recurring(IEnumerable<Entry> entries) {
        var groups = new Dictionary<string, RecurringError>();
        foreach (var v in entries.Where(x => x.Category == "log" && x.State != "quarantine" && x.State != "rejected")) {
            string ver = ParseVersion(v.E);
            foreach (Match m in LogLine.Matches((v.E.Text ?? "").Replace("\r\n", "\n"))) {
                string signature = Signature(m.Groups[2].Value);
                string k = v.E.Repo + "\n" + signature;
                RecurringError g;
                if (!groups.TryGetValue(k, out g)) groups[k] = g = new RecurringError { Repo = v.E.Repo, Signature = signature, Example = m.Groups[2].Value.Trim() };
                g.Times++;
                g.Reports.Add(v.E.Key);
                g.Releases.Add(ver);
                DateTime t;
                if (DateTime.TryParseExact(m.Groups[1].Value, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) {
                    if (t < g.First) g.First = t;
                    if (t > g.Last) g.Last = t;
                }
            }
        }
        return groups.Values.OrderByDescending(g => g.Reports.Count).ThenByDescending(g => g.Times).ThenByDescending(g => g.Last).ToList();
    }
}
