using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class CreateIcon {
    static readonly int[] Measures = { 16, 24, 32, 48, 64, 256 };

    static Bitmap Drawing(int n) {
        var b = new Bitmap(n, n, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); LogoMark.Draw(g, new Rectangle(0, 0, n, n)); }
        return b;
    }

    static byte[] Png(Bitmap b) { using (var m = new MemoryStream()) { b.Save(m, ImageFormat.Png); return m.ToArray(); } }

    static byte[] Dib(Bitmap b) {
        int n = b.Width, line = ((n + 31) / 32) * 4;
        using (var m = new MemoryStream()) using (var w = new BinaryWriter(m)) {
            w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(n * n * 4 + line * n); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = n - 1; y >= 0; y--) for (int x = 0; x < n; x++) w.Write(b.GetPixel(x, y).ToArgb());
            for (int y = 0; y < n; y++) for (int k = 0; k < line; k++) w.Write((byte)0);
            return m.ToArray();
        }
    }

    public static int Main(string[] a) {
        if (a.Length == 2 && a[0] == "--previews") { Screenshots(a[1]); return 0; }
        var png = new byte[Measures.Length][];
        for (int i = 0; i < Measures.Length; i++) using (var b = Drawing(Measures[i])) png[i] = Measures[i] >= 256 ? Png(b) : Dib(b);
        using (var f = File.Create(a[0])) using (var w = new BinaryWriter(f)) {
            w.Write((short)0); w.Write((short)1); w.Write((short)Measures.Length);
            int pos = 6 + 16 * Measures.Length;
            for (int i = 0; i < Measures.Length; i++) {
                int n = Measures[i];
                w.Write((byte)(n >= 256 ? 0 : n)); w.Write((byte)(n >= 256 ? 0 : n)); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32); w.Write(png[i].Length); w.Write(pos);
                pos += png[i].Length;
            }
            foreach (var p in png) w.Write(p);
        }
        return 0;
    }

    static void Screenshots(string folder) {
        Directory.CreateDirectory(folder);
        foreach (var background in new[] { new { N = "chiaro", C = Color.FromArgb(243, 243, 243) }, new { N = "scuro", C = Color.FromArgb(32, 32, 32) } }) {
            int[] screenshot = { 16, 32, 48, 256 };
            int width = 20;
            foreach (int n in screenshot) width += n + 20;
            using (var b = new Bitmap(width, 256 + 40)) using (var g = Graphics.FromImage(b)) {
                g.Clear(background.C); int x = 20;
                foreach (int n in screenshot) { using (var d = Drawing(n)) g.DrawImageUnscaled(d, x, 20); x += n + 20; }
                b.Save(Path.Combine(folder, "icona-reale-" + background.N + ".png"), ImageFormat.Png);
            }
            using (var b = new Bitmap(16 * 8 + 32 * 8 + 60, 32 * 8 + 40)) using (var g = Graphics.FromImage(b)) {
                g.Clear(background.C); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                using (var d = Drawing(16)) g.DrawImage(d, 20, 20, 128, 128);
                using (var d = Drawing(32)) g.DrawImage(d, 168, 20, 256, 256);
                b.Save(Path.Combine(folder, "icona-ingrandita-" + background.N + ".png"), ImageFormat.Png);
            }
        }
    }
}
