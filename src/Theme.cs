using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class Theme {
    public static readonly Color Ink = Color.FromArgb(11, 11, 13), Azure = Color.FromArgb(31, 111, 237);
    public static readonly Color Gray = Color.FromArgb(110, 116, 128), Border = Color.FromArgb(214, 218, 226), Backdrop = Color.FromArgb(246, 247, 249);
    public static readonly Color Tint = Color.FromArgb(220, 232, 252);
    public static readonly Color Background = Color.FromArgb(32, 33, 38), Capsule = Color.FromArgb(44, 47, 54), Line = Color.FromArgb(62, 66, 76);
    public static readonly Color Text = Color.FromArgb(242, 244, 248), Muted = Color.FromArgb(154, 163, 178), Label = Color.FromArgb(143, 182, 255);
    public static readonly Color Lime = Color.FromArgb(201, 240, 77), Amber = Color.FromArgb(255, 181, 71), Red = Color.FromArgb(255, 90, 82);
    public static readonly Color AmberDark = Color.FromArgb(168, 100, 0), AmberTint = Color.FromArgb(255, 241, 219);
    public static readonly Color GreenDark = Color.FromArgb(72, 118, 0), GreenTint = Color.FromArgb(236, 247, 205);

    public static Font Normal = new Font("Segoe UI", 9.5f), Small = new Font("Segoe UI", 8.5f);
    public static Font Strong = new Font("Segoe UI Semibold", 9.5f), SmallF = new Font("Segoe UI Semibold", 8.5f);
    public static Font Large = new Font("Segoe UI Semibold", 15f), Title = new Font("Segoe UI Semibold", 17f), Medium = new Font("Segoe UI Semibold", 11f);

    public const TextFormatFlags One = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
    public const TextFormatFlags MultiLineFlags = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    public static float K = 1f;
    public static int S(int v) { return (int)Math.Round(v * K); }
    public static float Real() { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f; }
    public static void Simulate(float scale) {
        float f = scale / Real();
        K = scale;
        Normal = new Font("Segoe UI", 9.5f * f); Strong = new Font("Segoe UI Semibold", 9.5f * f); Large = new Font("Segoe UI Semibold", 15f * f);
    }

    public static int MeasureWidth(Graphics g, string s, Font f) { return TextRenderer.MeasureText(g, s ?? "", f, new Size(9999, 999), One).Width; }
    public static int MeasureHeight(Graphics g, string s, Font f, int width) { return TextRenderer.MeasureText(g, s ?? "", f, new Size(Math.Max(1, width), 9999), MultiLineFlags).Height; }
    public static void T(Graphics g, string s, Font f, int x, int y, int width, int alt, Color c) { TextRenderer.DrawText(g, s ?? "", f, new Rectangle(x, y, Math.Max(1, width), alt), c, One); }
    public static void DrawTextRight(Graphics g, string s, Font f, int right, int y, int alt, Color c) { int w = MeasureWidth(g, s, f); T(g, s, f, right - w, y, w + 1, alt, c); }
    public static void DrawTextWrapped(Graphics g, string s, Font f, int x, int y, int width, int alt, Color c) { TextRenderer.DrawText(g, s ?? "", f, new Rectangle(x, y, Math.Max(1, width), alt), c, MultiLineFlags); }

    public static GraphicsPath Round(Rectangle r, int radius) {
        var p = new GraphicsPath(); int d = Math.Max(2, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
        return p;
    }
    public static void Full(Graphics g, Rectangle r, int radius, Color c) {
        var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Round(r, radius)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        g.SmoothingMode = v;
    }
    public static void Card(Graphics g, Rectangle r, int radius, Color background, Color line) {
        Full(g, r, radius, background);
        var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius)) using (var n = new Pen(line)) g.DrawPath(n, p);
        g.SmoothingMode = v;
    }
    public static int DrawPill(Graphics g, string s, Font f, int x, int y, int alt, Color background, Color text) {
        int w = MeasureWidth(g, s, f) + S(14);
        if (g != null) { Full(g, new Rectangle(x, y, w, alt), alt / 2, background); T(g, s, f, x + S(7), y, w - S(14) + 1, alt, text); }
        return w;
    }

    public static void Logo(Graphics g, Rectangle r) { LogoMark.Draw(g, r); }

    public static Icon AppIcon() {
        using (var b = new Bitmap(64, 64)) {
            using (var g = Graphics.FromImage(b)) Logo(g, new Rectangle(0, 0, 64, 64));
            return Icon.FromHandle(b.GetHicon());
        }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int val, int size);

    public static void DarkBar(Form f) {
        try {
            int si = 1, color = Background.R | Background.G << 8 | Background.B << 16, text = Text.R | Text.G << 8 | Text.B << 16;
            DwmSetWindowAttribute(f.Handle, 20, ref si, 4);
            DwmSetWindowAttribute(f.Handle, 35, ref color, 4);
            DwmSetWindowAttribute(f.Handle, 36, ref text, 4);
        } catch { }
    }
}
