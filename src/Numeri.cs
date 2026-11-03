// Numeri.cs (Project Manager 1.1, 03/11/2026): lo storico di «Come sta andando». GitHub tiene il traffico solo 14 giorni: i giorni
// vecchi restano solo qui, in storico.json nella cartella dati, separato dall'archivio perché cresce e perché può rompersi da solo.
// Un file rotto non ferma niente: si mette da parte e si riparte da vuoto. Un file che non si riesce ad aprire non è rotto: non si
// sovrascrive finché non si è riusciti a rileggerlo. Stessa data: vale il valore di GitHub. Un giorno che
// GitHub non dà più resta com'è. Per i download GitHub dà solo il totale: quelli del giorno sono la differenza fra due totali.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class Storico {
    static string FILE { get { return Path.Combine(Dati.Cartella, "storico.json"); } }

    static bool chiuso;   // il file c'è ma non si è riusciti ad aprirlo: potrebbe essere buono

    static ArchivioStorico Leggi() {
        if (!File.Exists(FILE)) return null;
        var s = Dati.Json().Deserialize<ArchivioStorico>(File.ReadAllText(FILE, Encoding.UTF8));
        if (s == null || s.Repo == null) throw new FormatException("vuoto");
        return s;
    }

    static void DaParte(Exception ex) {
        Dati.Log("storico illeggibile, ne copio uno da parte: " + ex.Message);
        try { File.Copy(FILE, FILE + ".rotto", true); } catch { }
    }

    public static ArchivioStorico Carica() {
        ArchivioStorico s = null;
        chiuso = false;
        try { s = Leggi(); }
        catch (IOException ex) { chiuso = true; Dati.Log("storico chiuso, per ora non lo scrivo: " + ex.Message); }
        catch (UnauthorizedAccessException ex) { chiuso = true; Dati.Log("storico chiuso, per ora non lo scrivo: " + ex.Message); }
        catch (Exception ex) { DaParte(ex); }
        return Ordina(s ?? new ArchivioStorico());
    }

    // i giorni del file che si è riusciti a riaprire tornano dentro: dove c'è già un valore di adesso, vale quello
    static void Riprendi(ArchivioStorico s, ArchivioStorico file) {
        foreach (var r in file.Repo) {
            string k = Chiave(s, r.Key) ?? r.Key;
            List<GiornoStorico> giorni;
            if (!s.Repo.TryGetValue(k, out giorni)) giorni = new List<GiornoStorico>();
            foreach (var v in r.Value) {
                var g = Giorno(giorni, v.Giorno);
                if (g.Visite < 0) g.Visite = v.Visite;
                if (g.Unici < 0) g.Unici = v.Unici;
                if (g.Cloni < 0) g.Cloni = v.Cloni;
                if (g.Download < 0) g.Download = v.Download;
            }
            giorni.Sort((a, b) => string.CompareOrdinal(a.Giorno, b.Giorno));
            s.Repo[k] = giorni;
        }
    }

    // dal file può arrivare di tutto: si tengono solo i giorni con una data vera, una volta sola, in ordine
    static ArchivioStorico Ordina(ArchivioStorico s) {
        var pulito = new ArchivioStorico();
        foreach (var r in s.Repo) {
            if (r.Value == null) continue;
            var visti = new Dictionary<string, GiornoStorico>();
            foreach (var g in r.Value) if (g != null && DataBuona(g.Giorno)) visti[g.Giorno] = g;
            pulito.Repo[r.Key] = visti.Values.OrderBy(x => x.Giorno, StringComparer.Ordinal).ToList();
        }
        return pulito;
    }

    static bool DataBuona(string g) {
        DateTime d;
        return g != null && Regex.IsMatch(g, @"\A\d{4}-\d{2}-\d{2}\z") && DateTime.TryParseExact(g, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d);
    }

    public static void Salva(ArchivioStorico s) {
        if (chiuso) {
            ArchivioStorico file = null;
            try { file = Leggi(); }
            catch (IOException ex) { Dati.Log("storico ancora chiuso, non lo scrivo: " + ex.Message); return; }
            catch (UnauthorizedAccessException ex) { Dati.Log("storico ancora chiuso, non lo scrivo: " + ex.Message); return; }
            catch (Exception ex) { DaParte(ex); }
            chiuso = false;
            if (file != null) Riprendi(s, Ordina(file));
        }
        Directory.CreateDirectory(Dati.Cartella);
        string tmp = FILE + ".nuovo";
        File.WriteAllText(tmp, Dati.Json().Serialize(s), new UTF8Encoding(false));
        if (File.Exists(FILE)) File.Replace(tmp, FILE, FILE + ".prima");
        else File.Move(tmp, FILE);
    }

    static string Chiave(ArchivioStorico s, string repo) {
        return s.Repo.Keys.FirstOrDefault(k => string.Equals(k, repo, StringComparison.OrdinalIgnoreCase));
    }

    static GiornoStorico Giorno(List<GiornoStorico> giorni, string data) {
        var g = giorni.FirstOrDefault(x => x.Giorno == data);
        if (g == null) { g = new GiornoStorico { Giorno = data }; giorni.Add(g); }
        return g;
    }

    // mette nello storico quello che questa lettura ha portato davvero: una parte con l'errore è la lettura di prima, non si riscrive.
    // oggi: yyyy-MM-dd in UTC, come i giorni di GitHub
    public static void Unisci(ArchivioStorico s, string repo, NumeriRepo n, string oggi) {
        if (n == null) return;
        string k = Chiave(s, repo) ?? repo;
        List<GiornoStorico> giorni;
        if (!s.Repo.TryGetValue(k, out giorni)) giorni = new List<GiornoStorico>();
        if (n.VisiteLette && n.VisiteErrore == null && n.VisiteGiorni != null)
            foreach (var g in n.VisiteGiorni) { var x = Giorno(giorni, g.Giorno); x.Visite = g.Conta; x.Unici = g.Unici; }
        if (n.CloniLetti && n.CloniErrore == null && n.CloniGiorni != null)
            foreach (var g in n.CloniGiorni) Giorno(giorni, g.Giorno).Cloni = g.Conta;
        if (n.DownloadLetti && n.DownloadErrore == null) Giorno(giorni, oggi).Download = n.Download;
        if (giorni.Count == 0) return;
        giorni.Sort((a, b) => string.CompareOrdinal(a.Giorno, b.Giorno));
        s.Repo[k] = giorni;
    }

    // i download di ogni giorno: la differenza fra due totali di fila, sul giorno più recente. Il primo giorno non si sa.
    // Il totale può scendere se si cancella una versione: quel giorno vale 0, non un numero negativo
    public static List<GiornoTraffico> DownloadPerGiorno(IEnumerable<GiornoStorico> giorni) {
        var uscita = new List<GiornoTraffico>();
        long prima = -1;
        foreach (var g in giorni.Where(x => x.Download >= 0).OrderBy(x => x.Giorno, StringComparer.Ordinal)) {
            if (prima >= 0) uscita.Add(new GiornoTraffico { Giorno = g.Giorno, Conta = Math.Max(0, g.Download - prima) });
            prima = g.Download;
        }
        return uscita;
    }

    public static void Rinomina(ArchivioStorico s, string vecchio, string nuovo) {
        string k = Chiave(s, vecchio);
        if (k == null || string.Equals(k, nuovo, StringComparison.OrdinalIgnoreCase)) return;
        var giorni = s.Repo[k];
        s.Repo.Remove(k);
        string gia = Chiave(s, nuovo);
        if (gia != null) {
            foreach (var g in s.Repo[gia]) { giorni.RemoveAll(x => x.Giorno == g.Giorno); giorni.Add(g); }
            s.Repo.Remove(gia);
        }
        giorni.Sort((a, b) => string.CompareOrdinal(a.Giorno, b.Giorno));
        s.Repo[nuovo] = giorni;
    }

    // dice quanti giorni si sono persi
    public static int Togli(ArchivioStorico s, string repo) {
        string k = Chiave(s, repo);
        if (k == null) return 0;
        int n = s.Repo[k].Count;
        s.Repo.Remove(k);
        return n;
    }

    public static int QuantiGiorni(ArchivioStorico s, string repo) {
        string k = Chiave(s, repo);
        return k == null ? 0 : s.Repo[k].Count;
    }

    public static List<GiornoStorico> Copia(ArchivioStorico s, string repo) {
        string k = Chiave(s, repo);
        if (k == null) return new List<GiornoStorico>();
        return s.Repo[k].Select(g => new GiornoStorico { Giorno = g.Giorno, Visite = g.Visite, Unici = g.Unici, Cloni = g.Cloni, Download = g.Download }).ToList();
    }
}
