using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class Finestra : Dialogo {
    static readonly Color INCHIOSTRO = Aspetto.INCHIOSTRO, AZZURRO = Aspetto.AZZURRO, GRIGIO = Aspetto.GRIGIO, BORDO = Aspetto.BORDO, FONDINO = Aspetto.FONDINO;

    Archivio a;
    List<Voce> voci = new List<Voce>();
    List<ErroreRicorrente> errori = new List<ErroreRicorrente>();
    List<string> modelli = new List<string>();
    Dictionary<string, int> consegne = new Dictionary<string, int>();
    bool occupato;
    bool spariteAperte;   // il gruppo «Sparite (N)»: chiuso a ogni avvio, non si salva
    int ultimaSel;        // l'ultima riga vera scelta a sinistra: il gruppo non resta mai scelto
    ArchivioStorico storico = new ArchivioStorico();
    static bool avvisoPartito;   // la finestra si riapre quando cambia la lingua: la domanda a GitHub all'avvio si fa una volta sola
    static Finestra aperta;      // quella che c'è adesso, per chi risponde dopo
    public static bool SoloFoto;   // per le foto: niente rete, niente procedura
    public static bool RiapriPerLingua;   // la lingua è cambiata: chi ha aperto la finestra ne apre una nuova
    public static Action<Form> FotoImpostazioni;   // per le foto: la finestra delle Impostazioni si fotografa invece di aspettare una risposta
    public void ApriImpostazioni() { Impostazioni(); }
    // vale subito: i testi si leggono quando la finestra si costruisce, quindi se ne apre una nuova
    public bool CambiaLingua(string nuova) {
        if (nuova == Lingua.Codice) return false;
        Lingua.Scegli(nuova, Dati.Cartella);
        RiapriPerLingua = true;
        Close();
        return true;
    }
    // per le foto: una scheda con la prima riga scelta, oppure una repo della colonna
    public void FotoScheda(int i) {
        schede.Scegli(i);
        var lista = i == 2 ? lQuar : i == 3 ? lLav : i == 4 ? lErr : i == 5 ? lArch : null;
        if (lista != null && lista.Items.Count > 0) lista.Items[0].Selected = true;
    }
    public void FotoRepo(string nome) {
        var x = RepoOra().FirstOrDefault(r => r.Nome == nome);
        if (x != null && x.Sparita && !spariteAperte) { spariteAperte = true; Mostra(); }
        for (int i = 0; i < repo.Items.Count; i++) { var r = ((RigaRepo)repo.Items[i]).R; if (r != null && r.Nome == nome) repo.SelectedIndex = i; }
    }
    public void FotoSparite(bool aperte) { spariteAperte = aperte; Mostra(); }
    // per le prove: i pezzi veri della finestra, senza mouse e senza schermo
    public ListaRepo Colonna { get { return repo; } }
    public PaginaPanoramica Panoramica { get { return panoramica; } }
    public Label Stato { get { return stato; } }
    public bool SparitePerProva { get { return spariteAperte; } }
    public RepoInfo SceltaPerProva { get { return Scelta(); } }
    public void OccupatoPerProva(bool si) { occupato = si; }
    public void AggiornaPerProva() { Aggiorna(); }
    public bool OccupatoOra { get { return occupato; } }
    public static Func<Form, DialogResult> RispostaTogli;    // per le prove e le foto: la conferma risponde subito
    public static Func<Form, DialogResult> RispostaAvviso;
    HashSet<string> nascoste = new HashSet<string>();   // le repo che l'account o il filtro non mostrano
    ComboBox cConto, cFiltro;
    List<string[]> filtri = new List<string[]>();
    bool nonAggiornato;   // motivoVuoto dice che l'ultimo aggiornamento non è andato: non si riconosce dal testo, che dipende dalla lingua
    string motivoVuoto;   // perché le liste possono essere vuote dopo l'ultimo aggiornamento (nullo = tutto bene)

    Bottone bAggiorna;
    Label stato, letto;
    ListaRepo repo;
    Testata testata;
    BarraSchede schede;
    PaginaPanoramica panoramica;
    PaginaGestione gestione;
    Dictionary<ListView, Label> vuoti = new Dictionary<ListView, Label>();
    BarraSchede.Voce tQuar, tLav, tErr, tArch;
    ListView lQuar, lLav, lErr, lArch;
    TextBox dQuar, dLav, dErr, dArch;

    int S(int v) { return Aspetto.S(v); }

    public Finestra() {
        using (var g = CreateGraphics()) Aspetto.K = g.DpiX / 96f;
        Text = "Project Manager " + Programma.VERSIONE;
        Font = Aspetto.NORMALE;
        BackColor = Color.White; ForeColor = INCHIOSTRO;
        ClientSize = new Size(S(1320), S(820));
        MinimumSize = new Size(S(900), S(560));
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        try { Icon = Aspetto.Icona(); } catch { }

        // ---------- la colonna a sinistra ----------
        var colonna = new Panel { Dock = DockStyle.Left, Width = S(272), BackColor = Aspetto.FONDO };
        var marchio = new Panel { Dock = DockStyle.Top, Height = S(76), BackColor = Aspetto.FONDO };
        marchio.Paint += (s, e) => {
            e.Graphics.Clear(Aspetto.FONDO);
            Aspetto.Logo(e.Graphics, new Rectangle(S(18), S(18), S(40), S(40)));
            Aspetto.T(e.Graphics, "Project Manager", Aspetto.MEDIO, S(68), S(11), marchio.Width - S(76), S(24), Aspetto.TESTO);
            Aspetto.T(e.Graphics, Lingua.T("finestra.for_github_versione", "versione", Programma.VERSIONE), Aspetto.PICCOLO, S(68), S(35), marchio.Width - S(76), S(16), Aspetto.MUTO);
        };
        lic = new ScrittaLicenza { Bounds = new Rectangle(S(68), S(51), S(272) - S(76), S(18)) };
        lic.Click += delegate { ApriLicenza(); };
        marchio.Controls.Add(lic);
        MostraLicenza();
        repo = new ListaRepo { Dock = DockStyle.Fill };
        repo.SelectedIndexChanged += delegate { if (!caricando) { if (SuGruppo()) return; ultimaSel = repo.SelectedIndex; Riempi(); } };
        repo.GruppoCliccato += ApriChiudiSparite;
        var scelte = new Panel { Dock = DockStyle.Top, Height = S(112), BackColor = Aspetto.FONDO };
        Func<string, int, Label> sopra = (x, y) => new Label { Text = x, Font = Aspetto.PICCOLO, ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Bounds = new Rectangle(S(20), y, S(232), S(18)), UseMnemonic = false };
        cConto = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(S(20), S(18), S(232), S(28)) };
        cConto.SelectedIndexChanged += delegate { if (!caricando) CambiaConto(); };
        cFiltro = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(S(20), S(72), S(232), S(28)) };
        cFiltro.SelectedIndexChanged += delegate { if (!caricando) CambiaFiltro(); };
        scelte.Controls.AddRange(new Control[] { sopra(Lingua.T("comune.account"), 0), cConto, sopra(Lingua.T("finestra.mostra"), S(54)), cFiltro });
        var piede = new Panel { Dock = DockStyle.Bottom, Height = S(162), BackColor = Aspetto.FONDO };
        bAggiorna = Tasto(Lingua.T("finestra.aggiorna_github"), true);
        bAggiorna.SetBounds(S(20), S(12), S(232), S(36));
        bAggiorna.Click += delegate { Aggiorna(); };
        var bConti = TastoScuro(Lingua.T("comune.account"));
        bConti.SetBounds(S(20), S(54), S(232), S(32));
        bConti.Click += delegate { Conti(); };
        var bImp = TastoScuro(Lingua.T("finestra.impostazioni"));
        bImp.SetBounds(S(20), S(92), S(232), S(32));
        bImp.Click += delegate { Impostazioni(); };
        letto = new Label { Bounds = new Rectangle(S(20), S(132), S(232), S(20)), ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Font = Aspetto.PICCOLO, UseMnemonic = false, TextAlign = ContentAlignment.MiddleCenter };
        piede.Controls.AddRange(new Control[] { bAggiorna, bConti, bImp, letto });
        colonna.Controls.Add(repo); colonna.Controls.Add(piede); colonna.Controls.Add(scelte); colonna.Controls.Add(marchio);

        // ---------- la pagina a destra ----------
        var destra = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        stato = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, ForeColor = GRIGIO, Dock = DockStyle.Bottom, Height = S(26),
                            Padding = new Padding(S(16), 0, 0, 0), BackColor = FONDINO, UseMnemonic = false, AutoEllipsis = true };
        testata = new Testata { Dock = DockStyle.Top, Height = S(74) };
        testata.Interruttore += delegate { CambiaSeguo(); };
        schede = new BarraSchede { Dock = DockStyle.Top, Height = S(42) };
        var pagine = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        panoramica = new PaginaPanoramica { Dock = DockStyle.Fill };
        panoramica.RepoScelta += r => { for (int i = 0; i < repo.Items.Count; i++) if (((RigaRepo)repo.Items[i]).R == r) repo.SelectedIndex = i; };
        panoramica.GruppoCliccato += ApriChiudiSparite;
        panoramica.TogliTutte += delegate { Togli(RepoVisibili().Where(x => x.Sparita).Select(x => x.Nome).ToList()); };
        panoramica.TogliQuesta += delegate { var s = Scelta(); if (s != null) Togli(new List<string> { s.Nome }); };
        schede.Voci.Add(new BarraSchede.Voce { Nome = Lingua.T("finestra.panoramica"), Pagina = panoramica });
        gestione = new PaginaGestione(PortaScrittura.Vera(new PortaVera(), StradaVera)) { Occupata = () => occupato };
        gestione.Cambiata += delegate { Aggiorna(); };
        gestione.Rinominata += (vecchio, nuovo) => { lock (this) Scrittura.RinominaInArchivio(a, vecchio, nuovo); lock (storico) Storico.Rinomina(storico, vecchio, nuovo); Salva(); SalvaStorico(); Ricalcola(); Mostra(); Aggiorna(); };
        gestione.Cancellata += nome => { lock (this) Scrittura.SegnaCancellata(a, nome); Salva(); Ricalcola(); Mostra(); Aggiorna(); };
        schede.Voci.Add(new BarraSchede.Voce { Nome = Lingua.T("finestra.gestisci"), Pagina = gestione });
        tQuar = Scheda(Lingua.T("finestra.quarantena"), out lQuar, out dQuar,
            new[] { Lingua.T("comune.repo"), "#", Lingua.T("comune.titolo"), Lingua.T("finestra.autore"), Lingua.T("finestra.data"), Lingua.T("finestra.motivi") }, new[] { 220, 50, 300, 110, 110, 380 },
            TastoAzione(Lingua.T("finestra.approva_passa_fra_quelli_lavorare"), true, delegate { Decidi(lQuar, "approvato", true); }),
            TastoAzione(Lingua.T("finestra.rifiuta"), false, delegate { Decidi(lQuar, "rifiutato", false); }),
            TastoAzione(Lingua.T("finestra.apri_github"), false, delegate { ApriSuGitHub(lQuar); }));
        tQuar.Allarme = true;
        tLav = Scheda(Lingua.T("comune.lavorare"), out lLav, out dLav,
            new[] { Lingua.T("finestra.tipo"), Lingua.T("comune.repo"), "#", Lingua.T("comune.titolo"), Lingua.T("finestra.autore"), Lingua.T("finestra.data"), Lingua.T("finestra.riassunto") }, new[] { 90, 220, 50, 300, 110, 110, 380 },
            TastoAzione(Lingua.T("finestra.crea_pacchetto_claude_code"), true, delegate { CreaPacchetto(); }),
            TastoAzione(Lingua.T("finestra.riassumi_ollama"), false, delegate { Riassumi(); }),
            TastoAzione(Lingua.T("finestra.segna_fatto"), false, delegate { Decidi(lLav, "fatto", false); }),
            TastoAzione(Lingua.T("finestra.rimetti_quarantena"), false, delegate { Decidi(lLav, "quarantena", false); }),
            TastoAzione(Lingua.T("finestra.apri_github"), false, delegate { ApriSuGitHub(lLav); }));
        tErr = Scheda(Lingua.T("finestra.errori_ricorrenti"), out lErr, out dErr,
            new[] { Lingua.T("finestra.rapporti"), Lingua.T("finestra.volte"), Lingua.T("finestra.versioni"), Lingua.T("finestra.ultima_volta"), Lingua.T("comune.repo"), Lingua.T("finestra.errore") }, new[] { 70, 60, 110, 110, 200, 640 },
            TastoAzione(Lingua.T("finestra.crea_pacchetto_claude_code"), true, delegate { CreaPacchetto(); }));
        tErr.Allarme = true;
        tArch = Scheda(Lingua.T("finestra.archivio"), out lArch, out dArch,
            new[] { Lingua.T("comune.stato"), Lingua.T("comune.repo"), "#", Lingua.T("comune.titolo"), Lingua.T("finestra.autore"), Lingua.T("finestra.deciso") }, new[] { 90, 200, 50, 360, 120, 120 },
            TastoAzione(Lingua.T("finestra.annulla_decisione"), false, delegate { Decidi(lArch, "", false); }),
            TastoAzione(Lingua.T("finestra.apri_github"), false, delegate { ApriSuGitHub(lArch); }));
        schede.Voci.AddRange(new[] { tQuar, tLav, tErr, tArch });
        foreach (var v in schede.Voci) { v.Pagina.Dock = DockStyle.Fill; pagine.Controls.Add(v.Pagina); }
        destra.Controls.Add(pagine); destra.Controls.Add(schede); destra.Controls.Add(testata); destra.Controls.Add(stato);

        Controls.Add(destra); Controls.Add(colonna);
        Load += delegate {
            Aspetto.BarraScura(this); schede.Scegli(0); Carica();
            aperta = this;
            if (SoloFoto) return;
            CercaModelli();
            if (a.Conti.Count > 0) Aggiorna();
            if (!avvisoPartito) {
                avvisoPartito = true;
                Aggiornamenti.AllAvvio(() => aperta.a.AvvisoSpento, () => aperta.a.VersioneSaltata, n => aperta.InUi(() => aperta.Avvisa(n)));
            }
        };
        // nessun account collegato: la procedura si apre sopra la finestra già visibile
        Shown += delegate { if (!SoloFoto && a.Conti.Count == 0) Collega(true); };
    }

    // ---------- licenza ----------

    ScrittaLicenza lic;
    readonly ToolTip suggerimento = new ToolTip();

    public string TestoLicenza { get { return lic.Text; } }

    void MostraLicenza() {
        lic.Text = LicenzaQui.Scritta(LicenzaQui.Ora(Dati.Cartella));
        suggerimento.SetToolTip(lic, lic.Text);   // il nome lungo si taglia: qui si legge intero
        lic.Invalidate();
    }

    void ApriLicenza() {
        using (var f = new FinestraLicenza(Dati.Cartella)) {
            f.ShowDialog(this);
            if (f.Cambiata) { MostraLicenza(); stato.Text = Lingua.T("finestra.licenza_aggiornata", "lic", lic.Text); }
        }
    }

    // ---------- costruzione ----------

    Bottone Tasto(string testo, bool primario) {
        return new Bottone { Text = testo, Fondo = primario ? AZZURRO : Color.White, Scritta = primario ? Color.White : INCHIOSTRO, Linea = primario ? Color.Empty : BORDO };
    }

    Bottone TastoScuro(string testo) {
        return new Bottone { Text = testo, Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA };
    }

    Button TastoAzione(string testo, bool primario, EventHandler clic) {
        var b = Tasto(testo, primario);
        b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.Margin = new Padding(0, 0, S(8), 0);
        b.Click += clic;
        return b;
    }

    static string SCEGLI { get { return Lingua.T("finestra.scegli_riga_qui_sopra_leggere"); } }

    BarraSchede.Voce Scheda(string nome, out ListView lista, out TextBox dettaglio, string[] colonne, int[] larghezze, params Button[] tasti) {
        var t = new Panel { BackColor = Color.White, Padding = new Padding(S(16), S(10), S(16), S(10)), Visible = false };
        var sp = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = BORDO, SplitterWidth = S(4) };
        sp.Panel1.BackColor = Color.White; sp.Panel2.BackColor = Color.White;
        var l = new Tabella { Dock = DockStyle.Fill };
        for (int i = 0; i < colonne.Length; i++) l.Columns.Add(colonne[i], S(larghezze[i]));
        var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(44), Padding = new Padding(0, S(8), 0, 0), BackColor = Color.White, WrapContents = false };
        barra.Controls.AddRange(tasti);
        var d = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true,
                              BorderStyle = BorderStyle.None, BackColor = FONDINO, Font = new Font("Consolas", 9.5f), Text = SCEGLI };
        var vuoto = new Label { Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = GRIGIO, Padding = new Padding(S(12), S(12), S(12), 0),
                                UseMnemonic = false, Visible = false };
        vuoti[l] = vuoto;
        sp.Panel1.Controls.Add(l); sp.Panel1.Controls.Add(vuoto); sp.Panel1.Controls.Add(barra);
        sp.Panel2.Controls.Add(d);
        sp.Panel2.Padding = new Padding(0, S(6), 0, 0);
        t.Controls.Add(sp);
        t.Layout += delegate { if (sp.Height > S(300) && sp.SplitterDistance < S(200)) sp.SplitterDistance = sp.Height / 2; };
        var lCopia = l; var dCopia = d;
        l.SelectedIndexChanged += delegate { Dettaglio(lCopia, dCopia); };
        lista = l; dettaglio = d;
        return new BarraSchede.Voce { Nome = nome, Pagina = t };
    }

    // ---------- dati ----------

    bool caricando;

    void Carica() {
        a = Dati.Carica();
        storico = Storico.Carica();
        Ricalcola();
        Mostra();
    }

    void Ricalcola() {
        voci = Smistamento.Voci(a);
        errori = Smistamento.Ricorrenti(voci);
    }

    void Salva() {
        try { lock (this) Dati.Salva(a); } catch (Exception ex) { Dati.Log("salvataggio: " + ex.Message); MessageBox.Show(this, Lingua.T("finestra.non_riesco_salvare_l_archivio", "errore", ex.Message), Text); }
    }

    void SalvaStorico() {
        try { lock (storico) Storico.Salva(storico); } catch (Exception ex) { Dati.Log("storico: " + ex.Message); }
    }

    RepoInfo Scelta() {
        var r = repo.SelectedItem as RigaRepo;
        if (r != null && r.Gruppo) r = ultimaSel >= 0 && ultimaSel < repo.Items.Count ? repo.Items[ultimaSel] as RigaRepo : null;
        return r == null ? null : r.R;
    }

    // con le frecce si può arrivare sulla riga del gruppo: scendendo si entra nel gruppo, salendo si torna sopra
    bool SuGruppo() {
        var r = repo.SelectedItem as RigaRepo;
        if (r == null || !r.Gruppo) return false;
        int qui = repo.SelectedIndex;
        if (qui < ultimaSel) { repo.SelectedIndex = qui - 1; return true; }
        if (!spariteAperte) { spariteAperte = true; Mostra(); }
        repo.SelectedIndex = qui + 1;
        return true;
    }

    void ApriChiudiSparite() {
        var s = Scelta();
        if (spariteAperte && s != null && s.Sparita) { caricando = true; repo.SelectedIndex = 0; ultimaSel = 0; caricando = false; }   // la scelta non resta su una riga nascosta
        spariteAperte = !spariteAperte;
        Mostra();
    }

    // solo su questo computer: su GitHub non si chiede e non si cambia niente
    void Togli(List<string> nomi) {
        if (occupato) { stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); return; }
        Accessi.Togliere quanto;
        lock (this) lock (storico) quanto = Accessi.ContaTogli(a, storico, nomi);
        if (quanto.Repo.Count == 0) return;
        using (var f = new FinestraTogli(quanto)) {
            var r = RispostaTogli != null ? RispostaTogli(f) : f.ShowDialog(this);
            if (r != DialogResult.OK) return;
        }
        var s = Scelta();
        if (s != null && quanto.Repo.Contains(s.Nome)) { caricando = true; repo.SelectedIndex = 0; ultimaSel = 0; caricando = false; }
        lock (this) lock (storico) Accessi.TogliSparite(a, storico, quanto.Repo);
        Salva();
        SalvaStorico();
        Ricalcola();
        Mostra();
        stato.Text = Lingua.N("sparite.tolte", quanto.Repo.Count);
    }

    // ---------- l'avviso di versione nuova ----------

    // se è aperta un'altra finestra si aspetta: l'avviso non si mette in mezzo a una conferma
    void Avvisa(VersioneNuova n) {
        if (CanFocus || RispostaAvviso != null) { MostraAvviso(n, this); return; }
        AspettaAvviso(n);
    }

    public static int AttesaAvvisoMs = 5000;

    // mentre si aspetta la finestra può cambiare (la lingua ne apre una nuova): vale quella aperta adesso
    public void AspettaAvviso(VersioneNuova n) {
        var t = new System.Windows.Forms.Timer { Interval = AttesaAvvisoMs };
        t.Tick += delegate { var f = aperta; if (f == null || f.IsDisposed || !f.CanFocus) return; t.Stop(); t.Dispose(); f.MostraAvviso(n, f); };
        t.Start();
    }

    public void MostraAvviso(VersioneNuova n, IWin32Window sopra) {
        using (var f = new FinestraAvviso(n)) {
            var r = RispostaAvviso != null ? RispostaAvviso(f) : f.ShowDialog(sopra);
            if (r == DialogResult.Ignore) { a.VersioneSaltata = n.Etichetta; Salva(); }
            if (r == DialogResult.OK) Aggiornamenti.Apri(n.Pagina);
        }
    }

    List<RepoInfo> RepoOra() { lock (this) return a.Repo.ToList(); }
    List<RepoInfo> RepoVisibili() { lock (this) return Accessi.Visibili(a); }

    PaginaPanoramica.Conti Conta(string nome) {
        int c;
        string soglia = DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd");   // le date ISO si confrontano come testo
        return new PaginaPanoramica.Conti {
            Quarantena = voci.Count(v => v.E.Repo == nome && v.Stato == "quarantena"), Lavorare = voci.Count(v => v.E.Repo == nome && v.Stato == "lavorare"),
            Errori = errori.Count(g => g.Repo == nome), Consegne = consegne.TryGetValue(nome, out c) ? c : -1,
            NuoveSegnalazioni = voci.Count(v => v.E.Repo == nome && v.E.Tipo != "commento" && string.CompareOrdinal(v.E.Creato, soglia) >= 0),
            NuoviCommenti = voci.Count(v => v.E.Repo == nome && v.E.Tipo == "commento" && string.CompareOrdinal(v.E.Creato, soglia) >= 0) };
    }

    // la colonna di sinistra: prima «Tutte le repo», poi le repo vive, in fondo le sparite
    void Mostra() {
        var scelta = Scelta();
        int cima = repo.TopIndex;   // riempire da capo riporta la colonna in cima: resta dove l'hai lasciata
        caricando = true;
        RiempiScelte();
        repo.BeginUpdate();
        repo.Items.Clear();
        var mie = RepoVisibili().OrderBy(r => r.Sparita).ThenBy(r => r.Nome, StringComparer.OrdinalIgnoreCase).ToList();
        var nomi = new HashSet<string>(mie.Select(r => r.Nome));
        nascoste = new HashSet<string>(RepoOra().Select(r => r.Nome).Where(n => !nomi.Contains(n)));
        consegne.Clear();
        foreach (var r in mie) consegne[r.Nome] = Dati.ConsegneInAttesa(r.Nome);
        int spar = mie.Count(r => r.Sparita);
        if (spar == 0) spariteAperte = false;
        if (scelta != null && scelta.Sparita && mie.Any(r => r.Nome == scelta.Nome)) spariteAperte = true;   // la repo scelta non si nasconde
        repo.Items.Add(new RigaRepo { Nome = Lingua.T("comune.tutte_repo"), Sotto = Lingua.N("finestra.su_github", mie.Count - spar) + (spar > 0 ? ", " + Lingua.N("comune.sparite", spar) : ""),
                                      Quarantena = voci.Count(v => v.Stato == "quarantena" && !nascoste.Contains(v.E.Repo)), Lavorare = voci.Count(v => v.Stato == "lavorare" && !nascoste.Contains(v.E.Repo)) });
        int sel = 0;
        bool gruppo = false;
        foreach (var r in mie) {
            if (r.Sparita && !gruppo) {
                gruppo = true;
                repo.Items.Add(new RigaRepo { Gruppo = true, Aperto = spariteAperte, Nome = Lingua.T("sparite.gruppo", "n", Lingua.Numero(spar)) });
            }
            if (r.Sparita && !spariteAperte) continue;
            var c = Conta(r.Nome);
            int i = repo.Items.Add(new RigaRepo { R = r, Nome = PaginaPanoramica.CortoDi(r, a.Login ?? ""), Quarantena = c.Quarantena, Lavorare = c.Lavorare });
            if (scelta != null && r.Nome == scelta.Nome) sel = i;
        }
        repo.SelectedIndex = sel;
        ultimaSel = sel;
        repo.EndUpdate();
        repo.TopIndex = Math.Min(cima, repo.Items.Count - 1);
        caricando = false;
        Riempi();
    }

    // le due tendine in alto a sinistra: si cambiano sempre con «caricando» acceso, così non scattano da sole
    void RiempiScelte() {
        cConto.Items.Clear();
        foreach (var c in a.Conti) cConto.Items.Add(c.Login);
        cConto.Enabled = a.Conti.Count > 0;
        if (a.Conti.Count == 0) { cConto.Items.Add(Lingua.T("finestra.nessun_account_collegato")); cConto.SelectedIndex = 0; }
        else cConto.SelectedIndex = Math.Max(0, a.Conti.FindIndex(c => Accessi.Uguali(c.Login, a.Login)));
        filtri = Accessi.Filtri(a);
        cFiltro.Items.Clear();
        foreach (var f in filtri) cFiltro.Items.Add(f[1]);
        cFiltro.Enabled = a.Conti.Count > 0;
        cFiltro.SelectedIndex = Math.Max(0, filtri.FindIndex(f => f[0] == (a.Filtro ?? Accessi.TUTTE)));
    }

    void CambiaConto() {
        if (cConto.SelectedIndex < 0 || cConto.SelectedIndex >= a.Conti.Count) return;
        var c = a.Conti[cConto.SelectedIndex];
        if (Accessi.Uguali(c.Login, a.Login)) return;
        if (occupato) { stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); caricando = true; RiempiScelte(); caricando = false; return; }
        a.Login = c.Login; a.Filtro = null;
        GitHub.Esci();
        motivoVuoto = null;
        Salva(); Ricalcola(); Mostra();
        Aggiorna();
    }

    void CambiaFiltro() {
        if (cFiltro.SelectedIndex < 0 || cFiltro.SelectedIndex >= filtri.Count) return;
        a.Filtro = filtri[cFiltro.SelectedIndex][0];
        if (occupato) { Mostra(); return; }
        Salva(); Mostra();
    }

    IEnumerable<Voce> Visibili() {
        var r = Scelta();
        return voci.Where(v => !nascoste.Contains(v.E.Repo) && (r == null || v.E.Repo == r.Nome));
    }

    // la testata e la Panoramica; si richiama anche mentre le letture arrivano, una repo alla volta
    void Pagina() {
        var r = Scelta();
        bool vecchio = motivoVuoto != null && nonAggiornato;
        string quando = r != null && r.Pannello != null && r.Pannello.LettoIl != null ? r.Pannello.LettoIl : r == null ? a.UltimoAggiornamento : null;
        testata.Nome = r == null ? Lingua.T("comune.tutte_repo") : PaginaPanoramica.CortoDi(r, a.Login ?? "");
        testata.Pill = r == null ? null : r.Privata ? Lingua.T("comune.privata") : Lingua.T("comune.pubblica");
        testata.Pubblica = r != null && !r.Privata;
        testata.Descrizione = r == null ? Lingua.T("finestra.segnalazioni_tutte_repo_come_sta") : r.Sparita ? Lingua.T("finestra.non_c_piu_github")
            : string.IsNullOrEmpty(r.Descrizione) ? Lingua.T("finestra.nessuna_descrizione_github") : Riga(r.Descrizione, 200);
        testata.HaInterruttore = r != null && !r.Sparita;
        testata.Seguo = r != null && r.Seguita;
        testata.Letto = quando == null ? (r != null && r.Sparita ? Lingua.T("finestra.mai_letta") : Lingua.T("finestra.non_ancora_letta")) : (vecchio ? Lingua.T("finestra.senza_aggiornamento_dati_del") : Lingua.T("finestra.letto")) + quando;
        testata.LettoVecchio = vecchio;
        testata.Invalidate();
        letto.Text = a.UltimoAggiornamento == null ? Lingua.T("finestra.mai_aggiornato") : Lingua.T("finestra.aggiornato_2", "ultimoaggiornamento", a.UltimoAggiornamento);
        gestione.Imposta(r, a.Login);
        panoramica.Imposta(r, RepoVisibili().OrderBy(x => x.Sparita).ThenBy(x => x.Nome, StringComparer.OrdinalIgnoreCase).ToList(), a.Login, Conta, vecchio, spariteAperte, n => { lock (storico) return Storico.Copia(storico, n); });
    }

    // l'interruttore «Seguo le segnalazioni» (prima era la spunta accanto alla repo)
    void CambiaSeguo() {
        var r = Scelta();
        if (r == null || r.Sparita) return;   // una repo sparita non si può seguire
        if (occupato) { stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); return; }
        r.Seguita = !r.Seguita;
        if (r.Seguita) r.Dal = null;   // appena seguita: al prossimo aggiornamento si scarica tutta
        Salva();
        Mostra();
        stato.Text = r.Seguita ? Lingua.T("finestra.ora_segui_premi_aggiorna_github", "nome", r.Nome) : Lingua.T("finestra.non_segui_piu", "nome", r.Nome);
    }

    static string Data(string iso) {
        DateTime t;
        return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out t) ? t.ToLocalTime().ToString("dd/MM/yyyy") : "";
    }

    static string Riga(string s, int max) {
        s = Dogana.RendiVisibile((s ?? "").Replace("\r", " ").Replace("\n", " "));
        return s.Length > max ? s.Substring(0, max) + "..." : s;
    }

    string RiassuntoDi(Voce v) { return v.D != null && v.D.RiassuntoImpronta == v.Impronta ? v.D.Riassunto ?? "" : ""; }

    void Riempi() {
        if (a == null) return;
        var vis = Visibili().OrderByDescending(v => v.E.Aggiornato).ToList();
        Popola(lQuar, vis.Where(v => v.Stato == "quarantena"), v => new[] {
            v.E.Repo, v.E.Numero.ToString(), Riga(Smistamento.TitoloDi(v.E, a), 120), Riga(v.E.Autore, 40), Data(v.E.Creato),
            string.Join("; ", v.Motivi.Select(m => m.Spiegazione)) }, null);
        Popola(lLav, vis.Where(v => v.Stato == "lavorare"), v => new[] {
            Smistamento.NomeCategoria(v.Categoria), v.E.Repo, v.E.Numero.ToString(), Riga(Smistamento.TitoloDi(v.E, a), 120), Riga(v.E.Autore, 40), Data(v.E.Creato),
            Riga(RiassuntoDi(v), 200) }, v => v.Categoria);
        Popola(lArch, vis.Where(v => v.Stato == "fatto" || v.Stato == "rifiutato"), v => new[] {
            Smistamento.NomeStato(v.Stato), v.E.Repo, v.E.Numero.ToString(), Riga(Smistamento.TitoloDi(v.E, a), 120), Riga(v.E.Autore, 40),
            v.D == null ? "" : Riga(v.D.Quando, 20) }, null);
        var r = Scelta();
        var err = errori.Where(g => !nascoste.Contains(g.Repo) && (r == null || g.Repo == r.Nome)).ToList();
        lErr.BeginUpdate(); lErr.Items.Clear();
        foreach (var g in err)
            lErr.Items.Add(new ListViewItem(new[] { g.Rapporti.Count.ToString(), g.Volte.ToString(), string.Join(", ", g.Versioni.OrderBy(x => x)),
                g.Ultimo == DateTime.MinValue ? "" : g.Ultimo.ToString("dd/MM/yyyy"), g.Repo, Riga(g.Firma, 200) }) { Tag = g });
        lErr.EndUpdate();
        Vuoto(lQuar, Lingua.T("finestra.nessuna_segnalazione"), true); Vuoto(lLav, Lingua.T("finestra.niente_lavorare"), true);
        Vuoto(lErr, Lingua.T("finestra.nessun_errore_ricorrente_nei_log"), true); Vuoto(lArch, Lingua.T("finestra.ancora_nessuna_decisione_presa"), false);
        tQuar.Numero = lQuar.Items.Count; tLav.Numero = lLav.Items.Count; tErr.Numero = lErr.Items.Count; tArch.Numero = lArch.Items.Count;
        schede.Invalidate();
        foreach (var d in new[] { dQuar, dLav, dErr, dArch }) d.Text = "";
        Pagina();
    }

    // una lista vuota dice perché: repo sparita, non aggiornato, repo non seguite, oppure davvero niente
    void Vuoto(ListView l, string niente, bool suffisso) {
        Label m;
        if (!vuoti.TryGetValue(l, out m)) return;
        string t = null;
        if (l.Items.Count == 0) {
            var una = Scelta();
            if (a.Conti.Count == 0) t = Lingua.T("finestra.nessun_account_collegato_premi_account");
            else if (una != null && una.Sparita) t = Lingua.T("finestra.questa_repo_non_c_piu");
            else if (motivoVuoto != null && nonAggiornato) t = motivoVuoto;
            else if (a.UltimoAggiornamento == null) t = Lingua.T("finestra.non_aggiornato_premi_aggiorna_github");
            else if (una != null && !una.Seguita) t = Lingua.T("finestra.non_segui_questa_repo_accendi");
            else if (una == null && !RepoVisibili().Any(x => x.Seguita && !x.Sparita))
                t = Lingua.T("finestra.non_segui_nessuna_repo_sceglila");
            else t = niente + (!suffisso ? "." : una != null ? Lingua.T("finestra.in", "nome", una.Nome) : Lingua.T("finestra.nelle_repo_seguite"))
                     + (motivoVuoto != null ? " " + motivoVuoto : "");
        }
        m.Text = t ?? ""; m.Visible = t != null;
        if (t != null) m.BringToFront();
    }

    void Popola(ListView l, IEnumerable<Voce> lista, Func<Voce, string[]> colonne, Func<Voce, string> gruppo) {
        l.BeginUpdate();
        l.Items.Clear(); l.Groups.Clear();
        var gruppi = new Dictionary<string, ListViewGroup>();
        if (gruppo != null)
            foreach (var c in Smistamento.CATEGORIE) { var g = new ListViewGroup(c, Smistamento.NomeCategoria(c).Substring(0, 1).ToUpper() + Smistamento.NomeCategoria(c).Substring(1)); gruppi[c] = g; l.Groups.Add(g); }
        foreach (var v in lista) {
            var it = new ListViewItem(colonne(v)) { Tag = v };
            if (gruppo != null) it.Group = gruppi[gruppo(v)];
            l.Items.Add(it);
        }
        l.EndUpdate();
    }

    void Dettaglio(ListView l, TextBox d) {
        if (l.SelectedItems.Count == 0) { d.Text = SCEGLI; return; }
        var sb = new StringBuilder();
        var g = l.SelectedItems[0].Tag as ErroreRicorrente;
        if (g != null) {
            sb.AppendLine(Lingua.T("finestra.errore_2", "esempio", Dogana.RendiVisibile(g.Esempio)));
            sb.AppendLine(Lingua.T("finestra.firma", "firma", Dogana.RendiVisibile(g.Firma)));
            sb.AppendLine(Lingua.T("finestra.versioni_2", "rapporti", Lingua.N("finestra.n_rapporti", g.Rapporti.Count), "volte", Lingua.N("finestra.n_volte", g.Volte), "testo", string.Join(", ", g.Versioni.OrderBy(x => x)), "parte", (g.Ultimo == DateTime.MinValue ? "" : Lingua.T("finestra.dal_al", "primo", g.Primo.ToString("dd/MM/yyyy HH:mm"), "ultimo", g.Ultimo.ToString("dd/MM/yyyy HH:mm")))));
            sb.AppendLine();
            foreach (var v in voci.Where(x => g.Rapporti.Contains(x.E.Chiave))) sb.AppendLine("#" + v.E.Numero + " " + Riga(v.E.Titolo, 120) + "  " + v.E.Url);
            d.Text = sb.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
            return;
        }
        var w = (Voce)l.SelectedItems[0].Tag;
        sb.AppendLine(Lingua.T("finestra.categoria", "repo", w.E.Repo, "numero", w.E.Numero, "tipo", Smistamento.NomeTipo(w.E.Tipo), "autore", Dogana.RendiVisibile(w.E.Autore), "creato", Data(w.E.Creato), "parte", (w.E.Aperta == "closed" ? Lingua.T("finestra.chiusa") : ""), "categoria", Smistamento.NomeCategoria(w.Categoria)));
        sb.AppendLine(Dogana.RendiVisibile(Smistamento.TitoloDi(w.E, a)));
        sb.AppendLine(w.E.Url);
        if (w.Motivi.Count > 0) {
            sb.AppendLine();
            sb.AppendLine(w.Stato == "quarantena" ? Lingua.T("finestra.perche_quarantena") : Lingua.T("finestra.dogana_aveva_segnalato_approvato_mano"));
            foreach (var m in w.Motivi) sb.AppendLine("  - " + m);
        }
        string r = RiassuntoDi(w);
        if (r.Length > 0) { sb.AppendLine(); sb.AppendLine(Lingua.T("finestra.riassunto_2", "riassuntomodello", w.D.RiassuntoModello, "r", Dogana.RendiVisibile(r))); }
        sb.AppendLine();
        sb.AppendLine(Lingua.T("finestra.testo_originale_caratteri_invisibili_a"));
        sb.AppendLine(new string('_', 60));
        sb.AppendLine(Dogana.RendiVisibile(w.E.Testo ?? ""));
        d.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        d.SelectionStart = 0; d.SelectionLength = 0;
    }

    List<Voce> Scelte(ListView l) { return l.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag).OfType<Voce>().ToList(); }

    Decisione DecisioneDi(Voce v) {
        var d = a.Decisioni.FirstOrDefault(x => x.Chiave == v.E.Chiave);
        if (d == null) { d = new Decisione { Chiave = v.E.Chiave }; a.Decisioni.Add(d); }
        return d;
    }

    void Decidi(ListView l, string nuovo, bool chiedi) {
        if (occupato) { stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); return; }
        var sc = Scelte(l);
        if (sc.Count == 0) { stato.Text = Lingua.T("finestra.scegli_prima_piu_testi_nella"); return; }
        if (chiedi && sc.Count > 1 && MessageBox.Show(this, Lingua.T("finestra.approvi_testi_li_hai_letti", "n", sc.Count), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        foreach (var v in sc) {
            var d = DecisioneDi(v);
            d.Stato = nuovo; d.Impronta = v.Impronta; d.Quando = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }
        Salva(); Ricalcola(); Mostra();
        stato.Text = nuovo == "approvato" ? Lingua.N("finestra.approvati", sc.Count) : nuovo == "" ? Lingua.N("finestra.tornati", sc.Count) : Lingua.N("finestra.spostati", sc.Count, "stato", Smistamento.NomeStato(nuovo));
    }

    // col percorso pieno: un explorer.exe lasciato accanto al programma non deve partire
    static string Explorer { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"); } }

    void ApriSuGitHub(ListView l) {
        foreach (var v in Scelte(l).Take(5)) {
            if (!v.E.Url.StartsWith("https://github.com/" + v.E.Repo + "/issues/")) continue;   // solo l'indirizzo costruito da noi
            try { Process.Start(Explorer, "\"" + v.E.Url + "\""); } catch (Exception ex) { Dati.Log("browser: " + ex.Message); }
        }
    }

    // ---------- GitHub ----------

    void Occupato(bool si, string testo) {
        occupato = si;
        bAggiorna.Enabled = !si;
        if (!si) gestione.Imposta(Scelta(), a.Login);
        if (testo != null) stato.Text = testo;
    }

    void InUi(Action f) { try { if (!IsDisposed) BeginInvoke(f); } catch { } }

    // la chiave dell'account in uso per le scritture. Gira fuori dalla finestra, mai durante un aggiornamento
    Cliente StradaVera() {
        var conto = Accessi.Attivo(a);
        if (conto == null) throw new ErroreScrittura(Lingua.T("finestra.nessun_account_collegato_premi_account_2"));
        if (!GitHub.Collegato) GitHub.Accedi(conto);
        Scrittura.StessoConto(conto.Login, GitHub.Login, GitHub.Esci);
        return GitHub.Scrivente();
    }

    void Aggiorna() {
        if (occupato) return;
        var conto = Accessi.Attivo(a);
        if (conto == null) { stato.Text = Lingua.T("finestra.nessun_account_collegato_premi_account_2"); return; }
        Occupato(true, GitHub.Collegato ? Lingua.T("finestra.scarico_github") : Lingua.T("finestra.mi_collego_github_come", "login", conto.Login));
        new Thread(() => {
            string esito;
            try {
                if (!GitHub.Collegato) {
                    string vero = GitHub.Accedi(conto);
                    lock (this) {
                        // il conto di un archivio vecchio: ora si sa chi è davvero, e d'ora in poi lo si chiede a Git per nome
                        if (!Accessi.Uguali(vero, conto.Login)) Accessi.Rinomina(a, conto.Login, vero);
                        if (conto.Origine == Accessi.VECCHIO) conto.Origine = Accessi.GIT;
                    }
                }
                string chi = GitHub.Login;
                InUi(() => stato.Text = Lingua.T("finestra.accesso_come_leggo_repo", "chi", chi));
                var nuove = GitHub.Repo();
                int scaricati = 0, riuscite = 0, sparite = 0;
                var fallite = new List<string>();
                // una repo che GitHub non elenca più (cancellata o rinominata) si segna e non si scarica più
                lock (this) sparite = Accessi.Unisci(a, chi, nuove, n => n.Mia);
                InUi(() => { motivoVuoto = null; Mostra(); });
                // la Panoramica di ogni repo, anche di quelle non seguite: ogni richiesta può fallire da sola
                int nonLette = 0;
                foreach (var r in RepoOra().Where(x => !x.Sparita && Accessi.Vede(x, chi))) {
                    var r2 = r;
                    InUi(() => stato.Text = Lingua.T("finestra.leggo_come_sta", "nome", r2.Nome));
                    var nuova = GitHub.Panoramica(r, r.Pannello);
                    if (nuova.ReleaseErrore != null || nuova.TagErrore != null || nuova.CommitDopoErrore != null || nuova.CommitErrore != null
                        || nuova.CollaboratoriErrore != null || nuova.LicenzaErrore != null || nuova.ReadmeErrore != null || nuova.ReadmeDataErrore != null) nonLette++;
                    r.Pannello = nuova;
                    // i numeri di «Come sta andando» non contano per «letta a metà»: se GitHub non li dà, lo dice il loro riquadro
                    r.Numeri = GitHub.LeggiNumeri(r, r.Numeri);
                    lock (storico) Storico.Unisci(storico, r.Nome, r.Numeri, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                    InUi(Pagina);
                }
                SalvaStorico();
                // ogni repo si aggiorna per conto suo: se una fallisce, le altre vanno avanti
                foreach (var r in RepoOra().Where(x => x.Seguita && !x.Sparita && Accessi.Vede(x, chi))) {
                    var r2 = r;
                    InUi(() => stato.Text = Lingua.T("finestra.leggo", "nome", r2.Nome));
                    try {
                        string inizio = DateTime.UtcNow.AddMinutes(-2).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        var arrivati = GitHub.Novita(r.Nome, r.Dal);
                        lock (this) {
                            var indice = new Dictionary<string, int>();
                            for (int i = 0; i < a.Elementi.Count; i++) indice[a.Elementi[i].Chiave] = i;
                            foreach (var e in arrivati) {
                                int i;
                                if (indice.TryGetValue(e.Chiave, out i)) a.Elementi[i] = e; else { indice[e.Chiave] = a.Elementi.Count; a.Elementi.Add(e); }
                            }
                            r.Dal = inizio;
                        }
                        scaricati += arrivati.Count;
                        riuscite++;
                    } catch (Exception ex) {
                        Dati.Log("aggiornamento " + r.Nome + ": " + ex.Message);
                        fallite.Add(r.Nome + " (" + ex.Message + ")");
                    }
                }
                a.UltimoAggiornamento = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                lock (this) Dati.Salva(a);
                esito = Lingua.T("finestra.aggiornato", "ora", DateTime.Now.ToString("HH:mm"), "chi", chi) + " " + Lingua.N("finestra.testi_nuovi", scaricati) + " "
                      + Lingua.N("finestra.repo_aggiornate", riuscite)
                      + (fallite.Count > 0 ? ", " + Lingua.T("finestra.fallite", "n", fallite.Count, "elenco", string.Join("; ", fallite)) : "")
                      + (sparite > 0 ? Lingua.T("finestra.non_ci_sono_piu_github", "sparite", sparite) : "")
                      + (nonLette > 0 ? Lingua.N("finestra.letta_in", nonLette) : "") + ".";
                nonAggiornato = false;
                motivoVuoto = fallite.Count > 0 ? Lingua.T("finestra.alcune_repo_non_si_sono") : null;
            } catch (Exception ex) {
                Dati.Log("aggiornamento: " + ex.Message);
                esito = Lingua.T("finestra.non_aggiornato", "errore", ex.Message, "parte", (a.UltimoAggiornamento == null ? "" : Lingua.T("finestra.mostro_dati_del", "ultimoaggiornamento", a.UltimoAggiornamento)));
                nonAggiornato = true;
                motivoVuoto = esito;
            }
            InUi(() => { Ricalcola(); Mostra(); Occupato(false, esito); });
        }) { IsBackground = true }.Start();
    }

    // ---------- gli account ----------

    void Collega(bool prima) {
        var porta = new PortaVera();
        using (var g = new Guidata(a, porta, prima, Salva)) g.ShowDialog(this);
        GitHub.Esci();   // la procedura ha usato un altro accesso: al prossimo aggiornamento si riprende quello dell'account attivo
        Ricalcola(); Mostra();
        if (a.Conti.Count > 0) Aggiorna();
        else stato.Text = Lingua.T("finestra.nessun_account_collegato_quando_vuoi");
    }

    // la finestra degli account: quali sono collegati, aggiungerne uno, scollegarne uno
    void Conti() {
        if (occupato) { stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); return; }
        using (var f = new FinestraConti(a)) {
            f.ShowDialog(this);
            if (f.Scollegato != null) {
                var c = f.Scollegato;
                if (c.Origine == Accessi.BROWSER) Cassaforte.Cancella(Cassaforte.NomeDi(c.Login));
                Accessi.Scollega(a, c.Login);
                GitHub.Esci();
                Salva(); Ricalcola(); Mostra();
                if (a.Conti.Count > 0) Aggiorna();
                stato.Text = Lingua.T("finestra.scollegato_sue_repo_restano_nell", "login", c.Login);
            }
            if (f.Aggiungi) Collega(a.Conti.Count == 0);
        }
    }

    // ---------- Ollama ----------

    void CercaModelli() {
        new Thread(() => {
            try {
                var m = Ollama.Modelli();
                InUi(() => modelli = m);
            } catch { InUi(() => modelli = new List<string>()); }
        }) { IsBackground = true }.Start();
    }

    void Riassumi() {
        if (occupato) return;
        var sc = Scelte(lLav).Where(v => v.Stato == "lavorare").ToList();
        if (sc.Count == 0) { stato.Text = Lingua.T("finestra.scegli_prima_piu_testi_riassumere"); return; }
        string m = Ollama.Scegli(modelli, a.Modello);
        if (m == null) { stato.Text = Lingua.T("finestra.nessun_modello_ollama_accendi_ollama"); CercaModelli(); return; }
        Occupato(true, Lingua.T("finestra.riassumo_testi_sul_computer_gratis", "n", sc.Count, "m", m));
        new Thread(() => {
            int fatti = 0, scartati = 0;
            string errore = null;
            foreach (var v in sc) {
                try {
                    string r = Ollama.Riassumi(m, v.Categoria, Smistamento.TitoloDi(v.E, a), v.E.Testo ?? "");
                    // anche la risposta del modello passa dalla dogana: se un testo malevolo l'ha piegata, si butta
                    if (Dogana.Esamina("", r, a.DominiFidati).Count > 0) { scartati++; continue; }
                    lock (this) { var d = DecisioneDi(v); d.Riassunto = r; d.RiassuntoImpronta = v.Impronta; d.RiassuntoModello = m; }
                    fatti++;
                    int f2 = fatti;
                    InUi(() => stato.Text = Lingua.T("finestra.riassunti", "n", f2, "n2", sc.Count));
                } catch (Exception ex) { errore = ex.Message; break; }
            }
            InUi(() => {
                Salva(); Ricalcola(); Mostra();
                Occupato(false, Lingua.T("finestra.riassunti_fatti", "fatti", fatti, "parte", (scartati > 0 ? Lingua.T("finestra.scartati_perche_sospetti", "scartati", scartati) : ""), "parte2", (errore != null ? Lingua.T("finestra.fermato", "errore", errore) : ".")));
            });
        }) { IsBackground = true }.Start();
    }

    // ---------- pacchetto e impostazioni ----------

    void CreaPacchetto() {
        var r = Scelta();
        var nomi = r != null ? new List<string> { r.Nome }
                 : Visibili().Where(v => v.Stato == "lavorare").Select(v => v.E.Repo).Distinct().OrderBy(x => x).ToList();
        if (nomi.Count == 0) { stato.Text = Lingua.T("finestra.non_c_niente_lavorare_qui"); return; }
        string ultimo = null, testo = null;
        foreach (var n in nomi) { testo = Pacchetto.Testo(a, n, voci, errori); ultimo = Pacchetto.Scrivi(n, testo); }
        bool appunti = nomi.Count == 1 && Pacchetto.VaNegliAppunti(a.Repo.FirstOrDefault(x => x.Nome == nomi[0]));
        if (appunti) try { Clipboard.SetText(testo); } catch { }
        try { Process.Start(Explorer, "/select,\"" + ultimo + "\""); } catch { }
        stato.Text = appunti ? Lingua.T("finestra.pacchetto_scritto_copiato_negli_appunt", "ultimo", ultimo)
                   : nomi.Count == 1 ? Lingua.T("finestra.pacchetto_scritto_repo_privata_quindi", "ultimo", ultimo)
                   : Lingua.T("finestra.pacchetti_scritti", "nomi", nomi.Count, "pacchetti", Dati.Pacchetti);
    }

    // la finestra Impostazioni, scura come i dialoghi di WeightMonitor
    void Impostazioni() {
        if (occupato) { stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); return; }
        CercaModelli();
        using (var f = new Form { Text = Lingua.T("finestra.impostazioni"), ClientSize = new Size(S(520), S(470 + 74 + 118)), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                                  MinimizeBox = false, MaximizeBox = false, BackColor = Aspetto.FONDO, ForeColor = Aspetto.TESTO, Font = Font, AutoScaleMode = AutoScaleMode.None }) {
            Func<string, Font, Color, Rectangle, Label> scritta = (x, fo, co, re) => new Label { Text = x, Font = fo, ForeColor = co, Bounds = re, UseMnemonic = false, BackColor = Aspetto.FONDO };
            int m = S(24), lu = S(520) - m * 2, y0 = S(74);
            var l0 = scritta(Lingua.T("lingua.lingua"), Aspetto.MEDIO, Aspetto.TESTO, new Rectangle(m, S(16), lu, S(24)));
            var lingue = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(m, S(44), S(220), S(28)) };
            foreach (var c in Lingua.CODICI) lingue.Items.Add(Lingua.NomeDi(c));
            lingue.SelectedIndex = Array.IndexOf(Lingua.CODICI, Lingua.Codice);
            var l1 = scritta(Lingua.T("finestra.modello_riassunti"), Aspetto.MEDIO, Aspetto.TESTO, new Rectangle(m, y0 + S(20), lu, S(24)));
            var s1 = scritta(Lingua.T("finestra.ollama_lavora_questo_computer_gratis"), Aspetto.NORMALE, Aspetto.MUTO, new Rectangle(m, y0 + S(46), lu, S(22)));
            var scelta = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(m, y0 + S(72), lu, S(28)) };
            if (modelli.Count > 0) {
                foreach (var x in modelli) scelta.Items.Add(x);
                scelta.SelectedItem = Ollama.Scegli(modelli, a.Modello);
            } else { scelta.Items.Add(Lingua.T("finestra.nessun_modello_ollama_spento_non")); scelta.SelectedIndex = 0; scelta.Enabled = false; }
            var l2 = scritta(Lingua.T("finestra.siti_fidati"), Aspetto.MEDIO, Aspetto.TESTO, new Rectangle(m, y0 + S(122), lu, S(24)));
            var s2 = scritta(Lingua.T("finestra.sito_riga_link_verso_questi"), Aspetto.NORMALE, Aspetto.MUTO, new Rectangle(m, y0 + S(148), lu, S(44)));
            var t = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Text = string.Join("\r\n", a.DominiFidati), BackColor = Color.FromArgb(24, 25, 29), ForeColor = Aspetto.TESTO,
                                  BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(m, y0 + S(196), lu, S(110)) };
            var l3 = scritta(Lingua.T("finestra.cartella_delle_repo"), Aspetto.MEDIO, Aspetto.TESTO, new Rectangle(m, y0 + S(318), lu, S(24)));
            var s3 = scritta(Lingua.T("finestra.facoltativa_dove_tieni_repo_questo"), Aspetto.NORMALE, Aspetto.MUTO, new Rectangle(m, y0 + S(342), lu, S(22)));
            var cart = new TextBox { Text = a.CartellaRepo ?? "", BackColor = Color.FromArgb(24, 25, 29), ForeColor = Aspetto.TESTO, BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(m, y0 + S(368), lu - S(110), S(26)) };
            var sf = TastoScuro(Lingua.T("finestra.sfoglia")); sf.SetBounds(m + lu - S(100), y0 + S(366), S(100), S(30));
            sf.Click += delegate {
                using (var d = new FolderBrowserDialog { Description = Lingua.T("finestra.cartella_che_contiene_tue_repo"), ShowNewFolderButton = false, SelectedPath = cart.Text })
                    if (d.ShowDialog(f) == DialogResult.OK) cart.Text = d.SelectedPath;
            };
            var l4 = scritta(Lingua.T("agg.sezione"), Aspetto.MEDIO, Aspetto.TESTO, new Rectangle(m, y0 + S(410), lu, S(24)));
            var avviso = new CheckBox { Text = Lingua.T("agg.controlla_all_avvio"), Checked = !a.AvvisoSpento, ForeColor = Aspetto.TESTO, BackColor = Aspetto.FONDO, UseMnemonic = false, AutoSize = false, Bounds = new Rectangle(m, y0 + S(438), lu - S(170), S(28)) };
            var ora = TastoScuro(Lingua.T("agg.controlla_ora")); ora.SetBounds(m + lu - S(160), y0 + S(436), S(160), S(32));
            var esito = scritta("", Aspetto.NORMALE, Aspetto.MUTO, new Rectangle(m, y0 + S(472), lu, S(40)));
            ora.Click += delegate {
                ora.Enabled = false;
                esito.Text = Lingua.T("agg.sto_controllando");
                new Thread(() => {
                    VersioneNuova n = null; string testo;
                    try { n = Aggiornamenti.Cerca(); testo = n == null ? Lingua.T("agg.ultima", "v", Programma.VERSIONE) : ""; }
                    catch (Exception ex) { Dati.Log("aggiornamenti: " + ex.Message); testo = Lingua.T("agg.non_cerca", "err", ex.Message.TrimEnd('.', ' ')); }
                    try {
                        f.BeginInvoke((Action)(() => {
                            esito.Text = testo; ora.Enabled = true;
                            if (n != null) MostraAvviso(n, f);
                        }));
                    } catch (InvalidOperationException) { }   // la finestra si è chiusa nel frattempo
                }) { IsBackground = true }.Start();
            };
            var ok = Tasto(Lingua.T("comune.salva"), true); ok.SetBounds(m + lu - S(130), y0 + S(528), S(130), S(36)); ok.DialogResult = DialogResult.OK;
            var no = TastoScuro(Lingua.T("comune.annulla")); no.SetBounds(m + lu - S(130) - S(10) - S(110), y0 + S(528), S(110), S(36)); no.DialogResult = DialogResult.Cancel;
            f.Controls.AddRange(new Control[] { l0, lingue, l1, s1, scelta, l2, s2, t, l3, s3, cart, sf, l4, avviso, ora, esito, ok, no });
            f.CancelButton = no;
            f.Load += delegate { Aspetto.BarraScura(f); ok.Focus(); t.SelectionLength = 0; };
            if (FotoImpostazioni != null) { FotoImpostazioni(f); return; }
            if (f.ShowDialog(this) != DialogResult.OK) return;
            if (scelta.Enabled && scelta.SelectedItem != null) a.Modello = Convert.ToString(scelta.SelectedItem);
            a.AvvisoSpento = !avviso.Checked;
            var nuovi = t.Lines.Select(x => x.Trim().ToLowerInvariant().TrimStart('*', '.')).Where(x => x.Length > 2 && x.Contains(".") && !x.Contains(" ") && !x.Contains("/")).Distinct().ToList();
            if (nuovi.Count > 0) a.DominiFidati = nuovi;
            string nuova = cart.Text.Trim();
            if (nuova.Length == 0 || Directory.Exists(nuova)) a.CartellaRepo = Dati.CartellaRepo = nuova;
            else MessageBox.Show(this, Lingua.T("finestra.questa_cartella_non_c_cartella"), Text);
            Salva();
            if (CambiaLingua(Lingua.CODICI[Math.Max(0, lingue.SelectedIndex)])) return;
            Ricalcola(); Mostra();
            stato.Text = Lingua.T("finestra.impostazioni_salvate");
        }
    }
}

// la conferma del togliere dall'elenco: dice che su GitHub non cambia niente e cosa si perde su questo computer. Il tasto di partenza è Annulla
public class FinestraTogli : Dialogo {
    public Bottone BTogli, BAnnulla;
    public string Racconto;

    public FinestraTogli(Accessi.Togliere t) {
        Func<int, int> S = Aspetto.S;
        Func<string, string> V = x => Dogana.RendiVisibile(x ?? "");
        Text = Lingua.T("sparite.togli_dall_elenco");
        try { Icon = Aspetto.Icona(); } catch { }
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        int m = S(24), lu = S(540) - m * 2, y = S(18);
        Func<string, Font, Color, Label> riga = (testo, fo, co) => {
            int h = TextRenderer.MeasureText(testo, fo, new Size(lu, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + S(4);
            var l = new Label { Text = testo, Font = fo, ForeColor = co, BackColor = Aspetto.FONDO, Bounds = new Rectangle(m, y, lu, h), UseMnemonic = false };
            Controls.Add(l);
            y += h + S(8);
            return l;
        };
        riga(t.Repo.Count == 1 ? Lingua.T("sparite.togliere_una", "nome", V(t.Repo[0])) : Lingua.N("sparite.togliere_molte", t.Repo.Count), Aspetto.GRANDE, Aspetto.TESTO);
        if (t.Repo.Count > 1) {
            string nomi = string.Join(", ", t.Repo.Take(8).Select(V)) + (t.Repo.Count > 8 ? ", " + Lingua.N("sparite.e_altre", t.Repo.Count - 8) : "");
            riga(nomi, Aspetto.NORMALE, Aspetto.MUTO);
        }
        var parti = new List<string> { Lingua.N("sparite.testi", t.Testi) };
        if (t.Decisioni > 0) parti.Add(Lingua.N("sparite.decisioni", t.Decisioni));
        parti.Add(Lingua.N("sparite.giorni", t.GiorniStorico));
        Racconto = Lingua.T("sparite.su_github_niente") + "\n" + Lingua.T("sparite.si_perdono", "elenco", string.Join(", ", parti)) + "\n" + Lingua.T("sparite.se_torna");
        foreach (var r in Racconto.Split('\n')) riga(r, Aspetto.NORMALE, Aspetto.TESTO);
        y += S(8);
        string siTesto = t.Repo.Count > 1 ? Lingua.T("sparite.togli_tutte") : Lingua.T("sparite.togli_dall_elenco");
        BTogli = new Bottone { Text = siTesto, Fondo = Aspetto.AZZURRO, Scritta = Color.White, Linea = Color.Empty, DialogResult = DialogResult.OK };
        BAnnulla = new Bottone { Text = Lingua.T("comune.annulla"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, DialogResult = DialogResult.Cancel };
        int ls = Math.Max(S(120), BTogli.GetPreferredSize(Size.Empty).Width), ln = Math.Max(S(110), BAnnulla.GetPreferredSize(Size.Empty).Width);
        BTogli.Bounds = new Rectangle(m + lu - ls, y, ls, S(36));
        BAnnulla.Bounds = new Rectangle(m + lu - ls - S(10) - ln, y, ln, S(36));
        Controls.AddRange(new Control[] { BAnnulla, BTogli });
        ClientSize = new Size(S(540), y + S(36) + S(18));
        CancelButton = BAnnulla;
        Load += delegate { Aspetto.BarraScura(this); };
        Shown += delegate { BAnnulla.Focus(); };
    }
}

// gli account collegati. Chi usa il programma qui collega o scollega; per passare dall'uno all'altro basta la tendina
public class FinestraConti : Dialogo {
    public ContoGitHub Scollegato;
    public bool Aggiungi;

    public FinestraConti(Archivio a) {
        Func<int, int> S = Aspetto.S;
        Text = Lingua.T("comune.account");
        try { Icon = Aspetto.Icona(); } catch { }
        ClientSize = new Size(S(520), S(400));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        int m = S(24), lu = S(520) - m * 2;
        var t = new Label { Text = Lingua.T("finestra.account_collegati"), Font = Aspetto.GRANDE, BackColor = Aspetto.FONDO, Bounds = new Rectangle(m, S(16), lu, S(34)), UseMnemonic = false };
        var s = new Label { Text = Lingua.T("finestra.passi_dall_all_altro_tendina"),
                            ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Bounds = new Rectangle(m, S(52), lu, S(44)), UseMnemonic = false };
        var l = new ListBox { BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, BorderStyle = BorderStyle.None, IntegralHeight = false, Bounds = new Rectangle(m, S(104), lu, S(170)), ItemHeight = S(24), DrawMode = DrawMode.OwnerDrawFixed };
        int oltre = TextRenderer.MeasureText(s.Text, Font, new Size(lu, 0), TextFormatFlags.WordBreak).Height + S(4) - s.Height;
        if (oltre > 0) { s.Height += oltre; l.Top += oltre; l.Height -= oltre; }
        foreach (var c in a.Conti) l.Items.Add(c);
        l.DrawItem += (o, e) => {
            if (e.Index < 0) return;
            bool su = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(su ? Aspetto.AZZURRO : Aspetto.CAPSULA)) e.Graphics.FillRectangle(b, e.Bounds);
            var c = (ContoGitHub)l.Items[e.Index];
            string d = (Accessi.Uguali(c.Login, a.Login) ? Lingua.T("finestra.uso") : "") + (c.Origine == Accessi.BROWSER ? Lingua.T("finestra.accesso_custodito_windows") : Lingua.T("finestra.accesso_git"));
            Aspetto.T(e.Graphics, c.Login, Aspetto.FORTE, e.Bounds.X + S(8), e.Bounds.Y, S(190), e.Bounds.Height, Color.White);
            Aspetto.T(e.Graphics, d, Aspetto.PICCOLO, e.Bounds.X + S(200), e.Bounds.Y, e.Bounds.Width - S(208), e.Bounds.Height, su ? Color.White : Aspetto.MUTO);
        };
        if (a.Conti.Count > 0) l.SelectedIndex = Math.Max(0, a.Conti.FindIndex(c => Accessi.Uguali(c.Login, a.Login)));
        var togli = new Bottone { Text = Lingua.T("finestra.scollega"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, Bounds = new Rectangle(m, S(284), S(120), S(36)), Enabled = a.Conti.Count > 0 };
        var nota = new Label { Text = Lingua.T("finestra.sue_repo_restano_nell_archivio"), Font = Aspetto.PICCOLO, ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Bounds = new Rectangle(m + S(130), S(284), lu - S(130), S(36)), UseMnemonic = false };
        var nuovo = new Bottone { Text = Lingua.T("comune.collega_altro_account"), Fondo = Aspetto.AZZURRO, Scritta = Color.White, Linea = Color.Empty, Bounds = new Rectangle(m, S(342), S(220), S(38)) };
        var chiudi = new Bottone { Text = Lingua.T("comune.chiudi"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, Bounds = new Rectangle(m + lu - S(110), S(342), S(110), S(38)), DialogResult = DialogResult.Cancel };
        togli.Click += delegate {
            var c = l.SelectedItem as ContoGitHub;
            if (c == null) return;
            if (MessageBox.Show(this, Lingua.T("finestra.scollegare_sue_repo_tue_decisioni", "login", c.Login), Lingua.T("comune.account"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Scollegato = c; DialogResult = DialogResult.OK;
        };
        nuovo.Click += delegate { Aggiungi = true; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { t, s, l, togli, nota, nuovo, chiudi });
        CancelButton = chiudi;
        Load += delegate { Aspetto.BarraScura(this); };
    }
}
