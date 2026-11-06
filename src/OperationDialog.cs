using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

public class OperationDialog : Dialog {
    protected readonly WriteGateway P;
    public Label Notice;
    public bool Working, Changed;
    protected readonly List<ThemedButton> Buttons = new List<ThemedButton>();
    protected readonly int M, Lu;

    protected int S(int v) { return Theme.S(v); }
    protected static string V(string s) { return Screener.MakeVisible(s ?? ""); }

    public OperationDialog(WriteGateway p, string title, int width, int height) {
        P = p;
        Text = title;
        ClientSize = new Size(S(width), S(height));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Normal;
        try { Icon = Theme.AppIcon(); } catch { }
        M = S(24); Lu = S(width) - M * 2;
        FormClosing += (o, e) => { if (Working) e.Cancel = true; };
        Load += delegate { Theme.DarkBar(this); };
    }

    protected Label AddLabel(string text, Font f, Color c, int y, int alt) {
        var l = new Label { Text = text, Font = f, ForeColor = c, BackColor = Theme.Background, Bounds = new Rectangle(M, y, Lu, alt), UseMnemonic = false };
        Controls.Add(l);
        return l;
    }

    protected void Line(int y, int alt) {
        Notice = new Label { ForeColor = Theme.Amber, BackColor = Theme.Background, Bounds = new Rectangle(M, y, Lu, alt), UseMnemonic = false };
        Controls.Add(Notice);
    }

    protected ThemedButton CreateButton(string text, bool primary, Rectangle r, EventHandler click) {
        var b = new ThemedButton { Text = text, Bounds = r, Background = primary ? Theme.Azure : Theme.Capsule, TextColor = primary ? Color.White : Theme.Text, Line = primary ? Color.Empty : Theme.Line };
        b.Click += click;
        Controls.Add(b); Buttons.Add(b);
        return b;
    }

    protected ThemedButton Red(string text, Rectangle r, EventHandler click) {
        var b = CreateButton(text, false, r, click);
        b.Background = Theme.Red; b.TextColor = Color.White; b.Line = Color.Empty;
        return b;
    }

    protected TextBox Field(string text, Rectangle r, bool lines) {
        var t = new TextBox { Multiline = lines, Text = text ?? "", Bounds = r, BackColor = Color.FromArgb(24, 25, 29), ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AcceptsReturn = lines, ScrollBars = lines ? ScrollBars.Vertical : ScrollBars.None };
        Controls.Add(t);
        return t;
    }

    protected ListView CreateList(Rectangle r, string[] columns, int[] widths) {
        int top = S(22), x = r.X;
        var l = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.None, BackColor = Theme.Capsule, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Bounds = new Rectangle(r.X, r.Y + top, r.Width, r.Height - top), HideSelection = false };
        for (int i = 0; i < columns.Length; i++) {
            l.Columns.Add(columns[i], S(widths[i]));
            Controls.Add(new Label { Text = columns[i], ForeColor = Theme.Muted, BackColor = Theme.Background, Bounds = new Rectangle(x + S(2), r.Y, S(widths[i]) - S(2), top), UseMnemonic = false });
            x += S(widths[i]);
        }
        l.SelectedIndexChanged += delegate { Redo(); };
        Controls.Add(l);
        return l;
    }

    protected virtual bool Can(ThemedButton b) { return true; }
    protected void Redo() { foreach (var b in Buttons) b.Enabled = (!Working && Can(b)) || EnabledWhileBusy(b); Explain(); }
    protected virtual bool EnabledWhileBusy(ThemedButton b) { return false; }
    protected virtual void Explain() { }

    public void ShowNote(string text) { if (Notice != null) { Notice.ForeColor = Theme.Amber; Notice.Text = text; } }
    protected void ShowSuccess(string text) { Changed = true; if (Notice != null) { Notice.ForeColor = Theme.Lime; Notice.Text = text; } }

    protected static string FormatFindings(IEnumerable<Finding> l) {
        var r = l.ToList();
        return string.Join("\n", r.Take(10).Select(x => V(x.ToString()))) + (r.Count > 10 ? Language.T("management.and_more", "part", (r.Count - 10)) : "");
    }

    protected string Sentence(Exception e) {
        var s = e as WriteException;
        if (s != null) return s.Findings != null ? s.Message + "\n" + FormatFindings(s.Findings) : s.Message;
        if (e is AccessException) return e.Message;
        DataStore.Log("GitHub work: " + e.GetType().Name);
        return Language.T("management.something_went_wrong_dont_know");
    }

    protected virtual void Mistake(Exception e) { ShowNote(Sentence(e)); }

    protected void Work<T>(Func<Client, T> work, Action<T> after) {
        Working = true; Redo();
        new Thread(() => {
            T r = default(T); Exception x = null;
            try { r = work(P.Route()); } catch (Exception e) { x = e; }
            try {
                BeginInvoke((Action)(() => {
                    Working = false;
                    if (x != null) Mistake(x); else after(r);
                    if (!IsDisposed) Redo();
                }));
            } catch (InvalidOperationException) { }
        }) { IsBackground = true }.Start();
    }

    protected void InUi(Action f) { try { if (!IsDisposed) BeginInvoke(f); } catch (InvalidOperationException) { } }

    public static string Describe(RepoFacts f) {
        return Language.T("management.here_you", "name", f.Name, "part", (f.Private ? Language.T("common.private") : Language.T("common.public")), "part2", (f.Archived ? Language.T("management.archived") : ""), "f", Stars(f), "f2", Forks(f), "f3", Permissions.Role(f));
    }

    protected static string Stars(RepoFacts f) { return Language.N("management.stars", f.Stars); }
    protected static string Forks(RepoFacts f) { return Language.N("management.forks", f.Forks); }
}
