using System.Drawing;
using System.Drawing.Drawing2D;

public static class LogoMark {
    public static readonly Color Background = Color.FromArgb(30, 36, 48), Lime = Color.FromArgb(201, 240, 77), Azure = Color.FromArgb(56, 168, 255);

    public static void Draw(Graphics g, Rectangle r) {
        var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        float n = r.Width, x = r.X, y = r.Y;
        float stroke = System.Math.Max(n * 0.1f, n < 40 ? 2f : 0f);
        using (var p = Round(r, (int)(n * 0.22f))) using (var b = new SolidBrush(Background)) g.FillPath(b, p);
        var c = new RectangleF(x + n * 0.17f, y + n * 0.14f, n * 0.54f, n * 0.54f);
        float inside = stroke * 0.5f;
        using (var b = new SolidBrush(Azure)) g.FillPie(b, c.X + inside, c.Y + inside, c.Width - stroke, c.Height - stroke, 0, 180);
        using (var q = new Pen(Lime, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round }) {
            g.DrawEllipse(q, c);
            g.DrawLine(q, x + n * 0.64f, y + n * 0.62f, x + n * 0.83f, y + n * 0.83f);
        }
        g.SmoothingMode = v;
    }

    static GraphicsPath Round(Rectangle r, int radius) {
        var p = new GraphicsPath(); int d = System.Math.Max(2, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
        return p;
    }
}
