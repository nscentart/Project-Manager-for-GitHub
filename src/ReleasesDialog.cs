using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class ReleasesDialog : OperationDialog {
    public ListView Lines;
    public ThemedButton NewButton, EditButton, AttachButton, LastButton, PublishButton, DeleteButton, CloseButton;
    readonly string repo;
    RepoFacts done;

    public ReleasesDialog(WriteGateway p, string repoName) : base(p, Language.T("management.releases"), 760, 540) {
        repo = repoName;
        AddLabel(Language.T("management.the_releases_of", "repo", V(repo)), Theme.Large, Theme.Text, S(16), S(34));
        AddLabel(Language.T("management.release_version_ready_download_programs"), Theme.Normal, Theme.Muted, S(54), S(44));
        Lines = CreateList(new Rectangle(M, S(102), Lu, S(250)), new[] { Language.T("management.tag"), Language.T("common.title"), Language.T("common.status"), Language.T("management.attachments") }, new[] { 110, 300, 130, 150 });
        Line(S(358), S(64));
        int y = S(430), y2 = S(476);
        NewButton = CreateButton(Language.T("management.new_draft"), true, new Rectangle(M, y, S(140), S(36)), delegate { New(); });
        EditButton = CreateButton(Language.T("management.edit"), false, new Rectangle(M + S(150), y, S(110), S(36)), delegate { Edit(); });
        AttachButton = CreateButton(Language.T("management.attach_file"), false, new Rectangle(M + S(270), y, S(140), S(36)), delegate {
            using (var d = new OpenFileDialog { Title = Language.T("management.file_attach") }) if (d.ShowDialog(this) == DialogResult.OK) AttachAsset(Choice().Id, d.FileName);
        });
        DeleteButton = Red(Language.T("management.delete"), new Rectangle(M + Lu - S(110), y, S(110), S(36)), delegate { Delete(); });
        LastButton = CreateButton(Language.T("management.publish_latest_version"), false, new Rectangle(M, y2, S(260), S(36)), delegate { Publish(true); });
        PublishButton = CreateButton(Language.T("management.publish_not_latest"), false, new Rectangle(M + S(270), y2, S(250), S(36)), delegate { Publish(false); });
        CloseButton = CreateButton(Language.T("common.close"), false, new Rectangle(M + Lu - S(110), y2, S(110), S(36)), delegate { Close(); });
        Shown += delegate { ReadFileAgain(); };
        Redo();
    }

    public ReleaseInfo Choice() { return Lines.SelectedItems.Count == 0 ? null : (ReleaseInfo)Lines.SelectedItems[0].Tag; }

    protected override bool Can(ThemedButton b) {
        if (b == CloseButton) return true;
        if (done == null) return false;
        if (b == NewButton) return true;
        var r = Choice();
        if (r == null) return false;
        return (b != LastButton && b != PublishButton) || r.Draft;
    }

    void ReadFileAgain() {
        string name = repo;
        Work(c => { var f = Writer.Done(c, name); return new KeyValuePair<RepoFacts, List<ReleaseInfo>>(f, Writer.Release(c, f)); }, x => {
            done = x.Key;
            Lines.Items.Clear();
            foreach (var r in x.Value) Lines.Items.Add(new ListViewItem(new[] { V(r.Tag), V(r.Title), r.Draft ? Language.T("management.draft") : r.Preview ? Language.T("management.pre_release") : Language.T("management.published"), r.Assets.Count == 0 ? Language.T("common.none_3") : V(string.Join(", ", r.Assets.Select(a => a.Name))) }) { Tag = r });
        });
    }

    public void AttachAsset(long id, string file) {
        var f = done;
        ShowNote(Language.T("management.sending_file_github"));
        Work<bool>(c => { Writer.AttachAsset(c, f, id, file); return true; }, x => { ShowSuccess(Language.T("management.attached")); ReadFileAgain(); });
    }

    void New() {
        using (var m = new ReleaseEditDialog(P, null)) {
            if (P.Open(m) != DialogResult.OK) return;
            string tag = m.TagName.Text.Trim(), ti = m.Title.Text.Trim(), no = m.Note.Text, fi = m.Asset;
            var f = done;
            Work<string>(c => {
                var r = Writer.CreateDraft(c, f, tag, ti, no);
                if (fi != null) try { Writer.AttachAsset(c, f, r.Id, fi); } catch (WriteException e) { return Language.T("management.draft_exists_attachment_not_go", "error", Sentence(e)); }
                return null;
            }, e => { Changed = true; if (e != null) ShowNote(e); else ShowSuccess(Language.T("management.draft_created_when_ready_publish")); ReadFileAgain(); });
        }
    }

    void Edit() {
        var r = Choice();
        using (var m = new ReleaseEditDialog(P, r)) {
            if (P.Open(m) != DialogResult.OK) return;
            string tag = m.TagName.Text.Trim(), ti = m.Title.Text.Trim(), no = m.Note.Text;
            var seen = done;
            if (r.Draft) { Work<bool>(c => { Writer.EditRelease(c, seen, r.Id, tag, ti, no, null); return true; }, x => { ShowSuccess(Language.T("management.saved")); ReadFileAgain(); }); return; }
            using (var k = new ConfirmDialog(P, repo, Language.T("management.edit_published_release"), Language.T("management.save_changes"), true, false,
                (c, f) => Language.T("management.people_downloading_github_immediately_see", "tag", V(Writer.OneRelease(c, f, r.Id).Tag)),
                (c, f, s) => Writer.EditRelease(c, f, r.Id, tag, ti, no, s)))
                if (P.Open(k) == DialogResult.OK) { ShowSuccess(Language.T("management.saved")); ReadFileAgain(); }
        }
    }

    void Publish(bool latest) {
        var r = Choice();
        ConfirmDialog k = null;
        k = new ConfirmDialog(P, repo, latest ? Language.T("management.publish_latest_version") : Language.T("management.publish_release"), Language.T("management.publish"), true, false, (c, f) => {
            var now = Writer.OneRelease(c, f, r.Id); string u = Writer.Last(c, f);
            return Language.T("management.release_attachments_latest_version_now", "tag", V(now.Tag), "part", (now.Assets.Count == 0 ? Language.T("management.none_people_who_download_wont") : V(string.Join(", ", now.Assets.Select(a => a.Name)))), "part2", (u.Length == 0 ? Language.T("common.none") : V(u)), "part3", (latest ? Language.T("management.programs_automatic_updates_pick_one") : Language.T("management.one_stays")));
        }, (c, f, s) => Writer.PublishWithRetry(c, f, s, r.Id, latest, t => k.Step(t), () => k.Check())) { Retry = true };
        using (k) if (P.Open(k) == DialogResult.OK) { ShowSuccess(Language.T("management.published_2")); ReadFileAgain(); }
    }

    void Delete() {
        var r = Choice();
        using (var k = new ConfirmDialog(P, repo, Language.T("management.delete_release"), Language.T("management.delete_release_2"), true, true, (c, f) => {
            var now = Writer.OneRelease(c, f, r.Id);
            return Language.T("management.deletes_release_attachments_tag_bookmark", "tag", V(now.Tag), "attachments", now.Assets.Count);
        }, (c, f, s) => Writer.DeleteRelease(c, f, s, r.Id)))
            if (P.Open(k) == DialogResult.OK) { ShowSuccess(Language.T("management.deleted")); ReadFileAgain(); }
    }
}
