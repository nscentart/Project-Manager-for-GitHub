using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

// quello che le finestre chiedono al mondo fuori: le prove e le foto ne usano uno che parla col server finto
public class PortaScrittura {
    public Func<Cliente> Strada;
    public string Login;
    public IPorta Porta;
    public Func<CodiceDispositivo> ChiediCancella;
    public Func<CodiceDispositivo, Func<bool>, string> AspettaCancella;
    public Func<string, Cliente> UsaEGetta;
    public Action<int> Dorme = Thread.Sleep;
    public Func<Form, DialogResult> Apri = f => f.ShowDialog();

    public static PortaScrittura Vera(PortaVera porta, Func<Cliente> strada) {
        return new PortaScrittura {
            Strada = strada, Porta = porta, UsaEGetta = t => Cliente.Vero(t, false),
            ChiediCancella = () => Accessi.Chiedi(Accessi.CLIENT_ID, Accessi.PERMESSO_CANCELLA, Accessi.Posta),
            AspettaCancella = (c, annullato) => Accessi.Aspetta(c, Accessi.CLIENT_ID, Accessi.Posta, s => { for (int i = 0; i < s * 5 && !annullato(); i++) Thread.Sleep(200); }, annullato) };
    }
}

// la base delle finestre: scura, con la riga dell'avviso. Il lavoro gira fuori dalla finestra e i tasti restano spenti finché non finisce
public class Modulo : Dialogo {
    protected readonly PortaScrittura P;
    public Label Avviso;
    public bool Lavorando, Cambiato;
    protected readonly List<Bottone> Tasti = new List<Bottone>();
    protected readonly int M, Lu;

    protected int S(int v) { return Aspetto.S(v); }
    protected static string V(string s) { return Dogana.RendiVisibile(s ?? ""); }

    public Modulo(PortaScrittura p, string titolo, int larghezza, int altezza) {
        P = p;
        Text = titolo;
        ClientSize = new Size(S(larghezza), S(altezza));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        try { Icon = Aspetto.Icona(); } catch { }
        M = S(24); Lu = S(larghezza) - M * 2;
        FormClosing += (o, e) => { if (Lavorando) e.Cancel = true; };
        Load += delegate { Aspetto.BarraScura(this); };
    }

    protected Label Scritta(string testo, Font f, Color c, int y, int alt) {
        var l = new Label { Text = testo, Font = f, ForeColor = c, BackColor = Aspetto.FONDO, Bounds = new Rectangle(M, y, Lu, alt), UseMnemonic = false };
        Controls.Add(l);
        return l;
    }

    protected void Riga(int y, int alt) {
        Avviso = new Label { ForeColor = Aspetto.AMBRA, BackColor = Aspetto.FONDO, Bounds = new Rectangle(M, y, Lu, alt), UseMnemonic = false };
        Controls.Add(Avviso);
    }

    protected Bottone Tasto(string testo, bool primario, Rectangle r, EventHandler clic) {
        var b = new Bottone { Text = testo, Bounds = r, Fondo = primario ? Aspetto.AZZURRO : Aspetto.CAPSULA, Scritta = primario ? Color.White : Aspetto.TESTO, Linea = primario ? Color.Empty : Aspetto.RIGA };
        b.Click += clic;
        Controls.Add(b); Tasti.Add(b);
        return b;
    }

    protected Bottone Rosso(string testo, Rectangle r, EventHandler clic) {
        var b = Tasto(testo, false, r, clic);
        b.Fondo = Aspetto.ROSSO; b.Scritta = Color.White; b.Linea = Color.Empty;
        return b;
    }

    protected TextBox Campo(string testo, Rectangle r, bool righe) {
        var t = new TextBox { Multiline = righe, Text = testo ?? "", Bounds = r, BackColor = Color.FromArgb(24, 25, 29), ForeColor = Aspetto.TESTO, BorderStyle = BorderStyle.FixedSingle, AcceptsReturn = righe, ScrollBars = righe ? ScrollBars.Vertical : ScrollBars.None };
        Controls.Add(t);
        return t;
    }

    protected ListView Lista(Rectangle r, string[] colonne, int[] larghezze) {
        // le intestazioni sono scritte a mano: quelle di Windows nel tema scuro restano una striscia bianca
        int alto = S(22), x = r.X;
        var l = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.None, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, BorderStyle = BorderStyle.None, Bounds = new Rectangle(r.X, r.Y + alto, r.Width, r.Height - alto), HideSelection = false };
        for (int i = 0; i < colonne.Length; i++) {
            l.Columns.Add(colonne[i], S(larghezze[i]));
            Controls.Add(new Label { Text = colonne[i], ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Bounds = new Rectangle(x + S(2), r.Y, S(larghezze[i]) - S(2), alto), UseMnemonic = false });
            x += S(larghezze[i]);
        }
        l.SelectedIndexChanged += delegate { Rifai(); };
        Controls.Add(l);
        return l;
    }

    protected virtual bool Puoi(Bottone b) { return true; }
    protected void Rifai() { foreach (var b in Tasti) b.Enabled = (!Lavorando && Puoi(b)) || AncheAlLavoro(b); Spiega(); }
    protected virtual bool AncheAlLavoro(Bottone b) { return false; }
    protected virtual void Spiega() { }

    public void Nota(string testo) { if (Avviso != null) { Avviso.ForeColor = Aspetto.AMBRA; Avviso.Text = testo; } }
    protected void Bene(string testo) { Cambiato = true; if (Avviso != null) { Avviso.ForeColor = Aspetto.LIME; Avviso.Text = testo; } }

    protected static string Elenco(IEnumerable<Riscontro> l) {
        var r = l.ToList();
        return string.Join("\n", r.Take(10).Select(x => V(x.ToString()))) + (r.Count > 10 ? Lingua.T("gestione.altri", "parte", (r.Count - 10)) : "");
    }

    // le frasi d'errore le scriviamo noi: niente testi di eccezioni qualsiasi, che potrebbero portarsi dietro di tutto
    protected string Frase(Exception e) {
        var s = e as ErroreScrittura;
        if (s != null) return s.Riscontri != null ? s.Message + "\n" + Elenco(s.Riscontri) : s.Message;
        if (e is ErroreAccesso) return e.Message;
        Dati.Log("lavoro su GitHub: " + e.GetType().Name);
        return Lingua.T("gestione.qualcosa_non_andato_non_so");
    }

    protected virtual void Sbaglio(Exception e) { Nota(Frase(e)); }

    protected void Lavora<T>(Func<Cliente, T> lavoro, Action<T> dopo) {
        Lavorando = true; Rifai();
        new Thread(() => {
            T r = default(T); Exception x = null;
            try { r = lavoro(P.Strada()); } catch (Exception e) { x = e; }
            try {
                BeginInvoke((Action)(() => {
                    Lavorando = false;
                    if (x != null) Sbaglio(x); else dopo(r);
                    if (!IsDisposed) Rifai();
                }));
            } catch (InvalidOperationException) { }
        }) { IsBackground = true }.Start();
    }

    protected void InUi(Action f) { try { if (!IsDisposed) BeginInvoke(f); } catch (InvalidOperationException) { } }

    public static string Dice(FattiRepo f) {
        return Lingua.T("gestione.qui", "nome", f.Nome, "parte", (f.Privata ? Lingua.T("comune.privata") : Lingua.T("comune.pubblica")), "parte2", (f.Archiviata ? Lingua.T("gestione.archiviata") : ""), "f", Stelle(f), "f2", Copie(f), "f3", Diritti.Ruolo(f));
    }

    protected static string Stelle(FattiRepo f) { return Lingua.N("gestione.stelle", f.Stelle); }
    protected static string Copie(FattiRepo f) { return Lingua.N("gestione.copie", f.Copie); }
}

// la conferma, sempre riletta da GitHub: quello che si mostra e il nome da scrivere vengono dalla lettura di adesso. Scrivere: col nome della repo, altrimenti un clic
public class Conferma : Modulo {
    public TextBox Nome;
    public Bottone BSi, BNo;
    public Label Racconto, Chiede;
    public bool Fatto;
    public FattiRepo Visti;
    public volatile bool Fermato;
    public bool Riprova;   // il lavoro dopo la conferma può durare e dire a che punto è: si può fermare anche lì
    readonly bool scrivere;
    bool conPassi;
    string letto = "";   // quello che si sta confermando: ci si torna quando i testi dell'attesa non sono più veri

    public Conferma(PortaScrittura p, string repo, string titolo, string tasto, bool scrivereNome, bool grave, Func<Cliente, FattiRepo, string> leggi, Action<Cliente, FattiRepo, string> fa) : base(p, titolo, 600, 460) {
        scrivere = scrivereNome;
        Scritta(titolo, Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Racconto = Scritta(Lingua.T("gestione.rileggo_github"), Aspetto.NORMALE, Aspetto.TESTO, S(56), S(224));
        Chiede = Scritta("", Aspetto.NORMALE, Aspetto.MUTO, S(284), S(22));
        Nome = Campo("", new Rectangle(M, S(308), Lu, S(26)), false);
        Nome.Visible = Chiede.Visible = scrivere;
        Nome.TextChanged += delegate { Rifai(); };
        Riga(S(340), S(62));
        var r = new Rectangle(M + Lu - S(240), S(410), S(240), S(36));
        EventHandler si = delegate {
            var v = Visti; string scritto = Nome.Text;
            Fermato = false; conPassi = false;   // l'Annulla di prima valeva per il giro di prima
            Lavora<bool>(c => { fa(c, v, scritto); return true; }, x => { Fatto = true; DialogResult = DialogResult.OK; });
        };
        BSi = grave ? Rosso(tasto, r, si) : Tasto(tasto, true, r, si);
        BNo = Tasto(Lingua.T("comune.annulla"), false, new Rectangle(r.X - S(120), S(410), S(110), S(36)), delegate {
            if (!Lavorando) { Close(); return; }
            Fermato = true; Racconto.Text = Lingua.T("gestione.mi_fermo"); Rifai();
        });
        Shown += delegate {
            if (scrivere) Nome.Focus(); else BNo.Focus();
            Lavora(c => { var f = Scrittura.Fatti(c, repo); return new KeyValuePair<FattiRepo, string>(f, leggi(c, f)); }, x => {
                if (Fermato) { Close(); return; }   // l'Annulla è arrivato quando il controllo aveva già finito
                Visti = x.Key;
                Racconto.Text = letto = V(Dice(x.Key)) + (P.Login != null ? Lingua.T("gestione.agisci_come", "login", V(P.Login)) : "") + "\n\n" + x.Value;
                Chiede.Text = Lingua.T("gestione.confermare_scrivi", "nome", V(x.Key.Nome));
            });
        };
        Rifai();
    }

    // un controllo lungo dice a che punto è e si può fermare: si ferma al passo dopo, e fino a lì non si è scritto niente
    public void Passo(string testo) {
        Controlla();
        InUi((Action)(() => { if (Fermato) return; conPassi = true; Racconto.Text = V(testo); Rifai(); }));
    }

    // subito prima di scrivere: l'Annulla vale fino a qui, dopo la richiesta è partita e non si richiama
    public void Controlla() { if (Fermato) throw new ControlloFermato(Lingua.T("gestione.controllo_fermato_non_ho_fatto")); }

    protected override bool AncheAlLavoro(Bottone b) { return b == BNo && conPassi && !Fermato && (Visti == null || Riprova); }
    protected override bool Puoi(Bottone b) { return b == BNo || (Visti != null && (!scrivere || Nome.Text == Visti.Nome)); }
    // «Riprovo...» e «Mi fermo...» alla fine non sono più veri: si torna a quello che si stava confermando, e sotto c'è come è andata
    protected override void Sbaglio(Exception e) { if (Fermato && Visti == null) { Close(); return; } base.Sbaglio(e); if (Visti != null) { Racconto.Text = letto; return; } Racconto.Text = Lingua.T("gestione.non_faccio_niente"); Racconto.Height = S(24); Nome.Visible = Chiede.Visible = false; Avviso.Bounds = new Rectangle(M, S(84), Lu, S(318)); }
}

// un campo solo. Con «fa» il lavoro parte da qui, altrimenti chi l'ha aperta legge Valore
public class ModuloTesto : Modulo {
    public TextBox Valore;
    public Bottone BSi, BNo;
    public Func<string, bool> Valido;

    public ModuloTesto(PortaScrittura p, string titolo, string spiega, string valore, bool righe, string tasto, Action<Cliente, string> fa) : base(p, titolo, 600, righe ? 340 : 270) {
        Scritta(titolo, Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Scritta(spiega, Aspetto.NORMALE, Aspetto.MUTO, S(54), S(46));
        Valore = Campo(valore, new Rectangle(M, S(104), Lu, righe ? S(96) : S(26)), righe);
        Valore.TextChanged += delegate { Rifai(); };
        int y = righe ? S(204) : S(134);
        Riga(y, S(70));
        var r = new Rectangle(M + Lu - S(170), y + S(78), S(170), S(36));
        BSi = Tasto(tasto, true, r, delegate {
            string t = Valore.Text.Trim();
            if (fa == null) { DialogResult = DialogResult.OK; return; }
            Lavora<bool>(c => { fa(c, t); return true; }, x => DialogResult = DialogResult.OK);
        });
        BNo = Tasto(Lingua.T("comune.annulla"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(36)), delegate { Close(); });
        Shown += delegate { Valore.Focus(); Rifai(); };
    }

    protected override bool Puoi(Bottone b) { return b == BNo || Valido == null || Valido(Valore.Text.Trim()); }
}

// una repo nuova: nasce privata, sempre
public class ModuloRepoNuova : Modulo {
    static string NOME_NO { get { return Lingua.T("comune.nome_puo_avere_lettere_senza"); } }
    public ComboBox Dove;
    public TextBox NomeRepo, Descrizione;
    public CheckBox ConReadme;
    public Bottone BSi, BNo;
    public FattiRepo Creata;

    public ModuloRepoNuova(PortaScrittura p, string dovePreferito) : base(p, Lingua.T("gestione.crea_repo"), 600, 490) {
        Scritta(Lingua.T("gestione.crea_repo"), Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Scritta(Lingua.T("gestione.nasce_privata_vedi_solo_tu"), Aspetto.NORMALE, Aspetto.MUTO, S(54), S(44));
        Scritta(Lingua.T("gestione.chi_sara"), Aspetto.NORMALE, Aspetto.TESTO, S(102), S(20));
        Dove = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(M, S(124), Lu, S(28)) };
        Dove.SelectedIndexChanged += delegate { Rifai(); };
        Controls.Add(Dove);
        Scritta(Lingua.T("gestione.nome"), Aspetto.NORMALE, Aspetto.TESTO, S(160), S(20));
        NomeRepo = Campo("", new Rectangle(M, S(182), Lu, S(26)), false);
        NomeRepo.TextChanged += delegate { if (!Lavorando && Creata == null) Nota(""); Rifai(); };
        Scritta(Lingua.T("gestione.descrizione_facoltativa"), Aspetto.NORMALE, Aspetto.TESTO, S(216), S(20));
        Descrizione = Campo("", new Rectangle(M, S(238), Lu, S(26)), false);
        ConReadme = new CheckBox { Text = Lingua.T("gestione.mettici_subito_readme_pagina_presentaz"), Checked = true, Bounds = new Rectangle(M, S(274), Lu, S(26)), BackColor = Aspetto.FONDO, ForeColor = Aspetto.TESTO, UseMnemonic = false };
        Controls.Add(ConReadme);
        Riga(S(306), S(122));
        var r = new Rectangle(M + Lu - S(170), S(438), S(170), S(36));
        BSi = Tasto(Lingua.T("gestione.crea_repo_2"), true, r, delegate {
            var d = Dove.SelectedItem as Proprietario; string n = NomeRepo.Text.Trim(), t = Descrizione.Text.Trim(); bool k = ConReadme.Checked; string chi = P.Login;
            Lavora(c => Scrittura.CreaRepo(c, chi, d, n, t, k), f => {
                Creata = f; Cambiato = true;
                if (f.Nota == null) DialogResult = DialogResult.OK; else { Nota(f.Nota); BNo.Text = Lingua.T("comune.chiudi"); }
            });
        });
        BNo = Tasto(Lingua.T("comune.annulla"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(36)), delegate { Close(); });
        Shown += delegate {
            string chi = P.Login;
            NomeRepo.Focus();
            Lavora(c => Scrittura.Proprietari(c, chi), l => {
                foreach (var x in l) Dove.Items.Add(x);
                Dove.SelectedIndex = Math.Max(0, l.FindIndex(x => Organizzazione.Uguali(x.Nome, dovePreferito)));
            });
        };
        Rifai();
    }

    protected override bool Puoi(Bottone b) {
        if (b == BNo) return true;
        var d = Dove.SelectedItem as Proprietario;
        return Creata == null && d != null && d.Perche == null && Scrittura.NomeRepoValido(NomeRepo.Text.Trim());
    }

    string motivo;
    protected override void Spiega() {
        if (Lavorando) return;
        var d = Dove.SelectedItem as Proprietario;
        string n = NomeRepo.Text.Trim(), ora = d != null && d.Perche != null ? d.Perche : n.Length > 0 && !Scrittura.NomeRepoValido(n) ? NOME_NO : null;
        if (ora != null) Nota(ora); else if (Avviso.Text == motivo) Nota("");
        motivo = ora;
    }
}

// caricare una cartella: prima si guarda cosa c'è dentro. Se c'è qualcosa che non deve uscire, il tasto resta spento e si dice cosa
public class FinestraCarica : Modulo {
    public Bottone BScegli, BSi, BNo, BFuori;
    public Label Sommario, ChiedeNome, Tolti;
    public TextBox Dettaglio, Messaggio, NomeScritto;
    public CheckBox Spunta;
    public Cartella Pronta;
    public EsitoCarica Esito;
    readonly string repo;
    const int GIU = 34;                                 // lo spazio in più per la frase su ciò che resta su GitHub
    Tuple<Cartella, FattiRepo, List<string>> visti;     // la cartella, la repo com'era quando l'hai guardata, i file che sostituisce

    public FinestraCarica(PortaScrittura p, string nomeRepo) : base(p, Lingua.T("gestione.carica_cartella"), 680, 650 + GIU) {
        repo = nomeRepo;
        Scritta(Lingua.T("gestione.carica_cartella_2", "repo", V(repo)), Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Scritta(Lingua.T("gestione.file_della_cartella_vanno_nella"), Aspetto.NORMALE, Aspetto.MUTO, S(54), S(44));
        Tolti = Scritta(Lingua.T("gestione.file_tolti_dalla_cartella_restano_su_github", "tasto", Lingua.T("gestione.file_3")), Aspetto.NORMALE, Aspetto.TESTO, S(94), S(GIU + 4));
        BScegli = Tasto(Lingua.T("gestione.scegli_cartella"), false, new Rectangle(M, S(104 + GIU), S(190), S(34)), delegate {
            using (var d = new FolderBrowserDialog { Description = Lingua.T("gestione.cartella_caricare"), ShowNewFolderButton = false })
                if (d.ShowDialog(this) == DialogResult.OK) Guarda(d.SelectedPath);
        });
        Sommario = Scritta(Lingua.T("gestione.nessuna_cartella_scelta"), Aspetto.NORMALE, Aspetto.TESTO, S(146 + GIU), S(84));
        Dettaglio = Campo("", new Rectangle(M, S(234 + GIU), Lu, S(150)), true);
        Dettaglio.ReadOnly = true; Dettaglio.AcceptsReturn = false;
        Spunta = new CheckBox { Bounds = new Rectangle(M, S(388 + GIU), Lu - S(150), S(40)), BackColor = Aspetto.FONDO, ForeColor = Aspetto.TESTO, UseMnemonic = false, Visible = false };
        Spunta.CheckedChanged += delegate { Rifai(); };
        Controls.Add(Spunta);
        BFuori = Tasto(Lingua.T("gestione.lasciali_fuori"), false, new Rectangle(M + Lu - S(140), S(390 + GIU), S(140), S(34)), delegate {
            Scrittura.LasciaFuori(Pronta);
            Spunta.Checked = false;
            Disegna();
        });
        BFuori.Visible = false;
        Scritta(Lingua.T("gestione.cosa_dire_questo_caricamento"), Aspetto.NORMALE, Aspetto.TESTO, S(432 + GIU), S(20));
        Messaggio = Campo("", new Rectangle(M, S(454 + GIU), Lu, S(26)), false);
        ChiedeNome = Scritta("", Aspetto.NORMALE, Aspetto.MUTO, S(484 + GIU), S(20));
        NomeScritto = Campo("", new Rectangle(M, S(506 + GIU), Lu, S(26)), false);
        NomeScritto.TextChanged += delegate { Rifai(); };
        ChiedeNome.Visible = NomeScritto.Visible = false;
        Riga(S(484 + GIU), S(110));
        var r = new Rectangle(M + Lu - S(170), S(600 + GIU), S(170), S(38));
        BSi = Tasto(Lingua.T("gestione.carica"), true, r, delegate { Carica(); });
        BNo = Tasto(Lingua.T("comune.chiudi"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(38)), delegate { Close(); });
        Rifai();
    }

    protected override bool Puoi(Bottone b) {
        if (b != BSi && b != BFuori) return true;
        if (Pronta == null || visti == null || Esito != null || Pronta.Bloccata) return false;
        if (b == BFuori) return Pronta.DaConfermare.Count > 0;
        return Pronta.File.Count > 0 && (Pronta.DaConfermare.Count == 0 || Spunta.Checked) && (visti.Item2.Privata || NomeScritto.Text == visti.Item2.Nome);
    }

    public void Guarda(string cartella) {
        Pronta = null; Esito = null; visti = null;
        Sommario.Text = Lingua.T("gestione.guardo_cartella"); Sommario.ForeColor = Aspetto.TESTO; Dettaglio.Text = ""; Nota("");
        Spunta.Visible = BFuori.Visible = ChiedeNome.Visible = NomeScritto.Visible = false; Spunta.Checked = false; NomeScritto.Text = "";
        Avviso.Bounds = new Rectangle(M, S(484 + GIU), Lu, S(110));
        string nome = repo;
        Lavora(c => {
            var k = Scrittura.Prepara(cartella, null);
            var f = Scrittura.Fatti(c, nome);
            return Tuple.Create(k, f, k.Bloccata ? new List<string>() : Scrittura.Sostituiti(c, f, k));
        }, x => {
            visti = x;
            Pronta = x.Item1;
            Disegna();
        });
    }

    // quello che si vede della cartella: si rifà quando si lasciano fuori gli archivi
    void Disegna() {
        var k = visti.Item1; var f = visti.Item2;
        var dentro = new HashSet<string>(k.File.Select(z => z.Percorso));
        var so = visti.Item3.Where(dentro.Contains).ToList();
        Sommario.ForeColor = f.Privata ? Aspetto.TESTO : Aspetto.AMBRA;
        Sommario.Text = Lingua.T("gestione.kb", "parte", (f.Privata ? Lingua.T("gestione.repo_privata_vedi_tu_chi") : Lingua.T("gestione.repo_pubblica_quello_che_carichi")), "parte2", (P.Login != null ? Lingua.T("gestione.agisci_come_2", "login", V(P.Login)) : ""), "file", Lingua.N("pubblica.n_file", k.File.Count), "parte3", (k.Peso / 1024 + 1), "parte4", (k.Bloccata ? "." : so.Count == 0 ? Lingua.T("gestione.tutti_nuovi") : so.Count == 1 ? Lingua.T("gestione.1_sostituisce_file_che_c") : Lingua.T("gestione.sostituiscono_file_che_ci_sono", "n", so.Count)), "parte5", (k.Saltati.Count > 0 ? Lingua.N("gestione.saltati", k.Saltati.Count) : ""));
        if (k.Bloccata) {
            Nota(Lingua.T("gestione.mi_fermo_non_carico_niente"));
            Dettaglio.Text = string.Join("\r\n", k.Riscontri.Select(z => V(z.ToString())).Concat(k.Rifiutati.Select(V)));
            return;
        }
        var t = new List<string>();
        Action<string, IEnumerable<string>> sezione = (titolo, l) => {
            var e = l.ToList();
            if (e.Count == 0) return;
            t.Add(titolo);
            t.AddRange(e.Take(200).Select(z => "  " + V(z)));
            if (e.Count > 200) t.Add(Lingua.T("gestione.altri_2", "parte", (e.Count - 200)));
            t.Add("");
        };
        sezione(Lingua.T("gestione.sostituiti_quello_che_c_ora"), so);
        sezione(Lingua.T("gestione.confermare_archivi_basi_dati_dentro"), k.DaConfermare);
        sezione(Lingua.T("gestione.non_leggibili_documenti_immagini_dentr"), k.NonLeggibili);
        sezione(Lingua.T("gestione.guardare_non_ferma_ma_contengono"), k.Avvisi.Select(z => z.ToString()));
        sezione(Lingua.T("gestione.caricare"), k.File.Select(z => z.Percorso));
        Dettaglio.Text = string.Join("\r\n", t);
        Spunta.Visible = BFuori.Visible = k.DaConfermare.Count > 0;
        if (k.DaConfermare.Count > 0) Spunta.Text = Lingua.T("gestione.li_carico_anche_senza_guardarci", "elenco", string.Join(", ", k.DaConfermare.Take(4).Select(V)), "parte", (k.DaConfermare.Count > 4 ? Lingua.T("comune.altri", "parte", (k.DaConfermare.Count - 4)) : ""));
        ChiedeNome.Visible = NomeScritto.Visible = !f.Privata;
        ChiedeNome.Text = Lingua.T("gestione.repo_pubblica_caricare_scrivi", "nome", V(f.Nome));
        Avviso.Bounds = f.Privata ? new Rectangle(M, S(484 + GIU), Lu, S(110)) : new Rectangle(M, S(536 + GIU), Lu, S(58));
        Rifai();
    }

    void Carica() {
        var k = Pronta; var f = visti.Item2; string m = Messaggio.Text.Trim(), scritto = NomeScritto.Text;
        if (Spunta.Checked) foreach (var x in k.DaConfermare) k.Confermati.Add(x);
        Nota(Lingua.T("gestione.carico"));
        Lavora(c => Scrittura.CaricaCartella(c, f, k, m, (i, n) => InUi(() => Nota(Lingua.T("gestione.mando_file", "i", i, "n", n))), P.Dorme, scritto), e => {
            Esito = e;
            Bene(Lingua.N("gestione.caricati", e.File) + (e.EraVuota && e.Passi > 1 ? Lingua.T("gestione.repo_era_vuota_github_ha") : "."));
        });
    }
}

// i file uno per uno
public class FinestraFile : Modulo {
    public ListView Righe;
    public Bottone BNuovo, BModifica, BCancella, BChiudi;
    readonly string repo;
    FattiRepo fatti;

    public FinestraFile(PortaScrittura p, string nomeRepo) : base(p, Lingua.T("gestione.file"), 680, 540) {
        repo = nomeRepo;
        Scritta(Lingua.T("gestione.file_2", "repo", V(repo)), Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Righe = Lista(new Rectangle(M, S(56), Lu, S(340)), new[] { Lingua.T("gestione.file"), Lingua.T("gestione.peso") }, new[] { 520, 90 });
        Riga(S(402), S(66));
        int y = S(478);
        BNuovo = Tasto(Lingua.T("gestione.nuovo_file"), false, new Rectangle(M, y, S(120), S(38)), delegate { Apri(null); });
        BModifica = Tasto(Lingua.T("gestione.modifica"), true, new Rectangle(M + S(130), y, S(120), S(38)), delegate { Apri(Scelto()); });
        BCancella = Tasto(Lingua.T("gestione.cancella"), false, new Rectangle(M + S(260), y, S(120), S(38)), delegate { Cancella(); });
        BChiudi = Tasto(Lingua.T("comune.chiudi"), false, new Rectangle(M + Lu - S(110), y, S(110), S(38)), delegate { Close(); });
        Shown += delegate { Rileggi(); };
        Rifai();
    }

    public string Scelto() { return Righe.SelectedItems.Count == 0 ? null : (string)Righe.SelectedItems[0].Tag; }

    protected override bool Puoi(Bottone b) {
        if (b == BChiudi) return true;
        if (fatti == null) return false;
        if (b == BNuovo) return true;
        return Scelto() != null && !Scrittura.Workflow(Scelto());
    }

    protected override void Spiega() { if (!Lavorando && Scelto() != null && Scrittura.Workflow(Scelto())) Nota(Frasi.WORKFLOW); else if (Avviso.Text == Frasi.WORKFLOW) Nota(""); }

    void Rileggi() {
        string nome = repo;
        Lavora(c => { var f = Scrittura.Fatti(c, nome); return new KeyValuePair<FattiRepo, List<FileRepo>>(f, Scrittura.ElencoFile(c, f)); }, x => {
            fatti = x.Key;
            Righe.Items.Clear();
            foreach (var r in x.Value) Righe.Items.Add(new ListViewItem(new[] { V(r.Percorso), r.Peso < 1024 ? Lingua.T("gestione.b", "peso", r.Peso) : Lingua.T("gestione.kb_2", "peso", r.Peso / 1024) }) { Tag = r.Percorso });
            if (x.Value.Count == 0) Nota(Lingua.T("gestione.repo_vuota_crea_file_oppure"));
        });
    }

    void Apri(string percorso) {
        using (var e = new EditorFile(P, repo, percorso)) if (P.Apri(e) == DialogResult.OK) { Bene(Lingua.T("gestione.salvato_github")); Rileggi(); }
    }

    void Cancella() {
        string x = Scelto();
        FileRepo letto = null;
        using (var k = new Conferma(P, repo, Lingua.T("gestione.cancella_file"), Lingua.T("gestione.cancella_file_2"), false, true,
            (c, f) => { letto = Scrittura.LeggiFile(c, f, x); return Lingua.T("gestione.cancello_dal_ramo_resta_nella", "testo", V(x), "ramo", V(f.Ramo)); },
            (c, f, s) => Scrittura.CancellaFile(c, f, x, letto.Sha, null)))
            if (P.Apri(k) == DialogResult.OK) { Bene(Lingua.T("gestione.cancellato")); Rileggi(); }
    }
}

public class EditorFile : Modulo {
    public TextBox Percorso, Testo;
    public Bottone BSalva, BNo;
    public Label Testata;
    readonly string repo, percorso;
    string sha, fineRiga = "\n";
    bool bom;
    FattiRepo visti;
    public bool Pronto;

    public EditorFile(PortaScrittura p, string nomeRepo, string quale) : base(p, quale == null ? Lingua.T("gestione.nuovo_file") : Lingua.T("gestione.modifica_file"), 760, 600) {
        repo = nomeRepo; percorso = quale;
        string inizio = quale == null ? Lingua.T("gestione.nuovo_file_dove_va_come") : Lingua.T("gestione.modifica_questo_file");
        Testata = Scritta(inizio, Aspetto.NORMALE, Aspetto.MUTO, S(14), S(20));
        Percorso = Campo(quale ?? "", new Rectangle(M, S(36), Lu, S(26)), false);
        Percorso.ReadOnly = quale != null;
        Percorso.TextChanged += delegate { Rifai(); };
        Testo = Campo("", new Rectangle(M, S(70), Lu, S(400)), true);
        Testo.Font = new Font("Consolas", 9.5f); Testo.AcceptsTab = true; Testo.WordWrap = false; Testo.ScrollBars = ScrollBars.Both; Testo.MaxLength = 0;
        Riga(S(476), S(66));
        var r = new Rectangle(M + Lu - S(170), S(548), S(170), S(38));
        BSalva = Tasto(Lingua.T("gestione.salva_github"), true, r, delegate {
            string x = Percorso.Text.Trim(), t = Testo.Text.Replace("\r\n", "\n"), s = sha; var f = visti; bool b = bom;
            if (fineRiga == "\r\n") t = t.Replace("\n", "\r\n");
            Lavora<bool>(c => { Scrittura.ScriviFile(c, f, x, t, s, null, b); return true; }, v => { Cambiato = true; DialogResult = DialogResult.OK; });
        });
        BNo = Tasto(Lingua.T("comune.annulla"), false, new Rectangle(r.X - S(120), r.Y, S(110), S(38)), delegate { Close(); });
        Shown += delegate {
            if (percorso == null) Percorso.Focus();
            string nome = repo, qualeFile = percorso;
            Lavora(c => { var f = Scrittura.Fatti(c, nome); return new KeyValuePair<FattiRepo, FileRepo>(f, qualeFile == null ? null : Scrittura.LeggiFile(c, f, qualeFile)); }, x => {
                visti = x.Key;
                Testata.Text = inizio + ". " + (visti.Privata ? Lingua.T("gestione.repo_privata") : Lingua.T("gestione.repo_pubblica_vede_chiunque")) + (P.Login != null ? Lingua.T("gestione.agisci_come_2", "login", V(P.Login)) : "");
                if (!visti.Privata) Testata.ForeColor = Aspetto.AMBRA;
                if (x.Value != null) { sha = x.Value.Sha; bom = x.Value.Bom; fineRiga = x.Value.FineRiga; Testo.Text = x.Value.Testo.Replace("\r\n", "\n").Replace("\n", "\r\n"); }
                Pronto = true;
            });
        };
        Rifai();
    }

    protected override bool Puoi(Bottone b) { return b == BNo || (Pronto && Percorso.Text.Trim().Length > 0 && !Scrittura.Workflow(Percorso.Text.Trim())); }
    protected override void Spiega() { if (Scrittura.Workflow(Percorso.Text.Trim())) Nota(Frasi.WORKFLOW); else if (Avviso.Text == Frasi.WORKFLOW) Nota(""); }
}

// le release. Una nuova nasce bozza, si allega l'exe, poi si pubblica: così l'ultima versione non resta mai senza exe
public class FinestraRelease : Modulo {
    public ListView Righe;
    public Bottone BNuova, BModifica, BAllega, BUltima, BPubblica, BCancella, BChiudi;
    readonly string repo;
    FattiRepo fatti;

    public FinestraRelease(PortaScrittura p, string nomeRepo) : base(p, Lingua.T("gestione.release"), 760, 540) {
        repo = nomeRepo;
        Scritta(Lingua.T("gestione.release_2", "repo", V(repo)), Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Scritta(Lingua.T("gestione.release_versione_pronta_scaricare_aggi"), Aspetto.NORMALE, Aspetto.MUTO, S(54), S(44));
        Righe = Lista(new Rectangle(M, S(102), Lu, S(250)), new[] { Lingua.T("gestione.tag"), Lingua.T("comune.titolo"), Lingua.T("comune.stato"), Lingua.T("gestione.allegati") }, new[] { 110, 300, 130, 150 });
        Riga(S(358), S(64));
        int y = S(430), y2 = S(476);
        BNuova = Tasto(Lingua.T("gestione.nuova_bozza"), true, new Rectangle(M, y, S(140), S(36)), delegate { Nuova(); });
        BModifica = Tasto(Lingua.T("gestione.modifica"), false, new Rectangle(M + S(150), y, S(110), S(36)), delegate { Modifica(); });
        BAllega = Tasto(Lingua.T("gestione.allega_file"), false, new Rectangle(M + S(270), y, S(140), S(36)), delegate {
            using (var d = new OpenFileDialog { Title = Lingua.T("gestione.file_allegare") }) if (d.ShowDialog(this) == DialogResult.OK) Allega(Scelta().Id, d.FileName);
        });
        BCancella = Rosso(Lingua.T("gestione.cancella"), new Rectangle(M + Lu - S(110), y, S(110), S(36)), delegate { Cancella(); });
        BUltima = Tasto(Lingua.T("gestione.pubblica_come_ultima_versione"), false, new Rectangle(M, y2, S(260), S(36)), delegate { Pubblica(true); });
        BPubblica = Tasto(Lingua.T("gestione.pubblica_ma_non_come_ultima"), false, new Rectangle(M + S(270), y2, S(250), S(36)), delegate { Pubblica(false); });
        BChiudi = Tasto(Lingua.T("comune.chiudi"), false, new Rectangle(M + Lu - S(110), y2, S(110), S(36)), delegate { Close(); });
        Shown += delegate { Rileggi(); };
        Rifai();
    }

    public ReleaseInfo Scelta() { return Righe.SelectedItems.Count == 0 ? null : (ReleaseInfo)Righe.SelectedItems[0].Tag; }

    protected override bool Puoi(Bottone b) {
        if (b == BChiudi) return true;
        if (fatti == null) return false;
        if (b == BNuova) return true;
        var r = Scelta();
        if (r == null) return false;
        return (b != BUltima && b != BPubblica) || r.Bozza;
    }

    void Rileggi() {
        string nome = repo;
        Lavora(c => { var f = Scrittura.Fatti(c, nome); return new KeyValuePair<FattiRepo, List<ReleaseInfo>>(f, Scrittura.Release(c, f)); }, x => {
            fatti = x.Key;
            Righe.Items.Clear();
            foreach (var r in x.Value) Righe.Items.Add(new ListViewItem(new[] { V(r.Tag), V(r.Titolo), r.Bozza ? Lingua.T("gestione.bozza") : r.Anteprima ? Lingua.T("gestione.anteprima") : Lingua.T("gestione.pubblicata"), r.Allegati.Count == 0 ? Lingua.T("comune.nessuno") : V(string.Join(", ", r.Allegati.Select(a => a.Nome))) }) { Tag = r });
        });
    }

    public void Allega(long id, string file) {
        var f = fatti;
        Nota(Lingua.T("gestione.mando_file_github"));
        Lavora<bool>(c => { Scrittura.Allega(c, f, id, file); return true; }, x => { Bene(Lingua.T("gestione.allegato")); Rileggi(); });
    }

    void Nuova() {
        using (var m = new ModuloRelease(P, null)) {
            if (P.Apri(m) != DialogResult.OK) return;
            string tag = m.NomeTag.Text.Trim(), ti = m.Titolo.Text.Trim(), no = m.Note.Text, fi = m.Allegato;
            var f = fatti;
            Lavora<string>(c => {
                var r = Scrittura.CreaBozza(c, f, tag, ti, no);
                if (fi != null) try { Scrittura.Allega(c, f, r.Id, fi); } catch (ErroreScrittura e) { return Lingua.T("gestione.bozza_c_ma_l_allegato", "errore", Frase(e)); }
                return null;
            }, e => { Cambiato = true; if (e != null) Nota(e); else Bene(Lingua.T("gestione.bozza_creata_quando_pronta_pubblicala")); Rileggi(); });
        }
    }

    void Modifica() {
        var r = Scelta();
        using (var m = new ModuloRelease(P, r)) {
            if (P.Apri(m) != DialogResult.OK) return;
            string tag = m.NomeTag.Text.Trim(), ti = m.Titolo.Text.Trim(), no = m.Note.Text;
            var visti = fatti;
            if (r.Bozza) { Lavora<bool>(c => { Scrittura.ModificaRelease(c, visti, r.Id, tag, ti, no, null); return true; }, x => { Bene(Lingua.T("gestione.salvato")); Rileggi(); }); return; }
            using (var k = new Conferma(P, repo, Lingua.T("gestione.modifica_release_pubblicata"), Lingua.T("gestione.salva_modifiche"), true, false,
                (c, f) => Lingua.T("gestione.chi_scarica_github_vede_subito", "tag", V(Scrittura.UnaRelease(c, f, r.Id).Tag)),
                (c, f, s) => Scrittura.ModificaRelease(c, f, r.Id, tag, ti, no, s)))
                if (P.Apri(k) == DialogResult.OK) { Bene(Lingua.T("gestione.salvato")); Rileggi(); }
        }
    }

    void Pubblica(bool ultima) {
        var r = Scelta();
        Conferma k = null;
        k = new Conferma(P, repo, ultima ? Lingua.T("gestione.pubblica_come_ultima_versione") : Lingua.T("gestione.pubblica_release"), Lingua.T("gestione.pubblica_tasto"), true, false, (c, f) => {
            var ora = Scrittura.UnaRelease(c, f, r.Id); string u = Scrittura.Ultima(c, f);
            return Lingua.T("gestione.release_allegati_ultima_versione_adess", "tag", V(ora.Tag), "parte", (ora.Allegati.Count == 0 ? Lingua.T("gestione.nessuno_chi_scarica_non_trova") : V(string.Join(", ", ora.Allegati.Select(a => a.Nome)))), "parte2", (u.Length == 0 ? Lingua.T("comune.nessuna") : V(u)), "parte3", (ultima ? Lingua.T("gestione.questa_aggiornamenti_automatici_dei_pr") : Lingua.T("gestione.resta_quella")));
        }, (c, f, s) => Scrittura.PubblicaConRiprova(c, f, s, r.Id, ultima, t => k.Passo(t), () => k.Controlla())) { Riprova = true };
        using (k) if (P.Apri(k) == DialogResult.OK) { Bene(Lingua.T("gestione.pubblicata_2")); Rileggi(); }
    }

    void Cancella() {
        var r = Scelta();
        using (var k = new Conferma(P, repo, Lingua.T("gestione.cancella_release"), Lingua.T("gestione.cancella_release_2"), true, true, (c, f) => {
            var ora = Scrittura.UnaRelease(c, f, r.Id);
            return Lingua.T("gestione.cancello_release_suoi_allegati_tag", "tag", V(ora.Tag), "allegati", ora.Allegati.Count);
        }, (c, f, s) => Scrittura.CancellaRelease(c, f, s, r.Id)))
            if (P.Apri(k) == DialogResult.OK) { Bene(Lingua.T("gestione.cancellata")); Rileggi(); }
    }
}

public class ModuloRelease : Modulo {
    public TextBox NomeTag, Titolo, Note;
    public Bottone BSi, BNo, BFile;
    public string Allegato;
    public Label LFile;

    public ModuloRelease(PortaScrittura p, ReleaseInfo r) : base(p, r == null ? Lingua.T("gestione.nuova_bozza_release") : Lingua.T("gestione.modifica_release"), 620, 520) {
        Scritta(r == null ? Lingua.T("gestione.nuova_bozza_release") : Lingua.T("gestione.modifica_release"), Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Scritta(Lingua.T("gestione.tag_segnalibro_della_versione_esempio"), Aspetto.NORMALE, Aspetto.MUTO, S(56), S(20));
        NomeTag = Campo(r == null ? "" : r.Tag, new Rectangle(M, S(78), S(200), S(26)), false);
        NomeTag.TextChanged += delegate { Rifai(); };
        Scritta(Lingua.T("comune.titolo"), Aspetto.NORMALE, Aspetto.MUTO, S(110), S(20));
        Titolo = Campo(r == null ? "" : r.Titolo, new Rectangle(M, S(132), Lu, S(26)), false);
        Scritta(Lingua.T("gestione.note_cosa_c_nuovo"), Aspetto.NORMALE, Aspetto.MUTO, S(164), S(20));
        Note = Campo(r == null ? "" : (r.Note ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"), new Rectangle(M, S(186), Lu, S(180)), true);
        if (r == null) {
            BFile = Tasto(Lingua.T("gestione.allega_l_exe"), false, new Rectangle(M, S(376), S(150), S(34)), delegate {
                using (var d = new OpenFileDialog { Title = Lingua.T("gestione.file_allegare") }) if (d.ShowDialog(this) == DialogResult.OK) { Allegato = d.FileName; LFile.Text = Path.GetFileName(Allegato); }
            });
            LFile = new Label { Text = Lingua.T("gestione.nessun_allegato"), ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Bounds = new Rectangle(M + S(160), S(382), Lu - S(160), S(22)), UseMnemonic = false };
            Controls.Add(LFile);
        }
        Riga(S(416), S(40));
        var q = new Rectangle(M + Lu - S(170), S(466), S(170), S(38));
        BSi = Tasto(r == null ? Lingua.T("gestione.crea_bozza") : Lingua.T("gestione.continua"), true, q, delegate { DialogResult = DialogResult.OK; });
        BNo = Tasto(Lingua.T("comune.annulla"), false, new Rectangle(q.X - S(120), q.Y, S(110), S(38)), delegate { Close(); });
        Rifai();
    }

    protected override bool Puoi(Bottone b) { return b != BSi || NomeTag.Text.Trim().Length > 0; }
}

// le persone della repo: quelle aggiunte a mano e gli inviti ancora aperti
public class FinestraCollaboratori : Modulo {
    public ListView Righe;
    public TextBox NomeNuovo;
    public ComboBox Ruolo;
    public Label NotaRuolo;
    public Bottone BAggiungi, BCambia, BTogli, BChiudi;
    public FattiRepo Fatti;
    readonly string repo;
    static string[] PAROLE { get { return new[] { Lingua.T("gestione.legge"), Lingua.T("gestione.legge_ordina_segnalazioni"), Lingua.T("gestione.scrive"), Lingua.T("gestione.gestisce"), Lingua.T("gestione.amministra") }; } }

    public FinestraCollaboratori(PortaScrittura p, string nomeRepo) : base(p, Lingua.T("comune.collaboratori"), 680, 560) {
        repo = nomeRepo;
        Scritta(Lingua.T("gestione.chi_lavora", "repo", V(repo)), Aspetto.GRANDE, Aspetto.TESTO, S(16), S(34));
        Righe = Lista(new Rectangle(M, S(56), Lu, S(230)), new[] { Lingua.T("comune.account"), Lingua.T("gestione.ruolo"), Lingua.T("comune.stato") }, new[] { 280, 180, 150 });
        Scritta(Lingua.T("gestione.aggiungi_nome_dell_account_github"), Aspetto.NORMALE, Aspetto.MUTO, S(296), S(20));
        NomeNuovo = Campo("", new Rectangle(M, S(318), S(280), S(26)), false);
        NomeNuovo.TextChanged += delegate { Rifai(); };
        Ruolo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(M + S(290), S(318), Lu - S(290), S(28)) };
        Ruolo.Items.AddRange(PAROLE);
        Ruolo.SelectedIndex = 2;
        Controls.Add(Ruolo);
        NotaRuolo = Scritta("", Aspetto.PICCOLO, Aspetto.AMBRA, S(350), S(36));
        Riga(S(390), S(64));
        int y = S(506);
        BAggiungi = Tasto(Lingua.T("gestione.aggiungi"), true, new Rectangle(M, y, S(120), S(38)), delegate { Aggiungi(); });
        BCambia = Tasto(Lingua.T("gestione.cambia_ruolo"), false, new Rectangle(M + S(130), y, S(130), S(38)), delegate { Cambia(); });
        BTogli = Rosso(Lingua.T("gestione.togli"), new Rectangle(M + S(270), y, S(110), S(38)), delegate { Togli(); });
        BChiudi = Tasto(Lingua.T("comune.chiudi"), false, new Rectangle(M + Lu - S(110), y, S(110), S(38)), delegate { Close(); });
        Shown += delegate { Rileggi(); };
        Rifai();
    }

    public PersonaRepo Scelta() { return Righe.SelectedItems.Count == 0 ? null : (PersonaRepo)Righe.SelectedItems[0].Tag; }
    string RuoloScelto() { return Scrittura.RUOLI[Math.Max(0, Ruolo.SelectedIndex)]; }

    static string Parola(string r) {
        switch (r) {
            case "read": case "pull": return PAROLE[0];
            case "triage": return PAROLE[1];
            case "write": case "push": return PAROLE[2];
            case "maintain": return PAROLE[3];
            case "admin": return PAROLE[4];
            default: return V(r);
        }
    }

    protected override bool Puoi(Bottone b) {
        if (b == BChiudi) return true;
        if (Fatti == null) return false;
        if (b == BAggiungi) return Scrittura.LoginValido(NomeNuovo.Text.Trim());
        var s = Scelta();
        if (s == null) return false;
        return b != BCambia || (Fatti.Organizzazione && s.Invito == 0);
    }

    void Rileggi() {
        string nome = repo;
        Lavora(c => { var f = Scrittura.Fatti(c, nome); Diritti.Consenti(Azione.Collaboratori, f); return new KeyValuePair<FattiRepo, List<PersonaRepo>>(f, Scrittura.Collaboratori(c, f)); }, x => {
            Fatti = x.Key;
            Righe.Items.Clear();
            foreach (var r in x.Value) Righe.Items.Add(new ListViewItem(new[] { V(r.Nome), Parola(r.Ruolo), r.Invito != 0 ? Lingua.T("gestione.invito_accettare") : Lingua.T("gestione.attivo") }) { Tag = r });
            Ruolo.Enabled = Fatti.Organizzazione;
            NotaRuolo.Text = Fatti.Organizzazione ? Lingua.T("gestione.chi_ha_accesso_perche_nell") : Lingua.T("gestione.repo_personale_github_ha_ruolo");
        });
    }

    void Chiedi(string titolo, string tasto, bool grave, Func<Cliente, FattiRepo, string> leggi, Action<Cliente, FattiRepo, string> fa, string fatto) {
        using (var k = new Conferma(P, repo, titolo, tasto, true, grave, leggi, fa))
            if (P.Apri(k) == DialogResult.OK) { Bene(fatto); Rileggi(); }
    }

    void Aggiungi() {
        string chi = NomeNuovo.Text.Trim(), ruolo = RuoloScelto();
        bool invito = false;
        Chiedi(Lingua.T("gestione.aggiungi_persona"), Lingua.T("gestione.aggiungi"), false, (c, f) => {
            string tipo = Scrittura.TipoAccount(c, chi);
            return Lingua.T("gestione.aggiungo", "chi", V(chi), "parte", (tipo == "Organization" ? Lingua.T("gestione.che_organizzazione") : ""), "parte2", (f.Organizzazione ? Lingua.T("gestione.col_ruolo", "ruolo", Parola(ruolo)) : Lingua.T("gestione.potra_leggere_scrivere")), "parte3", (f.Privata ? Lingua.T("gestione.vedra_tutto_quello_che_c") : ""));
        }, (c, f, s) => invito = Scrittura.AggiungiCollaboratore(c, f, s, chi, ruolo), Lingua.T("gestione.fatto"));
        if (invito) { Bene(Lingua.T("gestione.invito_mandato_entra_quando_accetta")); NomeNuovo.Text = ""; }
    }

    void Cambia() {
        var x = Scelta(); string ruolo = RuoloScelto();
        Chiedi(Lingua.T("gestione.cambia_ruolo_2"), Lingua.T("gestione.cambia_ruolo"), false, (c, f) => Lingua.T("gestione.adesso_dopo", "nome", V(x.Nome), "nome2", Parola(Scrittura.RuoloDi(c, f, x.Nome)), "ruolo", Parola(ruolo)), (c, f, s) => Scrittura.CambiaRuolo(c, f, s, x.Nome, ruolo), Lingua.T("gestione.ruolo_cambiato"));
    }

    void Togli() {
        var x = Scelta();
        if (x.Invito != 0) Chiedi(Lingua.T("gestione.ritira_l_invito"), Lingua.T("gestione.ritira_l_invito"), true, (c, f) => Lingua.T("gestione.ritiro_l_invito", "nome", V(x.Nome)), (c, f, s) => Scrittura.RitiraInvito(c, f, s, x.Invito), Lingua.T("gestione.invito_ritirato"));
        else Chiedi(Lingua.T("gestione.togli_persona"), Lingua.T("gestione.togli"), true, (c, f) => Lingua.T("gestione.non_potra_piu_lavorare_questa", "nome", V(x.Nome)), (c, f, s) => Scrittura.TogliCollaboratore(c, f, s, x.Nome), Lingua.T("gestione.tolto"));
    }
}

// cancellare una repo: nome scritto, poi un login apposta col permesso di cancellare. Quella chiave non si salva: si usa qui e si butta
public class FinestraCancellaRepo : Modulo {
    public TextBox Nome;
    public Label Racconto, Codice;
    public Bottone BAvanti, BApri, BRitira, BNo;
    public bool Cancellata, InAttesa;
    public FattiRepo Visti;
    public CodiceDispositivo Dispositivo;
    readonly string repo;
    volatile bool annullato;

    public FinestraCancellaRepo(PortaScrittura p, string nomeRepo) : base(p, Lingua.T("gestione.cancella_repo"), 620, 500) {
        repo = nomeRepo;
        Scritta(Lingua.T("gestione.cancella_repo"), Aspetto.GRANDE, Aspetto.ROSSO, S(16), S(34));
        Racconto = Scritta(Lingua.T("gestione.rileggo_github"), Aspetto.NORMALE, Aspetto.TESTO, S(56), S(170));
        Scritta(Lingua.T("gestione.confermare_scrivi_nome_completo_della"), Aspetto.NORMALE, Aspetto.MUTO, S(232), S(20));
        Nome = Campo("", new Rectangle(M, S(254), Lu, S(26)), false);
        Nome.TextChanged += delegate { Rifai(); };
        Codice = new Label { Font = new Font("Consolas", 24f, FontStyle.Bold), ForeColor = Aspetto.LIME, BackColor = Aspetto.FONDO, Bounds = new Rectangle(M, S(286), Lu, S(48)), TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false };
        Controls.Add(Codice);
        Riga(S(338), S(92));
        int y = S(442);
        BAvanti = Rosso(Lingua.T("gestione.autorizza_cancella"), new Rectangle(M + Lu - S(200), y, S(200), S(38)), delegate { Avanti(); });
        BApri = Tasto(Lingua.T("comune.copia_codice_apri_github"), true, new Rectangle(M + Lu - S(270), y, S(270), S(38)), delegate { P.Porta.Copia(Dispositivo.PerUtente); P.Porta.Apri(Accessi.PAGINA); });
        BRitira = Tasto(Lingua.T("gestione.apri_pagina_ritirarla"), true, new Rectangle(M + Lu - S(270), y, S(270), S(38)), delegate { P.Porta.Apri(Accessi.PAGINA_RITIRA); });
        BNo = Tasto(Lingua.T("comune.annulla"), false, new Rectangle(M, y, S(120), S(38)), delegate { annullato = true; Close(); });
        BApri.Visible = BRitira.Visible = false;
        Shown += delegate {
            Nome.Focus();
            Lavora(c => Scrittura.Fatti(c, repo), f => {
                Visti = f;
                Racconto.Text = Lingua.T("gestione.cancello_repo_tutti_file_storia", "f", V(Dice(f)), "parte", (f.Stelle + f.Copie > 0 ? Lingua.T("gestione.ha_altre_persone_usano", "f", Stelle(f), "f2", Copie(f)) : ""));
            });
        };
        FormClosed += delegate { annullato = true; };
        Rifai();
    }

    protected override bool Puoi(Bottone b) {
        if (b == BNo || b == BApri || b == BRitira) return true;
        return Visti != null && !InAttesa && !Cancellata && Nome.Text == Visti.Nome;
    }

    void Avanti() {
        string scritto = Nome.Text, chi = P.Login; var visti = Visti;
        Nota("");
        Lavora(c => { Scrittura.Fatti(c, repo); return P.ChiediCancella(); }, k => {
            Dispositivo = k; InAttesa = true;
            Nome.ReadOnly = true;
            Codice.Text = k.PerUtente;
            BAvanti.Visible = false; BApri.Visible = true;
            Nota(Lingua.T("gestione.scrivi_questo_codice_github_autorizza"));
            new Thread(() => {
                string token = null; Exception x = null;
                try { token = P.AspettaCancella(k, () => annullato); } catch (Exception e) { x = e; }
                InUi(() => {
                    InAttesa = false;
                    BApri.Visible = false; Codice.Text = "";
                    if (x != null || token == null) { Nome.ReadOnly = false; BAvanti.Visible = true; if (x != null) Sbaglio(x); else Nota(Lingua.T("gestione.annullato_non_ho_cancellato_niente")); Rifai(); return; }
                    var usa = chiave = P.UsaEGetta(token); token = null;
                    Nota(Lingua.T("gestione.cancello"));
                    Lavora<bool>(c => { Scrittura.CancellaRepo(c, usa, chi, visti, scritto); return true; }, v => {
                        Cancellata = true;
                        BNo.Text = Lingua.T("comune.chiudi"); BRitira.Visible = true;
                        Nota(Lingua.T("gestione.cancellata_chiave_questa_autorizzazion"));
                    });
                });
            }) { IsBackground = true }.Start();
        });
    }

    Cliente chiave;

    protected override void Sbaglio(Exception e) {
        base.Sbaglio(e);
        // non si è nemmeno arrivati a cancellare: la chiave si butta lo stesso. GitHub somma i permessi, e può rifiutare l'accesso normale
        if (chiave != null && !chiave.Buttata) {
            chiave.Butta();
            Nota(Lingua.T("gestione.non_ho_cancellato_niente_chiave", "avviso", Avviso.Text));
        }
        chiave = null;
        Nome.ReadOnly = false; BAvanti.Visible = !Cancellata; BApri.Visible = false; Codice.Text = "";
    }
}

// la scheda: ogni tasto ha accanto cosa fa, o perché adesso è spento
public class PaginaGestione : Panel {
    class Voce { public Bottone Tasto; public Label Testo; public Func<FattiRepo, string> Spento, Dice, Nome; public bool SoloConRepo = true, SoloOrg; }

    readonly PortaScrittura P;
    readonly List<Voce> voci = new List<Voce>();
    public Func<bool> Occupata = () => false;
    public event EventHandler Cambiata;
    public event Action<string, string> Rinominata;
    public event Action<string> Cancellata;
    public FattiRepo Fatti;
    public string Errore;
    public RepoInfo Repo;
    public Label Stato;
    int y, giro;
    string login;

    int S(int v) { return Aspetto.S(v); }

    public PaginaGestione(PortaScrittura p) {
        P = p;
        BackColor = Color.White; AutoScroll = true; Visible = false;
        y = S(14);
        Stato = new Label { Location = new Point(S(24), y), Size = new Size(S(800), S(44)), ForeColor = Aspetto.GRIGIO, BackColor = Color.White, UseMnemonic = false };
        Controls.Add(Stato);
        y += S(44);
        Titolo(Lingua.T("gestione.nuova"));
        var crea = Riga(Lingua.T("gestione.crea_repo"), null, Lingua.T("gestione.personale_organizzazione_nasce_sempre"), f => null, delegate { Crea(); });
        crea.SoloConRepo = false;
        Titolo(Lingua.T("gestione.questa_repo"));
        Riga(Lingua.T("comune.cambia_nome"), null, Lingua.T("gestione.link_vecchi_github_li_rimanda"), f => Diritti.Perche(Azione.Nome, f), delegate { CambiaNome(); });
        Riga(Lingua.T("gestione.cambia_descrizione"), null, Lingua.T("gestione.frase_che_si_vede_cima"), f => Diritti.Perche(Azione.Descrizione, f), delegate { CambiaDescrizione(); });
        Riga(Lingua.T("comune.cambia_sito"), null, null, f => Diritti.Perche(Azione.Descrizione, f), delegate { CambiaSito(); }).Dice = f => f.Sito.Length == 0
            ? Lingua.T("gestione.nessun_sito_link_che_si")
            : Lingua.T("gestione.adesso", "sito", Dogana.RendiVisibile(f.Sito));
        Riga(Lingua.T("gestione.visibilita"), f => f.Privata ? Lingua.T("comune.rendi_pubblica") : Lingua.T("gestione.rendi_privata"), null, f => Diritti.Perche(Azione.Visibilita, f), delegate { Visibilita(); }).Dice = f => f.Privata
            ? Lingua.T("gestione.vedra_chiunque_tutta_storia_segnalazio")
            : Lingua.T("gestione.vedranno_solo_persone_invitate_copie");
        Riga(Lingua.T("comune.archivia"), f => f.Archiviata ? Lingua.T("gestione.togli_dall_archivio") : Lingua.T("comune.archivia"), null, f => Diritti.Perche(Azione.Archivio, f), delegate { Archivia(); }).Dice = f => f.Archiviata
            ? Lingua.T("gestione.torna_essere_modificabile")
            : Lingua.T("gestione.diventa_sola_lettura_si_vede");
        Riga(Lingua.T("gestione.trasferisci"), null, Lingua.T("gestione.passa_altro_account_organizzazione_si"), f => Diritti.Perche(Azione.Trasferimento, f), delegate { Trasferisci(); });
        Titolo(Lingua.T("gestione.contenuto"));
        Riga(Lingua.T("gestione.carica_cartella"), null, Lingua.T("gestione.tutti_file_solo_salvataggio_prima"), f => Diritti.Perche(Azione.Contenuto, f), delegate { Apri(new FinestraCarica(P, Repo.Nome)); });
        Riga(Lingua.T("gestione.file_3"), null, Lingua.T("gestione.vedi_file_modificane_testo_creane"), f => Diritti.Perche(Azione.Contenuto, f), delegate { Apri(new FinestraFile(P, Repo.Nome)); });
        Titolo(Lingua.T("gestione.versioni_persone"));
        Riga(Lingua.T("gestione.release"), null, Lingua.T("gestione.crea_modifica_cancella_pubblica_versio"), f => Diritti.Perche(Azione.Release, f), delegate { Apri(new FinestraRelease(P, Repo.Nome)); });
        Riga(Lingua.T("comune.collaboratori"), null, Lingua.T("gestione.aggiungi_togli_cambia_ruolo_chi"), f => Diritti.Perche(Azione.Collaboratori, f), delegate { Apri(new FinestraCollaboratori(P, Repo.Nome)); });
        var org = Riga(Lingua.T("gestione.apri_l_organizzazione"), null, Lingua.T("gestione.membri_team_impostazioni_dell_organizz"), f => null, delegate { P.Porta.Apri("https://github.com/orgs/" + Fatti.Proprietario + "/people"); });
        org.SoloOrg = true;
        Titolo(Lingua.T("gestione.zona_pericolosa"));
        var via = Riga(Lingua.T("gestione.cancella_repo"), null, Lingua.T("gestione.sparisce_tutti_storia_release_si"), f => Diritti.Perche(Azione.Cancellazione, f), delegate { CancellaRepo(); });
        via.Tasto.Fondo = Aspetto.ROSSO; via.Tasto.Scritta = Color.White; via.Tasto.Linea = Color.Empty;
        VisibleChanged += delegate { if (Visible && Repo != null && Fatti == null) Leggi(); };
        Resize += delegate { foreach (var v in voci) v.Testo.Width = Math.Max(S(200), ClientSize.Width - v.Testo.Left - S(24)); Stato.Width = Math.Max(S(200), ClientSize.Width - S(48)); };
        Rifai();
    }

    void Titolo(string testo) {
        y += S(8);
        Controls.Add(new Label { Text = testo, Font = Aspetto.MEDIO, ForeColor = Aspetto.INCHIOSTRO, BackColor = Color.White, Location = new Point(S(24), y), Size = new Size(S(400), S(26)), UseMnemonic = false });
        y += S(30);
    }

    Voce Riga(string nome, Func<FattiRepo, string> nomeDa, string dice, Func<FattiRepo, string> spento, EventHandler clic) {
        var b = new Bottone { Text = nome, Location = new Point(S(24), y), Size = new Size(S(190), S(34)) };
        b.Click += (o, e) => { if (Occupata()) { Stato.Text = Lingua.T("comune.aspetta_che_finisca_lavoro_corso"); return; } clic(o, e); };
        var t = new Label { Text = dice, ForeColor = Aspetto.GRIGIO, BackColor = Color.White, Location = new Point(S(226), y - S(2)), Size = new Size(S(640), S(38)), UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft };
        Controls.Add(b); Controls.Add(t);
        var v = new Voce { Tasto = b, Testo = t, Spento = spento, Nome = nomeDa, Dice = dice == null ? null : (Func<FattiRepo, string>)(f => dice) };
        voci.Add(v);
        y += S(44);
        return v;
    }

    public Bottone TastoDi(string nome) { return voci.Select(v => v.Tasto).First(b => b.Text == nome); }
    public string TestoDi(Bottone b) { return voci.First(v => v.Tasto == b).Testo.Text; }

    // cambia la repo mostrata. Se è la stessa non rilegge: ogni azione rilegge comunque per conto suo
    public void Imposta(RepoInfo r, string chi) {
        bool stessa = Repo == r && login == chi;
        Repo = r; login = chi; P.Login = chi;
        if (stessa && (Fatti != null || r == null || Errore != null)) { Rifai(); return; }
        Fatti = null; Errore = null; giro++;
        Rifai();
        if (Visible && r != null && !r.Sparita) Leggi();
    }

    public void Leggi() {
        if (Repo == null || Repo.Sparita) return;
        if (Occupata()) { Stato.Text = Lingua.T("gestione.aspetto_che_finisca_lavoro_corso"); return; }
        int mio = ++giro; string nome = Repo.Nome;
        Fatti = null; Errore = null;
        Rifai();
        new Thread(() => {
            FattiRepo f = null; string e = null;
            try { f = Scrittura.Fatti(P.Strada(), nome); }
            catch (ErroreScrittura x) { e = x.Message; }
            catch (ErroreAccesso x) { e = x.Message; }
            catch (Exception x) { Dati.Log("gestione: " + x.GetType().Name); e = Lingua.T("gestione.non_riesco_leggere_questa_repo"); }
            try { BeginInvoke((Action)(() => { if (mio != giro) return; Fatti = f; Errore = e; Rifai(); })); } catch (InvalidOperationException) { }
        }) { IsBackground = true }.Start();
    }

    void Rifai() {
        SuspendLayout();
        foreach (var v in voci) {
            if (v.SoloOrg) v.Tasto.Visible = v.Testo.Visible = Fatti != null && Fatti.Organizzazione;
            if (v.Nome != null && Fatti != null) v.Tasto.Text = v.Nome(Fatti);
            string perche;
            if (!v.SoloConRepo) perche = null;
            else if (Repo == null) perche = Lingua.T("gestione.scegli_repo_sinistra");
            else if (Repo.Sparita) perche = Lingua.T("gestione.questa_repo_non_c_piu");
            else if (Fatti == null) perche = Errore != null ? Lingua.T("gestione.non_so_cosa_puoi_fare") : Lingua.T("gestione.leggo_permessi_github");
            else perche = v.Spento(Fatti);
            v.Tasto.Enabled = perche == null;
            v.Testo.Text = perche ?? (v.Dice == null ? "" : v.Dice(Fatti));
            v.Testo.ForeColor = perche != null && Repo != null ? Aspetto.AMBRA_S : Aspetto.GRIGIO;
        }
        Stato.ForeColor = Errore != null ? Aspetto.AMBRA_S : Aspetto.GRIGIO;
        Stato.Text = Repo == null ? Lingua.T("gestione.qui_crei_repo_nuova_resto")
            : Repo.Sparita ? Lingua.T("gestione.questa_repo_non_c_piu")
            : Errore ?? (Fatti == null ? Lingua.T("gestione.leggo_github_cosa_puoi_fare") : Lingua.T("gestione.github_controlla_comunque_ogni_azione", "fatti", Dogana.RendiVisibile(Modulo.Dice(Fatti))));
        ResumeLayout();
    }

    void Fine() { Leggi(); if (Cambiata != null) Cambiata(this, EventArgs.Empty); }

    void Apri(Modulo f) {
        bool cambiato;
        using (f) { P.Apri(f); cambiato = f.Cambiato; }
        if (cambiato) Fine(); else Leggi();
    }

    void Crea() {
        using (var m = new ModuloRepoNuova(P, Repo == null ? null : Repo.Proprietario)) {
            P.Apri(m);
            if (m.Cambiato && Cambiata != null) Cambiata(this, EventArgs.Empty);
        }
    }

    bool Chiedi(Conferma k) { using (k) return P.Apri(k) == DialogResult.OK; }

    void CambiaNome() {
        string vecchio = Repo.Nome, nuovo;
        using (var m = new ModuloTesto(P, Lingua.T("comune.cambia_nome"), Lingua.T("gestione.nome_nuovo_senza_proprietario_lettere"), Fatti.NomeCorto, false, Lingua.T("gestione.continua"), null) { Valido = Scrittura.NomeRepoValido }) {
            if (P.Apri(m) != DialogResult.OK) return;
            nuovo = m.Valore.Text.Trim();
        }
        string fatto = null;
        if (!Chiedi(new Conferma(P, vecchio, Lingua.T("comune.cambia_nome"), Lingua.T("comune.cambia_nome"), true, false,
            (c, f) => Lingua.T("gestione.nome_nuovo_sara_cartelle_sui", "nuovo", Dogana.RendiVisibile(f.Proprietario + "/" + nuovo)),
            (c, f, s) => fatto = Scrittura.Rinomina(c, f, s, nuovo)))) return;
        Fatti = null;
        if (Rinominata != null) Rinominata(vecchio, fatto);
    }

    void CambiaDescrizione() {
        var visti = Fatti;
        using (var m = new ModuloTesto(P, Lingua.T("gestione.cambia_descrizione"), Lingua.T("gestione.frase_che_si_vede_cima_2"), Fatti.Descrizione, true, Lingua.T("comune.salva"), (c, t) => Scrittura.CambiaDescrizione(c, visti, t)))
            if (P.Apri(m) == DialogResult.OK) Fine(); else Leggi();
    }

    public static string SPIEGA_SITO { get { return Lingua.T("gestione.sito_del_progetto_website_github"); } }

    void CambiaSito() {
        var visti = Fatti;
        using (var m = new ModuloTesto(P, Lingua.T("comune.cambia_sito"), SPIEGA_SITO, Fatti.Sito, false, Lingua.T("comune.salva"), (c, t) => Scrittura.CambiaSito(c, visti, t)) { Valido = Scrittura.SitoValido })
            if (P.Apri(m) == DialogResult.OK) Fine(); else Leggi();
    }


    public static string RaccontoPubblica(ControlloPubblica k) {
        string t = Lingua.T("pubblica.cercato", "file", Lingua.N("pubblica.n_file", k.Guardati), "rami", Lingua.N("pubblica.n_rami", k.Rami), "commit", Lingua.N("pubblica.n_commit", k.Commit), "file_storia", Lingua.N("pubblica.n_file", k.GuardatiStoria),
                "segnalazioni", Lingua.N("pubblica.n_segnalazioni", k.Issue), "pull", Lingua.N("pubblica.n_pull", k.Pull), "release", Lingua.N("pubblica.n_release", k.Release), "commenti", Lingua.N("pubblica.n_commenti", k.Commenti)) + "\n"
             + Lingua.T("pubblica.diventa_pubblico", "wiki", k.Wiki ? Lingua.T("pubblica.wiki") : "", "allegati", k.Allegati > 0 ? Lingua.T("pubblica.allegati", "quanti", Lingua.N("pubblica.n_allegati", k.Allegati)) : "");
        if (k.EmailInChiaro > 0) t += "\n" + Lingua.N("pubblica.email", k.EmailInChiaro);
        if (k.Avvisi.Count > 0) t += "\n" + Lingua.T("pubblica.da_guardare", "cosa", Dogana.RendiVisibile(k.Avvisi[0].ToString()), "altri", k.Avvisi.Count > 1 ? Lingua.T("pubblica.e_altri", "n", k.Avvisi.Count - 1) : "");
        return t;
    }

    void Visibilita() {
        string nome = Repo.Nome;
        if (Fatti.Privata) {
            ControlloPubblica k = null;
            Conferma conferma = null;
            conferma = new Conferma(P, nome, Lingua.T("comune.rendi_pubblica"), Lingua.T("comune.rendi_pubblica"), true, true, (c, f) => {
                k = Scrittura.ControllaPerPubblica(c, f, conferma.Passo);
                Scrittura.FermaSeNonPulito(k);
                return RaccontoPubblica(k);
            }, (c, f, s) => Scrittura.RendiPubblica(c, f, s, k));
            if (Chiedi(conferma)) Fine();
        } else if (Chiedi(new Conferma(P, nome, Lingua.T("gestione.rendi_privata"), Lingua.T("gestione.rendi_privata"), false, false,
            (c, f) => Lingua.T("gestione.vedranno_solo_persone_invitate_stelle"), (c, f, s) => Scrittura.RendiPrivata(c, f)))) Fine();
    }

    void Archivia() {
        bool via = !Fatti.Archiviata;
        if (Chiedi(new Conferma(P, Repo.Nome, via ? Lingua.T("comune.archivia") : Lingua.T("gestione.togli_dall_archivio"), via ? Lingua.T("comune.archivia") : Lingua.T("gestione.togli_dall_archivio"), false, false,
            (c, f) => via ? Lingua.T("comune.repo_diventa_sola_lettura_niente") : Lingua.T("gestione.repo_torna_modificabile"),
            (c, f, s) => Scrittura.Archivia(c, f, via)))) Fine();
    }

    void Trasferisci() {
        string vecchio = Repo.Nome, a;
        using (var m = new ModuloTesto(P, Lingua.T("gestione.trasferisci"), Lingua.T("gestione.nome_dell_account_dell_organizzazione"), "", false, Lingua.T("gestione.continua"), null) { Valido = Scrittura.LoginValido }) {
            if (P.Apri(m) != DialogResult.OK) return;
            a = m.Valore.Text.Trim();
        }
        string fatto = null;
        if (!Chiedi(new Conferma(P, vecchio, Lingua.T("gestione.trasferisci"), Lingua.T("gestione.trasferisci"), true, true,
            (c, f) => { string tipo = Scrittura.TipoAccount(c, a); return Lingua.T("gestione.repo_passa_riaverla_dovra_ridartela", "a", Dogana.RendiVisibile(a), "parte", (tipo == "Organization" ? Lingua.T("gestione.organizzazione") : Lingua.T("gestione.persona"))); },
            (c, f, s) => fatto = Scrittura.Trasferisci(c, f, s, a)))) return;
        Fatti = null;
        if (fatto != null && Rinominata != null) Rinominata(vecchio, fatto);
        else { Stato.Text = Lingua.T("gestione.richiesta_mandata_repo_passa_mano", "a", a); if (Cambiata != null) Cambiata(this, EventArgs.Empty); }
    }

    void CancellaRepo() {
        string nome = Repo.Nome;
        bool via;
        using (var f = new FinestraCancellaRepo(P, nome)) { P.Apri(f); via = f.Cancellata; }
        if (via) { Fatti = null; if (Cancellata != null) Cancellata(nome); }
    }
}
