using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class UpdateNoticeDialog : Dialog {
    public ThemedButton SkipButton, LaterButton, PageButton;
    public TextBox Note, Fingerprint;
    public Label Sentence, Alarm;

    public UpdateNoticeDialog(NewVersion n) {
        Func<int, int> S = Theme.S;
        Text = Language.T("updates.project_manager_update");
        try { Icon = Theme.AppIcon(); } catch { }
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        SkipButton = new ThemedButton { Text = Language.T("updates.skip_version"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, DialogResult = DialogResult.Ignore };
        LaterButton = new ThemedButton { Text = Language.T("updates.later"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, DialogResult = DialogResult.Cancel };
        PageButton = new ThemedButton { Text = Language.T("updates.open_version_page"), Background = Theme.Azure, TextColor = Color.White, Line = Color.Empty, DialogResult = DialogResult.OK };
        int ws = SkipButton.GetPreferredSize(Size.Empty).Width, wd = LaterButton.GetPreferredSize(Size.Empty).Width, wp = PageButton.GetPreferredSize(Size.Empty).Width;
        string line = n.Fingerprint != null ? Language.T("updates.sha_256_fingerprint", "h", n.Fingerprint) : Language.T("updates.fingerprint_not_available_see_page");
        var monoFont = new Font("Consolas", Theme.Normal.Size);
        int lr = TextRenderer.MeasureText(line, monoFont, new Size(9999, 99), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + S(12);
        int m = S(24), width = Math.Max(Math.Max(S(600), m * 2 + lr), m * 2 + ws + wd + wp + S(8) + S(24)), lu = width - m * 2;
        Func<Label, int, int, int, int> putDown = (l, x, yl, w) => { int h = l.GetPreferredSize(new Size(w, 0)).Height + S(2); l.Bounds = new Rectangle(x, yl, w, h); return h; };
        var mark = new Panel { Bounds = new Rectangle(m, S(20), S(48), S(48)), BackColor = Theme.Background };
        mark.Paint += (s, e) => Theme.Logo(e.Graphics, new Rectangle(0, 0, mark.Width, mark.Height));
        int xt = m + S(60), lt = lu - S(60);
        var title = new Label { Text = Language.T("updates.version_out", "n", Screener.MakeVisible(n.Label)), Font = Theme.Large, BackColor = Theme.Background, UseMnemonic = false };
        int y = S(16) + putDown(title, xt, S(16), lt) + S(2);
        Sentence = new Label { Text = Language.T("updates.version_program_doesnt_download_install", "v", Program.Version), Font = Theme.Normal, ForeColor = Theme.Muted, BackColor = Theme.Background, UseMnemonic = false };
        y = Math.Max(S(80), y + putDown(Sentence, xt, y, lt) + S(8));
        if (n.DifferentFingerprint) {
            Alarm = new Label { Text = Language.T("updates.fingerprint_differs_between_file_notes"), Font = Theme.Strong, ForeColor = Theme.Ink, BackColor = Theme.Red, Padding = new Padding(S(10), S(6), S(10), S(6)), UseMnemonic = false };
            y += putDown(Alarm, m, y, lu) + S(8);
        }
        Fingerprint = new TextBox { Text = line, ReadOnly = true, TabStop = false, BorderStyle = BorderStyle.None, Font = monoFont, BackColor = Theme.Background, ForeColor = n.Fingerprint != null ? Theme.Text : Theme.Amber };
        Fingerprint.Bounds = new Rectangle(m, y, lu, Fingerprint.PreferredHeight);
        y += Fingerprint.Height + S(8);
        Note = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = UpdateCheck.CleanNotes(n.Note), BackColor = Color.FromArgb(24, 25, 29), ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(m, y, lu, S(240)) };
        Note.HandleCreated += delegate { try { RepoList.SetWindowTheme(Note.Handle, "DarkMode_Explorer", null); } catch { } };
        y += Note.Height + S(10);
        string how = Language.T("updates.page_shows_what_changed_how");
        int hc = TextRenderer.MeasureText(how, Font, new Size(lu, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + S(4);
        var explain = new Label { Text = how, ForeColor = Theme.Muted, BackColor = Theme.Background, Bounds = new Rectangle(m, y, lu, hc), UseMnemonic = false };
        y += hc + S(12);
        PageButton.Bounds = new Rectangle(m + lu - wp, y, wp, S(36));
        LaterButton.Bounds = new Rectangle(PageButton.Left - S(8) - wd, y, wd, S(36));
        SkipButton.Bounds = new Rectangle(m, y, ws, S(36));
        Controls.AddRange(new Control[] { mark, title, Sentence, Fingerprint, Note, explain, SkipButton, LaterButton, PageButton });
        if (Alarm != null) Controls.Add(Alarm);
        ClientSize = new Size(width, y + S(36) + S(18));
        AcceptButton = PageButton; CancelButton = LaterButton;
        Load += delegate { Theme.DarkBar(this); };
        Shown += delegate { Note.SelectionLength = 0; Fingerprint.SelectionLength = 0; PageButton.Focus(); };
    }
}
