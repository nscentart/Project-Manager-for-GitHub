using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class ThemedButton : Button {
    public Color Background = Color.White, TextColor = Theme.Ink, Line = Theme.Border;
    bool above, down;

    public ThemedButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
    }
    public override Size GetPreferredSize(Size proposal) {
        using (var g = CreateGraphics()) return new Size(Theme.MeasureWidth(g, Text, Theme.Strong) + Theme.S(26), Theme.S(32));
    }
    protected override void OnMouseEnter(EventArgs e) { above = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { above = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    static Color Toward(Color c, Color d, float t) { return Color.FromArgb((int)(c.R + (d.R - c.R) * t), (int)(c.G + (d.G - c.G) * t), (int)(c.B + (d.B - c.B) * t)); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(Parent == null ? BackColor : Parent.BackColor);
        Color f = !Enabled ? Toward(Background, Parent == null ? BackColor : Parent.BackColor, 0.55f) : down ? Toward(Background, Color.Gray, 0.25f) : above ? Toward(Background, TextColor, 0.08f) : Background;
        var r = new Rectangle(0, 0, Width, Height);
        Theme.Card(g, r, Theme.S(7), f, Line == Color.Empty ? f : Line);
        TextRenderer.DrawText(g, Text, Theme.Strong, r, Enabled ? TextColor : Toward(TextColor, f, 0.45f),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}
