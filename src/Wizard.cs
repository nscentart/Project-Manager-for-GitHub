using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

public partial class Wizard : Dialog {
    readonly Archive a;
    readonly IGateway gateway;
    readonly Action save;
    readonly bool before;
    volatile int round;
    volatile bool closed;
    bool connect;
    DeviceCode code;
    List<RepoInfo> found = new List<RepoInfo>();
    HashSet<string> ticks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool fill;

    public string Connected;
    public Panel StartPanel, CodePanel, RepoPanel;
    public ThemedButton BBrowser, BGit, RemindLaterButton, OpenButton, BackButton, NextButton, AllButton, NoneButton;
    public ListView RepoListView;
    public Label Notice;
    Label codeLabel, waitLabel, whoLabel, accountsLabel, lOrg;
    ComboBox filterCombo;

    int S(int v) { return Theme.S(v); }

    Label AddLabel(Control place, string text, Font f, Color c, int y, int alt) {
        var l = new Label { Text = text, Font = f, ForeColor = c, BackColor = Theme.Background, Bounds = new Rectangle(S(28), y, S(584), alt), UseMnemonic = false };
        place.Controls.Add(l);
        return l;
    }

    ThemedButton CreateButton(Control place, string text, bool primary, Rectangle r, EventHandler click) {
        var b = new ThemedButton { Text = text, Bounds = r, Background = primary ? Theme.Azure : Theme.Capsule, TextColor = primary ? Color.White : Theme.Text, Line = primary ? Color.Empty : Theme.Line };
        b.Click += click;
        place.Controls.Add(b);
        return b;
    }

    Panel Page() {
        var p = new Panel { Bounds = new Rectangle(0, 0, S(640), S(460)), BackColor = Theme.Background, Visible = false };
        Controls.Add(p);
        return p;
    }

    public Wizard(Archive archive, IGateway p, bool firstTime, Action saveCallback) {
        a = archive; gateway = p; before = firstTime; save = saveCallback;
        using (var g = CreateGraphics()) Theme.K = g.DpiX / 96f;
        Text = firstTime ? Language.T("wizard.project_manager_first_start") : Language.T("common.connect_another_account");
        ClientSize = new Size(S(640), S(520));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        try { Icon = Theme.AppIcon(); } catch { }

        Notice = new Label { Bounds = new Rectangle(S(28), S(404), S(584), S(44)), ForeColor = Theme.Amber, BackColor = Theme.Background, UseMnemonic = false };
        Controls.Add(Notice);
        int yb = S(464), hb = S(38), right = S(612);

        StartPanel = Page();
        AddLabel(StartPanel, Language.T("wizard.step_1_2"), Theme.Small, Theme.Muted, S(20), S(20));
        AddLabel(StartPanel, firstTime ? Language.T("wizard.lets_connect_github_account") : Language.T("wizard.connect_another_github_account"), Theme.Large, Theme.Text, S(40), S(34));
        string text = Language.T("wizard.program_reads_repos_changes_only", "part", (firstTime
              ? Language.T("wizard.ill_guide_through_two_steps")
              : Language.T("wizard.connect_many_accounts_like_switch")));
        AddLabel(StartPanel, text, Theme.Normal, Theme.Text, S(84), S(222));
        var browserNote = gateway.Browser ? null : AddLabel(StartPanel, gateway.Git
            ? Language.T("wizard.browser_sign_not_active_yet")
            : Language.T("wizard.browser_sign_not_active_yet_2"), Theme.Normal, Theme.Amber, S(308), S(44));
        BBrowser = CreateButton(StartPanel, Language.T("wizard.sign_github"), true, new Rectangle(S(28), S(356), S(190), hb), delegate { FromBrowser(); });
        BGit = CreateButton(StartPanel, Language.T("wizard.use_gits_access_pc"), false, new Rectangle(S(228), S(356), S(250), hb), delegate { GitFrom(); });
        BBrowser.Visible = gateway.Browser;
        BGit.Visible = gateway.Git;
        if (!gateway.Browser) BGit.Left = S(28);
        RemindLaterButton = CreateButton(this, firstTime ? Language.T("wizard.later") : Language.T("common.cancel"), false, new Rectangle(right - S(120), yb, S(120), hb), delegate { Close(); });

        CodePanel = Page();
        AddLabel(CodePanel, Language.T("wizard.step_1_2"), Theme.Small, Theme.Muted, S(20), S(20));
        AddLabel(CodePanel, Language.T("wizard.type_code_github"), Theme.Large, Theme.Text, S(40), S(34));
        codeLabel = AddLabel(CodePanel, "", new Font("Consolas", 30f, FontStyle.Bold), Theme.Lime, S(90), S(66));
        codeLabel.TextAlign = ContentAlignment.MiddleCenter;
        string steps = Language.T("wizard.1_press_button_below_copy");
        if (!firstTime) steps += Language.T("wizard.page_uses_account_signed_browser");
        AddLabel(CodePanel, steps, Theme.Normal, Theme.Text, S(170), S(120));
        waitLabel = AddLabel(CodePanel, "", Theme.Normal, Theme.Label, S(300), S(44));
        OpenButton = CreateButton(CodePanel, Language.T("common.copy_code_open_github"), true, new Rectangle(S(28), S(350), S(260), hb), delegate { OpenPage(); });
        BackButton = CreateButton(CodePanel, Language.T("wizard.back"), false, new Rectangle(S(298), S(350), S(110), hb), delegate { round++; Vai(StartPanel); });

        RepoPanel = Page();
        AddLabel(RepoPanel, Language.T("wizard.step_2_2"), Theme.Small, Theme.Muted, S(20), S(20));
        whoLabel = AddLabel(RepoPanel, "", Theme.Large, Theme.Text, S(40), S(34));
        AddLabel(RepoPanel, Language.T("wizard.ones_follow_download_reports_change"), Theme.Normal, Theme.Muted, S(76), S(22));
        filterCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(S(28), S(106), S(300), S(28)) };
        filterCombo.SelectedIndexChanged += delegate { if (!fill) Lines(); };
        RepoPanel.Controls.Add(filterCombo);
        AllButton = CreateButton(RepoPanel, Language.T("wizard.all"), false, new Rectangle(right - S(190), S(104), S(90), S(30)), delegate { Tick(true); });
        NoneButton = CreateButton(RepoPanel, Language.T("wizard.none"), false, new Rectangle(right - S(90), S(104), S(90), S(30)), delegate { Tick(false); });
        RepoListView = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.None, BackColor = Theme.Capsule, ForeColor = Theme.Text,
                               BorderStyle = BorderStyle.None, Bounds = new Rectangle(S(28), S(144), S(584), S(206)), MultiSelect = false, ShowItemToolTips = false };
        RepoListView.Columns.Add(Language.T("common.repo"), S(350)); RepoListView.Columns.Add(Language.T("wizard.whose"), S(130)); RepoListView.Columns.Add(Language.T("common.who_sees"), S(80));
        RepoListView.ItemChecked += (o, e) => {
            if (fill) return;
            var r = (RepoInfo)e.Item.Tag;
            if (e.Item.Checked) ticks.Add(r.Name); else ticks.Remove(r.Name);
            Counted();
        };
        RepoPanel.Controls.Add(RepoListView);
        accountsLabel = AddLabel(RepoPanel, "", Theme.Normal, Theme.Text, S(354), S(22));
        lOrg = AddLabel(RepoPanel, "", Theme.Small, Theme.Amber, S(376), S(40));
        NextButton = CreateButton(RepoPanel, Language.T("wizard.finish"), true, new Rectangle(right - S(120), S(0), S(120), hb), delegate { ChosenRepos(); });
        NextButton.Parent = this; NextButton.Top = yb;

        FormClosing += (o, e) => { if (connect) e.Cancel = true; };
        FormClosed += delegate { closed = true; round++; };
        Load += delegate { Theme.DarkBar(this); };
        Vai(StartPanel);
    }

    public void Vai(Panel p) {
        foreach (var x in new[] { StartPanel, CodePanel, RepoPanel }) x.Visible = x == p;
        Notice.Visible = p != RepoPanel;
        BackButton.Enabled = RemindLaterButton.Enabled = true;
        RemindLaterButton.Visible = p == StartPanel || p == CodePanel;
        NextButton.Visible = p == RepoPanel;
        Notice.Text = "";
        foreach (var b in new[] { BBrowser, BGit }) b.Enabled = true;
        Notice.BringToFront();
    }

    void Work<T>(Func<T> act, Action<T> then, Action<string> badly) {
        int myTurn = round;
        new Thread(() => {
            T r = default(T); string error = null;
            try { r = act(); }
            catch (AccessException ex) { error = ex.Message; }
            catch (GitHubException ex) { error = ex.Message; }
            catch (Exception ex) { DataStore.Log("wizard: " + ex.GetType().Name); error = ex is NoNetworkException || ex is WriteException ? ex.Message : Language.T("wizard.something_went_wrong_try_again"); }
            try {
                BeginInvoke((Action)(() => {
                    if (closed || myTurn != round) return;
                    if (error != null) badly(error); else then(r);
                }));
            } catch (InvalidOperationException) { }
        }) { IsBackground = true }.Start();
    }

    void Mistake(string text) { Vai(StartPanel); Notice.Text = text; }

    void FromBrowser() {
        BBrowser.Enabled = BGit.Enabled = false;
        Notice.Text = Language.T("wizard.requesting_code_github");
        Work(gateway.Ask, c => {
            ShowCode(c);
            int current = round;
            Work(() => gateway.Wait(c, () => closed || current != round), t => { if (t != null) Connect(t, AccountAccess.OriginBrowser); }, Mistake);
        }, Mistake);
    }

    public void ShowCode(DeviceCode c) {
        code = c;
        codeLabel.Text = c.ForUser;
        waitLabel.Text = "";
        Vai(CodePanel);
    }

    void OpenPage() {
        gateway.Copy(code.ForUser);
        gateway.Open(code.Page);
        waitLabel.Text = Language.T("wizard.code_copied_paste_github_page");
    }

    void GitFrom() {
        BBrowser.Enabled = BGit.Enabled = false;
        Notice.Text = Language.T("wizard.asking_git_access_sign_window");
        Work(gateway.GitFrom, t => Connect(t, AccountAccess.OriginGit), Mistake);
    }

    class Detection { public string Login; public List<RepoInfo> Repo; public List<string> Org; }

    void Connect(string token, string origin) {
        Notice.Text = Language.T("wizard.signed_reading_repos");
        connect = true;
        BackButton.Enabled = RemindLaterButton.Enabled = false;
        Work(() => {
            var t = new Detection { Login = gateway.LoginOf(token) };
            if (origin == AccountAccess.OriginGit && a.Accounts.Any(x => AccountAccess.Equal(x.Login, t.Login))) throw new AccessException(Language.T("wizard.git_uses_account_which_already", "login", t.Login));
            t.Repo = gateway.RepoOf(token, t.Login);
            try { t.Org = gateway.OrganizationsOf(token, t.Login); } catch (Exception ex) { DataStore.Log("organizations: " + ex.GetType().Name); t.Org = new List<string>(); }
            if (origin == AccountAccess.OriginBrowser) gateway.Store(t.Login, token);
            return t;
        }, t => {
            connect = false;
            var already = new HashSet<string>(a.Repo.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            AccountAccess.Add(a, t.Login, origin);
            AccountAccess.Join(a, t.Login, t.Repo, x => false);
            save();
            Connected = t.Login;
            var repos = a.Repo.Where(x => AccountAccess.Sees(x, t.Login) && !x.Missing).ToList();
            ShowRepo(t.Login, repos, t.Org, repos.Where(x => already.Contains(x.Name) ? x.Followed : x.Mine));
        }, e => { connect = false; Mistake(e); });
    }

    public void ShowRepo(string login, List<RepoInfo> repo, List<string> org, IEnumerable<RepoInfo> choices) {
        found = repo;
        ticks = new HashSet<string>(choices.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        whoLabel.Text = Language.T("wizard.youre_what_want_follow", "login", login);
        using (var g = whoLabel.CreateGraphics()) whoLabel.Font = Theme.MeasureWidth(g, whoLabel.Text, Theme.Large) > whoLabel.Width ? Theme.Medium : Theme.Large;
        fill = true;
        filterCombo.Items.Clear();
        filters = AccountAccess.Filters(a);
        foreach (var f in filters) filterCombo.Items.Add(f[1]);
        filterCombo.SelectedIndex = 0;
        fill = false;
        var empty = (org ?? new List<string>()).Where(o => !repo.Any(r => AccountAccess.Equal(r.Owner, o))).ToList();
        lOrg.Text = empty.Count == 0 ? "" : Language.T("wizard.see_no_repos_either_empty", "empty", string.Join(", ", empty));
        Vai(RepoPanel);
        Lines();
    }

    List<string[]> filters = new List<string[]>();

    void Lines() {
        string f = filterCombo.SelectedIndex >= 0 && filterCombo.SelectedIndex < filters.Count ? filters[filterCombo.SelectedIndex][0] : AccountAccess.FilterAll;
        fill = true;
        RepoListView.BeginUpdate();
        RepoListView.Items.Clear();
        foreach (var r in found.Where(x => AccountAccess.Pass(x, a.Login, f)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
            string who = r.Organization ? Language.T("common.organization") : AccountAccess.Equal(r.Owner, a.Login) ? Language.T("wizard.yours") : Language.T("wizard.someone_elses");
            RepoListView.Items.Add(new ListViewItem(new[] { r.Name, who, r.Private ? Language.T("common.private") : Language.T("common.public") }) { Tag = r, Checked = ticks.Contains(r.Name) });
        }
        RepoListView.EndUpdate();
        fill = false;
        Counted();
    }

    void Counted() {
        accountsLabel.Text = found.Count == 0 ? Language.T("wizard.github_shows_no_repos_account")
                    : Language.T("wizard.you_chose_of", "ticks", ticks.Count, "found", found.Count);
    }

    void Tick(bool si) { foreach (ListViewItem i in RepoListView.Items) i.Checked = si; }

    void ChosenRepos() {
        foreach (var r in found) {
            bool s = ticks.Contains(r.Name);
            if (s && !r.Followed) r.Since = null;
            r.Followed = s;
        }
        if (a.ReposFolder == null) { a.ReposFolder = ""; DataStore.ReposFolder = ""; }
        save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
