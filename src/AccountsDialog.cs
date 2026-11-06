using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class AccountsDialog : Dialog {
    public GitHubAccount Disconnected;
    public bool Add;

    public AccountsDialog(Archive a) {
        Func<int, int> S = Theme.S;
        Text = Language.T("common.account");
        try { Icon = Theme.AppIcon(); } catch { }
        ClientSize = new Size(S(520), S(400));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        int m = S(24), lu = S(520) - m * 2;
        var t = new Label { Text = Language.T("window.connected_accounts"), Font = Theme.Large, BackColor = Theme.Background, Bounds = new Rectangle(m, S(16), lu, S(34)), UseMnemonic = false };
        var s = new Label { Text = Language.T("window.switch_between_account_drop_down"),
                            ForeColor = Theme.Muted, BackColor = Theme.Background, Bounds = new Rectangle(m, S(52), lu, S(44)), UseMnemonic = false };
        var l = new ListBox { BackColor = Theme.Capsule, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, IntegralHeight = false, Bounds = new Rectangle(m, S(104), lu, S(170)), ItemHeight = S(24), DrawMode = DrawMode.OwnerDrawFixed };
        int beyond = TextRenderer.MeasureText(s.Text, Font, new Size(lu, 0), TextFormatFlags.WordBreak).Height + S(4) - s.Height;
        if (beyond > 0) { s.Height += beyond; l.Top += beyond; l.Height -= beyond; }
        foreach (var c in a.Accounts) l.Items.Add(c);
        l.DrawItem += (o, e) => {
            if (e.Index < 0) return;
            bool up = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(up ? Theme.Azure : Theme.Capsule)) e.Graphics.FillRectangle(b, e.Bounds);
            var c = (GitHubAccount)l.Items[e.Index];
            string d = (AccountAccess.Equal(c.Login, a.Login) ? Language.T("window.in_use") : "") + (c.Origin == AccountAccess.OriginBrowser ? Language.T("window.access_kept_windows") : Language.T("window.access_through_git"));
            Theme.T(e.Graphics, c.Login, Theme.Strong, e.Bounds.X + S(8), e.Bounds.Y, S(190), e.Bounds.Height, Color.White);
            Theme.T(e.Graphics, d, Theme.Small, e.Bounds.X + S(200), e.Bounds.Y, e.Bounds.Width - S(208), e.Bounds.Height, up ? Color.White : Theme.Muted);
        };
        if (a.Accounts.Count > 0) l.SelectedIndex = Math.Max(0, a.Accounts.FindIndex(c => AccountAccess.Equal(c.Login, a.Login)));
        var removeButton = new ThemedButton { Text = Language.T("window.disconnect"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, Bounds = new Rectangle(m, S(284), S(120), S(36)), Enabled = a.Accounts.Count > 0 };
        var note = new Label { Text = Language.T("window.repos_stay_archive_withdraw_permission"), Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Theme.Background, Bounds = new Rectangle(m + S(130), S(284), lu - S(130), S(36)), UseMnemonic = false };
        var newValue = new ThemedButton { Text = Language.T("common.connect_another_account"), Background = Theme.Azure, TextColor = Color.White, Line = Color.Empty, Bounds = new Rectangle(m, S(342), S(220), S(38)) };
        var close = new ThemedButton { Text = Language.T("common.close"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, Bounds = new Rectangle(m + lu - S(110), S(342), S(110), S(38)), DialogResult = DialogResult.Cancel };
        removeButton.Click += delegate {
            var c = l.SelectedItem as GitHubAccount;
            if (c == null) return;
            if (MessageBox.Show(this, Language.T("window.disconnect_repos_decisions_stay_archive", "login", c.Login), Language.T("common.account"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Disconnected = c; DialogResult = DialogResult.OK;
        };
        newValue.Click += delegate { Add = true; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { t, s, l, removeButton, note, newValue, close });
        CancelButton = close;
        Load += delegate { Theme.DarkBar(this); };
    }
}
