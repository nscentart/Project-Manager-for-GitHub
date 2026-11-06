using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class RemoveDialog : Dialog {
    public ThemedButton RemoveButton, CancelChoiceButton;
    public string Story;

    public RemoveDialog(AccountAccess.Removal t) {
        Func<int, int> S = Theme.S;
        Func<string, string> V = x => Screener.MakeVisible(x ?? "");
        Text = Language.T("missing.remove_list");
        try { Icon = Theme.AppIcon(); } catch { }
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        int m = S(24), lu = S(540) - m * 2, y = S(18);
        Func<string, Font, Color, Label> line = (text, fo, co) => {
            int h = TextRenderer.MeasureText(text, fo, new Size(lu, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + S(4);
            var l = new Label { Text = text, Font = fo, ForeColor = co, BackColor = Theme.Background, Bounds = new Rectangle(m, y, lu, h), UseMnemonic = false };
            Controls.Add(l);
            y += h + S(8);
            return l;
        };
        line(t.Repo.Count == 1 ? Language.T("missing.remove_list_2", "name", V(t.Repo[0])) : Language.N("missing.remove_repos_list", t.Repo.Count), Theme.Large, Theme.Text);
        if (t.Repo.Count > 1) {
            string names = string.Join(", ", t.Repo.Take(8).Select(V)) + (t.Repo.Count > 8 ? ", " + Language.N("missing.and_more", t.Repo.Count - 8) : "");
            line(names, Theme.Normal, Theme.Muted);
        }
        var parts = new List<string> { Language.N("missing.downloaded_texts", t.Texts) };
        if (t.Decisions > 0) parts.Add(Language.N("missing.decisions_made", t.Decisions));
        parts.Add(Language.N("missing.days_history", t.HistoryDays));
        Story = Language.T("missing.nothing_changes_github_only_row") + "\n" + Language.T("missing.lost_computer", "list", string.Join(", ", parts)) + "\n" + Language.T("missing.repo_comes_back_github_returns");
        foreach (var r in Story.Split('\n')) line(r, Theme.Normal, Theme.Text);
        y += S(8);
        string textYes = t.Repo.Count > 1 ? Language.T("missing.remove_all") : Language.T("missing.remove_list");
        RemoveButton = new ThemedButton { Text = textYes, Background = Theme.Azure, TextColor = Color.White, Line = Color.Empty, DialogResult = DialogResult.OK };
        CancelChoiceButton = new ThemedButton { Text = Language.T("common.cancel"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, DialogResult = DialogResult.Cancel };
        int ls = Math.Max(S(120), RemoveButton.GetPreferredSize(Size.Empty).Width), ln = Math.Max(S(110), CancelChoiceButton.GetPreferredSize(Size.Empty).Width);
        RemoveButton.Bounds = new Rectangle(m + lu - ls, y, ls, S(36));
        CancelChoiceButton.Bounds = new Rectangle(m + lu - ls - S(10) - ln, y, ln, S(36));
        Controls.AddRange(new Control[] { CancelChoiceButton, RemoveButton });
        ClientSize = new Size(S(540), y + S(36) + S(18));
        CancelButton = CancelChoiceButton;
        Load += delegate { Theme.DarkBar(this); };
        Shown += delegate { CancelChoiceButton.Focus(); };
    }
}
