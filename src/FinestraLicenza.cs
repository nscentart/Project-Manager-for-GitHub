using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

public static class LicenzaQui {
    public const string PROGRAMMA = "ProjectManager";
    public const string CONTATTO = "info@nscent.art";
    // l'indirizzo del negozio, in un punto solo. Niente link cliccabile finché il negozio non c'è
    public static string Negozio(string lingua) {
        switch (lingua) {
            case "en": return "www.nscent.art/en/software";
            case "fr": return "www.nscent.art/fr/software";
            case "es": return "www.nscent.art/es/software";
            case "de": return "www.nscent.art/de/software";
            default: return "www.nscent.art/software";
        }
    }
    public static string VENDITA { get { return Lingua.T("licenza.vendita", "negozio", Negozio(Lingua.Codice), "contatto", CONTATTO); } }
    public static string LIMITI { get { return Lingua.T("licenza.programma_funziona_uguale_senza_licenz"); } }

    public static string[] Chiavi = Licenza.CHIAVI_PUBBLICHE;   // solo prove e foto ne passano altre

    public static EsitoLicenza Ora(string cartella) { return Licenza.Carica(cartella, PROGRAMMA, Chiavi, Licenza.REVOCATI); }
    public static EsitoLicenza Controlla(string testo) { return Licenza.Controlla(testo, PROGRAMMA, Chiavi, Licenza.REVOCATI); }

    public static string Scritta(EsitoLicenza e) { return e.Valida ? Lingua.T("licenza.commerciale_di", "intestatario", e.Intestatario) : Lingua.T("licenza.personale"); }

    public static string Data(EsitoLicenza e) {
        DateTime d;
        return DateTime.TryParseExact(e.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : e.Data;
    }

    // la frase per ogni esito: chi è «questa licenza» o «la licenza salvata» lo dice chi chiama. Nulla se non c'è da dire niente
    public static string Problema(EsitoLicenza e, string quale) {
        switch (e.Stato) {
            case StatoLicenza.NonValida: return Lingua.T("licenza.non_valida_forse_stata_copiata", "quale", quale);
            case StatoLicenza.AltroProgramma: return Lingua.T("licenza.licenza_vale_solo_programma_resta", "quale", quale, "programma", e.Programma);
            case StatoLicenza.Revocata: return Lingua.T("licenza.numero_stata_ritirata_resta_licenza", "quale", quale, "numero", e.Numero, "contatto", CONTATTO);
            case StatoLicenza.Illeggibile: return Lingua.T("licenza.non_riesco_leggere_file_della");
            default: return null;
        }
    }

    public static string Dettaglio(EsitoLicenza e) {
        if (e.Valida) return Lingua.T("licenza.numero_del_vale_questo_programma", "numero", e.Numero, "data", Data(e));
        return Problema(e, Lingua.T("licenza.licenza_salvata")) ?? Lingua.T("licenza.va_bene_l_uso_personale");
    }
}

// la scritta nell'intestazione: si legge come un testo, ma è un tasto vero (mouse, Tab e Invio)
public class ScrittaLicenza : Button {
    static readonly Font SOTTOLINEATA = new Font(Aspetto.PICCOLO, FontStyle.Underline);
    bool sopra;

    public ScrittaLicenza() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
        BackColor = Aspetto.FONDO; UseMnemonic = false;
    }
    protected override void OnMouseEnter(EventArgs e) { sopra = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { sopra = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Aspetto.FONDO);
        Aspetto.T(e.Graphics, Text, sopra || Focused ? SOTTOLINEATA : Aspetto.PICCOLO, 0, 0, Width, Height, Aspetto.ETICHETTA);
    }
}

// la finestra: stato, casella per incollare, «Usa questa licenza» e «Togli la licenza»
public class FinestraLicenza : Dialogo {
    readonly string cartella;
    readonly Label lStato, lDettaglio, lEsito;
    public readonly TextBox Casella;
    public readonly Bottone BUsa, BTogli;
    public Func<bool> ChiediTogli;
    public bool Cambiata;

    public string TestoStato { get { return lStato.Text; } }
    public string TestoDettaglio { get { return lDettaglio.Text; } }
    public string TestoEsito { get { return lEsito.Text; } }

    public FinestraLicenza(string cartellaDati) {
        cartella = cartellaDati;
        Func<int, int> S = Aspetto.S;
        Text = Lingua.T("comune.licenza");
        try { Icon = Aspetto.Icona(); } catch { }
        ClientSize = new Size(S(560), S(500));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        int m = S(24), lu = S(560) - m * 2;
        Func<string, Font, Color, int, int, Label> scritta = (x, fo, co, y, al) => new Label { Text = x, Font = fo, ForeColor = co, BackColor = Aspetto.FONDO, Bounds = new Rectangle(m, y, lu, al), UseMnemonic = false };
        lStato = scritta("", Aspetto.GRANDE, Aspetto.TESTO, S(16), S(64));
        lStato.AutoEllipsis = true;
        lDettaglio = scritta("", Aspetto.NORMALE, Aspetto.MUTO, S(82), S(44));
        var lIncolla = scritta(Lingua.T("licenza.incolla_qui_licenza"), Aspetto.FORTE, Aspetto.TESTO, S(134), S(22));
        Casella = new TextBox { Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(24, 25, 29), ForeColor = Aspetto.TESTO,
                                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.5f), Bounds = new Rectangle(m, S(158), lu, S(84)) };
        lEsito = scritta("", Aspetto.NORMALE, Aspetto.AMBRA, S(248), S(44));
        BUsa = new Bottone { Text = Lingua.T("licenza.usa_questa_licenza"), Fondo = Aspetto.AZZURRO, Scritta = Color.White, Linea = Color.Empty, Bounds = new Rectangle(m, S(296), S(180), S(38)) };
        BTogli = new Bottone { Text = Lingua.T("licenza.togli_licenza"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, Bounds = new Rectangle(m + S(190), S(296), S(150), S(38)) };
        var chiudi = new Bottone { Text = Lingua.T("comune.chiudi"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, Bounds = new Rectangle(m + lu - S(110), S(296), S(110), S(38)), DialogResult = DialogResult.Cancel };
        var filo = new Label { BackColor = Aspetto.RIGA, Bounds = new Rectangle(m, S(350), lu, 1) };
        var lVendita = scritta(LicenzaQui.VENDITA, Aspetto.NORMALE, Aspetto.TESTO, S(362), S(44));
        var lLimiti = scritta(LicenzaQui.LIMITI, Aspetto.PICCOLO, Aspetto.MUTO, S(412), S(72));
        ChiediTogli = () => MessageBox.Show(this, Lingua.T("licenza.togliere_licenza_questo_computer_torna"), Lingua.T("comune.licenza"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        BUsa.Click += delegate { Usa(); };
        BTogli.Click += delegate { Togli(); };
        Controls.AddRange(new Control[] { lStato, lDettaglio, lIncolla, Casella, lEsito, BUsa, BTogli, chiudi, filo, lVendita, lLimiti });
        CancelButton = chiudi;
        Load += delegate { Aspetto.BarraScura(this); };
        Mostra();
    }

    void Mostra() {
        var e = LicenzaQui.Ora(cartella);
        lStato.Text = LicenzaQui.Scritta(e);
        lDettaglio.Text = LicenzaQui.Dettaglio(e);
        lDettaglio.ForeColor = LicenzaQui.Problema(e, "") == null ? Aspetto.MUTO : Aspetto.AMBRA;
        BTogli.Enabled = e.Stato != StatoLicenza.Assente;
    }

    void Di(string testo, bool bene) { lEsito.Text = testo; lEsito.ForeColor = bene ? Aspetto.LIME : Aspetto.AMBRA; }

    void Usa() {
        if (string.IsNullOrEmpty(Licenza.Pulisci(Casella.Text ?? "") ?? "x")) { Di(Lingua.T("licenza.incolla_prima_testo_della_licenza"), false); return; }
        var e = LicenzaQui.Controlla(Casella.Text);
        // una licenza non buona non si salva: quella che c'è resta com'è
        if (!e.Valida) { Di(LicenzaQui.Problema(e, Lingua.T("licenza.questa_licenza")), false); return; }
        if (!Licenza.Scrivi(cartella, Casella.Text)) { Di(Lingua.T("licenza.licenza_buona_ma_non_riesco"), false); return; }
        Casella.Text = ""; Cambiata = true;
        Mostra();
        Di(Lingua.T("licenza.licenza_salvata_2"), true);
    }

    void Togli() {
        if (!ChiediTogli()) return;
        if (!Licenza.Togli(cartella)) { Di(Lingua.T("licenza.non_riesco_togliere_file_della"), false); Mostra(); return; }
        Cambiata = true;
        Mostra();
        Di(Lingua.T("licenza.licenza_tolta"), true);
    }
}
