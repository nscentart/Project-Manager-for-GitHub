// ProvaSparite.cs (Project Manager 1.1, 03/11/2026): togliere dall'elenco le repo che GitHub non elenca più. Prima i conti, poi la finestra
// vera, fuori schermo, con gli stessi clic di chi la usa. SuCopia è la prova sulla copia di un archivio vero: scrive solo nomi e conteggi.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class ProvaSparite {
    static void Mostra(Form f) {
        f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000);
        f.Show();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(10); }
    }

    // la pagina non è sullo schermo: si disegna qui, così le zone dei tasti esistono come per chi la guarda
    static void Disegna(Finestra f) {
        var p = f.Panoramica;
        using (var b = new Bitmap(Math.Max(1, p.Width), Math.Max(1, p.Height))) p.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
        Application.DoEvents();
    }

    static void Clic(Finestra f, PaginaPanoramica.Tasto t) {
        Disegna(f);
        var z = f.Panoramica.ZonaDi(t);
        f.Panoramica.Clic(new Point(z.X + z.Width / 2, z.Y + z.Height / 2));
        Application.DoEvents();
    }

    static List<RigaRepo> Righe(Finestra f) { return f.Colonna.Items.Cast<RigaRepo>().ToList(); }
    static int Gruppo(Finestra f) { return Righe(f).FindIndex(r => r.Gruppo); }

    static Archivio Esempio() {
        Func<string, bool, RepoInfo> repo = (n, via) => new RepoInfo { Nome = n, Proprietario = n.Split('/')[0], Mia = true, Sparita = via, Conti = new List<string> { "mario" } };
        return new Archivio {
            Login = "mario", DominiFidati = Dati.FIDATI.ToList(), CartellaRepo = "",
            Conti = new List<ContoGitHub> { new ContoGitHub { Login = "mario", Origine = Accessi.BROWSER } },
            Repo = new List<RepoInfo> { repo("mario/viva", false), repo("mario/altra", false), repo("mario/via1", true), repo("mario/via2", true), repo("mario/via3", true) },
            Elementi = new List<Elemento> {
                new Elemento { Chiave = "issue:1", Repo = "mario/via1", Numero = 1, Tipo = "issue", Titolo = "a", Autore = "x", Testo = "a", Url = "https://github.com/mario/via1/issues/1", Creato = "2026-10-01T00:00:00Z", Aggiornato = "2026-10-01T00:00:00Z", Aperta = "open" },
                new Elemento { Chiave = "commento:2", Repo = "mario/via1", Numero = 1, Tipo = "commento", Titolo = "a", Autore = "x", Testo = "b", Url = "https://github.com/mario/via1/issues/1", Creato = "2026-10-01T00:00:00Z", Aggiornato = "2026-10-01T00:00:00Z", Aperta = "open" },
                new Elemento { Chiave = "issue:3", Repo = "mario/viva", Numero = 3, Tipo = "issue", Titolo = "c", Autore = "x", Testo = "c", Url = "https://github.com/mario/viva/issues/3", Creato = "2026-10-01T00:00:00Z", Aggiornato = "2026-10-01T00:00:00Z", Aperta = "open" } },
            Decisioni = new List<Decisione> { new Decisione { Chiave = "issue:1", Impronta = "x", Stato = "ignorato" }, new Decisione { Chiave = "issue:3", Impronta = "y", Stato = "ignorato" } } };
    }

    static ArchivioStorico StoricoEsempio() {
        var s = new ArchivioStorico();
        s.Repo["mario/via1"] = new List<GiornoStorico> { new GiornoStorico { Giorno = "2026-10-01", Visite = 1 }, new GiornoStorico { Giorno = "2026-10-02", Visite = 2 } };
        s.Repo["mario/viva"] = new List<GiornoStorico> { new GiornoStorico { Giorno = "2026-10-01", Visite = 1 } };
        return s;
    }

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPM_DATI"))) { File.WriteAllText(esito, "SBAGLIA serve una cartella dati di prova\n"); return 1; }
        Lingua.Usa("it");
        Dialogo.Silenzioso = true;
        Finestra.SoloFoto = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try {
            // ---------- i conti ----------
            var a = Esempio(); var s = StoricoEsempio();
            var conta = Accessi.ContaTogli(a, s, new[] { "mario/via1", "MARIO/VIA1", "mario/via2" });
            v("conta: una volta sola per repo", conta.Repo.Count == 2);
            v("conta: testi, decisioni e giorni", conta.Testi == 2 && conta.Decisioni == 1 && conta.GiorniStorico == 2);
            v("conta: non cambia niente", a.Repo.Count == 5 && a.Elementi.Count == 3 && a.Decisioni.Count == 2 && s.Repo.Count == 2);
            var viva = Accessi.ContaTogli(a, s, new[] { "mario/viva" });
            v("conta: una viva non si può togliere", viva.Repo.Count == 0 && viva.Testi == 0);
            var fatto = Accessi.TogliSparite(a, s, new[] { "mario/via1", "mario/viva", "mario/inesistente" });
            v("togli: solo la sparita", fatto.Repo.SequenceEqual(new[] { "mario/via1" }) && a.Repo.Any(r => r.Nome == "mario/viva"));
            v("togli: la riga va via", a.Repo.Count == 4 && !a.Repo.Any(r => r.Nome == "mario/via1"));
            v("togli: i suoi testi vanno via, gli altri restano", a.Elementi.Count == 1 && a.Elementi[0].Repo == "mario/viva");
            v("togli: le sue decisioni vanno via, le altre restano", a.Decisioni.Count == 1 && a.Decisioni[0].Chiave == "issue:3");
            v("togli: il suo storico va via, gli altri restano", !s.Repo.ContainsKey("mario/via1") && s.Repo.ContainsKey("mario/viva"));

            // ---------- una repo che torna ----------
            Func<string, RepoInfo> daGitHub = n => new RepoInfo { Nome = n, Proprietario = n.Split('/')[0], Mia = true };
            var nuove = new List<RepoInfo> { daGitHub("mario/viva"), daGitHub("mario/altra"), daGitHub("mario/via1"), daGitHub("mario/via2") };
            int spar = Accessi.Unisci(a, "mario", nuove, n => n.Mia);
            var torna = a.Repo.First(r => r.Nome == "mario/via1");
            v("torna: quella tolta rientra da sola, come nuova", !torna.Sparita && torna.Dal == null && torna.Numeri == null && torna.Seguita);
            v("torna: quella solo segnata non è più sparita", !a.Repo.First(r => r.Nome == "mario/via2").Sparita);
            v("torna: quella che manca ancora resta sparita", a.Repo.First(r => r.Nome == "mario/via3").Sparita && spar == 1);

            // ---------- la finestra vera ----------
            a = Esempio();
            Dati.Salva(a); Storico.Salva(StoricoEsempio());
            var f = new Finestra();
            Mostra(f);
            var stati = Righe(f);
            v("il gruppo è chiuso in partenza", !f.SparitePerProva);
            int g = Gruppo(f);
            v("a sinistra: Tutte, le vive, poi il gruppo", stati.Count == 4 && g == 3);
            v("il gruppo dice quante sono", stati[g].Nome == "Sparite (3)" && stati[g].R == null);
            Disegna(f);
            v("tabella: chiuso si vedono solo le vive", f.Panoramica.NomiInTabella().Count == 2 && f.Panoramica.NomiInTabella().All(n => n == "mario/viva" || n == "mario/altra"));
            v("tabella: la riga del gruppo c'è", !f.Panoramica.ZonaDi(PaginaPanoramica.Tasto.Gruppo).IsEmpty);
            v("tabella: «Togli tutte» c'è anche da chiuso", !f.Panoramica.ZonaDi(PaginaPanoramica.Tasto.TogliTutte).IsEmpty);

            f.Colonna.SelectedIndex = 0;
            f.Colonna.ClicSu(g);
            Application.DoEvents();
            v("clic a sinistra: si apre", f.SparitePerProva && Righe(f).Count == 7);
            v("clic a sinistra: la scelta non si muove", f.Colonna.SelectedIndex == 0 && f.SceltaPerProva == null);
            v("clic a sinistra: la riga dice aperto", Righe(f)[g].Aperto);
            f.Colonna.ClicSu(g);
            Application.DoEvents();
            v("clic a sinistra: si richiude", !f.SparitePerProva && Righe(f).Count == 4);

            Clic(f, PaginaPanoramica.Tasto.Gruppo);
            v("clic in tabella: si apre anche a sinistra", f.SparitePerProva && Righe(f).Count == 7);
            Disegna(f);
            v("tabella: da aperto ci sono tutte", f.Panoramica.NomiInTabella().Count == 5);
            Clic(f, PaginaPanoramica.Tasto.Gruppo);
            v("clic in tabella: si richiude", !f.SparitePerProva);

            // con le frecce
            f.Colonna.SelectedIndex = 2;
            Application.DoEvents();
            f.Colonna.SelectedIndex = g;
            Application.DoEvents();
            v("freccia in giù sul gruppo: entra nella prima sparita", f.SparitePerProva && f.SceltaPerProva != null && f.SceltaPerProva.Sparita);
            f.Colonna.SelectedIndex = g;
            Application.DoEvents();
            v("freccia in su sul gruppo: torna all'ultima viva", f.SceltaPerProva != null && !f.SceltaPerProva.Sparita);
            f.Colonna.SelectedIndex = 0;
            Application.DoEvents();

            // la pagina di una sparita
            f.FotoRepo("mario/via1");
            Application.DoEvents();
            v("la sparita scelta: il gruppo è aperto", f.SparitePerProva && f.SceltaPerProva.Nome == "mario/via1");
            Disegna(f);
            v("pagina della sparita: c'è il tasto", !f.Panoramica.ZonaDi(PaginaPanoramica.Tasto.TogliQuesta).IsEmpty);
            f.Colonna.ClicSu(Gruppo(f));
            Application.DoEvents();
            v("chiudendo con una sparita scelta: si torna a Tutte", !f.SparitePerProva && f.SceltaPerProva == null);
            f.FotoRepo("mario/via1");
            Application.DoEvents();

            // annulla
            FinestraTogli vista = null; string racconto = null; bool annullaPrima = false, nessunInvio = false;
            Finestra.RispostaTogli = x => { vista = (FinestraTogli)x; racconto = vista.Racconto; annullaPrima = vista.CancelButton == vista.BAnnulla; nessunInvio = vista.AcceptButton == null; return DialogResult.Cancel; };
            Clic(f, PaginaPanoramica.Tasto.TogliQuesta);
            v("conferma: si apre", vista != null);
            v("conferma: su GitHub non cambia niente", racconto != null && racconto.Contains("Su GitHub non cambia niente"));
            v("conferma: quanti testi e quanto storico", racconto != null && racconto.Contains("2 testi scaricati") && racconto.Contains("1 decisione presa") && racconto.Contains("2 giorni di storico"));
            v("conferma: se torna, torna da sola", racconto != null && racconto.Contains("torna da sola"));
            v("conferma: Annulla di partenza, Invio non toglie", annullaPrima && nessunInvio);
            v("annulla: non toglie niente", Dati.Carica().Repo.Count == 5 && Dati.Carica().Elementi.Count == 3);
            v("annulla: la pagina resta", f.SceltaPerProva != null && f.SceltaPerProva.Nome == "mario/via1");

            // occupato
            int apertaOccupato = 0;
            Finestra.RispostaTogli = x => { apertaOccupato++; return DialogResult.OK; };
            f.OccupatoPerProva(true);
            Clic(f, PaginaPanoramica.Tasto.TogliQuesta);
            v("mentre aggiorna: rifiutato", apertaOccupato == 0 && f.Stato.Text == Lingua.T("comune.aspetta_che_finisca_lavoro_corso"));
            v("mentre aggiorna: non toglie niente", Dati.Carica().Repo.Count == 5);
            f.OccupatoPerProva(false);

            // conferma
            Finestra.RispostaTogli = x => DialogResult.OK;
            Clic(f, PaginaPanoramica.Tasto.TogliQuesta);
            var dopo = Dati.Carica();
            v("conferma: la repo va via e si salva", dopo.Repo.Count == 4 && !dopo.Repo.Any(r => r.Nome == "mario/via1"));
            v("conferma: i testi vanno via e si salva", dopo.Elementi.Count == 1 && dopo.Decisioni.Count == 1);
            v("conferma: lo storico va via e si salva", Storico.QuantiGiorni(Storico.Carica(), "mario/via1") == 0 && Storico.QuantiGiorni(Storico.Carica(), "mario/viva") == 1);
            v("conferma: la scelta non resta su una repo tolta", f.SceltaPerProva == null);
            v("conferma: il messaggio", f.Stato.Text == "1 repo tolta dall'elenco.");

            // togli tutte: dal gruppo, anche chiuso
            f.FotoSparite(false);
            int quante = 0;
            Finestra.RispostaTogli = x => { quante = ((FinestraTogli)x).BTogli.Text == "Togli tutte" ? 1 : 0; return DialogResult.OK; };
            Clic(f, PaginaPanoramica.Tasto.TogliTutte);
            dopo = Dati.Carica();
            v("togli tutte: restano le vive, esattamente", dopo.Repo.Count == 2 && dopo.Repo.All(r => !r.Sparita));
            v("togli tutte: il tasto si chiama così", quante == 1);
            v("togli tutte: il gruppo sparisce", Gruppo(f) < 0 && Righe(f).Count == 3 && !f.SparitePerProva);
            Disegna(f);
            v("togli tutte: in tabella non resta il gruppo", f.Panoramica.ZonaDi(PaginaPanoramica.Tasto.Gruppo).IsEmpty && f.Panoramica.ZonaDi(PaginaPanoramica.Tasto.TogliTutte).IsEmpty);
            f.Close();

            // una sparita che un filtro nasconde non si tocca: «Togli tutte» agisce su quello che si vede
            a = Esempio();
            a.Conti.Add(new ContoGitHub { Login = "altro", Origine = Accessi.BROWSER });
            a.Repo.First(r => r.Nome == "mario/via3").Conti = new List<string> { "altro" };
            Dati.Salva(a);
            f = new Finestra();
            Mostra(f);
            Finestra.RispostaTogli = x => DialogResult.OK;
            Clic(f, PaginaPanoramica.Tasto.TogliTutte);
            dopo = Dati.Carica();
            v("togli tutte: una sparita di un altro account non si tocca", dopo.Repo.Any(r => r.Nome == "mario/via3") && !dopo.Repo.Any(r => r.Nome == "mario/via1"));
            f.Close();
            Finestra.RispostaTogli = null;

            // ---------- con tante repo la colonna resta dove l'hai lasciata ----------
            a = Esempio();
            for (int i = 0; i < 80; i++) a.Repo.Add(new RepoInfo { Nome = "mario/tanta" + i.ToString("00"), Proprietario = "mario", Mia = true, Conti = new List<string> { "mario" } });
            Dati.Salva(a);
            f = new Finestra();
            Mostra(f);
            f.Colonna.TopIndex = Gruppo(f);
            Application.DoEvents();
            int cima = f.Colonna.TopIndex;
            v("tante repo: la colonna si può scorrere", cima > 10);
            f.Colonna.ClicSu(Gruppo(f));
            Application.DoEvents();
            v("tante repo: aprendo il gruppo la colonna non torna in cima", f.SparitePerProva && f.Colonna.TopIndex == cima);
            f.Colonna.ClicSu(Gruppo(f));
            Application.DoEvents();
            v("tante repo: chiudendo il gruppo nemmeno", !f.SparitePerProva && f.Colonna.TopIndex == cima);
            f.Close();

            // ---------- tutto il giro, passando da «Aggiorna», con il GitHub finto ----------
            using (var srv = new ServerFinto()) {
                const string CHIAVE = "chiave-finta-sparite";
                srv.Chi[CHIAVE] = "mario"; srv.Permessi[CHIAVE] = "repo, read:org";
                GitHub.Usa(CHIAVE, "mario");
                GitHub.SuServerDiProva(srv.Porta);
                Func<string, ServerFinto.RepoF> crea = n => { var x = srv.Nuova(n, "User", "admin", false); srv.Metti(x, "README.md", Encoding.UTF8.GetBytes("# x")); return x; };
                var rVera = crea("mario/vera");
                rVera.Stelle = 4;
                rVera.Traffico["/traffic/views"] = ServerFinto.D("count", 10, "uniques", 4, "views", new object[] { ServerFinto.D("timestamp", "2026-10-20T00:00:00Z", "count", 10, "uniques", 4) });
                rVera.Traffico["/traffic/clones"] = ServerFinto.D("count", 1, "uniques", 1, "clones", new object[] { ServerFinto.D("timestamp", "2026-10-20T00:00:00Z", "count", 1, "uniques", 1) });
                rVera.Traffico["/traffic/popular/referrers"] = new object[0];
                rVera.Traffico["/traffic/popular/paths"] = new object[0];
                crea("mario/muta");                     // nessun permesso sul traffico: 403
                crea("mario/guasta");                   // le release non si leggono
                crea("mario/tornera");
                srv.ForzaVolte("GET", "/repos/mario/guasta/releases", 500, "{}", null, 999);
                Func<string[], string> elenco = nomi => "[" + string.Join(",", nomi.Select(x => "{\"full_name\":\"" + x + "\",\"description\":\"\",\"default_branch\":\"main\",\"private\":false,\"owner\":{\"login\":\"mario\",\"type\":\"User\"},\"permissions\":{\"admin\":true}}")) + "]";
                Dati.Salva(new Archivio { Login = "mario", DominiFidati = Dati.FIDATI.ToList(), CartellaRepo = "", Conti = new List<ContoGitHub> { new ContoGitHub { Login = "mario", Origine = Accessi.BROWSER } },
                    Repo = new List<RepoInfo> { new RepoInfo { Nome = "mario/tornera", Proprietario = "mario", Mia = true, Sparita = true, Conti = new List<string> { "mario" } } } });
                foreach (var x in Directory.GetFiles(Dati.Cartella, "storico.json*")) File.Delete(x);
                f = new Finestra();
                Mostra(f);
                Action<string[]> aggiorna = nomi => {
                    srv.Forza("GET", "/user/repos", 200, elenco(nomi), null);
                    f.AggiornaPerProva();
                    for (int i = 0; i < 1500 && f.OccupatoOra; i++) { Application.DoEvents(); Thread.Sleep(10); }
                    Application.DoEvents();
                };
                aggiorna(new[] { "mario/vera", "mario/muta", "mario/guasta", "mario/tornera" });
                v("Aggiorna: finisce", !f.OccupatoOra);
                dopo = Dati.Carica();
                Func<string, RepoInfo> di = n => dopo.Repo.FirstOrDefault(r => r.Nome == n);
                v("Aggiorna: una repo sparita che torna, torna da sola", !di("mario/tornera").Sparita);
                v("Aggiorna: i numeri restano nell'archivio", di("mario/vera").Numeri.Visite == 10 && di("mario/vera").Numeri.Stelle == 4);
                var suDisco = Storico.Copia(Storico.Carica(), "mario/vera");
                v("Aggiorna: lo storico è scritto sul disco", suDisco.Any(x => x.Giorno == "2026-10-20" && x.Visite == 10) && suDisco.Any(x => x.Giorno == DateTime.UtcNow.ToString("yyyy-MM-dd") && x.Download == 0) && !File.Exists(Path.Combine(Dati.Cartella, "storico.json.nuovo")));
                v("Aggiorna: 403 sul traffico: non disponibile, il resto c'è", di("mario/muta").Numeri.VisiteErrore == Lingua.T("github.traffico_solo_chi_scrive") && !di("mario/muta").Numeri.VisiteLette && di("mario/muta").Numeri.RepoLetta);
                v("Aggiorna: 500 sulle release: il resto c'è", di("mario/guasta").Numeri.DownloadErrore != null && di("mario/guasta").Numeri.RepoLetta && PaginaPanoramica.LettaTutta(di("mario/guasta").Pannello));
                v("Aggiorna: i numeri che mancano non fanno fallire la lettura", f.Stato.Text.StartsWith("Aggiornato") && !f.Stato.Text.Contains("fallite") && !f.Stato.Text.Contains("letta a metà"));
                Disegna(f);
                v("tabella: dopo Aggiorna ci sono tutte", f.Panoramica.NomiInTabella().Count == 4);

                aggiorna(new[] { "mario/vera", "mario/muta", "mario/guasta" });
                dopo = Dati.Carica();
                v("Aggiorna: una che non c'è più va nel gruppo", di("mario/tornera").Sparita && Gruppo(f) >= 0 && Righe(f)[Gruppo(f)].Nome == "Sparite (1)");
                v("Aggiorna: i numeri della sparita non si cercano più", srv.Di("GET", "/repos/mario/tornera/traffic/views").Count == 1);
                Finestra.RispostaTogli = x => DialogResult.OK;
                Clic(f, PaginaPanoramica.Tasto.TogliTutte);
                Finestra.RispostaTogli = null;
                dopo = Dati.Carica();
                v("tolta dal gruppo", di("mario/tornera") == null && Gruppo(f) < 0);

                aggiorna(new[] { "mario/vera", "mario/muta", "mario/guasta", "mario/tornera" });
                dopo = Dati.Carica();
                v("Aggiorna: la repo tolta che torna su GitHub rientra da sola", di("mario/tornera") != null && !di("mario/tornera").Sparita && di("mario/tornera").Numeri != null);
                v("Aggiorna: rientra anche a sinistra", Righe(f).Any(r => r.R != null && r.R.Nome == "mario/tornera") && Gruppo(f) < 0);

                var molte = new List<string> { "mario/vera", "mario/muta", "mario/guasta", "mario/tornera" };
                for (int i = 0; i < 40; i++) { crea("mario/molte" + i.ToString("00")); molte.Add("mario/molte" + i.ToString("00")); }
                aggiorna(molte.ToArray());
                f.Colonna.TopIndex = f.Colonna.Items.Count - 1;
                Application.DoEvents();
                int giu = f.Colonna.TopIndex;
                aggiorna(molte.ToArray());
                v("tante repo: dopo Aggiorna la colonna resta dove l'hai lasciata", giu > 10 && f.Colonna.TopIndex == giu);

                srv.Forza("GET", "/user/repos", 401, "{\"message\":\"Bad credentials\"}", null);
                f.AggiornaPerProva();
                for (int i = 0; i < 1500 && f.OccupatoOra; i++) { Application.DoEvents(); Thread.Sleep(10); }
                Application.DoEvents();
                dopo = Dati.Carica();
                v("Aggiorna fallito: non fa sparire niente", dopo.Repo.Count == 44 && dopo.Repo.All(r => !r.Sparita));
                f.Close();
                GitHub.SuGitHubVero();
                GitHub.Esci();
            }

            // la conferma con tante repo sta nella finestra, in ogni lingua
            var tante = new Accessi.Togliere { Testi = 1, GiorniStorico = 1 };
            for (int i = 0; i < 11; i++) tante.Repo.Add("azienda-con-un-nome-lungo/repo-con-un-nome-lungo-" + i);
            foreach (var c in Lingua.CODICI) {
                Lingua.Usa(c);
                using (var k = new FinestraTogli(tante)) {
                    Mostra(k);
                    bool dentro = k.BTogli.Right <= k.ClientSize.Width && k.BAnnulla.Left >= 0 && k.BTogli.Bottom <= k.ClientSize.Height;
                    bool altezze = k.Controls.OfType<Label>().All(l => TextRenderer.MeasureText(l.Text, l.Font, new Size(l.Width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height <= l.Height);
                    v(c + ": conferma: i tasti stanno dentro e i testi non si tagliano", dentro && altezze);
                    k.Close();
                }
                using (var k = new FinestraTogli(new Accessi.Togliere { Repo = new List<string> { "a/b" }, Testi = 1, GiorniStorico = 1 })) {
                    Mostra(k);
                    v(c + ": conferma di una sola: il testo del tasto ci sta", k.BTogli.Width >= TextRenderer.MeasureText(k.BTogli.Text, k.BTogli.Font).Width && k.BAnnulla.Left >= Aspetto.S(24));
                    k.Close();
                }
            }
            Lingua.Usa("it");
        } catch (Exception e) {
            v("eccezione: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
        }
        Finestra.RispostaTogli = null;
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }

    // sulla copia di un archivio vero (GPM_DATI obbligatoria e diversa dalla cartella vera), con la finestra e il flusso veri
    public static int SuCopia(string esito) {
        var sb = new StringBuilder();
        string dati = Environment.GetEnvironmentVariable("GPM_DATI");
        if (string.IsNullOrEmpty(dati)) { File.WriteAllText(esito, "SBAGLIA serve la copia in GPM_DATI\n"); return 1; }
        Dialogo.Silenzioso = true;
        Finestra.SoloFoto = true;
        var prima = Dati.Carica();
        var vive = prima.Repo.Where(r => !r.Sparita).Select(r => r.Nome).OrderBy(x => x, StringComparer.Ordinal).ToList();
        sb.AppendLine("prima: repo " + prima.Repo.Count + ", sparite " + prima.Repo.Count(r => r.Sparita) + ", vive " + vive.Count + ", testi " + prima.Elementi.Count + ", decisioni " + prima.Decisioni.Count);
        var f = new Finestra();
        Mostra(f);
        int aSinistra = Righe(f).Count(r => r.Gruppo);
        string confermaDice = null;
        Finestra.RispostaTogli = x => { confermaDice = ((FinestraTogli)x).Racconto.Replace("\n", " | "); return DialogResult.OK; };
        Clic(f, PaginaPanoramica.Tasto.TogliTutte);
        f.Close();
        Finestra.RispostaTogli = null;
        var dopo = Dati.Carica();
        var restate = dopo.Repo.Where(r => !r.Sparita).Select(r => r.Nome).OrderBy(x => x, StringComparer.Ordinal).ToList();
        sb.AppendLine("gruppo a sinistra: " + aSinistra);
        sb.AppendLine("la conferma dice: " + (confermaDice ?? "(non aperta)"));
        sb.AppendLine("dopo: repo " + dopo.Repo.Count + ", sparite " + dopo.Repo.Count(r => r.Sparita) + ", vive " + restate.Count + ", testi " + dopo.Elementi.Count + ", decisioni " + dopo.Decisioni.Count);
        sb.AppendLine("restano: " + string.Join(", ", dopo.Repo.Select(r => r.Nome)));
        bool buono = dopo.Repo.Count == vive.Count && restate.SequenceEqual(vive) && dopo.Repo.All(r => !r.Sparita);
        sb.AppendLine(buono ? "VERDE: restano esattamente le vive" : "ROSSO");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return buono ? 0 : 1;
    }
}
