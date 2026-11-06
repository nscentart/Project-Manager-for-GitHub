using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public static partial class Installer {

    public const string TestMark = "ProjectManager-test-";

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        string base_ = Path.Combine(Path.GetTempPath(), TestMark + Guid.NewGuid().ToString("N"));
        string root = @"Software\" + TestMark + Guid.NewGuid().ToString("N");
        var l = new Locations { Folder = Path.Combine(base_, "program"), MenuStart = Path.Combine(base_, "menu", "Project Manager.lnk"), Key = root + @"\Uninstall" };
        string data = Path.Combine(base_, "data"), pref = TestMark + Guid.NewGuid().ToString("N") + "/";
        bool realBefore = File.Exists(Real.MenuStart) || File.Exists(Real.Exe), keyBefore = Registry.CurrentUser.OpenSubKey(Real.Key) != null;
        var vv = Transition.Real.Program;
        bool oldBefore = File.Exists(vv.MenuStart) || File.Exists(vv.Exe), keyOldBefore = Registry.CurrentUser.OpenSubKey(vv.Key) != null;
        string pv = TestMark + "old-" + Guid.NewGuid().ToString("N") + "/", pn = TestMark + "new-" + Guid.NewGuid().ToString("N") + "/";
        try {
            v("the real places belong to the user, no administrator needed", Real.Folder.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) && Real.MenuStart.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Programs)));
            v("install: no error", Install(l) == null);
            v("install: the exe is in its folder, whole", File.Exists(l.Exe) && new FileInfo(l.Exe).Length == new FileInfo(Application.ExecutablePath).Length);
            v("install: the Start menu points to the exe", File.Exists(l.MenuStart) && Equal(LinkMeta(l.MenuStart), l.Exe));
            using (var k = Registry.CurrentUser.OpenSubKey(l.Key)) {
                v("install: the entry is in Installed apps", k != null && Convert.ToString(k.GetValue("DisplayName")) == "Project Manager" && Convert.ToString(k.GetValue("DisplayVersion")) == Program.Version);
                v("install: the entry says who made it", k != null && Convert.ToString(k.GetValue("Publisher")) == Publisher);
                v("install: the entry has today's date", k != null && Convert.ToString(k.GetValue("InstallDate")) == DateTime.Now.ToString("yyyyMMdd"));
                v("install: the entry knows how to remove", k != null && Convert.ToString(k.GetValue("UninstallString")) == "\"" + l.Exe + "\" --uninstall");
            }
            v("the installed exe contains its own icon", IconsInside(l.Exe) > 0);
            v("reinstall over: no error", Install(l) == null);
            v("the version written in the exe is the right one", FileVersionInfo.GetVersionInfo(l.Exe).FileVersion == Program.Version);

            Directory.CreateDirectory(Path.Combine(data, "packages"));
            File.WriteAllText(Path.Combine(data, "archive.json"), "{}");
            File.WriteAllText(Path.Combine(data, "packages", "mario_a_2026-11-03_1200.md"), "x");
            File.WriteAllText(Path.Combine(data, "packages", "my notes.md"), "x");
            File.WriteAllText(Path.Combine(data, "not-mine.txt"), "x");
            CredentialStore.Write(pref + "pippo", "pippo", "dummy");
            int end = RemoveFromOutside(l, data, pref, "no");
            v("remove without data: finishes well", end == 0);
            v("remove: the exe goes away, and the empty folder with it", Wait(() => !File.Exists(l.Exe) && !Directory.Exists(l.Folder)));
            v("remove: the Start menu goes away", !File.Exists(l.MenuStart));
            v("remove: the entry in Installed apps goes away", Registry.CurrentUser.OpenSubKey(l.Key) == null);
            v("remove: the access keys go away", CredentialStore.FormatFindings(pref).Count == 0);
            v("remove without data: the data stay", File.Exists(Path.Combine(data, "archive.json")) && File.Exists(Path.Combine(data, "packages", "mario_a_2026-11-03_1200.md")));

            v("install again: no error", Install(l) == null);
            File.WriteAllText(Path.Combine(l.Folder, "not-mine.txt"), "x");
            end = RemoveFromOutside(l, data, pref, "si");
            v("remove with data: finishes well", end == 0);
            v("remove with data: ours go away", Wait(() => !File.Exists(l.Exe)) && !File.Exists(Path.Combine(data, "archive.json")) && !File.Exists(Path.Combine(data, "packages", "mario_a_2026-11-03_1200.md")));
            v("remove with data: someone else's file among the packages stays", File.Exists(Path.Combine(data, "packages", "my notes.md")));
            string onlyData = Path.Combine(base_, "data-only");
            Directory.CreateDirectory(Path.Combine(onlyData, "packages"));
            File.WriteAllText(Path.Combine(onlyData, "packages", "mario_a_2026-11-03_1200.md"), "x");
            RemoveData(onlyData);
            v("remove with data: the empty packages folder goes away", !Directory.Exists(onlyData));
            string everything = Path.Combine(base_, "data-everything");
            Directory.CreateDirectory(Path.Combine(everything, "packages"));
            Directory.CreateDirectory(Path.Combine(everything, "pacchetti"));
            var removable = new[] { "archive.json", "archive.json.bak", "archive.json.new", "archive.json.broken", "ProjectManager.log", "ProjectManager.log.old", "no-install.txt", "transition-declined.txt", "language.txt",
                "archivio.json", "archivio.json.prima", "archivio.json.nuovo", "archivio.json.rotto", "ProjectManager.log.vecchio", "senza-installare.txt", "passaggio-no.txt", "lingua.txt",
                Path.Combine("packages", "mario_a_2026-11-03_1200.md"), Path.Combine("pacchetti", "mario_a_2026-11-03_1200.md") };
            foreach (var name in removable) File.WriteAllText(Path.Combine(everything, name), "x");
            File.WriteAllText(Path.Combine(everything, "not-mine.txt"), "x");
            RemoveData(everything);
            v("remove with data: every current and every old name goes away", removable.All(name => !File.Exists(Path.Combine(everything, name))) && !Directory.Exists(Path.Combine(everything, "packages")) && !Directory.Exists(Path.Combine(everything, "pacchetti")));
            v("remove with data: someone else's file next to ours stays, and so does the folder", File.Exists(Path.Combine(everything, "not-mine.txt")));
            v("the generated name is recognized", Package.NameGenerated(Path.GetFileName(Package.FileName("mario/a"))) && !Package.NameGenerated("a.md") && !Package.NameGenerated("note_2026.md"));
            v("remove with data: someone else's file in the data stays", File.Exists(Path.Combine(data, "not-mine.txt")));
            v("remove: someone else's file in the program folder stays", File.Exists(Path.Combine(l.Folder, "not-mine.txt")));

            RunTransitionTests(v, base_, root, pv, pn);

            v("the test did not touch the real places", (File.Exists(Real.MenuStart) || File.Exists(Real.Exe)) == realBefore && (Registry.CurrentUser.OpenSubKey(Real.Key) != null) == keyBefore);
            v("the test did not touch the places of the old name", (File.Exists(vv.MenuStart) || File.Exists(vv.Exe)) == oldBefore && (Registry.CurrentUser.OpenSubKey(vv.Key) != null) == keyOldBefore);
        } catch (Exception ex) { v("error: " + ex.Message, false); }
        finally {
            try { Registry.CurrentUser.DeleteSubKeyTree(root, false); } catch { }
            try { CredentialStore.RemoveAll(pref); CredentialStore.RemoveAll(pv); CredentialStore.RemoveAll(pn); } catch { }
            try { Directory.Delete(base_, true); } catch { }
        }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }

    static void RunTransitionTests(Action<string, bool> v, string base_, string root, string pv, string pn) {
        string guid = Guid.NewGuid().ToString("N"), dataV = Path.Combine(base_, "data-old"), dataN = Path.Combine(base_, "data-new");
        var lv = new Locations { Folder = Path.Combine(base_, "program-old"), MenuStart = Path.Combine(base_, "menu", "Project Monitor old.lnk"), Key = root + @"\UninstallOld", ExeName = "ProjectMonitor.exe" };
        var vec = new OldInstall { Program = lv, DataStore = dataV, CredentialStorePrefix = pv, Mutex = TestMark + "mutex-" + guid };
        Func<string, string> L = f => File.Exists(f) ? File.ReadAllText(f) : null;
        Install(lv);
        Directory.CreateDirectory(Path.Combine(dataV, "pacchetti"));
        File.WriteAllText(Path.Combine(dataV, "archivio.json"), "{\"x\":1}");
        File.WriteAllText(Path.Combine(dataV, "archivio.json.prima"), "before");
        File.WriteAllText(Path.Combine(dataV, "ProjectMonitor.log"), "line");
        File.WriteAllText(Path.Combine(dataV, "senza-installare.txt"), "x");
        File.WriteAllText(Path.Combine(dataV, "not-mine.txt"), "x");
        File.WriteAllText(Path.Combine(dataV, "pacchetti", "a.md"), "package");
        CredentialStore.Write(pv + "pippo", "Pippo", "fake-old-test");
        CredentialStore.Write(pv + "already", "already", "old-value");
        CredentialStore.Write(pn + "already", "already", "new-value");

        v("transition: it notices that there is an old name to bring over", Transition.Needs(vec, dataN, pn));
        v("transition: the old program, switched off, does not count as open", !Transition.Open(vec));
        using (var heldMutex = new Mutex(true, vec.Mutex)) v("transition: the old program, running, counts as open", Transition.Open(vec));
        v("transition: finishes without wrong steps", Transition.Run(vec, dataN, pn, true) == 0);
        v("transition: the access key is under the new name, identical", CredentialStore.Read(pn + "pippo") == "fake-old-test");
        v("transition: the old key is gone", CredentialStore.FormatFindings(pv).Count == 0);
        v("transition: a key already under the new name is not overwritten", CredentialStore.Read(pn + "already") == "new-value");
        v("transition: archive and log are under the new name", L(Path.Combine(dataN, "archive.json")) == "{\"x\":1}" && L(Path.Combine(dataN, "archive.json.bak")) == "before" && L(Path.Combine(dataN, "ProjectManager.log")) == "line");
        v("transition: the packages are under the new name", L(Path.Combine(dataN, "packages", "a.md")) == "package");
        v("transition: from the old data only ours go away", !File.Exists(Path.Combine(dataV, "archivio.json")) && !File.Exists(Path.Combine(dataV, "senza-installare.txt")) && File.Exists(Path.Combine(dataV, "not-mine.txt")));
        v("transition: the old program goes away (exe, shortcut, entry)", !File.Exists(lv.Exe) && !Directory.Exists(lv.Folder) && !File.Exists(lv.MenuStart) && Registry.CurrentUser.OpenSubKey(lv.Key) == null);
        v("transition: done, nothing is left to bring over", !Transition.Needs(vec, dataN, pn));
        v("transition: doing it again changes nothing", Transition.Run(vec, dataN, pn, true) == 0 && L(Path.Combine(dataN, "archive.json")) == "{\"x\":1}");

        File.WriteAllText(Path.Combine(dataV, "archivio.json"), "{\"x\":2}");
        v("transition: an archive already under the new name is not overwritten", Transition.Run(vec, dataN, pn, false) == 1 && L(Path.Combine(dataN, "archive.json")) == "{\"x\":1}" && L(Path.Combine(dataV, "archivio.json")) == "{\"x\":2}");

        string dataM = Path.Combine(base_, "data-mixed");
        Directory.CreateDirectory(dataM);
        File.WriteAllText(Path.Combine(dataM, "archivio.json"), "{\"x\":3}");
        v("transition: an archive already there under the old Italian name is not overwritten either", Transition.Run(vec, dataM, pn, false) == 1 && L(Path.Combine(dataM, "archivio.json")) == "{\"x\":3}" && !File.Exists(Path.Combine(dataM, "archive.json")) && L(Path.Combine(dataV, "archivio.json")) == "{\"x\":2}");

        string dataD = Path.Combine(base_, "data-declined"), dataO = Path.Combine(base_, "data-declined-old");
        v("transition: nothing declined yet", !Transition.AlreadyRejected(dataD));
        Transition.Decide(vec, dataD, pn, false, question => false);
        v("transition: refusing writes the new marker only", File.Exists(Path.Combine(dataD, "transition-declined.txt")) && !File.Exists(Path.Combine(dataD, "passaggio-no.txt")) && Transition.AlreadyRejected(dataD));
        Directory.CreateDirectory(dataO);
        File.WriteAllText(Path.Combine(dataO, "passaggio-no.txt"), "x");
        v("transition: the old marker still counts as a refusal", Transition.AlreadyRejected(dataO));

        string dataA = Path.Combine(base_, "data-foreign");
        var la = new Locations { Folder = Path.Combine(base_, "program-foreign"), MenuStart = Path.Combine(base_, "menu", "Other Monitor.lnk"), Key = root + @"\UninstallForeign", ExeName = "ProjectMonitor.exe" };
        var va = new OldInstall { Program = la, DataStore = null, CredentialStorePrefix = pv, Mutex = TestMark + "mutex-foreign-" + guid };
        Directory.CreateDirectory(la.Folder);
        File.WriteAllText(la.Exe, "another program");
        File.WriteAllText(la.MenuStart, "x");
        using (var k = Registry.CurrentUser.CreateSubKey(la.Key)) k.SetValue("Publisher", "Other Company");
        v("transition: whoever made the entry is not us, so it is not ours", !Transition.Its(la));
        v("transition: someone else's program is not to be brought over", !Transition.Needs(va, dataA, pn));
        CredentialStore.Write(pv + "foreign", "Foreign", "fake-foreign");
        v("transition: a sign-in under the old name yes", Transition.Needs(va, dataA, pn));
        string asked = null;
        v("transition: answering no finishes well", Transition.Decide(va, dataA, pn, true, d => { asked = d; return false; }) == 0);
        v("transition: the question says what it found and how to say no", asked != null && asked.Contains("1 saved access key") && asked.Contains("another program") && asked.Contains("answer No") && !asked.Contains("old copy"));
        v("transition: with no it touches nothing", CredentialStore.Read(pv + "foreign") == "fake-foreign" && CredentialStore.Read(pn + "foreign") == null && File.Exists(la.Exe));
        v("transition: with no it does not ask again", Transition.AlreadyRejected(dataA));
        v("transition: without no it can ask", !Transition.AlreadyRejected(dataN));
        v("transition: with yes it brings the access", Transition.Decide(va, dataA, pn, true, d => true) == 0 && CredentialStore.Read(pn + "foreign") == "fake-foreign");
        v("transition: but the foreign program stays (exe, shortcut, entry)", File.Exists(la.Exe) && File.Exists(la.MenuStart) && Registry.CurrentUser.OpenSubKey(la.Key) != null);
        Registry.CurrentUser.DeleteSubKeyTree(la.Key, false);
        v("transition: without an entry and with a foreign exe, it is not ours", !Transition.Its(la));
        File.Copy(Application.ExecutablePath, la.Exe, true);
        v("transition: without an entry and with our exe, it is ours", Transition.Its(la));
        File.WriteAllText(la.Exe, "another program");
        using (var k = Registry.CurrentUser.CreateSubKey(la.Key)) k.SetValue("Publisher", Publisher);
        v("transition: our entry but a foreign exe, it is not ours", !Transition.Its(la));
        var lf = new Locations { Folder = Path.Combine(base_, "program-samename"), MenuStart = Path.Combine(base_, "menu", "Samename.lnk"), Key = root + @"\UninstallSamename" };
        Directory.CreateDirectory(lf.Folder);
        File.WriteAllText(lf.Exe, "another program");
        string noInstall = Install(lf);
        v("install: a foreign exe with the same name is not overwritten", noInstall != null && noInstall.Contains("I won't overwrite it") && L(lf.Exe) == "another program" && Registry.CurrentUser.OpenSubKey(lf.Key) == null && !File.Exists(lf.MenuStart));
    }

    static int RemoveFromOutside(Locations l, string data, string pref, string dataWith) {
        var psi = new ProcessStartInfo(l.Exe, "--test-remove \"" + l.Folder + "\" \"" + l.MenuStart + "\" \"" + l.Key + "\" \"" + pref + "\" " + dataWith) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() };
        psi.EnvironmentVariables[DataStore.DataVariable] = data;
        using (var p = Process.Start(psi)) { p.WaitForExit(30000); return p.ExitCode; }
    }

    static bool Wait(Func<bool> done) {
        for (int i = 0; i < 40; i++) { if (done()) return true; Thread.Sleep(250); }
        return false;
    }
}
