using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

public class DeleteRepoDialog : OperationDialog {
    public TextBox NameBox;
    public Label Story, Code;
    public ThemedButton NextButton, OpenButton, WithdrawButton, NoButton;
    public bool Deleted, Pending;
    public RepoFacts Seen;
    public DeviceCode Device;
    readonly string repo;
    volatile bool cancelled;

    public DeleteRepoDialog(WriteGateway p, string repoName) : base(p, Language.T("management.delete_repo"), 620, 500) {
        repo = repoName;
        AddLabel(Language.T("management.delete_repo"), Theme.Large, Theme.Red, S(16), S(34));
        Story = AddLabel(Language.T("management.reading_again_github"), Theme.Normal, Theme.Text, S(56), S(170));
        AddLabel(Language.T("management.confirm_type_full_name_repo"), Theme.Normal, Theme.Muted, S(232), S(20));
        NameBox = Field("", new Rectangle(M, S(254), Lu, S(26)), false);
        NameBox.TextChanged += delegate { Redo(); };
        Code = new Label { Font = new Font("Consolas", 24f, FontStyle.Bold), ForeColor = Theme.Lime, BackColor = Theme.Background, Bounds = new Rectangle(M, S(286), Lu, S(48)), TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false };
        Controls.Add(Code);
        Line(S(338), S(92));
        int y = S(442);
        NextButton = Red(Language.T("management.authorize_delete"), new Rectangle(M + Lu - S(200), y, S(200), S(38)), delegate { Next(); });
        OpenButton = CreateButton(Language.T("common.copy_code_open_github"), true, new Rectangle(M + Lu - S(270), y, S(270), S(38)), delegate { P.Gateway.Copy(Device.ForUser); P.Gateway.Open(AccountAccess.DefaultVerificationUrl); });
        WithdrawButton = CreateButton(Language.T("management.open_page_withdraw"), true, new Rectangle(M + Lu - S(270), y, S(270), S(38)), delegate { P.Gateway.Open(AccountAccess.WithdrawPage); });
        NoButton = CreateButton(Language.T("common.cancel"), false, new Rectangle(M, y, S(120), S(38)), delegate { cancelled = true; Close(); });
        OpenButton.Visible = WithdrawButton.Visible = false;
        Shown += delegate {
            NameBox.Focus();
            Work(c => Writer.Fetch(c, repo), f => {
                Seen = f;
                Story.Text = Language.T("management.deletes_repo_all_files_history", "f", V(Describe(f)), "part", (f.Stars + f.Forks > 0 ? Language.T("management.other_people_use", "f", Stars(f), "f2", Forks(f)) : ""));
            });
        };
        FormClosed += delegate { cancelled = true; };
        Redo();
    }

    protected override bool Can(ThemedButton b) {
        if (b == NoButton || b == OpenButton || b == WithdrawButton) return true;
        return Seen != null && !Pending && !Deleted && NameBox.Text == Seen.Name;
    }

    void Next() {
        string written = NameBox.Text, who = P.Login; var seen = Seen;
        ShowNote("");
        Work(c => { Writer.Fetch(c, repo); return P.AskDelete(); }, k => {
            Device = k; Pending = true;
            NameBox.ReadOnly = true;
            Code.Text = k.ForUser;
            NextButton.Visible = false; OpenButton.Visible = true;
            ShowNote(Language.T("management.type_code_github_authorize_github"));
            new Thread(() => {
                string token = null; Exception x = null;
                try { token = P.WaitDelete(k, () => cancelled); } catch (Exception e) { x = e; }
                InUi(() => {
                    Pending = false;
                    OpenButton.Visible = false; Code.Text = "";
                    if (x != null || token == null) { NameBox.ReadOnly = false; NextButton.Visible = true; if (x != null) Mistake(x); else ShowNote(Language.T("management.canceled_deleted_nothing")); Redo(); return; }
                    var use = key = P.Disposable(token); token = null;
                    ShowNote(Language.T("management.deleting"));
                    Work<bool>(c => { Writer.DeleteRepo(c, use, who, seen, written); return true; }, v => {
                        Deleted = true;
                        NoButton.Text = Language.T("common.close"); WithdrawButton.Visible = true;
                        ShowNote(Language.T("management.deleted_threw_away_key_authorization"));
                    });
                });
            }) { IsBackground = true }.Start();
        });
    }

    Client key;

    protected override void Mistake(Exception e) {
        base.Mistake(e);
        if (key != null && !key.Discarded) {
            key.Discard();
            ShowNote(Language.T("management.deleted_nothing_threw_away_key", "notice", Notice.Text));
        }
        key = null;
        NameBox.ReadOnly = false; NextButton.Visible = !Deleted; OpenButton.Visible = false; Code.Text = "";
    }
}
