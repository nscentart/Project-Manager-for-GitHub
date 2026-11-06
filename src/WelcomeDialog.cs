using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class WelcomeDialog : Dialog {
    public static string FolderForScreenshot;
    public enum Choice { Cancel, Install, Without }
    public Choice Response = Choice.Cancel;

    public WelcomeDialog(string already) {
        using (var g = CreateGraphics()) Theme.K = g.DpiX / 96f;
        Func<int, int> S = Theme.S;
        Text = "Project Manager " + Program.Version;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        try { Icon = Theme.AppIcon(); } catch { }
        string explain = Language.T("installer.install_copies_program_adds_start", "part", (already == null ? "" : Language.T("installer.computer_already_version_install_replaces", "part", (already.Length > 0 ? already : Language.T("installer.earlier")), "version", Program.Version)), "folder", FolderForScreenshot ?? Installer.Real.Folder);
        int alt = TextRenderer.MeasureText(explain, Font, new Size(S(512), 0), TextFormatFlags.WordBreak).Height + S(10);
        var logo = new Panel { Bounds = new Rectangle(S(24), S(22), S(52), S(52)), BackColor = Theme.Background };
        logo.Paint += (o, e) => Theme.Logo(e.Graphics, new Rectangle(0, 0, logo.Width - 1, logo.Height - 1));
        var title = new Label { Text = "Project Manager", Font = Theme.Large, Bounds = new Rectangle(S(88), S(20), S(448), S(32)), UseMnemonic = false };
        var below = new Label { Text = Language.T("installer.github_repos_reports_one_place"), ForeColor = Theme.Muted, Bounds = new Rectangle(S(90), S(52), S(446), S(22)), UseMnemonic = false };
        var text = new Label { Text = explain, Bounds = new Rectangle(S(24), S(92), S(512), alt), UseMnemonic = false };
        int yb = S(100) + alt, hb = S(38);
        ClientSize = new Size(S(560), yb + hb + S(22));
        var without = new ThemedButton { Text = Language.T("installer.use_without_installing"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, Bounds = new Rectangle(S(24), yb, S(180), hb) };
        var cancel = new ThemedButton { Text = Language.T("common.cancel"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, Bounds = new Rectangle(S(536) - S(100), yb, S(100), hb), DialogResult = DialogResult.Cancel };
        var install = new ThemedButton { Text = Language.T("common.install"), Background = Theme.Azure, TextColor = Color.White, Line = Color.Empty, Bounds = new Rectangle(cancel.Left - S(10) - S(120), yb, S(120), hb) };
        install.Click += delegate { Response = Choice.Install; DialogResult = DialogResult.OK; };
        without.Click += delegate { Response = Choice.Without; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { logo, title, below, text, without, install, cancel });
        AcceptButton = install; CancelButton = cancel;
        Load += delegate { Theme.DarkBar(this); };
    }
}
