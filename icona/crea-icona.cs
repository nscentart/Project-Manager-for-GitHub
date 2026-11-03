// crea-icona.cs: scrive icona\ProjectManager.ico (16, 24, 32, 48, 64, 256, ognuna disegnata a quella misura) col disegno di
// src\Segno.cs. Con "--foto cartella" salva anche le foto su fondo chiaro e scuro. Lo compila crea-icona.ps1.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class CreaIcona {
    static readonly int[] MISURE = { 16, 24, 32, 48, 64, 256 };

    static Bitmap Disegno(int n) {
        var b = new Bitmap(n, n, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); Segno.Disegna(g, new Rectangle(0, 0, n, n)); }
        return b;
    }

    static byte[] Png(Bitmap b) { using (var m = new MemoryStream()) { b.Save(m, ImageFormat.Png); return m.ToArray(); } }

    static byte[] Dib(Bitmap b) {
        int n = b.Width, riga = ((n + 31) / 32) * 4;
        using (var m = new MemoryStream()) using (var w = new BinaryWriter(m)) {
            w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(n * n * 4 + riga * n); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = n - 1; y >= 0; y--) for (int x = 0; x < n; x++) w.Write(b.GetPixel(x, y).ToArgb());
            for (int y = 0; y < n; y++) for (int k = 0; k < riga; k++) w.Write((byte)0);   // maschera: conta l'alfa
            return m.ToArray();
        }
    }

    public static int Main(string[] a) {
        if (a.Length == 2 && a[0] == "--foto") { Foto(a[1]); return 0; }
        // 256 in PNG, le altre nel formato classico (BMP con maschera) che ogni Windows e ogni programma legge
        var png = new byte[MISURE.Length][];
        for (int i = 0; i < MISURE.Length; i++) using (var b = Disegno(MISURE[i])) png[i] = MISURE[i] >= 256 ? Png(b) : Dib(b);
        using (var f = File.Create(a[0])) using (var w = new BinaryWriter(f)) {
            w.Write((short)0); w.Write((short)1); w.Write((short)MISURE.Length);
            int pos = 6 + 16 * MISURE.Length;
            for (int i = 0; i < MISURE.Length; i++) {
                int n = MISURE[i];
                w.Write((byte)(n >= 256 ? 0 : n)); w.Write((byte)(n >= 256 ? 0 : n)); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32); w.Write(png[i].Length); w.Write(pos);
                pos += png[i].Length;
            }
            foreach (var p in png) w.Write(p);
        }
        return 0;
    }

    // la foto: ogni misura vera (pixel per pixel), poi ingrandita a occhio, su chiaro e su scuro
    static void Foto(string cartella) {
        Directory.CreateDirectory(cartella);
        foreach (var sfondo in new[] { new { N = "chiaro", C = Color.FromArgb(243, 243, 243) }, new { N = "scuro", C = Color.FromArgb(32, 32, 32) } }) {
            int[] foto = { 16, 32, 48, 256 };
            int larg = 20;
            foreach (int n in foto) larg += n + 20;
            using (var b = new Bitmap(larg, 256 + 40)) using (var g = Graphics.FromImage(b)) {
                g.Clear(sfondo.C); int x = 20;
                foreach (int n in foto) { using (var d = Disegno(n)) g.DrawImageUnscaled(d, x, 20); x += n + 20; }
                b.Save(Path.Combine(cartella, "icona-reale-" + sfondo.N + ".png"), ImageFormat.Png);
            }
            // le piccole ingrandite 8 volte senza sfumare, per vedere i pixel
            using (var b = new Bitmap(16 * 8 + 32 * 8 + 60, 32 * 8 + 40)) using (var g = Graphics.FromImage(b)) {
                g.Clear(sfondo.C); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                using (var d = Disegno(16)) g.DrawImage(d, 20, 20, 128, 128);
                using (var d = Disegno(32)) g.DrawImage(d, 168, 20, 256, 256);
                b.Save(Path.Combine(cartella, "icona-ingrandita-" + sfondo.N + ".png"), ImageFormat.Png);
            }
        }
    }
}
