using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public class PaginaPanoramica : Panel {
    public class Conti { public int Quarantena, Lavorare, Errori, Consegne = -1; }

    class Carta {
        public string Titolo, Grande, Frase, Avviso;
        public Color ColGrande = Aspetto.INCHIOSTRO;
        public List<string[]> Righe = new List<string[]>();
        public int[] Numeri; public string[] Didascalie;
    }

    RepoInfo repo;
    List<RepoInfo> elenco = new List<RepoInfo>();
    Func<string, Conti> conti = n => new Conti();
    string login = "";
    bool archivioVecchio;
    int alto;
    readonly List<KeyValuePair<Rectangle, RepoInfo>> righe = new List<KeyValuePair<Rectangle, RepoInfo>>();
    RepoInfo sopra;
    public event Action<RepoInfo> RepoScelta;

    static int S(int v) { return Aspetto.S(v); }

    public PaginaPanoramica() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        AutoScroll = true; BackColor = Aspetto.FONDINO;
    }

    public void Imposta(RepoInfo r, List<RepoInfo> tutte, string chi, Func<string, Conti> quanti, bool nonAggiornato) {
        bool altra = r != repo;
        repo = r; elenco = tutte; login = chi ?? ""; conti = quanti; archivioVecchio = nonAggiornato;
        if (altra) AutoScrollPosition = Point.Empty;
        Misura();
        Invalidate();
    }

    void Misura() {
        using (var g = CreateGraphics()) { alto = 0; int h = Scena(g, false); AutoScrollMinSize = new Size(0, h); }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); if (IsHandleCreated) Misura(); Invalidate(); }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (Form.ActiveForm == FindForm()) Focus(); }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        alto = AutoScrollPosition.Y;
        righe.Clear();
        Scena(e.Graphics, true);
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        RepoInfo s = null;
        foreach (var kv in righe) if (kv.Key.Contains(e.Location)) s = kv.Value;
        if (s != sopra) { sopra = s; Cursor = s != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (sopra != null) { sopra = null; Invalidate(); } }
    protected override void OnMouseClick(MouseEventArgs e) {
        base.OnMouseClick(e);
        foreach (var kv in righe) if (kv.Key.Contains(e.Location) && RepoScelta != null) { RepoScelta(kv.Value); return; }
    }

    // ---------- le parole ----------

    public static string Data(string iso) {
        DateTime t;
        return DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out t) ? t.ToLocalTime().ToString("dd/MM/yyyy") : "";
    }
    static string V(string s) { return Dogana.RendiVisibile((s ?? "").Replace("\r", " ").Replace("\n", " ")); }
    public static string CortoDi(RepoInfo r, string chi) {
        return V(Organizzazione.Corto(r, chi));
    }

    // di chi è la repo, per la tabella: Corto toglie il proprietario, qui lo si rimette, con una parola che dice che tipo è
    public static string ProprietarioDi(RepoInfo r, string chi) {
        string p = r.Proprietario;
        if (string.IsNullOrEmpty(p) && r.Nome != null && r.Nome.IndexOf('/') > 0) p = r.Nome.Substring(0, r.Nome.IndexOf('/'));
        string tipo = Organizzazione.Uguali(p, chi) ? Lingua.T("panoramica.personale") : (r.Organizzazione || r.Mia) ? Lingua.T("comune.organizzazione") : Lingua.T("panoramica.altri");
        return tipo + " · " + V(p);
    }

    static string RuoloDi(string ruolo) {
        switch ((ruolo ?? "").ToLowerInvariant()) {
            case "read": return Lingua.T("panoramica.legge");
            case "triage": return Lingua.T("panoramica.legge_ordina_segnalazioni");
            case "write": return Lingua.T("panoramica.scrive");
            case "maintain": return Lingua.T("panoramica.scrive_gestisce");
            case "admin": return Lingua.T("panoramica.amministra");
            default: return Lingua.T("panoramica.permesso_non_noto", "ruolo", V(ruolo));
        }
    }

    // le cose da sistemare, nell'ordine in cui conviene farle
    static List<string> CosaManca(Panorama p, Conti c) {
        var l = new List<string>();
        if (p != null) {
            if (p.ReleaseLetta && string.IsNullOrEmpty(p.ReleaseTag)) l.Add(Lingua.T("panoramica.nessuna_release"));
            else if (p.CommitDopo > 0) l.Add(Lingua.N("panoramica.commit_dopo", p.CommitDopo));
            if (p.LicenzaLetta && string.IsNullOrEmpty(p.Licenza)) l.Add(Lingua.T("panoramica.nessuna_licenza"));
            if (p.ReadmeLetto && !p.Readme) l.Add(Lingua.T("panoramica.nessuna_pagina_principale"));
        }
        if (c.Consegne > 0) l.Add(Lingua.N("panoramica.consegne", c.Consegne));
        if (c.Quarantena > 0) l.Add(Lingua.N("panoramica.testi_quarantena", c.Quarantena));
        if (c.Errori > 0) l.Add(Lingua.N("panoramica.errori", c.Errori));
        return l;
    }

    // si può dire «non manca niente» solo con i dati da cui dipende «cosa manca». I collaboratori non contano: a chi non
    // amministra la repo GitHub non li dà mai, e la scritta non comparirebbe più
    public static bool LettaTutta(Panorama p) { return p != null && p.ReleaseLetta && p.LicenzaLetta && p.ReadmeLetto; }

    // le repo su cui non si può ancora dire niente: mai lette, o lette solo in parte
    public static List<string> DaLeggere(IEnumerable<RepoInfo> vive) {
        var l = new List<string>();
        int mai = vive.Count(r => r.Pannello == null), meta = vive.Count(r => r.Pannello != null && !LettaTutta(r.Pannello));
        if (mai > 0) l.Add(Lingua.N("panoramica.non_lette", mai));
        if (meta > 0) l.Add(Lingua.N("panoramica.a_meta", meta));
        return l;
    }

    // la frase sotto il titolo di «Tutte le repo», quando non c'è niente da sistemare fra quello che si è letto
    public static string FraseTutte(int vive, int chip, List<string> daLeggere) {
        if (chip > 0) return null;
        if (vive == 0) return Lingua.T("panoramica.nessuna_repo_controllare");
        return daLeggere.Count > 0 ? Lingua.T("panoramica.finche_non_leggo_non_so") : Lingua.T("panoramica.non_manca_niente");
    }

    public static string TitoloRepo(Panorama p, int chip) {
        return chip > 0 ? Lingua.T("panoramica.cosa_manca") : LettaTutta(p) ? Lingua.T("panoramica.non_manca_niente_2") : Lingua.T("panoramica.lettura_incompleta");
    }

    // ---------- la prova, con dati finti ----------
    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        Func<Panorama> letta = () => new Panorama { ReleaseLetta = true, LicenzaLetta = true, ReadmeLetto = true };
        Func<Panorama, RepoInfo> repo = p => new RepoInfo { Nome = "pippo/x", Pannello = p };
        Func<RepoInfo[], int, string> frase = (r, chip) => FraseTutte(r.Length, chip, DaLeggere(r));
        const string TUTTO = "Non manca niente.";

        var mai = new[] { repo(null), repo(null) };
        v("nessuna repo letta: non dice che è tutto a posto", frase(mai, 0) != TUTTO && frase(mai, 0) != null);
        v("nessuna repo letta: dice quante mancano", DaLeggere(mai).SequenceEqual(new[] { "2 repo non ancora lette" }));
        var una = new[] { repo(letta()), repo(null) };
        v("una letta e una no: non dice che è tutto a posto", frase(una, 0) != TUTTO);
        v("una letta e una no: dice quale conto", DaLeggere(una).SequenceEqual(new[] { "1 repo non ancora letta" }));
        var senzaLic = letta(); senzaLic.LicenzaLetta = false; senzaLic.LicenzaErrore = "errore finto";
        var meta = new[] { repo(senzaLic) };
        v("licenza non letta: non dice che è tutto a posto", frase(meta, 0) != TUTTO);
        v("licenza non letta: è una repo letta a metà", DaLeggere(meta).SequenceEqual(new[] { "1 repo letta a metà" }));
        var senzaRel = letta(); senzaRel.ReleaseLetta = false;
        var senzaRea = letta(); senzaRea.ReadmeLetto = false;
        v("release non letta: letta a metà, anche senza errore scritto", !LettaTutta(senzaRel) && frase(new[] { repo(senzaRel) }, 0) != TUTTO);
        v("pagina principale non letta: letta a metà", !LettaTutta(senzaRea) && frase(new[] { repo(senzaRea) }, 0) != TUTTO);
        v("due lette a metà e una mai: conta tutte e due le voci", DaLeggere(new[] { repo(senzaRel), repo(senzaRea), repo(null) }).SequenceEqual(new[] { "1 repo non ancora letta", "2 repo lette a metà" }));
        var tutte = new[] { repo(letta()), repo(letta()) };
        v("tutte lette e niente da sistemare: non manca niente", frase(tutte, 0) == TUTTO && DaLeggere(tutte).Count == 0);
        var noCol = letta(); noCol.CollaboratoriErrore = "403";
        v("collaboratori negati: non conta, GitHub li dà solo a chi amministra", frase(new[] { repo(noCol) }, 0) == TUTTO);
        v("con qualcosa da sistemare: la frase non serve", frase(tutte, 2) == null && frase(una, 2) == null);
        v("nessuna repo: non dice che è tutto a posto", frase(new RepoInfo[0], 0) != TUTTO);
        v("la scheda di una repo: tutta letta, non manca niente", TitoloRepo(letta(), 0) == "Non manca niente");
        v("la scheda di una repo: letta a metà, non lo dice", TitoloRepo(senzaLic, 0) != "Non manca niente");
        v("la scheda di una repo: letta a metà con qualcosa da sistemare", TitoloRepo(senzaLic, 1) == "Cosa manca");
        Func<string, string, bool, bool, RepoInfo> di = (nome, prop, org, mia) => new RepoInfo { Nome = nome, Proprietario = prop, Organizzazione = org, Mia = mia };
        v("tabella: la repo personale dice «personale» e il proprietario", ProprietarioDi(di("pippo/x", "pippo", false, true), "pippo") == "personale · pippo");
        v("tabella: la repo di un'organizzazione dice «organizzazione» e il suo nome", ProprietarioDi(di("azienda/app", "azienda", true, true), "pippo") == "organizzazione · azienda");
        v("tabella: l'organizzazione di cui non sei amministratore è sempre un'organizzazione", ProprietarioDi(di("azienda/app", "azienda", true, false), "pippo") == "organizzazione · azienda");
        v("tabella: la repo di un'altra persona dice «di altri»", ProprietarioDi(di("tizio/x", "tizio", false, false), "pippo") == "di altri · tizio");
        v("tabella: maiuscole del login uguali, resta personale", ProprietarioDi(di("Pippo/x", "Pippo", false, true), "pippo") == "personale · Pippo");
        v("tabella: archivio vecchio, tua ma di altro proprietario, è un'organizzazione", ProprietarioDi(di("azienda/app", "azienda", false, true), "pippo") == "organizzazione · azienda");
        v("tabella: senza proprietario scritto lo prende dal nome", ProprietarioDi(di("tizio/x", null, false, false), "pippo") == "di altri · tizio");
        v("tabella: il nome breve resta quello di prima", CortoDi(di("azienda/app", "azienda", true, true), "pippo") == "app");
        v("tabella: nel proprietario i caratteri invisibili si vedono", ProprietarioDi(di("a/x", "a​b", false, false), "pippo").Contains("{U+200B}"));
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }

    // quello che non si è potuto leggere e di cui non c'è nemmeno un dato vecchio
    static List<string> NonLetti(Panorama p) {
        var l = new List<string>();
        if (p == null) return l;
        if (!p.ReleaseLetta && p.ReleaseErrore != null) l.Add(Lingua.T("panoramica.release"));
        if (p.Collaboratori == null && p.CollaboratoriErrore != null) l.Add(Lingua.T("panoramica.collaboratori"));
        if (!p.LicenzaLetta && p.LicenzaErrore != null) l.Add(Lingua.T("panoramica.licenza"));
        if (!p.ReadmeLetto && p.ReadmeErrore != null) l.Add(Lingua.T("panoramica.pagina_principale"));
        return l;
    }

    // l'errore di stavolta: se c'è un dato vecchio si dice che è vecchio, se no che non si è potuto leggere
    static string Errori(Panorama p, bool haDato, params string[] errori) {
        var e = errori.Where(x => x != null).Distinct().ToList();
        if (e.Count == 0) return null;
        return (haDato ? Lingua.T("panoramica.non_riletto_sono_dati_prima") : Lingua.T("panoramica.non_ho_potuto_leggerlo")) + string.Join("; ", e) + ".";
    }

    List<Carta> Carte() {
        var p = repo.Pannello;
        var c = conti(repo.Nome);
        var l = new List<Carta>();
        string NON = Lingua.T("panoramica.non_letto");

        var v = new Carta { Titolo = Lingua.T("panoramica.versione") };
        if (p == null || !p.ReleaseLetta) { v.Grande = Lingua.T("panoramica.non_letta"); v.ColGrande = Aspetto.GRIGIO; v.Frase = Lingua.T("panoramica.non_so_ancora_quale_sia"); }
        else if (string.IsNullOrEmpty(p.ReleaseTag)) { v.Grande = Lingua.T("panoramica.nessuna_release_2"); v.ColGrande = Aspetto.AMBRA_S; v.Frase = Lingua.T("panoramica.release_versione_pronta_scaricare_qui"); }
        else {
            v.Grande = V(p.ReleaseTag);
            v.Frase = Lingua.T("panoramica.l_ultima_versione_pronta_scaricare", "parte", (Data(p.ReleaseData).Length > 0 ? Lingua.T("panoramica.pubblicata", "releasedata", Data(p.ReleaseData)) : ""));
        }
        bool tag = p != null && p.TagLetto, com = p != null && p.CommitLetto;
        v.Righe.Add(new[] { Lingua.T("panoramica.ultimo_tag_segnalibro_versione"), !tag ? NON : string.IsNullOrEmpty(p.UltimoTag) ? Lingua.T("comune.nessuno") : V(p.UltimoTag) });
        v.Righe.Add(new[] { Lingua.T("panoramica.commit_dopo_l_ultima_release"), p == null || !p.ReleaseLetta ? NON : string.IsNullOrEmpty(p.ReleaseTag) ? Lingua.T("panoramica.nessuna_release") : p.CommitDopo < 0 ? NON : p.CommitDopo.ToString() });
        v.Righe.Add(new[] { Lingua.T("panoramica.ultimo_commit_modifica_salvata"), !com ? NON : string.IsNullOrEmpty(p.UltimoCommit) ? Lingua.T("comune.nessuno") : Data(p.UltimoCommit) });
        if (p != null) v.Avviso = Errori(p, p.ReleaseLetta || tag || com, p.ReleaseErrore, p.TagErrore, p.CommitDopoErrore, p.CommitErrore);
        l.Add(v);

        var w = new Carta { Titolo = Lingua.T("comune.chi_vede"), Grande = repo.Privata ? Lingua.T("panoramica.privata") : Lingua.T("comune.pubblica_2"), ColGrande = repo.Privata ? Aspetto.INCHIOSTRO : Aspetto.VERDE_S,
            Frase = repo.Privata ? Lingua.T("panoramica.vedono_solo_persone_cui_hai") : Lingua.T("panoramica.vede_chiunque_internet_anche_senza") };
        bool org = repo.Organizzazione || (repo.Mia && !Organizzazione.Uguali(repo.Proprietario, login));
        w.Righe.Add(new[] { org ? Lingua.T("panoramica.proprietario_organizzazione") : Lingua.T("panoramica.proprietario"), V(repo.Proprietario) });
        if (org && repo.Privata) w.Frase = Lingua.T("panoramica.vedono_solo_membri_dell_organizzazione");
        l.Add(w);

        var k = new Carta { Titolo = Lingua.T("comune.collaboratori") };
        if (p == null || p.Collaboratori == null) { k.Grande = Lingua.T("panoramica.non_letti"); k.ColGrande = Aspetto.GRIGIO; k.Frase = Lingua.T("panoramica.non_so_ancora_chi_puo"); }
        else {
            var altri = Organizzazione.Altri(p.Collaboratori, repo, login);
            k.Grande = altri.Count == 0 ? (p.Collaboratori.Count == 0 ? Lingua.T("panoramica.nessuno") : Lingua.T("panoramica.solo_tu")) : Lingua.N("panoramica.n_persone", p.Collaboratori.Count);
            k.Frase = altri.Count == 0 ? Lingua.T("panoramica.nessun_altro_puo_entrare_questa") : Lingua.T("panoramica.chi_puo_entrare_cosa_puo");
            foreach (var x in p.Collaboratori.OrderByDescending(x => x.Nome == repo.Proprietario).ThenBy(x => x.Nome))
                k.Righe.Add(new[] { V(x.Nome) + (Organizzazione.Uguali(x.Nome, login) ? Lingua.T("panoramica.tu") : ""), RuoloDi(x.Ruolo) });
        }
        if (p != null) k.Avviso = Errori(p, p.Collaboratori != null, p.CollaboratoriErrore);
        l.Add(k);

        var q = new Carta { Titolo = Lingua.T("comune.licenza") };
        if (p == null || !p.LicenzaLetta) { q.Grande = Lingua.T("panoramica.non_letta"); q.ColGrande = Aspetto.GRIGIO; q.Frase = Lingua.T("panoramica.non_so_ancora_se_c"); }
        else if (string.IsNullOrEmpty(p.Licenza)) { q.Grande = Lingua.T("comune.nessuna_2"); q.ColGrande = Aspetto.AMBRA_S; q.Frase = Lingua.T("panoramica.senza_licenza_nessuno_ha_permesso"); }
        else { q.Grande = V(p.Licenza); q.Frase = p.LicenzaAltra ? Lingua.T("panoramica.dice_agli_altri_cosa_possono") : Lingua.T("panoramica.dice_agli_altri_cosa_possono_2"); }
        q.Righe.Add(new[] { Lingua.T("panoramica.avviso_copyright_nel_readme"), p == null || !p.ReadmeLetto ? NON : !p.Readme ? Lingua.T("panoramica.manca_readme") : p.ReadmeCopyright ? Lingua.T("panoramica.si") : Lingua.T("panoramica.no") });
        if (p != null) q.Avviso = Errori(p, p.LicenzaLetta, p.LicenzaErrore);
        l.Add(q);

        var m = new Carta { Titolo = Lingua.T("panoramica.pagina_principale_readme") };
        if (p == null || !p.ReadmeLetto) { m.Grande = Lingua.T("panoramica.non_letta"); m.ColGrande = Aspetto.GRIGIO; m.Frase = Lingua.T("panoramica.non_so_ancora_se_c_2"); }
        else if (!p.Readme) { m.Grande = Lingua.T("panoramica.non_c"); m.ColGrande = Aspetto.AMBRA_S; m.Frase = Lingua.T("panoramica.pagina_che_si_vede_aprendo"); }
        else { m.Grande = Lingua.T("panoramica.c"); m.Frase = Lingua.T("panoramica.pagina_che_si_vede_aprendo_2"); }
        m.Righe.Add(new[] { Lingua.T("panoramica.ultimo_cambiamento"), p == null || !p.ReadmeLetto ? NON : !p.Readme ? Lingua.T("comune.nessuno") : Data(p.ReadmeData).Length > 0 ? Data(p.ReadmeData) : NON });
        if (c.Consegne >= 0) m.Righe.Add(new[] { Lingua.T("panoramica.consegne_accettate_attesa_rilascio"), c.Consegne.ToString() });
        if (p != null) m.Avviso = Errori(p, p.ReadmeLetto, p.ReadmeErrore, p.ReadmeDataErrore);
        l.Add(m);

        l.Add(new Carta { Titolo = Lingua.T("comune.segnalazioni"), Numeri = new[] { c.Quarantena, c.Lavorare, c.Errori }, Didascalie = new[] { Lingua.T("panoramica.quarantena"), Lingua.T("panoramica.lavorare"), Lingua.T("panoramica.errori_ricorrenti") },
            Frase = repo.Sparita ? Lingua.T("panoramica.testi_gia_scaricati_restano_nell") : repo.Seguita ? Lingua.T("panoramica.issue_commenti_rapporti_arrivati_githu") : Lingua.T("panoramica.non_segui_questa_repo_testi") });
        return l;
    }

    // ---------- il disegno: lo stesso codice misura (dis = false) e disegna ----------

    int Scena(Graphics g, bool dis) {
        int m = S(20), w = ClientSize.Width - m * 2, y = m + alto;
        if (w < S(200)) return 0;
        y = repo == null ? SceneTutte(g, dis, m, y, w) : SceneRepo(g, dis, m, y, w);
        return y - alto;
    }

    int SceneRepo(Graphics g, bool dis, int m, int y, int w) {
        var p = repo.Pannello;
        var c = conti(repo.Nome);
        List<string> chip; string titolo, sotto = null; Color punto = Aspetto.AMBRA;
        if (repo.Sparita) {
            titolo = Lingua.T("panoramica.questa_repo_non_c_piu");
            sotto = Lingua.T("panoramica.stata_cancellata_stato_cambiato_nome", "parte", (p != null && p.LettoIl != null ? Lingua.T("panoramica.quello_che_vedi_l_ultima", "lettoil", p.LettoIl) : Lingua.T("panoramica.non_l_ho_letta_prima")));
            chip = new List<string>();
        } else if (p == null) {
            titolo = Lingua.T("panoramica.non_l_ho_ancora_letta");
            sotto = archivioVecchio ? Lingua.T("panoramica.serve_rete_appena_github_risponde") : Lingua.T("panoramica.sto_leggendo_oppure_premi_aggiorna");
            chip = new List<string>();
        } else {
            chip = CosaManca(p, c);
            titolo = TitoloRepo(p, chip.Count);
            if (chip.Count == 0 && !LettaTutta(p)) sotto = Lingua.T("panoramica.finche_non_leggo_tutta_non");
        }
        var nonLetti = NonLetti(p).Select(x => Lingua.T("panoramica.non_ho_potuto_leggere", "testo", x)).ToList();
        y = Striscia(g, dis, m, y, w, titolo, sotto, chip, nonLetti, chip.Count == 0 && sotto == null ? Aspetto.LIME : Aspetto.TESTO, punto) + S(14);

        var carte = Carte();
        if (repo.Sparita && p == null) carte = carte.GetRange(carte.Count - 1, 1);   // sparita prima di essere letta: restano solo i testi
        int gap = S(14), col = w >= S(700) ? 2 : 1, lc = (w - gap * (col - 1)) / col;
        for (int i = 0; i < carte.Count; i += col) {
            int h = 0;
            for (int j = i; j < i + col && j < carte.Count; j++) h = Math.Max(h, AltCarta(g, carte[j], lc));
            if (dis) for (int j = i; j < i + col && j < carte.Count; j++) DisegnaCarta(g, carte[j], m + (j - i) * (lc + gap), y, lc, h);
            y += h + gap;
        }
        return y - gap + m;
    }

    // la striscia scura in testa, come una tendina di WeightMonitor
    int Striscia(Graphics g, bool dis, int x, int y, int w, string titolo, string sotto, List<string> chip, List<string> grigi, Color colTitolo, Color punto) {
        int pad = S(16), inizio = y, aChip = S(26), passo = S(32);
        int yy = y + S(12);
        var pezzi = new List<KeyValuePair<Rectangle, KeyValuePair<string, Color>>>();
        int hT = S(24);
        yy += hT;
        int hS = 0;
        if (sotto != null) { hS = Aspetto.Alt(g, sotto, Aspetto.NORMALE, w - pad * 2); yy += S(2) + hS; }
        int yTesti = yy - hS;
        var tutti = chip.Select(s => new KeyValuePair<string, Color>(s, punto)).Concat(grigi.Select(s => new KeyValuePair<string, Color>(s, Aspetto.MUTO))).ToList();
        if (tutti.Count > 0) {
            yy += S(8);
            int cx = x + pad;
            foreach (var t in tutti) {
                int lw = Aspetto.Larg(g, t.Key, Aspetto.NORMALE) + S(32);
                if (cx + lw > x + w - pad && cx > x + pad) { cx = x + pad; yy += passo; }
                pezzi.Add(new KeyValuePair<Rectangle, KeyValuePair<string, Color>>(new Rectangle(cx, yy, Math.Min(lw, w - pad * 2), aChip), t));
                cx += lw + S(8);
            }
            yy += aChip;
        }
        yy += S(14);
        if (dis) {
            Aspetto.Pieno(g, new Rectangle(x, inizio, w, yy - inizio), S(12), Aspetto.FONDO);
            Aspetto.T(g, titolo, Aspetto.MEDIO, x + pad, inizio + S(12), w - pad * 2, hT, colTitolo);
            if (sotto != null) Aspetto.TPiu(g, sotto, Aspetto.NORMALE, x + pad, yTesti, w - pad * 2, hS + S(2), Aspetto.MUTO);
            foreach (var pz in pezzi) {
                var r = pz.Key;
                Aspetto.Pieno(g, r, r.Height / 2, Aspetto.CAPSULA);
                Aspetto.Pieno(g, new Rectangle(r.X + S(11), r.Y + (r.Height - S(7)) / 2, S(7), S(7)), S(4), pz.Value.Value);
                Aspetto.T(g, pz.Value.Key, Aspetto.NORMALE, r.X + S(23), r.Y, r.Width - S(30), r.Height, Aspetto.TESTO);
            }
        }
        return yy;
    }

    int AltCarta(Graphics g, Carta c, int w) {
        int pad = S(16), lt = w - pad * 2;
        int h = S(14) + S(20);
        if (c.Numeri != null) h += S(8) + S(36) + S(20);
        else h += S(4) + S(30);
        if (c.Frase != null) h += S(4) + Aspetto.Alt(g, c.Frase, Aspetto.NORMALE, lt);
        if (c.Righe.Count > 0) h += S(8) + c.Righe.Count * S(26);
        if (c.Avviso != null) h += S(8) + Aspetto.Alt(g, c.Avviso, Aspetto.PICCOLO, lt);
        return h + S(14);
    }

    void DisegnaCarta(Graphics g, Carta c, int x, int y, int w, int h) {
        int pad = S(16), lt = w - pad * 2, xt = x + pad;
        Aspetto.Carta(g, new Rectangle(x, y, w, h), S(10), Color.White, Aspetto.BORDO);
        int yy = y + S(14);
        Aspetto.T(g, c.Titolo, Aspetto.FORTE, xt, yy, lt, S(20), Aspetto.AZZURRO);
        yy += S(20);
        if (c.Numeri != null) {
            yy += S(8);
            int lc = lt / c.Numeri.Length;
            for (int i = 0; i < c.Numeri.Length; i++) {
                bool allarme = i != 1 && c.Numeri[i] > 0;
                Aspetto.T(g, c.Numeri[i].ToString(), Aspetto.TITOLO, xt + i * lc, yy, lc - S(6), S(36), c.Numeri[i] == 0 ? Aspetto.GRIGIO : allarme ? Aspetto.AMBRA_S : Aspetto.AZZURRO);
                Aspetto.T(g, c.Didascalie[i], Aspetto.PICCOLO, xt + i * lc, yy + S(36), lc - S(6), S(20), Aspetto.GRIGIO);
            }
            yy += S(36) + S(20);
        } else {
            yy += S(4);
            Aspetto.T(g, c.Grande, Aspetto.GRANDE, xt, yy, lt, S(30), c.ColGrande);
            yy += S(30);
        }
        if (c.Frase != null) {
            int hf = Aspetto.Alt(g, c.Frase, Aspetto.NORMALE, lt);
            yy += S(4);
            Aspetto.TPiu(g, c.Frase, Aspetto.NORMALE, xt, yy, lt, hf + S(2), Aspetto.GRIGIO);
            yy += hf;
        }
        if (c.Righe.Count > 0) {
            yy += S(8);
            using (var n = new Pen(Color.FromArgb(235, 237, 241)))
                foreach (var r in c.Righe) {
                    g.DrawLine(n, xt, yy, xt + lt, yy);
                    int wv = Math.Min(Aspetto.Larg(g, r[1], Aspetto.FORTE), lt * 3 / 5);
                    Aspetto.T(g, r[0], Aspetto.NORMALE, xt, yy, lt - wv - S(10), S(26), Aspetto.GRIGIO);
                    Aspetto.T(g, r[1], Aspetto.FORTE, xt + lt - wv, yy, wv + 1, S(26), r[1] == Lingua.T("panoramica.non_letto") ? Aspetto.GRIGIO : Aspetto.INCHIOSTRO);
                    yy += S(26);
                }
        }
        if (c.Avviso != null) {
            int ha = Aspetto.Alt(g, c.Avviso, Aspetto.PICCOLO, lt);
            yy += S(8);
            Aspetto.TPiu(g, c.Avviso, Aspetto.PICCOLO, xt, yy, lt, ha + S(2), Aspetto.AMBRA_S);
        }
    }

    // ---------- Tutte le repo ----------

    static string[] COLONNE { get { return new[] { Lingua.T("comune.repo"), Lingua.T("comune.chi_vede"), Lingua.T("panoramica.versione"), Lingua.T("panoramica.commit_dopo_release"), Lingua.T("comune.licenza"), Lingua.T("panoramica.persone"), Lingua.T("panoramica.quarantena_2"), Lingua.T("comune.lavorare"), Lingua.T("panoramica.errori") }; } }
    static readonly int[] PESI = { 23, 9, 8, 10, 20, 8, 8, 8, 6 };

    int SceneTutte(Graphics g, bool dis, int m, int y, int w) {
        var vive = elenco.Where(r => !r.Sparita).ToList();
        int spar = elenco.Count - vive.Count;
        Func<RepoInfo, bool> senzaRel = r => r.Pannello != null && r.Pannello.ReleaseLetta && string.IsNullOrEmpty(r.Pannello.ReleaseTag);
        Func<RepoInfo, bool> senzaLic = r => r.Pannello != null && r.Pannello.LicenzaLetta && string.IsNullOrEmpty(r.Pannello.Licenza);
        Func<RepoInfo, bool> senzaRea = r => r.Pannello != null && r.Pannello.ReadmeLetto && !r.Pannello.Readme;
        var chip = new List<string>();
        Action<int, string> conta = (n, chiave) => { if (n > 0) chip.Add(Lingua.N(chiave, n)); };
        conta(vive.Count(senzaRel), "panoramica.senza_release");
        conta(vive.Count(senzaLic), "panoramica.senza_licenza");
        conta(vive.Count(senzaRea), "panoramica.senza_readme");
        conta(elenco.Sum(r => Math.Max(0, conti(r.Nome).Consegne)), "panoramica.consegne");
        conta(elenco.Sum(r => conti(r.Nome).Quarantena), "panoramica.testi_quarantena");
        conta(elenco.Sum(r => conti(r.Nome).Errori), "panoramica.errori");
        string titolo = Lingua.N("panoramica.su_github", vive.Count) + (spar > 0 ? ", " + Lingua.N("comune.sparite", spar) : "");
        var daLeggere = DaLeggere(vive);
        y = Striscia(g, dis, m, y, w, titolo, FraseTutte(vive.Count, chip.Count, daLeggere), chip, daLeggere, Aspetto.TESTO, Aspetto.AMBRA) + S(14);

        int aTesta = S(44), aRiga = S(66), hTab = aTesta + aRiga * elenco.Count;
        if (dis) {
            Aspetto.Carta(g, new Rectangle(m, y, w, hTab), S(10), Color.White, Aspetto.BORDO);
            int pad = S(14), lu = w - pad * 2, sp = PESI.Sum();
            var xs = new int[COLONNE.Length + 1];
            int acc = 0;
            for (int i = 0; i < COLONNE.Length; i++) { xs[i] = m + pad + acc * lu / sp; acc += PESI[i]; }
            xs[COLONNE.Length] = m + pad + lu;
            for (int i = 0; i < COLONNE.Length; i++)
                TextRenderer.DrawText(g, COLONNE[i], Aspetto.PICCOLO_F, new Rectangle(xs[i], y, xs[i + 1] - xs[i] - S(8), aTesta), Aspetto.GRIGIO,
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.TextBoxControl);
            int yy = y + aTesta;
            using (var n = new Pen(Color.FromArgb(235, 237, 241)))
                foreach (var r in elenco) {
                    var zona = new Rectangle(m + 1, yy, w - 2, aRiga);
                    righe.Add(new KeyValuePair<Rectangle, RepoInfo>(zona, r));
                    if (r == sopra) using (var b = new SolidBrush(Aspetto.FONDINO)) g.FillRectangle(b, zona);
                    g.DrawLine(n, m + 1, yy, m + w - 2, yy);
                    RigaTabella(g, r, xs, yy, aRiga);
                    yy += aRiga;
                }
        }
        return y + hTab + S(20);
    }

    void RigaTabella(Graphics g, RepoInfo r, int[] xs, int y, int h) {
        var p = r.Pannello; var c = conti(r.Nome);
        Action<int, string, Color> cel = (i, s, col) => Aspetto.T(g, s, Aspetto.NORMALE, xs[i], y, xs[i + 1] - xs[i] - S(8), h, col);
        Color ink = Aspetto.INCHIOSTRO, gr = Aspetto.GRIGIO, am = Aspetto.AMBRA_S;
        int wn = xs[1] - xs[0] - S(8);
        Aspetto.T(g, CortoDi(r, login), Aspetto.FORTE, xs[0], y + S(7), wn, S(20), r.Sparita ? gr : ink);
        Aspetto.T(g, ProprietarioDi(r, login), Aspetto.PICCOLO, xs[0], y + S(27), wn, S(17), Aspetto.AZZURRO);
        string sotto = r.Sparita ? Lingua.T("comune.non_c_piu_github") : !r.Seguita ? Lingua.T("comune.non_seguita") : p == null || !p.CommitLetto ? "" : string.IsNullOrEmpty(p.UltimoCommit) ? Lingua.T("panoramica.vuota_nessun_commit") : Lingua.T("panoramica.ultimo_commit", "ultimocommit", Data(p.UltimoCommit));
        Aspetto.T(g, sotto, Aspetto.PICCOLO, xs[0], y + S(45), wn, S(17), r.Sparita ? am : gr);
        Aspetto.Pillola(g, r.Privata ? Lingua.T("comune.privata") : Lingua.T("comune.pubblica"), Aspetto.PICCOLO_F, xs[1], y + (h - S(20)) / 2, S(20), r.Privata ? Aspetto.FONDINO : Aspetto.VERDE_T, r.Privata ? gr : Aspetto.VERDE_S);
        if (r.Sparita && p == null) { cel(2, "-", gr); cel(3, "-", gr); cel(4, "-", gr); cel(5, "-", gr); }
        else {
            bool rel = p != null && p.ReleaseLetta, lic = p != null && p.LicenzaLetta;
            cel(2, !rel ? Lingua.T("panoramica.non_letta_2") : string.IsNullOrEmpty(p.ReleaseTag) ? Lingua.T("comune.nessuna") : V(p.ReleaseTag), !rel ? gr : string.IsNullOrEmpty(p.ReleaseTag) ? am : ink);
            cel(3, !rel || p.CommitDopo < 0 ? "-" : p.CommitDopo.ToString(), rel && p.CommitDopo > 0 ? am : gr);
            cel(4, !lic ? Lingua.T("panoramica.non_letta_2") : string.IsNullOrEmpty(p.Licenza) ? Lingua.T("comune.nessuna") : V(p.Licenza), !lic ? gr : string.IsNullOrEmpty(p.Licenza) ? am : ink);
            cel(5, p == null || p.Collaboratori == null ? Lingua.T("panoramica.non_letti_2") : p.Collaboratori.Count.ToString(), p == null || p.Collaboratori == null ? gr : ink);
        }
        cel(6, c.Quarantena.ToString(), c.Quarantena > 0 ? am : gr);
        cel(7, c.Lavorare.ToString(), c.Lavorare > 0 ? Aspetto.AZZURRO : gr);
        cel(8, c.Errori.ToString(), c.Errori > 0 ? am : gr);
    }
}
