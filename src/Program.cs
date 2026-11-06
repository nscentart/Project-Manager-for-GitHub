using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Project Manager")]
[assembly: AssemblyProduct("Project Manager")]
[assembly: AssemblyFileVersion(Program.Version)]
[assembly: AssemblyInformationalVersion(Program.Version)]

public static partial class Program {
    public const string Version = "1.2";
    public const string UniqueCopy = "ProjectManager-una-copia";

    public static int LanguageOption(string[] args) {
        int here = Array.IndexOf(args, "--language");
        return here >= 0 ? here : Array.IndexOf(args, Legacy.LanguageArgument);
    }

    public static bool WantsUninstall(string[] args) { return args.Length == 1 && (args[0] == "--uninstall" || args[0] == Legacy.UninstallArgument); }

    public static bool WantsPortable(string[] args) { return args.Length == 1 && (args[0] == "--portable" || args[0] == Legacy.PortableArgument); }

    public static Stream Resource(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

    [STAThread]
    static int Main(string[] args) {
        string language = null;
        int here = LanguageOption(args);
        if (here >= 0 && here + 1 < args.Length) {
            if (!Language.Valid(args[here + 1])) return 2;
            language = args[here + 1];
            Language.Use(language);
            args = args.Where((x, i) => i != here && i != here + 1).ToArray();
        }
        if (language == null && args.Length > 0 && args[0].StartsWith("--test-")) Language.Use("en");
        if (args.Length == 3 && args[0] == "--test-locales") return LocaleTests.Tables(args[1], args[2]);
        if (args.Length == 3 && args[0] == "--test-screener") return RunScreenerTests(args[1], args[2]);
        if (args.Length == 2 && args[0] == "--test-organizations") return Organization.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-overview") return OverviewPage.RunTests(args[1]);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && args[0] == "--test-accounts") return AccountAccess.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-writes") return WriteTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-install") return Installer.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-wizard") return Wizard.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-license") return LicenseTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-stats") return StatsTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-missing-repos") return MissingReposTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-missing-repos-copy") return MissingReposTests.OnCopy(args[1]);
        if (args.Length == 2 && args[0] == "--test-update-notice") return UpdateNoticeTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-retry") return RetryTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-migration") return MigrationTests.RunTests(args[1]);
        if (args.Length == 2 && args[0] == "--test-migration-copy") return MigrationTests.OnCopy(args[1]);
        if (args.Length == 2 && args[0] == "--test-live-locales") return LocaleTests.Live(args[1]);
        if (args.Length == 2 && args[0] == "--test-language-command") return LocaleTests.CommandFrom(args[1], language);
        if (args.Length == 2 && args[0] == "--screenshots") return Screenshots.All(args[1], null);
        if (args.Length == 3 && args[0] == "--screenshots") return Language.Valid(args[2]) ? Screenshots.All(args[1], args[2]) : 2;
        if (args.Length == 3 && args[0] == "--test-repo") return RunRepoTests(args[1], args[2]);
        if (args.Length == 6 && args[0] == "--test-remove") return RunRemoveTests(args);
        bool hereData = DataStore.HasArchive();
        Language.Start(language, DataStore.Folder, hereData);
        if (WantsUninstall(args)) return Installer.Ask();
        bool first, withoutAsk = WantsPortable(args);
        string after = null;
        using (var m = new Mutex(true, UniqueCopy, out first)) {
            if (!first) { MessageBox.Show(Language.T("program.project_manager_already_open_install"), "Project Manager"); return 0; }
            Application.ThreadException += (s, e) => { DataStore.Log("error: " + e.Exception); MessageBox.Show(e.Exception.Message, "Project Manager"); };
            var oldInstall = Transition.ForNow();
            bool transition = !Transition.AlreadyRejected(DataStore.Folder) && Transition.Needs(oldInstall, DataStore.Folder, CredentialStore.Prefix);
            if (transition && Transition.Open(oldInstall)) { MessageBox.Show(Language.T("program.project_monitor_old_name_still"), "Project Manager"); return 0; }
            if (language == null && transition && !hereData && Language.ToChoose(DataStore.Folder, false)) Language.Use(Language.DefaultCode);
            if (language == null && Language.ToChoose(DataStore.Folder, hereData || transition)) {
                using (var q = new LanguageChoiceDialog(Language.Code)) {
                    if (q.ShowDialog() != DialogResult.OK) return 0;
                    Language.Choose(q.Code, DataStore.Folder);
                }
            }
            if (!withoutAsk && Installer.ToAsk()) {
                var choice = WelcomeDialog.Choice.Cancel;
                using (var b = new WelcomeDialog(Installer.InstalledVersion())) { b.ShowDialog(); choice = b.Response; }
                if (choice == WelcomeDialog.Choice.Cancel) return 0;
                if (choice == WelcomeDialog.Choice.Without) Installer.MarkWithoutInstall();
                else {
                    string error = Installer.Install(Installer.Real);
                    if (error != null) { MessageBox.Show(Language.T("program.could_not_install", "error", error), "Project Manager", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
                    after = Installer.Real.Exe;
                }
            }
            if (transition) Transition.Decide(oldInstall, DataStore.Folder, CredentialStore.Prefix, after != null, question => MessageBox.Show(question, "Project Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
            if (after == null) do { MainWindow.ReopenForLanguage = false; Application.Run(new MainWindow()); } while (MainWindow.ReopenForLanguage);
        }
        if (after != null) try { Process.Start(new ProcessStartInfo(after) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(after) }); } catch (Exception ex) { DataStore.Log("start after install: " + ex.Message); }
        return 0;
    }
}
