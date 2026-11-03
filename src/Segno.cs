// Segno.cs (Project Manager 0.4, 02/11/2026): il disegno del programma, uno solo per la finestra e per l'icona dell'exe
// (icona\crea-icona.ps1 compila questo file per fare icona\ProjectManager.ico). Una lente su fondo scuro, sorella del
// kettlebell di WeightMonitor: stessi colori, contorno lime, dentro azzurro.
using System.Drawing;
using System.Drawing.Drawing2D;

public static class Segno {
    public static readonly Color FONDO = Color.FromArgb(30, 36, 48), LIME = Color.FromArgb(201, 240, 77), AZZURRO = Color.FromArgb(56, 168, 255);

    public static void Disegna(Graphics g, Rectangle r) {
        var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        float n = r.Width, x = r.X, y = r.Y;
        // a 16 pixel il tratto non scende sotto i 2: così la lente si legge
        float tratto = System.Math.Max(n * 0.1f, n < 40 ? 2f : 0f);
        using (var p = Tondo(r, (int)(n * 0.22f))) using (var b = new SolidBrush(FONDO)) g.FillPath(b, p);
        var c = new RectangleF(x + n * 0.17f, y + n * 0.14f, n * 0.54f, n * 0.54f);
        float dentro = tratto * 0.5f;
        using (var b = new SolidBrush(AZZURRO)) g.FillPie(b, c.X + dentro, c.Y + dentro, c.Width - tratto, c.Height - tratto, 0, 180);
        using (var q = new Pen(LIME, tratto) { StartCap = LineCap.Round, EndCap = LineCap.Round }) {
            g.DrawEllipse(q, c);
            g.DrawLine(q, x + n * 0.64f, y + n * 0.62f, x + n * 0.83f, y + n * 0.83f);
        }
        g.SmoothingMode = v;
    }

    static GraphicsPath Tondo(Rectangle r, int raggio) {
        var p = new GraphicsPath(); int d = System.Math.Max(2, raggio * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
        return p;
    }
}
