using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

public class ManagementPage : Panel {
    class Entry { public ThemedButton EntryButton; public Label Text; public Func<RepoFacts, string> Off, Describe, Name; public bool OnlyWithRepo = true, OrgOnly, MissingOnly; }

    readonly WriteGateway P;
    readonly List<Entry> entries = new List<Entry>();
    public Func<bool> BusyCheck = () => false;
    public event EventHandler Changed;
    public event Action<string, string> Renamed;
    public event Action<string> Deleted;
    public event Action<string> RemoveRequested;
    public RepoFacts Done;
    public string Error;
    public RepoInfo Repo;
    public Label State;
    int y, round;
    string login;

    int S(int v) { return Theme.S(v); }

    public ManagementPage(WriteGateway p) {
        P = p;
        BackColor = Color.White; AutoScroll = true; Visible = false;
        y = S(14);
        State = new Label { Location = new Point(S(24), y), Size = new Size(S(800), S(44)), ForeColor = Theme.Gray, BackColor = Color.White, UseMnemonic = false };
        Controls.Add(State);
        y += S(44);
        Title(Language.T("management.new"));
        var create = Line(Language.T("management.create_repo"), null, Language.T("management.personal_organization_always_starts_out"), f => null, delegate { Create(); });
        create.OnlyWithRepo = false;
        Title(Language.T("management.this_repo"));
        Line(Language.T("common.rename"), null, Language.T("management.github_redirects_old_links_new"), f => Permissions.Because(RepoAction.Name, f), delegate { ChangeName(); });
        Line(Language.T("management.change_description"), null, Language.T("management.sentence_shown_top_repo"), f => Permissions.Because(RepoAction.Description, f), delegate { ChangeDescription(); });
        Line(Language.T("common.change_website"), null, null, f => Permissions.Because(RepoAction.Description, f), delegate { ChangeWebsite(); }).Describe = f => f.Website.Length == 0
            ? Language.T("management.no_website_link_shown_next")
            : Language.T("management.now", "site", Screener.MakeVisible(f.Website));
        Line(Language.T("management.visibility"), f => f.Private ? Language.T("common.make_public") : Language.T("management.make_private"), null, f => Permissions.Because(RepoAction.Visibility, f), delegate { Visibility(); }).Describe = f => f.Private
            ? Language.T("management.anyone_see_all_history_reports")
            : Language.T("management.only_invited_people_see_copies");
        Line(Language.T("common.archive"), f => f.Archived ? Language.T("management.unarchive") : Language.T("common.archive"), null, f => Permissions.Because(RepoAction.Archive, f), delegate { ArchiveRepo(); }).Describe = f => f.Archived
            ? Language.T("management.becomes_editable_again")
            : Language.T("management.becomes_read_only_seen_not");
        Line(Language.T("management.transfer"), null, Language.T("management.hands_another_account_organization_type"), f => Permissions.Because(RepoAction.Transfer, f), delegate { Transfer(); });
        Title(Language.T("management.content"));
        Line(Language.T("management.upload_folder"), null, Language.T("management.all_files_single_save_first"), f => Permissions.Because(RepoAction.Content, f), delegate { Open(new UploadDialog(P, Repo.Name)); });
        Line(Language.T("management.single_files"), null, Language.T("management.see_files_edit_text_one"), f => Permissions.Because(RepoAction.Content, f), delegate { Open(new FilesDialog(P, Repo.Name)); });
        Title(Language.T("management.versions_people"));
        Line(Language.T("management.releases"), null, Language.T("management.create_edit_delete_publish_versions"), f => Permissions.Because(RepoAction.Release, f), delegate { Open(new ReleasesDialog(P, Repo.Name)); });
        Line(Language.T("common.collaborators"), null, Language.T("management.add_remove_people_who_work"), f => Permissions.Because(RepoAction.Collaborators, f), delegate { Open(new CollaboratorsDialog(P, Repo.Name)); });
        var org = Line(Language.T("management.open_organization"), null, Language.T("management.organization_members_teams_settings_managed"), f => null, delegate { P.Gateway.Open("https://github.com/orgs/" + Done.Owner + "/people"); });
        org.OrgOnly = true;
        Title(Language.T("management.danger_zone"));
        var via = Line(Language.T("management.delete_repo"), null, Language.T("management.disappears_everyone_history_releases_type"), f => Permissions.Because(RepoAction.Deletion, f), delegate { DeleteRepo(); });
        via.EntryButton.Background = Theme.Red; via.EntryButton.TextColor = Color.White; via.EntryButton.Line = Color.Empty;
        Title(Language.T("management.repo_gone"));
        var away = Line(Language.T("missing.remove_list"), null, Language.T("missing.github_no_longer_lists_remove"), f => null, delegate { if (RemoveRequested != null && Repo != null) RemoveRequested(Repo.Name); });
        away.OnlyWithRepo = false; away.MissingOnly = true;
        VisibleChanged += delegate { if (Visible && Repo != null && Done == null) Read(); };
        Resize += delegate { foreach (var v in entries) v.Text.Width = Math.Max(S(200), ClientSize.Width - v.Text.Left - S(24)); State.Width = Math.Max(S(200), ClientSize.Width - S(48)); };
        Redo();
    }

    void Title(string text) {
        y += S(8);
        Controls.Add(new Label { Text = text, Font = Theme.Medium, ForeColor = Theme.Ink, BackColor = Color.White, Location = new Point(S(24), y), Size = new Size(S(400), S(26)), UseMnemonic = false });
        y += S(30);
    }

    Entry Line(string name, Func<RepoFacts, string> nameFrom, string says, Func<RepoFacts, string> disabled, EventHandler click) {
        var b = new ThemedButton { Text = name, Location = new Point(S(24), y), Size = new Size(S(190), S(34)) };
        b.Click += (o, e) => { if (BusyCheck()) { State.Text = Language.T("common.wait_current_work_finish"); return; } click(o, e); };
        var t = new Label { Text = says, ForeColor = Theme.Gray, BackColor = Color.White, Location = new Point(S(226), y - S(2)), Size = new Size(S(640), S(38)), UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft };
        Controls.Add(b); Controls.Add(t);
        var v = new Entry { EntryButton = b, Text = t, Off = disabled, Name = nameFrom, Describe = says == null ? null : (Func<RepoFacts, string>)(f => says) };
        entries.Add(v);
        y += S(44);
        return v;
    }

    public ThemedButton ButtonOf(string name) { return entries.Select(v => v.EntryButton).First(b => b.Text == name); }
    public string TextOf(ThemedButton b) { return entries.First(v => v.EntryButton == b).Text.Text; }

    public void Configure(RepoInfo r, string who) {
        bool same = Repo == r && login == who;
        Repo = r; login = who; P.Login = who;
        if (same && (Done != null || r == null || Error != null)) { Redo(); return; }
        Done = null; Error = null; round++;
        Redo();
        if (Visible && r != null && !r.Missing) Read();
    }

    public void Read() {
        if (Repo == null || Repo.Missing) return;
        if (BusyCheck()) { State.Text = Language.T("management.waiting_current_work_finish"); return; }
        int myRound = ++round; string name = Repo.Name;
        Done = null; Error = null;
        Redo();
        new Thread(() => {
            RepoFacts f = null; string e = null;
            try { f = Writer.Fetch(P.Route(), name); }
            catch (WriteException x) { e = x.Message; }
            catch (AccessException x) { e = x.Message; }
            catch (Exception x) { DataStore.Log("management: " + x.GetType().Name); e = Language.T("management.cant_read_repo_github"); }
            try { BeginInvoke((Action)(() => { if (myRound != round) return; Done = f; Error = e; Redo(); })); } catch (InvalidOperationException) { }
        }) { IsBackground = true }.Start();
    }

    void Redo() {
        SuspendLayout();
        foreach (var v in entries) {
            if (v.OrgOnly) v.EntryButton.Visible = v.Text.Visible = Done != null && Done.Organization;
            if (v.Name != null && Done != null) v.EntryButton.Text = v.Name(Done);
            string because;
            if (v.MissingOnly) because = Repo == null ? Language.T("management.choose_repo_left") : Repo.Missing ? null : Language.T("management.remove_list_only_gone");
            else if (!v.OnlyWithRepo) because = null;
            else if (Repo == null) because = Language.T("management.choose_repo_left");
            else if (Repo.Missing) because = Language.T("management.repo_no_longer_github");
            else if (Done == null) because = Error != null ? Language.T("management.dont_know_what_could_not") : Language.T("management.reading_permissions_github");
            else because = v.Off(Done);
            v.EntryButton.Enabled = because == null;
            v.Text.Text = because ?? (v.Describe == null ? "" : v.Describe(Done));
            v.Text.ForeColor = because != null && Repo != null && !v.MissingOnly ? Theme.AmberDark : Theme.Gray;
        }
        State.ForeColor = Error != null ? Theme.AmberDark : Theme.Gray;
        State.Text = Repo == null ? Language.T("management.create_new_repo_everything_else")
            : Repo.Missing ? Language.T("management.repo_no_longer_github")
            : Error ?? (Done == null ? Language.T("management.reading_github_what") : Language.T("management.github_still_checks_every_action", "done", Screener.MakeVisible(OperationDialog.Describe(Done))));
        ResumeLayout();
    }

    void End() { Read(); if (Changed != null) Changed(this, EventArgs.Empty); }

    void Open(OperationDialog f) {
        bool changed;
        using (f) { P.Open(f); changed = f.Changed; }
        if (changed) End(); else Read();
    }

    void Create() {
        using (var m = new NewRepoDialog(P, Repo == null ? null : Repo.Owner)) {
            P.Open(m);
            if (m.Changed && Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    bool Ask(ConfirmDialog k) { using (k) return P.Open(k) == DialogResult.OK; }

    void ChangeName() {
        string oldName = Repo.Name, newName;
        using (var m = new TextInputDialog(P, Language.T("common.rename"), Language.T("management.new_name_without_owner_letters"), Done.ShortName, false, Language.T("management.continue"), null) { Valid = Writer.ValidRepoName }) {
            if (P.Open(m) != DialogResult.OK) return;
            newName = m.Value.Text.Trim();
        }
        string done = null;
        if (!Ask(new ConfirmDialog(P, oldName, Language.T("common.rename"), Language.T("common.rename"), true, false,
            (c, f) => Language.T("management.new_name_folders_computers_people", "new", Screener.MakeVisible(f.Owner + "/" + newName)),
            (c, f, s) => done = Writer.Rename(c, f, s, newName)))) return;
        Done = null;
        if (Renamed != null) Renamed(oldName, done);
    }

    void ChangeDescription() {
        var seen = Done;
        using (var m = new TextInputDialog(P, Language.T("management.change_description"), Language.T("management.sentence_shown_top_repo_left"), Done.Description, true, Language.T("common.save"), (c, t) => Writer.ChangeDescription(c, seen, t)))
            if (P.Open(m) == DialogResult.OK) End(); else Read();
    }

    public static string ExplainWebsite { get { return Language.T("management.projects_website_website_github_link"); } }

    void ChangeWebsite() {
        var seen = Done;
        using (var m = new TextInputDialog(P, Language.T("common.change_website"), ExplainWebsite, Done.Website, false, Language.T("common.save"), (c, t) => Writer.ChangeWebsite(c, seen, t)) { Valid = Writer.ValidWebsite })
            if (P.Open(m) == DialogResult.OK) End(); else Read();
    }

    public static string PublishStory(PublishCheck k) {
        string t = Language.T("publish.searched_passwords_keys_history_another", "file", Language.N("publish.files", k.Scanned), "branches", Language.N("publish.branches", k.Branches), "commit", Language.N("publish.commits", k.Commit), "history_files", Language.N("publish.files", k.HistoryScanned),
                "issues", Language.N("publish.reports", k.Issue), "pull", Language.N("publish.pull_requests", k.Pull), "release", Language.N("publish.releases", k.Release), "comments", Language.N("publish.comments", k.Comments)) + "\n"
             + Language.T("publish.all_becomes_public_what_cant", "wiki", k.Wiki ? Language.T("publish.wiki_one") : "", "attachments", k.Assets > 0 ? Language.T("publish.releases_which_dont_open", "how_many", Language.N("publish.attachments", k.Assets)) : "");
        if (k.PlainEmail > 0) t += "\n" + Language.N("publish.warning_commits_show_real_email", k.PlainEmail);
        if (k.Notices.Count > 0) t += "\n" + Language.T("publish.check_not_blocking", "what", Screener.MakeVisible(k.Notices[0].ToString()), "others", k.Notices.Count > 1 ? Language.T("publish.and_more", "n", k.Notices.Count - 1) : "");
        return t;
    }

    void Visibility() {
        string name = Repo.Name;
        if (Done.Private) {
            PublishCheck k = null;
            ConfirmDialog confirm = null;
            confirm = new ConfirmDialog(P, name, Language.T("common.make_public"), Language.T("common.make_public"), true, true, (c, f) => {
                k = Writer.CheckForPublish(c, f, confirm.Step);
                Writer.StopIfNotClean(k);
                return PublishStory(k);
            }, (c, f, s) => Writer.MakePublic(c, f, s, k));
            if (Ask(confirm)) End();
        } else if (Ask(new ConfirmDialog(P, name, Language.T("management.make_private"), Language.T("management.make_private"), false, false,
            (c, f) => Language.T("management.only_invited_people_see_stars"), (c, f, s) => Writer.MakePrivate(c, f)))) End();
    }

    void ArchiveRepo() {
        bool via = !Done.Archived;
        if (Ask(new ConfirmDialog(P, Repo.Name, via ? Language.T("common.archive") : Language.T("management.unarchive"), via ? Language.T("common.archive") : Language.T("management.unarchive"), false, false,
            (c, f) => via ? Language.T("common.repo_becomes_read_only_no") : Language.T("management.repo_changed_again"),
            (c, f, s) => Writer.ArchiveRepo(c, f, via)))) End();
    }

    void Transfer() {
        string oldName = Repo.Name, a;
        using (var m = new TextInputDialog(P, Language.T("management.transfer"), Language.T("management.name_account_organization_receives_person"), "", false, Language.T("management.continue"), null) { Valid = Writer.ValidLogin }) {
            if (P.Open(m) != DialogResult.OK) return;
            a = m.Value.Text.Trim();
        }
        string done = null;
        if (!Ask(new ConfirmDialog(P, oldName, Language.T("management.transfer"), Language.T("management.transfer"), true, true,
            (c, f) => { string type = Writer.AccountType(c, a); return Language.T("management.repo_goes_get_back_whoever", "to", Screener.MakeVisible(a), "part", (type == "Organization" ? Language.T("management.an_organization") : Language.T("management.a_person"))); },
            (c, f, s) => done = Writer.Transfer(c, f, s, a)))) return;
        Done = null;
        if (done != null && Renamed != null) Renamed(oldName, done);
        else { State.Text = Language.T("management.request_sent_repo_changes_hands", "to", a); if (Changed != null) Changed(this, EventArgs.Empty); }
    }

    void DeleteRepo() {
        string name = Repo.Name;
        bool via;
        using (var f = new DeleteRepoDialog(P, name)) { P.Open(f); via = f.Deleted; }
        if (via) { Done = null; if (Deleted != null) Deleted(name); }
    }
}
