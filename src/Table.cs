using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class Table : ListView {
    public Table() {
        DoubleBuffered = true; OwnerDraw = true;
        View = View.Details; FullRowSelect = true; MultiSelect = true; HideSelection = false;
        BorderStyle = BorderStyle.None; ShowItemToolTips = false; HeaderStyle = ColumnHeaderStyle.Nonclickable;
        SmallImageList = new ImageList { ImageSize = new Size(1, Theme.S(28)) };
    }
    int[] weights; bool fit;
    void Fit() {
        if (fit || Columns.Count == 0 || ClientSize.Width <= 0) return;
        fit = true;
        if (weights == null) weights = Columns.Cast<ColumnHeader>().Select(c => c.Width).ToArray();
        int tot = weights.Sum(), rest = ClientSize.Width;
        for (int i = 0; i < weights.Length - 1; i++) { int w = ClientSize.Width * weights[i] / tot; Columns[i].Width = w; rest -= w; }
        Columns[weights.Length - 1].Width = rest;
        fit = false;
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Fit(); }
    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) {
        using (var b = new SolidBrush(Theme.Backdrop)) e.Graphics.FillRectangle(b, e.Bounds);
        using (var n = new Pen(Theme.Border)) e.Graphics.DrawLine(n, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        Theme.T(e.Graphics, e.Header.Text, Theme.SmallF, e.Bounds.X + Theme.S(8), e.Bounds.Y, e.Bounds.Width - Theme.S(10), e.Bounds.Height, Theme.Gray);
    }
    protected override void OnDrawItem(DrawListViewItemEventArgs e) { }
    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) {
        using (var b = new SolidBrush(e.Item.Selected ? Theme.Tint : Color.White)) e.Graphics.FillRectangle(b, e.Bounds);
        Theme.T(e.Graphics, e.SubItem.Text, Theme.Normal, e.Bounds.X + Theme.S(8), e.Bounds.Y, e.Bounds.Width - Theme.S(10), e.Bounds.Height, Theme.Ink);
    }
}
