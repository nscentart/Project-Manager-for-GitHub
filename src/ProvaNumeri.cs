// ProvaNumeri.cs (Project Manager 1.1, 03/11/2026): «Come sta andando» contro il GitHub finto: i numeri giusti, i totali di GitHub e non le
// somme, e soprattutto i casi cattivi. Un numero che non si è letto non deve mai diventare uno 0. Testi per chi prova: non si traducono.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class ProvaNumeri {
    const string CHIAVE = "chiave-finta-numeri";

    static Dictionary<string, object> Giorno(string data, int conta, int unici) { return ServerFinto.D("timestamp", data + "T00:00:00Z", "count", conta, "uniques", unici); }

    static void TraficoPieno(ServerFinto.RepoF r) {
        r.Traffico["/traffic/views"] = ServerFinto.D("count", 10, "uniques", 4, "views", new object[] { Giorno("2026-10-20", 6, 3), Giorno("2026-10-21", 4, 3) });
        r.Traffico["/traffic/clones"] = ServerFinto.D("count", 5, "uniques", 2, "clones", new object[] { Giorno("2026-10-20", 3, 1), Giorno("2026-10-21", 2, 2) });
        r.Traffico["/traffic/popular/referrers"] = new object[] { ServerFinto.D("referrer", "github.com", "count", 7, "uniques", 3) };
        r.Traffico["/traffic/popular/paths"] = new object[] { ServerFinto.D("path", "/mario/app", "title", "app", "count", 9, "uniques", 4) };
    }

    static RepoInfo Info(string nome) { return new RepoInfo { Nome = nome, Proprietario = nome.Split('/')[0] }; }

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPM_DATI"))) { File.WriteAllText(esito, "SBAGLIA serve una cartella dati di prova\n"); return 1; }
        Lingua.Usa("it");
        Dialogo.Silenzioso = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string solo = Lingua.T("github.traffico_solo_chi_scrive"), capisco = Lingua.T("github.risposta_github_che_non_capisco"), limite = Lingua.T("github.github_ha_messo_limite_alle");
        string oggi = DateTime.UtcNow.ToString("yyyy-MM-dd");
        using (var s = new ServerFinto())
        using (var altro = new ServerFinto()) {
            try {
                s.Chi[CHIAVE] = "mario"; s.Permessi[CHIAVE] = "repo, read:org";
                GitHub.Usa(CHIAVE, "mario");
                GitHub.SuServerDiProva(s.Porta);

                // ---------- tutto bene ----------
                var r = s.Nuova("mario/app", "User", "admin", false);
                r.Stelle = 5; r.Copie = 2; r.Osservatori = 3;
                TraficoPieno(r);
                var rel = new ServerFinto.ReleaseF { Id = 1, Tag = "v1.0", Ultima = "true" };
                rel.Allegati.Add(new KeyValuePair<string, long>("App.exe", 100)); rel.Scaricati["App.exe"] = 40;
                rel.Allegati.Add(new KeyValuePair<string, long>("App.zip", 100)); rel.Scaricati["App.zip"] = 2;
                r.Release.Add(rel);
                var bozza = new ServerFinto.ReleaseF { Id = 2, Tag = "v2.0", Bozza = true };
                bozza.Allegati.Add(new KeyValuePair<string, long>("App.exe", 100)); bozza.Scaricati["App.exe"] = 999;
                r.Release.Add(bozza);
                var n = GitHub.LeggiNumeri(Info("mario/app"), null);
                v("visite e persone: i totali di GitHub", n.VisiteLette && n.Visite == 10 && n.VisiteUnici == 4);
                v("le persone non sono la somma dei giorni (3+3)", n.VisiteUnici != n.VisiteGiorni.Sum(x => x.Unici));
                v("giorni delle visite", n.VisiteGiorni.Count == 2 && n.VisiteGiorni[0].Giorno == "2026-10-20" && n.VisiteGiorni[0].Conta == 6);
                v("cloni", n.CloniLetti && n.Cloni == 5 && n.CloniUnici == 2 && n.CloniGiorni.Count == 2);
                v("da dove arriva la gente", n.FontiLette && n.Fonti.Count == 1 && n.Fonti[0].Nome == "github.com" && n.Fonti[0].Conta == 7);
                v("pagine più viste", n.PagineLette && n.Pagine.Count == 1 && n.Pagine[0].Nome == "/mario/app" && n.Pagine[0].Unici == 4);
                v("download: somma degli allegati", n.DownloadLetti && n.Download == 42);
                v("download per versione", n.Versioni.Count == 1 && n.Versioni[0].Tag == "v1.0" && n.Versioni[0].Download == 42 && n.Versioni[0].Allegati.Count == 2);
                v("la bozza non conta", n.Versioni.All(x => x.Tag != "v2.0"));
                v("stelle, copie e osservatori", n.RepoLetta && n.Stelle == 5 && n.Copie == 2 && n.Osservatori == 3);
                v("nessun errore", n.VisiteErrore == null && n.CloniErrore == null && n.FontiErrore == null && n.PagineErrore == null && n.DownloadErrore == null && n.RepoErrore == null);
                v("letti: c'è l'ora", !string.IsNullOrEmpty(n.LettoIl));
                v("lettura e nient'altro", s.Scritture == 0);

                // ---------- 403 sul traffico: non disponibile, col perché, mai 0 ----------
                var altrui = s.Nuova("luisa/ricette", "User", "read", false);
                altrui.Stelle = 8;
                s.Metti(altrui, "README.md", Encoding.UTF8.GetBytes("# ricette"));
                var no = GitHub.LeggiNumeri(Info("luisa/ricette"), null);
                v("403: le visite non sono lette", !no.VisiteLette && !no.CloniLetti && !no.FontiLette && !no.PagineLette);
                v("403: il perché preciso", no.VisiteErrore == solo && no.CloniErrore == solo && no.FontiErrore == solo && no.PagineErrore == solo);
                v("403: il resto si legge lo stesso", no.DownloadLetti && no.RepoLetta && no.Stelle == 8 && no.DownloadErrore == null && no.RepoErrore == null);
                var cella = PaginaPanoramica.CellaNumero(no, no.VisiteLette, no.VisiteErrore, no.Visite);
                v("403: la cella dice «non disponibile»", cella.Testo == "non disponibile");
                v("403: la frase lo dice e non scrive 0 visite", PaginaPanoramica.FraseNumeri(no, 0).Contains("visite non disponibili") && !PaginaPanoramica.FraseNumeri(no, 0).Contains("0 visite"));
                var avvisi = PaginaPanoramica.AvvisiNumeri(no);
                v("403: un perché solo, una volta sola", avvisi.Count == 1 && avvisi[0].Contains(solo) && avvisi[0].StartsWith("Visite, cloni, provenienza, pagine viste: non disponibili."));

                // ---------- altri errori, ognuno per conto suo ----------
                s.Forza("GET", "/repos/mario/app/traffic/views", 403, "{\"message\":\"API rate limit exceeded\"}", new Dictionary<string, string> { { "X-RateLimit-Remaining", "0" } });
                var lim = GitHub.LeggiNumeri(Info("mario/app"), null);
                v("403 da limite: dice il limite, non il permesso", lim.VisiteErrore == limite && !lim.VisiteLette);
                v("403 da limite: le altre letture vanno", lim.CloniLetti && lim.DownloadLetti && lim.RepoLetta);

                s.Forza("GET", "/repos/mario/app/releases", 404, "{\"message\":\"Not Found\"}", null);
                var rel404 = GitHub.LeggiNumeri(Info("mario/app"), null);
                v("404 sulle release: non letti, mai 0", !rel404.DownloadLetti && rel404.DownloadErrore != null);
                v("404 sulle release: le visite ci sono", rel404.VisiteLette && rel404.RepoLetta);
                v("404 sulle release: la frase lo dice", PaginaPanoramica.FraseNumeri(rel404, 0).Contains("download non disponibili"));

                s.Forza("GET", "/repos/mario/app/traffic/views", 200, "{rotto", null);
                var rotto = GitHub.LeggiNumeri(Info("mario/app"), null);
                v("JSON rotto: non capisco", rotto.VisiteErrore == capisco && !rotto.VisiteLette);

                foreach (var caso in new[] {
                    new[] { "manca il totale", "{\"uniques\":1,\"views\":[]}" }, new[] { "numero come testo", "{\"count\":\"12\",\"uniques\":1,\"views\":[]}" },
                    new[] { "numero negativo", "{\"count\":-3,\"uniques\":1,\"views\":[]}" }, new[] { "numero nullo", "{\"count\":null,\"uniques\":1,\"views\":[]}" },
                    new[] { "manca l'elenco", "{\"count\":1,\"uniques\":1}" }, new[] { "giorno senza data", "{\"count\":1,\"uniques\":1,\"views\":[{\"count\":1,\"uniques\":1}]}" },
                    new[] { "giorno con la data sbagliata", "{\"count\":1,\"uniques\":1,\"views\":[{\"timestamp\":\"ieri\",\"count\":1,\"uniques\":1}]}" },
                    new[] { "elenco al posto dell'oggetto", "[]" } }) {
                    s.Forza("GET", "/repos/mario/app/traffic/views", 200, caso[1], null);
                    var x = GitHub.LeggiNumeri(Info("mario/app"), null);
                    v(caso[0] + ": non diventa uno 0", !x.VisiteLette && x.VisiteErrore == capisco && x.VisiteGiorni == null);
                }
                s.Forza("GET", "/repos/mario/app/traffic/popular/referrers", 200, "{\"niente\":1}", null);
                v("provenienza con una forma strana", GitHub.LeggiNumeri(Info("mario/app"), null).FontiErrore == capisco);
                s.Forza("GET", "/repos/mario/app/releases", 200, "[{\"tag_name\":\"v1\",\"assets\":[{\"name\":\"a\",\"download_count\":\"tanti\"}]}]", null);
                v("download non numerici", !GitHub.LeggiNumeri(Info("mario/app"), null).DownloadLetti);
                s.Forza("GET", "/repos/mario/app", 200, "{\"stargazers_count\":1,\"forks_count\":0}", null);
                var senzaOss = GitHub.LeggiNumeri(Info("mario/app"), null);
                v("osservatori mancanti: stelle e copie non a metà", !senzaOss.RepoLetta && senzaOss.RepoErrore == capisco && senzaOss.Stelle == 0);

                // ---------- errore dopo una lettura buona: i dati di prima, con l'errore accanto ----------
                var buona = GitHub.LeggiNumeri(Info("mario/app"), null);
                s.Forza("GET", "/repos/mario/app/traffic/views", 500, "{}", null);
                var dopo = GitHub.LeggiNumeri(Info("mario/app"), buona);
                v("dopo: restano le visite di prima", dopo.VisiteLette && dopo.Visite == 10 && dopo.VisiteErrore != null);
                v("dopo: la lettura vecchia non cambia", buona.VisiteErrore == null);
                var ambra = PaginaPanoramica.CellaNumero(dopo, dopo.VisiteLette, dopo.VisiteErrore, dopo.Visite);
                v("dopo: il numero vecchio è in ambra", ambra.Testo == "10" && ambra.Colore == Aspetto.AMBRA_S);
                v("dopo: la pagina dice che è vecchio", PaginaPanoramica.AvvisiNumeri(dopo).Any(x => x.Contains("non riletti, sono i dati di prima")));
                var ripresa = GitHub.LeggiNumeri(Info("mario/app"), dopo);
                v("dopo: l'errore se ne va quando si rilegge", ripresa.VisiteErrore == null);

                // ---------- rete giù ----------
                GitHub.SuServerDiProva(1);
                var giu = GitHub.LeggiNumeri(Info("mario/app"), null);
                string rete = Lingua.T("github.github_non_risponde_controlla_connessi");
                v("rete giù: nessuna eccezione, tutto non letto", !giu.VisiteLette && !giu.DownloadLetti && !giu.RepoLetta && giu.LettoIl == null);
                v("rete giù: il perché", giu.VisiteErrore == rete && giu.DownloadErrore == rete && giu.RepoErrore == rete);
                GitHub.SuServerDiProva(s.Porta);

                // ---------- tante pagine, e la chiave che non esce ----------
                var lunga = s.Nuova("mario/lunga", "User", "admin", false);
                for (int i = 1; i <= 120; i++) {
                    var q = new ServerFinto.ReleaseF { Id = 1000 + i, Tag = "v" + i };
                    q.Allegati.Add(new KeyValuePair<string, long>("A.exe", 1)); q.Scaricati["A.exe"] = 2;
                    lunga.Release.Add(q);
                }
                int primaGet = s.Di("GET", "/repos/mario/lunga/releases").Count;
                var tante = GitHub.LeggiNumeri(Info("mario/lunga"), null);
                v("120 versioni: tutte, seguendo le pagine", tante.Versioni.Count == 120 && tante.Download == 240);
                v("120 versioni: due pagine", s.Di("GET", "/repos/mario/lunga/releases").Count - primaGet == 2);
                s.LinkFinto = "<http://127.0.0.1:" + altro.Porta + "/repos/mario/lunga/releases?page=2>; rel=\"next\"";
                var fuori = GitHub.LeggiNumeri(Info("mario/lunga"), null);
                s.LinkFinto = null;
                v("una pagina dopo su un altro server: la chiave non ci va", altro.Registro.Count == 0);
                v("una pagina dopo su un altro server: niente totale a metà", !fuori.DownloadLetti && fuori.Versioni == null && fuori.DownloadErrore != null);

                // ---------- stelle e copie che cambiano ----------
                var s1 = GitHub.LeggiNumeri(Info("mario/app"), null);
                v("prima lettura: nessun cambio", s1.StellePrima == -1 && s1.StelleCambiate == null);
                r.Stelle = 7; r.Copie = 3;
                var s2 = GitHub.LeggiNumeri(Info("mario/app"), s1);
                v("stelle cambiate", s2.Stelle == 7 && s2.StellePrima == 5 && s2.StelleCambiate == oggi);
                v("copie cambiate", s2.Copie == 3 && s2.CopiePrima == 2 && s2.CopieCambiate == oggi);
                var novita = PaginaPanoramica.Novita(s2, new PaginaPanoramica.Conti { NuoveSegnalazioni = 2, NuoviCommenti = 1 }, oggi);
                v("novità: tutte e quattro", novita.Count == 4 && novita[0].StartsWith("2 segnalazioni nuove") && novita[1].StartsWith("1 commento nuovo") && novita[2].StartsWith("Stelle: da 5 a 7") && novita[3].StartsWith("Copie: da 2 a 3"));
                v("novità: dopo 14 giorni non sono più novità", PaginaPanoramica.Novita(s2, new PaginaPanoramica.Conti(), DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd")).Count == 0);
                var s3 = GitHub.LeggiNumeri(Info("mario/app"), s2);
                v("stesso numero: il cambio resta quello", s3.StellePrima == 5);

                // ---------- la Panoramica di sempre non ne risente ----------
                var solaPan = GitHub.Panoramica(Info("luisa/ricette"), null);
                v("i numeri che mancano non fanno «letta a metà»", no.VisiteErrore != null && PaginaPanoramica.LettaTutta(solaPan));

                // ---------- lo storico ----------
                string file = Path.Combine(Dati.Cartella, "storico.json");
                Action pulisci = () => { foreach (var x in Directory.GetFiles(Dati.Cartella, "storico.json*")) File.Delete(x); };
                pulisci();
                var st = new ArchivioStorico();
                Storico.Unisci(st, "mario/app", buona, "2026-10-21");
                v("storico: un giorno per data", Storico.QuantiGiorni(st, "mario/app") == 2);
                var c0 = Storico.Copia(st, "mario/app");
                v("storico: visite, unici, cloni e totale dei download", c0[1].Giorno == "2026-10-21" && c0[1].Visite == 4 && c0[1].Unici == 3 && c0[1].Cloni == 2 && c0[1].Download == 42);
                v("storico: il giorno senza download resta -1", c0[0].Download == -1);
                v("storico: la copia è una copia", ReferenceEquals(c0[0], st.Repo["mario/app"][0]) == false);

                var nuovi = buona.Copia();
                nuovi.VisiteGiorni = new List<GiornoTraffico> { new GiornoTraffico { Giorno = "2026-10-21", Conta = 9, Unici = 5 }, new GiornoTraffico { Giorno = "2026-10-22", Conta = 1, Unici = 1 } };
                nuovi.CloniGiorni = new List<GiornoTraffico>();
                nuovi.Download = 50;
                Storico.Unisci(st, "mario/app", nuovi, "2026-10-22");
                var c1 = Storico.Copia(st, "mario/app");
                v("storico: stessa data, vale GitHub", c1.First(x => x.Giorno == "2026-10-21").Visite == 9);
                v("storico: il giorno che GitHub non dà più resta", c1.First(x => x.Giorno == "2026-10-20").Visite == 6);
                v("storico: i cloni di quel giorno restano", c1.First(x => x.Giorno == "2026-10-21").Cloni == 2);
                v("storico: in ordine", c1.Select(x => x.Giorno).SequenceEqual(new[] { "2026-10-20", "2026-10-21", "2026-10-22" }));
                var perGiorno = Storico.DownloadPerGiorno(c1);
                v("download del giorno: la differenza fra due totali", perGiorno.Count == 1 && perGiorno[0].Giorno == "2026-10-22" && perGiorno[0].Conta == 8);
                v("download del giorno: il totale che scende non fa un negativo", Storico.DownloadPerGiorno(new[] { new GiornoStorico { Giorno = "2026-01-01", Download = 9 }, new GiornoStorico { Giorno = "2026-01-02", Download = 3 } }).Single().Conta == 0);
                v("download del giorno: il primo giorno non si sa", Storico.DownloadPerGiorno(new[] { new GiornoStorico { Giorno = "2026-01-01", Download = 9 } }).Count == 0);

                var fallita = nuovi.Copia();
                fallita.VisiteErrore = "x"; fallita.Visite = 999; fallita.CloniErrore = "x"; fallita.DownloadErrore = "x"; fallita.Download = 999;
                fallita.VisiteGiorni = new List<GiornoTraffico> { new GiornoTraffico { Giorno = "2026-10-21", Conta = 999, Unici = 999 } };
                Storico.Unisci(st, "mario/app", fallita, "2026-10-23");
                var c2 = Storico.Copia(st, "mario/app");
                v("storico: una lettura fallita non scrive niente", c2.Count == 3 && c2.All(x => x.Visite != 999 && x.Download != 999));
                var mai = new NumeriRepo { VisiteErrore = "x", DownloadErrore = "x" };
                Storico.Unisci(st, "mario/mai", mai, "2026-10-23");
                v("storico: una repo mai letta non crea niente", !st.Repo.ContainsKey("mario/mai"));

                Storico.Salva(st);
                v("storico: va e torna", Storico.Copia(Storico.Carica(), "mario/app").Select(x => x.Visite).SequenceEqual(c2.Select(x => x.Visite)));
                v("storico: nessun .nuovo in giro", !File.Exists(file + ".nuovo"));
                Storico.Salva(st);
                v("storico: anche la seconda volta", File.Exists(file) && !File.Exists(file + ".nuovo"));

                foreach (var caso in new[] { new[] { "JSON cattivo", "{rotto" }, new[] { "un numero al posto delle repo", "{\"Repo\":3}" }, new[] { "null", "null" }, new[] { "un elenco", "[]" }, new[] { "vuoto", "" } }) {
                    pulisci();
                    File.WriteAllText(file, caso[1], new UTF8Encoding(false));
                    bool regge = true; ArchivioStorico letto = null;
                    try { letto = Storico.Carica(); } catch { regge = false; }
                    v("storico rotto, " + caso[0] + ": non blocca e riparte vuoto", regge && letto != null && letto.Repo.Count == 0);
                    v("storico rotto, " + caso[0] + ": il file resta da parte", File.Exists(file + ".rotto") && File.ReadAllText(file + ".rotto") == caso[1]);
                    bool salva = true;
                    try { Storico.Unisci(letto, "mario/app", buona, oggi); Storico.Salva(letto); } catch { salva = false; }
                    v("storico rotto, " + caso[0] + ": si riparte e si salva", salva && Storico.QuantiGiorni(Storico.Carica(), "mario/app") > 0);
                }
                // ---------- un file che non si riesce ad aprire non è un file rotto ----------
                pulisci();
                string vecchi = "{\"Repo\":{\"mario/app\":[{\"Giorno\":\"2026-01-05\",\"Visite\":7,\"Unici\":-1,\"Cloni\":-1,\"Download\":3},{\"Giorno\":\"2026-01-06\",\"Visite\":8,\"Unici\":-1,\"Cloni\":-1,\"Download\":-1}],\"altro/x\":[{\"Giorno\":\"2026-01-07\",\"Visite\":9,\"Unici\":-1,\"Cloni\":-1,\"Download\":-1}]}}";
                File.WriteAllText(file, vecchi, new UTF8Encoding(false));
                ArchivioStorico bloccato = null; bool regge2 = true;
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    try {
                        bloccato = Storico.Carica();
                        Storico.Unisci(bloccato, "mario/app", buona, "2026-10-21");
                        Storico.Salva(bloccato);
                        Storico.Salva(bloccato);
                    } catch { regge2 = false; }
                    v("file bloccato: non ferma niente e parte senza i giorni vecchi", regge2 && bloccato != null && Storico.QuantiGiorni(bloccato, "mario/app") == 2);
                    v("file bloccato: non è trattato come rotto", !File.Exists(file + ".rotto"));
                }
                v("file bloccato: due salvataggi non l'hanno toccato", File.ReadAllText(file) == vecchi && !File.Exists(file + ".prima"));
                Storico.Salva(bloccato);
                var ripreso = Storico.Carica();
                v("riaperto: i giorni vecchi ci sono ancora", Storico.Copia(ripreso, "mario/app").Any(x => x.Giorno == "2026-01-05" && x.Visite == 7 && x.Download == 3) && Storico.QuantiGiorni(ripreso, "altro/x") == 1);
                v("riaperto: anche quelli nuovi", Storico.QuantiGiorni(ripreso, "mario/app") == 4 && Storico.Copia(ripreso, "mario/app").Select(x => x.Giorno).SequenceEqual(Storico.Copia(ripreso, "mario/app").Select(x => x.Giorno).OrderBy(x => x, StringComparer.Ordinal)));
                v("riaperto: il file buono resta anche come .prima", File.ReadAllText(file + ".prima") == vecchi);
                v("riaperto: anche in memoria si vedono tutti", Storico.QuantiGiorni(bloccato, "mario/app") == 4);
                Storico.Salva(ripreso); Storico.Salva(ripreso);
                v("riaperto: altri due salvataggi non perdono niente", Storico.QuantiGiorni(Storico.Carica(), "mario/app") == 4);
                pulisci();
                File.WriteAllText(file, vecchi, new UTF8Encoding(false));
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) bloccato = Storico.Carica();
                File.WriteAllText(file, "{rotto", new UTF8Encoding(false));
                Storico.Unisci(bloccato, "mario/app", buona, "2026-10-21");
                Storico.Salva(bloccato);
                v("riaperto ma rotto davvero: va da parte, e si salva", File.ReadAllText(file + ".rotto") == "{rotto" && Storico.QuantiGiorni(Storico.Carica(), "mario/app") == 2);

                pulisci();
                File.WriteAllText(file, "{\"Repo\":{\"a/b\":[{\"Giorno\":\"ieri\",\"Visite\":1},{\"Giorno\":\"2026-13-45\",\"Visite\":2},null,{\"Giorno\":\"2026-10-20\",\"Visite\":3},{\"Giorno\":\"2026-10-20\",\"Visite\":4}],\"c/d\":null}}", new UTF8Encoding(false));
                var sporco = Storico.Carica();
                v("storico: le date cattive si scartano, e una volta sola per data", Storico.QuantiGiorni(sporco, "a/b") == 1 && Storico.Copia(sporco, "a/b")[0].Visite == 4);
                v("storico: una repo senza giorni non lo ferma", !sporco.Repo.ContainsKey("c/d"));

                Storico.Rinomina(st, "mario/app", "mario/nuova");
                v("storico: segue il nome nuovo", Storico.QuantiGiorni(st, "mario/app") == 0 && Storico.QuantiGiorni(st, "mario/nuova") == 3);
                Storico.Rinomina(st, "MARIO/NUOVA", "mario/nuova");
                v("storico: stesso nome, non perde niente", Storico.QuantiGiorni(st, "mario/nuova") == 3);
                v("storico: togli dice quanti giorni", Storico.Togli(st, "Mario/Nuova") == 3 && Storico.QuantiGiorni(st, "mario/nuova") == 0);
                v("storico: togli di una che non c'è", Storico.Togli(st, "x/y") == 0);
                pulisci();

                // ---------- un archivio vecchio, di prima della 1.1 ----------
                var vecchio = Dati.Json().Deserialize<Archivio>("{\"Login\":\"mario\",\"Repo\":[{\"Nome\":\"mario/app\",\"Proprietario\":\"mario\"}]}");
                v("archivio vecchio: nessun numero, nessun errore", vecchio.Repo[0].Numeri == null && !vecchio.AvvisoSpento && vecchio.VersioneSaltata == null);
                var mai2 = PaginaPanoramica.CellaNumero(null, false, null, 0);
                v("archivio vecchio: la cella dice «non letto»", mai2.Testo == "non letto");
                v("archivio vecchio: la frase non inventa", PaginaPanoramica.FraseNumeri(null, 2).Contains("visite non disponibili") && !PaginaPanoramica.FraseNumeri(null, 2).Contains("0 download"));
                string ida = Dati.Json().Serialize(new Archivio { Repo = new List<RepoInfo> { new RepoInfo { Nome = "a/b", Numeri = buona } } });
                v("i numeri restano nell'archivio", Dati.Json().Deserialize<Archivio>(ida).Repo[0].Numeri.Visite == 10);

                // ---------- la frase e i plurali ----------
                var f = new NumeriRepo { VisiteLette = true, Visite = 312, VisiteUnici = 87, DownloadLetti = true, Download = 1204 };
                v("la frase, parola per parola", PaginaPanoramica.FraseNumeri(f, 3) == "Ultimi 14 giorni: 312 visite da 87 persone. In tutto: 1.204 download. Adesso: 3 segnalazioni da leggere.");
                var f1 = new NumeriRepo { VisiteLette = true, Visite = 1, VisiteUnici = 1, DownloadLetti = true, Download = 1 };
                v("la frase, al singolare", PaginaPanoramica.FraseNumeri(f1, 1) == "Ultimi 14 giorni: 1 visita da 1 persona. In tutto: 1 download. Adesso: 1 segnalazione da leggere.");
                var f0 = new NumeriRepo { VisiteLette = true, DownloadLetti = true };
                v("uno 0 letto davvero è 0", PaginaPanoramica.FraseNumeri(f0, 0) == "Ultimi 14 giorni: 0 visite da 0 persone. In tutto: 0 download. Adesso: 0 segnalazioni da leggere.");
                v("uno 0 letto si mostra", PaginaPanoramica.CellaNumero(f0, true, null, 0).Testo == "0");
                v("mai letto senza errore: non letto", PaginaPanoramica.CellaNumero(new NumeriRepo(), false, null, 0).Testo == "non letto");
                v("mille col separatore", PaginaPanoramica.CellaNumero(f, true, null, 1204).Testo == "1.204");

                // ---------- la tabella ----------
                v("tabella: otto colonne", PaginaPanoramica.COLONNE.Length == 8 && PaginaPanoramica.PESI.Length == 8);
                v("tabella: ogni colonna ha il suo spazio", PaginaPanoramica.PESI.All(x => x > 0));
                v("tabella: le colonne chieste", PaginaPanoramica.COLONNE.SequenceEqual(new[] { "Repo", "Chi la vede", "Versione", "Visite 14 giorni", "Download", "Stelle", "Segnalazioni da leggere", "Errori" }));
                var letta = new Panorama { ReleaseLetta = true, LicenzaLetta = true, ReadmeLetto = true, ReleaseTag = "v1", Licenza = "MIT", Readme = true, CommitDopo = 0 };
                var conDopo = new Panorama { ReleaseLetta = true, LicenzaLetta = true, ReadmeLetto = true, ReleaseTag = "v1", Licenza = "MIT", Readme = true, CommitDopo = 3 };
                var senza = new Panorama { ReleaseLetta = true, LicenzaLetta = true, ReadmeLetto = true, ReleaseTag = "", Licenza = "", Readme = false, CommitDopo = -1 };
                var vive = new List<RepoInfo> { new RepoInfo { Nome = "a/ok", Pannello = letta }, new RepoInfo { Nome = "a/dopo", Pannello = conDopo }, new RepoInfo { Nome = "a/senza", Pannello = senza } };
                Func<string, PaginaPanoramica.Conti> quanti = x => new PaginaPanoramica.Conti { Quarantena = x == "a/ok" ? 2 : 0, Errori = x == "a/senza" ? 1 : 0, Consegne = x == "a/ok" ? 1 : -1 };
                var chip = PaginaPanoramica.Problemi(vive, vive, quanti);
                v("in testa restano tutti i problemi, licenza e commit compresi", chip.SequenceEqual(new[] { "1 repo senza release", "1 repo con commit dopo la release", "1 repo senza licenza", "1 repo senza pagina principale", "1 consegna aspetta un rilascio", "2 testi in quarantena", "1 errore ricorrente" }));
                v("in testa: niente da dire, niente scritte", PaginaPanoramica.Problemi(new List<RepoInfo> { vive[0] }, new List<RepoInfo> { vive[0] }, x => new PaginaPanoramica.Conti()).Count == 0);
                v("in testa: i numeri che mancano stanno a parte", PaginaPanoramica.NumeriMancanti(new[] { new RepoInfo { Numeri = no }, new RepoInfo { Numeri = buona }, new RepoInfo() }).SequenceEqual(new[] { "numeri non disponibili su 1 repo" }));
                v("le carte della pagina: licenza, persone e commit dopo restano", Panoramica(new RepoInfo { Nome = "a/dopo", Pannello = conDopo }));
            } catch (Exception e) {
                v("eccezione: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
            } finally {
                GitHub.SuGitHubVero();
                GitHub.Esci();
            }
        }
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }

    // la pagina vera di una repo, disegnata: il testo che esce dalle carte non si può leggere, ma non deve fermarsi
    static bool Panoramica(RepoInfo r) {
        using (var f = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), ClientSize = new Size(1100, 800) })
        using (var p = new PaginaPanoramica { Dock = DockStyle.Fill }) {
            f.Controls.Add(p);
            f.Show();
            p.Imposta(r, new List<RepoInfo> { r }, "a", n => new PaginaPanoramica.Conti(), false, false, n => new List<GiornoStorico>());
            using (var b = new Bitmap(p.Width, p.Height)) p.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
            Application.DoEvents();
            var carte = p.CartePerProva();
            return carte.Any(c => c.StartsWith(Lingua.T("comune.licenza") + " | MIT")) && carte.Any(c => c.StartsWith(Lingua.T("comune.collaboratori") + " | ")) && carte.Any(c => c.Contains(Lingua.T("panoramica.commit_dopo_l_ultima_release") + ": 3"));
        }
    }
}
