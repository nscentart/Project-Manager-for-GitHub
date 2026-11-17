using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class ConfirmDialog : OperationDialog {
    public TextBox NameBox;
    public ThemedButton YesButton, NoButton;
    public Label Story, Asks;
    public bool Done;
    public RepoFacts Seen;
    public volatile bool Stopped;
    public bool Retry;
    readonly bool write;
    bool withSteps;
    string confirmedText = "";

    public ConfirmDialog(WriteGateway p, string repo, string title, string button, bool writeName, bool severe, Func<Client, RepoFacts, string> readFacts, Action<Client, RepoFacts, string> does) : base(p, title, 600, 460) {
        write = writeName;
        AddLabel(title, Theme.Large, Theme.Text, S(16), S(34));
        Story = AddLabel(Language.T("management.reading_again_github"), Theme.Normal, Theme.Text, S(56), S(224));
        Asks = AddLabel("", Theme.Normal, Theme.Muted, S(284), S(22));
        NameBox = Field("", new Rectangle(M, S(308), Lu, S(26)), false);
        NameBox.Visible = Asks.Visible = write;
        NameBox.TextChanged += delegate { Redo(); };
        Line(S(340), S(62));
        var r = new Rectangle(M + Lu - S(240), S(410), S(240), S(36));
        EventHandler si = delegate {
            var v = Seen; string written = NameBox.Text;
            Stopped = false; withSteps = false;
            Work<bool>(c => { does(c, v, written); return true; }, x => { Done = true; DialogResult = DialogResult.OK; });
        };
        YesButton = severe ? Red(button, r, si) : CreateButton(button, true, r, si);
        NoButton = CreateButton(Language.T("common.cancel"), false, new Rectangle(r.X - S(120), S(410), S(110), S(36)), delegate {
            if (!Working) { Close(); return; }
            Stopped = true; Story.Text = Language.T("management.stopping"); Redo();
        });
        Shown += delegate {
            if (write) NameBox.Focus(); else NoButton.Focus();
            Work(c => { var f = Writer.Fetch(c, repo); return new KeyValuePair<RepoFacts, string>(f, readFacts(c, f)); }, x => {
                if (Stopped) { Close(); return; }
                Seen = x.Key;
                Story.Text = confirmedText = V(Describe(x.Key)) + (P.Login != null ? Language.T("management.you_are_acting_as", "login", V(P.Login)) : "") + "\n\n" + x.Value;
                Asks.Text = Language.T("management.confirm_type", "name", V(x.Key.Name));
            });
        };
        Redo();
    }

    public void Step(string text) {
        Check();
        InUi((Action)(() => { if (Stopped) return; withSteps = true; Story.Text = V(text); Redo(); }));
    }

    public void Check() { if (Stopped) throw new CheckStoppedException(Language.T("management.check_stopped_nothing_been_done")); }

    protected override bool EnabledWhileBusy(ThemedButton b) { return b == NoButton && withSteps && !Stopped && (Seen == null || Retry); }
    protected override bool Can(ThemedButton b) { return b == NoButton || (Seen != null && (!write || NameBox.Text == Seen.Name)); }
    protected override void Mistake(Exception e) { if (Stopped && Seen == null) { Close(); return; } base.Mistake(e); if (Seen != null) { Story.Text = confirmedText; return; } Story.Text = Language.T("management.not_doing_anything"); Story.Height = S(24); NameBox.Visible = Asks.Visible = false; Notice.Bounds = new Rectangle(M, S(84), Lu, S(318)); }
}
