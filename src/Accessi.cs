using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

// un errore da far leggere: il testo è sempre scritto da noi, mai copiato da una risposta
public class ErroreAccesso : Exception {
    public ErroreAccesso(string testo) : base(testo) { }
}

public class CodiceDispositivo {
    public string Codice, PerUtente, Pagina;
    public int Scade, Intervallo;
}

public static class Accessi {
    // l'identificativo con cui il programma è registrato su GitHub: non è un segreto. Vuoto = il login col browser non è ancora attivo
    public const string CLIENT_ID = "Ov23liD9FyB7r5v9jZTH";
    // quello che serve per leggere e per lavorare sulle repo. Mai nella chiave custodita: delete_repo, admin:org, workflow
    public const string PERMESSI = "repo read:org";
    // solo per cancellare una repo, con un login apposta: quella chiave resta in memoria, si usa una volta e si butta
    public const string PERMESSO_CANCELLA = "repo delete_repo";
    public const string PAGINA_RITIRA = "https://github.com/settings/connections/applications/" + CLIENT_ID;
    const string URL_CODICE = "https://github.com/login/device/code", URL_TOKEN = "https://github.com/login/oauth/access_token";
    public const string PAGINA = "https://github.com/login/device";
    public const string BROWSER = "browser", GIT = "git", VECCHIO = "vecchio";

    public static bool Uguali(string a, string b) { return Organizzazione.Uguali(a, b); }

    // ---------- gli account nell'archivio ----------

    public static ContoGitHub Attivo(Archivio a) { return a.Conti.FirstOrDefault(c => Uguali(c.Login, a.Login)); }

    // un archivio di prima della 0.4 ha un solo Login e nessun conto: diventa il conto attivo. L'accesso resta quello di prima,
    // da Git, che alla prima lettura dice chi è davvero. Vero se l'archivio era di quelli vecchi.
    public static bool Migra(Archivio a) {
        if (a.Conti == null) a.Conti = new List<ContoGitHub>();
        if (a.Conti.Count > 0 || string.IsNullOrEmpty(a.Login)) return false;
        a.Conti.Add(new ContoGitHub { Login = a.Login, Origine = VECCHIO });
        foreach (var r in a.Repo) if (r.Conti == null) r.Conti = new List<string> { a.Login };
        return true;
    }

    public static ContoGitHub Aggiungi(Archivio a, string login, string origine) {
        var c = a.Conti.FirstOrDefault(x => Uguali(x.Login, login));
        if (c == null) { c = new ContoGitHub { Login = login }; a.Conti.Add(c); }
        c.Login = login; c.Origine = origine;
        a.Login = login; a.Filtro = null;
        return c;
    }

    // Git ha detto che il conto vecchio in realtà è un altro nome: le repo seguono il nome giusto
    public static void Rinomina(Archivio a, string vecchio, string nuovo) {
        var c = a.Conti.FirstOrDefault(x => Uguali(x.Login, vecchio));
        if (c != null) {
            if (a.Conti.Any(x => x != c && Uguali(x.Login, nuovo))) a.Conti.Remove(c);
            else c.Login = nuovo;
        }
        foreach (var r in a.Repo.Where(x => x.Conti != null)) {
            bool aveva = r.Conti.RemoveAll(x => Uguali(x, vecchio)) > 0;
            if (aveva && !r.Conti.Any(x => Uguali(x, nuovo))) r.Conti.Add(nuovo);
        }
        if (Uguali(a.Login, vecchio)) a.Login = nuovo;
    }

    // repo, testi e decisioni restano nell'archivio, solo nascosti: ricollegando l'account tornano. Passa al primo conto rimasto
    public static void Scollega(Archivio a, string login) {
        a.Conti.RemoveAll(x => Uguali(x.Login, login));
        foreach (var r in a.Repo.Where(x => x.Conti != null)) r.Conti.RemoveAll(x => Uguali(x, login));
        if (Uguali(a.Login, login)) { a.Login = a.Conti.Count > 0 ? a.Conti[0].Login : null; a.Filtro = null; }
    }

    // l'elenco che GitHub dà a questo account entra nell'archivio. Una repo che non elenca più è sparita solo se nessun altro
    // account collegato la vede. Dice quante sono sparite.
    public static int Unisci(Archivio a, string login, List<RepoInfo> nuove, Func<RepoInfo, bool> seguiNuova) {
        foreach (var n in nuove) {
            var v = a.Repo.FirstOrDefault(x => Uguali(x.Nome, n.Nome));
            if (v == null) { n.Seguita = seguiNuova(n); n.Conti = new List<string> { login }; a.Repo.Add(n); continue; }
            v.Privata = n.Privata; v.Descrizione = n.Descrizione; v.Proprietario = n.Proprietario; v.Mia = n.Mia; v.Organizzazione = n.Organizzazione; v.Ramo = n.Ramo; v.Sparita = false;
            if (v.Conti == null) v.Conti = new List<string>();
            if (!v.Conti.Any(x => Uguali(x, login))) v.Conti.Add(login);
        }
        int sparite = 0;
        foreach (var v in a.Repo) {
            if (nuove.Any(n => Uguali(n.Nome, v.Nome))) continue;
            if (v.Conti == null) v.Conti = new List<string> { login };
            if (!v.Conti.Any(x => Uguali(x, login))) continue;
            if (v.Conti.Count > 1) { v.Conti.RemoveAll(x => Uguali(x, login)); continue; }
            v.Sparita = true; sparite++;
        }
        return sparite;
    }

    public static bool Vede(RepoInfo r, string login) { return r.Conti == null || r.Conti.Any(x => Uguali(x, login)); }

    static string Padrone(RepoInfo r) {
        if (!string.IsNullOrEmpty(r.Proprietario)) return r.Proprietario;
        int i = (r.Nome ?? "").IndexOf('/');
        return i > 0 ? r.Nome.Substring(0, i) : "";
    }

    public const string TUTTE = "", PERSONALI = "personali", ALTRI = "altri", ORG = "org:";

    public static bool Passa(RepoInfo r, string login, string filtro) {
        if (!Vede(r, login)) return false;
        if (string.IsNullOrEmpty(filtro)) return true;
        if (filtro == PERSONALI) return !r.Organizzazione && Uguali(Padrone(r), login);
        if (filtro == ALTRI) return !r.Organizzazione && !Uguali(Padrone(r), login);
        return filtro.StartsWith(ORG) && r.Organizzazione && Uguali(Padrone(r), filtro.Substring(ORG.Length));
    }

    public static List<RepoInfo> Visibili(Archivio a) {
        var f = a.Filtro;
        if (!Filtri(a).Any(x => x[0] == (f ?? TUTTE))) f = TUTTE;   // l'organizzazione scelta non c'è più: si torna a tutte
        return a.Repo.Where(r => Passa(r, a.Login, f)).ToList();
    }

    // le scelte del filtro per il conto attivo: {codice, etichetta}. Compaiono solo quelle che hanno qualcosa dentro
    public static List<string[]> Filtri(Archivio a) {
        var mie = a.Repo.Where(r => Vede(r, a.Login)).ToList();
        var l = new List<string[]> { new[] { TUTTE, Lingua.T("comune.tutte_repo") } };
        if (mie.Any(r => Passa(r, a.Login, PERSONALI))) l.Add(new[] { PERSONALI, Lingua.T("accessi.solo_personali") });
        foreach (var o in mie.Where(r => r.Organizzazione).Select(Padrone).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            l.Add(new[] { ORG + o, Lingua.T("accessi.organizzazione", "o", o) });
        if (mie.Any(r => Passa(r, a.Login, ALTRI))) l.Add(new[] { ALTRI, Lingua.T("accessi.altre_persone") });
        return l;
    }

    // ---------- il login col browser ----------

    static Dictionary<string, object> Risposta(string corpo) {
        try {
            var d = new JavaScriptSerializer().DeserializeObject(corpo ?? "") as Dictionary<string, object>;
            if (d != null) return d;
        } catch { }
        throw new ErroreAccesso(Lingua.T("comune.github_ha_dato_risposta_che"));
    }

    static string Campo(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
    static int Numero(Dictionary<string, object> d, string k, int altrimenti) { int n; return int.TryParse(Campo(d, k), out n) && n > 0 ? n : altrimenti; }

    static string Spiega(string errore) {
        switch (errore) {
            case "access_denied": return Lingua.T("accessi.github_hai_scelto_non_autorizzare");
            case "expired_token": case "token_expired": return Lingua.T("accessi.codice_scaduto_ricomincia_ne_arriva");
            case "device_flow_disabled": return Lingua.T("accessi.github_login_col_codice_non");
            case "incorrect_client_credentials": return Lingua.T("accessi.github_non_riconosce_questo_programma");
            default: return Lingua.T("accessi.github_non_ha_dato_l");
        }
    }

    // il primo passo: GitHub dà il codice da mostrare alla persona. La pagina si apre solo se è davvero su github.com
    public static CodiceDispositivo Chiedi(string clientId, Func<string, string, string> posta) { return Chiedi(clientId, PERMESSI, posta); }

    public static CodiceDispositivo Chiedi(string clientId, string permessi, Func<string, string, string> posta) {
        var d = Risposta(posta(URL_CODICE, "client_id=" + Uri.EscapeDataString(clientId) + "&scope=" + Uri.EscapeDataString(permessi)));
        if (Campo(d, "error").Length > 0) throw new ErroreAccesso(Spiega(Campo(d, "error")));
        var c = new CodiceDispositivo { Codice = Campo(d, "device_code"), PerUtente = Campo(d, "user_code"), Pagina = Campo(d, "verification_uri"),
                                        Scade = Numero(d, "expires_in", 900), Intervallo = Numero(d, "interval", 5) };
        if (c.Codice.Length == 0 || c.PerUtente.Length == 0) throw new ErroreAccesso(Lingua.T("comune.github_ha_dato_risposta_che"));
        if (!c.Pagina.StartsWith("https://github.com/")) c.Pagina = PAGINA;
        return c;
    }

    // il secondo passo: si chiede a GitHub ogni tanto se la persona ha approvato. Nullo = annullato dalla persona
    public static string Aspetta(CodiceDispositivo c, string clientId, Func<string, string, string> posta, Action<int> dorme, Func<bool> annullato) {
        string corpo = "client_id=" + Uri.EscapeDataString(clientId) + "&device_code=" + Uri.EscapeDataString(c.Codice) + "&grant_type=" + Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:device_code");
        int passo = Math.Max(1, c.Intervallo), trascorsi = 0, cadute = 0;
        while (trascorsi < c.Scade) {
            dorme(passo);
            trascorsi += passo;
            if (annullato()) return null;
            string testo;
            try { testo = posta(URL_TOKEN, corpo); cadute = 0; }
            catch (ErroreAccesso) { throw; }
            catch (Exception) {   // la rete può saltare un attimo mentre la persona sta approvando: si rallenta e si riprova
                if (++cadute >= 5) throw new ErroreAccesso(Lingua.T("comune.github_non_risponde_controlla_connessi"));
                passo += 5;
                continue;
            }
            var d = Risposta(testo);
            string t = Campo(d, "access_token");
            if (t.Length > 0) return t;
            string e = Campo(d, "error");
            if (e == "authorization_pending") continue;
            if (e == "slow_down") { passo = Numero(d, "interval", passo + 5); continue; }
            throw new ErroreAccesso(Spiega(e));
        }
        throw new ErroreAccesso(Spiega("expired_token"));
    }

    public static string Posta(string url, string corpo) {
        if (url != URL_CODICE && url != URL_TOKEN) throw new Exception("indirizzo fuori dal login di GitHub");
        return PostaA(url, corpo);
    }

    static string PostaA(string url, string corpo) {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // TLS 1.2
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "POST";
        req.ContentType = "application/x-www-form-urlencoded";
        req.Accept = "application/json";
        req.UserAgent = "ProjectManager/" + Programma.VERSIONE;
        req.AllowAutoRedirect = false;
        req.Timeout = 30000;
        var b = Encoding.ASCII.GetBytes(corpo);
        req.ContentLength = b.Length;
        try {
            using (var s = req.GetRequestStream()) s.Write(b, 0, b.Length);
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h == null) throw new ErroreSenzaRete(Lingua.T("accessi.github_non_risponde"));
            if ((int)h.StatusCode == 404) throw new ErroreAccesso(Spiega("incorrect_client_credentials"));
            throw new ErroreAccesso(Spiega(""));
        }
    }

    // git cercato solo nelle cartelle di PATH: mai accanto all'exe o nella cartella corrente, dove può esserci un file qualunque
    public static string TrovaGit() {
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
            try {
                string c = d.Trim().Trim('"');
                if (c.Length == 0 || !Path.IsPathRooted(c)) continue;
                string f = Path.Combine(c, "git.exe");
                if (File.Exists(f)) return f;
            } catch (ArgumentException) { }
        }
        return null;
    }

    // ---------- la prova, con dati finti ----------

    static RepoInfo R(string nome, string prop, bool org, bool mia) { return new RepoInfo { Nome = nome, Proprietario = prop, Organizzazione = org, Mia = mia }; }

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };

        // un archivio scritto dalla 0.3, così com'era: nessuna scelta deve perdersi
        const string VECCHIO_JSON = "{\"Login\":\"pippo\",\"UltimoAggiornamento\":\"01/11/2026 20:00\",\"Modello\":\"m\",\"DominiFidati\":[\"github.com\",\"esempio.it\"],"
            + "\"Repo\":[{\"Nome\":\"pippo/a\",\"Proprietario\":\"pippo\",\"Seguita\":true,\"Mia\":true,\"Dal\":\"2026-11-01T10:00:00Z\"},{\"Nome\":\"pippo/b\",\"Proprietario\":\"pippo\",\"Seguita\":false,\"Mia\":true},"
            + "{\"Nome\":\"azienda/c\",\"Proprietario\":\"azienda\",\"Organizzazione\":true,\"Seguita\":true,\"Mia\":true}],"
            + "\"Elementi\":[{\"Chiave\":\"issue:1\",\"Repo\":\"pippo/a\",\"Numero\":1,\"Titolo\":\"t\"}],\"Decisioni\":[{\"Chiave\":\"issue:1\",\"Stato\":\"fatto\"}]}";
        var js = new JavaScriptSerializer();
        var a = js.Deserialize<Archivio>(VECCHIO_JSON);
        v("archivio 0.3: si riconosce", Migra(a));
        v("archivio 0.3: diventa il conto attivo", a.Conti.Count == 1 && Attivo(a) != null && Attivo(a).Origine == VECCHIO);
        v("archivio 0.3: repo seguite, testi e decisioni restano", a.Repo.Count(x => x.Seguita) == 2 && a.Repo[0].Dal != null && a.Elementi.Count == 1 && a.Decisioni.Count == 1);
        v("archivio 0.3: siti fidati restano", a.DominiFidati.Contains("esempio.it"));
        v("archivio 0.3: tutte le repo si vedono", Visibili(a).Count == 3);
        v("archivio 0.3: la seconda volta non cambia niente", !Migra(a) && a.Conti.Count == 1);
        var ricaricato = js.Deserialize<Archivio>(js.Serialize(a));
        v("salvato e riletto resta uguale", !Migra(ricaricato) && ricaricato.Conti.Count == 1 && Visibili(ricaricato).Count == 3);
        var nuovo = new Archivio();
        v("archivio nuovo: nessun conto", !Migra(nuovo) && nuovo.Conti.Count == 0 && Attivo(nuovo) == null);

        // Git dice che il conto vecchio ha un altro nome
        Rinomina(a, "pippo", "pippo2");
        v("rinomina: il conto e le repo seguono il nome giusto", a.Login == "pippo2" && Attivo(a) != null && Vede(a.Repo[0], "pippo2") && !Vede(a.Repo[0], "pippo"));
        Rinomina(a, "pippo2", "pippo");

        // il secondo account
        Aggiungi(a, "caio", BROWSER);
        var sparite = Unisci(a, "caio", new List<RepoInfo> { R("azienda/c", "azienda", true, false), R("caio/d", "caio", false, true) }, x => false);
        v("secondo account: non fa sparire le repo del primo", sparite == 0 && a.Repo.Count(x => x.Sparita) == 0);
        v("secondo account: è quello attivo", a.Login == "caio" && a.Conti.Count == 2);
        v("secondo account: vede solo le sue", Visibili(a).Select(x => x.Nome).OrderBy(x => x).SequenceEqual(new[] { "azienda/c", "caio/d" }));
        v("repo in comune: ne resta una, seguita come prima", a.Repo.Count(x => x.Nome == "azienda/c") == 1 && a.Repo.First(x => x.Nome == "azienda/c").Seguita);
        a.Login = "pippo";
        v("cambio account: si rivedono le sue", Visibili(a).Select(x => x.Nome).OrderBy(x => x).SequenceEqual(new[] { "azienda/c", "pippo/a", "pippo/b" }));

        // il filtro
        var f = Filtri(a);
        v("filtro: tutte, personali e l'organizzazione", f.Select(x => x[0]).SequenceEqual(new[] { TUTTE, PERSONALI, ORG + "azienda" }));
        a.Filtro = PERSONALI;
        v("filtro personali", Visibili(a).Select(x => x.Nome).OrderBy(x => x).SequenceEqual(new[] { "pippo/a", "pippo/b" }));
        a.Filtro = ORG + "AZIENDA";
        v("filtro organizzazione, maiuscole a parte", Passa(a.Repo.First(x => x.Nome == "azienda/c"), "pippo", a.Filtro) && !Passa(a.Repo[0], "pippo", a.Filtro));
        a.Filtro = ORG + "non-ci-sono-piu";
        v("filtro su un'organizzazione lasciata: si torna a tutte", Visibili(a).Count == 3);
        a.Filtro = null;
        a.Repo.Add(new RepoInfo { Nome = "tizio/x", Proprietario = "tizio", Conti = new List<string> { "pippo" } });
        v("filtro: compaiono le repo di altre persone", Filtri(a).Any(x => x[0] == ALTRI) && Passa(a.Repo.Last(), "pippo", ALTRI) && !Passa(a.Repo.Last(), "pippo", PERSONALI));
        a.Repo.RemoveAll(x => x.Nome == "tizio/x");

        // repo che spariscono
        sparite = Unisci(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true), R("pippo/b", "pippo", false, true) }, x => false);
        v("sparita: l'altro account la vede ancora, non è sparita", sparite == 0 && !a.Repo.First(x => x.Nome == "azienda/c").Sparita && !Vede(a.Repo.First(x => x.Nome == "azienda/c"), "pippo"));
        sparite = Unisci(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true) }, x => false);
        v("sparita: nessuno la vede più", sparite == 1 && a.Repo.First(x => x.Nome == "pippo/b").Sparita && Vede(a.Repo.First(x => x.Nome == "pippo/b"), "pippo"));
        Unisci(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true), R("pippo/b", "pippo", false, true) }, x => false);
        v("sparita: se torna, torna", !a.Repo.First(x => x.Nome == "pippo/b").Sparita);
        Unisci(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true), R("pippo/b", "pippo", false, true), R("pippo/n1", "pippo", false, true), R("altro/n2", "altro", false, false) }, x => x.Mia);
        v("repo nuova: si segue come dice chi chiama", a.Repo.First(x => x.Nome == "pippo/n1").Seguita && !a.Repo.First(x => x.Nome == "altro/n2").Seguita);

        // scollegare
        int testi = a.Elementi.Count;
        Scollega(a, "pippo");
        v("scollega: si passa all'altro conto", a.Login == "caio" && a.Conti.Count == 1);
        v("scollega: le repo restano nascoste, i testi restano", a.Repo.Any(x => x.Nome == "pippo/a") && !Vede(a.Repo.First(x => x.Nome == "pippo/a"), "caio") && a.Elementi.Count == testi);
        Aggiungi(a, "pippo", BROWSER);
        Unisci(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true) }, x => false);
        v("ricollega: la repo torna seguita, dov'era", a.Repo.First(x => x.Nome == "pippo/a").Seguita && Vede(a.Repo.First(x => x.Nome == "pippo/a"), "pippo"));
        Scollega(a, "pippo"); Scollega(a, "caio");
        v("scollega l'ultimo: nessun conto attivo", a.Login == null && Attivo(a) == null);

        // il login col browser: la rete è finta, il tempo anche
        var chiamate = new List<string>();
        Func<string[], Func<string, string, string>> rete = risposte => {
            int i = 0;
            return (url, corpo) => {
                chiamate.Add(url + " " + corpo);
                string r = risposte[Math.Min(i++, risposte.Length - 1)];
                if (r == "CADUTA") throw new Exception("rete");
                return r;
            };
        };
        const string CODICE = "{\"device_code\":\"dc1\",\"user_code\":\"ABCD-1234\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":900,\"interval\":5}";
        const string FINTO = "gho_finto_per_la_prova";
        var c = Chiedi("id1", rete(new[] { CODICE }));
        v("codice: arriva", c.PerUtente == "ABCD-1234" && c.Codice == "dc1" && c.Intervallo == 5 && c.Scade == 900);
        v("codice: si chiede coi permessi giusti", chiamate[0].StartsWith("https://github.com/login/device/code client_id=id1&scope=repo%20read%3Aorg"));
        v("codice: una pagina fuori da github.com non si apre", Chiedi("id1", rete(new[] { "{\"device_code\":\"d\",\"user_code\":\"U\",\"verification_uri\":\"https://evil.example.com/\"}" })).Pagina == PAGINA);
        v("codice: github.com finto nel nome non passa", Chiedi("id1", rete(new[] { "{\"device_code\":\"d\",\"user_code\":\"U\",\"verification_uri\":\"https://github.com.evil.example/\"}" })).Pagina == PAGINA);
        Func<Action, string> errore = fai => { try { fai(); return null; } catch (ErroreAccesso ex) { return ex.Message; } };
        v("codice: programma non abilitato", errore(() => Chiedi("id1", rete(new[] { "{\"error\":\"device_flow_disabled\"}" }))) != null);
        v("codice: risposta che non è JSON", errore(() => Chiedi("id1", rete(new[] { "<html>" }))) != null);

        var dormite = new List<int>();
        Action<int> dorme = dormite.Add;
        Func<bool> mai = () => false;
        chiamate.Clear();
        string tok = Aspetta(c, "id1", rete(new[] { "{\"error\":\"authorization_pending\"}", "{\"error\":\"slow_down\",\"interval\":10}", "{\"access_token\":\"" + FINTO + "\",\"token_type\":\"bearer\"}" }), dorme, mai);
        v("attesa: dopo l'attesa arriva l'accesso", tok == FINTO);
        v("attesa: rallenta quando GitHub lo chiede", dormite.SequenceEqual(new[] { 5, 5, 10 }));
        v("attesa: si chiede il tipo giusto", chiamate[0].Contains("device_code=dc1") && chiamate[0].Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code"));
        dormite.Clear();
        tok = Aspetta(c, "id1", rete(new[] { "{\"error\":\"slow_down\"}", "{\"access_token\":\"" + FINTO + "\"}" }), dorme, mai);
        v("attesa: rallenta di 5 se non dice quanto", tok == FINTO && dormite.SequenceEqual(new[] { 5, 10 }));
        var msg = errore(() => Aspetta(c, "id1", rete(new[] { "{\"error\":\"access_denied\",\"error_description\":\"testo di fuori\"}" }), dorme, mai));
        v("attesa: se dici no su GitHub lo dice, a parole nostre", msg != null && msg.Contains("non autorizzare") && !msg.Contains("testo di fuori"));
        v("attesa: codice scaduto da GitHub", errore(() => Aspetta(c, "id1", rete(new[] { "{\"error\":\"expired_token\"}" }), dorme, mai)).Contains("scaduto"));
        v("attesa: codice scaduto da solo, non gira per sempre", errore(() => Aspetta(new CodiceDispositivo { Codice = "d", Scade = 20, Intervallo = 5 }, "id1", rete(new[] { "{\"error\":\"authorization_pending\"}" }), dorme, mai)).Contains("scaduto"));
        v("attesa: annullata", Aspetta(c, "id1", rete(new[] { "{\"error\":\"authorization_pending\"}" }), dorme, () => true) == null);
        v("attesa: la rete salta un attimo e si riprende", Aspetta(c, "id1", rete(new[] { "CADUTA", "CADUTA", "{\"access_token\":\"" + FINTO + "\"}" }), dorme, mai) == FINTO);
        msg = errore(() => Aspetta(c, "id1", rete(new[] { "CADUTA" }), dorme, mai));
        v("attesa: senza rete si ferma e lo dice", msg != null && msg.Contains("connessione"));
        msg = errore(() => Aspetta(c, "id1", rete(new[] { "{\"error\":\"qualcosa\",\"access_tokn\":\"" + FINTO + "\"}" }), dorme, mai));
        v("attesa: nessun messaggio contiene il testo della risposta", msg != null && !msg.Contains(FINTO));

        // la richiesta vera, sui fili, verso un finto GitHub su questo computer
        var finto = new TcpListener(IPAddress.Loopback, 0);
        finto.Start();
        int porta = ((IPEndPoint)finto.LocalEndpoint).Port;
        string ricevuta = "";
        var server = new Thread(() => {
            using (var s = finto.AcceptTcpClient()) {
                var st = s.GetStream(); st.ReadTimeout = 5000;
                var buf = new byte[4096]; int n; var sbr = new StringBuilder();
                while (!sbr.ToString().Contains("\r\n\r\n") || !sbr.ToString().EndsWith("scope=x")) {
                    n = st.Read(buf, 0, buf.Length); if (n <= 0) break;
                    sbr.Append(Encoding.ASCII.GetString(buf, 0, n));
                }
                ricevuta = sbr.ToString();
                var r = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 8\r\nConnection: close\r\n\r\n{\"a\":22}");
                st.Write(r, 0, r.Length);
            }
        }) { IsBackground = true };
        server.Start();
        string sulFilo = null;
        try { sulFilo = PostaA("http://127.0.0.1:" + porta + "/login", "client_id=i&scope=x"); } catch (Exception ex) { sb.AppendLine("   " + ex.Message); }
        server.Join(5000); finto.Stop();
        v("sui fili: arriva la risposta", sulFilo == "{\"a\":22}");
        v("sui fili: POST con JSON e modulo", ricevuta.StartsWith("POST /login HTTP/1.1") && ricevuta.Contains("Accept: application/json") && ricevuta.Contains("Content-Type: application/x-www-form-urlencoded") && ricevuta.EndsWith("client_id=i&scope=x"));
        v("indirizzo fuori dal login di GitHub: si rifiuta", errore2(() => Posta("https://evil.example.com/", "x")));

        // la cassaforte di Windows: una voce finta, con un nome suo, che si toglie subito
        string pref = "ProjectManager-prova-" + Guid.NewGuid().ToString("N") + "/";
        try {
            Cassaforte.Scrivi(pref + "uno", "uno", FINTO);
            Cassaforte.Scrivi(pref + "due", "due", FINTO + "2");
            v("cassaforte: si rilegge uguale", Cassaforte.Leggi(pref + "uno") == FINTO && Cassaforte.Leggi(pref + "due") == FINTO + "2");
            v("cassaforte: elenca solo le sue", Cassaforte.Elenco(pref).OrderBy(x => x).SequenceEqual(new[] { pref + "due", pref + "uno" }));
            Cassaforte.Scrivi(pref + "uno", "uno", "sostituita");
            v("cassaforte: si sostituisce", Cassaforte.Leggi(pref + "uno") == "sostituita");
            Cassaforte.Cancella(pref + "uno");
            v("cassaforte: cancellata non c'è più", Cassaforte.Leggi(pref + "uno") == null);
            Cassaforte.TogliTutto(pref);
            v("cassaforte: togli tutto", Cassaforte.Elenco(pref).Count == 0 && Cassaforte.Leggi(pref + "due") == null);
            v("cassaforte: ciò che non c'è è nullo", Cassaforte.Leggi(pref + "mai") == null);
        } catch (Exception ex) { v("cassaforte: " + ex.Message, false); }
        finally { try { Cassaforte.TogliTutto(pref); } catch { } }
        v("cassaforte: il nome dell'account non distingue le maiuscole", Cassaforte.NomeDi("Pippo") == Cassaforte.NomeDi("pippo") && Cassaforte.NomeDi("pippo").StartsWith(Cassaforte.PREFISSO));

        // siti fidati e cartella: scelte dell'utente, niente di scritto nel programma
        v("siti fidati nuovi: solo GitHub", Dati.FIDATI.OrderBy(x => x).SequenceEqual(new[] { "github.com", "githubusercontent.com" }));
        v("sito fuori lista: quarantena", Dogana.Esamina("t", "vedi https://esempio.it/x", Dati.FIDATI).Count > 0);
        v("sito scelto dall'utente: passa", Dogana.Esamina("t", "vedi https://esempio.it/x", new[] { "github.com", "esempio.it" }).Count == 0);
        string cartellaPrima = Dati.CartellaRepo;
        Dati.CartellaRepo = "";
        v("nessuna cartella delle repo: non si contano le consegne", Dati.ConsegneInAttesa("pippo/a") == -1);
        Dati.CartellaRepo = cartellaPrima;
        string g = TrovaGit();
        v("git: se c'è, sta in PATH e non accanto al programma", g == null || !string.Equals(Path.GetDirectoryName(g), Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), StringComparison.OrdinalIgnoreCase));

        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }

    static bool errore2(Action fai) { try { fai(); return false; } catch (Exception) { return true; } }
}
