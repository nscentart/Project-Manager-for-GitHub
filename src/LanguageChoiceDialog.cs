using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class LanguageChoiceDialog : Dialog {
    public string Code;
    readonly Label question;
    readonly ThemedButton next;

    public LanguageChoiceDialog(string proposal) {
        using (var g = CreateGraphics()) Theme.K = g.DpiX / 96f;
        Func<int, int> S = Theme.S;
        Code = Language.Valid(proposal) ? proposal : Language.Fallback;
        Language.Use(Code);
        Text = "Project Manager";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        ClientSize = new Size(S(340), S(310));
        try { Icon = Theme.AppIcon(); } catch { }
        question = new Label { Font = Theme.Large, Bounds = new Rectangle(S(24), S(16), S(292), S(36)), UseMnemonic = false };
        var languageList = new ListBox { BorderStyle = BorderStyle.None, BackColor = Theme.Capsule, ForeColor = Theme.Text, IntegralHeight = false, ItemHeight = S(32),
                                   DrawMode = DrawMode.OwnerDrawFixed, Bounds = new Rectangle(S(24), S(58), S(292), 5 * S(32) + S(2)) };
        foreach (var c in Language.Codes) languageList.Items.Add(Language.NameOf(c));
        languageList.DrawItem += (o, e) => {
            if (e.Index < 0) return;
            bool choice = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(choice ? Theme.Azure : Theme.Capsule)) e.Graphics.FillRectangle(b, e.Bounds);
            Theme.T(e.Graphics, (string)languageList.Items[e.Index], Theme.Normal, e.Bounds.X + S(10), e.Bounds.Y, e.Bounds.Width - S(20), e.Bounds.Height, choice ? Color.White : Theme.Text);
        };
        next = new ThemedButton { Background = Theme.Azure, TextColor = Color.White, Line = Color.Empty, Bounds = new Rectangle(S(24), S(58) + 5 * S(32) + S(14), S(292), S(40)), DialogResult = DialogResult.OK };
        AcceptButton = next;
        languageList.SelectedIndex = Array.IndexOf(Language.Codes, Code);
        languageList.SelectedIndexChanged += delegate {
            if (languageList.SelectedIndex < 0) return;
            Code = Language.Codes[languageList.SelectedIndex];
            Language.Use(Code);
            Texts();
        };
        Controls.AddRange(new Control[] { question, languageList, next });
        Load += delegate { Theme.DarkBar(this); };
        Texts();
    }

    void Texts() { question.Text = Language.T("language.choose_language"); next.Text = Language.T("language.continue"); next.Invalidate(); }
}
