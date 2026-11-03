using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

public static class Dati {
    public static readonly string Cartella = Environment.GetEnvironmentVariable("GPM_DATI") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectManager");
    static string FILE { get { return Path.Combine(Cartella, "archivio.json"); } }
    public static string Pacchetti { get { return Path.Combine(Cartella, "pacchetti"); } }
    public static string CartellaRepo;   // la scelta dell'utente, dall'archivio; vuota = non le tiene su questo computer
    public static readonly string[] FIDATI = { "github.com", "githubusercontent.com" };

    public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 }; }

    public static Archivio Carica() {
        Archivio a = null;
        try { if (File.Exists(FILE)) a = Json().Deserialize<Archivio>(File.ReadAllText(FILE, Encoding.UTF8)); }
        catch (Exception ex) { Log("archivio illeggibile, ne copio uno da parte: " + ex.Message); try { File.Copy(FILE, FILE + ".rotto", true); } catch { } }
        if (a == null) a = new Archivio();
        if (a.Repo == null) a.Repo = new List<RepoInfo>();
        if (a.Elementi == null) a.Elementi = new List<Elemento>();
        if (a.Decisioni == null) a.Decisioni = new List<Decisione>();
        if (a.DominiFidati == null || a.DominiFidati.Count == 0) a.DominiFidati = FIDATI.ToList();
        // da un archivio di prima della 0.4 la cartella delle repo si ricava una volta sola, poi è un'impostazione come le altre
        if (Accessi.Migra(a) && a.CartellaRepo == null) a.CartellaRepo = CartellaDiPrima();
        CartellaRepo = a.CartellaRepo;
        return a;
    }

    static string CartellaDiPrima() {
        string c = Environment.GetEnvironmentVariable("GPM_REPO");
        return string.IsNullOrEmpty(c) ? CartellaProposta() : c;
    }

    // la cartella che si propone alla prima apertura, solo se c'è davvero
    public static string CartellaProposta() {
        string c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GitHub");
        return Directory.Exists(c) ? c : "";
    }

    // si scrive accanto e poi si sostituisce: un'interruzione a metà non rovina l'archivio
    public static void Salva(Archivio a) {
        Directory.CreateDirectory(Cartella);
        string tmp = FILE + ".nuovo";
        File.WriteAllText(tmp, Json().Serialize(a), new UTF8Encoding(false));
        if (File.Exists(FILE)) File.Replace(tmp, FILE, FILE + ".prima");
        else File.Move(tmp, FILE);
    }

    // le consegne accettate che aspettano un rilascio, dalla cartella locale della repo (formato di consegna.py:
    // «rilascio» è nullo finché non escono). -1 = questa repo non ha la cartella su questo computer.
    public static int ConsegneInAttesa(string repo) {
        if (string.IsNullOrEmpty(CartellaRepo)) return -1;
        string nome = repo.Substring(repo.IndexOf('/') + 1);
        if (nome.Length == 0 || nome.Trim('.').Length == 0 || nome.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return -1;
        string c = Path.Combine(CartellaRepo, nome, "consegne", "accettate");
        if (!Directory.Exists(c)) return -1;
        int n = 0;
        foreach (var f in Directory.GetFiles(c, "*.json")) {
            try {
                var d = Json().DeserializeObject(File.ReadAllText(f, Encoding.UTF8)) as Dictionary<string, object>;
                object r;
                if (d != null && d.TryGetValue("rilascio", out r) && r == null) n++;
            } catch (Exception ex) { Log("consegna illeggibile " + Path.GetFileName(f) + ": " + ex.Message); }
        }
        return n;
    }

    public static string Impronta(Elemento e) {
        using (var h = SHA256.Create()) {
            var b = h.ComputeHash(Encoding.UTF8.GetBytes((e.Titolo ?? "") + "\n" + (e.Testo ?? "")));
            return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant().Substring(0, 32);
        }
    }

    public static void Log(string riga) {
        try {
            Directory.CreateDirectory(Cartella);
            string f = Path.Combine(Cartella, "ProjectManager.log");
            if (File.Exists(f) && new FileInfo(f).Length > 256 * 1024) { File.Copy(f, f + ".vecchio", true); File.Delete(f); }
            File.AppendAllText(f, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " " + riga + Environment.NewLine, Encoding.UTF8);
        } catch { }
    }
}
