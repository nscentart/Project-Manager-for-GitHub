using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class NewRepoDialog : OperationDialog {
    static string NameNo { get { return Language.T("common.name_contain_letters_without_accents"); } }
    public ComboBox Where;
    public TextBox RepoName, Description;
    public CheckBox WithReadme;
    public ThemedButton YesButton, NoButton;
    public RepoFacts CreatedRepo;

    public NewRepoDialog(WriteGateway p, string favoriteWhere) : base(p, Language.T("management.create_repo"), 600, 490) {
        AddLabel(Language.T("management.create_repo"), Theme.Large, Theme.Text, S(16), S(34));
        AddLabel(Language.T("management.starts_out_private_only_people"), Theme.Normal, Theme.Muted, S(54), S(44));
        AddLabel(Language.T("management.owner"), Theme.Normal, Theme.Text, S(102), S(20));
        Where = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Capsule, ForeColor = Theme.Text, Bounds = new Rectangle(M, S(124), Lu, S(28)) };
        Where.SelectedIndexChanged += delegate { Redo(); };
        Controls.Add(Where);
        AddLabel(Language.T("management.name"), Theme.Normal, Theme.Text, S(160), S(20));
        RepoName = Field("", new Rectangle(M, S(182), Lu, S(26)), false);
        RepoName.TextChanged += delegate { if (!Working && CreatedRepo == null) ShowNote(""); Redo(); };
        AddLabel(Language.T("management.description_optional"), Theme.Normal, Theme.Text, S(216), S(20));
        Description = Field("", new Rectangle(M, S(238), Lu, S(26)), false);
        WithReadme = new CheckBox { Text = Language.T("management.add_readme_right_away_presentation"), Checked = true, Bounds = new Rectangle(M, S(274), Lu, S(26)), BackColor = Theme.Background, ForeColor = Theme.Text, UseMnemonic = false };
        Controls.Add(WithReadme);
        Line(S(306), S(122));
        var r = new Rectangle(M + Lu - S(170), S(438), S(170), S(36));
        YesButton = CreateButton(Language.T("management.create_repo_2"), true, r, delegate {
            var d = Where.SelectedItem as Owner; string n = RepoName.Text.Trim(), t = Description.Text.Trim(); bool k = WithReadme.Checked; string who = P.Login;
            Work(c => Writer.CreateRepo(c, who, d, n, t, k), f => {
                CreatedRepo = f; Changed = true;
                if (f.ShowNote == null) DialogResult = DialogResult.OK; else { ShowNote(f.ShowNote); NoButton.Text = Language.T("common.close"); }
            });
        });
        NoButton = CreateButton(Language.T("common.cancel"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(36)), delegate { Close(); });
        Shown += delegate {
            string who = P.Login;
            RepoName.Focus();
            Work(c => Writer.Owners(c, who), l => {
                foreach (var x in l) Where.Items.Add(x);
                Where.SelectedIndex = Math.Max(0, l.FindIndex(x => Organization.Equal(x.Name, favoriteWhere)));
            });
        };
        Redo();
    }

    protected override bool Can(ThemedButton b) {
        if (b == NoButton) return true;
        var d = Where.SelectedItem as Owner;
        return CreatedRepo == null && d != null && d.Because == null && Writer.ValidRepoName(RepoName.Text.Trim());
    }

    string reason;
    protected override void Explain() {
        if (Working) return;
        var d = Where.SelectedItem as Owner;
        string n = RepoName.Text.Trim(), now = d != null && d.Because != null ? d.Because : n.Length > 0 && !Writer.ValidRepoName(n) ? NameNo : null;
        if (now != null) ShowNote(now); else if (Notice.Text == reason) ShowNote("");
        reason = now;
    }
}
