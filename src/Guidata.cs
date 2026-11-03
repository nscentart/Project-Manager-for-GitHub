using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// una finestra che le prove e le foto aprono senza rubare il fuoco a chi sta lavorando
public class Dialogo : Form {
    public static bool Silenzioso;
    protected override bool ShowWithoutActivation { get { return Silenzioso; } }
}

public interface IPorta {
    bool Browser { get; }
    bool Git { get; }
    CodiceDispositivo Chiedi();
    string Aspetta(CodiceDispositivo c, Func<bool> annullato);
    string DaGit();
    string LoginDi(string token);
    List<RepoInfo> RepoDi(string token, string login);
    List<string> OrganizzazioniDi(string token, string login);
    void Custodisci(string login, string token);
    void Copia(string testo);
    void Apri(string pagina);
}

public class PortaVera : IPorta {
    public bool Browser { get { return Accessi.CLIENT_ID.Length > 0; } }
    public bool Git { get { return Accessi.TrovaGit() != null; } }
    public CodiceDispositivo Chiedi() { return Accessi.Chiedi(Accessi.CLIENT_ID, Accessi.Posta); }
    public string Aspetta(CodiceDispositivo c, Func<bool> annullato) {
        return Accessi.Aspetta(c, Accessi.CLIENT_ID, Accessi.Posta, s => { for (int i = 0; i < s * 5 && !annullato(); i++) Thread.Sleep(200); }, annullato);
    }
    public string DaGit() { return GitHub.DaGit(null); }
    public string LoginDi(string token) { GitHub.Usa(token, null); return GitHub.LoginDi(); }
    public List<RepoInfo> RepoDi(string token, string login) { GitHub.Usa(token, login); return GitHub.Repo(); }
    public List<string> OrganizzazioniDi(string token, string login) { GitHub.Usa(token, login); return GitHub.Organizzazioni(); }
    // nessuna chiave con i permessi più pericolosi resta su questo computer: GitHub può sommare a una chiave nuova quelli già dati
    public void Custodisci(string login, string token) {
        Scrittura.ControllaChiave(Cliente.Vero(token, true));
        Cassaforte.Scrivi(Cassaforte.NomeDi(login), login, token);
    }
    public void Copia(string testo) { try { Clipboard.SetText(testo); } catch (Exception ex) { Dati.Log("appunti: " + ex.Message); } }
    public void Apri(string pagina) {
        if (!pagina.StartsWith("https://github.com/")) return;
        try { Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + pagina + "\""); } catch (Exception ex) { Dati.Log("browser: " + ex.Message); }
    }
}

public class Guidata : Dialogo {
    readonly Archivio a;
    readonly IPorta porta;
    readonly Action salva;
    readonly bool prima;
    volatile int giro;   // ogni attesa ha il suo numero: una risposta di un'attesa annullata non conta più
    volatile bool chiusa;
    bool collego;   // sta leggendo con la chiave nuova: GitHub ha un accesso solo, la finestra non può chiudersi a metà
    CodiceDispositivo codice;
    List<RepoInfo> trovate = new List<RepoInfo>();
    HashSet<string> spunte = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool riempio;

    public string Collegato;   // l'account collegato qui, nullo se nessuno
    public Panel PInizio, PCodice, PRepo;
    public Bottone BBrowser, BGit, BPiuTardi, BApri, BIndietro, BAvanti, BTutte, BNessuna;
    public ListView Lista;
    public Label Avviso;
    Label lCodice, lAttesa, lChi, lConti, lOrg;
    ComboBox cFiltro;

    int S(int v) { return Aspetto.S(v); }

    Label Scritta(Control dove, string testo, Font f, Color c, int y, int alt) {
        var l = new Label { Text = testo, Font = f, ForeColor = c, BackColor = Aspetto.FONDO, Bounds = new Rectangle(S(28), y, S(584), alt), UseMnemonic = false };
        dove.Controls.Add(l);
        return l;
    }

    Bottone Tasto(Control dove, string testo, bool primario, Rectangle r, EventHandler clic) {
        var b = new Bottone { Text = testo, Bounds = r, Fondo = primario ? Aspetto.AZZURRO : Aspetto.CAPSULA, Scritta = primario ? Color.White : Aspetto.TESTO, Linea = primario ? Color.Empty : Aspetto.RIGA };
        b.Click += clic;
        dove.Controls.Add(b);
        return b;
    }

    Panel Pagina() {
        var p = new Panel { Bounds = new Rectangle(0, 0, S(640), S(460)), BackColor = Aspetto.FONDO, Visible = false };
        Controls.Add(p);
        return p;
    }

    public Guidata(Archivio archivio, IPorta p, bool primaVolta, Action salvare) {
        a = archivio; porta = p; prima = primaVolta; salva = salvare;
        using (var g = CreateGraphics()) Aspetto.K = g.DpiX / 96f;
        Text = primaVolta ? Lingua.T("guidata.project_manager_prima_apertura") : Lingua.T("comune.collega_altro_account");
        ClientSize = new Size(S(640), S(520));
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        try { Icon = Aspetto.Icona(); } catch { }

        Avviso = new Label { Bounds = new Rectangle(S(28), S(404), S(584), S(44)), ForeColor = Aspetto.AMBRA, BackColor = Aspetto.FONDO, UseMnemonic = false };
        Controls.Add(Avviso);
        int yb = S(464), hb = S(38), destra = S(612);

        // ---------- passo 1: come si entra ----------
        PInizio = Pagina();
        Scritta(PInizio, Lingua.T("guidata.passo_1_2"), Aspetto.PICCOLO, Aspetto.MUTO, S(20), S(20));
        Scritta(PInizio, primaVolta ? Lingua.T("guidata.colleghiamo_tuo_account_github") : Lingua.T("guidata.collega_altro_account_github"), Aspetto.GRANDE, Aspetto.TESTO, S(40), S(34));
        string testo = Lingua.T("guidata.programma_legge_tue_repo_modifica", "parte", (primaVolta
              ? Lingua.T("guidata.ti_accompagno_due_passi_1")
              : Lingua.T("guidata.puoi_collegare_quanti_account_vuoi")));
        Scritta(PInizio, testo, Aspetto.NORMALE, Aspetto.TESTO, S(84), S(222));
        var manca = porta.Browser ? null : Scritta(PInizio, porta.Git
            ? Lingua.T("guidata.login_col_browser_non_ancora")
            : Lingua.T("guidata.login_col_browser_non_ancora_2"), Aspetto.NORMALE, Aspetto.AMBRA, S(308), S(44));
        BBrowser = Tasto(PInizio, Lingua.T("guidata.accedi_github"), true, new Rectangle(S(28), S(356), S(190), hb), delegate { DalBrowser(); });
        BGit = Tasto(PInizio, Lingua.T("guidata.usa_l_accesso_git_questo"), false, new Rectangle(S(228), S(356), S(250), hb), delegate { DaGit(); });
        BBrowser.Visible = porta.Browser;
        BGit.Visible = porta.Git;
        if (!porta.Browser) BGit.Left = S(28);
        BPiuTardi = Tasto(this, primaVolta ? Lingua.T("guidata.piu_tardi") : Lingua.T("comune.annulla"), false, new Rectangle(destra - S(120), yb, S(120), hb), delegate { Close(); });

        // ---------- passo 1: il codice ----------
        PCodice = Pagina();
        Scritta(PCodice, Lingua.T("guidata.passo_1_2"), Aspetto.PICCOLO, Aspetto.MUTO, S(20), S(20));
        Scritta(PCodice, Lingua.T("guidata.scrivi_questo_codice_github"), Aspetto.GRANDE, Aspetto.TESTO, S(40), S(34));
        lCodice = Scritta(PCodice, "", new Font("Consolas", 30f, FontStyle.Bold), Aspetto.LIME, S(90), S(66));
        lCodice.TextAlign = ContentAlignment.MiddleCenter;
        string passi = Lingua.T("guidata.1_premi_tasto_qui_sotto");
        if (!primaVolta) passi += Lingua.T("guidata.pagina_usa_l_account_cui");
        Scritta(PCodice, passi, Aspetto.NORMALE, Aspetto.TESTO, S(170), S(120));
        lAttesa = Scritta(PCodice, "", Aspetto.NORMALE, Aspetto.ETICHETTA, S(300), S(44));
        BApri = Tasto(PCodice, Lingua.T("comune.copia_codice_apri_github"), true, new Rectangle(S(28), S(350), S(260), hb), delegate { ApriPagina(); });
        BIndietro = Tasto(PCodice, Lingua.T("guidata.indietro"), false, new Rectangle(S(298), S(350), S(110), hb), delegate { giro++; Vai(PInizio); });

        // ---------- passo 2: le repo ----------
        PRepo = Pagina();
        Scritta(PRepo, Lingua.T("guidata.passo_2_2"), Aspetto.PICCOLO, Aspetto.MUTO, S(20), S(20));
        lChi = Scritta(PRepo, "", Aspetto.GRANDE, Aspetto.TESTO, S(40), S(34));
        Scritta(PRepo, Lingua.T("guidata.quelle_che_segui_scarico_segnalazioni"), Aspetto.NORMALE, Aspetto.MUTO, S(76), S(22));
        cFiltro = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, Bounds = new Rectangle(S(28), S(106), S(300), S(28)) };
        cFiltro.SelectedIndexChanged += delegate { if (!riempio) Righe(); };
        PRepo.Controls.Add(cFiltro);
        BTutte = Tasto(PRepo, Lingua.T("guidata.tutte"), false, new Rectangle(destra - S(190), S(104), S(90), S(30)), delegate { Spunta(true); });
        BNessuna = Tasto(PRepo, Lingua.T("guidata.nessuna"), false, new Rectangle(destra - S(90), S(104), S(90), S(30)), delegate { Spunta(false); });
        Lista = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.None, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO,
                               BorderStyle = BorderStyle.None, Bounds = new Rectangle(S(28), S(144), S(584), S(206)), MultiSelect = false, ShowItemToolTips = false };
        Lista.Columns.Add(Lingua.T("comune.repo"), S(350)); Lista.Columns.Add(Lingua.T("guidata.chi"), S(130)); Lista.Columns.Add(Lingua.T("comune.chi_vede"), S(80));
        Lista.ItemChecked += (o, e) => {
            if (riempio) return;
            var r = (RepoInfo)e.Item.Tag;
            if (e.Item.Checked) spunte.Add(r.Nome); else spunte.Remove(r.Nome);
            Contate();
        };
        PRepo.Controls.Add(Lista);
        lConti = Scritta(PRepo, "", Aspetto.NORMALE, Aspetto.TESTO, S(354), S(22));
        lOrg = Scritta(PRepo, "", Aspetto.PICCOLO, Aspetto.AMBRA, S(376), S(40));
        BAvanti = Tasto(PRepo, Lingua.T("guidata.fine"), true, new Rectangle(destra - S(120), S(0), S(120), hb), delegate { RepoScelte(); });
        BAvanti.Parent = this; BAvanti.Top = yb;

        FormClosing += (o, e) => { if (collego) e.Cancel = true; };
        FormClosed += delegate { chiusa = true; giro++; };
        Load += delegate { Aspetto.BarraScura(this); };
        Vai(PInizio);
    }

    public void Vai(Panel p) {
        foreach (var x in new[] { PInizio, PCodice, PRepo }) x.Visible = x == p;
        Avviso.Visible = p != PRepo;
        BIndietro.Enabled = BPiuTardi.Enabled = true;
        BPiuTardi.Visible = p == PInizio || p == PCodice;
        BAvanti.Visible = p == PRepo;
        Avviso.Text = "";
        foreach (var b in new[] { BBrowser, BGit }) b.Enabled = true;
        Avviso.BringToFront();
    }

    // il lavoro di rete sta fuori dalla finestra; gli errori si dicono qui, mai con testi copiati dalle risposte
    void Lavora<T>(Func<T> fai, Action<T> poi, Action<string> male) {
        int mio = giro;
        new Thread(() => {
            T r = default(T); string errore = null;
            try { r = fai(); }
            catch (ErroreAccesso ex) { errore = ex.Message; }
            catch (ErroreGitHub ex) { errore = ex.Message; }
            catch (Exception ex) { Dati.Log("guidata: " + ex.GetType().Name); errore = ex is ErroreSenzaRete || ex is ErroreScrittura ? ex.Message : Lingua.T("guidata.qualcosa_non_andato_riprova_fra"); }
            try {
                BeginInvoke((Action)(() => {
                    if (chiusa || mio != giro) return;
                    if (errore != null) male(errore); else poi(r);
                }));
            } catch (InvalidOperationException) { }  // la finestra è già chiusa
        }) { IsBackground = true }.Start();
    }

    void Sbaglio(string testo) { Vai(PInizio); Avviso.Text = testo; }

    void DalBrowser() {
        BBrowser.Enabled = BGit.Enabled = false;
        Avviso.Text = Lingua.T("guidata.chiedo_codice_github");
        Lavora(porta.Chiedi, c => {
            MostraCodice(c);
            int questa = giro;
            Lavora(() => porta.Aspetta(c, () => chiusa || questa != giro), t => { if (t != null) Collega(t, Accessi.BROWSER); }, Sbaglio);
        }, Sbaglio);
    }

    public void MostraCodice(CodiceDispositivo c) {
        codice = c;
        lCodice.Text = c.PerUtente;
        lAttesa.Text = "";
        Vai(PCodice);
    }

    void ApriPagina() {
        porta.Copia(codice.PerUtente);
        porta.Apri(codice.Pagina);
        lAttesa.Text = Lingua.T("guidata.codice_copiato_incollalo_nella_pagina");
    }

    void DaGit() {
        BBrowser.Enabled = BGit.Enabled = false;
        Avviso.Text = Lingua.T("guidata.chiedo_l_accesso_git_puo");
        Lavora(porta.DaGit, t => Collega(t, Accessi.GIT), Sbaglio);
    }

    class Trovato { public string Login; public List<RepoInfo> Repo; public List<string> Org; }

    // il conto entra nell'archivio solo se ha letto davvero: mai un account a metà
    void Collega(string token, string origine) {
        Avviso.Text = Lingua.T("guidata.accesso_riuscito_leggo_repo");
        collego = true;
        BIndietro.Enabled = BPiuTardi.Enabled = false;
        Lavora(() => {
            var t = new Trovato { Login = porta.LoginDi(token) };
            if (origine == Accessi.GIT && a.Conti.Any(x => Accessi.Uguali(x.Login, t.Login))) throw new ErroreAccesso(Lingua.T("guidata.git_usa_l_account_che", "login", t.Login));
            t.Repo = porta.RepoDi(token, t.Login);
            try { t.Org = porta.OrganizzazioniDi(token, t.Login); } catch (Exception ex) { Dati.Log("organizzazioni: " + ex.GetType().Name); t.Org = new List<string>(); }
            if (origine == Accessi.BROWSER) porta.Custodisci(t.Login, token);
            return t;
        }, t => {
            collego = false;
            var gia = new HashSet<string>(a.Repo.Select(x => x.Nome), StringComparer.OrdinalIgnoreCase);
            Accessi.Aggiungi(a, t.Login, origine);
            Accessi.Unisci(a, t.Login, t.Repo, x => false);
            salva();
            Collegato = t.Login;
            var mie = a.Repo.Where(x => Accessi.Vede(x, t.Login) && !x.Sparita).ToList();
            MostraRepo(t.Login, mie, t.Org, mie.Where(x => gia.Contains(x.Nome) ? x.Seguita : x.Mia));
        }, e => { collego = false; Sbaglio(e); });
    }

    public void MostraRepo(string login, List<RepoInfo> repo, List<string> org, IEnumerable<RepoInfo> scelte) {
        trovate = repo;
        spunte = new HashSet<string>(scelte.Select(x => x.Nome), StringComparer.OrdinalIgnoreCase);
        lChi.Text = Lingua.T("guidata.sei_dentro_come_cosa_vuoi", "login", login);
        using (var g = lChi.CreateGraphics()) lChi.Font = Aspetto.Larg(g, lChi.Text, Aspetto.GRANDE) > lChi.Width ? Aspetto.MEDIO : Aspetto.GRANDE;
        riempio = true;
        cFiltro.Items.Clear();
        filtri = Accessi.Filtri(a);
        foreach (var f in filtri) cFiltro.Items.Add(f[1]);
        cFiltro.SelectedIndex = 0;
        riempio = false;
        var vuote = (org ?? new List<string>()).Where(o => !repo.Any(r => Accessi.Uguali(r.Proprietario, o))).ToList();
        lOrg.Text = vuote.Count == 0 ? "" : Lingua.T("guidata.non_vedo_repo_vuota_chi", "vuote", string.Join(", ", vuote));
        Vai(PRepo);
        Righe();
    }

    List<string[]> filtri = new List<string[]>();

    void Righe() {
        string f = cFiltro.SelectedIndex >= 0 && cFiltro.SelectedIndex < filtri.Count ? filtri[cFiltro.SelectedIndex][0] : Accessi.TUTTE;
        riempio = true;
        Lista.BeginUpdate();
        Lista.Items.Clear();
        foreach (var r in trovate.Where(x => Accessi.Passa(x, a.Login, f)).OrderBy(x => x.Nome, StringComparer.OrdinalIgnoreCase)) {
            string chi = r.Organizzazione ? Lingua.T("comune.organizzazione") : Accessi.Uguali(r.Proprietario, a.Login) ? Lingua.T("guidata.tua") : Lingua.T("guidata.altra_persona");
            Lista.Items.Add(new ListViewItem(new[] { r.Nome, chi, r.Privata ? Lingua.T("comune.privata") : Lingua.T("comune.pubblica") }) { Tag = r, Checked = spunte.Contains(r.Nome) });
        }
        Lista.EndUpdate();
        riempio = false;
        Contate();
    }

    void Contate() {
        lConti.Text = trovate.Count == 0 ? Lingua.T("guidata.github_non_mi_mostra_nessuna")
                    : Lingua.T("guidata.ne_hai_scelte", "spunte", spunte.Count, "trovate", trovate.Count);
    }

    // «Tutte» e «Nessuna» valgono per quelle che il filtro mostra
    void Spunta(bool si) { foreach (ListViewItem i in Lista.Items) i.Checked = si; }

    void RepoScelte() {
        foreach (var r in trovate) {
            bool s = spunte.Contains(r.Nome);
            if (s && !r.Seguita) r.Dal = null;
            r.Seguita = s;
        }
        // la cartella delle repo non si chiede qui: vuota finché non la sceglie la persona dalle Impostazioni
        if (a.CartellaRepo == null) { a.CartellaRepo = ""; Dati.CartellaRepo = ""; }
        salva();
        DialogResult = DialogResult.OK;
        Close();
    }

    // ---------- la prova: si clicca da dentro, in una finestra fuori dallo schermo, con la rete finta ----------

    public class PortaFinta : IPorta {
        public bool ConBrowser = true, ConGit = true;
        public string LoginFinto = "pippo", LoginGit = "pippo", Errore;
        public List<RepoInfo> Elenco = new List<RepoInfo>();
        public List<string> Org = new List<string>();
        public bool OrgRotte;
        public ManualResetEvent Via = new ManualResetEvent(false);
        public Dictionary<string, string> Custoditi = new Dictionary<string, string>();
        public string Copiato, Aperta;
        public const string TOKEN = "gho_finto_per_la_prova";
        public bool Browser { get { return ConBrowser; } }
        public bool Git { get { return ConGit; } }
        public CodiceDispositivo Chiedi() { return new CodiceDispositivo { Codice = "dc", PerUtente = "ABCD-1234", Pagina = Accessi.PAGINA, Scade = 900, Intervallo = 5 }; }
        public string Aspetta(CodiceDispositivo c, Func<bool> annullato) {
            Via.WaitOne(10000);
            if (Errore != null) throw new ErroreAccesso(Errore);
            return TOKEN;
        }
        public string DaGit() { return TOKEN + "-git"; }
        public string LoginDi(string token) { return token.EndsWith("-git") ? LoginGit : LoginFinto; }
        public List<RepoInfo> RepoDi(string token, string login) { return Elenco.Select(r => new RepoInfo { Nome = r.Nome, Proprietario = r.Proprietario, Organizzazione = r.Organizzazione, Mia = r.Mia, Privata = r.Privata }).ToList(); }
        public List<string> OrganizzazioniDi(string token, string login) { if (OrgRotte) throw new Exception("403"); return Org; }
        public void Custodisci(string login, string token) { Custoditi[login] = token; }
        public void Copia(string testo) { Copiato = testo; }
        public void Apri(string pagina) { Aperta = pagina; }
    }

    static bool Finche(Func<bool> fatto) {
        for (int i = 0; i < 400; i++) { Application.DoEvents(); if (fatto()) return true; Thread.Sleep(25); }
        return false;
    }

    static Guidata Apri(Archivio a, IPorta p, bool prima, Action salva) {
        var g = new Guidata(a, p, prima, salva) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        g.Show();
        return g;
    }

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        Silenzioso = true;
        var js = new JavaScriptSerializer();
        string temp = Path.Combine(Path.GetTempPath(), "gpm-prova-guidata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            var a = new Archivio(); int salvate = 0;
            Action salva = () => salvate++;
            var p = new PortaFinta { Elenco = new List<RepoInfo> {
                new RepoInfo { Nome = "pippo/uno", Proprietario = "pippo", Mia = true },
                new RepoInfo { Nome = "pippo/due", Proprietario = "pippo", Mia = true, Privata = true },
                new RepoInfo { Nome = "azienda/app", Proprietario = "azienda", Organizzazione = true, Mia = false },
                new RepoInfo { Nome = "tizio/x", Proprietario = "tizio" } }, Org = new List<string> { "azienda", "vuota" } };

            // prima apertura, col browser
            var g = Apri(a, p, true, salva);
            v("si apre sul primo passo", g.PInizio.Visible && g.BBrowser.Visible && g.BGit.Visible);
            v("nessun avviso copre i tasti", !g.Avviso.Bounds.IntersectsWith(g.BBrowser.Bounds) && !g.Avviso.Bounds.IntersectsWith(g.BApri.Bounds));
            g.BBrowser.PerformClick();
            v("il codice si mostra", Finche(() => g.PCodice.Visible));
            v("nessun account finché non autorizzi", a.Conti.Count == 0);
            g.BApri.PerformClick();
            v("il tasto copia il codice e apre GitHub", p.Copiato == "ABCD-1234" && p.Aperta == Accessi.PAGINA);
            p.Via.Set();
            v("dopo l'autorizzazione si scelgono le repo", Finche(() => g.PRepo.Visible));
            v("l'account è collegato", a.Conti.Count == 1 && a.Login == "pippo" && a.Conti[0].Origine == Accessi.BROWSER && g.Collegato == "pippo");
            v("il token va a Windows", p.Custoditi.ContainsKey("pippo") && p.Custoditi["pippo"] == PortaFinta.TOKEN);
            v("il token non è nell'archivio", !js.Serialize(a).Contains(PortaFinta.TOKEN));
            v("si propongono le tue, non quelle degli altri", g.Lista.Items.Count == 4 && g.Lista.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => i.Text).OrderBy(x => x).SequenceEqual(new[] { "pippo/due", "pippo/uno" }));
            v("chiudendo adesso non segui niente", a.Repo.Count(x => x.Seguita) == 0);
            v("sulle repo l'avviso non copre la nota", !g.Avviso.Visible);
            v("l'organizzazione senza repo si segnala", g.lOrg.Text.Contains("vuota") && !g.lOrg.Text.Contains("azienda"));
            var org = g.filtri.FindIndex(f => f[0] == Accessi.ORG + "azienda");
            v("il filtro ha l'organizzazione", org > 0);
            g.cFiltro.SelectedIndex = org;
            Application.DoEvents();
            v("il filtro mostra solo l'organizzazione", g.Lista.Items.Count == 1 && g.Lista.Items[0].Text == "azienda/app");
            g.BTutte.PerformClick();
            v("«Tutte» vale per quelle mostrate", g.spunte.Count == 3 && g.spunte.Contains("azienda/app"));
            g.cFiltro.SelectedIndex = 0;
            Application.DoEvents();
            g.Lista.Items.Cast<ListViewItem>().First(i => i.Text == "pippo/due").Checked = false;
            g.BAvanti.PerformClick();
            v("sono due passi: dopo le repo ha finito, senza chiedere la cartella", Finche(() => !g.Visible));
            v("la cartella delle repo resta vuota, si sceglie dalle Impostazioni", a.CartellaRepo == "" && Dati.CartellaRepo == "");
            v("le scelte sono salvate", a.Repo.Where(x => x.Seguita).Select(x => x.Nome).OrderBy(x => x).SequenceEqual(new[] { "azienda/app", "pippo/uno" }));
            a.CartellaRepo = temp; Dati.CartellaRepo = temp;   // come se l'avesse scelta dalle Impostazioni

            // un altro account: la cartella già scelta non si tocca
            var q = new PortaFinta { LoginFinto = "caio", Elenco = new List<RepoInfo> { new RepoInfo { Nome = "azienda/app", Proprietario = "azienda", Organizzazione = true }, new RepoInfo { Nome = "caio/d", Proprietario = "caio", Mia = true } } };
            q.Via.Set();
            g = Apri(a, q, false, salva);
            g.BBrowser.PerformClick();
            v("altro account: si arriva alle repo", Finche(() => g.PRepo.Visible));
            v("altro account: è attivo, il primo resta", a.Login == "caio" && a.Conti.Count == 2);
            v("altro account: la repo in comune resta com'era, una sola", a.Repo.Count(x => x.Nome == "azienda/app") == 1 && g.spunte.Contains("azienda/app") && g.spunte.Contains("caio/d"));
            g.BAvanti.PerformClick();
            v("altro account: la cartella scelta resta, finito", Finche(() => !g.Visible) && a.CartellaRepo == temp);
            v("altro account: le repo del primo non sono sparite", !a.Repo.Any(x => x.Sparita));

            // Git con un account già collegato
            var r = new PortaFinta { LoginGit = "caio" };
            g = Apri(a, r, false, salva);
            g.BGit.PerformClick();
            v("Git con un account già collegato: lo dice", Finche(() => g.PInizio.Visible && g.Avviso.Text.Contains("già collegato")));
            v("Git: non conserva niente", r.Custoditi.Count == 0);
            g.Close();

            // Git con un account nuovo: la chiave resta a Git
            var s = new PortaFinta { LoginGit = "sempronio", OrgRotte = true };
            g = Apri(a, s, false, salva);
            g.BGit.PerformClick();
            v("Git: account nuovo collegato, anche se le organizzazioni non si leggono", Finche(() => g.PRepo.Visible) && a.Conti.Any(x => x.Login == "sempronio" && x.Origine == Accessi.GIT));
            v("Git: la chiave resta a Git", s.Custoditi.Count == 0);
            v("nessuna repo: lo dice", g.lConti.Text.Contains("nessuna repo"));
            g.Close();
            v("chiudendo a metà l'account resta collegato", a.Conti.Any(x => x.Login == "sempronio"));

            // l'autorizzazione negata e l'annullamento
            var n = new PortaFinta { Errore = "Su GitHub hai scelto di non autorizzare: nessun account collegato." };
            var b = new Archivio();
            g = Apri(b, n, true, salva);
            g.BBrowser.PerformClick();
            Finche(() => g.PCodice.Visible);
            n.Via.Set();
            v("negata: si torna indietro e lo dice", Finche(() => g.PInizio.Visible && g.Avviso.Text.Contains("non autorizzare")) && b.Conti.Count == 0 && n.Custoditi.Count == 0);
            g.Close();
            var m = new PortaFinta();
            g = Apri(b, m, true, salva);
            g.BBrowser.PerformClick();
            Finche(() => g.PCodice.Visible);
            g.BIndietro.PerformClick();
            m.Via.Set();
            for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(25); }
            v("annullata: l'autorizzazione arrivata dopo non conta", g.PInizio.Visible && b.Conti.Count == 0 && m.Custoditi.Count == 0);
            g.Close();

            // senza login col browser e senza Git
            g = Apri(new Archivio(), new PortaFinta { ConBrowser = false }, true, salva);
            v("senza client id: si offre solo Git", !g.BBrowser.Visible && g.BGit.Visible);
            g.Close();
            g = Apri(new Archivio(), new PortaFinta { ConBrowser = false, ConGit = false }, true, salva);
            v("senza client id e senza Git: nessun tasto che non funziona", !g.BBrowser.Visible && !g.BGit.Visible);
            g.Close();
        } catch (Exception ex) { v("errore: " + ex, false); }
        finally { try { Directory.Delete(temp, true); } catch { } }
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }
}
