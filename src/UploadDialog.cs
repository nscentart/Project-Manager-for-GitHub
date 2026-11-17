using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public class UploadDialog : OperationDialog {
    public ThemedButton ChooseButton, YesButton, NoButton, LeaveOutButton;
    public Label Summary, AsksName, Removed;
    public TextBox Detail, Message, NameWritten;
    public CheckBox Tick;
    public Folder Ready;
    public UploadResult Result;
    readonly string repo;
    const int Down = 34;
    Tuple<Folder, RepoFacts, List<string>> seen;

    public UploadDialog(WriteGateway p, string repoName) : base(p, Language.T("management.upload_folder"), 680, 650 + Down) {
        repo = repoName;
        AddLabel(Language.T("management.upload_folder_2", "repo", V(repo)), Theme.Large, Theme.Text, S(16), S(34));
        AddLabel(Language.T("management.folders_files_go_repo_all"), Theme.Normal, Theme.Muted, S(54), S(44));
        Removed = AddLabel(Language.T("management.files_removed_folder_stay_github", "button", Language.T("management.single_files")), Theme.Normal, Theme.Text, S(94), S(Down + 4));
        ChooseButton = CreateButton(Language.T("management.choose_folder"), false, new Rectangle(M, S(104 + Down), S(190), S(34)), delegate {
            using (var d = new FolderBrowserDialog { Description = Language.T("management.folder_upload"), ShowNewFolderButton = false })
                if (d.ShowDialog(this) == DialogResult.OK) Look(d.SelectedPath);
        });
        Summary = AddLabel(Language.T("management.no_folder_chosen"), Theme.Normal, Theme.Text, S(146 + Down), S(84));
        Detail = Field("", new Rectangle(M, S(234 + Down), Lu, S(150)), true);
        Detail.ReadOnly = true; Detail.AcceptsReturn = false;
        Tick = new CheckBox { Bounds = new Rectangle(M, S(388 + Down), Lu - S(150), S(40)), BackColor = Theme.Background, ForeColor = Theme.Text, UseMnemonic = false, Visible = false };
        Tick.CheckedChanged += delegate { Redo(); };
        Controls.Add(Tick);
        LeaveOutButton = CreateButton(Language.T("management.leave_out"), false, new Rectangle(M + Lu - S(140), S(390 + Down), S(140), S(34)), delegate {
            Writer.LeaveOut(Ready);
            Tick.Checked = false;
            Draw();
        });
        LeaveOutButton.Visible = false;
        AddLabel(Language.T("management.what_say_about_upload"), Theme.Normal, Theme.Text, S(432 + Down), S(20));
        Message = Field("", new Rectangle(M, S(454 + Down), Lu, S(26)), false);
        AsksName = AddLabel("", Theme.Normal, Theme.Muted, S(484 + Down), S(20));
        NameWritten = Field("", new Rectangle(M, S(506 + Down), Lu, S(26)), false);
        NameWritten.TextChanged += delegate { Redo(); };
        AsksName.Visible = NameWritten.Visible = false;
        Line(S(484 + Down), S(110));
        var r = new Rectangle(M + Lu - S(170), S(600 + Down), S(170), S(38));
        YesButton = CreateButton(Language.T("management.upload"), true, r, delegate { StartUpload(); });
        NoButton = CreateButton(Language.T("common.close"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(38)), delegate { Close(); });
        Redo();
    }

    protected override bool Can(ThemedButton b) {
        if (b != YesButton && b != LeaveOutButton) return true;
        if (Ready == null || seen == null || Result != null || Ready.Blocked) return false;
        if (b == LeaveOutButton) return Ready.ToConfirm.Count > 0;
        return Ready.File.Count > 0 && (Ready.ToConfirm.Count == 0 || Tick.Checked) && (seen.Item2.Private || NameWritten.Text == seen.Item2.Name);
    }

    public void Look(string folder) {
        Ready = null; Result = null; seen = null;
        Summary.Text = Language.T("management.checking_folder"); Summary.ForeColor = Theme.Text; Detail.Text = ""; ShowNote("");
        Tick.Visible = LeaveOutButton.Visible = AsksName.Visible = NameWritten.Visible = false; Tick.Checked = false; NameWritten.Text = "";
        Notice.Bounds = new Rectangle(M, S(484 + Down), Lu, S(110));
        string name = repo;
        Work(c => {
            var k = Writer.Prepare(folder, null);
            var f = Writer.Fetch(c, name);
            return Tuple.Create(k, f, k.Blocked ? new List<string>() : Writer.Replaced(c, f, k));
        }, x => {
            seen = x;
            Ready = x.Item1;
            Draw();
        });
    }

    void Draw() {
        var k = seen.Item1; var f = seen.Item2;
        var inside = new HashSet<string>(k.File.Select(z => z.Path));
        var so = seen.Item3.Where(inside.Contains).ToList();
        Summary.ForeColor = f.Private ? Theme.Text : Theme.Amber;
        Summary.Text = Language.T("management.kb", "part", (f.Private ? Language.T("management.private_repo_people_invited_see") : Language.T("management.public_repo_anyone_see_what")), "part2", (P.Login != null ? Language.T("management.you_are_acting_as_2", "login", V(P.Login)) : ""), "file", Language.N("publish.files", k.File.Count), "part3", (k.ByteSize / 1024 + 1), "part4", (k.Blocked ? "." : so.Count == 0 ? Language.T("management.all_new") : so.Count == 1 ? Language.T("management.1_replaces_file_already_exists") : Language.T("management.replace_files_already_exist", "n", so.Count)), "part5", (k.Skipped.Count > 0 ? Language.N("management.leaving_out_items_git_folder", k.Skipped.Count) : ""));
        if (k.Blocked) {
            ShowNote(Language.T("management.stopping_nothing_uploaded_remove_folder"));
            Detail.Text = string.Join("\r\n", k.Findings.Select(z => V(z.ToString())).Concat(k.Rejected.Select(V)));
            return;
        }
        var t = new List<string>();
        Action<string, IEnumerable<string>> section = (title, l) => {
            var e = l.ToList();
            if (e.Count == 0) return;
            t.Add(title);
            t.AddRange(e.Take(200).Select(z => "  " + V(z)));
            if (e.Count > 200) t.Add(Language.T("management.and_more_2", "part", (e.Count - 200)));
            t.Add("");
        };
        section(Language.T("management.replaced_what_github_now_gets"), so);
        section(Language.T("management.confirm_archives_databases_dont_look"), k.ToConfirm);
        section(Language.T("management.not_readable_documents_images_dont"), k.NotReadable);
        section(Language.T("management.check_not_blocking_contain_peoples"), k.Notices.Select(z => z.ToString()));
        section(Language.T("management.to_upload"), k.File.Select(z => z.Path));
        Detail.Text = string.Join("\r\n", t);
        Tick.Visible = LeaveOutButton.Visible = k.ToConfirm.Count > 0;
        if (k.ToConfirm.Count > 0) Tick.Text = Language.T("management.upload_anyway_without_looking_inside", "list", string.Join(", ", k.ToConfirm.Take(4).Select(V)), "part", (k.ToConfirm.Count > 4 ? Language.T("common.and_more", "part", (k.ToConfirm.Count - 4)) : ""));
        AsksName.Visible = NameWritten.Visible = !f.Private;
        AsksName.Text = Language.T("management.repo_public_upload_type", "name", V(f.Name));
        Notice.Bounds = f.Private ? new Rectangle(M, S(484 + Down), Lu, S(110)) : new Rectangle(M, S(536 + Down), Lu, S(58));
        Redo();
    }

    void StartUpload() {
        var k = Ready; var f = seen.Item2; string m = Message.Text.Trim(), written = NameWritten.Text;
        if (Tick.Checked) foreach (var x in k.ToConfirm) k.Confirmed.Add(x);
        ShowNote(Language.T("management.uploading"));
        Work(c => Writer.LoadFolder(c, f, k, m, (i, n) => InUi(() => ShowNote(Language.T("management.sending_file", "i", i, "n", n))), P.Sleeps, written), e => {
            Result = e;
            ShowSuccess(Language.N("management.done_files_uploaded", e.File) + (e.WasEmpty && e.Steps > 1 ? Language.T("management.repo_empty_github_wanted_first") : "."));
        });
    }
}
