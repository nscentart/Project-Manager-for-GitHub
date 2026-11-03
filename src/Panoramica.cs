using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public class PaginaPanoramica : Panel {
    public class Conti { public int Quarantena, Lavorare, Errori, Consegne = -1, NuoveSegnalazioni, NuoviCommenti; }
    public class Cella { public string Testo; public Color Colore; }
    public enum Tasto { Nessuno, Gruppo, TogliTutte, TogliQuesta }

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
    bool spariteAperte;
    Func<string, List<GiornoStorico>> storicoDi = n => new List<GiornoStorico>();
    List<GiornoStorico> giorni = new List<GiornoStorico>();
    readonly List<KeyValuePair<Rectangle, Tasto>> tasti = new List<KeyValuePair<Rectangle, Tasto>>();
    readonly List<KeyValuePair<Rectangle, string>> barre = new List<KeyValuePair<Rectangle, string>>();
    readonly ToolTip suggerimento = new ToolTip();
    Tasto sopraTasto;
    string sopraBarra;
    public event Action<RepoInfo> RepoScelta;
    public event Action GruppoCliccato, TogliTutte, TogliQuesta;

    static int S(int v) { return Aspetto.S(v); }

    public PaginaPanoramica() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        AutoScroll = true; BackColor = Aspetto.FONDINO;
    }

    public void Imposta(RepoInfo r, List<RepoInfo> tutte, string chi, Func<string, Conti> quanti, bool nonAggiornato, bool aperte, Func<string, List<GiornoStorico>> storico) {
        bool altra = r != repo;
        repo = r; elenco = tutte; login = chi ?? ""; conti = quanti; archivioVecchio = nonAggiornato; spariteAperte = aperte; storicoDi = storico;
        giorni = r == null ? new List<GiornoStorico>() : storico(r.Nome);
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
        righe.Clear(); tasti.Clear(); barre.Clear();
        Scena(e.Graphics, true);
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        RepoInfo s = null;
        var t = TastoSotto(e.Location);
        if (t == Tasto.Nessuno) foreach (var kv in righe) if (kv.Key.Contains(e.Location)) s = kv.Value;
        if (s != sopra || t != sopraTasto) { sopra = s; sopraTasto = t; Cursor = s != null || t != Tasto.Nessuno ? Cursors.Hand : Cursors.Default; Invalidate(); }
        string b = null;
        foreach (var kv in barre) if (kv.Key.Contains(e.Location)) b = kv.Value;
        if (b != sopraBarra) { sopraBarra = b; if (b == null) suggerimento.Hide(this); else suggerimento.Show(b, this, e.X + S(12), e.Y + S(12), 4000); }
    }
    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        if (sopraBarra != null) { sopraBarra = null; suggerimento.Hide(this); }
        if (sopra != null || sopraTasto != Tasto.Nessuno) { sopra = null; sopraTasto = Tasto.Nessuno; Invalidate(); }
    }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); Clic(e.Location); }

    Tasto TastoSotto(Point p) {
        var t = Tasto.Nessuno;
        foreach (var kv in tasti) if (kv.Key.Contains(p)) t = kv.Value;   // «Togli tutte» sta sopra la riga del gruppo: vince l'ultimo disegnato
        return t;
    }

    // il clic, anche per le prove: lì non c'è il mouse
    public void Clic(Point p) {
        var t = TastoSotto(p);
        if (t == Tasto.Gruppo) { if (GruppoCliccato != null) GruppoCliccato(); return; }
        if (t == Tasto.TogliTutte) { if (TogliTutte != null) TogliTutte(); return; }
        if (t == Tasto.TogliQuesta) { if (TogliQuesta != null) TogliQuesta(); return; }
        foreach (var kv in righe) if (kv.Key.Contains(p) && RepoScelta != null) { RepoScelta(kv.Value); return; }
    }

    // dov'è un tasto, dopo l'ultimo disegno; per le prove
    public Rectangle ZonaDi(Tasto t) {
        foreach (var kv in tasti) if (kv.Value == t) return kv.Key;
        return Rectangle.Empty;
    }
    public List<string> NomiInTabella() { return righe.Select(x => x.Value.Nome).ToList(); }

    // per le prove: il testo delle carte della repo scelta, una stringa per carta
    public List<string> CartePerProva() { return Carte().Select(x => x.Titolo + " | " + x.Grande + " | " + x.Frase + " | " + string.Join(" | ", x.Righe.Select(r => string.Join(": ", r)))).ToList(); }

    // per le prove: le scritte di «Come sta andando» e del gruppo per la repo scelta, in tutte le loro forme
    public List<string> Disegnate() {
        var n = repo != null ? repo.Numeri : null;
        var l = new List<string> { Lingua.T("numeri.titolo"), Lingua.T("numeri.limiti"), Lingua.T("numeri.titolo_fonti"), Lingua.T("numeri.titolo_pagine"), Lingua.T("numeri.titolo_novita"), Lingua.T("numeri.titolo_visite_giorno"), Lingua.T("numeri.titolo_download_giorno"), Lingua.T("numeri.titolo_versioni"),
            Lingua.T("sparite.togli_tutte"), Lingua.T("sparite.togli_dall_elenco"), Lingua.T("sparite.frase_pagina"), Lingua.T("numeri.non_ancora_letti"), Lingua.T("numeri.nessun_dato"), Lingua.T("numeri.download_da_adesso"), Lingua.T("numeri.nessuna_versione"), Lingua.T("numeri.non_segui"), Lingua.T("numeri.niente_di_nuovo"),
            Lingua.T("numeri.didascalia_visite"), Lingua.T("numeri.didascalia_persone"), Lingua.T("numeri.didascalia_cloni"), Lingua.T("numeri.didascalia_download"), Lingua.T("numeri.didascalia_stelle"), Lingua.T("numeri.didascalia_copie"), Lingua.T("numeri.didascalia_osservatori"), Lingua.T("numeri.pagina_della_repo") };
        l.AddRange(COLONNE);
        l.Add(FraseNumeri(n, 3));
        l.AddRange(AvvisiNumeri(n));
        l.AddRange(Novita(n, new Conti { NuoveSegnalazioni = 2, NuoviCommenti = 1 }, DateTime.UtcNow.ToString("yyyy-MM-dd")));
        return l;
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
        if (repo.Sparita) y = RigaTogli(g, dis, m, y, w) + S(14);
        if (repo.Numeri != null || !repo.Sparita) y = Riquadro(g, dis, m, y, w) + S(14);

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

    public static string[] COLONNE { get { return new[] { Lingua.T("comune.repo"), Lingua.T("comune.chi_vede"), Lingua.T("panoramica.versione"), Lingua.T("numeri.colonna_visite"), Lingua.T("numeri.colonna_download"), Lingua.T("numeri.colonna_stelle"), Lingua.T("numeri.colonna_da_leggere"), Lingua.T("panoramica.errori") }; } }
    public static readonly int[] PESI = { 25, 9, 9, 13, 11, 9, 13, 8 };

    int SceneTutte(Graphics g, bool dis, int m, int y, int w) {
        var vive = elenco.Where(r => !r.Sparita).ToList();
        var fuori = elenco.Where(r => r.Sparita).ToList();
        int spar = fuori.Count;
        var chip = Problemi(vive, elenco, conti);
        string titolo = Lingua.N("panoramica.su_github", vive.Count) + (spar > 0 ? ", " + Lingua.N("comune.sparite", spar) : "");
        var daLeggere = DaLeggere(vive);
        var grigi = daLeggere.Concat(NumeriMancanti(vive)).ToList();
        y = Striscia(g, dis, m, y, w, titolo, FraseTutte(vive.Count, chip.Count, daLeggere), chip, grigi, Aspetto.TESTO, Aspetto.AMBRA) + S(14);

        int aTesta = S(44), aRiga = S(66), aGruppo = S(48), hTab = aTesta + aRiga * (vive.Count + (spariteAperte ? spar : 0)) + (spar > 0 ? aGruppo : 0);
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
            using (var n = new Pen(Color.FromArgb(235, 237, 241))) {
                Action<RepoInfo> riga = r => {
                    var zona = new Rectangle(m + 1, yy, w - 2, aRiga);
                    righe.Add(new KeyValuePair<Rectangle, RepoInfo>(zona, r));
                    if (r == sopra) using (var b = new SolidBrush(Aspetto.FONDINO)) g.FillRectangle(b, zona);
                    g.DrawLine(n, m + 1, yy, m + w - 2, yy);
                    RigaTabella(g, r, xs, yy, aRiga);
                    yy += aRiga;
                };
                foreach (var r in vive) riga(r);
                if (spar > 0) {
                    var zona = new Rectangle(m + 1, yy, w - 2, aGruppo);
                    tasti.Add(new KeyValuePair<Rectangle, Tasto>(zona, Tasto.Gruppo));
                    if (sopraTasto == Tasto.Gruppo) using (var b = new SolidBrush(Aspetto.FONDINO)) g.FillRectangle(b, zona);
                    g.DrawLine(n, m + 1, yy, m + w - 2, yy);
                    Freccia(g, xs[0], yy + aGruppo / 2, spariteAperte, Aspetto.GRIGIO);
                    Aspetto.T(g, Lingua.T("sparite.gruppo", "n", Lingua.Numero(spar)), Aspetto.FORTE, xs[0] + S(18), yy, S(240), aGruppo, Aspetto.GRIGIO);
                    TastoDisegnato(g, Lingua.T("sparite.togli_tutte"), m + w - pad, yy + (aGruppo - S(30)) / 2, S(30), Tasto.TogliTutte, true);
                    yy += aGruppo;
                    if (spariteAperte) foreach (var r in fuori) riga(r);
                }
            }
        }
        return y + hTab + S(20);
    }

    // le scritte in testa alla tabella: quello che manca su GitHub e quello che aspetta qui
    public static List<string> Problemi(List<RepoInfo> vive, List<RepoInfo> tutte, Func<string, Conti> quanti) {
        Func<RepoInfo, bool> senzaRel = r => r.Pannello != null && r.Pannello.ReleaseLetta && string.IsNullOrEmpty(r.Pannello.ReleaseTag);
        Func<RepoInfo, bool> conDopo = r => r.Pannello != null && r.Pannello.ReleaseLetta && !string.IsNullOrEmpty(r.Pannello.ReleaseTag) && r.Pannello.CommitDopo > 0;
        Func<RepoInfo, bool> senzaLic = r => r.Pannello != null && r.Pannello.LicenzaLetta && string.IsNullOrEmpty(r.Pannello.Licenza);
        Func<RepoInfo, bool> senzaRea = r => r.Pannello != null && r.Pannello.ReadmeLetto && !r.Pannello.Readme;
        var chip = new List<string>();
        Action<int, string> conta = (n, chiave) => { if (n > 0) chip.Add(Lingua.N(chiave, n)); };
        conta(vive.Count(senzaRel), "panoramica.senza_release");
        conta(vive.Count(conDopo), "panoramica.con_commit_dopo");
        conta(vive.Count(senzaLic), "panoramica.senza_licenza");
        conta(vive.Count(senzaRea), "panoramica.senza_readme");
        conta(tutte.Sum(r => Math.Max(0, quanti(r.Nome).Consegne)), "panoramica.consegne");
        conta(tutte.Sum(r => quanti(r.Nome).Quarantena), "panoramica.testi_quarantena");
        conta(tutte.Sum(r => quanti(r.Nome).Errori), "panoramica.errori");
        return chip;
    }

    // le repo su cui GitHub non ha dato i numeri: in grigio, perché non è una cosa da sistemare
    public static List<string> NumeriMancanti(IEnumerable<RepoInfo> vive) {
        int n = vive.Count(r => r.Numeri != null && (!r.Numeri.VisiteLette || !r.Numeri.DownloadLetti));
        return n > 0 ? new List<string> { Lingua.N("numeri.repo_senza_numeri", n) } : new List<string>();
    }

    // un numero nella tabella: mai letto non è uno 0; se la lettura di stavolta è fallita resta il numero di prima, in ambra
    public static Cella CellaNumero(NumeriRepo n, bool letto, string errore, long valore) {
        if (n == null) return new Cella { Testo = Lingua.T("panoramica.non_letto"), Colore = Aspetto.GRIGIO };
        if (!letto) return new Cella { Testo = errore != null ? Lingua.T("numeri.non_disponibile") : Lingua.T("panoramica.non_letto"), Colore = Aspetto.GRIGIO };
        return new Cella { Testo = Lingua.Numero(valore), Colore = errore != null ? Aspetto.AMBRA_S : Aspetto.INCHIOSTRO };
    }

    void RigaTabella(Graphics g, RepoInfo r, int[] xs, int y, int h) {
        var p = r.Pannello; var c = conti(r.Nome); var d = r.Numeri;
        Action<int, string, Color> cel = (i, s, col) => Aspetto.T(g, s, Aspetto.NORMALE, xs[i], y, xs[i + 1] - xs[i] - S(8), h, col);
        Color ink = Aspetto.INCHIOSTRO, gr = Aspetto.GRIGIO, am = Aspetto.AMBRA_S;
        int wn = xs[1] - xs[0] - S(8);
        Aspetto.T(g, CortoDi(r, login), Aspetto.FORTE, xs[0], y + S(7), wn, S(20), r.Sparita ? gr : ink);
        Aspetto.T(g, ProprietarioDi(r, login), Aspetto.PICCOLO, xs[0], y + S(27), wn, S(17), Aspetto.AZZURRO);
        string sotto = r.Sparita ? Lingua.T("comune.non_c_piu_github") : !r.Seguita ? Lingua.T("comune.non_seguita") : p == null || !p.CommitLetto ? "" : string.IsNullOrEmpty(p.UltimoCommit) ? Lingua.T("panoramica.vuota_nessun_commit") : Lingua.T("panoramica.ultimo_commit", "ultimocommit", Data(p.UltimoCommit));
        Aspetto.T(g, sotto, Aspetto.PICCOLO, xs[0], y + S(45), wn, S(17), r.Sparita ? am : gr);
        Aspetto.Pillola(g, r.Privata ? Lingua.T("comune.privata") : Lingua.T("comune.pubblica"), Aspetto.PICCOLO_F, xs[1], y + (h - S(20)) / 2, S(20), r.Privata ? Aspetto.FONDINO : Aspetto.VERDE_T, r.Privata ? gr : Aspetto.VERDE_S);
        if (r.Sparita && p == null) cel(2, "-", gr);
        else {
            bool rel = p != null && p.ReleaseLetta;
            cel(2, !rel ? Lingua.T("panoramica.non_letta_2") : string.IsNullOrEmpty(p.ReleaseTag) ? Lingua.T("comune.nessuna") : V(p.ReleaseTag), !rel ? gr : string.IsNullOrEmpty(p.ReleaseTag) ? am : ink);
        }
        if (r.Sparita && d == null) { cel(3, "-", gr); cel(4, "-", gr); cel(5, "-", gr); }
        else {
            Action<int, Cella> num = (i, x) => cel(i, x.Testo, x.Colore);
            num(3, d == null ? CellaNumero(null, false, null, 0) : CellaNumero(d, d.VisiteLette, d.VisiteErrore, d.Visite));
            num(4, d == null ? CellaNumero(null, false, null, 0) : CellaNumero(d, d.DownloadLetti, d.DownloadErrore, d.Download));
            num(5, d == null ? CellaNumero(null, false, null, 0) : CellaNumero(d, d.RepoLetta, d.RepoErrore, d.Stelle));
        }
        int leggere = c.Quarantena + c.Lavorare;
        cel(6, Lingua.Numero(leggere), c.Quarantena > 0 ? am : leggere > 0 ? Aspetto.AZZURRO : gr);
        cel(7, Lingua.Numero(c.Errori), c.Errori > 0 ? am : gr);
    }

    // ---------- le sparite ----------

    // la freccia del gruppo: a destra chiuso, in giù aperto
    static void Freccia(Graphics g, int x, int cy, bool aperto, Color c) {
        var punti = aperto ? new[] { new Point(x, cy - S(3)), new Point(x + S(10), cy - S(3)), new Point(x + S(5), cy + S(3)) }
                           : new[] { new Point(x + S(2), cy - S(5)), new Point(x + S(8), cy), new Point(x + S(2), cy + S(5)) };
        var v = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(c)) g.FillPolygon(b, punti);
        g.SmoothingMode = v;
    }

    // un tasto disegnato, non un controllo: la pagina scorre e si ridisegna tutta. x è il bordo sinistro, o quello destro con «aDestra»
    Rectangle TastoDisegnato(Graphics g, string testo, int x, int y, int alt, Tasto t, bool aDestra) {
        int w = Aspetto.Larg(g, testo, Aspetto.FORTE) + S(26);
        var r = new Rectangle(aDestra ? x - w : x, y, w, alt);
        Aspetto.Carta(g, r, S(7), sopraTasto == t ? Aspetto.FONDINO : Color.White, Aspetto.BORDO);
        TextRenderer.DrawText(g, testo, Aspetto.FORTE, r, Aspetto.INCHIOSTRO, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        tasti.Add(new KeyValuePair<Rectangle, Tasto>(r, t));
        return r;
    }

    int RigaTogli(Graphics g, bool dis, int m, int y, int w) {
        string testo = Lingua.T("sparite.togli_dall_elenco"), frase = Lingua.T("sparite.frase_pagina");
        int alt = S(34), lt = Aspetto.Larg(g, testo, Aspetto.FORTE) + S(26), lf = w - lt - S(14);
        int hf = Aspetto.Alt(g, frase, Aspetto.NORMALE, lf), h = Math.Max(alt, hf);
        if (dis) {
            TastoDisegnato(g, testo, m, y + (h - alt) / 2, alt, Tasto.TogliQuesta, false);
            Aspetto.TPiu(g, frase, Aspetto.NORMALE, m + lt + S(14), y + (h - hf) / 2, lf, hf + S(2), Aspetto.GRIGIO);
        }
        return y + h;
    }

    // ---------- «Come sta andando» ----------

    // la frase in testa: le visite sono dei 14 giorni di GitHub, coi suoi totali (gli unici di ogni giorno non si sommano); il totale dei download è di sempre
    public static string FraseNumeri(NumeriRepo n, int daLeggere) {
        string visite = n != null && n.VisiteLette ? Lingua.N("numeri.visite", n.Visite) + " " + Lingua.N("numeri.da_persone", n.VisiteUnici) : Lingua.T("numeri.visite_non_disponibili");
        string download = n != null && n.DownloadLetti ? Lingua.N("numeri.download", n.Download) : Lingua.T("numeri.download_non_disponibili");
        return Lingua.T("numeri.frase", "visite", visite, "download", download, "segnalazioni", Lingua.N("numeri.segnalazioni_da_leggere", daLeggere));
    }

    // un perché si dice una volta sola, con accanto tutto quello che riguarda; se c'è un dato vecchio si dice che è vecchio
    public static List<string> AvvisiNumeri(NumeriRepo n) {
        var uscita = new List<string>();
        if (n == null) return uscita;
        var parti = new[] {
            new { Cosa = Lingua.T("numeri.parte_visite"), Letto = n.VisiteLette, Errore = n.VisiteErrore },
            new { Cosa = Lingua.T("numeri.parte_cloni"), Letto = n.CloniLetti, Errore = n.CloniErrore },
            new { Cosa = Lingua.T("numeri.parte_fonti"), Letto = n.FontiLette, Errore = n.FontiErrore },
            new { Cosa = Lingua.T("numeri.parte_pagine"), Letto = n.PagineLette, Errore = n.PagineErrore },
            new { Cosa = Lingua.T("numeri.parte_download"), Letto = n.DownloadLetti, Errore = n.DownloadErrore },
            new { Cosa = Lingua.T("numeri.parte_stelle"), Letto = n.RepoLetta, Errore = n.RepoErrore } };
        foreach (var gruppo in parti.Where(p => p.Errore != null).GroupBy(p => new { p.Errore, p.Letto })) {
            string cosa = string.Join(", ", gruppo.Select(p => p.Cosa));
            cosa = char.ToUpper(cosa[0], CultureInfo.CurrentCulture) + cosa.Substring(1);
            uscita.Add(Lingua.T(gruppo.Key.Letto ? "numeri.non_riletti_perche" : "numeri.non_disponibili_perche", "cosa", cosa, "perche", gruppo.Key.Errore.TrimEnd('.', ' ')));
        }
        return uscita;
    }

    // quello che è cambiato e merita un'occhiata. oggi: yyyy-MM-dd in UTC
    public static List<string> Novita(NumeriRepo n, Conti c, string oggi) {
        var l = new List<string>();
        if (c.NuoveSegnalazioni > 0) l.Add(Lingua.N("numeri.nuove_segnalazioni", c.NuoveSegnalazioni));
        if (c.NuoviCommenti > 0) l.Add(Lingua.N("numeri.nuovi_commenti", c.NuoviCommenti));
        if (n == null) return l;
        Func<string, bool> recente = g => {
            DateTime d, o;
            return g != null && DateTime.TryParseExact(g, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                && DateTime.TryParseExact(oggi, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out o) && (o - d).TotalDays <= 14;
        };
        if (n.StellePrima >= 0 && recente(n.StelleCambiate)) l.Add(Lingua.T("numeri.stelle_cambiate", "da", Lingua.Numero(n.StellePrima), "a", Lingua.Numero(n.Stelle), "data", GiornoCorto(n.StelleCambiate)));
        if (n.CopiePrima >= 0 && recente(n.CopieCambiate)) l.Add(Lingua.T("numeri.copie_cambiate", "da", Lingua.Numero(n.CopiePrima), "a", Lingua.Numero(n.Copie), "data", GiornoCorto(n.CopieCambiate)));
        return l;
    }

    static string GiornoCorto(string giorno) {
        DateTime d;
        return DateTime.TryParseExact(giorno, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("dd/MM") : "";
    }

    // lo stesso codice misura e disegna: prima si misura per sapere quanto è alto il fondo bianco
    int Riquadro(Graphics g, bool dis, int x, int y, int w) {
        int fine = Contenuto(g, false, x, y, w);
        if (!dis) return fine;
        Aspetto.Carta(g, new Rectangle(x, y, w, fine - y), S(10), Color.White, Aspetto.BORDO);
        return Contenuto(g, true, x, y, w);
    }

    int Contenuto(Graphics g, bool dis, int x, int y, int w) {
        var n = repo.Numeri;
        var c = conti(repo.Nome);
        int pad = S(16), lt = w - pad * 2, xt = x + pad, yy = y + S(14);
        string quando = n != null && n.LettoIl != null ? Lingua.T("numeri.letti_il", "data", n.LettoIl) : "";
        if (dis) {
            Aspetto.T(g, Lingua.T("numeri.titolo"), Aspetto.FORTE, xt, yy, lt - Aspetto.Larg(g, quando, Aspetto.PICCOLO) - S(10), S(20), Aspetto.AZZURRO);
            if (quando.Length > 0) Aspetto.TDestra(g, quando, Aspetto.PICCOLO, xt + lt, yy, S(20), Aspetto.GRIGIO);
        }
        yy += S(24);
        if (n == null) {
            string ancora = Lingua.T("numeri.non_ancora_letti");
            int ha = Aspetto.Alt(g, ancora, Aspetto.NORMALE, lt);
            if (dis) Aspetto.TPiu(g, ancora, Aspetto.NORMALE, xt, yy, lt, ha + S(2), Aspetto.GRIGIO);
            return yy + ha + S(14);
        }
        string frase = FraseNumeri(n, c.Quarantena + c.Lavorare);
        int hf = Aspetto.Alt(g, frase, Aspetto.MEDIO, lt);
        if (dis) Aspetto.TPiu(g, frase, Aspetto.MEDIO, xt, yy, lt, hf + S(2), Aspetto.INCHIOSTRO);
        yy += hf + S(12);

        // i sette numeri in fila; quello che non si è letto ha il trattino, non uno 0
        var conte = new[] {
            new { Cosa = Lingua.T("numeri.didascalia_visite"), Letto = n.VisiteLette, Errore = n.VisiteErrore, Quanti = n.Visite },
            new { Cosa = Lingua.T("numeri.didascalia_persone"), Letto = n.VisiteLette, Errore = n.VisiteErrore, Quanti = n.VisiteUnici },
            new { Cosa = Lingua.T("numeri.didascalia_cloni"), Letto = n.CloniLetti, Errore = n.CloniErrore, Quanti = n.Cloni },
            new { Cosa = Lingua.T("numeri.didascalia_download"), Letto = n.DownloadLetti, Errore = n.DownloadErrore, Quanti = n.Download },
            new { Cosa = Lingua.T("numeri.didascalia_stelle"), Letto = n.RepoLetta, Errore = n.RepoErrore, Quanti = n.Stelle },
            new { Cosa = Lingua.T("numeri.didascalia_copie"), Letto = n.RepoLetta, Errore = n.RepoErrore, Quanti = n.Copie },
            new { Cosa = Lingua.T("numeri.didascalia_osservatori"), Letto = n.RepoLetta, Errore = n.RepoErrore, Quanti = n.Osservatori } };
        int lc = lt / conte.Length;
        if (dis) for (int i = 0; i < conte.Length; i++) {
            var k = conte[i];
            Aspetto.T(g, k.Letto ? Lingua.Numero(k.Quanti) : "-", Aspetto.GRANDE, xt + i * lc, yy, lc - S(6), S(30), !k.Letto ? Aspetto.GRIGIO : k.Errore != null ? Aspetto.AMBRA_S : Aspetto.INCHIOSTRO);
            Aspetto.T(g, k.Cosa, Aspetto.PICCOLO, xt + i * lc, yy + S(30), lc - S(6), S(18), Aspetto.GRIGIO);
        }
        yy += S(48) + S(10);

        foreach (var av in AvvisiNumeri(n)) {
            int ha = Aspetto.Alt(g, av, Aspetto.PICCOLO, lt);
            if (dis) Aspetto.TPiu(g, av, Aspetto.PICCOLO, xt, yy, lt, ha + S(2), Aspetto.AMBRA_S);
            yy += ha + S(6);
        }
        yy += S(6);

        yy = Affianca(xt, yy, lt, (px, py, pw) => BloccoGiorni(g, dis, px, py, pw, n), (px, py, pw) => BloccoVersioni(g, dis, px, py, pw, n)) + S(14);
        yy = Affianca(xt, yy, lt,
            (px, py, pw) => BloccoVoci(g, dis, px, py, pw, Lingua.T("numeri.titolo_fonti"), n.FontiLette, n.Fonti, null),
            (px, py, pw) => BloccoVoci(g, dis, px, py, pw, Lingua.T("numeri.titolo_pagine"), n.PagineLette, n.Pagine, repo.Nome)) + S(14);

        yy = Sezione(g, dis, xt, yy, lt, Lingua.T("numeri.titolo_novita"));
        var nuove = Novita(n, c, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        if (!repo.Seguita) nuove.Add(Lingua.T("numeri.non_segui"));
        if (nuove.Count == 0) nuove.Add(Lingua.T("numeri.niente_di_nuovo"));
        foreach (var r in nuove) {
            int ha = Aspetto.Alt(g, r, Aspetto.NORMALE, lt);
            if (dis) Aspetto.TPiu(g, r, Aspetto.NORMALE, xt, yy, lt, ha + S(2), Aspetto.INCHIOSTRO);
            yy += ha + S(4);
        }
        yy += S(10);

        string limiti = Lingua.T("numeri.limiti");
        int hl = Aspetto.Alt(g, limiti, Aspetto.PICCOLO, lt);
        if (dis) Aspetto.TPiu(g, limiti, Aspetto.PICCOLO, xt, yy, lt, hl + S(2), Aspetto.GRIGIO);
        return yy + hl + S(14);
    }

    // due blocchi uno accanto all'altro se c'è posto, se no uno sotto l'altro
    int Affianca(int x, int y, int w, Func<int, int, int, int> a, Func<int, int, int, int> b) {
        int gap = S(24), col = w >= S(640) ? 2 : 1, lc = (w - gap * (col - 1)) / col;
        int f1 = a(x, y, lc);
        int f2 = col == 2 ? b(x + lc + gap, y, lc) : b(x, f1 + S(12), lc);
        return Math.Max(f1, f2);
    }

    int Sezione(Graphics g, bool dis, int x, int y, int w, string titolo) {
        if (dis) Aspetto.T(g, titolo, Aspetto.PICCOLO_F, x, y, w, S(18), Aspetto.GRIGIO);
        return y + S(22);
    }

    int RigaVoce(Graphics g, bool dis, int x, int y, int w, string nome, string valore) {
        if (dis) {
            using (var n = new Pen(Color.FromArgb(235, 237, 241))) g.DrawLine(n, x, y, x + w, y);
            int wv = Math.Min(Aspetto.Larg(g, valore, Aspetto.FORTE), w * 3 / 5);
            Aspetto.T(g, nome, Aspetto.NORMALE, x, y, w - wv - S(10), S(24), Aspetto.INCHIOSTRO);
            Aspetto.T(g, valore, Aspetto.FORTE, x + w - wv, y, wv + 1, S(24), Aspetto.INCHIOSTRO);
        }
        return y + S(24);
    }

    int Frasetta(Graphics g, bool dis, int x, int y, int w, string testo) {
        int h = Aspetto.Alt(g, testo, Aspetto.NORMALE, w);
        if (dis) Aspetto.TPiu(g, testo, Aspetto.NORMALE, x, y, w, h + S(2), Aspetto.GRIGIO);
        return y + h;
    }

    // visite sopra, download sotto: un grafico per numero, ognuno con la sua scala
    int BloccoGiorni(Graphics g, bool dis, int x, int y, int w, NumeriRepo n) {
        var visite = giorni.Where(d => d.Visite >= 0).Select(d => new GiornoTraffico { Giorno = d.Giorno, Conta = d.Visite }).ToList();
        if (visite.Count == 0 && n.VisiteLette && n.VisiteGiorni != null) visite = n.VisiteGiorni;
        y = Sezione(g, dis, x, y, w, Lingua.T("numeri.titolo_visite_giorno"));
        if (visite.Count == 0) y = Frasetta(g, dis, x, y, w, n.VisiteLette ? Lingua.T("numeri.nessun_dato") : Lingua.T("numeri.non_disponibile"));
        else y = Barre(g, dis, x, y, w, visite, v => Lingua.N("numeri.visite", v));
        y += S(12);
        y = Sezione(g, dis, x, y, w, Lingua.T("numeri.titolo_download_giorno"));
        var scaricati = Storico.DownloadPerGiorno(giorni);
        if (scaricati.Count >= 2) return Barre(g, dis, x, y, w, scaricati, v => Lingua.N("numeri.download", v));
        return Frasetta(g, dis, x, y, w, Lingua.T("numeri.download_da_adesso"));
    }

    // un solo numero, una sola scala che parte da 0, l'ultimo mese al massimo. Un giorno senza dato resta vuoto: nessuna barra e nessun numero
    int Barre(Graphics g, bool dis, int x, int y, int w, List<GiornoTraffico> dati, Func<long, string> dice) {
        int alt = S(58);
        if (!dis) return y + alt + S(20);
        var perGiorno = new Dictionary<string, long>();
        foreach (var d in dati) perGiorno[d.Giorno] = d.Conta;
        var date = dati.Select(d => DateTime.ParseExact(d.Giorno, "yyyy-MM-dd", CultureInfo.InvariantCulture)).OrderBy(d => d).ToList();
        DateTime ultimo = date[date.Count - 1], primo = date[0];
        if ((ultimo - primo).TotalDays > 29) primo = ultimo.AddDays(-29);
        int quanti = (int)(ultimo - primo).TotalDays + 1;
        long massimo = Math.Max(1, perGiorno.Where(kv => string.CompareOrdinal(kv.Key, primo.ToString("yyyy-MM-dd")) >= 0).Select(kv => kv.Value).DefaultIfEmpty(0).Max());
        int passo = Math.Max(S(4), Math.Min(S(26), w / quanti)), spazio = S(2), larga = Math.Max(S(2), passo - spazio), baseY = y + alt;
        for (int i = 0; i < quanti; i++) {
            string giorno = primo.AddDays(i).ToString("yyyy-MM-dd");
            long v;
            if (!perGiorno.TryGetValue(giorno, out v)) continue;
            int bx = x + i * passo;
            barre.Add(new KeyValuePair<Rectangle, string>(new Rectangle(bx, y, passo, alt), primo.AddDays(i).ToString("dd/MM") + ": " + dice(v)));
            if (v == 0) continue;
            int h = Math.Max(S(3), (int)(v * (alt - S(2)) / massimo));
            Aspetto.Pieno(g, new Rectangle(bx, baseY - h, larga, h), Math.Min(S(3), larga / 2), Aspetto.AZZURRO);
            using (var b = new SolidBrush(Aspetto.AZZURRO)) g.FillRectangle(b, bx, baseY - Math.Min(h, S(3)), larga, Math.Min(h, S(3)));
        }
        int finePlot = x + quanti * passo;
        using (var n = new Pen(Aspetto.BORDO)) g.DrawLine(n, x, baseY, finePlot, baseY);
        Aspetto.T(g, primo.ToString("dd/MM"), Aspetto.PICCOLO, x, baseY + S(3), S(50), S(16), Aspetto.GRIGIO);
        Aspetto.TDestra(g, ultimo.ToString("dd/MM"), Aspetto.PICCOLO, finePlot, baseY + S(3), S(16), Aspetto.GRIGIO);
        string mx = Lingua.T("numeri.massimo", "n", Lingua.Numero(massimo));
        int lm = Aspetto.Larg(g, mx, Aspetto.PICCOLO);
        Aspetto.T(g, mx, Aspetto.PICCOLO, x + (finePlot - x - lm) / 2, baseY + S(3), lm + 1, S(16), Aspetto.GRIGIO);
        return baseY + S(20);
    }

    int BloccoVersioni(Graphics g, bool dis, int x, int y, int w, NumeriRepo n) {
        y = Sezione(g, dis, x, y, w, Lingua.T("numeri.titolo_versioni"));
        if (!n.DownloadLetti) return Frasetta(g, dis, x, y, w, Lingua.T("numeri.non_disponibile"));
        if (n.Versioni == null || n.Versioni.Count == 0) return Frasetta(g, dis, x, y, w, Lingua.T("numeri.nessuna_versione"));
        foreach (var v in n.Versioni.Take(6)) {
            string nome = V(v.Tag) + (v.Allegati.Count == 1 ? " · " + V(v.Allegati[0].Nome) : v.Allegati.Count > 1 ? " · " + Lingua.N("pubblica.n_file", v.Allegati.Count) : "");
            y = RigaVoce(g, dis, x, y, w, nome, Lingua.Numero(v.Download));
        }
        if (n.Versioni.Count > 6) y = Frasetta(g, dis, x, y + S(4), w, Lingua.N("numeri.altre_versioni", n.Versioni.Count - 6));
        return y;
    }

    // da dove arriva la gente e quali pagine guarda. togli: il pezzo «/proprietario/repo» davanti ai percorsi, che è sempre uguale
    int BloccoVoci(Graphics g, bool dis, int x, int y, int w, string titolo, bool lette, List<VoceTraffico> voci, string togli) {
        y = Sezione(g, dis, x, y, w, titolo);
        if (!lette) return Frasetta(g, dis, x, y, w, Lingua.T("numeri.non_disponibile"));
        if (voci == null || voci.Count == 0) return Frasetta(g, dis, x, y, w, Lingua.T("numeri.nessun_dato"));
        foreach (var v in voci.Take(5)) {
            string nome = v.Nome ?? "";
            if (togli != null && nome.StartsWith("/" + togli, StringComparison.OrdinalIgnoreCase)) nome = nome.Substring(togli.Length + 1);
            if (togli != null && nome.Trim('/').Length == 0) nome = Lingua.T("numeri.pagina_della_repo");
            y = RigaVoce(g, dis, x, y, w, V(nome), Lingua.N("numeri.visite", v.Conta) + ", " + Lingua.N("numeri.persone", v.Unici));
        }
        return y;
    }
}
