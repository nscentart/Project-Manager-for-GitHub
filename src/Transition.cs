using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

public static class Transition {
    public static readonly OldInstall Real = new OldInstall {
        Program = new Locations {
            Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProjectMonitor"),
            MenuStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Monitor.lnk"),
            Key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectMonitor", ExeName = "ProjectMonitor.exe" },
        DataStore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMonitor"),
        CredentialStorePrefix = "ProjectMonitor/",
        Mutex = "ProjectMonitor-una-copia" };

    const string OldLogFile = "ProjectMonitor.log";

    static readonly string[][] DataFile = {
        new[] { Legacy.ArchiveFile, DataStore.ArchiveFile, Legacy.ArchiveFile },
        new[] { Legacy.ArchiveFile + Legacy.BackupSuffix, DataStore.ArchiveFile + DataStore.BackupSuffix, Legacy.ArchiveFile + Legacy.BackupSuffix },
        new[] { Legacy.ArchiveFile + Legacy.BrokenSuffix, DataStore.ArchiveFile + DataStore.BrokenSuffix, Legacy.ArchiveFile + Legacy.BrokenSuffix },
        new[] { OldLogFile, DataStore.LogFile, DataStore.LogFile },
        new[] { OldLogFile + Legacy.OldLogSuffix, DataStore.LogFile + DataStore.OldLogSuffix, DataStore.LogFile + Legacy.OldLogSuffix } };

    static readonly string[] Products = { "GitHub Project Monitor", "Project Monitor", "Project Manager" };

    public static bool OwnExe(string exe) {
        try { return Products.Contains(FileVersionInfo.GetVersionInfo(exe).ProductName); } catch { return false; }
    }

    public static bool Its(Locations l) {
        try {
            using (var k = Registry.CurrentUser.OpenSubKey(l.Key)) {
                string who = k == null ? "" : Convert.ToString(k.GetValue("Publisher"));
                if (!string.IsNullOrEmpty(who)) return who == Installer.Publisher && (!File.Exists(l.Exe) || OwnExe(l.Exe));
            }
            return File.Exists(l.Exe) && OwnExe(l.Exe);
        } catch { return false; }
    }

    public static bool AlreadyRejected(string newData) {
        return File.Exists(Path.Combine(newData, DataStore.TransitionDeclinedFile)) || File.Exists(Path.Combine(newData, Legacy.TransitionDeclinedFile));
    }

    public static string Question(OldInstall v, string newData) {
        var things = new List<string>();
        int n = CredentialStore.FormatFindings(v.CredentialStorePrefix).Count;
        if (n > 0) things.Add(Language.N("transition.saved_access_keys", n));
        if (v.DataStore != null && !Installer.Equal(v.DataStore, newData) && File.Exists(Path.Combine(v.DataStore, Legacy.ArchiveFile))) things.Add(Language.T("transition.the_data"));
        if (v.Program != null && Its(v.Program)) things.Add(Language.T("transition.old_copy_program_which_remove"));
        return Language.T("transition.computer_found_under_old_name", "things", string.Join(", ", things));
    }

    public static int Decide(OldInstall v, string newData, string newPrefix, bool removeProgram, Func<string, bool> ask) {
        if (ask(Question(v, newData))) return Run(v, newData, newPrefix, removeProgram);
        try { Directory.CreateDirectory(newData); File.WriteAllText(Path.Combine(newData, DataStore.TransitionDeclinedFile), "chosen on " + DateTime.Now.ToString("dd/MM/yyyy") + "\r\n"); }
        catch (Exception ex) { DataStore.Log("transition from the old name, choice: " + ex.Message); }
        return 0;
    }

    public static OldInstall ForNow() {
        if (string.IsNullOrEmpty(DataStore.DataOverride)) return Real;
        return new OldInstall { Program = Real.Program, DataStore = null, CredentialStorePrefix = Real.CredentialStorePrefix, Mutex = Real.Mutex };
    }

    public static bool Open(OldInstall v) {
        try { Mutex m; if (!Mutex.TryOpenExisting(v.Mutex, out m)) return false; m.Close(); return true; } catch { return true; }
    }

    public static bool Needs(OldInstall v, string newData, string newPrefix) {
        try {
            if (v.Program != null && Its(v.Program)) return true;
            if (CredentialStore.FormatFindings(v.CredentialStorePrefix).Count > 0) return true;
            return v.DataStore != null && !Installer.Equal(v.DataStore, newData) && File.Exists(Path.Combine(v.DataStore, Legacy.ArchiveFile));
        } catch (Exception ex) { DataStore.Log("transition from the old name, check: " + ex.Message); return false; }
    }

    public static int Run(OldInstall v, string newData, string newPrefix, bool removeProgram) {
        int badly = 0;
        try {
            foreach (var name in CredentialStore.FormatFindings(v.CredentialStorePrefix)) {
                string newValue = newPrefix + name.Substring(v.CredentialStorePrefix.Length);
                if (CredentialStore.Read(newValue) != null || CredentialStore.Copy(name, newValue)) CredentialStore.Delete(name);
                else { badly++; DataStore.Log("transition from the old name: an access key could not be moved"); }
            }
        } catch (Exception ex) { badly++; DataStore.Log("transition from the old name, keys: " + ex.Message); }
        if (v.DataStore != null && Directory.Exists(v.DataStore) && !Installer.Equal(v.DataStore, newData)) badly += DataGateway(v.DataStore, newData);
        if (removeProgram && v.Program != null && Its(v.Program)) badly += RemoveProgram(v.Program);
        return badly;
    }

    static int DataGateway(string source, string a) {
        int badly = 0;
        try {
            Directory.CreateDirectory(a);
            foreach (var f in DataFile) {
                string s = Path.Combine(source, f[0]), d = Path.Combine(a, f[1]);
                if (!File.Exists(s)) continue;
                try { if (File.Exists(d) || File.Exists(Path.Combine(a, f[2]))) { if (f[0] != Legacy.ArchiveFile) File.Delete(s); else badly++; } else File.Move(s, d); }
                catch (Exception ex) { badly++; DataStore.Log("transition from the old name, " + f[0] + ": " + ex.Message); }
            }
            try { File.Delete(Path.Combine(source, Legacy.NoInstallFile)); } catch { }
            string ps = Path.Combine(source, Legacy.PackagesFolder), pd = Path.Combine(a, DataStore.PackagesFolder);
            if (Directory.Exists(ps)) {
                Directory.CreateDirectory(pd);
                foreach (var f in Directory.GetFiles(ps, "*.md")) {
                    string d = Path.Combine(pd, Path.GetFileName(f));
                    try { if (File.Exists(d) || File.Exists(Path.Combine(a, Legacy.PackagesFolder, Path.GetFileName(f)))) File.Delete(f); else File.Move(f, d); } catch (Exception ex) { badly++; DataStore.Log("transition from the old name, package: " + ex.Message); }
                }
                try { Directory.Delete(ps, false); } catch (IOException) { }
            }
            try { Directory.Delete(source, false); } catch (IOException) { }
        } catch (Exception ex) { badly++; DataStore.Log("transition from the old name, data: " + ex.Message); }
        return badly;
    }

    static int RemoveProgram(Locations l) {
        int badly = 0;
        try { Registry.CurrentUser.DeleteSubKeyTree(l.Key, false); } catch (Exception ex) { badly++; DataStore.Log("transition from the old name, registry: " + ex.Message); }
        try { File.Delete(l.MenuStart); } catch (Exception ex) { badly++; DataStore.Log("transition from the old name, Start menu: " + ex.Message); }
        try { File.Delete(l.Exe); Directory.Delete(l.Folder, false); } catch (DirectoryNotFoundException) { } catch (IOException ex) { DataStore.Log("transition from the old name, folder: " + ex.Message); }
        catch (Exception ex) { badly++; DataStore.Log("transition from the old name, program: " + ex.Message); }
        return badly;
    }
}
