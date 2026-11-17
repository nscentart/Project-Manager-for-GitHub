using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class CollaboratorsDialog : OperationDialog {
    public ListView Lines;
    public TextBox NewName;
    public ComboBox Role;
    public Label RoleNote;
    public ThemedButton AddButton, ChangeButton, RemoveButton, CloseButton;
    public RepoFacts Done;
    readonly string repo;
    static string[] Words { get { return new[] { Language.T("management.read"), Language.T("management.read_sort_reports"), Language.T("management.write"), Language.T("management.manage"), Language.T("management.administer") }; } }

    public CollaboratorsDialog(WriteGateway p, string repoName) : base(p, Language.T("common.collaborators"), 680, 560) {
        repo = repoName;
        AddLabel(Language.T("management.who_works", "repo", V(repo)), Theme.Large, Theme.Text, S(16), S(34));
        Lines = CreateList(new Rectangle(M, S(56), Lu, S(230)), new[] { Language.T("common.account"), Language.T("management.role"), Language.T("common.status") }, new[] { 280, 180, 150 });
        AddLabel(Language.T("management.add_account_name_github"), Theme.Normal, Theme.Muted, S(296), S(20));
        NewName = Field("", new Rectangle(M, S(318), S(280), S(26)), false);
        NewName.TextChanged += delegate { Redo(); };
        Role = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(M + S(290), S(318), Lu - S(290), S(28)) };
        Role.Items.AddRange(Words);
        Role.SelectedIndex = 2;
        Controls.Add(Role);
        RoleNote = AddLabel("", Theme.Small, Theme.Amber, S(350), S(36));
        Line(S(390), S(64));
        int y = S(506);
        AddButton = CreateButton(Language.T("management.add"), true, new Rectangle(M, y, S(120), S(38)), delegate { Add(); });
        ChangeButton = CreateButton(Language.T("management.change_role"), false, new Rectangle(M + S(130), y, S(130), S(38)), delegate { Change(); });
        RemoveButton = Red(Language.T("management.remove"), new Rectangle(M + S(270), y, S(110), S(38)), delegate { Remove(); });
        CloseButton = CreateButton(Language.T("common.close"), false, new Rectangle(M + Lu - S(110), y, S(110), S(38)), delegate { Close(); });
        Shown += delegate { ReadFileAgain(); };
        Redo();
    }

    public RepoPerson Choice() { return Lines.SelectedItems.Count == 0 ? null : (RepoPerson)Lines.SelectedItems[0].Tag; }
    string RoleChosen() { return Writer.Roles[Math.Max(0, Role.SelectedIndex)]; }

    static string Word(string r) {
        switch (r) {
            case "read": case "pull": return Words[0];
            case "triage": return Words[1];
            case "write": case "push": return Words[2];
            case "maintain": return Words[3];
            case "admin": return Words[4];
            default: return V(r);
        }
    }

    protected override bool Can(ThemedButton b) {
        if (b == CloseButton) return true;
        if (Done == null) return false;
        if (b == AddButton) return Writer.ValidLogin(NewName.Text.Trim());
        var s = Choice();
        if (s == null) return false;
        return b != ChangeButton || (Done.Organization && s.Invitation == 0);
    }

    void ReadFileAgain() {
        string name = repo;
        Work(c => { var f = Writer.Fetch(c, name); Permissions.Allow(RepoAction.Collaborators, f); return new KeyValuePair<RepoFacts, List<RepoPerson>>(f, Writer.Collaborators(c, f)); }, x => {
            Done = x.Key;
            Lines.Items.Clear();
            foreach (var r in x.Value) Lines.Items.Add(new ListViewItem(new[] { V(r.Name), Word(r.Role), r.Invitation != 0 ? Language.T("management.invitation_accept") : Language.T("management.active") }) { Tag = r });
            Role.Enabled = Done.Organization;
            RoleNote.Text = Done.Organization ? Language.T("management.people_access_through_organization_dont") : Language.T("management.personal_repo_github_only_one");
        });
    }

    void Ask(string title, string button, bool severe, Func<Client, RepoFacts, string> readFacts, Action<Client, RepoFacts, string> does, string done) {
        using (var k = new ConfirmDialog(P, repo, title, button, true, severe, readFacts, does))
            if (P.Open(k) == DialogResult.OK) { ShowSuccess(done); ReadFileAgain(); }
    }

    void Add() {
        string who = NewName.Text.Trim(), role = RoleChosen();
        bool invitation = false;
        Ask(Language.T("management.add_person"), Language.T("management.add"), false, (c, f) => {
            string type = Writer.AccountType(c, who);
            return Language.T("management.adding", "who", V(who), "part", (type == "Organization" ? Language.T("management.which_organization") : ""), "part2", (f.Organization ? Language.T("management.with_the_role", "role", Word(role)) : Language.T("management.able_read_write")), "part3", (f.Private ? Language.T("management.see_everything_private_repo") : ""));
        }, (c, f, s) => invitation = Writer.AddCollaborator(c, f, s, who, role), Language.T("management.done"));
        if (invitation) { ShowSuccess(Language.T("management.invitation_sent_join_when_accept")); NewName.Text = ""; }
    }

    void Change() {
        var x = Choice(); string role = RoleChosen();
        Ask(Language.T("management.change_role_2"), Language.T("management.change_role"), false, (c, f) => Language.T("management.now_after", "name", V(x.Name), "name2", Word(Writer.RoleOf(c, f, x.Name)), "role", Word(role)), (c, f, s) => Writer.ChangeRole(c, f, s, x.Name, role), Language.T("management.role_changed"));
    }

    void Remove() {
        var x = Choice();
        if (x.Invitation != 0) Ask(Language.T("management.withdraw_invitation"), Language.T("management.withdraw_invitation"), true, (c, f) => Language.T("management.withdrawing_invitation", "name", V(x.Name)), (c, f, s) => Writer.WithdrawInvitation(c, f, s, x.Invitation), Language.T("management.invitation_withdrawn"));
        else Ask(Language.T("management.remove_person"), Language.T("management.remove"), true, (c, f) => Language.T("management.no_longer_able_work_repo", "name", V(x.Name)), (c, f, s) => Writer.RemoveCollaborator(c, f, s, x.Name), Language.T("management.removed"));
    }
}
