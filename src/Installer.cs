using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public static partial class Installer {
    public static readonly Locations Real = new Locations {
        Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProjectManager"),
        MenuStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Manager.lnk"),
        Key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectManager" };

    [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, uint marks, IntPtr a, IntPtr b);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string file, int source, IntPtr[] large, IntPtr[] small, uint howMany);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public const string Publisher = "NSCENT.ART di Scentoni Nicolò";

    public static int IconsInside(string file) { try { return (int)ExtractIconEx(file, -1, null, null, 0); } catch { return 0; } }

    static string Marker { get { return Path.Combine(DataStore.Folder, DataStore.NoInstallFile); } }
    static string LegacyMarker { get { return Path.Combine(DataStore.Folder, Legacy.NoInstallFile); } }

    public static bool Equal(string a, string b) { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
    public static bool Installed() { return Equal(Application.ExecutablePath, Real.Exe); }

    public static string InstalledVersion() {
        try { return File.Exists(Real.Exe) ? FileVersionInfo.GetVersionInfo(Real.Exe).FileVersion ?? "" : null; } catch { return null; }
    }

    public static bool ToAsk() { return ToAsk(Installed(), InstalledVersion()); }

    public static bool ToAsk(bool installed, string installedVersion) {
        return !installed && (!(File.Exists(Marker) || File.Exists(LegacyMarker)) || installedVersion != null);
    }

    public static void MarkWithoutInstall() {
        try { Directory.CreateDirectory(DataStore.Folder); File.WriteAllText(Marker, "chosen on " + DateTime.Now.ToString("dd/MM/yyyy") + "\r\n"); }
        catch (Exception ex) { DataStore.Log("choice to run without installing: " + ex.Message); }
    }

    public static string Install(Locations l) {
        try {
            if (File.Exists(l.Exe) && !Equal(Application.ExecutablePath, l.Exe) && !Transition.OwnExe(l.Exe)) return Language.T("installer.already_another_program_same_name", "folder", l.Folder);
            Directory.CreateDirectory(l.Folder);
            if (!Equal(Application.ExecutablePath, l.Exe)) File.WriteAllBytes(l.Exe, File.ReadAllBytes(Application.ExecutablePath));
            using (var k = Registry.CurrentUser.CreateSubKey(l.Key)) {
                k.SetValue("DisplayName", "Project Manager");
                k.SetValue("DisplayVersion", Program.Version);
                k.SetValue("DisplayIcon", l.Exe);
                k.SetValue("Publisher", Publisher);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("InstallLocation", l.Folder);
                k.SetValue("UninstallString", "\"" + l.Exe + "\" --uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(l.Exe).Length / 1024), RegistryValueKind.DWord);
            }
            Link(l);
            try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
            return null;
        } catch (IOException ex) {
            DataStore.Log("install: " + ex.Message);
            return Language.T("installer.file_use_close_project_manager");
        } catch (Exception ex) {
            DataStore.Log("install: " + ex.Message);
            return ex.Message;
        }
    }

    static void Link(Locations l) {
        Directory.CreateDirectory(Path.GetDirectoryName(l.MenuStart));
        var type = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(type);
        object lnk = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { l.MenuStart });
        var t = lnk.GetType();
        foreach (var v in new[] { new[] { "TargetPath", l.Exe }, new[] { "WorkingDirectory", l.Folder }, new[] { "IconLocation", l.Exe + ",0" }, new[] { "Description", "Project Manager" } })
            t.InvokeMember(v[0], System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { v[1] });
        t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null);
    }

    static string LinkMeta(string file) {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(type);
        object lnk = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { file });
        return Convert.ToString(lnk.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, lnk, null));
    }

    static bool OpenElsewhere() {
        try { Mutex m; if (!Mutex.TryOpenExisting(Program.UniqueCopy, out m)) return false; m.Close(); return true; } catch { return true; }
    }

    public static int Ask() {
        const string T = "Project Manager";
        if (OpenElsewhere()) { MessageBox.Show(Language.T("installer.project_manager_open_close_try"), T); return 1; }
        if (MessageBox.Show(Language.T("installer.remove_project_manager_computer_github"),
                            T, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 1;
        bool data = MessageBox.Show(Language.T("installer.delete_data_too_thats_downloaded", "folder", DataStore.Folder),
                                    T, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        Remove(Real, data, CredentialStore.Prefix);
        MessageBox.Show(Language.T("installer.done", "part", (data ? "" : Language.T("installer.data_stayed", "folder", DataStore.Folder))), T);
        return 0;
    }

    public static void Remove(Locations l, bool data, string credentialStorePrefix) {
        try { Registry.CurrentUser.DeleteSubKeyTree(l.Key, false); } catch (Exception ex) { DataStore.Log("uninstall, registry: " + ex.Message); }
        try { File.Delete(l.MenuStart); } catch (Exception ex) { DataStore.Log("uninstall, Start menu: " + ex.Message); }
        try { CredentialStore.RemoveAll(credentialStorePrefix); } catch (Exception ex) { DataStore.Log("uninstall, credential store: " + ex.Message); }
        if (data) RemoveData(DataStore.Folder);
        if (Equal(Application.ExecutablePath, l.Exe)) {
            string sys = Environment.SystemDirectory;
            Process.Start(new ProcessStartInfo(Path.Combine(sys, "cmd.exe"), "/c \"\"" + Path.Combine(sys, "ping.exe") + "\" -n 3 127.0.0.1 >nul & del /f /q \"" + l.Exe + "\" & rmdir \"" + l.Folder + "\"\"")
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = sys });
        } else {
            try { File.Delete(l.Exe); Directory.Delete(l.Folder, false); } catch (Exception ex) { DataStore.Log("uninstall, folder: " + ex.Message); }
        }
    }

    static bool OldPackage(string name) { return System.Text.RegularExpressions.Regex.IsMatch(name, @"_\d{4}-\d{2}-\d{2}_\d{4}\.md$"); }

    static void RemoveData(string c) {
        var names = new List<string> {
            DataStore.ArchiveFile, DataStore.ArchiveFile + DataStore.BackupSuffix, DataStore.ArchiveFile + DataStore.TempSuffix, DataStore.ArchiveFile + DataStore.BrokenSuffix,
            DataStore.LogFile, DataStore.LogFile + DataStore.OldLogSuffix, DataStore.NoInstallFile, DataStore.TransitionDeclinedFile, DataStore.LanguageFile,
            Legacy.ArchiveFile, Legacy.ArchiveFile + Legacy.BackupSuffix, Legacy.ArchiveFile + Legacy.TempSuffix, Legacy.ArchiveFile + Legacy.BrokenSuffix,
            DataStore.LogFile + Legacy.OldLogSuffix, Legacy.NoInstallFile, Legacy.TransitionDeclinedFile, Legacy.LanguageFile };
        foreach (var n in names)
            try { File.Delete(Path.Combine(c, n)); } catch (Exception ex) { DataStore.Log("uninstall, " + n + ": " + ex.Message); }
        foreach (var folder in new[] { DataStore.ExportsFolder, DataStore.PackagesFolder, Legacy.PackagesFolder }) {
            string p = Path.Combine(c, folder);
            bool exported = folder == DataStore.ExportsFolder;
            try {
                if (Directory.Exists(p)) {
                    foreach (var f in Directory.GetFiles(p, exported ? "*.txt" : "*.md"))
                        if (exported ? TextExport.NameGenerated(Path.GetFileName(f)) : OldPackage(Path.GetFileName(f))) File.Delete(f);
                    if (Directory.GetFileSystemEntries(p).Length == 0) Directory.Delete(p, false);
                }
            } catch (Exception ex) { DataStore.Log("uninstall, " + folder + ": " + ex.Message); }
        }
        try { Directory.Delete(c, false); } catch (IOException) { }
    }
}
