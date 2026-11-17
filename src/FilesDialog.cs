using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public class FilesDialog : OperationDialog {
    public ListView Lines;
    public ThemedButton NewButton, EditButton, DeleteButton, CloseButton;
    readonly string repo;
    RepoFacts done;

    public FilesDialog(WriteGateway p, string repoName) : base(p, Language.T("management.files"), 680, 540) {
        repo = repoName;
        AddLabel(Language.T("management.the_files_of", "repo", V(repo)), Theme.Large, Theme.Text, S(16), S(34));
        Lines = CreateList(new Rectangle(M, S(56), Lu, S(340)), new[] { Language.T("management.files"), Language.T("management.size") }, new[] { 520, 90 });
        Line(S(402), S(66));
        int y = S(478);
        NewButton = CreateButton(Language.T("management.new_file"), false, new Rectangle(M, y, S(120), S(38)), delegate { Open(null); });
        EditButton = CreateButton(Language.T("management.edit"), true, new Rectangle(M + S(130), y, S(120), S(38)), delegate { Open(Chosen()); });
        DeleteButton = CreateButton(Language.T("management.delete"), false, new Rectangle(M + S(260), y, S(120), S(38)), delegate { Delete(); });
        CloseButton = CreateButton(Language.T("common.close"), false, new Rectangle(M + Lu - S(110), y, S(110), S(38)), delegate { Close(); });
        Shown += delegate { ReadFileAgain(); };
        Redo();
    }

    public string Chosen() { return Lines.SelectedItems.Count == 0 ? null : (string)Lines.SelectedItems[0].Tag; }

    protected override bool Can(ThemedButton b) {
        if (b == CloseButton) return true;
        if (done == null) return false;
        if (b == NewButton) return true;
        return Chosen() != null && !Writer.Workflow(Chosen());
    }

    protected override void Explain() { if (!Working && Chosen() != null && Writer.Workflow(Chosen())) ShowNote(Phrases.WorkflowBlocked); else if (Notice.Text == Phrases.WorkflowBlocked) ShowNote(""); }

    void ReadFileAgain() {
        string name = repo;
        Work(c => { var f = Writer.Fetch(c, name); return new KeyValuePair<RepoFacts, List<RepoFile>>(f, Writer.FileList(c, f)); }, x => {
            done = x.Key;
            Lines.Items.Clear();
            foreach (var r in x.Value) Lines.Items.Add(new ListViewItem(new[] { V(r.Path), r.ByteSize < 1024 ? Language.T("management.b", "size", r.ByteSize) : Language.T("management.kb_2", "size", r.ByteSize / 1024) }) { Tag = r.Path });
            if (x.Value.Count == 0) ShowNote(Language.T("management.repo_empty_create_file_upload"));
        });
    }

    void Open(string path) {
        using (var e = new FileEditorDialog(P, repo, path)) if (P.Open(e) == DialogResult.OK) { ShowSuccess(Language.T("management.saved_github")); ReadFileAgain(); }
    }

    void Delete() {
        string x = Chosen();
        RepoFile fileRead = null;
        using (var k = new ConfirmDialog(P, repo, Language.T("management.delete_file"), Language.T("management.delete_file_2"), false, true,
            (c, f) => { fileRead = Writer.ReadFile(c, f, x); return Language.T("management.deletes_branch_stays_repos_history", "text", V(x), "branch", V(f.Branch)); },
            (c, f, s) => Writer.DeleteFile(c, f, x, fileRead.Sha, null)))
            if (P.Open(k) == DialogResult.OK) { ShowSuccess(Language.T("management.deleted_2")); ReadFileAgain(); }
    }
}
