using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// un errore di GitHub col suo codice: la Panoramica distingue «non c'è» (404) da «non si è potuto leggere»
// la rete non risponde: il testo è nostro e si può mostrare
public class ErroreSenzaRete : Exception {
    public ErroreSenzaRete(string testo) : base(testo) { }
}

public class ErroreGitHub : Exception {
    public int Codice;
    public bool Limite;     // è il limite alle richieste, non un permesso che manca
    public ErroreGitHub(int codice, string testo) : base(testo) { Codice = codice; }
}

public static class GitHub {
    const string API_VERA = "https://api.github.com";
    static string API = API_VERA;
    static string token;
    static bool custodita;   // la chiave viene dalla Cassaforte, cioè la custodisce il programma: vedi Cliente.GuardaPermessi
    public static string Login;

    public static bool Collegato { get { return token != null; } }

    // mette in uso l'accesso di questo account; dice chi è davvero, che per Git può essere un altro nome
    public static string Accedi(ContoGitHub c) {
        Esci();
        if (c.Origine == Accessi.BROWSER) {
            string t = Cassaforte.Leggi(Cassaforte.NomeDi(c.Login));
            if (t == null) throw new ErroreAccesso(Lingua.T("github.l_accesso_non_c_piu", "login", c.Login));
            Usa(t, c.Login);
            custodita = true;
            return Login;
        }
        Usa(DaGit(c.Origine == Accessi.GIT ? c.Login : null), null);
        Login = LoginDi();
        return Login;
    }

    public static void Usa(string nuovo, string login) { token = nuovo; Login = login; custodita = false; }
    public static void Esci() { token = null; Login = null; custodita = false; }

    // solo per le prove: le stesse strade, contro il GitHub finto su questo computer
    public static void SuServerDiProva(int porta) { API = "http://127.0.0.1:" + porta; }
    public static void SuGitHubVero() { API = API_VERA; }

    // la strada per le scritture, con la stessa chiave: ogni lavoro ne prende una sua
    public static Cliente Scrivente() {
        if (token == null) throw new ErroreScrittura(Lingua.T("github.nessun_accesso_github_ricollega_l"));
        return Cliente.Vero(token, custodita);
    }

    public static string LoginDi() {
        var u = (Dictionary<string, object>)Leggi(API + "/user");
        return Convert.ToString(u["login"]);
    }

    // il protocollo di git: si chiede per github.com, si legge solo la riga «password=», il resto si scarta.
    // Con un nome si chiede l'accesso di quell'account, senza si prende quello che Git propone.
    public static string DaGit(string login) {
        string git = Accessi.TrovaGit();
        if (git == null) throw new ErroreAccesso(Lingua.T("github.questo_computer_non_c_git"));
        var psi = new ProcessStartInfo(git, "credential fill") {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8 };
        string uscita;
        using (var p = Process.Start(psi)) {
            bool nomeBuono = !string.IsNullOrEmpty(login) && Regex.IsMatch(login, "^[A-Za-z0-9-]+$");
            p.StandardInput.Write("protocol=https\nhost=github.com\n" + (nomeBuono ? "username=" + login + "\n" : "") + "\n");
            p.StandardInput.Close();
            var err = p.StandardError.ReadToEndAsync();
            uscita = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(5 * 60 * 1000)) { try { p.Kill(); } catch { } throw new Exception(Lingua.T("github.login_github_non_arrivato_entro")); }
            err.Wait(1000);
            if (p.ExitCode != 0) throw new Exception(Lingua.T("github.git_credential_manager_non_ha", "exitcode", p.ExitCode));
        }
        foreach (var riga in uscita.Split('\n'))
            if (riga.StartsWith("password=")) {
                string t = riga.Substring(9).Trim();
                if (t.Length > 0) return t;
            }
        throw new Exception(Lingua.T("github.git_credential_manager_non_ha_2"));
    }

    static object Leggi(string url) { string prossima; return Leggi(url, out prossima); }

    static object Leggi(string url, out string prossima) {
        if (!url.StartsWith(API + "/")) throw new Exception("indirizzo fuori da api.github.com");  // il token va solo lì
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // TLS 1.2
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "GET";
        req.UserAgent = "ProjectManager/" + Programma.VERSIONE;
        req.Accept = "application/vnd.github+json";
        req.Headers["X-GitHub-Api-Version"] = "2022-11-28";
        req.Headers["Authorization"] = "Bearer " + token;
        req.AllowAutoRedirect = false;
        req.Timeout = 30000;
        try {
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) {
                if ((int)r.StatusCode >= 300) throw new ErroreGitHub((int)r.StatusCode, Lingua.T("github.github_si_spostata_riprova_dopo"));
                prossima = Prossima(r.Headers["Link"]);
                return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(sr.ReadToEnd());
            }
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h == null) throw new ErroreSenzaRete(Lingua.T("github.github_non_risponde_controlla_connessi"));
            int c = (int)h.StatusCode;
            if (c == 401) throw new ErroreGitHub(c, Lingua.T("github.github_non_riconosce_piu_l"));
            // 403 vuol dire due cose: limite alle richieste (ci sono le intestazioni apposta) oppure nessun permesso
            if (c == 429 || (c == 403 && (h.Headers["Retry-After"] != null || h.Headers["X-RateLimit-Remaining"] == "0")))
                throw new ErroreGitHub(c, Lingua.T("github.github_ha_messo_limite_alle")) { Limite = true };
            if (c == 403) throw new ErroreGitHub(c, Lingua.T("github.github_non_permesso_leggerlo"));
            if (c == 404) throw new ErroreGitHub(c, Lingua.T("github.non_c_piu_github_non"));
            throw new ErroreGitHub(c, Lingua.T("github.github_ha_risposto_l_errore", "c", c));
        }
    }

    static string Prossima(string link) {
        if (string.IsNullOrEmpty(link)) return null;
        var m = Regex.Match(link, "<([^>]+)>;\\s*rel=\"next\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    static List<Dictionary<string, object>> Tutte(string url) {
        var tutte = new List<Dictionary<string, object>>();
        for (int pagina = 0; url != null && pagina < 100; pagina++) {
            string prossima;
            foreach (var o in (IEnumerable)Leggi(url, out prossima)) tutte.Add((Dictionary<string, object>)o);
            url = prossima;
        }
        return tutte;
    }

    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }

    // «permissions.admin» che GitHub dà per ogni repo: vero se il login la amministra
    static bool Admin(Dictionary<string, object> d) {
        object p; if (!d.TryGetValue("permissions", out p)) return false;
        var m = p as Dictionary<string, object>; object a;
        return m != null && m.TryGetValue("admin", out a) && a is bool && (bool)a;
    }

    // le organizzazioni di cui l'account è membro: serve solo a dire quali non mostrano repo
    public static List<string> Organizzazioni() {
        return Tutte(API + "/user/orgs?per_page=100").Select(d => S(d, "login")).ToList();
    }

    public static List<RepoInfo> Repo() {
        return Tutte(API + "/user/repos?per_page=100&affiliation=owner,collaborator,organization_member&sort=full_name")
            .Select(d => new RepoInfo {
                Nome = S(d, "full_name"), Descrizione = S(d, "description"),
                Proprietario = S((Dictionary<string, object>)d["owner"], "login"), Ramo = S(d, "default_branch"),
                Privata = d.ContainsKey("private") && d["private"] is bool && (bool)d["private"],
                Organizzazione = S((Dictionary<string, object>)d["owner"], "type") == "Organization",
                Mia = Organizzazione.Mia(S((Dictionary<string, object>)d["owner"], "login"), Login, Admin(d)) }).ToList();
    }

    // issue, pull request e commenti di una repo, aggiornati dopo «dal» (vuoto = tutti)
    public static List<Elemento> Novita(string repo, string dal) {
        string coda = string.IsNullOrEmpty(dal) ? "" : "&since=" + Uri.EscapeDataString(dal);
        var lista = new List<Elemento>();
        foreach (var d in Tutte(API + "/repos/" + repo + "/issues?state=all&per_page=100&sort=updated&direction=asc" + coda)) {
            int n = Convert.ToInt32(d["number"]);
            var e = new Elemento {
                Chiave = "issue:" + S(d, "id"), Repo = repo, Numero = n,
                Tipo = d.ContainsKey("pull_request") && d["pull_request"] != null ? "pull" : "issue",
                Titolo = S(d, "title"), Testo = S(d, "body"),
                Autore = d["user"] == null ? "" : S((Dictionary<string, object>)d["user"], "login"),
                Etichette = string.Join(",", ((IEnumerable)d["labels"]).Cast<Dictionary<string, object>>().Select(l => S(l, "name"))),
                Url = "https://github.com/" + repo + "/issues/" + n,
                Creato = S(d, "created_at"), Aggiornato = S(d, "updated_at"), Aperta = S(d, "state") };
            lista.Add(e);
        }
        foreach (var d in Tutte(API + "/repos/" + repo + "/issues/comments?per_page=100&sort=updated&direction=asc" + coda)) {
            var m = Regex.Match(S(d, "issue_url"), "/issues/(\\d+)$");
            int n = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            lista.Add(new Elemento {
                Chiave = "commento:" + S(d, "id"), Repo = repo, Numero = n, Tipo = "commento",
                Testo = S(d, "body"),
                Autore = d["user"] == null ? "" : S((Dictionary<string, object>)d["user"], "login"),
                Url = "https://github.com/" + repo + "/issues/" + n + "#issuecomment-" + S(d, "id"),
                Creato = S(d, "created_at"), Aggiornato = S(d, "updated_at") });
        }
        return lista;
    }

    // ---------- la Panoramica di una repo ----------

    static Dictionary<string, object> Oggetto(object o, string k) {
        object v;
        return o is Dictionary<string, object> && ((Dictionary<string, object>)o).TryGetValue(k, out v) ? v as Dictionary<string, object> : null;
    }

    // nullo se GitHub risponde 404: per release, licenza e README vuol dire «non c'è»
    static Dictionary<string, object> SeCe(string url) {
        try { return (Dictionary<string, object>)Leggi(url); }
        catch (ErroreGitHub e) { if (e.Codice == 404) return null; throw; }
    }

    // la data dell'ultimo commit; vuota se non ce ne sono (GitHub risponde 409 su una repo vuota)
    static string DataUltimoCommit(string url) {
        try {
            foreach (var o in (IEnumerable)Leggi(url)) return S(Oggetto(Oggetto(o, "commit"), "committer") ?? new Dictionary<string, object>(), "date");
            return "";
        } catch (ErroreGitHub e) { if (e.Codice == 409) return ""; throw; }
    }

    // GitHub non promette l'ordine dei tag: si prende il numero di versione più alto, altrimenti il primo che dà
    static string TagPiuAlto(List<string> tag) {
        string meglio = null; long[] vMeglio = null;
        foreach (var t in tag) {
            var m = Regex.Match(t, @"^v?(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var v = m.Groups[1].Value.Split('.').Select(x => { long n; return long.TryParse(x, out n) ? n : 0; }).ToArray();
            bool piu = vMeglio == null;
            for (int i = 0; !piu && i < Math.Max(v.Length, vMeglio.Length); i++) {
                long a = i < v.Length ? v[i] : 0, b = i < vMeglio.Length ? vMeglio[i] : 0;
                if (a != b) { piu = a > b; break; }
            }
            if (piu) { meglio = t; vMeglio = v; }
        }
        return meglio ?? tag.FirstOrDefault() ?? "";
    }

    // una richiesta alla volta, ognuna per conto suo: il messaggio d'errore finisce sul suo dato, le altre vanno avanti
    static void Prova(string repo, string cosa, Action leggi, Action<string> errore) {
        try { leggi(); errore(null); }
        catch (ErroreGitHub ex) { Dati.Log("panoramica " + repo + " " + cosa + ": " + ex.Message); errore(ex.Message); }
        catch (Exception ex) {
            Dati.Log("panoramica " + repo + " " + cosa + ": " + ex.Message);
            errore(ex is ErroreSenzaRete ? ex.Message : Lingua.T("github.risposta_github_che_non_capisco"));
        }
    }

    // si parte dalla lettura vecchia: quello che stavolta non arriva resta com'era, con l'errore accanto
    public static Panorama Panoramica(RepoInfo r, Panorama vecchia) {
        var p = vecchia == null ? new Panorama() : vecchia.Copia();
        string b = API + "/repos/" + r.Nome;
        int riuscite = 0;
        Prova(r.Nome, "release", () => {
            var d = SeCe(b + "/releases/latest");
            p.ReleaseTag = d == null ? "" : S(d, "tag_name");
            p.ReleaseData = d == null ? "" : S(d, "published_at");
            p.ReleaseLetta = true; riuscite++;
        }, e => p.ReleaseErrore = e);
        Prova(r.Nome, "tag", () => {
            p.UltimoTag = TagPiuAlto(((IEnumerable)Leggi(b + "/tags?per_page=100")).Cast<Dictionary<string, object>>().Select(x => S(x, "name")).ToList());
            p.TagLetto = true; riuscite++;
        }, e => p.TagErrore = e);
        if (p.ReleaseErrore == null && string.IsNullOrEmpty(p.ReleaseTag)) { p.CommitDopo = -1; p.CommitDopoErrore = null; }
        else if (!string.IsNullOrEmpty(p.ReleaseTag))
            Prova(r.Nome, "commit dopo la release", () => {
                string ramo = string.IsNullOrEmpty(r.Ramo) ? "HEAD" : r.Ramo;
                var d = (Dictionary<string, object>)Leggi(b + "/compare/" + Uri.EscapeDataString(p.ReleaseTag) + "..." + Uri.EscapeDataString(ramo) + "?per_page=1");
                p.CommitDopo = Convert.ToInt32(d["ahead_by"]); riuscite++;
            }, e => p.CommitDopoErrore = e);
        Prova(r.Nome, "ultimo commit", () => {
            p.UltimoCommit = DataUltimoCommit(b + "/commits?per_page=1");
            p.CommitLetto = true; riuscite++;
        }, e => p.CommitErrore = e);
        Prova(r.Nome, "collaboratori", () => {
            p.Collaboratori = Tutte(b + "/collaborators?per_page=100").Select(x => new Collaboratore { Nome = S(x, "login"), Ruolo = S(x, "role_name") }).ToList();
            riuscite++;
        }, e => p.CollaboratoriErrore = e);
        Prova(r.Nome, "licenza", () => {
            var d = SeCe(b + "/license");
            var l = d == null ? null : Oggetto(d, "license");
            p.LicenzaAltra = d != null && (l == null || S(l, "spdx_id") == "NOASSERTION");
            p.Licenza = d == null ? "" : p.LicenzaAltra ? NomeDalFile(S(d, "content")) : S(l, "name");
            p.LicenzaLetta = true; riuscite++;
        }, e => p.LicenzaErrore = e);
        string percorso = null;
        Prova(r.Nome, "README", () => {
            var d = SeCe(b + "/readme");
            p.Readme = d != null;
            p.ReadmeCopyright = d != null && HaCopyright(S(d, "content"));
            if (d == null) p.ReadmeData = "";
            percorso = d == null ? null : S(d, "path");
            p.ReadmeLetto = true; riuscite++;
        }, e => p.ReadmeErrore = e);
        if (percorso != null)
            Prova(r.Nome, "data del README", () => {
                p.ReadmeData = DataUltimoCommit(b + "/commits?per_page=1&path=" + Uri.EscapeDataString(percorso));
                riuscite++;
            }, e => p.ReadmeDataErrore = e);
        else p.ReadmeDataErrore = null;
        if (riuscite > 0) p.LettoIl = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        return p;
    }

    // ---------- i numeri di «Come sta andando» ----------

    // un numero che manca o che non è un numero è una risposta che non capisco: non diventa mai uno 0
    static long Conteggio(Dictionary<string, object> d, string k) {
        object v;
        if (!d.TryGetValue(k, out v)) throw new FormatException(k);
        long n;
        if (v is int) n = (int)v; else if (v is long) n = (long)v; else throw new FormatException(k);
        if (n < 0) throw new FormatException(k);
        return n;
    }

    // GitHub dà il traffico solo a chi può scrivere nella repo: qui il 403 ha un perché preciso
    static object Traffico(string url) {
        try { return Leggi(url); }
        catch (ErroreGitHub e) {
            if (e.Codice == 403 && !e.Limite) throw new ErroreGitHub(403, Lingua.T("github.traffico_solo_chi_scrive"));
            throw;
        }
    }

    static List<GiornoTraffico> GiorniDi(Dictionary<string, object> d, string lista) {
        var g = new List<GiornoTraffico>();
        foreach (var o in (IEnumerable)d[lista]) {
            var x = (Dictionary<string, object>)o;
            string t = S(x, "timestamp");
            if (!Regex.IsMatch(t, @"^\d{4}-\d{2}-\d{2}")) throw new FormatException(lista);
            g.Add(new GiornoTraffico { Giorno = t.Substring(0, 10), Conta = Conteggio(x, "count"), Unici = Conteggio(x, "uniques") });
        }
        return g;
    }

    static List<VoceTraffico> VociDi(object risposta, string nome) {
        var l = new List<VoceTraffico>();
        foreach (var o in (IEnumerable)risposta) {
            var x = (Dictionary<string, object>)o;
            l.Add(new VoceTraffico { Nome = S(x, nome), Titolo = S(x, "title"), Conta = Conteggio(x, "count"), Unici = Conteggio(x, "uniques") });
        }
        return l;
    }

    // come la Panoramica: si parte dalla lettura vecchia, ogni richiesta sta per conto suo. Si assegna solo a risposta capita per intero
    public static NumeriRepo LeggiNumeri(RepoInfo r, NumeriRepo vecchi) {
        var n = vecchi == null ? new NumeriRepo() : vecchi.Copia();
        string b = API + "/repos/" + r.Nome;
        int riuscite = 0;
        Prova(r.Nome, "visite", () => {
            var d = (Dictionary<string, object>)Traffico(b + "/traffic/views");
            var g = GiorniDi(d, "views");
            long tot = Conteggio(d, "count"), uni = Conteggio(d, "uniques");
            n.VisiteGiorni = g; n.Visite = tot; n.VisiteUnici = uni; n.VisiteLette = true; riuscite++;
        }, e => n.VisiteErrore = e);
        Prova(r.Nome, "cloni", () => {
            var d = (Dictionary<string, object>)Traffico(b + "/traffic/clones");
            var g = GiorniDi(d, "clones");
            long tot = Conteggio(d, "count"), uni = Conteggio(d, "uniques");
            n.CloniGiorni = g; n.Cloni = tot; n.CloniUnici = uni; n.CloniLetti = true; riuscite++;
        }, e => n.CloniErrore = e);
        Prova(r.Nome, "provenienza", () => {
            n.Fonti = VociDi(Traffico(b + "/traffic/popular/referrers"), "referrer"); n.FontiLette = true; riuscite++;
        }, e => n.FontiErrore = e);
        Prova(r.Nome, "pagine", () => {
            n.Pagine = VociDi(Traffico(b + "/traffic/popular/paths"), "path"); n.PagineLette = true; riuscite++;
        }, e => n.PagineErrore = e);
        Prova(r.Nome, "download", () => {
            var v = new List<VersioneScaricata>();
            long tot = 0;
            foreach (var d in Tutte(b + "/releases?per_page=100")) {
                object bozza;
                if (d.TryGetValue("draft", out bozza) && bozza is bool && (bool)bozza) continue;
                var x = new VersioneScaricata { Tag = S(d, "tag_name") };
                foreach (var o in (IEnumerable)d["assets"]) {
                    var al = (Dictionary<string, object>)o;
                    long q = Conteggio(al, "download_count");
                    x.Allegati.Add(new AllegatoScaricato { Nome = S(al, "name"), Download = q });
                    x.Download += q;
                }
                tot += x.Download;
                v.Add(x);
            }
            n.Versioni = v; n.Download = tot; n.DownloadLetti = true; riuscite++;
        }, e => n.DownloadErrore = e);
        Prova(r.Nome, "stelle e copie", () => {
            var d = (Dictionary<string, object>)Leggi(b);
            long stelle = Conteggio(d, "stargazers_count"), copie = Conteggio(d, "forks_count"), oss = Conteggio(d, "subscribers_count");
            string oggi = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (n.RepoLetta && stelle != n.Stelle) { n.StellePrima = n.Stelle; n.StelleCambiate = oggi; }
            if (n.RepoLetta && copie != n.Copie) { n.CopiePrima = n.Copie; n.CopieCambiate = oggi; }
            n.Stelle = stelle; n.Copie = copie; n.Osservatori = oss; n.RepoLetta = true; riuscite++;
        }, e => n.RepoErrore = e);
        if (riuscite > 0) n.LettoIl = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        return n;
    }

    // una licenza che GitHub non riconosce: il nome è la prima riga del file, se sembra un nome di licenza
    static string NomeDalFile(string base64) {
        try {
            string t = Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(base64 ?? "", @"\s+", "")));
            string r = t.Split('\n').Select(x => x.Trim(' ', '\t', '\r', '#', '*', '=')).FirstOrDefault(x => x.Length > 0) ?? "";
            if (r.Length >= 3 && r.Length <= 60 && Regex.IsMatch(r, "licen[sz]", RegexOptions.IgnoreCase)) return r;
        } catch { }
        return Lingua.T("github.altra_licenza");
    }

    // del testo del README si guarda solo se c'è un avviso di copyright: il testo non si tiene e non si mostra
    static bool HaCopyright(string base64) {
        try {
            string t = Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(base64 ?? "", @"\s+", "")));
            return Regex.IsMatch(t, @"\bcopyright\b|©|\(c\)\s*(19|20)\d\d", RegexOptions.IgnoreCase);
        } catch { return false; }
    }
}
