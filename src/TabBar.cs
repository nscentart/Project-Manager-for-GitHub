using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class TabBar : Control {
    public class Entry { public string Name; public int Number; public bool Alarm; public Control Page; public Rectangle Zone; }
    public List<Entry> Entries = new List<Entry>();
    public int Choice;
    public event EventHandler Changed;

    public TabBar() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }
    public void Choose(int i) {
        Choice = i;
        for (int v = 0; v < Entries.Count; v++) Entries[v].Page.Visible = v == i;
        Invalidate();
        if (Changed != null) Changed(this, EventArgs.Empty);
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics; Func<int, int> S = Theme.S;
        g.Clear(BackColor);
        using (var n = new Pen(Theme.Border)) g.DrawLine(n, 0, Height - 1, Width, Height - 1);
        int x = S(24);
        for (int i = 0; i < Entries.Count; i++) {
            var v = Entries[i];
            if (i == 2) {
                using (var n = new Pen(Theme.Border)) g.DrawLine(n, x + S(2), S(12), x + S(2), Height - S(12));
                x += S(16);
                string gr = Language.T("common.reports");
                int wg = Theme.MeasureWidth(g, gr, Theme.Small);
                Theme.T(g, gr, Theme.Small, x, 0, wg + 1, Height - 1, Theme.Gray);
                x += wg + S(16);
            }
            bool sc = i == Choice;
            int w = Theme.MeasureWidth(g, v.Name, Theme.Strong);
            Theme.T(g, v.Name, Theme.Strong, x, 0, w + 1, Height - 1, sc ? Theme.Ink : Theme.Gray);
            int tot = w;
            if (v.Number > 0) {
                tot += S(6) + Theme.DrawPill(g, v.Number.ToString(), Theme.SmallF, x + w + S(6), (Height - S(18)) / 2, S(18),
                    v.Alarm ? Theme.AmberTint : Theme.Backdrop, v.Alarm ? Theme.AmberDark : Theme.Gray);
            }
            if (sc) Theme.Full(g, new Rectangle(x, Height - S(3), tot, S(3)), S(1), Theme.Azure);
            v.Zone = new Rectangle(x - S(8), 0, tot + S(16), Height);
            x += tot + S(20);
        }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        Cursor = Entries.Any(v => v.Zone.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        for (int i = 0; i < Entries.Count; i++) if (Entries[i].Zone.Contains(e.Location)) Choose(i);
        base.OnMouseDown(e);
    }
}
