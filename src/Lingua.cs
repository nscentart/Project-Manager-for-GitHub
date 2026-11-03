// Lingua.cs (Project Manager 1.1, 03/11/2026): i testi che l'utente vede, in cinque lingue, col metodo di WeightMonitor. Stanno in
// lingue\<codice>.txt, una riga «chiave=testo», dentro l'exe come risorse. Se manca un testo: lingua scelta, poi inglese, poi
// italiano, poi la chiave: non si resta mai senza scritta e non si cade mai. I segnaposto {nome} si sostituiscono come testo
// semplice, mai con string.Format: una parentesi sbagliata in una traduzione non deve poter far cadere il programma.
// Non si traducono: registro di Windows, log, pacchetto per Claude Code, valori salvati nell'archivio, nomi dei file.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static class Lingua {
    public static readonly string[] CODICI = { "it", "en", "fr", "es", "de" };
    static readonly string[] NOMI = { "Italiano", "English", "Français", "Español", "Deutsch" };
    static readonly string[] CULTURE = { "it-IT", "en-US", "fr-FR", "es-ES", "de-DE" };
    static readonly string[] PER_MODELLO = { "italiano", "inglese", "francese", "spagnolo", "tedesco" };
    public const string PARTENZA = "it", RIPIEGO = "en";
    const string FILE_SCELTA = "lingua.txt";

    static readonly Dictionary<string, Dictionary<string, string>> tabelle = new Dictionary<string, Dictionary<string, string>>();
    static volatile string corrente = PARTENZA;
    static volatile CultureInfo cultura = new CultureInfo("it-IT");

    public static string Codice { get { return corrente; } }
    public static bool Valido(string codice) { return codice != null && Array.IndexOf(CODICI, codice) >= 0; }
    public static string NomeDi(string codice) { int i = Array.IndexOf(CODICI, codice); return i < 0 ? codice : NOMI[i]; }
    // il nome della lingua dentro il compito dato al modello locale per i riassunti, che è scritto in italiano
    public static string NomePerModello() { return PER_MODELLO[Array.IndexOf(CODICI, corrente)]; }

    // la lingua di Windows, se è fra le cinque; se no l'inglese
    public static string DaWindows() { return DaCultura(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName); }
    public static string DaCultura(string duelettere) { return Valido(duelettere) ? duelettere : RIPIEGO; }

    public static string FileScelta(string cartella) { return Path.Combine(cartella, FILE_SCELTA); }

    static string Salvata(string cartella) {
        try {
            string f = FileScelta(cartella);
            if (!File.Exists(f)) return null;
            string v = File.ReadAllText(f).Trim();
            return Valido(v) ? v : "";
        } catch (Exception ex) { Dati.Log("lingua: " + ex.GetType().Name); return ""; }
    }

    // vero se la persona deve ancora scegliere: niente lingua salvata e nessun dato di una versione di prima
    public static bool DaScegliere(string cartella, bool datiDiPrima) { return Salvata(cartella) == null && !datiDiPrima; }

    // all'avvio. --lingua vale solo per questa volta e non si scrive; se no la scelta salvata. Chi ha già dei dati senza questa
    // scelta viene da una versione solo italiana: resta in italiano. Chi non ha niente parte dalla lingua di Windows
    public static void Inizia(string daComando, string cartella, bool datiDiPrima) {
        if (Valido(daComando)) { Usa(daComando); return; }
        string salvata = Salvata(cartella);
        Usa(Valido(salvata) ? salvata : salvata != null || datiDiPrima ? PARTENZA : DaWindows());
    }

    public static void Usa(string codice) {
        if (!Valido(codice)) return;
        cultura = new CultureInfo(CULTURE[Array.IndexOf(CODICI, codice)]);
        corrente = codice;
    }

    // dalla finestra della scelta e dalle Impostazioni: vale subito e sopravvive agli aggiornamenti
    public static void Scegli(string codice, string cartella) {
        if (!Valido(codice)) return;
        Usa(codice);
        try {
            Directory.CreateDirectory(cartella);
            File.WriteAllText(FileScelta(cartella), codice, new UTF8Encoding(false));
        } catch (Exception ex) { Dati.Log("lingua: " + ex.GetType().Name); }
    }

    static Dictionary<string, string> Tabella(string codice) {
        lock (tabelle) {
            Dictionary<string, string> t;
            if (tabelle.TryGetValue(codice, out t)) return t;
            t = new Dictionary<string, string>();
            try {
                using (var s = Programma.Risorsa("lingue." + codice + ".txt"))
                    if (s != null) using (var r = new StreamReader(s, new UTF8Encoding(false))) t = Leggi(r.ReadToEnd());
            } catch (Exception ex) { Dati.Log("lingua " + codice + ": " + ex.Message); }
            tabelle[codice] = t;
            return t;
        }
    }

    // gli spazi si tengono così come sono: il francese ha quelli indivisibili
    public static Dictionary<string, string> Leggi(string testo) {
        var t = new Dictionary<string, string>();
        foreach (var riga in testo.Split('\n')) {
            string r = riga.TrimEnd('\r');
            if (r.Length == 0 || r[0] == '#') continue;
            int i = r.IndexOf('=');
            if (i <= 0) continue;
            t[r.Substring(0, i)] = r.Substring(i + 1).Replace("\\n", "\n");
        }
        return t;
    }

    static string Cerca(string chiave) {
        string v;
        if (Tabella(corrente).TryGetValue(chiave, out v)) return v;
        if (Tabella(RIPIEGO).TryGetValue(chiave, out v)) return v;
        if (Tabella(PARTENZA).TryGetValue(chiave, out v)) return v;
        lock (nonTrovate) nonTrovate.Add(chiave);
        return chiave;
    }

    // solo per le prove: i testi cercati e non trovati
    static readonly HashSet<string> nonTrovate = new HashSet<string>();
    public static List<string> NonTrovate() { lock (nonTrovate) return new List<string>(nonTrovate); }
    public static void SvuotaNonTrovate() { lock (nonTrovate) nonTrovate.Clear(); }

    // Lingua.T("agg.uscita", "n", "1.0"): dopo la chiave, coppie di nome e valore
    public static string T(string chiave, params object[] coppie) { return Riempi(Cerca(chiave), coppie); }

    // i plurali sono due chiavi, «.uno» e «.molti». {n} lo mette già lui, coi separatori della lingua
    public static string N(string chiave, long n, params object[] coppie) {
        bool uno = corrente == "fr" ? n == 0 || n == 1 : n == 1;
        var tutte = new object[coppie.Length + 2];
        tutte[0] = "n"; tutte[1] = Numero(n);
        Array.Copy(coppie, 0, tutte, 2, coppie.Length);
        return T(chiave + (uno ? ".uno" : ".molti"), tutte);
    }

    public static string Numero(long n) { return n.ToString("N0", cultura); }

    // una passata sola: quello che entra da un valore non viene mai riletto come segnaposto
    public static string Riempi(string testo, object[] coppie) {
        if (coppie == null || coppie.Length < 2 || testo.IndexOf('{') < 0) return testo;
        var sb = new StringBuilder(testo.Length + 16);
        int i = 0;
        while (i < testo.Length) {
            char c = testo[i];
            int fine = c == '{' ? testo.IndexOf('}', i + 1) : -1;
            if (fine < 0) { sb.Append(c); i++; continue; }
            string nome = testo.Substring(i + 1, fine - i - 1);
            bool messo = false;
            for (int k = 0; k + 1 < coppie.Length; k += 2)
                if (string.Equals(coppie[k] as string, nome)) { sb.Append(Convert.ToString(coppie[k + 1], CultureInfo.InvariantCulture)); messo = true; break; }
            if (messo) i = fine + 1; else { sb.Append(c); i++; }
        }
        return sb.ToString();
    }

    public static IEnumerable<string> Segnaposto(string testo) {
        int i = 0;
        while ((i = testo.IndexOf('{', i)) >= 0) {
            int fine = testo.IndexOf('}', i);
            if (fine < 0) yield break;
            yield return testo.Substring(i + 1, fine - i - 1);
            i = fine + 1;
        }
    }
    public static IEnumerable<string> Chiavi(string codice) { return Tabella(codice).Keys; }
    public static string Grezzo(string codice, string chiave) { string v; return Tabella(codice).TryGetValue(chiave, out v) ? v : null; }
    // solo per la prova del ripiego
    public static Dictionary<string, string> TabellaPerProva(string codice) { return Tabella(codice); }
}
