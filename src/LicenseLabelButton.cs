using System;
using System.Drawing;
using System.Windows.Forms;

public class LicenseLabelButton : Button {
    static readonly Font Underlined = new Font(Theme.Small, FontStyle.Underline);
    bool above;

    public LicenseLabelButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
        BackColor = Theme.Background; UseMnemonic = false;
    }
    protected override void OnMouseEnter(EventArgs e) { above = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { above = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Theme.Background);
        Theme.T(e.Graphics, Text, above || Focused ? Underlined : Theme.Small, 0, 0, Width, Height, Theme.Label);
    }
}
