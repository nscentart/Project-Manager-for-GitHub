using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public class ErroreScrittura : Exception {
    public int Codice;
    public List<Riscontro> Riscontri;
    public ErroreScrittura(string testo) : base(testo) { }
    public ErroreScrittura(int codice, string testo) : base(testo) { Codice = codice; }
    public ErroreScrittura(string testo, List<Riscontro> riscontri) : base(testo) { Riscontri = riscontri; }
}

public class Risposta {
    public int Codice;
    public object Json;
    public Dictionary<string, object> D { get { return Json as Dictionary<string, object> ?? new Dictionary<string, object>(); } }
    public IEnumerable<Dictionary<string, object>> L { get { return Json is IEnumerable && !(Json is string) && !(Json is IDictionary) ? ((IEnumerable)Json).Cast<object>().OfType<Dictionary<string, object>>() : new Dictionary<string, object>[0]; } }
}

// la strada verso GitHub. Gli indirizzi sono fissi: si passa solo il pezzo dopo il sito, così la chiave non può andare altrove.
public class Cliente {
    const string API = "https://api.github.com";
    // l'unica eccezione ad api.github.com: GitHub accetta gli allegati delle release solo qui. L'indirizzo è questo, scritto da noi:
    // quello che GitHub propone nella risposta («upload_url») non si usa mai
    const string CARICAMENTI = "https://uploads.github.com";
    static readonly string[] VIETATI = { "delete_repo", "admin:org", "workflow" };

    readonly string radice, radiceCarica;
    readonly bool salvata;
    string token;
    public string Permessi;     // i permessi che GitHub dice di questa chiave, se li dice

    Cliente(string r, string c, string t, bool s) { radice = r; radiceCarica = c; token = t; salvata = s; }
    // salvata: una chiave che il programma custodisce. Quella di Git la gestisce Git, e può avere permessi più larghi
    public static Cliente Vero(string token, bool salvata) { return new Cliente(API, CARICAMENTI, token, salvata); }
    public static Cliente Finto(int porta, string token, bool salvata) { string b = "http://127.0.0.1:" + porta; return new Cliente(b, b + "/caricamenti", token, salvata); }

    public void Butta() { token = null; }
    public bool Buttata { get { return token == null; } }

    public static bool HaVietati(string permessi) { return Pezzi(permessi).Any(x => VIETATI.Contains(x)); }
    public bool HaPermesso(string p) { return Pezzi(Permessi).Contains(p); }
    static string[] Pezzi(string s) { return (s ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray(); }

    public Risposta Chiama(string metodo, string percorso, object corpo) {
        byte[] b = corpo == null ? null : Encoding.UTF8.GetBytes(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(corpo));
        return Invia(radice, metodo, percorso, b, "application/json", 60000);
    }

    public Risposta Carica(string percorso, byte[] dati) { return Invia(radiceCarica, "POST", percorso, dati, "application/octet-stream", 30 * 60000); }

    Risposta Invia(string sito, string metodo, string percorso, byte[] corpo, string tipo, int attesa) {
        if (token == null) throw new ErroreScrittura(Lingua.T("scrittura.questo_accesso_gia_stato_usato"));
        if (!percorso.StartsWith("/") || percorso.StartsWith("//") || percorso.Contains("://") || percorso.Contains("\\")) throw new ErroreScrittura(Lingua.T("scrittura.indirizzo_non_valido_richiesta_non"));
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // TLS 1.2
        var req = (HttpWebRequest)WebRequest.Create(sito + percorso);
        req.Method = metodo;
        req.UserAgent = "ProjectManager/" + Programma.VERSIONE;
        req.Accept = "application/vnd.github+json";
        req.Headers["X-GitHub-Api-Version"] = "2022-11-28";
        req.Headers["Authorization"] = "Bearer " + token;
        req.AllowAutoRedirect = false;
        req.ServicePoint.Expect100Continue = false;
        req.Timeout = attesa; req.ReadWriteTimeout = attesa;
        string dove = percorso.Split('?')[0];
        try {
            if (corpo != null || metodo != "GET") {
                req.ContentType = tipo;
                req.ContentLength = corpo == null ? 0 : corpo.Length;
                if (corpo != null) using (var s = req.GetRequestStream()) s.Write(corpo, 0, corpo.Length);
            }
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) {
                string testo = sr.ReadToEnd();
                GuardaPermessi(r.Headers["X-OAuth-Scopes"]);
                int c = (int)r.StatusCode;
                if (metodo != "GET") Dati.Log("scrittura " + metodo + " " + dove + ": " + c);
                if (c >= 300) throw new ErroreScrittura(c, metodo == "GET" ? Lingua.T("scrittura.github_questa_repo_ha_cambiato")
                                                            : Lingua.T("scrittura.github_questa_repo_ha_cambiato_2"));
                return new Risposta { Codice = c, Json = testo.Length == 0 ? null : new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(testo) };
            }
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h == null) throw new ErroreScrittura(Lingua.T("comune.github_non_risponde_controlla_connessi"));
            string testo = "";
            try { using (var sr = new StreamReader(h.GetResponseStream(), Encoding.UTF8)) testo = sr.ReadToEnd(); } catch { }
            int c = (int)h.StatusCode;
            if (metodo != "GET" || (c != 404 && c != 409)) Dati.Log("scrittura " + metodo + " " + dove + ": " + c);
            GuardaPermessi(h.Headers["X-OAuth-Scopes"]);
            throw new ErroreScrittura(c, Frasi.Di(c, testo, h.Headers["Retry-After"] != null || h.Headers["X-RateLimit-Remaining"] == "0"));
        } catch (ArgumentException) { throw new ErroreScrittura(Lingua.T("comune.github_ha_dato_risposta_che")); }
    }

    // nessuna chiave custodita dal programma deve avere quei permessi: se succede non si scrive, e lo si dice
    void GuardaPermessi(string dettoDaGitHub) {
        if (dettoDaGitHub == null) return;
        Permessi = dettoDaGitHub;
        if (salvata && HaVietati(dettoDaGitHub)) {
            Butta();
            throw new ErroreScrittura(Lingua.T("scrittura.chiave_questo_account_ha_piu"));
        }
    }
}

// le risposte di GitHub in parole semplici: il loro testo serve solo a capire il caso, non si mostra mai
public static class Frasi {
    public static string BLOCCO_ORG { get { return Lingua.T("scrittura.l_organizzazione_che_possiede_questa"); } }
    public static string WORKFLOW { get { return Lingua.T("scrittura.file_github_workflows_fanno_partire"); } }

    public static string Di(int codice, string testo, bool limite) {
        string t = (testo ?? "").ToLowerInvariant();
        if (codice == 401) return Lingua.T("scrittura.github_non_riconosce_piu_l");
        if (codice == 429 || (codice == 403 && (limite || t.Contains("rate limit")))) return Lingua.T("scrittura.github_ha_messo_limite_alle");
        if (t.Contains("oauth app access restrictions")) return BLOCCO_ORG;
        if (t.Contains("saml")) return Lingua.T("scrittura.questa_organizzazione_chiede_l_accesso");
        if (t.Contains("workflow")) return WORKFLOW;
        if (t.Contains("archived")) return Lingua.T("scrittura.repo_archiviata_cioe_sola_lettura");
        if (t.Contains("protected branch")) return Lingua.T("scrittura.ramo_principale_protetto_regole_della");
        if (codice == 403) return Lingua.T("scrittura.github_non_permesso_farlo_tuo");
        if (codice == 404) return Lingua.T("scrittura.github_non_c_oppure_non");
        if (codice == 409) return t.Contains("empty") ? Lingua.T("scrittura.repo_ancora_vuota") : Lingua.T("scrittura.github_cambiata_nel_frattempo_rileggi");
        if (codice == 422) {
            if (t.Contains("name already exists")) return Lingua.T("scrittura.esiste_gia_repo_questo_nome");
            if (t.Contains("visibility")) return Lingua.T("scrittura.github_non_permette_ora_questo");
            if (t.Contains("already_exists") && t.Contains("releaseasset")) return Lingua.T("scrittura.questa_release_ha_gia_allegato");
            if (t.Contains("already_exists")) return Lingua.T("scrittura.esiste_gia_release_questo_tag");
            if (t.Contains("not a valid") || t.Contains("could not resolve")) return Lingua.T("scrittura.github_non_riconosce_questo_nome");
            return Lingua.T("scrittura.github_ha_rifiutato_questi_dati");
        }
        if (codice >= 500) return Lingua.T("scrittura.github_ha_problema_questo_momento");
        return Lingua.T("scrittura.github_ha_risposto_l_errore", "codice", codice);
    }
}

public class FattiRepo {
    public long Id, Peso;
    public string Nome, Proprietario, Descrizione, Ramo, Ultima, Sito;
    public string Nota;     // dopo una creazione: quello che è andato storto a metà
    public bool Privata, Archiviata, Organizzazione, Admin, Maintain, Push, Triage, Pull, Wiki;
    public int Stelle, Copie, Segnalazioni;
    public string NomeCorto { get { return Nome.Substring(Nome.IndexOf('/') + 1); } }
}

public enum Azione { Nome, Descrizione, Visibilita, Archivio, Trasferimento, Contenuto, Release, Collaboratori, Cancellazione }

// chi può fare cosa, con la frase da mostrare accanto al tasto spento. Nullo = si può
public static class Diritti {
    public static string Ruolo(FattiRepo f) { return f.Admin ? Lingua.T("scrittura.amministri") : f.Maintain ? Lingua.T("scrittura.gestisci") : f.Push ? Lingua.T("scrittura.scrivi") : f.Triage ? Lingua.T("scrittura.ordini_segnalazioni") : f.Pull ? Lingua.T("scrittura.leggi_soltanto") : Lingua.T("scrittura.non_hai_nessun_ruolo"); }

    public static string Perche(Azione a, FattiRepo f) {
        if (f.Archiviata && a != Azione.Archivio && a != Azione.Cancellazione) return Lingua.T("scrittura.repo_archiviata_cioe_sola_lettura");
        string ruolo = Lingua.T("scrittura.qui", "f", Ruolo(f));
        switch (a) {
            case Azione.Contenuto:
            case Azione.Release:
                return f.Push ? null : Lingua.T("scrittura.farlo_serve_permesso_scrivere_puo", "ruolo", ruolo);
            case Azione.Descrizione:
                return f.Admin || f.Maintain ? null : Lingua.T("scrittura.serve_essere_amministratore_gestore_de", "ruolo", ruolo);
            default:
                return f.Admin ? null : Lingua.T("scrittura.serve_essere_amministratore_della_repo", "ruolo", ruolo);
        }
    }

    public static void Consenti(Azione a, FattiRepo f) {
        string p = Perche(a, f);
        if (p != null) throw new ErroreScrittura(403, p);
    }
}

public class Proprietario {
    public string Nome, Perche;     // Perche: nullo = qui si può creare una repo
    public bool Organizzazione;
    public override string ToString() { return Nome + (Organizzazione ? Lingua.T("scrittura.organizzazione") : Lingua.T("scrittura.personale")); }
}

public class PersonaRepo {
    public string Nome, Ruolo;      // read, triage, write, maintain, admin
    public long Invito;             // diverso da zero: non ha ancora accettato
}

public class AllegatoInfo { public string Nome; public long Peso; }

public class ReleaseInfo {
    public long Id;
    public string Tag, Titolo, Note, Ramo, Data;
    public bool Bozza, Anteprima;
    public List<AllegatoInfo> Allegati = new List<AllegatoInfo>();
}

public class FileRepo { public string Percorso, Sha, Testo, FineRiga = "\n"; public long Peso; public bool Bom; }

public class FileDaCaricare { public string Percorso, Pieno; public long Peso; }

// una cartella preparata per il caricamento: se è bloccata non parte niente
public class Cartella {
    public string Radice;
    public List<FileDaCaricare> File = new List<FileDaCaricare>();
    public List<string> Saltati = new List<string>();     // .git e quello che dice .gitignore
    public List<string> Rifiutati = new List<string>();   // workflow, collegamenti, file troppo grandi
    public List<Riscontro> Riscontri = new List<Riscontro>();
    public List<string> DaConfermare = new List<string>();     // archivi e basi di dati: dentro non si guarda, si carica solo col sì
    public HashSet<string> Confermati = new HashSet<string>();
    public List<string> NonLeggibili = new List<string>();     // documenti e immagini: si dice, non ferma
    public List<Riscontro> Avvisi = new List<Riscontro>();     // email e codici fiscali: si dice, non ferma
    public long Peso;
    public bool Bloccata { get { return Rifiutati.Count > 0 || Riscontri.Count > 0; } }
}

public class EsitoCarica { public string Commit; public int File, Passi; public bool EraVuota; }

// il controllo prima di passare a pubblica: vale solo per le punte dei rami che ha guardato
public class ControlloPubblica {
    public long RepoId;
    public string Sha, Incompleto;      // Sha: l'impronta di tutti i rami, ognuno col suo ultimo commit
    public int Guardati, Rami, Commit, GuardatiStoria, Issue, Pull, Release, Commenti, Allegati, EmailInChiaro;
    public bool Wiki;
    public List<string> NonGuardati = new List<string>();
    public List<Riscontro> Riscontri = new List<Riscontro>();
    public List<Riscontro> Avvisi = new List<Riscontro>();     // email e codici fiscali: si dicono, non fermano
    public bool Pulito { get { return Incompleto == null && Riscontri.Count == 0 && NonGuardati.Count == 0; } }
}

public static class Scrittura {
    public const int MAX_FILE = 400;
    public const long MAX_PESO_FILE = 100L * 1024 * 1024, MAX_PESO = 500L * 1024 * 1024, MAX_ALLEGATO = 1024L * 1024 * 1024;
    const long MAX_DA_GUARDARE = 100L * 1024 * 1024, MAX_LETTI = 300L * 1024 * 1024;
    const int MAX_GUARDATI = 1000, MAX_RAMI = 300, MAX_PAGINE = 30;
    // quanto si guarda della storia, dentro le 5000 richieste all'ora di GitHub e lasciandone per il resto. Le prove li abbassano
    public static int MaxCommitStoria = 1000, MaxFileStoria = 2000, MaxRichieste = 4000;

    // ---------- pezzi comuni ----------

    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
    static bool B(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is bool && (bool)v; }
    static long N(Dictionary<string, object> d, string k) { long n; return long.TryParse(S(d, k), out n) ? n : 0; }
    static Dictionary<string, object> O(Dictionary<string, object> d, string k) { object v; return (d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null) ?? new Dictionary<string, object>(); }
    static Dictionary<string, object> D(params object[] kv) { var d = new Dictionary<string, object>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1]; return d; }

    public static bool NomeRepoValido(string n) { return !string.IsNullOrEmpty(n) && n.Length <= 100 && Regex.IsMatch(n, @"^[A-Za-z0-9._-]+$") && n.Trim('.').Length > 0 && !n.EndsWith(".git", StringComparison.OrdinalIgnoreCase); }
    public static bool LoginValido(string n) { return !string.IsNullOrEmpty(n) && n.Length <= 39 && Regex.IsMatch(n, @"^[A-Za-z0-9-]+$"); }
    static bool RepoValida(string r) { var p = (r ?? "").Split('/'); return p.Length == 2 && LoginValidoLargo(p[0]) && Regex.IsMatch(p[1], @"^[A-Za-z0-9._-]+$") && p[1].Trim('.').Length > 0; }
    static bool LoginValidoLargo(string n) { return Regex.IsMatch(n, @"^[A-Za-z0-9._-]+$"); }   // i nomi veri di GitHub possono avere _ per le aziende gestite
    static string RepoSicura(string r) { if (!RepoValida(r)) throw new ErroreScrittura(Lingua.T("scrittura.nome_questa_repo_non_valido")); return r; }

    public static bool Workflow(string percorso) {
        string p = (percorso ?? "").Replace('\\', '/').TrimStart('/');
        return p.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) || p.Equals(".github/workflows", StringComparison.OrdinalIgnoreCase);
    }

    // un percorso dentro la repo: niente salite, niente .git, niente caratteri di controllo
    public static string PercorsoSicuro(string percorso) {
        string p = (percorso ?? "").Trim().Replace('\\', '/');
        if (p.Length == 0 || p.StartsWith("/") || p.Any(char.IsControl)) throw new ErroreScrittura(Lingua.T("scrittura.questo_percorso_non_valido"));
        foreach (var s in p.Split('/'))
            if (s.Length == 0 || s == "." || s == ".." || s.Equals(".git", StringComparison.OrdinalIgnoreCase)) throw new ErroreScrittura(Lingua.T("scrittura.questo_percorso_non_valido"));
        return p;
    }

    static string Segmenti(string p) { return string.Join("/", p.Split('/').Select(Uri.EscapeDataString)); }

    static void Permesso(string percorso) { if (Workflow(percorso)) throw new ErroreScrittura(403, Frasi.WORKFLOW); }

    static void FermaSeTrova(List<Riscontro> trovati, string cosa) {
        if (trovati.Count > 0) { Dati.Log("controllo segreti: fermato, " + trovati.Count + " cose trovate"); throw new ErroreScrittura(cosa, trovati); }
    }
    static string FERMO { get { return Lingua.T("scrittura.mi_fermo_qui_dentro_sembra"); } }

    // chi firma un commit: il login e l'indirizzo finto che GitHub dà a ogni account, mai l'email vera. Senza l'id non si scrive
    static Dictionary<string, object> Firma(Cliente c) {
        var d = c.Chiama("GET", "/user", null).D;
        long id = N(d, "id"); string login = S(d, "login");
        if (id <= 0 || !LoginValidoLargo(login)) throw new ErroreScrittura(Lingua.T("scrittura.github_non_mi_ha_detto"));
        return D("name", login, "email", id + "+" + login + "@users.noreply.github.com");
    }

    // ---------- leggere prima di scrivere ----------

    static FattiRepo Leggi(Dictionary<string, object> d) {
        var p = O(d, "permissions"); var o = O(d, "owner");
        return new FattiRepo {
            Id = N(d, "id"), Nome = S(d, "full_name"), Proprietario = S(o, "login"), Organizzazione = S(o, "type") == "Organization", Descrizione = S(d, "description"), Sito = S(d, "homepage"), Wiki = B(d, "has_wiki"),
            Ramo = S(d, "default_branch"), Privata = B(d, "private"), Archiviata = B(d, "archived"), Ultima = S(d, "pushed_at"), Peso = N(d, "size"),
            Stelle = (int)N(d, "stargazers_count"), Copie = (int)N(d, "forks_count"), Segnalazioni = (int)N(d, "open_issues_count"),
            Admin = B(p, "admin"), Maintain = B(p, "maintain"), Push = B(p, "push"), Triage = B(p, "triage"), Pull = B(p, "pull") };
    }

    public static FattiRepo Fatti(Cliente c, string repo) { return Leggi(c.Chiama("GET", "/repos/" + RepoSicura(repo), null).D); }

    // rilegge e controlla che sia ancora la repo vista nella conferma, e che si possa ancora fare
    static FattiRepo Rilettura(Cliente c, FattiRepo visti, Azione a) {
        if (visti == null) throw new ErroreScrittura(Lingua.T("scrittura.prima_devo_rileggere_repo_github"));
        var f = Fatti(c, visti.Nome);
        if (f.Id != visti.Id || f.Nome != visti.Nome) throw new ErroreScrittura(Lingua.T("scrittura.github_repo_cambiata_nel_frattempo"));
        if (f.Privata != visti.Privata) throw new ErroreScrittura(Lingua.T("scrittura.mentre_guardavi_repo_diventata_non", "parte", (f.Privata ? Lingua.T("comune.privata") : Lingua.T("comune.pubblica"))));
        Diritti.Consenti(a, f);
        return f;
    }

    // come Rilettura, in più vuole il nome scritto a mano, uguale a quello che dice GitHub adesso
    static FattiRepo Riconferma(Cliente c, FattiRepo visti, string scritto, Azione a) {
        var f = Rilettura(c, visti, a);
        if (scritto != f.Nome) throw new ErroreScrittura(Lingua.T("scrittura.nome_scritto_non_quello_della"));
        return f;
    }

    // ---------- creare ----------

    // dove si può creare: il tuo account e le organizzazioni di cui sei membro. GitHub resta il giudice: se non dice, si prova
    public static List<Proprietario> Proprietari(Cliente c, string login) {
        var l = new List<Proprietario> { new Proprietario { Nome = login } };
        foreach (var m in c.Chiama("GET", "/user/memberships/orgs?state=active&per_page=100", null).L) {
            string org = S(O(m, "organization"), "login");
            if (!RepoValida(org + "/x")) continue;
            var p = new Proprietario { Nome = org, Organizzazione = true };
            if (S(m, "role") != "admin")
                try {
                    var d = c.Chiama("GET", "/orgs/" + org, null).D;
                    if ((d.ContainsKey("members_can_create_private_repositories") && !B(d, "members_can_create_private_repositories"))
                        || (d.ContainsKey("members_can_create_repositories") && !B(d, "members_can_create_repositories")))
                        p.Perche = Lingua.T("scrittura.questa_organizzazione_membri_non_posso");
                } catch (ErroreScrittura) { }
            l.Add(p);
        }
        return l;
    }

    // un nome che risponde con un rimando era di una repo rinominata o spostata: chi crea quel nome si prende anche chi ha ancora il vecchio indirizzo
    static void NomeLibero(Cliente c, string repo) {
        try { c.Chiama("GET", "/repos/" + RepoSicura(repo), null); }
        catch (ErroreScrittura e) {
            if (e.Codice == 404) return;
            if (e.Codice >= 300 && e.Codice < 400) throw new ErroreScrittura(e.Codice, Lingua.T("scrittura.nome_era_repo_che_stata", "repo", repo));
            throw;
        }
        throw new ErroreScrittura(Lingua.T("scrittura.esiste_gia_repo_questo_nome"));
    }

    // una repo nasce sempre privata: non c'è modo di chiederla pubblica qui.
    // Il README non lo fa GitHub (lì firmerebbe con l'email vera): è il primo file, scritto da qui con la firma giusta
    public static FattiRepo CreaRepo(Cliente c, string login, Proprietario dove, string nome, string descrizione, bool conReadme) {
        if (!NomeRepoValido(nome)) throw new ErroreScrittura(Lingua.T("comune.nome_puo_avere_lettere_senza"));
        if (dove == null) throw new ErroreScrittura(Lingua.T("scrittura.scegli_chi_sara_repo"));
        if (dove.Perche != null) throw new ErroreScrittura(403, dove.Perche);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.descrizione"), descrizione), FERMO);
        string via = dove.Organizzazione ? "/orgs/" + RepoSicura(dove.Nome + "/x").Split('/')[0] + "/repos" : "/user/repos";
        NomeLibero(c, dove.Nome + "/" + nome);
        if (conReadme) Firma(c);
        var f = Leggi(c.Chiama("POST", via, D("name", nome, "description", descrizione ?? "", "private", true, "auto_init", false)).D);
        if (!f.Privata) throw new ErroreScrittura(Lingua.T("scrittura.github_dice_che_repo_non"));
        if (conReadme)
            try { ScriviFile(c, f, "README.md", "# " + nome + "\n" + (string.IsNullOrWhiteSpace(descrizione) ? "" : "\n" + descrizione + "\n"), null, Lingua.T("scrittura.primo_readme")); }
            catch (ErroreScrittura e) { f.Nota = Lingua.T("scrittura.repo_creata_privata_ma_readme", "errore", e.Message); }
        return f;
    }

    // ---------- modificare ----------

    public static void CambiaDescrizione(Cliente c, FattiRepo visti, string testo) {
        var f = Rilettura(c, visti, Azione.Descrizione);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.descrizione"), testo), FERMO);
        c.Chiama("PATCH", "/repos/" + f.Nome, D("description", testo ?? ""));
    }

    // il sito della repo: solo https, senza nome e password dentro. Vuoto: nessun sito
    public static bool SitoValido(string url) {
        string u = url ?? "";
        if (u.Length == 0) return true;
        if (u.Length > 255 || !u.StartsWith("https://", StringComparison.Ordinal) || u.Any(x => char.IsWhiteSpace(x) || char.IsControl(x) || "<>\"'`".IndexOf(x) >= 0)) return false;
        Uri a;
        if (!Uri.TryCreate(u, UriKind.Absolute, out a) || a.Scheme != Uri.UriSchemeHttps || a.UserInfo.Length > 0) return false;
        string h = a.Host;
        return h.IndexOf('.') > 0 && !h.EndsWith(".");
    }

    public static void CambiaSito(Cliente c, FattiRepo visti, string url) {
        string u = (url ?? "").Trim();
        if (!SitoValido(u)) throw new ErroreScrittura(Lingua.T("scrittura.sito_deve_essere_indirizzo_che"));
        var f = Rilettura(c, visti, Azione.Descrizione);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.sito"), u), FERMO);
        c.Chiama("PATCH", "/repos/" + f.Nome, D("homepage", u));
    }

    public static string Rinomina(Cliente c, FattiRepo visti, string scritto, string nuovo) {
        if (!NomeRepoValido(nuovo)) throw new ErroreScrittura(Lingua.T("comune.nome_puo_avere_lettere_senza"));
        var f = Riconferma(c, visti, scritto, Azione.Nome);
        string n = S(c.Chiama("PATCH", "/repos/" + f.Nome, D("name", nuovo)).D, "full_name");
        return RepoValida(n) ? n : f.Proprietario + "/" + nuovo;
    }

    public static void Archivia(Cliente c, FattiRepo visti, bool archivia) {
        var f = Rilettura(c, visti, Azione.Archivio);
        c.Chiama("PATCH", "/repos/" + f.Nome, D("archived", archivia));
    }

    public static void RendiPrivata(Cliente c, FattiRepo visti) {
        var f = Rilettura(c, visti, Azione.Visibilita);
        c.Chiama("PATCH", "/repos/" + f.Nome, D("private", true));
    }

    // passa a pubblica solo col controllo pulito e fatto sugli stessi rami, allo stesso punto, che ci sono adesso
    public static void RendiPubblica(Cliente c, FattiRepo visti, string scritto, ControlloPubblica k) {
        var f = Riconferma(c, visti, scritto, Azione.Visibilita);
        if (k == null || k.RepoId != f.Id) throw new ErroreScrittura(Lingua.T("scrittura.prima_passare_pubblica_devo_controllar"));
        FermaSeNonPulito(k);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.descrizione"), f.Descrizione), FERMO_PUBBLICA);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.sito"), f.Sito), FERMO_PUBBLICA);
        bool troppi;
        var punteOra = Punte(c, f, out troppi);
        bool troppiAltri = false;
        if (!troppi) punteOra.AddRange(AltrePunte(c, f, out troppiAltri));
        string ora = ImprontaRami(punteOra);
        if (troppi || troppiAltri || ora != k.Sha) throw new ErroreScrittura(Lingua.T("scrittura.file_sono_cambiati_dopo_controllo"));
        if (B(c.Chiama("PATCH", "/repos/" + f.Nome, D("private", false)).D, "private")) throw new ErroreScrittura(Lingua.T("scrittura.github_dice_che_repo_ancora"));
    }

    static string FERMO_PUBBLICA { get { return Lingua.T("scrittura.mi_fermo_nella_repo_c"); } }
    static string NELLA_STORIA { get { return Lingua.T("scrittura.nella_storia_commit"); } }
    static string NEL_MESSAGGIO { get { return Lingua.T("scrittura.messaggio_del_commit"); } }
    static string FERMO_STORIA { get { return Lingua.T("scrittura.quello_che_sta_nella_storia"); } }

    // quello che ferma il passaggio a pubblica: il controllo non è pulito, oppure non è finito
    public static void FermaSeNonPulito(ControlloPubblica k) {
        if (k.Riscontri.Count > 0) throw new ErroreScrittura(FERMO_PUBBLICA + (k.Riscontri.Any(x => x.File.Contains(NELLA_STORIA) || x.File.StartsWith(NEL_MESSAGGIO)) ? FERMO_STORIA : ""), k.Riscontri);
        if (k.Incompleto != null) throw new ErroreScrittura(k.Incompleto);
        if (k.NonGuardati.Count > 0) throw new ErroreScrittura(Lingua.T("scrittura.mi_fermo_non_sono_riuscito", "parte", (k.NonGuardati.Any(x => x.Contains(NELLA_STORIA)) ? FERMO_STORIA : "")), k.NonGuardati.Select(x => new Riscontro { File = x, Cosa = Lingua.T("scrittura.non_sono_riuscito_leggerlo") }).ToList());
    }

    // quanto si aspetta, a scatti, perché GitHub sposti la repo prima di dire che sta aspettando: le prove lo accorciano
    public static int AttesaTrasferimentoMs = 500;

    // il nome nuovo, se GitHub l'ha già spostata; nullo se aspetta che il nuovo proprietario accetti
    public static string Trasferisci(Cliente c, FattiRepo visti, string scritto, string nuovoProprietario) {
        if (!LoginValido(nuovoProprietario)) throw new ErroreScrittura(Lingua.T("scrittura.questo_nome_account_non_valido"));
        var f = Riconferma(c, visti, scritto, Azione.Trasferimento);
        string n = S(c.Chiama("POST", "/repos/" + f.Nome + "/transfer", D("new_owner", nuovoProprietario)).D, "full_name");
        if (RepoValida(n) && n != f.Nome) return n;
        // la risposta del POST riporta ancora il nome vecchio anche quando la repo è già passata: si rilegge sotto il nuovo proprietario
        return SpostataA(c, f, nuovoProprietario + "/" + f.Nome.Substring(f.Nome.IndexOf('/') + 1));
    }

    // Il nome nuovo se la repo (la stessa, riconosciuta dal numero) è già lì; nullo se non c'è ancora (404) o se non si riesce a
    // saperlo. La rilettura non fa mai fallire il trasferimento, che a questo punto è già partito.
    static string SpostataA(Cliente c, FattiRepo f, string nuovo) {
        if (!RepoValida(nuovo)) return null;
        for (int i = 0; i < 4; i++) {
            if (i > 0) System.Threading.Thread.Sleep(AttesaTrasferimentoMs * i);
            try {
                var d = c.Chiama("GET", "/repos/" + nuovo, null).D;
                string n = S(d, "full_name");
                return RepoValida(n) && N(d, "id") == f.Id ? n : null;
            } catch (ErroreScrittura e) { if (e.Codice != 404) return null; }
            catch (Exception) { return null; }
        }
        return null;
    }

    // ---------- il controllo prima di passare a pubblica ----------

    // l'ultimo commit del ramo principale; nullo se la repo è vuota
    static string Testa(Cliente c, FattiRepo f) {
        try { return S(O(c.Chiama("GET", "/repos/" + f.Nome + "/git/ref/heads/" + Segmenti(f.Ramo), null).D, "object"), "sha"); }
        catch (ErroreScrittura e) { if (e.Codice == 404 || e.Codice == 409) return null; throw; }
    }

    static List<FileRepo> Albero(Cliente c, FattiRepo f, string sha, out bool tagliato) {
        tagliato = false;
        var l = new List<FileRepo>();
        if (sha == null) return l;
        var d = c.Chiama("GET", "/repos/" + f.Nome + "/git/trees/" + sha + "?recursive=1", null).D;
        tagliato = B(d, "truncated") || !d.ContainsKey("tree");
        foreach (var o in ((IEnumerable)(d.ContainsKey("tree") ? d["tree"] : new object[0])).Cast<object>().OfType<Dictionary<string, object>>())
            if (S(o, "type") == "blob") l.Add(new FileRepo { Percorso = S(o, "path"), Sha = S(o, "sha"), Peso = N(o, "size") });
        return l;
    }

    public static List<FileRepo> ElencoFile(Cliente c, FattiRepo f) { bool t; return Albero(c, f, Testa(c, f), out t); }

    static byte[] Base64(string s) { return Convert.FromBase64String(Regex.Replace(s ?? "", @"\s+", "")); }

    // i rami, ognuno col suo ultimo commit. troppi: più di quanti se ne guardano da qui
    static List<KeyValuePair<string, string>> Punte(Cliente c, FattiRepo f, out bool troppi) {
        troppi = false;
        var l = new List<KeyValuePair<string, string>>();
        for (int p = 1; ; p++) {
            List<Dictionary<string, object>> pagina;
            try { pagina = c.Chiama("GET", "/repos/" + f.Nome + "/branches?per_page=100&page=" + p, null).L.ToList(); }
            catch (ErroreScrittura e) { if (e.Codice == 404 || e.Codice == 409) return l; throw; }
            foreach (var b in pagina) l.Add(new KeyValuePair<string, string>(S(b, "name"), S(O(b, "commit"), "sha")));
            if (l.Count > MAX_RAMI) { troppi = true; return l; }
            if (pagina.Count < 100) return l;
        }
    }

    static string ImprontaRami(List<KeyValuePair<string, string>> punte) { return string.Join("\n", punte.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value)); }

    // nullo: non sono riuscito a leggerlo, e il controllo lo dirà per nome
    static byte[] LeggiBlob(Cliente c, FattiRepo f, FileRepo x, ref long letti) {
        if (x.Peso > MAX_DA_GUARDARE || letti + x.Peso > MAX_LETTI) return null;
        letti += x.Peso;
        try {
            var d = c.Chiama("GET", "/repos/" + f.Nome + "/git/blobs/" + x.Sha, null).D;
            if (S(d, "encoding") != "base64") return null;
            var b = Base64(S(d, "content"));
            return x.Peso > 0 && b.Length != x.Peso ? null : b;
        }
        catch (ErroreScrittura e) { if (e.Codice == 403 || e.Codice == 404 || e.Codice == 413 || e.Codice == 422 || (e.Codice == 0 && x.Peso > 5L * 1024 * 1024)) return null; throw; }
        catch (FormatException) { return null; }
        catch (OutOfMemoryException) { return null; }
    }

    static string TROPPI { get { return Lingua.T("scrittura.questa_repo_ha_troppi_file"); } }
    static string TROPPI_TESTI { get { return Lingua.T("scrittura.questa_repo_ha_troppe_segnalazioni"); } }
    static string STORIA_NON_LETTA { get { return Lingua.T("scrittura.github_non_mi_ha_fatto"); } }
    static string TroppaStoria() { return Lingua.T("comune.storia_questa_repo_troppo_lunga", "maxcommitstoria", MaxCommitStoria, "maxfilestoria", MaxFileStoria); }

    static string Corto(string sha) { return sha.Length > 7 ? sha.Substring(0, 7) : sha; }

    // tutte le pagine di un elenco; falso se sono troppe. Un elenco spento sulla repo è un elenco vuoto
    static bool OgniPagina(Cliente c, string percorso, Action<Dictionary<string, object>> ogni) {
        for (int p = 1; p <= MAX_PAGINE; p++) {
            List<Dictionary<string, object>> l;
            try { l = c.Chiama("GET", percorso + "per_page=100&page=" + p, null).L.ToList(); }
            catch (ErroreScrittura e) { if (e.Codice == 404 || e.Codice == 410) return true; throw; }
            foreach (var x in l) ogni(x);
            if (l.Count < 100) return true;
        }
        return false;
    }

    // tag e pull request: i loro commit restano leggibili anche quando il ramo non c'è più
    static List<KeyValuePair<string, string>> AltrePunte(Cliente c, FattiRepo f, out bool troppi) {
        var l = new List<KeyValuePair<string, string>>();
        bool tag = OgniPagina(c, "/repos/" + f.Nome + "/tags?", x => l.Add(new KeyValuePair<string, string>(Lingua.T("scrittura.tag", "testo", S(x, "name")), S(O(x, "commit"), "sha"))));
        bool pull = OgniPagina(c, "/repos/" + f.Nome + "/pulls?state=all&", x => l.Add(new KeyValuePair<string, string>(Lingua.T("scrittura.pull", "testo", N(x, "number")), S(O(x, "head"), "sha"))));
        troppi = !tag || !pull;
        return l;
    }

    // guarda i file all'ultimo commit di tutti i rami, poi la storia, poi i testi. Quello che resta fuori lo dice la conferma
    public static ControlloPubblica ControllaPerPubblica(Cliente c, FattiRepo visti, Action<string> passo) {
        var f = Rilettura(c, visti, Azione.Visibilita);
        var k = new ControlloPubblica { RepoId = f.Id, Wiki = f.Wiki };
        k.Riscontri.AddRange(Dogana.ControllaTesto(Lingua.T("scrittura.descrizione"), f.Descrizione));
        k.Riscontri.AddRange(Dogana.ControllaTesto(Lingua.T("scrittura.sito"), f.Sito));
        bool troppi;
        var punte = Punte(c, f, out troppi);
        k.Sha = ImprontaRami(punte);
        k.Rami = punte.Count;
        if (troppi) { k.Incompleto = TROPPI; return k; }
        bool troppiAltri;
        var altre = AltrePunte(c, f, out troppiAltri);
        k.Sha = ImprontaRami(punte.Concat(altre).ToList());
        if (troppiAltri) { k.Incompleto = TROPPI_TESTI; return k; }
        if (altre.Any(x => x.Value.Length == 0)) { k.Incompleto = STORIA_NON_LETTA; return k; }
        if ((Testa(c, f) != null && punte.Count == 0) || punte.Any(x => x.Value.Length == 0)) { k.Incompleto = Lingua.T("scrittura.github_non_mi_ha_detto_2"); return k; }
        var contenuti = new Dictionary<string, List<Riscontro>>();     // per ogni blob quello che c'è dentro: nullo = non letto
        var nomi = new HashSet<string>();
        var coppie = new HashSet<string>();
        long letti = 0;
        // più rami sullo stesso commit si guardano una volta, e il principale per primo: i suoi file si chiamano col nome semplice
        foreach (var g in punte.GroupBy(x => x.Value).OrderByDescending(x => x.Any(p => p.Key == f.Ramo))) {
            string ramo = g.Any(p => p.Key == f.Ramo) ? null : g.First().Key;
            bool tagliato;
            var file = Albero(c, f, g.Key, out tagliato);
            if (tagliato) { k.Incompleto = TROPPI; return k; }
            foreach (var x in file) {
                string et = ramo == null ? x.Percorso : Lingua.T("scrittura.ramo", "percorso", x.Percorso, "ramo", ramo);
                if (nomi.Add(x.Percorso)) foreach (var r in Dogana.ControllaNome(x.Percorso)) { r.File = et; k.Riscontri.Add(r); }
                if (!coppie.Add(x.Percorso + "\n" + x.Sha)) continue;
                if (passo != null) passo(Lingua.T("scrittura.guardo_file", "et", et));
                List<Riscontro> dentro;
                if (!contenuti.TryGetValue(x.Sha, out dentro)) {
                    if (contenuti.Count >= MAX_GUARDATI) { k.Incompleto = TROPPI; return k; }
                    var b = LeggiBlob(c, f, x, ref letti);
                    dentro = b == null ? null : Dogana.ControllaContenuto(et, b);
                    contenuti[x.Sha] = dentro;
                    if (b != null) foreach (var a in Dogana.Avvisi(et, b)) { a.File = et; k.Avvisi.Add(a); }
                }
                if (dentro == null) { k.NonGuardati.Add(et); continue; }
                k.Guardati++;
                foreach (var r in dentro) k.Riscontri.Add(new Riscontro { File = et, Riga = r.Riga, Cosa = r.Cosa });
            }
        }
        GuardaStoria(c, f, k, punte.Concat(altre).Select(x => x.Value).Distinct().ToList(), new HashSet<string>(punte.Select(x => x.Value)), contenuti, nomi, passo);
        if (k.Incompleto == null) Conta(c, f, k, passo);
        return k;
    }

    // la storia: un segreto cancellato in un commit vecchio resta leggibile. Prima si elencano i commit, che costa poco, e il limite scatta subito;
    // poi si apre ogni albero mai visto e si leggono solo i file mai visti. Quelli dei rami sono già guardati
    static void GuardaStoria(Cliente c, FattiRepo f, ControlloPubblica k, List<string> punte, HashSet<string> rami, Dictionary<string, List<Riscontro>> contenuti, HashSet<string> nomi, Action<string> passo) {
        var noti = new HashSet<string>();
        var elenco = new List<KeyValuePair<string, string>>();  // commit e suo albero
        int richieste = contenuti.Count;
        foreach (var punta in punte) {
            if (noti.Contains(punta)) continue;
            // finché resta un genitore mai visto si continua; quando non ne resta, il resto è già stato elencato partendo da un'altra punta
            var mancanti = new HashSet<string> { punta };
            for (int p = 1; mancanti.Count > 0; p++) {
                if (passo != null) passo(Lingua.T("scrittura.guardo_storia_finora_commit", "noti", noti.Count));
                if (++richieste > MaxRichieste) { k.Incompleto = TroppaStoria(); return; }
                List<Dictionary<string, object>> pagina;
                try { pagina = c.Chiama("GET", "/repos/" + f.Nome + "/commits?sha=" + punta + "&per_page=100&page=" + p, null).L.ToList(); }
                catch (ErroreScrittura e) { if (e.Codice == 404 || e.Codice == 409 || e.Codice == 422) { k.Incompleto = STORIA_NON_LETTA; return; } throw; }
                foreach (var x in pagina) {
                    string sha = S(x, "sha");
                    mancanti.Remove(sha);
                    if (!noti.Add(sha)) continue;
                    if (noti.Count > MaxCommitStoria) { k.Incompleto = TroppaStoria(); return; }
                    var cm = O(x, "commit");
                    string albero = S(O(cm, "tree"), "sha");
                    var genitori = x.ContainsKey("parents") ? x["parents"] as IEnumerable : null;
                    if (sha.Length == 0 || albero.Length == 0 || genitori == null) { k.Incompleto = STORIA_NON_LETTA; return; }
                    foreach (var g in genitori.Cast<object>().OfType<Dictionary<string, object>>()) if (!noti.Contains(S(g, "sha"))) mancanti.Add(S(g, "sha"));
                    elenco.Add(new KeyValuePair<string, string>(sha, albero));
                    k.Riscontri.AddRange(Dogana.ControllaTesto(NEL_MESSAGGIO + Corto(sha), S(cm, "message")));
                    if (EmailVera(S(O(cm, "author"), "email")) || EmailVera(S(O(cm, "committer"), "email"))) k.EmailInChiaro++;
                }
                if (mancanti.Count > 0 && pagina.Count < 100) { k.Incompleto = STORIA_NON_LETTA; return; }
            }
        }
        k.Commit = elenco.Count;
        var alberi = new HashSet<string>(elenco.Where(x => rami.Contains(x.Key)).Select(x => x.Value));
        var trovati = new HashSet<string>();    // lo stesso segreto in tante versioni dello stesso file si dice una volta
        long letti = 0;
        int nuovi = 0, fatti = 0;
        foreach (var e in elenco) {
            fatti++;
            if (rami.Contains(e.Key) || !alberi.Add(e.Value)) continue;
            if (passo != null) passo(Lingua.T("scrittura.guardo_storia_commit", "fatti", fatti, "elenco", elenco.Count));
            if (++richieste > MaxRichieste) { k.Incompleto = TroppaStoria(); return; }
            bool tagliato;
            var file = Albero(c, f, e.Value, out tagliato);
            if (tagliato) { k.Incompleto = TroppaStoria(); return; }
            string dove = NELLA_STORIA + Corto(e.Key) + ")";
            foreach (var x in file) if (nomi.Add(x.Percorso)) foreach (var r in Dogana.ControllaNome(x.Percorso)) { r.File = x.Percorso + dove; k.Riscontri.Add(r); }
            var daLeggere = file.Where(x => !contenuti.ContainsKey(x.Sha)).GroupBy(x => x.Sha).Select(g => g.First()).ToList();
            nuovi += daLeggere.Count; richieste += daLeggere.Count;
            if (nuovi > MaxFileStoria || richieste > MaxRichieste) { k.Incompleto = TroppaStoria(); return; }
            foreach (var x in daLeggere) {
                if (letti + x.Peso > MAX_LETTI) { k.Incompleto = TroppaStoria(); return; }
                string et = x.Percorso + dove;
                var b = LeggiBlob(c, f, x, ref letti);
                var dentro = b == null ? null : Dogana.ControllaContenuto(et, b);
                contenuti[x.Sha] = dentro;
                if (dentro == null) { k.NonGuardati.Add(et); continue; }
                k.GuardatiStoria++;
                foreach (var a in Dogana.Avvisi(et, b)) if (trovati.Add(x.Percorso + "\n" + a.Cosa)) { a.File = et; k.Avvisi.Add(a); }
                foreach (var r in dentro) if (trovati.Add(x.Percorso + "\n" + r.Cosa)) k.Riscontri.Add(new Riscontro { File = et, Riga = r.Riga, Cosa = r.Cosa });
            }
        }
    }

    // quello che diventa pubblico insieme ai file: si conta, e i testi passano dalla stessa ricerca dei file
    static void Conta(Cliente c, FattiRepo f, ControlloPubblica k, Action<string> passo) {
        if (passo != null) passo(Lingua.T("scrittura.guardo_segnalazioni_pull_request_relea"));
        string base_ = "/repos/" + f.Nome;
        Action<string, string> cerca = (dove, testo) => k.Riscontri.AddRange(Dogana.ControllaTesto(dove, testo));
        Func<string, string> ultimo = u => u.Substring(u.LastIndexOf('/') + 1);
        var numeri = new List<string>();
        bool tutti = OgniPagina(c, base_ + "/issues?state=all&", x => {
            bool pull = x.ContainsKey("pull_request");
            if (pull) { k.Pull++; numeri.Add(N(x, "number").ToString()); } else k.Issue++;
            string chi = (pull ? Lingua.T("scrittura.pull_request") : Lingua.T("scrittura.segnalazione")) + N(x, "number");
            cerca(Lingua.T("scrittura.titolo", "chi", chi), S(x, "title"));
            cerca(Lingua.T("scrittura.testo", "chi", chi), S(x, "body"));
        });
        // i commenti stanno in tre elenchi unici per tutta la repo: poche richieste
        tutti = OgniPagina(c, base_ + "/issues/comments?", x => { k.Commenti++; cerca(Lingua.T("scrittura.commento", "testo", ultimo(S(x, "issue_url"))), S(x, "body")); }) && tutti;
        tutti = OgniPagina(c, base_ + "/pulls/comments?", x => { k.Commenti++; cerca(Lingua.T("scrittura.commento_revisione", "testo", ultimo(S(x, "pull_request_url"))), S(x, "body")); }) && tutti;
        tutti = OgniPagina(c, base_ + "/comments?", x => { k.Commenti++; cerca(Lingua.T("scrittura.commento_al_commit", "testo", Corto(S(x, "commit_id"))), S(x, "body")); }) && tutti;
        // i giudizi delle revisioni costano una richiesta per ogni pull request
        if (numeri.Count > MaxRichieste / 4) tutti = false;
        else foreach (var n in numeri) {
            if (passo != null) passo(Lingua.T("scrittura.guardo_revisioni_della_pull_request", "n", n));
            tutti = OgniPagina(c, base_ + "/pulls/" + n + "/reviews?", x => { if (S(x, "body").Length == 0) return; k.Commenti++; cerca(Lingua.T("scrittura.revisione", "n", n), S(x, "body")); }) && tutti;
        }
        // le bozze restano nascoste anche su una repo pubblica
        tutti = OgniPagina(c, base_ + "/releases?", x => {
            if (B(x, "draft")) return;
            k.Release++;
            string chi = Lingua.T("scrittura.release", "testo", S(x, "tag_name"));
            cerca(chi, S(x, "tag_name"));
            cerca(Lingua.T("scrittura.titolo", "chi", chi), S(x, "name"));
            cerca(Lingua.T("scrittura.note", "chi", chi), S(x, "body"));
            object allegati;
            if (!x.TryGetValue("assets", out allegati) || !(allegati is IEnumerable)) return;
            foreach (var a in ((IEnumerable)allegati).Cast<object>().OfType<Dictionary<string, object>>()) {
                k.Allegati++;
                foreach (var r in Dogana.ControllaNome(S(a, "name"))) { r.File = Lingua.T("scrittura.allegato", "chi", chi, "a", S(a, "name")); k.Riscontri.Add(r); }
            }
        }) && tutti;
        if (!tutti) k.Incompleto = TROPPI_TESTI;
    }

    static bool EmailVera(string e) { return e.Length > 0 && !e.EndsWith("@users.noreply.github.com", StringComparison.OrdinalIgnoreCase) && !e.Equals("noreply@github.com", StringComparison.OrdinalIgnoreCase); }

    // ---------- un file alla volta ----------

    public static FileRepo LeggiFile(Cliente c, FattiRepo f, string percorso) {
        string p = PercorsoSicuro(percorso);
        var d = c.Chiama("GET", "/repos/" + f.Nome + "/contents/" + Segmenti(p) + "?ref=" + Uri.EscapeDataString(f.Ramo), null).D;
        if (S(d, "type") != "file" || S(d, "encoding") != "base64") throw new ErroreScrittura(Lingua.T("scrittura.questo_file_troppo_grande_modificarlo"));
        var b = Base64(S(d, "content"));
        if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) throw new ErroreScrittura(Lingua.T("scrittura.questo_file_scritto_utf_16"));
        if (Dogana.Binario(b)) throw new ErroreScrittura(Lingua.T("scrittura.questo_non_file_testo_qui"));
        bool bom = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
        string testo;
        try { testo = new UTF8Encoding(false, true).GetString(b, bom ? 3 : 0, b.Length - (bom ? 3 : 0)); }
        catch (ArgumentException) { throw new ErroreScrittura(Lingua.T("scrittura.questo_file_non_utf_8")); }
        // gli a capo si rimettono come erano: uno script con quelli di Windows non parte più
        int doppi = Regex.Matches(testo, "\r\n").Count, tutti = testo.Count(x => x == '\n');
        return new FileRepo { Percorso = p, Sha = S(d, "sha"), Peso = b.Length, Testo = testo, Bom = bom, FineRiga = doppi > tutti - doppi ? "\r\n" : "\n" };
    }

    // sha vuoto: un file nuovo
    public static void ScriviFile(Cliente c, FattiRepo visti, string percorso, string testo, string sha, string messaggio, bool bom = false) {
        string p = PercorsoSicuro(percorso);
        Permesso(p);
        var f = Rilettura(c, visti, Azione.Contenuto);
        var b = new UTF8Encoding(false).GetBytes(testo ?? "");
        if (bom) b = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(b).ToArray();
        FermaSeTrova(Dogana.ControllaFile(p, b), FERMO);
        string m = string.IsNullOrWhiteSpace(messaggio) ? Lingua.T("scrittura.modifica", "p", p) : messaggio;
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.messaggio"), m), FERMO);
        var chi = Firma(c);
        var corpo = D("message", m, "content", Convert.ToBase64String(b), "author", chi, "committer", chi);
        // in una repo ancora vuota il ramo non esiste: senza dirlo, GitHub usa quello principale
        if (!string.IsNullOrEmpty(sha) || Testa(c, f) != null) corpo["branch"] = f.Ramo;
        if (!string.IsNullOrEmpty(sha)) corpo["sha"] = sha;
        c.Chiama("PUT", "/repos/" + f.Nome + "/contents/" + Segmenti(p), corpo);
    }

    public static void CancellaFile(Cliente c, FattiRepo visti, string percorso, string sha, string messaggio) {
        string p = PercorsoSicuro(percorso);
        Permesso(p);
        var f = Rilettura(c, visti, Azione.Contenuto);
        if (string.IsNullOrEmpty(sha)) throw new ErroreScrittura(Lingua.T("scrittura.prima_devo_rileggere_file_github"));
        string m = string.IsNullOrWhiteSpace(messaggio) ? Lingua.T("scrittura.cancella", "p", p) : messaggio;
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.messaggio"), m), FERMO);
        var chi = Firma(c);
        c.Chiama("DELETE", "/repos/" + f.Nome + "/contents/" + Segmenti(p), D("message", m, "sha", sha, "branch", f.Ramo, "author", chi, "committer", chi));
    }

    // ---------- caricare una cartella: Git Data API, senza dipendere da git installato ----------

    // le regole di un file .gitignore, che valgono dalla sua cartella in giù. Si guarda solo cosa saltare: quello che sfugge a queste regole viene controllato, non perso di vista
    class Ignora {
        readonly string baseRel;
        readonly List<KeyValuePair<Regex, bool[]>> regole = new List<KeyValuePair<Regex, bool[]>>();   // bool[]: negata, solo cartelle
        public Ignora(string testo, string cartella) {
            baseRel = cartella;
            foreach (var grezza in (testo ?? "").Replace("\r", "").Split('\n')) {
                string r = grezza.Trim();
                if (r.Length == 0 || r[0] == '#') continue;
                bool neg = r[0] == '!'; if (neg) r = r.Substring(1);
                bool dir = r.EndsWith("/"); r = r.TrimEnd('/');
                bool fissa = r.Contains("/"); r = r.TrimStart('/');
                if (r.Length == 0) continue;
                var sb = new StringBuilder(fissa ? "^" : "(^|/)");
                for (int i = 0; i < r.Length; i++) {
                    if (r[i] == '*' && i + 1 < r.Length && r[i + 1] == '*') { i++; if (i + 1 < r.Length && r[i + 1] == '/') { i++; sb.Append("(.*/)?"); } else sb.Append(".*"); }
                    else if (r[i] == '*') sb.Append("[^/]*");
                    else if (r[i] == '?') sb.Append("[^/]");
                    else if (r[i] == '\\' && i + 1 < r.Length) sb.Append(Regex.Escape(r[++i].ToString()));
                    else if (r[i] == '[') i = Classe(r, i, sb);
                    else sb.Append(Regex.Escape(r[i].ToString()));
                }
                sb.Append("$");
                try { regole.Add(new KeyValuePair<Regex, bool[]>(new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), new[] { neg, dir })); } catch (ArgumentException) { }
            }
        }
        // [Bb], [!a], [^a]. Una parentesi che non si chiude vale per se stessa. Dice dove è arrivata
        static int Classe(string r, int i, StringBuilder sb) {
            int j = i + 1;
            if (j < r.Length && (r[j] == '!' || r[j] == '^')) j++;
            if (j < r.Length && r[j] == ']') j++;
            while (j < r.Length && r[j] != ']') j++;
            if (j >= r.Length) { sb.Append("\\["); return i; }
            string dentro = r.Substring(i + 1, j - i - 1);
            bool no = dentro[0] == '!' || dentro[0] == '^';
            if (no) dentro = dentro.Substring(1);
            sb.Append("(?!/)[").Append(no ? "^" : "").Append(dentro.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]")).Append("]");
            return j;
        }
        // nullo: su questo percorso non dice niente
        public bool? Dice(string percorso, bool cartella) {
            if (!percorso.StartsWith(baseRel, StringComparison.OrdinalIgnoreCase)) return null;
            string p = percorso.Substring(baseRel.Length);
            bool? s = null;
            foreach (var r in regole) if ((!r.Value[1] || cartella) && r.Key.IsMatch(p)) s = !r.Value[0];
            return s;
        }
    }

    // la regola della cartella più vicina vince, come in Git
    static bool Salta(List<Ignora> pila, string percorso, bool cartella) {
        for (int i = pila.Count - 1; i >= 0; i--) { var d = pila[i].Dice(percorso, cartella); if (d.HasValue) return d.Value; }
        return false;
    }

    static readonly HashSet<string> DA_CONFERMARE = new HashSet<string>(new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb", ".bak", ".dump" }, StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> NON_LEGGIBILI = new HashSet<string>(new[] { ".pdf", ".doc", ".docx", ".docm", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx", ".pptm", ".odt", ".ods", ".odp", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" }, StringComparer.OrdinalIgnoreCase);

    public static Cartella Prepara(string radice, Action<string> passo) {
        var k = new Cartella { Radice = radice };
        var pila = new List<Ignora>();
        string ex = Path.Combine(radice, ".git", "info", "exclude");
        if (File.Exists(ex)) pila.Add(new Ignora(File.ReadAllText(ex), ""));
        Scendi(k, radice, "", pila, passo);
        if (k.File.Count > MAX_FILE) k.Rifiutati.Add(Lingua.T("scrittura.sono_file_github_limita_scritture", "file", k.File.Count, "max_file", MAX_FILE));
        if (k.Peso > MAX_PESO) k.Rifiutati.Add(Lingua.T("scrittura.cartella_pesa_troppo_qui_si", "max_peso", MAX_PESO / 1024 / 1024));
        return k;
    }

    static void Scendi(Cartella k, string dir, string rel, List<Ignora> pila, Action<string> passo) {
        string gi = Path.Combine(dir, ".gitignore");
        bool regole = File.Exists(gi) && (File.GetAttributes(gi) & FileAttributes.ReparsePoint) == 0;
        if (regole) pila.Add(new Ignora(File.ReadAllText(gi), rel));
        foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
            string nome = Path.GetFileName(d), r = rel + nome;
            if (nome.Equals(".git", StringComparison.OrdinalIgnoreCase)) { k.Saltati.Add(r + "/"); continue; }
            if (Salta(pila, r, true)) { k.Saltati.Add(r + "/"); continue; }
            // un collegamento può portare fuori dalla cartella: non si segue
            if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) { k.Rifiutati.Add(Lingua.T("scrittura.collegamento_altra_cartella", "r", r)); continue; }
            Scendi(k, d, r + "/", pila, passo);
        }
        foreach (var f in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
            string nome = Path.GetFileName(f), r = rel + nome;
            if (nome.Equals(".git", StringComparison.OrdinalIgnoreCase)) { k.Saltati.Add(r); continue; }   // lo lascia Git nelle copie collegate
            if (Salta(pila, r, false)) { k.Saltati.Add(r); continue; }
            if (passo != null) passo(r);
            if (Workflow(r)) { k.Rifiutati.Add(r + ": " + Frasi.WORKFLOW); continue; }
            var i = new FileInfo(f);
            if ((i.Attributes & FileAttributes.ReparsePoint) != 0) { k.Rifiutati.Add(Lingua.T("scrittura.collegamento_altro_file", "r", r)); continue; }
            if (i.Length > MAX_PESO_FILE) { k.Rifiutati.Add(Lingua.T("scrittura.piu_grande_100_mb_github", "r", r)); continue; }
            var b = File.ReadAllBytes(f);
            k.Riscontri.AddRange(Dogana.ControllaFile(r, b));
            k.Avvisi.AddRange(Dogana.Avvisi(r, b));
            string est = Path.GetExtension(nome);
            if (DA_CONFERMARE.Contains(est)) k.DaConfermare.Add(r);
            else if (NON_LEGGIBILI.Contains(est)) k.NonLeggibili.Add(r);
            k.File.Add(new FileDaCaricare { Percorso = r, Pieno = f, Peso = i.Length });
            k.Peso += i.Length;
        }
        if (regole) pila.RemoveAt(pila.Count - 1);
    }

    // toglie dal caricamento gli archivi e le basi di dati che non hanno avuto il sì
    public static void LasciaFuori(Cartella k) {
        var via = new HashSet<string>(k.DaConfermare.Where(x => !k.Confermati.Contains(x)));
        foreach (var x in k.File.Where(z => via.Contains(z.Percorso))) { k.Peso -= x.Peso; k.Saltati.Add(x.Percorso); }
        k.File.RemoveAll(z => via.Contains(z.Percorso));
        k.Avvisi.RemoveAll(z => via.Contains(z.File));
        k.DaConfermare.RemoveAll(via.Contains);
    }

    // quanti dei file preparati esistono già nella repo: vengono sostituiti, gli altri della repo restano
    public static int GiaPresenti(Cliente c, FattiRepo f, Cartella k) { return Sostituiti(c, f, k).Count; }

    public static List<string> Sostituiti(Cliente c, FattiRepo f, Cartella k) {
        var su = new HashSet<string>(ElencoFile(c, f).Select(x => x.Percorso));
        return k.File.Where(x => su.Contains(x.Percorso)).Select(x => x.Percorso).ToList();
    }

    // tutti i file diventano un commit solo: finché il ramo non si sposta, alla fine, nella repo non si vede niente.
    // Il ramo si sposta sempre senza forzare. I file si rileggono e si ricontrollano prima di partire: conta quello che va su davvero.
    public static EsitoCarica CaricaCartella(Cliente c, FattiRepo visti, Cartella k, string messaggio, Action<int, int> avanza, Action<int> dorme, string scritto = null) {
        if (k == null || k.File.Count == 0) throw new ErroreScrittura(Lingua.T("scrittura.non_c_niente_caricare"));
        if (k.Riscontri.Count > 0) throw new ErroreScrittura(FERMO, k.Riscontri);
        if (k.Rifiutati.Count > 0) throw new ErroreScrittura(k.Rifiutati[0]);
        var senzaSi = k.DaConfermare.Where(x => !k.Confermati.Contains(x)).ToList();
        if (senzaSi.Count > 0) throw new ErroreScrittura(Lingua.T("scrittura.dentro_archivi_basi_dati_non", "elenco", string.Join(", ", senzaSi.Take(10)), "parte", (senzaSi.Count > 10 ? Lingua.T("comune.altri", "parte", (senzaSi.Count - 10)) : "")));
        var f = Rilettura(c, visti, Azione.Contenuto);
        if (!f.Privata && scritto != f.Nome) throw new ErroreScrittura(Lingua.T("scrittura.questa_repo_pubblica_caricare_scrivi"));
        string msg = string.IsNullOrWhiteSpace(messaggio) ? Lingua.N("scrittura.carica", k.File.Count) : messaggio;
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.messaggio"), msg), FERMO);
        var chi = Firma(c);
        var file = k.File.ToList();
        var esito = new EsitoCarica { File = file.Count, Passi = 1 };
        string padre = Testa(c, f);

        // una repo vuota non accetta la Git Data API: GitHub vuole prima un file, e quello è un commit a parte
        string giaSu = null;
        if (padre == null) {
            esito.EraVuota = true;
            foreach (var x in file) Rileggi(x);     // prima del primo file: quello resta su anche se il resto si ferma
            var primo = file.FirstOrDefault(x => x.Percorso.Equals("README.md", StringComparison.OrdinalIgnoreCase)) ?? file.OrderBy(x => x.Peso).First();
            var dati = Rileggi(primo);
            padre = S(O(c.Chiama("PUT", "/repos/" + f.Nome + "/contents/" + Segmenti(PercorsoSicuro(primo.Percorso)), D("message", msg, "content", Convert.ToBase64String(dati), "author", chi, "committer", chi)).D, "commit"), "sha");
            file.Remove(primo);
            giaSu = primo.Percorso;
            if (file.Count == 0) { esito.Commit = padre; return esito; }
            esito.Passi = 2;
        }

        try {
            var voci = new List<object>();
            int fatti = 0;
            foreach (var x in file) {
                var dati = Rileggi(x);
                string sha = S(c.Chiama("POST", "/repos/" + f.Nome + "/git/blobs", D("content", Convert.ToBase64String(dati), "encoding", "base64")).D, "sha");
                voci.Add(D("path", PercorsoSicuro(x.Percorso), "mode", "100644", "type", "blob", "sha", sha));
                if (avanza != null) avanza(++fatti, file.Count);
                if (dorme != null && file.Count > 50) dorme(800);   // GitHub limita le scritture al minuto
            }
            string baseAlbero = S(O(c.Chiama("GET", "/repos/" + f.Nome + "/git/commits/" + padre, null).D, "tree"), "sha");
            string albero = S(c.Chiama("POST", "/repos/" + f.Nome + "/git/trees", D("base_tree", baseAlbero, "tree", voci)).D, "sha");
            string commit = S(c.Chiama("POST", "/repos/" + f.Nome + "/git/commits", D("message", msg, "tree", albero, "parents", new[] { padre }, "author", chi, "committer", chi)).D, "sha");
            c.Chiama("PATCH", "/repos/" + f.Nome + "/git/refs/heads/" + Segmenti(f.Ramo), D("sha", commit, "force", false));
            esito.Commit = commit;
            return esito;
        }
        catch (ErroreScrittura e) {
            if (giaSu == null) throw;
            throw new ErroreScrittura(e.Codice, Lingua.T("scrittura.file_pero_gia_nella_repo", "errore", e.Message, "giasu", giaSu)) { Riscontri = e.Riscontri };
        }
    }

    static byte[] Rileggi(FileDaCaricare x) {
        byte[] b;
        try { b = File.ReadAllBytes(x.Pieno); } catch (IOException) { throw new ErroreScrittura(Lingua.T("scrittura.non_riesco_piu_leggere_non", "percorso", x.Percorso)); }
        FermaSeTrova(Dogana.ControllaFile(x.Percorso, b), FERMO);
        return b;
    }

    // ---------- release ----------

    static ReleaseInfo Rel(Dictionary<string, object> d) {
        var r = new ReleaseInfo { Id = N(d, "id"), Tag = S(d, "tag_name"), Titolo = S(d, "name"), Note = S(d, "body"), Ramo = S(d, "target_commitish"), Data = S(d, "published_at"), Bozza = B(d, "draft"), Anteprima = B(d, "prerelease") };
        object a;
        if (d.TryGetValue("assets", out a) && a is IEnumerable)
            foreach (var x in ((IEnumerable)a).Cast<object>().OfType<Dictionary<string, object>>()) r.Allegati.Add(new AllegatoInfo { Nome = S(x, "name"), Peso = N(x, "size") });
        return r;
    }

    public static List<ReleaseInfo> Release(Cliente c, FattiRepo f) { return c.Chiama("GET", "/repos/" + f.Nome + "/releases?per_page=100", null).L.Select(Rel).ToList(); }
    public static ReleaseInfo UnaRelease(Cliente c, FattiRepo f, long id) { return Rel(c.Chiama("GET", "/repos/" + f.Nome + "/releases/" + id, null).D); }

    // il tag dell'ultima versione pubblicata, vuoto se non ce n'è
    public static string Ultima(Cliente c, FattiRepo f) {
        try { return S(c.Chiama("GET", "/repos/" + f.Nome + "/releases/latest", null).D, "tag_name"); }
        catch (ErroreScrittura e) { if (e.Codice == 404) return ""; throw; }
    }

    static void TagValido(string tag) {
        if (string.IsNullOrWhiteSpace(tag) || !Regex.IsMatch(tag, @"^[A-Za-z0-9][A-Za-z0-9._\-]*$")) throw new ErroreScrittura(Lingua.T("scrittura.tag_segnalibro_della_versione_esempio"));
    }

    // una release nuova nasce sempre bozza: si vede solo da chi scrive nella repo finché non la pubblichi
    public static ReleaseInfo CreaBozza(Cliente c, FattiRepo visti, string tag, string titolo, string note) {
        TagValido(tag);
        var f = Rilettura(c, visti, Azione.Release);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.titolo_note_della_release"), (titolo ?? "") + "\n" + (note ?? "")), FERMO);
        return Rel(c.Chiama("POST", "/repos/" + f.Nome + "/releases", D("tag_name", tag, "name", titolo ?? "", "body", note ?? "", "target_commitish", f.Ramo, "draft", true)).D);
    }

    public static void Allega(Cliente c, FattiRepo visti, long id, string file) {
        var f = Rilettura(c, visti, Azione.Release);
        var i = new FileInfo(file);
        if (!i.Exists) throw new ErroreScrittura(Lingua.T("scrittura.questo_file_non_c_piu"));
        if (i.Length == 0 || i.Length > MAX_ALLEGATO) throw new ErroreScrittura(Lingua.T("scrittura.qui_si_allegano_file_1"));
        var b = File.ReadAllBytes(file);
        FermaSeTrova(Dogana.ControllaFile(i.Name, b), FERMO);
        var r = c.Carica("/repos/" + f.Nome + "/releases/" + id + "/assets?name=" + Uri.EscapeDataString(i.Name), b).D;
        if (N(r, "size") != b.Length) throw new ErroreScrittura(Lingua.T("scrittura.github_dice_aver_ricevuto_file"));
    }

    // una bozza si modifica con un clic; una release già pubblicata vuole il nome scritto. Non si sposta mai «ultima versione» da qui:
    // se questa non è l'ultima lo si dice a GitHub, perché una release vecchia che torna ultima riporta indietro gli aggiornamenti automatici
    public static void ModificaRelease(Cliente c, FattiRepo visti, long id, string tag, string titolo, string note, string scritto) {
        TagValido(tag);
        var f = Rilettura(c, visti, Azione.Release);
        var ora = UnaRelease(c, f, id);
        if (!ora.Bozza) Riconferma(c, visti, scritto, Azione.Release);
        FermaSeTrova(Dogana.ControllaTesto(Lingua.T("scrittura.titolo_note_della_release"), (titolo ?? "") + "\n" + (note ?? "")), FERMO);
        var corpo = D("tag_name", tag, "name", titolo ?? "", "body", note ?? "");
        if (!ora.Bozza) {
            long ultima = 0;
            try { ultima = N(c.Chiama("GET", "/repos/" + f.Nome + "/releases/latest", null).D, "id"); }
            catch (ErroreScrittura e) { if (e.Codice != 404) throw; }
            if (ultima != id) corpo["make_latest"] = "false";
        }
        c.Chiama("PATCH", "/repos/" + f.Nome + "/releases/" + id, corpo);
    }

    // comeUltima: diventa la «Latest», quella di cui si fidano gli aggiornamenti automatici
    public static void Pubblica(Cliente c, FattiRepo visti, string scritto, long id, bool comeUltima) {
        var f = Riconferma(c, visti, scritto, Azione.Release);
        if (!UnaRelease(c, f, id).Bozza) throw new ErroreScrittura(Lingua.T("scrittura.questa_release_gia_pubblicata"));
        c.Chiama("PATCH", "/repos/" + f.Nome + "/releases/" + id, D("draft", false, "make_latest", comeUltima ? "true" : "false"));
    }

    public static void CancellaRelease(Cliente c, FattiRepo visti, string scritto, long id) {
        var f = Riconferma(c, visti, scritto, Azione.Release);
        c.Chiama("DELETE", "/repos/" + f.Nome + "/releases/" + id, null);
    }

    // ---------- chi agisce ----------

    // l'accesso trovato deve essere dell'account scelto: altrimenti la finestra direbbe un nome e il commit ne porterebbe un altro
    public static void StessoConto(string scelto, string vero, Action esci) {
        if (Accessi.Uguali(scelto, vero)) return;
        esci();
        throw new ErroreScrittura(Lingua.T("scrittura.l_account_scelto_ma_l", "scelto", scelto, "vero", vero));
    }

    // prima di tenere una chiave si guarda che permessi ha. Se non si riesce a guardare, non si tiene
    public static void ControllaChiave(Cliente c) {
        try { c.Chiama("GET", "/user", null); }
        catch (ErroreScrittura e) { throw new ErroreAccesso(c.Buttata ? e.Message : Lingua.T("scrittura.non_ho_potuto_controllare_l", "errore", e.Message)); }
    }

    // ---------- collaboratori ----------

    // le persone aggiunte a mano e gli inviti non ancora accettati. Chi entra perché è nell'organizzazione si gestisce sul sito
    public static List<PersonaRepo> Collaboratori(Cliente c, FattiRepo f) {
        var l = c.Chiama("GET", "/repos/" + f.Nome + "/collaborators?affiliation=direct&per_page=100", null).L.Select(x => new PersonaRepo { Nome = S(x, "login"), Ruolo = S(x, "role_name") }).ToList();
        l.AddRange(c.Chiama("GET", "/repos/" + f.Nome + "/invitations?per_page=100", null).L.Select(x => new PersonaRepo { Nome = S(O(x, "invitee"), "login"), Ruolo = S(x, "permissions"), Invito = N(x, "id") }));
        return l;
    }

    // chi è davvero questo nome su GitHub: si mostra nella conferma
    public static string TipoAccount(Cliente c, string login) {
        if (!LoginValido(login)) throw new ErroreScrittura(Lingua.T("scrittura.questo_nome_account_non_valido"));
        return S(c.Chiama("GET", "/users/" + login, null).D, "type");
    }

    public static string RuoloDi(Cliente c, FattiRepo f, string login) {
        if (!LoginValido(login)) throw new ErroreScrittura(Lingua.T("scrittura.questo_nome_account_non_valido"));
        return S(c.Chiama("GET", "/repos/" + f.Nome + "/collaborators/" + login + "/permission", null).D, "role_name");
    }

    public static readonly string[] RUOLI = { "pull", "triage", "push", "maintain", "admin" };

    // nelle repo personali GitHub ha un ruolo solo: il permesso vale solo per le organizzazioni. Vero se è partito un invito
    public static bool AggiungiCollaboratore(Cliente c, FattiRepo visti, string scritto, string login, string ruolo) {
        if (!LoginValido(login)) throw new ErroreScrittura(Lingua.T("scrittura.questo_nome_account_non_valido"));
        var f = Riconferma(c, visti, scritto, Azione.Collaboratori);
        if (f.Organizzazione && !RUOLI.Contains(ruolo)) throw new ErroreScrittura(Lingua.T("scrittura.scegli_ruolo"));
        return c.Chiama("PUT", "/repos/" + f.Nome + "/collaborators/" + login, f.Organizzazione ? D("permission", ruolo) : D()).Codice == 201;
    }

    public static void CambiaRuolo(Cliente c, FattiRepo visti, string scritto, string login, string ruolo) {
        if (!LoginValido(login)) throw new ErroreScrittura(Lingua.T("scrittura.questo_nome_account_non_valido"));
        var f = Riconferma(c, visti, scritto, Azione.Collaboratori);
        if (!f.Organizzazione) throw new ErroreScrittura(Lingua.T("scrittura.nelle_repo_personali_github_ha"));
        if (!RUOLI.Contains(ruolo)) throw new ErroreScrittura(Lingua.T("scrittura.scegli_ruolo"));
        c.Chiama("PUT", "/repos/" + f.Nome + "/collaborators/" + login, D("permission", ruolo));
    }

    public static void TogliCollaboratore(Cliente c, FattiRepo visti, string scritto, string login) {
        if (!LoginValido(login)) throw new ErroreScrittura(Lingua.T("scrittura.questo_nome_account_non_valido"));
        var f = Riconferma(c, visti, scritto, Azione.Collaboratori);
        c.Chiama("DELETE", "/repos/" + f.Nome + "/collaborators/" + login, null);
    }

    public static void RitiraInvito(Cliente c, FattiRepo visti, string scritto, long invito) {
        var f = Riconferma(c, visti, scritto, Azione.Collaboratori);
        c.Chiama("DELETE", "/repos/" + f.Nome + "/invitations/" + invito, null);
    }

    // ---------- cancellare una repo ----------

    // la chiave di «usaEGetta» viene dal login apposta e non si salva da nessuna parte: si usa per questa cancellazione e si butta, comunque vada
    public static void CancellaRepo(Cliente lettore, Cliente usaEGetta, string login, FattiRepo visti, string scritto) {
        try {
            var f = Riconferma(lettore, visti, scritto, Azione.Cancellazione);
            string chi = S(usaEGetta.Chiama("GET", "/user", null).D, "login");
            if (!Organizzazione.Uguali(chi, login)) throw new ErroreScrittura(Lingua.T("scrittura.sul_browser_hai_autorizzato_l", "chi", Dogana.RendiVisibile(chi), "login", login));
            if (!usaEGetta.HaPermesso("delete_repo")) throw new ErroreScrittura(Lingua.T("scrittura.github_non_stato_dato_permesso"));
            usaEGetta.Chiama("DELETE", "/repos/" + f.Nome, null);
        } finally { usaEGetta.Butta(); }
    }

    // ---------- l'archivio del programma segue il nome nuovo: testi e decisioni non si perdono ----------

    public static void RinominaInArchivio(Archivio a, string vecchio, string nuovo) {
        foreach (var r in a.Repo.Where(x => x.Nome == vecchio)) {
            r.Nome = nuovo;
            r.Proprietario = nuovo.Substring(0, nuovo.IndexOf('/'));
        }
        foreach (var e in a.Elementi.Where(x => x.Repo == vecchio)) {
            e.Repo = nuovo;
            string prima = "https://github.com/" + vecchio + "/";
            if (e.Url != null && e.Url.StartsWith(prima)) e.Url = "https://github.com/" + nuovo + "/" + e.Url.Substring(prima.Length);
        }
    }

    public static void SegnaCancellata(Archivio a, string nome) { foreach (var r in a.Repo.Where(x => x.Nome == nome)) { r.Sparita = true; r.Seguita = false; } }
}
