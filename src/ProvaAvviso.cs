// ProvaAvviso.cs (Project Manager 1.1, 03/11/2026): l'avviso di versione nuova, con le prove di WeightMonitor adattate e quelle contro il GitHub
// finto: nessuna chiave, una sola domanda, nessun file scaricato, un indirizzo cattivo nella risposta non arriva mai al browser.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class ProvaAvviso {
    const string PERCORSO = "/repos/nscentart/Project-Manager-for-GitHub/releases/latest";
    const string BASE = "https://github.com/nscentart/Project-Manager-for-GitHub/";

    static string Risposta(string tag, string pagina, string allegato) {
        return "{\"tag_name\":\"" + tag + "\",\"body\":\"note\",\"html_url\":\"" + pagina + "\",\"assets\":[{\"name\":\"" + allegato + "\",\"browser_download_url\":\"https://evil.example/x.exe\",\"digest\":\"sha256:abc\"}]}";
    }

    // un GitHub che si ferma a metà risposta: chiude la connessione, oppure resta lì senza dire più niente
    class ServerRotto : IDisposable {
        readonly TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        readonly ManualResetEvent fine = new ManualResetEvent(false);
        public readonly int Porta;
        public ServerRotto(string risposta, bool resta) {
            l.Start();
            Porta = ((IPEndPoint)l.LocalEndpoint).Port;
            new Thread(() => {
                try {
                    using (var c = l.AcceptTcpClient()) {
                        var st = c.GetStream();
                        st.Read(new byte[8192], 0, 8192);
                        var b = Encoding.ASCII.GetBytes(risposta);
                        st.Write(b, 0, b.Length);
                        st.Flush();
                        if (resta) fine.WaitOne(30000);
                    }
                } catch { }
            }) { IsBackground = true }.Start();
        }
        public void Dispose() { fine.Set(); l.Stop(); }
    }

    static void Mostra(Form f) {
        f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000);
        f.Show();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(10); }
    }

    static bool Finche(Func<bool> c) {
        for (int i = 0; i < 600; i++) { if (c()) return true; Application.DoEvents(); Thread.Sleep(10); }
        return c();
    }

    static IEnumerable<Control> Tutti(Control c) { foreach (Control x in c.Controls) { yield return x; foreach (var y in Tutti(x)) yield return y; } }

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPM_DATI"))) { File.WriteAllText(esito, "SBAGLIA serve una cartella dati di prova\n"); return 1; }
        Lingua.Usa("it");
        Dialogo.Silenzioso = true;
        Finestra.SoloFoto = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) Aspetto.K = g.DpiX / 96f;   // come fa la finestra principale: qui le finestre si misurano senza
        string apiVera = Aggiornamenti.Api; int attesaVera = Aggiornamenti.AttesaMs, tempoVero = Aggiornamenti.TempoMs;
        float scalaVera = Aspetto.K;
        var mia = new Version("1.0.0");
        try {
            // ---------- gli indirizzi ----------
            foreach (var buono in new[] { BASE + "releases/tag/v0.6", BASE + "releases/tag/v1.1", BASE + "releases/latest" }) v("indirizzo buono: " + buono, Aggiornamenti.IndirizzoBuono(buono));
            foreach (var cattivo in new[] {
                "https://github.com/nscentart/Project-Manager-for-GitHubFalso/releases/download/v1/x.exe", "https://github.com/altro/Project-Manager-for-GitHub/x.exe",
                "http://github.com/nscentart/Project-Manager-for-GitHub/x", "https://github.com.falso.it/nscentart/Project-Manager-for-GitHub/x",
                "https://github.com@falso.it/nscentart/Project-Manager-for-GitHub/x", BASE + "../Altro/x.exe", BASE + "%2e%2e/Altro/x.exe", BASE + "\" --altro",
                BASE + "a b", BASE + "?x=1", BASE + "#x", BASE + "a\\b", "HTTPS://GITHUB.COM/nscentart/Project-Manager-for-GitHub/x", "file:///C:/x.exe", "", null,
                // della repo giusta, ma non pagine di una versione
                BASE, BASE + "issues", BASE + "tags", BASE + "releases", BASE + "releases/", BASE + "releases/tag/", BASE + "releases/tag/v1.1/", BASE + "releases/tag/v1.1/x",
                BASE + "releases/tag/..", BASE + "releases/tag/.", BASE + "releases/tag/%2e%2e", BASE + "releases/tag/v1.1?x=1", BASE + "releases/tag/v1.1#x", BASE + "releases/tag/v1.1\n",
                BASE + "releases/latest/", BASE + "releases/latest/download/ProjectManager.exe", BASE + "releases/latestx", BASE + "releases/download/v1.1/ProjectManager.exe",
                BASE + "RELEASES/latest", "https://github.com/nscentart/project-manager-for-github/releases/latest", "https://www.github.com/nscentart/Project-Manager-for-GitHub/releases/latest" })
                v("indirizzo rifiutato: " + (cattivo ?? "(nullo)"), !Aggiornamenti.IndirizzoBuono(cattivo));

            // ---------- la risposta ----------
            var n = Aggiornamenti.DaRisposta(Risposta("v1.1", BASE + "releases/tag/v1.1", "ProjectManager.exe"), mia);
            v("v1.1: si avvisa, con la sua pagina", n != null && n.Etichetta == "1.1" && n.Versione == new Version("1.1.0") && n.Pagina == BASE + "releases/tag/v1.1" && n.Note == "note");
            v("v1.0: uguale, niente", Aggiornamenti.DaRisposta(Risposta("v1.0", BASE + "releases/tag/v1.0", "ProjectManager.exe"), mia) == null);
            v("v0.9: più vecchia, niente", Aggiornamenti.DaRisposta(Risposta("v0.9", BASE + "releases/tag/v0.9", "ProjectManager.exe"), mia) == null);
            v("etichetta senza senso: niente", Aggiornamenti.DaRisposta(Risposta("ultima", BASE + "releases/tag/x", "ProjectManager.exe"), mia) == null);
            v("senza il nostro file: niente", Aggiornamenti.DaRisposta(Risposta("v1.1", BASE + "releases/tag/v1.1", "Altro.exe"), mia) == null);
            foreach (var fuori in new[] { "https://evil.example/x.exe", "https://github.com/nscentart/Project-Manager-for-GitHub/../../altro/x.exe", "https://github.com/nscentart/Project-Manager-for-GitHub/releases/download/v1.1/x.exe?a=1", "", "javascript:alert(1)", BASE + "releases/download/v1.1/ProjectManager.exe", BASE + "issues", BASE })
                v("pagina fuori dalla repo: la pagina ufficiale (" + fuori + ")", Aggiornamenti.DaRisposta(Risposta("v1.1", fuori, "ProjectManager.exe"), mia).Pagina == Aggiornamenti.PAGINA_ULTIMA);
            v("senza pagina: la pagina ufficiale", Aggiornamenti.DaRisposta("{\"tag_name\":\"v1.1\",\"assets\":[{\"name\":\"ProjectManager.exe\"}]}", mia).Pagina == Aggiornamenti.PAGINA_ULTIMA);
            v("si legge nessun indirizzo da scaricare", typeof(VersioneNuova).GetField("Url") == null);
            var metodi = typeof(Aggiornamenti).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Select(x => x.Name).ToList();
            v("non c'è niente che scarichi, installi o confronti un file", !metodi.Any(x => x.Contains("Scarica") || x.Contains("Installa") || x.Contains("Download") || x.Contains("Confronta") || x.Contains("Hash")));

            // ---------- l'impronta: da dove viene e cosa succede se non torna ----------
            string H = string.Concat(Enumerable.Repeat("3F9A1C7E", 8)), H2 = string.Concat(Enumerable.Repeat("0B6D24F8", 8)), h = H.ToLowerInvariant();
            Func<string, string> tra = x => "\"" + x + "\"";
            Func<string, string, VersioneNuova> conImpronta = (digest, corpo) => Aggiornamenti.DaRisposta("{\"tag_name\":\"v1.1\",\"body\":" + corpo + ",\"html_url\":\"" + BASE + "releases/tag/v1.1\",\"assets\":[{\"name\":\"Altro.exe\",\"digest\":\"sha256:" + H2 + "\"},{\"name\":\"ProjectManager.exe\"" + (digest == null ? "" : ",\"digest\":" + digest) + "}]}", mia);
            Func<string, string, string, bool, bool> impronta = (digest, corpo, attesa, diversa) => { var x = conImpronta(digest, corpo); return x.Impronta == attesa && x.ImprontaDiversa == diversa; };
            string dig = tra("sha256:" + h);
            v("impronta: dal digest, in maiuscolo", impronta(dig, tra("note"), H, false));
            v("impronta: digest e note uguali, nessun allarme", impronta(dig, tra("x `" + H + "` y"), H, false));
            v("impronta: uguali anche con maiuscole diverse", impronta(tra("sha256:" + H), tra(h), H, false));
            v("impronta: la stessa cinque volte nelle note, nessun allarme", impronta(dig, tra(string.Join(" ", Enumerable.Repeat(H, 5))), H, false));
            v("impronta: senza digest, dalle note", impronta(null, tra("x `" + H + "` y"), H, false));
            v("impronta: senza digest, cinque volte la stessa nelle note", impronta(null, tra(string.Join(" ", Enumerable.Repeat(h, 5))), H, false));
            v("impronta: nessuno dei due, non disponibile", impronta(null, tra("note"), null, false));
            v("impronta: il digest di un altro file non vale", conImpronta(null, tra("note")).Impronta == null);
            foreach (var cattivo in new[] { tra("sha256:abc"), tra("md5:" + h), tra("sha256:" + h.Substring(1)), tra("sha256:" + h + "0"), tra(" sha256:" + h), tra("SHA256:" + h), tra(h), tra("sha256:" + h.Substring(1) + "g"), tra(""), "12", "true", "null", "[]", "{}" }) {
                v("digest cattivo, vale come assente: " + (cattivo.Length > 24 ? cattivo.Substring(0, 24) + "..." : cattivo), impronta(cattivo, tra(H), H, false));
                v("digest cattivo e note vuote: non disponibile, " + (cattivo.Length > 24 ? cattivo.Substring(0, 24) + "..." : cattivo), impronta(cattivo, "null", null, false));
            }
            foreach (var corpo in new[] { "null", "12", "[]", "{}", tra(""), tra(h.Substring(1)), tra(h + "0"), tra("sha256:abc") }) v("note cattive: non disponibile, " + (corpo.Length > 24 ? corpo.Substring(0, 24) + "..." : corpo), impronta(null, corpo, null, false));
            v("due impronte diverse nelle note, senza digest: non disponibile, e l'allarme", impronta(null, tra(H + " " + H2), null, true));
            v("digest e note diversi: allarme, si mostra il digest", impronta(dig, tra("x `" + H2 + "` y"), H, true));
            v("digest giusto ma una delle note diversa: allarme", impronta(dig, tra(H + " " + H + " " + H2), H, true));

            // ---------- da un GitHub che si ferma a metà risposta ----------
            string corpoInizio = "{\"tag_name\":\"v1.1\",\"bo";
            Func<string, bool, string> rotto = (risposta, resta) => {
                using (var sr = new ServerRotto(risposta, resta)) {
                    Aggiornamenti.Api = "http://127.0.0.1:" + sr.Porta + PERCORSO;
                    try { Aggiornamenti.Cerca(mia); return null; } catch (Exception e) { return e.Message; }
                }
            };
            Aggiornamenti.TempoMs = 500;
            string capisco = Lingua.T("github.risposta_github_che_non_capisco");
            string chiuso = rotto("HTTP/1.1 200 OK\r\nContent-Length: 5000\r\nConnection: close\r\n\r\n" + corpoInizio, false);
            v("a metà, chiude con la lunghezza dichiarata: lo dice", chiuso != null && (chiuso.StartsWith("GitHub non risponde (") || chiuso == capisco));
            v("a metà, chiude senza lunghezza: lo dice", rotto("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n" + corpoInizio, false) == capisco);
            string aPezzi = rotto("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n10\r\n" + corpoInizio.Substring(0, 16) + "\r\n", false);
            v("a metà, chiude tra un pezzo e l'altro: lo dice", aPezzi != null && (aPezzi.StartsWith("GitHub non risponde (") || aPezzi == capisco));
            var orologio = System.Diagnostics.Stopwatch.StartNew();
            string fermo = rotto("HTTP/1.1 200 OK\r\nContent-Length: 5000\r\n\r\n" + corpoInizio, true);
            v("a metà, resta fermo: dopo il tempo massimo lo dice", fermo != null && fermo.StartsWith("GitHub non risponde (") && orologio.ElapsedMilliseconds < 8000);
            orologio.Restart();
            string muto = rotto("", true);
            v("non dice niente: dopo il tempo massimo lo dice", muto != null && muto.StartsWith("GitHub non risponde (") && orologio.ElapsedMilliseconds < 8000);
            v("il tempo massimo vero è 10 secondi, anche per chi si ferma a metà", tempoVero == 10000);
            Aggiornamenti.TempoMs = tempoVero;

            // ---------- le note ----------
            string pulite = Aggiornamenti.PulisciNote("**Titolo**\r\n\r\n\r\n\r\n<details><summary>Italiano</summary>\r\n`abc123`\r\n</details>\u202Ex");
            v("note: si leggono come testo", pulite.StartsWith("Titolo\r\n\r\n") && pulite.Contains("Italiano") && pulite.Contains("abc123") && !pulite.Contains("**") && !pulite.Contains("`") && !pulite.Contains("<details>"));
            v("note: i caratteri invisibili si vedono", pulite.Contains("{U+202E}") && !pulite.Contains("\u202E"));
            foreach (var tag in new[] { "<details>", "</details>", "<summary>", "</summary>", "<details open>", "<DETAILS open>", "<b>", "</b>", "<br>", "<br/>", "<br />", "<a href=\"https://evil.example/x.exe\">", "</a>", "<img src=x onerror=y>", "<h2>", "<table>", "<!-- nota -->", "<!-- su\ndue righe -->", "<!doctype html>", "<?xml version=\"1.0\"?>", "<a\nhref=\"x\">", "<de<b>tails>", "<de**tails>", "<de`tails>" })
                v("note: via il tag " + tag.Replace("\n", " "), Aggiornamenti.PulisciNote("prima " + tag + " dopo") == "prima  dopo");
            string sporche = Aggiornamenti.PulisciNote("<details>\n<summary>**Italiano**</summary>\n\n**Scarica** `ProjectManager.exe`: uno < due, due > uno, info@nscent.art\n<https://www.nscent.art/software>\n</details>");
            v("note: restano il testo, i confronti e gli indirizzi", sporche.StartsWith("Italiano\r\n") && sporche.Contains("Scarica ProjectManager.exe: uno < due, due > uno, info@nscent.art") && sporche.Contains("https://www.nscent.art/software"));
            v("note: niente tag, niente **, niente apici", !sporche.Contains("details") && !sporche.Contains("summary") && !sporche.Contains("**") && !sporche.Contains("`"));
            v("note: nulle", Aggiornamenti.PulisciNote(null) == "");

            // ---------- contro il GitHub finto ----------
            using (var s = new ServerFinto()) {
                Aggiornamenti.Api = "http://127.0.0.1:" + s.Porta + PERCORSO;
                Aggiornamenti.AttesaMs = 1;
                Action<string> risponde = t => s.Forza("GET", PERCORSO, 200, t, null);

                risponde(Risposta("v1.1", BASE + "releases/tag/v1.1", "ProjectManager.exe"));
                var uguale = Aggiornamenti.Cerca(new Version("1.1.0"));
                v("server: stessa versione, niente", uguale == null);
                var reg = s.Registro.ToList();
                v("server: una sola domanda", reg.Count == 1 && reg[0].Metodo == "GET" && reg[0].Percorso == PERCORSO);
                v("server: nessuna chiave", string.IsNullOrEmpty(reg[0].Auth));
                risponde(Risposta("v1.1", BASE + "releases/tag/v1.1", "ProjectManager.exe"));
                var finta = Aggiornamenti.Cerca(new Version("0.9.0"));
                v("server: più vecchia, trova la versione", finta != null && finta.Etichetta == "1.1");
                risponde(Risposta("v1.1", BASE + "releases/tag/v1.1", "ProjectManager.exe"));
                v("server: più nuova, niente", Aggiornamenti.Cerca(new Version("9.0.0")) == null);
                int primaImpronta = s.Registro.Count;
                risponde("{\"tag_name\":\"v1.1\",\"body\":\"`" + H2 + "`\",\"html_url\":\"" + BASE + "releases/tag/v1.1\",\"assets\":[{\"name\":\"ProjectManager.exe\",\"digest\":\"sha256:" + h + "\",\"browser_download_url\":\"http://127.0.0.1:" + s.Porta + "/scarica/ProjectManager.exe\"}]}");
                var conDigest = Aggiornamenti.Cerca(new Version("0.9.0"));
                v("server: impronta letta e allarme, con una sola domanda", conDigest.Impronta == H && conDigest.ImprontaDiversa && s.Registro.Count == primaImpronta + 1);
                v("server: nessun file scaricato", s.Registro.All(x => x.Percorso == PERCORSO));
                v("server: nessuna scrittura", s.Scritture == 0);

                Func<int, string, string> sbaglia = (codice, testo) => {
                    s.Forza("GET", PERCORSO, codice, testo, null);
                    try { Aggiornamenti.Cerca(mia); return null; } catch (Exception e) { return e.Message; }
                };
                v("404: la pagina non è pubblica", sbaglia(404, "{}") == Lingua.T("agg.non_pubblica"));
                string e500 = sbaglia(500, "{}");
                v("500: dice che GitHub non risponde", e500 != null && e500.StartsWith("GitHub non risponde ("));
                v("JSON rotto: lo dice", sbaglia(200, "{rotto") == Lingua.T("github.risposta_github_che_non_capisco"));
                v("JSON senza versione: lo dice", sbaglia(200, "{\"body\":\"x\"}") == Lingua.T("github.risposta_github_che_non_capisco"));
                v("JSON elenco: lo dice", sbaglia(200, "[]") == Lingua.T("github.risposta_github_che_non_capisco"));
                string e302 = sbaglia(302, "{}");
                v("un rimando non si segue", e302 != null && e302.StartsWith("GitHub non risponde ("));

                // all'avvio
                var mio = Aggiornamenti.Mia();
                string nuova = "v" + (mio.Major + 1) + "." + mio.Minor;
                Func<bool, string, string, VersioneNuova> avvio = (spento, saltata, risposta) => {
                    if (risposta != null) s.Forza("GET", PERCORSO, 200, risposta, null);
                    VersioneNuova vista = null;
                    var t = Aggiornamenti.AllAvvio(() => spento, () => saltata, x => vista = x);
                    if (t != null) t.Join(15000);
                    return vista;
                };
                int prima = s.Registro.Count;
                v("spento: nessuna finestra", avvio(true, null, null) == null);
                v("spento: non chiede niente", s.Registro.Count == prima);
                bool spenti = false;
                var ts = Aggiornamenti.AllAvvio(() => { bool x = spenti; spenti = true; return x; }, () => null, x => { });   // si spegne mentre aspetta
                ts.Join(15000);
                v("spento durante l'attesa: non chiede niente", s.Registro.Count == prima);
                v("avvio, più nuova: avvisa", avvio(false, null, Risposta(nuova, BASE + "releases/latest", "ProjectManager.exe")) != null);
                v("avvio, uguale: nessuna finestra", avvio(false, null, Risposta("v" + Programma.VERSIONE, BASE + "releases/latest", "ProjectManager.exe")) == null);
                v("avvio, più vecchia: nessuna finestra", avvio(false, null, Risposta("v0.1", BASE + "releases/latest", "ProjectManager.exe")) == null);
                v("avvio, saltata: nessuna finestra", avvio(false, nuova.TrimStart('v'), Risposta(nuova, BASE + "releases/latest", "ProjectManager.exe")) == null);
                v("avvio, saltata un'altra: avvisa", avvio(false, "0.1", Risposta(nuova, BASE + "releases/latest", "ProjectManager.exe")) != null);
                string logFile = Path.Combine(Dati.Cartella, "ProjectManager.log");
                File.Delete(logFile);   // è quello della cartella di prova: conta solo quello che si scrive adesso
                s.Forza("GET", PERCORSO, 500, "{}", null);
                v("avvio, GitHub muto: nessuna finestra", avvio(false, null, null) == null);
                string logDopo = File.Exists(logFile) ? File.ReadAllText(logFile) : "";
                v("avvio, GitHub muto: lo scrive nel log", logDopo.Contains("aggiornamenti:"));

                // il browser
                string aperto = null;
                Aggiornamenti.ApriBrowser = x => aperto = x;
                Aggiornamenti.Apri(BASE + "releases/tag/v1.2");
                v("apri: la pagina buona", aperto == BASE + "releases/tag/v1.2");
                foreach (var cattivo in new[] { "https://evil.example/x.exe", "", null, BASE + "?x=1", BASE, BASE + "issues", BASE + "releases/download/v1.2/ProjectManager.exe", BASE + "releases/tag/v1.2/x" }) {
                    aperto = null;
                    Aggiornamenti.Apri(cattivo);
                    v("apri: un indirizzo cattivo non arriva al browser (" + (cattivo ?? "nullo") + ")", aperto == Aggiornamenti.PAGINA_ULTIMA);
                }

                // ---------- la finestra, in tutte le lingue ----------
                string lunghe = string.Join("\n\n", Enumerable.Repeat("**Note** `abc` una riga di note abbastanza lunga da andare a capo nella casella, come quelle vere in cinque lingue", 60));
                var esempio = new VersioneNuova { Versione = new Version(1, 2, 0), Etichetta = "1.2", Pagina = BASE + "releases/tag/v1.2", Note = "**Note**\n\n`abc`", Impronta = H };
                var stati = new[] {
                    new KeyValuePair<string, VersioneNuova>("con impronta", esempio),
                    new KeyValuePair<string, VersioneNuova>("note lunghe", new VersioneNuova { Versione = esempio.Versione, Etichetta = "1.2", Pagina = esempio.Pagina, Note = lunghe, Impronta = H }),
                    new KeyValuePair<string, VersioneNuova>("impronta diversa", new VersioneNuova { Versione = esempio.Versione, Etichetta = "1.2", Pagina = esempio.Pagina, Note = esempio.Note, Impronta = H, ImprontaDiversa = true }),
                    new KeyValuePair<string, VersioneNuova>("senza impronta", new VersioneNuova { Versione = esempio.Versione, Etichetta = "1.2", Pagina = esempio.Pagina, Note = esempio.Note }),
                    new KeyValuePair<string, VersioneNuova>("due nelle note", new VersioneNuova { Versione = esempio.Versione, Etichetta = "1.2", Pagina = esempio.Pagina, Note = esempio.Note, ImprontaDiversa = true })
                };
                var frase100 = new Dictionary<string, int>();
                foreach (var scala in new[] { 1f, 1.5f }) {
                    Aspetto.Simula(scala);
                    foreach (var c in Lingua.CODICI) {
                        Lingua.Usa(c);
                        foreach (var st in stati) {
                            var x = st.Value;
                            string t = c + " " + (int)(scala * 100) + "% " + st.Key + ": ";
                            using (var f = new FinestraAvviso(x)) {
                                Mostra(f);
                                var fissi = f.Controls.Cast<Control>().ToList();
                                v(t + "tre tasti, non sovrapposti, dentro la finestra", f.BSalta.Right < f.BDopo.Left && f.BDopo.Right < f.BPagina.Left && f.BPagina.Right <= f.ClientSize.Width && f.BPagina.Bottom <= f.ClientSize.Height);
                                v(t + "il testo di ogni tasto ci sta", new[] { f.BSalta, f.BDopo, f.BPagina }.All(b => TextRenderer.MeasureText(b.Text, b.Font).Width < b.Width));
                                v(t + "ogni tasto dà la sua risposta", f.BSalta.DialogResult == DialogResult.Ignore && f.BDopo.DialogResult == DialogResult.Cancel && f.BPagina.DialogResult == DialogResult.OK && f.AcceptButton == f.BPagina && f.CancelButton == f.BDopo);
                                v(t + "le note in sola lettura", f.Note.ReadOnly && f.Note.Text.Contains("abc") && !f.Note.Text.Contains("**"));
                                var scritte = Tutti(f).OfType<Label>().ToList();
                                v(t + "dice la versione nuova e la tua", scritte.Any(y => y.Text.Contains("1.2")) && scritte.Any(y => y.Text.Contains(Programma.VERSIONE)));
                                v(t + "le scritte non si tagliano", scritte.All(y => y.GetPreferredSize(new Size(y.Width, 0)).Height <= y.Height && TextRenderer.MeasureText(y.Text, y.Font, new Size(y.Width - y.Padding.Horizontal, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height + y.Padding.Vertical <= y.Height));
                                v(t + "nulla si sovrappone e nulla esce dalla finestra", fissi.All(y => y.Left >= 0 && y.Top >= 0 && y.Right <= f.ClientSize.Width && y.Bottom <= f.ClientSize.Height) && !fissi.Any(y => fissi.Any(z => z != y && y.Bounds.IntersectsWith(z.Bounds))));
                                // la frase sotto il titolo
                                v(t + "la frase sotto il titolo è intera e non si taglia", f.Frase.Text == Lingua.T("agg.tu_hai", "v", Programma.VERSIONE) && f.Frase.GetPreferredSize(new Size(f.Frase.Width, 0)).Height <= f.Frase.Height && f.Frase.Bottom <= f.Impronta.Top);
                                int largFrase = TextRenderer.MeasureText(f.Frase.Text, f.Frase.Font).Width;
                                if (scala == 1f && st.Key == "con impronta") frase100[c] = largFrase;
                                if (scala != 1f && st.Key == "con impronta") v(t + "il carattere è davvero una volta e mezza", Math.Abs((float)largFrase / frase100[c] - 1.5f) < 0.2f);
                                // la riga dell'impronta: fissa, sopra le note, intera, selezionabile
                                v(t + "l'impronta sta in una riga fissa sopra le note", f.Impronta.Parent == f && f.Impronta.Visible && !f.Impronta.Multiline && f.Impronta.ReadOnly && f.Impronta.Bottom <= f.Note.Top);
                                if (x.Impronta != null) {
                                    int inizio = f.Impronta.Text.IndexOf(H);
                                    v(t + "la riga dice l'impronta", f.Impronta.Text == Lingua.T("agg.impronta", "h", H) && inizio > 0 && f.Impronta.Text.Length == inizio + 64);
                                    f.Impronta.Select(inizio, 64);
                                    v(t + "l'impronta si seleziona", f.Impronta.SelectedText == H);
                                } else v(t + "la riga dice che non è disponibile", f.Impronta.Text == Lingua.T("agg.impronta_manca"));
                                f.Impronta.SelectionLength = 0;
                                var fineRiga = f.Impronta.GetPositionFromCharIndex(f.Impronta.TextLength - 1);
                                int ultimo = TextRenderer.MeasureText("W", f.Impronta.Font, new Size(999, 99), TextFormatFlags.NoPadding).Width;
                                v(t + "la riga si vede tutta senza scorrere", f.Impronta.GetPositionFromCharIndex(0).X >= 0 && fineRiga.X + ultimo <= f.Impronta.ClientSize.Width && f.Impronta.Height >= TextRenderer.MeasureText("W", f.Impronta.Font).Height - Aspetto.S(4));
                                // l'allarme
                                if (x.ImprontaDiversa) {
                                    v(t + "l'allarme si vede, sopra l'impronta", f.Allarme != null && f.Allarme.Visible && f.Allarme.Parent == f && f.Allarme.Text == Lingua.T("agg.impronta_diversa") && f.Allarme.Bottom <= f.Impronta.Top && f.Allarme.BackColor == Aspetto.ROSSO);
                                    v(t + "la pagina si può aprire lo stesso", f.BPagina.Enabled && f.BPagina.DialogResult == DialogResult.OK);
                                } else v(t + "nessun allarme", f.Allarme == null);
                                f.Close();
                            }
                        }
                    }
                }
                Aspetto.Simula(scalaVera);
                Lingua.Usa("it");

                // ---------- la finestra principale: salta, più tardi, apri; spento dalle Impostazioni ----------
                Dati.Salva(new Archivio { Login = "mario", DominiFidati = Dati.FIDATI.ToList(), CartellaRepo = "" });
                var fin = new Finestra();
                Mostra(fin);
                aperto = null;
                Finestra.RispostaAvviso = x => DialogResult.Cancel;
                fin.MostraAvviso(esempio, fin);
                v("più tardi: non ricorda niente e non apre niente", Dati.Carica().VersioneSaltata == null && aperto == null);
                Finestra.RispostaAvviso = x => DialogResult.OK;
                fin.MostraAvviso(esempio, fin);
                v("apri la pagina: solo la pagina", aperto == esempio.Pagina && Dati.Carica().VersioneSaltata == null);
                aperto = null;
                Finestra.RispostaAvviso = x => DialogResult.Ignore;
                fin.MostraAvviso(esempio, fin);
                v("salta questa: non apre niente", aperto == null);
                v("salta questa: resta dopo aver riaperto", Dati.Carica().VersioneSaltata == "1.2");

                // il giro vero: ShowDialog e clic sul tasto, fuori schermo
                Func<Func<FinestraAvviso, Bottone>, Func<Form, DialogResult>> clic = quale => modulo => {
                    var av = (FinestraAvviso)modulo;
                    av.ShowInTaskbar = false; av.StartPosition = FormStartPosition.Manual; av.Location = new Point(-20000, -20000);
                    var tm = new System.Windows.Forms.Timer { Interval = 50 };
                    tm.Tick += delegate { tm.Stop(); quale(av).PerformClick(); };
                    tm.Start();
                    var r = av.ShowDialog();
                    tm.Dispose();
                    return r;
                };
                var allarme = new VersioneNuova { Versione = new Version(1, 3, 0), Etichetta = "1.3", Pagina = BASE + "releases/tag/v1.3", Note = "x", Impronta = H, ImprontaDiversa = true };
                aperto = null;
                Finestra.RispostaAvviso = clic(q => q.BDopo);
                fin.MostraAvviso(allarme, fin);
                v("finestra vera, più tardi: non apre e non ricorda", aperto == null && Dati.Carica().VersioneSaltata == "1.2");
                Finestra.RispostaAvviso = clic(q => q.BPagina);
                fin.MostraAvviso(allarme, fin);
                v("finestra vera, impronta diversa: la pagina si apre lo stesso, solo la pagina", aperto == allarme.Pagina && Dati.Carica().VersioneSaltata == "1.2");
                aperto = null;
                Finestra.RispostaAvviso = clic(q => q.BSalta);
                fin.MostraAvviso(allarme, fin);
                v("finestra vera, salta: ricorda e non apre", aperto == null && Dati.Carica().VersioneSaltata == "1.3");
                Dati.Salva(new Archivio { Login = "mario", DominiFidati = Dati.FIDATI.ToList(), CartellaRepo = "", VersioneSaltata = "1.2" });
                fin.Close();
                fin = new Finestra();
                Mostra(fin);

                // le Impostazioni: la voce per spegnere, e quella per controllare a mano che risponde sempre
                CheckBox spunta = null; Bottone ora = null; Label dice = null; string dopoGiu = null, dopoUguale = null, dopoNuova = null; bool chiesta = false;
                Finestra.FotoImpostazioni = x => {
                    Mostra(x);
                    spunta = Tutti(x).OfType<CheckBox>().FirstOrDefault(k => k.Text == Lingua.T("agg.controlla_all_avvio"));
                    ora = Tutti(x).OfType<Bottone>().FirstOrDefault(k => k.Text == Lingua.T("agg.controlla_ora"));
                    if (ora == null) return;
                    dice = Tutti(x).OfType<Label>().First(k => k.Bounds.Top == ora.Top + Aspetto.S(36));
                    Func<string> chiedi = () => { dice.Text = ""; ora.PerformClick(); Finche(() => ora.Enabled && dice.Text != Lingua.T("agg.sto_controllando")); return dice.Text; };
                    s.Forza("GET", PERCORSO, 500, "{}", null);
                    dopoGiu = chiedi();
                    s.Forza("GET", PERCORSO, 200, Risposta("v" + Programma.VERSIONE, BASE + "releases/latest", "ProjectManager.exe"), null);
                    dopoUguale = chiedi();
                    Finestra.RispostaAvviso = y => { chiesta = true; return DialogResult.Cancel; };
                    s.Forza("GET", PERCORSO, 200, Risposta(nuova, BASE + "releases/latest", "ProjectManager.exe"), null);
                    dopoNuova = chiedi();
                    Finche(() => chiesta);
                };
                var imp = new Finestra();
                Mostra(imp);
                imp.ApriImpostazioni();
                Finestra.FotoImpostazioni = null;
                v("impostazioni: la voce c'è, ed è accesa", spunta != null && spunta.Checked);
                v("impostazioni: c'è il controllo a mano", ora != null);
                v("controllo a mano, GitHub muto: risponde col motivo", dopoGiu != null && dopoGiu.StartsWith("Non riesco a controllare gli aggiornamenti:"));
                v("controllo a mano, uguale: «hai già l'ultima»", dopoUguale == Lingua.T("agg.ultima", "v", Programma.VERSIONE));
                v("controllo a mano, più nuova: apre l'avviso", chiesta);
                v("controllo a mano: risponde anche sulla versione saltata", Dati.Carica().VersioneSaltata == "1.2" && chiesta && dopoNuova != null);
                imp.Close(); fin.Close();

                // l'avviso arriva con le Impostazioni aperte, e lì si cambia lingua: deve comparire sulla finestra nuova
                int attesaAvviso = Finestra.AttesaAvvisoMs, comparsi = 0;
                Finestra.AttesaAvvisoMs = 20;
                Finestra.RispostaAvviso = y => { comparsi++; return DialogResult.Cancel; };
                var primaDellaLingua = new Finestra();
                Mostra(primaDellaLingua);
                primaDellaLingua.Enabled = false;   // come con le Impostazioni aperte sopra
                primaDellaLingua.AspettaAvviso(new VersioneNuova { Versione = new Version(1, 2, 0), Etichetta = "1.2", Pagina = BASE + "releases/latest", Note = "x", Impronta = H });
                for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(5); }
                v("finestra occupata: l'avviso aspetta", comparsi == 0);
                primaDellaLingua.Enabled = true;
                v("cambio lingua: chiude la finestra", primaDellaLingua.CambiaLingua("de") && primaDellaLingua.IsDisposed);
                var dopoLaLingua = new Finestra();
                Mostra(dopoLaLingua);
                v("cambio lingua: l'avviso compare sulla finestra nuova", Finche(() => comparsi == 1));
                for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(5); }
                v("cambio lingua: una volta sola", comparsi == 1);
                dopoLaLingua.Close();
                Finestra.AttesaAvvisoMs = attesaAvviso; Finestra.RiapriPerLingua = false;
                Lingua.Scegli("it", Dati.Cartella);

                var conSpento = new Archivio { AvvisoSpento = true };
                v("spento: resta dopo aver riaperto", Dati.Json().Deserialize<Archivio>(Dati.Json().Serialize(conSpento)).AvvisoSpento);
            }
        } catch (Exception e) {
            v("eccezione: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
        } finally {
            Aggiornamenti.Api = apiVera; Aggiornamenti.AttesaMs = attesaVera; Aggiornamenti.TempoMs = tempoVero; Aggiornamenti.ApriBrowser = null;
            Aspetto.Simula(scalaVera);
            Finestra.RispostaAvviso = null; Finestra.FotoImpostazioni = null; Finestra.AttesaAvvisoMs = 5000;
        }
        v("l'indirizzo vero è quello della repo ufficiale", Aggiornamenti.Api == "https://api.github.com" + PERCORSO && attesaVera == 20000);
        v("la scala è tornata quella vera", Aspetto.K == scalaVera && Aspetto.NORMALE.Size == 9.5f);
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }
}
