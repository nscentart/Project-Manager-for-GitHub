using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class Aspetto {
    public static readonly Color INCHIOSTRO = Color.FromArgb(11, 11, 13), AZZURRO = Color.FromArgb(31, 111, 237);
    public static readonly Color GRIGIO = Color.FromArgb(110, 116, 128), BORDO = Color.FromArgb(214, 218, 226), FONDINO = Color.FromArgb(246, 247, 249);
    public static readonly Color TINTA = Color.FromArgb(220, 232, 252);
    // i colori di WeightMonitor, per le parti scure
    public static readonly Color FONDO = Color.FromArgb(32, 33, 38), CAPSULA = Color.FromArgb(44, 47, 54), RIGA = Color.FromArgb(62, 66, 76);
    public static readonly Color TESTO = Color.FromArgb(242, 244, 248), MUTO = Color.FromArgb(154, 163, 178), ETICHETTA = Color.FromArgb(143, 182, 255);
    public static readonly Color LIME = Color.FromArgb(201, 240, 77), AMBRA = Color.FromArgb(255, 181, 71), ROSSO = Color.FromArgb(255, 90, 82);
    // gli stessi, leggibili sul chiaro
    public static readonly Color AMBRA_S = Color.FromArgb(168, 100, 0), AMBRA_T = Color.FromArgb(255, 241, 219);
    public static readonly Color VERDE_S = Color.FromArgb(72, 118, 0), VERDE_T = Color.FromArgb(236, 247, 205);

    public static Font NORMALE = new Font("Segoe UI", 9.5f), PICCOLO = new Font("Segoe UI", 8.5f);
    public static Font FORTE = new Font("Segoe UI Semibold", 9.5f), PICCOLO_F = new Font("Segoe UI Semibold", 8.5f);
    public static Font GRANDE = new Font("Segoe UI Semibold", 15f), TITOLO = new Font("Segoe UI Semibold", 17f), MEDIO = new Font("Segoe UI Semibold", 11f);

    public const TextFormatFlags UNA = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
    public const TextFormatFlags PIU = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    public static float K = 1f;
    public static int S(int v) { return (int)Math.Round(v * K); }
    public static float Vera() { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f; }
    // per le prove e le foto: una finestra a un'altra scala dello schermo, senza toccare quella di Windows. Simula(Vera()) rimette tutto com'era
    public static void Simula(float scala) {
        float f = scala / Vera();
        K = scala;
        NORMALE = new Font("Segoe UI", 9.5f * f); FORTE = new Font("Segoe UI Semibold", 9.5f * f); GRANDE = new Font("Segoe UI Semibold", 15f * f);
    }

    public static int Larg(Graphics g, string s, Font f) { return TextRenderer.MeasureText(g, s ?? "", f, new Size(9999, 999), UNA).Width; }
    public static int Alt(Graphics g, string s, Font f, int larg) { return TextRenderer.MeasureText(g, s ?? "", f, new Size(Math.Max(1, larg), 9999), PIU).Height; }
    public static void T(Graphics g, string s, Font f, int x, int y, int larg, int alt, Color c) { TextRenderer.DrawText(g, s ?? "", f, new Rectangle(x, y, Math.Max(1, larg), alt), c, UNA); }
    public static void TDestra(Graphics g, string s, Font f, int destra, int y, int alt, Color c) { int w = Larg(g, s, f); T(g, s, f, destra - w, y, w + 1, alt, c); }
    public static void TPiu(Graphics g, string s, Font f, int x, int y, int larg, int alt, Color c) { TextRenderer.DrawText(g, s ?? "", f, new Rectangle(x, y, Math.Max(1, larg), alt), c, PIU); }

    public static GraphicsPath Tondo(Rectangle r, int raggio) {
        var p = new GraphicsPath(); int d = Math.Max(2, raggio * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
        return p;
    }
    public static void Pieno(Graphics g, Rectangle r, int raggio, Color c) {
        var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Tondo(r, raggio)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        g.SmoothingMode = v;
    }
    public static void Carta(Graphics g, Rectangle r, int raggio, Color fondo, Color linea) {
        Pieno(g, r, raggio, fondo);
        var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Tondo(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), raggio)) using (var n = new Pen(linea)) g.DrawPath(n, p);
        g.SmoothingMode = v;
    }
    // un'etichetta tonda; restituisce la larghezza
    public static int Pillola(Graphics g, string s, Font f, int x, int y, int alt, Color fondo, Color testo) {
        int w = Larg(g, s, f) + S(14);
        if (g != null) { Pieno(g, new Rectangle(x, y, w, alt), alt / 2, fondo); T(g, s, f, x + S(7), y, w - S(14) + 1, alt, testo); }
        return w;
    }

    // il segno del programma: lo disegna Segno.cs, lo stesso dell'icona dell'exe
    public static void Logo(Graphics g, Rectangle r) { Segno.Disegna(g, r); }

    public static Icon Icona() {
        using (var b = new Bitmap(64, 64)) {
            using (var g = Graphics.FromImage(b)) Logo(g, new Rectangle(0, 0, 64, 64));
            return Icon.FromHandle(b.GetHicon());
        }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attributo, ref int valore, int taglia);

    // la barra del titolo scura come la colonna (Windows 11; su Windows 10 solo il tema scuro, altrove niente)
    public static void BarraScura(Form f) {
        try {
            int si = 1, colore = FONDO.R | FONDO.G << 8 | FONDO.B << 16, testo = TESTO.R | TESTO.G << 8 | TESTO.B << 16;
            DwmSetWindowAttribute(f.Handle, 20, ref si, 4);
            DwmSetWindowAttribute(f.Handle, 35, ref colore, 4);
            DwmSetWindowAttribute(f.Handle, 36, ref testo, 4);
        } catch { }
    }
}

// un tasto tondo: Fondo, Scritta e Linea decidono se è primario, chiaro o scuro
public class Bottone : Button {
    public Color Fondo = Color.White, Scritta = Aspetto.INCHIOSTRO, Linea = Aspetto.BORDO;
    bool sopra, giu;

    public Bottone() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
    }
    public override Size GetPreferredSize(Size proposta) {
        using (var g = CreateGraphics()) return new Size(Aspetto.Larg(g, Text, Aspetto.FORTE) + Aspetto.S(26), Aspetto.S(32));
    }
    protected override void OnMouseEnter(EventArgs e) { sopra = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { sopra = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { giu = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { giu = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    static Color Verso(Color c, Color d, float t) { return Color.FromArgb((int)(c.R + (d.R - c.R) * t), (int)(c.G + (d.G - c.G) * t), (int)(c.B + (d.B - c.B) * t)); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(Parent == null ? BackColor : Parent.BackColor);
        Color f = !Enabled ? Verso(Fondo, Parent == null ? BackColor : Parent.BackColor, 0.55f) : giu ? Verso(Fondo, Color.Gray, 0.25f) : sopra ? Verso(Fondo, Scritta, 0.08f) : Fondo;
        var r = new Rectangle(0, 0, Width, Height);
        Aspetto.Carta(g, r, Aspetto.S(7), f, Linea == Color.Empty ? f : Linea);
        TextRenderer.DrawText(g, Text, Aspetto.FORTE, r, Enabled ? Scritta : Verso(Scritta, f, 0.45f),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

// una riga a sinistra: una repo, oppure «Tutte le repo» (R nullo)
public class RigaRepo {
    public RepoInfo R;
    public string Nome, Sotto;
    public bool Gruppo, Aperto;   // la riga «Sparite (N)»: non è una repo e non resta mai scelta
    public int Quarantena, Lavorare;
    public override string ToString() { return Nome; }
}

// la colonna delle repo. Il nome sta sopra, etichetta e avvisi sotto: «non c'è più su GitHub» non si taglia mai.
public class ListaRepo : ListBox {
    public event Action GruppoCliccato;
    const int WM_LBUTTONDOWN = 0x201, WM_LBUTTONDBLCLK = 0x203;

    // il clic sul gruppo apre o chiude e basta: non deve spostare la scelta
    protected override void WndProc(ref Message m) {
        if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK) {
            int xy = (int)m.LParam.ToInt64();
            var p = new Point((short)(xy & 0xFFFF), (short)((xy >> 16) & 0xFFFF));
            int i = IndexFromPoint(p);
            if (i >= 0 && i < Items.Count && GetItemRectangle(i).Contains(p)) {
                var r = Items[i] as RigaRepo;
                if (r != null && r.Gruppo) { Focus(); if (GruppoCliccato != null) GruppoCliccato(); return; }
            }
        }
        base.WndProc(ref m);
    }

    // per le prove: lo stesso clic, passando per la stessa strada, senza toccare il mouse
    public void ClicSu(int indice) {
        var r = GetItemRectangle(indice);
        var m = Message.Create(Handle, WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)(((r.Y + r.Height / 2) << 16) | (r.X + r.Width / 2)));
        WndProc(ref m);
    }

    public ListaRepo() {
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = Aspetto.S(56);
        BorderStyle = BorderStyle.None; IntegralHeight = false; BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] internal static extern int SetWindowTheme(IntPtr h, string tema, string elenco);

    // la barra di scorrimento scura come la colonna: col gruppo delle sparite aperto serve spesso (Windows 10 recente e 11; altrove resta quella di Windows)
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var g = e.Graphics; var b = e.Bounds; var r = (RigaRepo)Items[e.Index];
        using (var f = new SolidBrush(Aspetto.FONDO)) g.FillRectangle(f, b);
        Func<int, int> S = Aspetto.S;
        var c = new Rectangle(b.X + S(10), b.Y + S(3), b.Width - S(20), b.Height - S(6));
        if (r.Gruppo) {
            int cy = c.Y + c.Height / 2, xg = c.X + S(12);
            var punti = r.Aperto ? new[] { new Point(xg, cy - S(3)), new Point(xg + S(10), cy - S(3)), new Point(xg + S(5), cy + S(3)) }
                                 : new[] { new Point(xg + S(2), cy - S(5)), new Point(xg + S(8), cy), new Point(xg + S(2), cy + S(5)) };
            var v = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fr = new SolidBrush(Aspetto.MUTO)) g.FillPolygon(fr, punti);
            g.SmoothingMode = v;
            Aspetto.T(g, r.Nome, Aspetto.FORTE, xg + S(18), c.Y, c.Right - xg - S(28), c.Height, Aspetto.MUTO);
            return;
        }
        if ((e.State & DrawItemState.Selected) != 0) {
            Aspetto.Pieno(g, c, S(8), Aspetto.CAPSULA);
            Aspetto.Pieno(g, new Rectangle(c.X + S(1), c.Y + S(10), S(3), c.Height - S(20)), S(1), Aspetto.AZZURRO);
        }
        bool sparita = r.R != null && r.R.Sparita, seguita = r.R == null || r.R.Seguita;
        int x = c.X + S(12), destra = c.Right - S(10), ySu = c.Y + S(6), aSu = S(20), yGiu = c.Y + S(27), aGiu = S(17);
        // i numeri a destra del nome: ambra la quarantena, azzurro il da lavorare
        int d = destra;
        if (r.Lavorare > 0) { string s = r.Lavorare.ToString(); int w = Aspetto.Larg(g, s, Aspetto.PICCOLO_F) + S(14); d -= w; Aspetto.Pillola(g, s, Aspetto.PICCOLO_F, d, ySu + S(1), S(18), Aspetto.AZZURRO, Color.White); d -= S(4); }
        if (r.Quarantena > 0) { string s = r.Quarantena.ToString(); int w = Aspetto.Larg(g, s, Aspetto.PICCOLO_F) + S(14); d -= w; Aspetto.Pillola(g, s, Aspetto.PICCOLO_F, d, ySu + S(1), S(18), Aspetto.AMBRA, Aspetto.INCHIOSTRO); d -= S(4); }
        Aspetto.T(g, r.Nome, Aspetto.FORTE, x, ySu, d - x - S(4), aSu, sparita ? Aspetto.MUTO : seguita ? Aspetto.TESTO : Color.FromArgb(200, 205, 214));
        if (r.R == null) { Aspetto.T(g, r.Sotto, Aspetto.PICCOLO, x, yGiu, destra - x, aGiu, Aspetto.MUTO); return; }
        int wp = r.R.Privata ? Aspetto.Pillola(g, Lingua.T("comune.privata"), Aspetto.PICCOLO, x, yGiu, aGiu, Aspetto.RIGA, Aspetto.TESTO)
                             : Aspetto.Pillola(g, Lingua.T("comune.pubblica"), Aspetto.PICCOLO, x, yGiu, aGiu, Aspetto.LIME, Aspetto.INCHIOSTRO);
        Aspetto.T(g, sparita ? Lingua.T("comune.non_c_piu_github") : seguita ? "" : Lingua.T("comune.non_seguita"), Aspetto.PICCOLO, x + wp + S(7), yGiu, destra - x - wp - S(7), aGiu, sparita ? Aspetto.AMBRA : Aspetto.MUTO);
    }
}

// le schede disegnate: «Panoramica» da sola, poi le quattro delle segnalazioni raggruppate
public class BarraSchede : Control {
    public class Voce { public string Nome; public int Numero; public bool Allarme; public Control Pagina; public Rectangle Zona; }
    public List<Voce> Voci = new List<Voce>();
    public int Scelta;
    public event EventHandler Cambiata;

    public BarraSchede() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }
    public void Scegli(int i) {
        Scelta = i;
        for (int v = 0; v < Voci.Count; v++) Voci[v].Pagina.Visible = v == i;
        Invalidate();
        if (Cambiata != null) Cambiata(this, EventArgs.Empty);
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics; Func<int, int> S = Aspetto.S;
        g.Clear(BackColor);
        using (var n = new Pen(Aspetto.BORDO)) g.DrawLine(n, 0, Height - 1, Width, Height - 1);
        int x = S(24);
        for (int i = 0; i < Voci.Count; i++) {
            var v = Voci[i];
            if (i == 2) {
                using (var n = new Pen(Aspetto.BORDO)) g.DrawLine(n, x + S(2), S(12), x + S(2), Height - S(12));
                x += S(16);
                string gr = Lingua.T("comune.segnalazioni");
                int wg = Aspetto.Larg(g, gr, Aspetto.PICCOLO);
                Aspetto.T(g, gr, Aspetto.PICCOLO, x, 0, wg + 1, Height - 1, Aspetto.GRIGIO);
                x += wg + S(16);
            }
            bool sc = i == Scelta;
            int w = Aspetto.Larg(g, v.Nome, Aspetto.FORTE);
            Aspetto.T(g, v.Nome, Aspetto.FORTE, x, 0, w + 1, Height - 1, sc ? Aspetto.INCHIOSTRO : Aspetto.GRIGIO);
            int tot = w;
            if (v.Numero > 0) {
                tot += S(6) + Aspetto.Pillola(g, v.Numero.ToString(), Aspetto.PICCOLO_F, x + w + S(6), (Height - S(18)) / 2, S(18),
                    v.Allarme ? Aspetto.AMBRA_T : Aspetto.FONDINO, v.Allarme ? Aspetto.AMBRA_S : Aspetto.GRIGIO);
            }
            if (sc) Aspetto.Pieno(g, new Rectangle(x, Height - S(3), tot, S(3)), S(1), Aspetto.AZZURRO);
            v.Zona = new Rectangle(x - S(8), 0, tot + S(16), Height);
            x += tot + S(20);
        }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        Cursor = Voci.Any(v => v.Zona.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        for (int i = 0; i < Voci.Count; i++) if (Voci[i].Zona.Contains(e.Location)) Scegli(i);
        base.OnMouseDown(e);
    }
}

// la testata della pagina: nome, visibilità, descrizione, quando è stata letta, e l'interruttore «Seguo»
public class Testata : Control {
    public string Nome = "", Descrizione = "", Letto = "";
    public bool LettoVecchio;
    public string Pill; public bool Pubblica;
    public bool HaInterruttore, Seguo;
    public event EventHandler Interruttore;
    Rectangle zona;

    public Testata() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics; Func<int, int> S = Aspetto.S;
        g.Clear(BackColor);
        int x = S(24), destra = Width - S(24), yGiu = S(46), aGiu = S(20), dx = destra;
        // a destra l'interruttore sopra e la data sotto
        zona = Rectangle.Empty;
        if (HaInterruttore) {
            string s = Lingua.T("aspetto.seguo_segnalazioni");
            int wt = Aspetto.Larg(g, s, Aspetto.NORMALE), wi = S(34), ai = S(18), yi = S(18);
            var tr = new Rectangle(destra - wi, yi, wi, ai);
            Aspetto.Pieno(g, tr, ai / 2, Seguo ? Aspetto.AZZURRO : Aspetto.BORDO);
            Aspetto.Pieno(g, new Rectangle(Seguo ? tr.Right - ai + S(2) : tr.X + S(2), yi + S(2), ai - S(4), ai - S(4)), (ai - S(4)) / 2, Color.White);
            Aspetto.T(g, s, Aspetto.NORMALE, tr.X - S(8) - wt, yi - S(1), wt + 1, ai + S(2), Aspetto.INCHIOSTRO);
            zona = new Rectangle(tr.X - S(8) - wt, yi - S(4), wt + S(8) + wi, ai + S(8));
            dx = zona.X;
        }
        int wl = Aspetto.Larg(g, Letto, Aspetto.PICCOLO);
        Aspetto.T(g, Letto, Aspetto.PICCOLO, destra - wl, yGiu, wl + 1, aGiu, LettoVecchio ? Aspetto.AMBRA_S : Aspetto.GRIGIO);
        // a sinistra il nome con l'etichetta, e la descrizione sotto
        int wp = 0, ap = S(20);
        if (Pill != null) wp = Aspetto.Larg(g, Pill, Aspetto.PICCOLO_F) + S(14) + S(10);
        int wn = Math.Min(Aspetto.Larg(g, Nome, Aspetto.TITOLO), dx - x - wp - S(16));
        Aspetto.T(g, Nome, Aspetto.TITOLO, x, S(10), wn, S(36), Aspetto.INCHIOSTRO);
        if (Pill != null) Aspetto.Pillola(g, Pill, Aspetto.PICCOLO_F, x + wn + S(10), S(18), ap, Pubblica ? Aspetto.VERDE_T : Aspetto.FONDINO, Pubblica ? Aspetto.VERDE_S : Aspetto.GRIGIO);
        Aspetto.T(g, Descrizione, Aspetto.NORMALE, x, yGiu, destra - wl - S(16) - x, aGiu, Aspetto.GRIGIO);
    }
    protected override void OnMouseMove(MouseEventArgs e) { Cursor = zona.Contains(e.Location) ? Cursors.Hand : Cursors.Default; base.OnMouseMove(e); }
    protected override void OnMouseDown(MouseEventArgs e) {
        if (zona.Contains(e.Location) && Interruttore != null) Interruttore(this, EventArgs.Empty);
        base.OnMouseDown(e);
    }
}

// le liste: stesse colonne di prima, righe più alte e intestazione leggera
public class Tabella : ListView {
    public Tabella() {
        DoubleBuffered = true; OwnerDraw = true;
        View = View.Details; FullRowSelect = true; MultiSelect = true; HideSelection = false;
        BorderStyle = BorderStyle.None; ShowItemToolTips = false; HeaderStyle = ColumnHeaderStyle.Nonclickable;
        SmallImageList = new ImageList { ImageSize = new Size(1, Aspetto.S(28)) };
    }
    // le colonne si stringono e si allargano con la finestra: niente barra orizzontale
    int[] pesi; bool adatto;
    void Adatta() {
        if (adatto || Columns.Count == 0 || ClientSize.Width <= 0) return;
        adatto = true;
        if (pesi == null) pesi = Columns.Cast<ColumnHeader>().Select(c => c.Width).ToArray();
        int tot = pesi.Sum(), resto = ClientSize.Width;
        for (int i = 0; i < pesi.Length - 1; i++) { int w = ClientSize.Width * pesi[i] / tot; Columns[i].Width = w; resto -= w; }
        Columns[pesi.Length - 1].Width = resto;
        adatto = false;
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Adatta(); }
    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) {
        using (var b = new SolidBrush(Aspetto.FONDINO)) e.Graphics.FillRectangle(b, e.Bounds);
        using (var n = new Pen(Aspetto.BORDO)) e.Graphics.DrawLine(n, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        Aspetto.T(e.Graphics, e.Header.Text, Aspetto.PICCOLO_F, e.Bounds.X + Aspetto.S(8), e.Bounds.Y, e.Bounds.Width - Aspetto.S(10), e.Bounds.Height, Aspetto.GRIGIO);
    }
    protected override void OnDrawItem(DrawListViewItemEventArgs e) { }
    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) {
        using (var b = new SolidBrush(e.Item.Selected ? Aspetto.TINTA : Color.White)) e.Graphics.FillRectangle(b, e.Bounds);
        Aspetto.T(e.Graphics, e.SubItem.Text, Aspetto.NORMALE, e.Bounds.X + Aspetto.S(8), e.Bounds.Y, e.Bounds.Width - Aspetto.S(10), e.Bounds.Height, Aspetto.INCHIOSTRO);
    }
}
