using System;
using System.Drawing;
using System.Windows.Forms;

public class LicenseDialog : Dialog {
    readonly string folder;
    readonly Label stateLabel, detailLabel, resultLabel;
    public readonly TextBox Box;
    public readonly ThemedButton UseButton, RemoveButton;
    public Func<bool> AskRemove;
    public bool Changed;

    public string StateText { get { return stateLabel.Text; } }
    public string DetailText { get { return detailLabel.Text; } }
    public string ResultText { get { return resultLabel.Text; } }

    public LicenseDialog(string dataFolder) {
        folder = dataFolder;
        Func<int, int> S = Theme.S;
        Text = Language.T("common.license");
        try { Icon = Theme.AppIcon(); } catch { }
        ClientSize = new Size(S(560), S(500));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        int m = S(24), lu = S(560) - m * 2;
        Func<string, Font, Color, int, int, Label> makeLabel = (x, fo, co, y, at) => new Label { Text = x, Font = fo, ForeColor = co, BackColor = Theme.Background, Bounds = new Rectangle(m, y, lu, at), UseMnemonic = false };
        stateLabel = makeLabel("", Theme.Large, Theme.Text, S(16), S(64));
        stateLabel.AutoEllipsis = true;
        detailLabel = makeLabel("", Theme.Normal, Theme.Muted, S(82), S(44));
        var pasteLabel = makeLabel(Language.T("license.paste_license"), Theme.Strong, Theme.Text, S(134), S(22));
        Box = new TextBox { Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(24, 25, 29), ForeColor = Theme.Text,
                                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.5f), Bounds = new Rectangle(m, S(158), lu, S(84)) };
        resultLabel = makeLabel("", Theme.Normal, Theme.Amber, S(248), S(44));
        UseButton = new ThemedButton { Text = Language.T("license.use_license"), Background = Theme.Azure, TextColor = Color.White, Line = Color.Empty, Bounds = new Rectangle(m, S(296), S(180), S(38)) };
        RemoveButton = new ThemedButton { Text = Language.T("license.remove_license"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, Bounds = new Rectangle(m + S(190), S(296), S(150), S(38)) };
        var close = new ThemedButton { Text = Language.T("common.close"), Background = Theme.Capsule, TextColor = Theme.Text, Line = Theme.Line, Bounds = new Rectangle(m + lu - S(110), S(296), S(110), S(38)), DialogResult = DialogResult.Cancel };
        var thread = new Label { BackColor = Theme.Line, Bounds = new Rectangle(m, S(350), lu, 1) };
        var saleLabel = makeLabel(AppLicense.Sale, Theme.Normal, Theme.Text, S(362), S(44));
        var limitsLabel = makeLabel(AppLicense.Limits, Theme.Small, Theme.Muted, S(412), S(72));
        AskRemove = () => MessageBox.Show(this, Language.T("license.remove_license_computer_personal_license"), Language.T("common.license"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        UseButton.Click += delegate { Use(); };
        RemoveButton.Click += delegate { Remove(); };
        Controls.AddRange(new Control[] { stateLabel, detailLabel, pasteLabel, Box, resultLabel, UseButton, RemoveButton, close, thread, saleLabel, limitsLabel });
        CancelButton = close;
        Load += delegate { Theme.DarkBar(this); };
        Render();
    }

    void Render() {
        var e = AppLicense.Now(folder);
        stateLabel.Text = AppLicense.LicenseCaption(e);
        detailLabel.Text = AppLicense.Detail(e);
        detailLabel.ForeColor = AppLicense.Problem(e, "") == null ? Theme.Muted : Theme.Amber;
        RemoveButton.Enabled = e.State != LicenseState.Missing;
    }

    void Of(string text, bool well) { resultLabel.Text = text; resultLabel.ForeColor = well ? Theme.Lime : Theme.Amber; }

    void Use() {
        if (string.IsNullOrEmpty(License.Clean(Box.Text ?? "") ?? "x")) { Of(Language.T("license.paste_license_text_first"), false); return; }
        var e = AppLicense.Check(Box.Text);
        if (!e.Valid) { Of(AppLicense.Problem(e, Language.T("license.this_license")), false); return; }
        if (!License.Write(folder, Box.Text)) { Of(Language.T("license.license_good_cant_save_programs"), false); return; }
        Box.Text = ""; Changed = true;
        Render();
        Of(Language.T("license.license_saved"), true);
    }

    void Remove() {
        if (!AskRemove()) return;
        if (!License.Remove(folder)) { Of(Language.T("license.cant_remove_license_file_try"), false); Render(); return; }
        Changed = true;
        Render();
        Of(Language.T("license.license_removed"), true);
    }
}
