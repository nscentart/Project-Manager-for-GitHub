using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class History {
    static string FilePath { get { return Path.Combine(DataStore.Folder, DataStore.HistoryFile); } }
    static string LegacyFilePath { get { return Path.Combine(DataStore.Folder, Legacy.HistoryFile); } }

    static bool closed;

    static HistoryArchive Read() {
        string source = Legacy.Existing(FilePath, LegacyFilePath);
        if (!File.Exists(source)) return null;
        var s = Legacy.HistoryFromJson(File.ReadAllText(source, Encoding.UTF8));
        if (s == null || s.Repo == null) throw new FormatException("empty");
        return s;
    }

    static void PartFrom(Exception ex) {
        DataStore.Log("history unreadable, copying it aside: " + ex.Message);
        try { File.Copy(Legacy.Existing(FilePath, LegacyFilePath), FilePath + DataStore.BrokenSuffix, true); } catch { }
    }

    public static HistoryArchive Load() {
        HistoryArchive s = null;
        closed = false;
        try { s = Read(); }
        catch (IOException ex) { closed = true; DataStore.Log("history locked, not writing it for now: " + ex.Message); }
        catch (UnauthorizedAccessException ex) { closed = true; DataStore.Log("history locked, not writing it for now: " + ex.Message); }
        catch (Exception ex) { PartFrom(ex); }
        return Sort(s ?? new HistoryArchive());
    }

    static void Resume(HistoryArchive s, HistoryArchive file) {
        foreach (var r in file.Repo) {
            string k = Key(s, r.Key) ?? r.Key;
            List<HistoryDay> days;
            if (!s.Repo.TryGetValue(k, out days)) days = new List<HistoryDay>();
            foreach (var v in r.Value) {
                var g = Day(days, v.Day);
                if (g.Views < 0) g.Views = v.Views;
                if (g.Uniques < 0) g.Uniques = v.Uniques;
                if (g.Clones < 0) g.Clones = v.Clones;
                if (g.Downloads < 0) g.Downloads = v.Downloads;
            }
            days.Sort((a, b) => string.CompareOrdinal(a.Day, b.Day));
            s.Repo[k] = days;
        }
    }

    static HistoryArchive Sort(HistoryArchive s) {
        var clean = new HistoryArchive();
        foreach (var r in s.Repo) {
            if (r.Value == null) continue;
            var seen = new Dictionary<string, HistoryDay>();
            foreach (var g in r.Value) if (g != null && GoodDate(g.Day)) seen[g.Day] = g;
            clean.Repo[r.Key] = seen.Values.OrderBy(x => x.Day, StringComparer.Ordinal).ToList();
        }
        return clean;
    }

    static bool GoodDate(string g) {
        DateTime d;
        return g != null && Regex.IsMatch(g, @"\A\d{4}-\d{2}-\d{2}\z") && DateTime.TryParseExact(g, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d);
    }

    public static void Save(HistoryArchive s) {
        if (closed) {
            HistoryArchive file = null;
            try { file = Read(); }
            catch (IOException ex) { DataStore.Log("history still locked, not writing it: " + ex.Message); return; }
            catch (UnauthorizedAccessException ex) { DataStore.Log("history still locked, not writing it: " + ex.Message); return; }
            catch (Exception ex) { PartFrom(ex); }
            closed = false;
            if (file != null) Resume(s, Sort(file));
        }
        Directory.CreateDirectory(DataStore.Folder);
        string tmp = FilePath + DataStore.TempSuffix;
        File.WriteAllText(tmp, DataStore.Json().Serialize(s), new UTF8Encoding(false));
        if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + DataStore.BackupSuffix);
        else File.Move(tmp, FilePath);
    }

    static string Key(HistoryArchive s, string repo) {
        return s.Repo.Keys.FirstOrDefault(k => string.Equals(k, repo, StringComparison.OrdinalIgnoreCase));
    }

    static HistoryDay Day(List<HistoryDay> days, string data) {
        var g = days.FirstOrDefault(x => x.Day == data);
        if (g == null) { g = new HistoryDay { Day = data }; days.Add(g); }
        return g;
    }

    public static void Join(HistoryArchive s, string repo, RepoStats n, string today) {
        if (n == null) return;
        string k = Key(s, repo) ?? repo;
        List<HistoryDay> days;
        if (!s.Repo.TryGetValue(k, out days)) days = new List<HistoryDay>();
        if (n.ViewsRead && n.ViewsError == null && n.ViewDays != null)
            foreach (var g in n.ViewDays) { var x = Day(days, g.Day); x.Views = g.Count; x.Uniques = g.Uniques; }
        if (n.ClonesRead && n.ClonesError == null && n.CloneDays != null)
            foreach (var g in n.CloneDays) Day(days, g.Day).Clones = g.Count;
        if (n.DownloadsRead && n.DownloadsError == null) Day(days, today).Downloads = n.Downloads;
        if (days.Count == 0) return;
        days.Sort((a, b) => string.CompareOrdinal(a.Day, b.Day));
        s.Repo[k] = days;
    }

    public static List<TrafficDay> DownloadForDay(IEnumerable<HistoryDay> days) {
        var output = new List<TrafficDay>();
        long before = -1;
        foreach (var g in days.Where(x => x.Downloads >= 0).OrderBy(x => x.Day, StringComparer.Ordinal)) {
            if (before >= 0) output.Add(new TrafficDay { Day = g.Day, Count = Math.Max(0, g.Downloads - before) });
            before = g.Downloads;
        }
        return output;
    }

    public static void Rename(HistoryArchive s, string oldName, string newName) {
        string k = Key(s, oldName);
        if (k == null || string.Equals(k, newName, StringComparison.OrdinalIgnoreCase)) return;
        var days = s.Repo[k];
        s.Repo.Remove(k);
        string already = Key(s, newName);
        if (already != null) {
            foreach (var g in s.Repo[already]) { days.RemoveAll(x => x.Day == g.Day); days.Add(g); }
            s.Repo.Remove(already);
        }
        days.Sort((a, b) => string.CompareOrdinal(a.Day, b.Day));
        s.Repo[newName] = days;
    }

    public static int Remove(HistoryArchive s, string repo) {
        string k = Key(s, repo);
        if (k == null) return 0;
        int n = s.Repo[k].Count;
        s.Repo.Remove(k);
        return n;
    }

    public static int DaysHowMany(HistoryArchive s, string repo) {
        string k = Key(s, repo);
        return k == null ? 0 : s.Repo[k].Count;
    }

    public static List<HistoryDay> Copy(HistoryArchive s, string repo) {
        string k = Key(s, repo);
        if (k == null) return new List<HistoryDay>();
        return s.Repo[k].Select(g => new HistoryDay { Day = g.Day, Views = g.Views, Uniques = g.Uniques, Clones = g.Clones, Downloads = g.Downloads }).ToList();
    }
}
