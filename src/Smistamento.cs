using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public class ErroreRicorrente {
    public string Repo, Firma, Esempio;
    public int Volte;
    public HashSet<string> Rapporti = new HashSet<string>(), Versioni = new HashSet<string>();
    public DateTime Primo = DateTime.MaxValue, Ultimo = DateTime.MinValue;
}

public static class Smistamento {
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    public static readonly string[] CATEGORIE = { "log", "bug", "richiesta", "domanda", "recensione", "pull request", "altro" };

    // un rapporto «Invia log degli errori»: WeightMonitor 0.3.1+ e i programmi futuri che usano lo stesso titolo
    static readonly Regex TITOLO_LOG = new Regex(@"^Log degli errori, (.+?) v?(\d+(?:\.\d+)+)", O);

    // la dogana su ogni testo a ogni giro è lenta: lo stesso testo con gli stessi siti fidati dà lo stesso esito
    static readonly Dictionary<string, List<Motivo>> GIA_VISTI = new Dictionary<string, List<Motivo>>();

    static List<Motivo> Esamina(Elemento e, string impronta, IEnumerable<string> fidati) {
        string chiave = Lingua.Codice + "\n" + impronta + "\n" + string.Join(",", fidati ?? new string[0]);
        lock (GIA_VISTI) {
            List<Motivo> m;
            if (GIA_VISTI.TryGetValue(chiave, out m)) return m;
        }
        var nuovi = Dogana.Esamina(e.Titolo, e.Testo, fidati);
        lock (GIA_VISTI) {
            if (GIA_VISTI.Count > 5000) GIA_VISTI.Clear();
            GIA_VISTI[chiave] = nuovi;
        }
        return nuovi;
    }

    public static List<Voce> Voci(Archivio a) {
        var decisioni = new Dictionary<string, Decisione>();
        foreach (var d in a.Decisioni) decisioni[d.Chiave] = d;
        var madri = new Dictionary<string, Elemento>();
        foreach (var e in a.Elementi) if (e.Tipo != "commento") madri[e.Repo + "#" + e.Numero] = e;
        var voci = new List<Voce>();
        foreach (var e in a.Elementi) {
            var v = new Voce { E = e, Impronta = Dati.Impronta(e) };
            v.Motivi = Esamina(e, v.Impronta, a.DominiFidati);
            Elemento madre;
            madri.TryGetValue(e.Repo + "#" + e.Numero, out madre);
            v.Categoria = Categoria(e, madre);
            Decisione d;
            decisioni.TryGetValue(e.Chiave, out d);
            // la decisione vale solo per il testo che si è visto: se il testo è cambiato, ripassa dalla dogana
            bool valida = d != null && d.Impronta == v.Impronta && !string.IsNullOrEmpty(d.Stato);
            v.D = d != null && (d.Impronta == v.Impronta || d.RiassuntoImpronta == v.Impronta) ? d : null;
            if (valida) v.Stato = d.Stato == "approvato" ? "lavorare" : d.Stato;   // fatto, rifiutato, quarantena
            else v.Stato = v.Motivi.Count > 0 ? "quarantena" : "lavorare";
            voci.Add(v);
        }
        return voci;
    }

    // i valori restano in italiano nei dati salvati: qui si dice solo come si leggono
    public static string NomeCategoria(string c) { return Lingua.T("categoria." + c.Replace(' ', '_')); }
    public static string NomeStato(string s) { return Lingua.T("stato." + s); }
    public static string NomeTipo(string t) { return Lingua.T("tipo." + t); }

    public static string TitoloDi(Elemento e, Archivio a) {
        if (e.Tipo != "commento") return e.Titolo;
        var m = a.Elementi.FirstOrDefault(x => x.Tipo != "commento" && x.Repo == e.Repo && x.Numero == e.Numero);
        return Lingua.T("smistamento.commento", "parte", (m == null ? "#" + e.Numero : m.Titolo));
    }

    public static string Categoria(Elemento e, Elemento madre) {
        var rif = e.Tipo == "commento" && madre != null ? madre : e;
        if (rif.Tipo == "pull") return "pull request";
        string et = (rif.Etichette ?? "").ToLowerInvariant();
        if (TITOLO_LOG.IsMatch(rif.Titolo ?? "") || et.Split(',').Contains("log")) return "log";
        if (Regex.IsMatch(et, @"\bbug\b")) return "bug";
        if (Regex.IsMatch(et, @"\b(enhancement|feature|richiesta)\b")) return "richiesta";
        if (Regex.IsMatch(et, @"\b(question|domanda)\b")) return "domanda";
        string t = ((rif.Titolo ?? "") + "\n" + (e.Testo ?? "")).ToLowerInvariant();
        if (Regex.IsMatch(t, @"\b(errore|error|crash\w*|bug|non funziona|non va|non parte|si blocca|bloccat\w*|si chiude|eccezione|exception|broken|doesn'?t work|not working|freez\w*|sbagliat\w*)\b")) return "bug";
        if (Regex.IsMatch(t, @"\b(vorrei|sarebbe bello|si potrebbe|potresti|aggiung\w*|richiesta|proposta|suggeriment\w*|feature|would be nice|please add|could you add|request)\b")) return "richiesta";
        if (Regex.IsMatch(t, @"\?\s*$|\b(come si (fa|può|imposta|installa|usa|toglie|cambia)|come faccio|how do i|how to|is it possible|si può)\b")) return "domanda";
        if (Regex.IsMatch(t, @"\b(grazie|ottimo|bellissim\w*|fantastic\w*|perfett\w*|funziona benissimo|thanks|thank you|great|love it|awesome)\b")) return "recensione";
        return "altro";
    }

    public static string Versione(Elemento e) {
        var m = TITOLO_LOG.Match(e.Titolo ?? "");
        if (m.Success) return m.Groups[2].Value;
        m = Regex.Match(e.Testo ?? "", @"^\s*`*\s*\S+ v?(\d+(?:\.\d+)+)", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : "?";
    }

    // la firma di una riga di log: numeri, percorsi e testi fra virgolette diventano segnaposto, così lo stesso errore
    // con valori diversi finisce nello stesso gruppo
    public static string Firma(string riga) {
        string s = riga;
        s = Regex.Replace(s, @"[a-zA-Z]:\\[^\s:;,]+", "<percorso>");
        s = Regex.Replace(s, "\"[^\"]*\"|«[^»]*»|'[^']{2,}'", "\"...\"");
        s = Regex.Replace(s, @"0x[0-9a-fA-F]+", "0x#");
        s = Regex.Replace(s, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", "<guid>");
        s = Regex.Replace(s, @"\d+([.,]\d+)*", "#");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 160 ? s.Substring(0, 160) : s;
    }

    static readonly Regex RIGA_LOG = new Regex(@"^\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}) (.+?)\s*$", RegexOptions.Multiline);

    public static List<ErroreRicorrente> Ricorrenti(IEnumerable<Voce> voci) {
        var gruppi = new Dictionary<string, ErroreRicorrente>();
        foreach (var v in voci.Where(x => x.Categoria == "log" && x.Stato != "quarantena" && x.Stato != "rifiutato")) {
            string ver = Versione(v.E);
            foreach (Match m in RIGA_LOG.Matches((v.E.Testo ?? "").Replace("\r\n", "\n"))) {
                string firma = Firma(m.Groups[2].Value);
                string k = v.E.Repo + "\n" + firma;
                ErroreRicorrente g;
                if (!gruppi.TryGetValue(k, out g)) gruppi[k] = g = new ErroreRicorrente { Repo = v.E.Repo, Firma = firma, Esempio = m.Groups[2].Value.Trim() };
                g.Volte++;
                g.Rapporti.Add(v.E.Chiave);
                g.Versioni.Add(ver);
                DateTime t;
                if (DateTime.TryParseExact(m.Groups[1].Value, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) {
                    if (t < g.Primo) g.Primo = t;
                    if (t > g.Ultimo) g.Ultimo = t;
                }
            }
        }
        // prima gli errori che tornano in più rapporti, poi quelli ripetuti più volte
        return gruppi.Values.OrderByDescending(g => g.Rapporti.Count).ThenByDescending(g => g.Volte).ThenByDescending(g => g.Ultimo).ToList();
    }
}
