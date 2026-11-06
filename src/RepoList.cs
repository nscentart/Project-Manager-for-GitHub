using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class RepoList : ListBox {
    public event Action GroupClicked;
    const int WmLButtonDown = 0x201, WmLButtonDblClk = 0x203;

    protected override void WndProc(ref Message m) {
        if (m.Msg == WmLButtonDown || m.Msg == WmLButtonDblClk) {
            int xy = (int)m.LParam.ToInt64();
            var p = new Point((short)(xy & 0xFFFF), (short)((xy >> 16) & 0xFFFF));
            int i = IndexFromPoint(p);
            if (i >= 0 && i < Items.Count && GetItemRectangle(i).Contains(p)) {
                var r = Items[i] as RepoRow;
                if (r != null && r.Group) { Focus(); if (GroupClicked != null) GroupClicked(); return; }
            }
        }
        base.WndProc(ref m);
    }

    public void ClickIndex(int index) {
        var r = GetItemRectangle(index);
        var m = Message.Create(Handle, WmLButtonDown, IntPtr.Zero, (IntPtr)(((r.Y + r.Height / 2) << 16) | (r.X + r.Width / 2)));
        WndProc(ref m);
    }

    public RepoList() {
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = Theme.S(56);
        BorderStyle = BorderStyle.None; IntegralHeight = false; BackColor = Theme.Background; ForeColor = Theme.Text;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] internal static extern int SetWindowTheme(IntPtr h, string theme, string joinedList);

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var g = e.Graphics; var b = e.Bounds; var r = (RepoRow)Items[e.Index];
        using (var f = new SolidBrush(Theme.Background)) g.FillRectangle(f, b);
        Func<int, int> S = Theme.S;
        var c = new Rectangle(b.X + S(10), b.Y + S(3), b.Width - S(20), b.Height - S(6));
        if (r.Group) {
            int cy = c.Y + c.Height / 2, xg = c.X + S(12);
            var points = r.Open ? new[] { new Point(xg, cy - S(3)), new Point(xg + S(10), cy - S(3)), new Point(xg + S(5), cy + S(3)) }
                                 : new[] { new Point(xg + S(2), cy - S(5)), new Point(xg + S(8), cy), new Point(xg + S(2), cy + S(5)) };
            var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fr = new SolidBrush(Theme.Muted)) g.FillPolygon(fr, points);
            g.SmoothingMode = v;
            Theme.T(g, r.Name, Theme.Strong, xg + S(18), c.Y, c.Right - xg - S(28), c.Height, Theme.Muted);
            return;
        }
        if ((e.State & DrawItemState.Selected) != 0) {
            Theme.Full(g, c, S(8), Theme.Capsule);
            Theme.Full(g, new Rectangle(c.X + S(1), c.Y + S(10), S(3), c.Height - S(20)), S(1), Theme.Azure);
        }
        bool missing = r.R != null && r.R.Missing, followed = r.R == null || r.R.Followed;
        int x = c.X + S(12), right = c.Right - S(10), upperY = c.Y + S(6), upperHeight = S(20), lowerY = c.Y + S(27), lowerHeight = S(17);
        int d = right;
        if (r.Work > 0) { string s = r.Work.ToString(); int w = Theme.MeasureWidth(g, s, Theme.SmallF) + S(14); d -= w; Theme.DrawPill(g, s, Theme.SmallF, d, upperY + S(1), S(18), Theme.Azure, Color.White); d -= S(4); }
        if (r.Quarantine > 0) { string s = r.Quarantine.ToString(); int w = Theme.MeasureWidth(g, s, Theme.SmallF) + S(14); d -= w; Theme.DrawPill(g, s, Theme.SmallF, d, upperY + S(1), S(18), Theme.Amber, Theme.Ink); d -= S(4); }
        Theme.T(g, r.Name, Theme.Strong, x, upperY, d - x - S(4), upperHeight, missing ? Theme.Muted : followed ? Theme.Text : Color.FromArgb(200, 205, 214));
        if (r.R == null) { Theme.T(g, r.Below, Theme.Small, x, lowerY, right - x, lowerHeight, Theme.Muted); return; }
        int wp = r.R.Private ? Theme.DrawPill(g, Language.T("common.private"), Theme.Small, x, lowerY, lowerHeight, Theme.Line, Theme.Text)
                             : Theme.DrawPill(g, Language.T("common.public"), Theme.Small, x, lowerY, lowerHeight, Theme.Lime, Theme.Ink);
        Theme.T(g, missing ? Language.T("common.no_longer_github") : followed ? "" : Language.T("common.not_followed"), Theme.Small, x + wp + S(7), lowerY, right - x - wp - S(7), lowerHeight, missing ? Theme.Amber : Theme.Muted);
    }
}
