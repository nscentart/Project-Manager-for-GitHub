using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class MainWindow : Dialog {
    static readonly Color Ink = Theme.Ink, Azure = Theme.Azure, Gray = Theme.Gray, Border = Theme.Border, Backdrop = Theme.Backdrop;

    Archive a;
    List<Entry> entries = new List<Entry>();
    List<RecurringError> errors = new List<RecurringError>();
    List<string> models = new List<string>();
    Dictionary<string, int> handovers = new Dictionary<string, int>();
    bool busy;
    bool missingOpen;
    int lastSelected;
    HistoryArchive history = new HistoryArchive();
    static bool noticeStarted;
    static MainWindow openWindow;
    public static bool ScreenshotOnly;
    public static bool ReopenForLanguage;
    public static Action<Form> SettingsScreenshot;
    public void OpenSettings() { Settings(); }
    public bool ChangeLanguage(string created) {
        if (created == Language.Code) return false;
        Language.Choose(created, DataStore.Folder);
        ReopenForLanguage = true;
        Close();
        return true;
    }
    public void TabScreenshot(int i) {
        tabs.Choose(i);
        var selectedList = i == 2 ? quarantineLabel : i == 3 ? workLabel : i == 4 ? lErr : i == 5 ? archiveLabel : null;
        if (selectedList != null && selectedList.Items.Count > 0) selectedList.Items[0].Selected = true;
    }
    public void RepoScreenshot(string name) {
        var x = RepoNow().FirstOrDefault(r => r.Name == name);
        if (x != null && x.Missing && !missingOpen) { missingOpen = true; Render(); }
        for (int i = 0; i < repo.Items.Count; i++) { var r = ((RepoRow)repo.Items[i]).R; if (r != null && r.Name == name) repo.SelectedIndex = i; }
    }
    public void MissingScreenshot(bool groupOpen) { missingOpen = groupOpen; Render(); }
    public RepoList Column { get { return repo; } }
    public OverviewPage Overview { get { return overview; } }
    public Label State { get { return state; } }
    public bool MissingForTest { get { return missingOpen; } }
    public RepoInfo ChoiceForTest { get { return Choice(); } }
    public void BusyForTest(bool si) { busy = si; }
    public void UpdateForTest() { RefreshAll(); }
    public bool BusyNow { get { return busy; } }
    public static Func<Form, DialogResult> RemoveResponse;
    public static Func<Form, DialogResult> NoticeResponse;
    HashSet<string> hidden = new HashSet<string>();
    ComboBox accountCombo, filterCombo;
    List<string[]> filters = new List<string[]>();
    bool notUpdated;
    string emptyReason;

    ThemedButton updateButton;
    Label state, read;
    RepoList repo;
    Header header;
    TabBar tabs;
    OverviewPage overview;
    ManagementPage management;
    Dictionary<ListView, Label> emptyLabels = new Dictionary<ListView, Label>();
    TabBar.Entry quarantineTitle, workTitle, tErr, archiveTitle;
    ListView quarantineLabel, workLabel, lErr, archiveLabel;
    TextBox quarantineBox, workBox, dErr, archiveBox;

    int S(int v) { return Theme.S(v); }

    public MainWindow() {
        using (var g = CreateGraphics()) Theme.K = g.DpiX / 96f;
        Text = "Project Manager " + Program.Version;
        Font = Theme.Normal;
        BackColor = Color.White; ForeColor = Ink;
        ClientSize = new Size(S(1320), S(820));
        MinimumSize = new Size(S(900), S(560));
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        try { Icon = Theme.AppIcon(); } catch { }

        var column = new Panel { Dock = DockStyle.Left, Width = S(272), BackColor = Theme.Background };
        var mark = new Panel { Dock = DockStyle.Top, Height = S(76), BackColor = Theme.Background };
        mark.Paint += (s, e) => {
            e.Graphics.Clear(Theme.Background);
            Theme.Logo(e.Graphics, new Rectangle(S(18), S(18), S(40), S(40)));
            Theme.T(e.Graphics, "Project Manager", Theme.Medium, S(68), S(11), mark.Width - S(76), S(24), Theme.Text);
            Theme.T(e.Graphics, Language.T("window.github_version", "version", Program.Version), Theme.Small, S(68), S(35), mark.Width - S(76), S(16), Theme.Muted);
        };
        license = new LicenseLabelButton { Bounds = new Rectangle(S(68), S(51), S(272) - S(76), S(18)) };
        license.Click += delegate { OpenLicense(); };
        mark.Controls.Add(license);
        ShowLicense();
        repo = new RepoList { Dock = DockStyle.Fill };
        repo.SelectedIndexChanged += delegate { if (!loading) { if (OnGroup()) return; lastSelected = repo.SelectedIndex; Fill(); } };
        repo.GroupClicked += OpenCloseMissing;
        var choices = new Panel { Dock = DockStyle.Top, Height = S(112), BackColor = Theme.Background };
        Func<string, int, Label> above = (x, y) => new Label { Text = x, Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Theme.Background, Bounds = new Rectangle(S(20), y, S(232), S(18)), UseMnemonic = false };
        accountCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(S(20), S(18), S(232), S(28)) };
        accountCombo.SelectedIndexChanged += delegate { if (!loading) ChangeAccount(); };
        filterCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(S(20), S(72), S(232), S(28)) };
        filterCombo.SelectedIndexChanged += delegate { if (!loading) ChangeFilter(); };
        choices.Controls.AddRange(new Control[] { above(Language.T("common.account"), 0), accountCombo, above(Language.T("window.show"), S(54)), filterCombo });
        var footer = new Panel { Dock = DockStyle.Bottom, Height = S(162), BackColor = Theme.Background };
        updateButton = CreateButton(Language.T("window.update_github"), true);
        updateButton.SetBounds(S(20), S(12), S(232), S(36));
        updateButton.Click += delegate { RefreshAll(); };
        var accountsButton = DarkButton(Language.T("common.account"));
        accountsButton.SetBounds(S(20), S(54), S(232), S(32));
        accountsButton.Click += delegate { Accounts(); };
        var settingsButton = DarkButton(Language.T("window.settings"));
        settingsButton.SetBounds(S(20), S(92), S(232), S(32));
        settingsButton.Click += delegate { Settings(); };
        read = new Label { Bounds = new Rectangle(S(20), S(132), S(232), S(20)), ForeColor = Theme.Muted, BackColor = Theme.Background, Font = Theme.Small, UseMnemonic = false, TextAlign = ContentAlignment.MiddleCenter };
        footer.Controls.AddRange(new Control[] { updateButton, accountsButton, settingsButton, read });
        column.Controls.Add(repo); column.Controls.Add(footer); column.Controls.Add(choices); column.Controls.Add(mark);

        var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        state = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Gray, Dock = DockStyle.Bottom, Height = S(26),
                            Padding = new Padding(S(16), 0, 0, 0), BackColor = Backdrop, UseMnemonic = false, AutoEllipsis = true };
        header = new Header { Dock = DockStyle.Top, Height = S(74) };
        header.Switch += delegate { ChangeFollow(); };
        tabs = new TabBar { Dock = DockStyle.Top, Height = S(42) };
        var pages = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        overview = new OverviewPage { Dock = DockStyle.Fill };
        overview.ChosenRepo += r => { for (int i = 0; i < repo.Items.Count; i++) if (((RepoRow)repo.Items[i]).R == r) repo.SelectedIndex = i; };
        overview.GroupClicked += OpenCloseMissing;
        overview.RemoveAll += delegate { Remove(VisibleRepo().Where(x => x.Missing).Select(x => x.Name).ToList()); };
        overview.RemoveThis += delegate { var s = Choice(); if (s != null) Remove(new List<string> { s.Name }); };
        tabs.Entries.Add(new TabBar.Entry { Name = Language.T("window.overview"), Page = overview });
        management = new ManagementPage(WriteGateway.Real(new RealGateway(), RealRoute)) { BusyCheck = () => busy };
        management.Changed += delegate { RefreshAll(); };
        management.Renamed += (oldName, newName) => { lock (this) Writer.RenameInArchive(a, oldName, newName); lock (history) History.Rename(history, oldName, newName); Save(); SaveHistory(); Recalculate(); Render(); RefreshAll(); };
        management.Deleted += name => { lock (this) Writer.MarkDeleted(a, name); Save(); Recalculate(); Render(); RefreshAll(); };
        tabs.Entries.Add(new TabBar.Entry { Name = Language.T("window.manage"), Page = management });
        quarantineTitle = Tab(Language.T("window.quarantine"), out quarantineLabel, out quarantineBox,
            new[] { Language.T("common.repo"), "#", Language.T("common.title"), Language.T("window.author"), Language.T("window.date"), Language.T("window.reasons") }, new[] { 220, 50, 300, 110, 110, 380 },
            ButtonAction(Language.T("window.approve_move_work"), true, delegate { Decide(quarantineLabel, "approved", true); }),
            ButtonAction(Language.T("window.reject"), false, delegate { Decide(quarantineLabel, "rejected", false); }),
            ButtonAction(Language.T("window.open_github"), false, delegate { OpenOnGitHub(quarantineLabel); }));
        quarantineTitle.Alarm = true;
        workTitle = Tab(Language.T("common.to_work_on"), out workLabel, out workBox,
            new[] { Language.T("window.type"), Language.T("common.repo"), "#", Language.T("common.title"), Language.T("window.author"), Language.T("window.date"), Language.T("window.summary") }, new[] { 90, 220, 50, 300, 110, 110, 380 },
            ButtonAction(Language.T("window.create_package_claude_code"), true, delegate { CreatePackage(); }),
            ButtonAction(Language.T("window.summarize_ollama"), false, delegate { Summarize(); }),
            ButtonAction(Language.T("window.mark_done"), false, delegate { Decide(workLabel, "done", false); }),
            ButtonAction(Language.T("window.back_quarantine"), false, delegate { Decide(workLabel, "quarantine", false); }),
            ButtonAction(Language.T("window.open_github"), false, delegate { OpenOnGitHub(workLabel); }));
        tErr = Tab(Language.T("window.recurring_errors"), out lErr, out dErr,
            new[] { Language.T("window.reports_2"), Language.T("window.times_2"), Language.T("window.versions"), Language.T("window.last_time"), Language.T("common.repo"), Language.T("window.error") }, new[] { 70, 60, 110, 110, 200, 640 },
            ButtonAction(Language.T("window.create_package_claude_code"), true, delegate { CreatePackage(); }));
        tErr.Alarm = true;
        archiveTitle = Tab(Language.T("window.archive"), out archiveLabel, out archiveBox,
            new[] { Language.T("common.status"), Language.T("common.repo"), "#", Language.T("common.title"), Language.T("window.author"), Language.T("window.decided_on") }, new[] { 90, 200, 50, 360, 120, 120 },
            ButtonAction(Language.T("window.undo_decision"), false, delegate { Decide(archiveLabel, "", false); }),
            ButtonAction(Language.T("window.open_github"), false, delegate { OpenOnGitHub(archiveLabel); }));
        tabs.Entries.AddRange(new[] { quarantineTitle, workTitle, tErr, archiveTitle });
        foreach (var v in tabs.Entries) { v.Page.Dock = DockStyle.Fill; pages.Controls.Add(v.Page); }
        right.Controls.Add(pages); right.Controls.Add(tabs); right.Controls.Add(header); right.Controls.Add(state);

        Controls.Add(right); Controls.Add(column);
        Load += delegate {
            Theme.DarkBar(this); tabs.Choose(0); LoadAll();
            openWindow = this;
            if (ScreenshotOnly) return;
            SearchModels();
            if (a.Accounts.Count > 0) RefreshAll();
            if (!noticeStarted) {
                noticeStarted = true;
                UpdateCheck.CheckAtStartup(() => openWindow.a.UpdateNoticeOff, () => openWindow.a.SkippedVersion, n => openWindow.InUi(() => openWindow.Warn(n)));
            }
        };
        Shown += delegate { if (!ScreenshotOnly && a.Accounts.Count == 0) Connect(true); };
    }

    LicenseLabelButton license;
    readonly ToolTip hint = new ToolTip();

    public string LicenseText { get { return license.Text; } }

    void ShowLicense() {
        license.Text = AppLicense.LicenseCaption(AppLicense.Now(DataStore.Folder));
        hint.SetToolTip(license, license.Text);
        license.Invalidate();
    }

    void OpenLicense() {
        using (var f = new LicenseDialog(DataStore.Folder)) {
            f.ShowDialog(this);
            if (f.Changed) { ShowLicense(); state.Text = Language.T("window.license_updated", "license", license.Text); }
        }
    }

    ThemedButton CreateButton(string text, bool primary) {
        return new ThemedButton { Text = text, Background = primary ? Azure : Color.White, TextColor = primary ? Color.White : Ink, Line = primary ? Color.Empty : Border };
    }

    ThemedButton DarkButton(string text) {
        return new ThemedButton { Text = text, Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line };
    }

    Button ButtonAction(string text, bool primary, EventHandler click) {
        var b = CreateButton(text, primary);
        b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.Margin = new Padding(0, 0, S(8), 0);
        b.Click += click;
        return b;
    }

    static string Choose { get { return Language.T("window.choose_row_above_read_text"); } }

    TabBar.Entry Tab(string name, out ListView list, out TextBox detail, string[] columns, int[] widths, params Button[] buttons) {
        var t = new Panel { BackColor = Color.White, Padding = new Padding(S(16), S(10), S(16), S(10)), Visible = false };
        var sp = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Border, SplitterWidth = S(4) };
        sp.Panel1.BackColor = Color.White; sp.Panel2.BackColor = Color.White;
        var l = new Table { Dock = DockStyle.Fill };
        for (int i = 0; i < columns.Length; i++) l.Columns.Add(columns[i], S(widths[i]));
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(44), Padding = new Padding(0, S(8), 0, 0), BackColor = Color.White, WrapContents = false };
        bar.Controls.AddRange(buttons);
        var d = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true,
                              BorderStyle = BorderStyle.None, BackColor = Backdrop, Font = new Font("Consolas", 9.5f), Text = Choose };
        var emptyLabel = new Label { Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Gray, Padding = new Padding(S(12), S(12), S(12), 0),
                                UseMnemonic = false, Visible = false };
        emptyLabels[l] = emptyLabel;
        sp.Panel1.Controls.Add(l); sp.Panel1.Controls.Add(emptyLabel); sp.Panel1.Controls.Add(bar);
        sp.Panel2.Controls.Add(d);
        sp.Panel2.Padding = new Padding(0, S(6), 0, 0);
        t.Controls.Add(sp);
        t.Layout += delegate { if (sp.Height > S(300) && sp.SplitterDistance < S(200)) sp.SplitterDistance = sp.Height / 2; };
        var copyLabel = l; var copyBox = d;
        l.SelectedIndexChanged += delegate { Detail(copyLabel, copyBox); };
        list = l; detail = d;
        return new TabBar.Entry { Name = name, Page = t };
    }

    bool loading;

    void LoadAll() {
        a = DataStore.Load();
        history = History.Load();
        Recalculate();
        Render();
    }

    void Recalculate() {
        entries = ItemSorter.Entries(a);
        errors = ItemSorter.Recurring(entries);
    }

    void Save() {
        try { lock (this) DataStore.Save(a); } catch (Exception ex) { DataStore.Log("saving: " + ex.Message); MessageBox.Show(this, Language.T("window.cant_save_archive", "error", ex.Message), Text); }
    }

    void SaveHistory() {
        try { lock (history) History.Save(history); } catch (Exception ex) { DataStore.Log("history: " + ex.Message); }
    }

    RepoInfo Choice() {
        var r = repo.SelectedItem as RepoRow;
        if (r != null && r.Group) r = lastSelected >= 0 && lastSelected < repo.Items.Count ? repo.Items[lastSelected] as RepoRow : null;
        return r == null ? null : r.R;
    }

    bool OnGroup() {
        var r = repo.SelectedItem as RepoRow;
        if (r == null || !r.Group) return false;
        int here = repo.SelectedIndex;
        if (here < lastSelected) { repo.SelectedIndex = here - 1; return true; }
        if (!missingOpen) { missingOpen = true; Render(); }
        repo.SelectedIndex = here + 1;
        return true;
    }

    void OpenCloseMissing() {
        var s = Choice();
        if (missingOpen && s != null && s.Missing) { loading = true; repo.SelectedIndex = 0; lastSelected = 0; loading = false; }
        missingOpen = !missingOpen;
        Render();
    }

    void Remove(List<string> names) {
        if (busy) { state.Text = Language.T("common.wait_current_work_finish"); return; }
        AccountAccess.Removal length;
        lock (this) lock (history) length = AccountAccess.CountRemoval(a, history, names);
        if (length.Repo.Count == 0) return;
        using (var f = new RemoveDialog(length)) {
            var r = RemoveResponse != null ? RemoveResponse(f) : f.ShowDialog(this);
            if (r != DialogResult.OK) return;
        }
        var s = Choice();
        if (s != null && length.Repo.Contains(s.Name)) { loading = true; repo.SelectedIndex = 0; lastSelected = 0; loading = false; }
        lock (this) lock (history) AccountAccess.RemoveMissing(a, history, length.Repo);
        Save();
        SaveHistory();
        Recalculate();
        Render();
        state.Text = Language.N("missing.repos_removed_list", length.Repo.Count);
    }

    void Warn(NewVersion n) {
        if (CanFocus || NoticeResponse != null) { ShowNotice(n, this); return; }
        WaitNotice(n);
    }

    public static int NoticeWaitMs = 5000;

    public void WaitNotice(NewVersion n) {
        var t = new System.Windows.Forms.Timer { Interval = NoticeWaitMs };
        t.Tick += delegate { var f = openWindow; if (f == null || f.IsDisposed || !f.CanFocus) return; t.Stop(); t.Dispose(); f.ShowNotice(n, f); };
        t.Start();
    }

    public void ShowNotice(NewVersion n, IWin32Window above) {
        using (var f = new UpdateNoticeDialog(n)) {
            var r = NoticeResponse != null ? NoticeResponse(f) : f.ShowDialog(above);
            if (r == DialogResult.Ignore) { a.SkippedVersion = n.Label; Save(); }
            if (r == DialogResult.OK) UpdateCheck.Open(n.Page);
        }
    }

    List<RepoInfo> RepoNow() { lock (this) return a.Repo.ToList(); }
    List<RepoInfo> VisibleRepo() { lock (this) return AccountAccess.VisibleRepos(a); }

    OverviewPage.Counts Count(string name) {
        int c;
        string threshold = DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd");
        return new OverviewPage.Counts {
            Quarantine = entries.Count(v => v.E.Repo == name && v.State == "quarantine"), Work = entries.Count(v => v.E.Repo == name && v.State == "work"),
            Errors = errors.Count(g => g.Repo == name), Handovers = handovers.TryGetValue(name, out c) ? c : -1,
            NewReports = entries.Count(v => v.E.Repo == name && v.E.Type != "comment" && string.CompareOrdinal(v.E.Created, threshold) >= 0),
            NewComments = entries.Count(v => v.E.Repo == name && v.E.Type == "comment" && string.CompareOrdinal(v.E.Created, threshold) >= 0) };
    }

    void Render() {
        var choice = Choice();
        int top = repo.TopIndex;
        loading = true;
        FillChoices();
        repo.BeginUpdate();
        repo.Items.Clear();
        var visibleRepos = VisibleRepo().OrderBy(r => r.Missing).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var names = new HashSet<string>(visibleRepos.Select(r => r.Name));
        hidden = new HashSet<string>(RepoNow().Select(r => r.Name).Where(n => !names.Contains(n)));
        handovers.Clear();
        foreach (var r in visibleRepos) handovers[r.Name] = DataStore.PendingHandovers(r.Name);
        int missingCount = visibleRepos.Count(r => r.Missing);
        if (missingCount == 0) missingOpen = false;
        if (choice != null && choice.Missing && visibleRepos.Any(r => r.Name == choice.Name)) missingOpen = true;
        repo.Items.Add(new RepoRow { Name = Language.T("common.all_repos"), Below = Language.N("window.on_github", visibleRepos.Count - missingCount) + (missingCount > 0 ? ", " + Language.N("common.gone", missingCount) : ""),
                                      Quarantine = entries.Count(v => v.State == "quarantine" && !hidden.Contains(v.E.Repo)), Work = entries.Count(v => v.State == "work" && !hidden.Contains(v.E.Repo)) });
        int selected = 0;
        bool groupRow = false;
        foreach (var r in visibleRepos) {
            if (r.Missing && !groupRow) {
                groupRow = true;
                repo.Items.Add(new RepoRow { Group = true, Open = missingOpen, Name = Language.T("missing.gone", "n", Language.Number(missingCount)) });
            }
            if (r.Missing && !missingOpen) continue;
            var c = Count(r.Name);
            int i = repo.Items.Add(new RepoRow { R = r, Name = OverviewPage.ShortOf(r, a.Login ?? ""), Quarantine = c.Quarantine, Work = c.Work });
            if (choice != null && r.Name == choice.Name) selected = i;
        }
        repo.SelectedIndex = selected;
        lastSelected = selected;
        repo.EndUpdate();
        repo.TopIndex = Math.Min(top, repo.Items.Count - 1);
        loading = false;
        Fill();
    }

    void FillChoices() {
        accountCombo.Items.Clear();
        foreach (var c in a.Accounts) accountCombo.Items.Add(c.Login);
        accountCombo.Enabled = a.Accounts.Count > 0;
        if (a.Accounts.Count == 0) { accountCombo.Items.Add(Language.T("window.no_account_connected")); accountCombo.SelectedIndex = 0; }
        else accountCombo.SelectedIndex = Math.Max(0, a.Accounts.FindIndex(c => AccountAccess.Equal(c.Login, a.Login)));
        filters = AccountAccess.Filters(a);
        filterCombo.Items.Clear();
        foreach (var f in filters) filterCombo.Items.Add(f[1]);
        filterCombo.Enabled = a.Accounts.Count > 0;
        filterCombo.SelectedIndex = Math.Max(0, filters.FindIndex(f => f[0] == (a.Filter ?? AccountAccess.FilterAll)));
    }

    void ChangeAccount() {
        if (accountCombo.SelectedIndex < 0 || accountCombo.SelectedIndex >= a.Accounts.Count) return;
        var c = a.Accounts[accountCombo.SelectedIndex];
        if (AccountAccess.Equal(c.Login, a.Login)) return;
        if (busy) { state.Text = Language.T("common.wait_current_work_finish"); loading = true; FillChoices(); loading = false; return; }
        a.Login = c.Login; a.Filter = null;
        GitHub.Exit();
        emptyReason = null;
        Save(); Recalculate(); Render();
        RefreshAll();
    }

    void ChangeFilter() {
        if (filterCombo.SelectedIndex < 0 || filterCombo.SelectedIndex >= filters.Count) return;
        a.Filter = filters[filterCombo.SelectedIndex][0];
        if (busy) { Render(); return; }
        Save(); Render();
    }

    IEnumerable<Entry> VisibleEntries() {
        var r = Choice();
        return entries.Where(v => !hidden.Contains(v.E.Repo) && (r == null || v.E.Repo == r.Name));
    }

    void Page() {
        var r = Choice();
        bool stale = emptyReason != null && notUpdated;
        string when = r != null && r.Overview != null && r.Overview.ReadAt != null ? r.Overview.ReadAt : r == null ? a.LastUpdate : null;
        header.RepoName = r == null ? Language.T("common.all_repos") : OverviewPage.ShortOf(r, a.Login ?? "");
        header.Pill = r == null ? null : r.Private ? Language.T("common.private") : Language.T("common.public");
        header.Publish = r != null && !r.Private;
        header.Description = r == null ? Language.T("window.reports_all_repos_how_each") : r.Missing ? Language.T("window.no_longer_github")
            : string.IsNullOrEmpty(r.Description) ? Language.T("window.no_description_github") : Line(r.Description, 200);
        header.HasSwitch = r != null && !r.Missing;
        header.Follow = r != null && r.Followed;
        header.Read = when == null ? (r != null && r.Missing ? Language.T("window.never_read") : Language.T("window.not_read_yet")) : (stale ? Language.T("window.not_updated_data") : Language.T("window.read_on")) + when;
        header.OldRead = stale;
        header.Invalidate();
        read.Text = a.LastUpdate == null ? Language.T("window.never_updated") : Language.T("window.updated_on", "last_update", a.LastUpdate);
        management.Configure(r, a.Login);
        overview.Configure(r, VisibleRepo().OrderBy(x => x.Missing).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList(), a.Login, Count, stale, missingOpen, n => { lock (history) return History.Copy(history, n); });
    }

    void ChangeFollow() {
        var r = Choice();
        if (r == null || r.Missing) return;
        if (busy) { state.Text = Language.T("common.wait_current_work_finish"); return; }
        r.Followed = !r.Followed;
        if (r.Followed) r.Since = null;
        Save();
        Render();
        state.Text = r.Followed ? Language.T("window.now_following_press_update_github", "name", r.Name) : Language.T("window.no_longer_following", "name", r.Name);
    }

    static string Data(string iso) {
        DateTime t;
        return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out t) ? t.ToLocalTime().ToString("dd/MM/yyyy") : "";
    }

    static string Line(string s, int max) {
        s = Screener.MakeVisible((s ?? "").Replace("\r", " ").Replace("\n", " "));
        return s.Length > max ? s.Substring(0, max) + "..." : s;
    }

    string SummaryOf(Entry v) { return v.D != null && v.D.SummaryFingerprint == v.Fingerprint ? v.D.Summary ?? "" : ""; }

    void Fill() {
        if (a == null) return;
        var vis = VisibleEntries().OrderByDescending(v => v.E.Updated).ToList();
        Populate(quarantineLabel, vis.Where(v => v.State == "quarantine"), v => new[] {
            v.E.Repo, v.E.Number.ToString(), Line(ItemSorter.TitleOf(v.E, a), 120), Line(v.E.Author, 40), Data(v.E.Created),
            string.Join("; ", v.Reasons.Select(m => m.Explanation)) }, null);
        Populate(workLabel, vis.Where(v => v.State == "work"), v => new[] {
            ItemSorter.CategoryName(v.Category), v.E.Repo, v.E.Number.ToString(), Line(ItemSorter.TitleOf(v.E, a), 120), Line(v.E.Author, 40), Data(v.E.Created),
            Line(SummaryOf(v), 200) }, v => v.Category);
        Populate(archiveLabel, vis.Where(v => v.State == "done" || v.State == "rejected"), v => new[] {
            ItemSorter.StateName(v.State), v.E.Repo, v.E.Number.ToString(), Line(ItemSorter.TitleOf(v.E, a), 120), Line(v.E.Author, 40),
            v.D == null ? "" : Line(v.D.When, 20) }, null);
        var r = Choice();
        var err = errors.Where(g => !hidden.Contains(g.Repo) && (r == null || g.Repo == r.Name)).ToList();
        lErr.BeginUpdate(); lErr.Items.Clear();
        foreach (var g in err)
            lErr.Items.Add(new ListViewItem(new[] { g.Reports.Count.ToString(), g.Times.ToString(), string.Join(", ", g.Releases.OrderBy(x => x)),
                g.Last == DateTime.MinValue ? "" : g.Last.ToString("dd/MM/yyyy"), g.Repo, Line(g.Signature, 200) }) { Tag = g });
        lErr.EndUpdate();
        Empty(quarantineLabel, Language.T("window.no_reports"), true); Empty(workLabel, Language.T("window.nothing_work"), true);
        Empty(lErr, Language.T("window.no_recurring_errors_logs"), true); Empty(archiveLabel, Language.T("window.no_decisions_made_yet"), false);
        quarantineTitle.Number = quarantineLabel.Items.Count; workTitle.Number = workLabel.Items.Count; tErr.Number = lErr.Items.Count; archiveTitle.Number = archiveLabel.Items.Count;
        tabs.Invalidate();
        foreach (var d in new[] { quarantineBox, workBox, dErr, archiveBox }) d.Text = "";
        Page();
    }

    void Empty(ListView l, string nothing, bool suffix) {
        Label m;
        if (!emptyLabels.TryGetValue(l, out m)) return;
        string t = null;
        if (l.Items.Count == 0) {
            var one = Choice();
            if (a.Accounts.Count == 0) t = Language.T("window.no_account_connected_press_account");
            else if (one != null && one.Missing) t = Language.T("window.repo_no_longer_github_no");
            else if (emptyReason != null && notUpdated) t = emptyReason;
            else if (a.LastUpdate == null) t = Language.T("window.not_updated_press_update_github");
            else if (one != null && !one.Followed) t = Language.T("window.arent_following_repo_turn_following");
            else if (one == null && !VisibleRepo().Any(x => x.Followed && !x.Missing))
                t = Language.T("window.arent_following_any_repo_choose");
            else t = nothing + (!suffix ? "." : one != null ? Language.T("window.in", "name", one.Name) : Language.T("window.followed_repos"))
                     + (emptyReason != null ? " " + emptyReason : "");
        }
        m.Text = t ?? ""; m.Visible = t != null;
        if (t != null) m.BringToFront();
    }

    void Populate(ListView l, IEnumerable<Entry> rowsToShow, Func<Entry, string[]> columns, Func<Entry, string> groupRow) {
        l.BeginUpdate();
        l.Items.Clear(); l.Groups.Clear();
        var groups = new Dictionary<string, ListViewGroup>();
        if (groupRow != null)
            foreach (var c in ItemSorter.Categories) { var g = new ListViewGroup(c, ItemSorter.CategoryName(c).Substring(0, 1).ToUpper() + ItemSorter.CategoryName(c).Substring(1)); groups[c] = g; l.Groups.Add(g); }
        foreach (var v in rowsToShow) {
            var it = new ListViewItem(columns(v)) { Tag = v };
            if (groupRow != null) it.Group = groups[groupRow(v)];
            l.Items.Add(it);
        }
        l.EndUpdate();
    }

    void Detail(ListView l, TextBox d) {
        if (l.SelectedItems.Count == 0) { d.Text = Choose; return; }
        var sb = new StringBuilder();
        var g = l.SelectedItems[0].Tag as RecurringError;
        if (g != null) {
            sb.AppendLine(Language.T("window.error_2", "example", Screener.MakeVisible(g.Example)));
            sb.AppendLine(Language.T("window.signature", "signature", Screener.MakeVisible(g.Signature)));
            sb.AppendLine(Language.T("window.in_versions", "reports", Language.N("window.reports", g.Reports.Count), "times", Language.N("window.times", g.Times), "text", string.Join(", ", g.Releases.OrderBy(x => x)), "part", (g.Last == DateTime.MinValue ? "" : Language.T("window.from_to", "first", g.First.ToString("dd/MM/yyyy HH:mm"), "last", g.Last.ToString("dd/MM/yyyy HH:mm")))));
            sb.AppendLine();
            foreach (var v in entries.Where(x => g.Reports.Contains(x.E.Key))) sb.AppendLine("#" + v.E.Number + " " + Line(v.E.Title, 120) + "  " + v.E.Url);
            d.Text = sb.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
            return;
        }
        var w = (Entry)l.SelectedItems[0].Tag;
        sb.AppendLine(Language.T("window.by_category", "repo", w.E.Repo, "number", w.E.Number, "type", ItemSorter.TypeName(w.E.Type), "author", Screener.MakeVisible(w.E.Author), "created", Data(w.E.Created), "part", (w.E.State == "closed" ? Language.T("window.closed") : ""), "category", ItemSorter.CategoryName(w.Category)));
        sb.AppendLine(Screener.MakeVisible(ItemSorter.TitleOf(w.E, a)));
        sb.AppendLine(w.E.Url);
        if (w.Reasons.Count > 0) {
            sb.AppendLine();
            sb.AppendLine(w.State == "quarantine" ? Language.T("window.why_quarantine") : Language.T("window.customs_check_flagged_approved_hand"));
            foreach (var m in w.Reasons) sb.AppendLine("  - " + m);
        }
        string r = SummaryOf(w);
        if (r.Length > 0) { sb.AppendLine(); sb.AppendLine(Language.T("window.summary_2", "summary_model", w.D.SummaryModel, "r", Screener.MakeVisible(r))); }
        sb.AppendLine();
        sb.AppendLine(Language.T("window.original_text_invisible_characters_appear"));
        sb.AppendLine(new string('_', 60));
        sb.AppendLine(Screener.MakeVisible(w.E.Text ?? ""));
        d.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        d.SelectionStart = 0; d.SelectionLength = 0;
    }

    List<Entry> Choices(ListView l) { return l.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag).OfType<Entry>().ToList(); }

    Decision DecisionOf(Entry v) {
        var d = a.Decisions.FirstOrDefault(x => x.Key == v.E.Key);
        if (d == null) { d = new Decision { Key = v.E.Key }; a.Decisions.Add(d); }
        return d;
    }

    void Decide(ListView l, string newValue, bool ask) {
        if (busy) { state.Text = Language.T("common.wait_current_work_finish"); return; }
        var sc = Choices(l);
        if (sc.Count == 0) { state.Text = Language.T("window.first_choose_one_more_texts"); return; }
        if (ask && sc.Count > 1 && MessageBox.Show(this, Language.T("window.approve_texts_read_all", "n", sc.Count), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        foreach (var v in sc) {
            var d = DecisionOf(v);
            d.State = newValue; d.Fingerprint = v.Fingerprint; d.When = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }
        Save(); Recalculate(); Render();
        state.Text = newValue == "approved" ? Language.N("window.texts_approved_now_work", sc.Count) : newValue == "" ? Language.N("window.texts_back_previous_status", sc.Count) : Language.N("window.texts_moved", sc.Count, "state", ItemSorter.StateName(newValue));
    }

    static string Explorer { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"); } }

    void OpenOnGitHub(ListView l) {
        foreach (var v in Choices(l).Take(5)) {
            if (!v.E.Url.StartsWith("https://github.com/" + v.E.Repo + "/issues/")) continue;
            try { Process.Start(Explorer, "\"" + v.E.Url + "\""); } catch (Exception ex) { DataStore.Log("browser: " + ex.Message); }
        }
    }

    void SetBusy(bool si, string text) {
        busy = si;
        updateButton.Enabled = !si;
        if (!si) management.Configure(Choice(), a.Login);
        if (text != null) state.Text = text;
    }

    void InUi(Action f) { try { if (!IsDisposed) BeginInvoke(f); } catch { } }

    Client RealRoute() {
        var account = AccountAccess.Active(a);
        if (account == null) throw new WriteException(Language.T("window.no_account_connected_press_account_2"));
        if (!GitHub.Connected) GitHub.SignIn(account);
        Writer.AccountSame(account.Login, GitHub.Login, GitHub.Exit);
        return GitHub.Writer();
    }

    void RefreshAll() {
        if (busy) return;
        var account = AccountAccess.Active(a);
        if (account == null) { state.Text = Language.T("window.no_account_connected_press_account_2"); return; }
        SetBusy(true, GitHub.Connected ? Language.T("window.downloading_github") : Language.T("window.connecting_github", "login", account.Login));
        new Thread(() => {
            string result;
            try {
                if (!GitHub.Connected) {
                    string real = GitHub.SignIn(account);
                    lock (this) {
                        if (!AccountAccess.Equal(real, account.Login)) AccountAccess.Rename(a, account.Login, real);
                        if (account.Origin == AccountAccess.OriginOld) account.Origin = AccountAccess.OriginGit;
                    }
                }
                string who = GitHub.Login;
                InUi(() => state.Text = Language.T("window.signed_reading_repos", "who", who));
                var newOnes = GitHub.Repo();
                int downloaded = 0, succeeded = 0, missingRepos = 0;
                var failed = new List<string>();
                lock (this) missingRepos = AccountAccess.Join(a, who, newOnes, n => n.Mine);
                InUi(() => { emptyReason = null; Render(); });
                int notRead = 0;
                foreach (var r in RepoNow().Where(x => !x.Missing && AccountAccess.Sees(x, who))) {
                    var r2 = r;
                    InUi(() => state.Text = Language.T("window.checking_status", "name", r2.Name));
                    var created = GitHub.Overview(r, r.Overview);
                    if (created.ReleaseError != null || created.TagError != null || created.CommitsSinceReleaseError != null || created.CommitError != null
                        || created.CollaboratorsError != null || created.LicenseError != null || created.ReadmeError != null || created.ReadmeDateError != null) notRead++;
                    r.Overview = created;
                    r.Stats = GitHub.ReadStats(r, r.Stats);
                    lock (history) History.Join(history, r.Name, r.Stats, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                    InUi(Page);
                }
                SaveHistory();
                foreach (var r in RepoNow().Where(x => x.Followed && !x.Missing && AccountAccess.Sees(x, who))) {
                    var r2 = r;
                    InUi(() => state.Text = Language.T("window.reading", "name", r2.Name));
                    try {
                        string start = DateTime.UtcNow.AddMinutes(-2).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        var arrived = GitHub.News(r.Name, r.Since);
                        lock (this) {
                            var index = new Dictionary<string, int>();
                            for (int i = 0; i < a.Items.Count; i++) index[a.Items[i].Key] = i;
                            foreach (var e in arrived) {
                                int i;
                                if (index.TryGetValue(e.Key, out i)) a.Items[i] = e; else { index[e.Key] = a.Items.Count; a.Items.Add(e); }
                            }
                            r.Since = start;
                        }
                        downloaded += arrived.Count;
                        succeeded++;
                    } catch (Exception ex) {
                        DataStore.Log("update " + r.Name + ": " + ex.Message);
                        failed.Add(r.Name + " (" + ex.Message + ")");
                    }
                }
                a.LastUpdate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                lock (this) DataStore.Save(a);
                result = Language.T("window.updated_at_as", "time", DateTime.Now.ToString("HH:mm"), "who", who) + " " + Language.N("window.new_changed_texts", downloaded) + " "
                      + Language.N("window.repos_updated", succeeded)
                      + (failed.Count > 0 ? ", " + Language.T("window.failed", "n", failed.Count, "list", string.Join("; ", failed)) : "")
                      + (missingRepos > 0 ? Language.T("window.no_longer_github_2", "missing", missingRepos) : "")
                      + (notRead > 0 ? Language.N("window.something_could_not_read_repos", notRead) : "") + ".";
                notUpdated = false;
                emptyReason = failed.Count > 0 ? Language.T("window.some_repos_not_update_see") : null;
            } catch (Exception ex) {
                DataStore.Log("update: " + ex.Message);
                result = Language.T("window.not_updated", "error", ex.Message, "part", (a.LastUpdate == null ? "" : Language.T("window.showing_data", "last_update", a.LastUpdate)));
                notUpdated = true;
                emptyReason = result;
            }
            InUi(() => { Recalculate(); Render(); SetBusy(false, result); });
        }) { IsBackground = true }.Start();
    }

    void Connect(bool before) {
        var gateway = new RealGateway();
        using (var g = new Wizard(a, gateway, before, Save)) g.ShowDialog(this);
        GitHub.Exit();
        Recalculate(); Render();
        if (a.Accounts.Count > 0) RefreshAll();
        else state.Text = Language.T("window.no_account_connected_press_account_3");
    }

    void Accounts() {
        if (busy) { state.Text = Language.T("common.wait_current_work_finish"); return; }
        using (var f = new AccountsDialog(a)) {
            f.ShowDialog(this);
            if (f.Disconnected != null) {
                var c = f.Disconnected;
                if (c.Origin == AccountAccess.OriginBrowser) CredentialStore.Delete(CredentialStore.NameOf(c.Login));
                AccountAccess.Disconnect(a, c.Login);
                GitHub.Exit();
                Save(); Recalculate(); Render();
                if (a.Accounts.Count > 0) RefreshAll();
                state.Text = Language.T("window.disconnected_repos_stay_archive_come", "login", c.Login);
            }
            if (f.Add) Connect(a.Accounts.Count == 0);
        }
    }

    void SearchModels() {
        new Thread(() => {
            try {
                var m = Ollama.Models();
                InUi(() => models = m);
            } catch { InUi(() => models = new List<string>()); }
        }) { IsBackground = true }.Start();
    }

    void Summarize() {
        if (busy) return;
        var sc = Choices(workLabel).Where(v => v.State == "work").ToList();
        if (sc.Count == 0) { state.Text = Language.T("window.first_choose_one_more_texts_2"); return; }
        string m = Ollama.Choose(models, a.Model);
        if (m == null) { state.Text = Language.T("window.no_ollama_model_start_ollama"); SearchModels(); return; }
        SetBusy(true, Language.T("window.summarizing_texts_computer_free", "n", sc.Count, "m", m));
        new Thread(() => {
            int done = 0, discarded = 0;
            string error = null;
            foreach (var v in sc) {
                try {
                    string r = Ollama.Summarize(m, v.Category, ItemSorter.TitleOf(v.E, a), v.E.Text ?? "");
                    if (Screener.Examine("", r, a.TrustedDomains).Count > 0) { discarded++; continue; }
                    lock (this) { var d = DecisionOf(v); d.Summary = r; d.SummaryFingerprint = v.Fingerprint; d.SummaryModel = m; }
                    done++;
                    int f2 = done;
                    InUi(() => state.Text = Language.T("window.summarized_of", "n", f2, "n2", sc.Count));
                } catch (Exception ex) { error = ex.Message; break; }
            }
            InUi(() => {
                Save(); Recalculate(); Render();
                SetBusy(false, Language.T("window.summaries_done", "done", done, "part", (discarded > 0 ? Language.T("window.discarded_suspicious", "discarded", discarded) : ""), "part2", (error != null ? Language.T("window.stopped", "error", error) : ".")));
            });
        }) { IsBackground = true }.Start();
    }

    void CreatePackage() {
        var r = Choice();
        var names = r != null ? new List<string> { r.Name }
                 : VisibleEntries().Where(v => v.State == "work").Select(v => v.E.Repo).Distinct().OrderBy(x => x).ToList();
        if (names.Count == 0) { state.Text = Language.T("window.nothing_work_2"); return; }
        string lastPath = null, text = null;
        foreach (var n in names) { text = Package.Text(a, n, entries, errors); lastPath = Package.Write(n, text); }
        bool clipboard = names.Count == 1 && Package.GoesToClipboard(a.Repo.FirstOrDefault(x => x.Name == names[0]));
        if (clipboard) try { Clipboard.SetText(text); } catch { }
        try { Process.Start(Explorer, "/select,\"" + lastPath + "\""); } catch { }
        state.Text = clipboard ? Language.T("window.package_written_copied_clipboard", "last", lastPath)
                   : names.Count == 1 ? Language.T("window.package_written_repo_private_dont", "last", lastPath)
                   : Language.T("window.packages_written", "names", names.Count, "packages", DataStore.Packages);
    }

    void Settings() {
        if (busy) { state.Text = Language.T("common.wait_current_work_finish"); return; }
        SearchModels();
        using (var f = new Form { Text = Language.T("window.settings"), ClientSize = new Size(S(520), S(470 + 74 + 118)), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                                  MinimizeBox = false, MaximizeBox = false, BackColor = Theme.Background, ForeColor = Theme.Text, Font = Font, AutoScaleMode = AutoScaleMode.None }) {
            Func<string, Font, Color, Rectangle, Label> makeLabel = (x, fo, co, re) => new Label { Text = x, Font = fo, ForeColor = co, Bounds = re, UseMnemonic = false, BackColor = Theme.Background };
            int m = S(24), lu = S(520) - m * 2, y0 = S(74);
            var l0 = makeLabel(Language.T("language.language"), Theme.Medium, Theme.Text, new Rectangle(m, S(16), lu, S(24)));
            var languages = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(m, S(44), S(220), S(28)) };
            foreach (var c in Language.Codes) languages.Items.Add(Language.NameOf(c));
            languages.SelectedIndex = Array.IndexOf(Language.Codes, Language.Code);
            var l1 = makeLabel(Language.T("window.model_summaries"), Theme.Medium, Theme.Text, new Rectangle(m, y0 + S(20), lu, S(24)));
            var s1 = makeLabel(Language.T("window.ollama_works_computer_free_offline"), Theme.Normal, Theme.Muted, new Rectangle(m, y0 + S(46), lu, S(22)));
            var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(m, y0 + S(72), lu, S(28)) };
            if (models.Count > 0) {
                foreach (var x in models) choice.Items.Add(x);
                choice.SelectedItem = Ollama.Choose(models, a.Model);
            } else { choice.Items.Add(Language.T("window.no_model_ollama_off_not")); choice.SelectedIndex = 0; choice.Enabled = false; }
            var l2 = makeLabel(Language.T("window.trusted_sites"), Theme.Medium, Theme.Text, new Rectangle(m, y0 + S(122), lu, S(24)));
            var s2 = makeLabel(Language.T("window.one_site_per_line_links"), Theme.Normal, Theme.Muted, new Rectangle(m, y0 + S(148), lu, S(44)));
            var t = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Text = string.Join("\r\n", a.TrustedDomains), BackColor = Color.FromArgb(24, 25, 29), ForeColor = Theme.Text,
                                  BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(m, y0 + S(196), lu, S(110)) };
            var l3 = makeLabel(Language.T("window.repos_folder"), Theme.Medium, Theme.Text, new Rectangle(m, y0 + S(318), lu, S(24)));
            var s3 = makeLabel(Language.T("window.optional_where_keep_repos_computer"), Theme.Normal, Theme.Muted, new Rectangle(m, y0 + S(342), lu, S(22)));
            var folder = new TextBox { Text = a.ReposFolder ?? "", BackColor = Color.FromArgb(24, 25, 29), ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(m, y0 + S(368), lu - S(110), S(26)) };
            var sf = DarkButton(Language.T("window.browse")); sf.SetBounds(m + lu - S(100), y0 + S(366), S(100), S(30));
            sf.Click += delegate {
                using (var d = new FolderBrowserDialog { Description = Language.T("window.folder_contains_repos"), ShowNewFolderButton = false, SelectedPath = folder.Text })
                    if (d.ShowDialog(f) == DialogResult.OK) folder.Text = d.SelectedPath;
            };
            var l4 = makeLabel(Language.T("updates.new_versions"), Theme.Medium, Theme.Text, new Rectangle(m, y0 + S(410), lu, S(24)));
            var notice = new CheckBox { Text = Language.T("updates.check_startup"), Checked = !a.UpdateNoticeOff, ForeColor = Theme.Text, BackColor = Theme.Background, UseMnemonic = false, AutoSize = false, Bounds = new Rectangle(m, y0 + S(438), lu - S(170), S(28)) };
            var now = DarkButton(Language.T("updates.check_now")); now.SetBounds(m + lu - S(160), y0 + S(436), S(160), S(32));
            var result = makeLabel("", Theme.Normal, Theme.Muted, new Rectangle(m, y0 + S(472), lu, S(40)));
            now.Click += delegate {
                now.Enabled = false;
                result.Text = Language.T("updates.checking");
                new Thread(() => {
                    NewVersion n = null; string text;
                    try { n = UpdateCheck.Search(); text = n == null ? Language.T("updates.already_latest_version_project_manager", "v", Program.Version) : ""; }
                    catch (Exception ex) { DataStore.Log("updates: " + ex.Message); text = Language.T("updates.cant_check_updates", "err", ex.Message.TrimEnd('.', ' ')); }
                    try {
                        f.BeginInvoke((Action)(() => {
                            result.Text = text; now.Enabled = true;
                            if (n != null) ShowNotice(n, f);
                        }));
                    } catch (InvalidOperationException) { }
                }) { IsBackground = true }.Start();
            };
            var ok = CreateButton(Language.T("common.save"), true); ok.SetBounds(m + lu - S(130), y0 + S(528), S(130), S(36)); ok.DialogResult = DialogResult.OK;
            var no = DarkButton(Language.T("common.cancel")); no.SetBounds(m + lu - S(130) - S(10) - S(110), y0 + S(528), S(110), S(36)); no.DialogResult = DialogResult.Cancel;
            f.Controls.AddRange(new Control[] { l0, languages, l1, s1, choice, l2, s2, t, l3, s3, folder, sf, l4, notice, now, result, ok, no });
            f.CancelButton = no;
            f.Load += delegate { Theme.DarkBar(f); ok.Focus(); t.SelectionLength = 0; };
            if (SettingsScreenshot != null) { SettingsScreenshot(f); return; }
            if (f.ShowDialog(this) != DialogResult.OK) return;
            if (choice.Enabled && choice.SelectedItem != null) a.Model = Convert.ToString(choice.SelectedItem);
            a.UpdateNoticeOff = !notice.Checked;
            var newItems = t.Lines.Select(x => x.Trim().ToLowerInvariant().TrimStart('*', '.')).Where(x => x.Length > 2 && x.Contains(".") && !x.Contains(" ") && !x.Contains("/")).Distinct().ToList();
            if (newItems.Count > 0) a.TrustedDomains = newItems;
            string created = folder.Text.Trim();
            if (created.Length == 0 || Directory.Exists(created)) a.ReposFolder = DataStore.ReposFolder = created;
            else MessageBox.Show(this, Language.T("window.folder_doesnt_exist_repos_folder"), Text);
            Save();
            if (ChangeLanguage(Language.Codes[Math.Max(0, languages.SelectedIndex)])) return;
            Recalculate(); Render();
            state.Text = Language.T("window.settings_saved");
        }
    }
}
