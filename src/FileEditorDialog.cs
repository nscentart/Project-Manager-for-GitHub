using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class FileEditorDialog : OperationDialog {
    public TextBox Path, EditorText;
    public ThemedButton SaveButton, NoButton;
    public Label Header;
    readonly string repo, path;
    string sha, lineEnd = "\n";
    bool bom;
    RepoFacts seen;
    public bool Ready;

    public FileEditorDialog(WriteGateway p, string repoName, string which) : base(p, which == null ? Language.T("management.new_file") : Language.T("management.edit_file"), 760, 600) {
        repo = repoName; path = which;
        string start = which == null ? Language.T("management.new_file_where_goes_what") : Language.T("management.edit_file_2");
        Header = AddLabel(start, Theme.Normal, Theme.Muted, S(14), S(20));
        Path = Field(which ?? "", new Rectangle(M, S(36), Lu, S(26)), false);
        Path.ReadOnly = which != null;
        Path.TextChanged += delegate { Redo(); };
        EditorText = Field("", new Rectangle(M, S(70), Lu, S(400)), true);
        EditorText.Font = new Font("Consolas", 9.5f); EditorText.AcceptsTab = true; EditorText.WordWrap = false; EditorText.ScrollBars = ScrollBars.Both; EditorText.MaxLength = 0;
        Line(S(476), S(66));
        var r = new Rectangle(M + Lu - S(170), S(548), S(170), S(38));
        SaveButton = CreateButton(Language.T("management.save_github"), true, r, delegate {
            string x = Path.Text.Trim(), t = EditorText.Text.Replace("\r\n", "\n"), s = sha; var f = seen; bool b = bom;
            if (lineEnd == "\r\n") t = t.Replace("\n", "\r\n");
            Work<bool>(c => { Writer.WriteFile(c, f, x, t, s, null, b); return true; }, v => { Changed = true; DialogResult = DialogResult.OK; });
        });
        NoButton = CreateButton(Language.T("common.cancel"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(38)), delegate { Close(); });
        Shown += delegate {
            if (path == null) Path.Focus();
            string name = repo, fileWhich = path;
            Work(c => { var f = Writer.Fetch(c, name); return new KeyValuePair<RepoFacts, RepoFile>(f, fileWhich == null ? null : Writer.ReadFile(c, f, fileWhich)); }, x => {
                seen = x.Key;
                Header.Text = start + ". " + (seen.Private ? Language.T("management.private_repo") : Language.T("management.public_repo_anyone_see")) + (P.Login != null ? Language.T("management.you_are_acting_as_2", "login", V(P.Login)) : "");
                if (!seen.Private) Header.ForeColor = Theme.Amber;
                if (x.Value != null) { sha = x.Value.Sha; bom = x.Value.Bom; lineEnd = x.Value.LineEnd; EditorText.Text = x.Value.Text.Replace("\r\n", "\n").Replace("\n", "\r\n"); }
                Ready = true;
            });
        };
        Redo();
    }

    protected override bool Can(ThemedButton b) { return b == NoButton || (Ready && Path.Text.Trim().Length > 0 && !Writer.Workflow(Path.Text.Trim())); }
    protected override void Explain() { if (Writer.Workflow(Path.Text.Trim())) ShowNote(Phrases.WorkflowBlocked); else if (Notice.Text == Phrases.WorkflowBlocked) ShowNote(""); }
}
