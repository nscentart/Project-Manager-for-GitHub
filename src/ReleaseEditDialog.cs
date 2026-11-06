using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class ReleaseEditDialog : OperationDialog {
    public TextBox TagName, Title, Note;
    public ThemedButton YesButton, NoButton, BFile;
    public string Asset;
    public Label LFile;

    public ReleaseEditDialog(WriteGateway p, ReleaseInfo r) : base(p, r == null ? Language.T("management.new_release_draft") : Language.T("management.edit_release"), 620, 520) {
        AddLabel(r == null ? Language.T("management.new_release_draft") : Language.T("management.edit_release"), Theme.Large, Theme.Text, S(16), S(34));
        AddLabel(Language.T("management.tag_versions_bookmark_example_v1"), Theme.Normal, Theme.Muted, S(56), S(20));
        TagName = Field(r == null ? "" : r.Tag, new Rectangle(M, S(78), S(200), S(26)), false);
        TagName.TextChanged += delegate { Redo(); };
        AddLabel(Language.T("common.title"), Theme.Normal, Theme.Muted, S(110), S(20));
        Title = Field(r == null ? "" : r.Title, new Rectangle(M, S(132), Lu, S(26)), false);
        AddLabel(Language.T("management.notes_whats_new"), Theme.Normal, Theme.Muted, S(164), S(20));
        Note = Field(r == null ? "" : (r.Note ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"), new Rectangle(M, S(186), Lu, S(180)), true);
        if (r == null) {
            BFile = CreateButton(Language.T("management.attach_exe"), false, new Rectangle(M, S(376), S(150), S(34)), delegate {
                using (var d = new OpenFileDialog { Title = Language.T("management.file_attach") }) if (d.ShowDialog(this) == DialogResult.OK) { Asset = d.FileName; LFile.Text = Path.GetFileName(Asset); }
            });
            LFile = new Label { Text = Language.T("management.no_attachment"), ForeColor = Theme.Muted, BackColor = Theme.Background, Bounds = new Rectangle(M + S(160), S(382), Lu - S(160), S(22)), UseMnemonic = false };
            Controls.Add(LFile);
        }
        Line(S(416), S(40));
        var q = new Rectangle(M + Lu - S(170), S(466), S(170), S(38));
        YesButton = CreateButton(r == null ? Language.T("management.create_draft") : Language.T("management.continue"), true, q, delegate { DialogResult = DialogResult.OK; });
        NoButton = CreateButton(Language.T("common.cancel"), false, new Rectangle(q.X - S(120), q.Y, S(110), S(38)), delegate { Close(); });
        Redo();
    }

    protected override bool Can(ThemedButton b) { return b != YesButton || TagName.Text.Trim().Length > 0; }
}
