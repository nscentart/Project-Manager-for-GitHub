using System;
using System.Drawing;
using System.Windows.Forms;

public class TextInputDialog : OperationDialog {
    public TextBox Value;
    public ThemedButton YesButton, NoButton;
    public Func<string, bool> Valid;

    public TextInputDialog(WriteGateway p, string title, string explain, string val, bool lines, string button, Action<Client, string> does) : base(p, title, 600, lines ? 340 : 270) {
        AddLabel(title, Theme.Large, Theme.Text, S(16), S(34));
        AddLabel(explain, Theme.Normal, Theme.Muted, S(54), S(46));
        Value = Field(val, new Rectangle(M, S(104), Lu, lines ? S(96) : S(26)), lines);
        Value.TextChanged += delegate { Redo(); };
        int y = lines ? S(204) : S(134);
        Line(y, S(70));
        var r = new Rectangle(M + Lu - S(170), y + S(78), S(170), S(36));
        YesButton = CreateButton(button, true, r, delegate {
            string t = Value.Text.Trim();
            if (does == null) { DialogResult = DialogResult.OK; return; }
            Work<bool>(c => { does(c, t); return true; }, x => DialogResult = DialogResult.OK);
        });
        NoButton = CreateButton(Language.T("common.cancel"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(36)), delegate { Close(); });
        Shown += delegate { Value.Focus(); Redo(); };
    }

    protected override bool Can(ThemedButton b) { return b == NoButton || Valid == null || Valid(Value.Text.Trim()); }
}
