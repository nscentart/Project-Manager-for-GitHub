using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

public static class DataStore {
    public const string DataVariable = "PM_DATA_DIR", ReposVariable = "PM_REPOS_DIR";
    public const string ArchiveFile = "archive.json", HistoryFile = "history.json", LicenseFile = "license.txt", LanguageFile = "language.txt",
        NoInstallFile = "no-install.txt", TransitionDeclinedFile = "transition-declined.txt", PackagesFolder = "packages", ExportsFolder = "exports", LogFile = "ProjectManager.log";
    public const string BackupSuffix = ".bak", TempSuffix = ".new", BrokenSuffix = ".broken", OldLogSuffix = ".old";
    public const string WorkFolder = "_lavoro";

    public static string DataOverride { get { return Legacy.Variable(DataVariable, Legacy.DataVariable); } }
    public static readonly string Folder = DataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectManager");
    static string FilePath { get { return Path.Combine(Folder, ArchiveFile); } }
    static string LegacyFilePath { get { return Path.Combine(Folder, Legacy.ArchiveFile); } }
    public static string Exports { get { return Path.Combine(Folder, ExportsFolder); } }
    public static string ReposFolder;
    public static readonly string[] DefaultTrustedDomains = { "github.com", "githubusercontent.com" };

    public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 }; }

    public static bool HasArchive() { return File.Exists(FilePath) || File.Exists(LegacyFilePath); }

    public static Archive Load() {
        Archive a = null;
        string source = Legacy.Existing(FilePath, LegacyFilePath);
        try { if (File.Exists(source)) a = Legacy.ArchiveFromJson(File.ReadAllText(source, Encoding.UTF8)); }
        catch (Exception ex) { Log("archive unreadable, copying it aside: " + ex.Message); try { File.Copy(source, FilePath + BrokenSuffix, true); } catch { } }
        if (a == null) a = new Archive();
        if (a.Repo == null) a.Repo = new List<RepoInfo>();
        if (a.Items == null) a.Items = new List<WorkItem>();
        if (a.Decisions == null) a.Decisions = new List<Decision>();
        if (a.TrustedDomains == null || a.TrustedDomains.Count == 0) a.TrustedDomains = DefaultTrustedDomains.ToList();
        if (AccountAccess.Migrate(a) && a.ReposFolder == null) a.ReposFolder = PreviousFolder();
        ReposFolder = a.ReposFolder;
        return a;
    }

    static string PreviousFolder() {
        string c = Legacy.Variable(ReposVariable, Legacy.ReposVariable);
        return string.IsNullOrEmpty(c) ? ProposalFolder() : c;
    }

    public static string ProposalFolder() {
        string c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GitHub");
        return Directory.Exists(c) ? c : "";
    }

    public static void Save(Archive a) {
        Directory.CreateDirectory(Folder);
        string tmp = FilePath + TempSuffix;
        File.WriteAllText(tmp, Json().Serialize(a), new UTF8Encoding(false));
        if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + BackupSuffix);
        else File.Move(tmp, FilePath);
    }

    public static int PendingHandovers(string repo) {
        if (string.IsNullOrEmpty(ReposFolder)) return -1;
        string name = repo.Substring(repo.IndexOf('/') + 1);
        if (name.Length == 0 || name.Trim('.').Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return -1;
        int n = CountPending(Path.Combine(ReposFolder, WorkFolder, name, "handovers", "accepted"), "release");
        return n >= 0 ? n : CountPending(Path.Combine(ReposFolder, name, Legacy.HandoversFolder, Legacy.AcceptedFolder), Legacy.ReleaseKey);
    }

    static int CountPending(string folder, string releaseKey) {
        if (!Directory.Exists(folder)) return -1;
        int n = 0;
        foreach (var f in Directory.GetFiles(folder, "*.json")) {
            try {
                var d = Json().DeserializeObject(File.ReadAllText(f, Encoding.UTF8)) as Dictionary<string, object>;
                object r;
                if (d != null && d.TryGetValue(releaseKey, out r) && r == null) n++;
            } catch (Exception ex) { Log("handover unreadable " + Path.GetFileName(f) + ": " + ex.Message); }
        }
        return n;
    }

    public static string Fingerprint(WorkItem e) {
        using (var h = SHA256.Create()) {
            var b = h.ComputeHash(Encoding.UTF8.GetBytes((e.Title ?? "") + "\n" + (e.Text ?? "")));
            return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant().Substring(0, 32);
        }
    }

    public static void Log(string line) {
        try {
            Directory.CreateDirectory(Folder);
            string f = Path.Combine(Folder, LogFile);
            if (File.Exists(f) && new FileInfo(f).Length > 256 * 1024) { File.Copy(f, f + OldLogSuffix, true); File.Delete(f); }
            File.AppendAllText(f, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " " + line + Environment.NewLine, Encoding.UTF8);
        } catch { }
    }
}
