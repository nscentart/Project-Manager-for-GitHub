using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class Header : Control {
    public string RepoName = "", Description = "", Read = "";
    public bool OldRead;
    public string Pill; public bool Publish;
    public bool HasSwitch, Follow;
    public event EventHandler Switch;
    Rectangle zone;

    public Header() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics; Func<int, int> S = Theme.S;
        g.Clear(BackColor);
        int x = S(24), right = Width - S(24), lowerY = S(46), lowerHeight = S(20), dx = right;
        zone = Rectangle.Empty;
        if (HasSwitch) {
            string s = Language.T("appearance.following_reports");
            int wt = Theme.MeasureWidth(g, s, Theme.Normal), wi = S(34), ai = S(18), yi = S(18);
            var tr = new Rectangle(right - wi, yi, wi, ai);
            Theme.Full(g, tr, ai / 2, Follow ? Theme.Azure : Theme.Border);
            Theme.Full(g, new Rectangle(Follow ? tr.Right - ai + S(2) : tr.X + S(2), yi + S(2), ai - S(4), ai - S(4)), (ai - S(4)) / 2, Color.White);
            Theme.T(g, s, Theme.Normal, tr.X - S(8) - wt, yi - S(1), wt + 1, ai + S(2), Theme.Ink);
            zone = new Rectangle(tr.X - S(8) - wt, yi - S(4), wt + S(8) + wi, ai + S(8));
            dx = zone.X;
        }
        int wl = Theme.MeasureWidth(g, Read, Theme.Small);
        Theme.T(g, Read, Theme.Small, right - wl, lowerY, wl + 1, lowerHeight, OldRead ? Theme.AmberDark : Theme.Gray);
        int wp = 0, ap = S(20);
        if (Pill != null) wp = Theme.MeasureWidth(g, Pill, Theme.SmallF) + S(14) + S(10);
        int wn = Math.Min(Theme.MeasureWidth(g, RepoName, Theme.Title), dx - x - wp - S(16));
        Theme.T(g, RepoName, Theme.Title, x, S(10), wn, S(36), Theme.Ink);
        if (Pill != null) Theme.DrawPill(g, Pill, Theme.SmallF, x + wn + S(10), S(18), ap, Publish ? Theme.GreenTint : Theme.Backdrop, Publish ? Theme.GreenDark : Theme.Gray);
        Theme.T(g, Description, Theme.Normal, x, lowerY, right - wl - S(16) - x, lowerHeight, Theme.Gray);
    }
    protected override void OnMouseMove(MouseEventArgs e) { Cursor = zone.Contains(e.Location) ? Cursors.Hand : Cursors.Default; base.OnMouseMove(e); }
    protected override void OnMouseDown(MouseEventArgs e) {
        if (zone.Contains(e.Location) && Switch != null) Switch(this, EventArgs.Empty);
        base.OnMouseDown(e);
    }
}
