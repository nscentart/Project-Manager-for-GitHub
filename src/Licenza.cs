// Licenza.cs (03/11/2026): la licenza personale e commerciale, lo stesso file in Project Manager e in WeightMonitor.
// Il testo è NSCENT-1.<dati>.<firma>: i dati sono programma, numero, intestatario e data; la firma è ECDSA P-256 con SHA-256
// sui byte del pezzo <dati>. Qui ci sono solo chiavi pubbliche: non permettono di creare licenze. Nessuna rete, nessuna
// libreria, nessun'altra classe del programma. Cartella dati e nome del programma li passa chi chiama, e le frasi per
// l'utente stanno fuori da qui. Si controlla senza mai fermare niente: un testo sbagliato dà un esito, non un errore.
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public enum StatoLicenza { Assente, Valida, NonValida, AltroProgramma, Revocata, Illeggibile }

// i campi si riempiono solo da dati firmati: con un testo non valido restano vuoti
public class EsitoLicenza {
    public StatoLicenza Stato;
    public string Programma = "", Intestatario = "", Data = "";
    public int Numero;
    public bool Valida { get { return Stato == StatoLicenza.Valida; } }
}

public static class Licenza {
    public const string FILE = "licenza.txt", INIZIO = "NSCENT-1";
    public const int MAX_INTESTATARIO = 80, MAX_TESTO = 1000;
    const int MAX_FILE = 8192, MAX_PROGRAMMA = 40, LATO = 32;

    // le chiavi pubbliche in base64url, una per riga: una nuova si affianca alle vecchie, così le licenze vendute restano buone
    public static readonly string[] CHIAVI_PUBBLICHE = { "6spl-c33gRNzEfGA1yLICcsI1-A8llhc5u2hYpdECvZge7moC6WzlDVXyWoCjxHhx9WwVBMiMVg7feeqcEk8Sg" };

    // i numeri ritirati: si aggiornano con le versioni nuove del programma
    public static readonly int[] REVOCATI = { };

    // ---------- il controllo ----------

    public static EsitoLicenza Controlla(string testo, string programma) { return Controlla(testo, programma, CHIAVI_PUBBLICHE, REVOCATI); }

    public static EsitoLicenza Controlla(string testo, string programma, string[] chiavi, int[] revocati) {
        var no = new EsitoLicenza { Stato = StatoLicenza.NonValida };
        try {
            string t = Pulisci(testo);
            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(programma) || chiavi == null) return no;
            var pezzi = t.Split('.');
            if (pezzi.Length != 3 || pezzi[0] != INIZIO) return no;
            byte[] firma = DaBase64Url(pezzi[2]);
            if (firma == null || firma.Length != LATO * 2 || DaBase64Url(pezzi[1]) == null) return no;
            if (!FirmaBuona(DaFirmare(pezzi[1]), firma, chiavi)) return no;
            // da qui in poi si legge solo quello che è firmato
            var e = Leggi(pezzi[1]);
            if (e == null) return no;
            if (e.Programma != programma) e.Stato = StatoLicenza.AltroProgramma;
            else if (revocati != null && Array.IndexOf(revocati, e.Numero) >= 0) e.Stato = StatoLicenza.Revocata;
            else e.Stato = StatoLicenza.Valida;
            return e;
        } catch { return no; }
    }

    static bool FirmaBuona(byte[] dati, byte[] firma, string[] chiavi) {
        foreach (var k in chiavi) {
            byte[] blob = BlobPubblico(k);
            if (blob == null) continue;   // una chiave rotta nell'elenco non ferma le altre
            try {
                using (var c = CngKey.Import(blob, CngKeyBlobFormat.EccPublicBlob))
                using (var e = new ECDsaCng(c)) {
                    e.HashAlgorithm = CngAlgorithm.Sha256;
                    if (e.VerifyData(dati, firma)) return true;
                }
            } catch { }
        }
        return false;
    }

    static EsitoLicenza Leggi(string dati) {
        var righe = new UTF8Encoding(false, true).GetString(DaBase64Url(dati)).Split('\n');
        int numero;
        if (righe.Length != 4 || !ProgrammaBuono(righe[0]) || !NumeroBuono(righe[1], out numero) || !IntestatarioBuono(righe[2]) || !DataBuona(righe[3])) return null;
        return new EsitoLicenza { Programma = righe[0], Numero = numero, Intestatario = righe[2], Data = righe[3] };
    }

    // ---------- i campi ----------

    static bool ProgrammaBuono(string s) {
        if (string.IsNullOrEmpty(s) || s.Length > MAX_PROGRAMMA) return false;
        foreach (char c in s) if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9')) return false;
        return true;
    }

    static bool NumeroBuono(string s, out int n) {
        n = 0;
        if (string.IsNullOrEmpty(s) || s.Length > 9 || s[0] == '0') return false;
        foreach (char c in s) if (c < '0' || c > '9') return false;
        n = int.Parse(s, CultureInfo.InvariantCulture);
        return true;
    }

    // 1-80 caratteri, senza spazi ai bordi, senza caratteri di controllo né invisibili
    public static bool IntestatarioBuono(string s) {
        if (string.IsNullOrEmpty(s) || s.Length > MAX_INTESTATARIO || s != s.Trim()) return false;
        for (int i = 0; i < s.Length; i++) {
            if (char.IsSurrogatePair(s, i)) { i++; continue; }
            var u = CharUnicodeInfo.GetUnicodeCategory(s[i]);
            if (u == UnicodeCategory.Control || u == UnicodeCategory.Format || u == UnicodeCategory.Surrogate
                || u == UnicodeCategory.LineSeparator || u == UnicodeCategory.ParagraphSeparator) return false;
        }
        return true;
    }

    static bool DataBuona(string s) {
        DateTime d;
        return s != null && s.Length == 10 && DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }

    // ---------- il formato ----------

    // toglie spazi e a capo di chi incolla; nullo se è troppo lungo per essere una licenza
    public static string Pulisci(string testo) {
        if (testo == null || testo.Length > MAX_FILE) return null;
        var sb = new StringBuilder();
        foreach (char c in testo) if (!char.IsWhiteSpace(c) && c != '\uFEFF') sb.Append(c);
        return sb.Length > MAX_TESTO ? null : sb.ToString();
    }

    // il pezzo <dati>, nullo se un campo non va bene. La firma la fa chi ha la chiave privata, fuori da questo file
    public static string Componi(string programma, int numero, string intestatario, string data) {
        int n;
        string num = numero.ToString(CultureInfo.InvariantCulture);
        if (!ProgrammaBuono(programma) || !NumeroBuono(num, out n) || !IntestatarioBuono(intestatario) || !DataBuona(data)) return null;
        return Base64Url(new UTF8Encoding(false, true).GetBytes(programma + "\n" + num + "\n" + intestatario + "\n" + data));
    }

    public static byte[] DaFirmare(string dati) { return Encoding.ASCII.GetBytes(dati); }

    public static string Unisci(string dati, byte[] firma) { return INIZIO + "." + dati + "." + Base64Url(firma); }

    public static string Base64Url(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

    // severa: un solo modo di scrivere gli stessi byte. Nullo se il testo non è esatto
    public static byte[] DaBase64Url(string s) {
        if (string.IsNullOrEmpty(s) || s.Length % 4 == 1) return null;
        foreach (char c in s) if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return null;
        try {
            var b = Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/') + new string('=', (4 - s.Length % 4) % 4));
            return Base64Url(b) == s ? b : null;
        } catch (FormatException) { return null; }
    }

    // la chiave pubblica come testo: X e Y (64 byte) in base64url, dal blob di CngKey.Export(EccPublicBlob). Nullo se non è P-256
    public static string ChiaveComeTesto(byte[] blob) {
        if (blob == null || blob.Length != 8 + LATO * 2 || blob[0] != 0x45 || blob[1] != 0x43 || blob[2] != 0x53 || blob[3] != 0x31) return null;
        var xy = new byte[LATO * 2];
        Buffer.BlockCopy(blob, 8, xy, 0, xy.Length);
        return Base64Url(xy);
    }

    static byte[] BlobPubblico(string chiave) {
        byte[] xy = DaBase64Url(chiave);
        if (xy == null || xy.Length != LATO * 2) return null;
        var blob = new byte[8 + xy.Length];
        blob[0] = 0x45; blob[1] = 0x43; blob[2] = 0x53; blob[3] = 0x31;   // «ECS1»: pubblica, P-256
        blob[4] = LATO;
        Buffer.BlockCopy(xy, 0, blob, 8, xy.Length);
        return blob;
    }

    // ---------- il file ----------

    public static EsitoLicenza Carica(string cartella, string programma) { return Carica(cartella, programma, CHIAVI_PUBBLICHE, REVOCATI); }

    // assente, rotto o illeggibile: si risponde sempre con un esito, il programma parte comunque
    public static EsitoLicenza Carica(string cartella, string programma, string[] chiavi, int[] revocati) {
        string testo;
        try {
            string f = Path.Combine(cartella, FILE);
            if (!File.Exists(f) && !Directory.Exists(f)) return new EsitoLicenza { Stato = StatoLicenza.Assente };
            using (var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                if (s.Length > MAX_FILE) return new EsitoLicenza { Stato = StatoLicenza.NonValida };
                var b = new byte[(int)s.Length];
                int letti = 0, n;
                while (letti < b.Length && (n = s.Read(b, letti, b.Length - letti)) > 0) letti += n;
                using (var r = new StreamReader(new MemoryStream(b, 0, letti), Encoding.UTF8, true)) testo = r.ReadToEnd();
            }
        } catch { return new EsitoLicenza { Stato = StatoLicenza.Illeggibile }; }
        return Controlla(testo, programma, chiavi, revocati);
    }

    // si scrive accanto e poi si sostituisce: un'interruzione a metà non rovina quella che c'era
    public static bool Scrivi(string cartella, string testo) {
        string t = Pulisci(testo), tmp = null;
        if (string.IsNullOrEmpty(t)) return false;
        try {
            Directory.CreateDirectory(cartella);
            string f = Path.Combine(cartella, FILE);
            tmp = f + ".nuovo";
            File.WriteAllText(tmp, t + "\r\n", new UTF8Encoding(false));
            if (File.Exists(f)) File.Replace(tmp, f, null); else File.Move(tmp, f);
            return true;
        } catch {
            try { if (tmp != null) File.Delete(tmp); } catch { }
            return false;
        }
    }

    // vero anche se non c'era; falso solo se è rimasta lì
    public static bool Togli(string cartella) {
        try {
            string f = Path.Combine(cartella, FILE);
            if (File.Exists(f)) File.Delete(f);
            return !File.Exists(f) && !Directory.Exists(f);
        } catch { return false; }
    }
}
