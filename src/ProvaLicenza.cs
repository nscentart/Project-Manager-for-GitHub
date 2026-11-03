// ProvaLicenza.cs (Project Manager, 03/11/2026): --prova-licenza prova il controllo, il file e la finestra con chiavi temporanee
// create in memoria e non esportabili: la chiave vera non serve e non si tocca. Nessuna rete; le finestre stanno fuori dallo schermo.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class ProvaLicenza {
    const string PM = LicenzaQui.PROGRAMMA, WM = "WeightMonitor";

    // la coppia di chiavi di una prova: vive in memoria e la parte privata non si può estrarre
    public class Coppia : IDisposable {
        readonly CngKey k = CngKey.Create(CngAlgorithm.ECDsaP256);
        public string Pubblica { get { return Licenza.ChiaveComeTesto(k.Export(CngKeyBlobFormat.EccPublicBlob)); } }
        public byte[] Firma(byte[] dati) { using (var e = new ECDsaCng(k)) { e.HashAlgorithm = CngAlgorithm.Sha256; return e.SignData(dati); } }
        public string FirmaDati(string dati) { return Licenza.Unisci(dati, Firma(Licenza.DaFirmare(dati))); }
        public string Testo(string programma, int numero, string intestatario, string data) { return FirmaDati(Licenza.Componi(programma, numero, intestatario, data)); }
        // qualunque contenuto, anche sbagliato, con una firma buona
        public string Righe(string righe) { return FirmaDati(Licenza.Base64Url(new UTF8Encoding(false).GetBytes(righe))); }
        public string Byte(byte[] b) { return FirmaDati(Licenza.Base64Url(b)); }
        public void Dispose() { k.Dispose(); }
    }

    // una licenza vera nel formato, firmata una volta sola col programmino da una chiave di prova poi buttata: se il formato
    // cambia per sbaglio questa prova diventa rossa, perché le licenze già vendute non sarebbero più buone
    const string CAMPIONE_CHIAVE = "QL4gH5LfgRlHa-HHi_mYEEzIR05zO3WfsQfXvqXturBg89xrznZq_WFFIjrfEoHwPzk_rQcCxJsPLMgtCVlxVA";
    const string CAMPIONE_TESTO = "NSCENT-1.UHJvamVjdE1hbmFnZXIKMQpSb3NzaSBTcmwKMjAyNi0xMS0wMw.x4uOx3v4G8F2mRuqfQAxBwiY6tYu0FRobzssDsSxgdF3rhMx40gXkNdl8BPxhUmN2z35tSlRdDBE7YIu76AYjQ";

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        // scrive licenza.txt nella cartella dati: solo con una cartella di prova
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPM_DATI"))) { File.WriteAllText(esito, "SBAGLIA serve GPM_DATI: una cartella di prova\r\n", new UTF8Encoding(false)); return 2; }
        string temp = Path.Combine(Path.GetTempPath(), "gpm-prova-licenza-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var chiaviVere = LicenzaQui.Chiavi;
        try {
            using (var uno = new Coppia())
            using (var due = new Coppia()) {
                var mie = new[] { uno.Pubblica };
                var nessunRitiro = new int[0];
                Func<string, EsitoLicenza> c = t => Licenza.Controlla(t, PM, mie, nessunRitiro);
                Func<string, bool> nonValida = t => { var e = c(t); return e.Stato == StatoLicenza.NonValida && !e.Valida && e.Intestatario == "" && e.Numero == 0; };
                string buona = uno.Testo(PM, 12, "Rossi Srl", "2026-11-03");
                var pezzi = buona.Split('.');

                // ---------- firma buona ----------
                var b = c(buona);
                v("firma buona: valida", b.Stato == StatoLicenza.Valida && b.Valida);
                v("firma buona: i campi sono quelli firmati", b.Programma == PM && b.Numero == 12 && b.Intestatario == "Rossi Srl" && b.Data == "2026-11-03");
                v("forma: comincia con NSCENT-1 e ha tre pezzi", pezzi.Length == 3 && pezzi[0] == "NSCENT-1");
                v("forma: la firma è di 64 byte", Licenza.DaBase64Url(pezzi[2]).Length == 64);
                v("forma: i dati sono quattro righe", Encoding.UTF8.GetString(Licenza.DaBase64Url(pezzi[1])) == PM + "\n12\nRossi Srl\n2026-11-03");
                v("forma: una riga sola, senza spazi", !buona.Any(char.IsWhiteSpace));
                v("incollata con spazi e a capo: valida", c("  \r\n" + buona + " \r\n\t").Valida);
                v("spezzata da un programma di posta: valida", c(buona.Substring(0, 40) + "\r\n" + buona.Substring(40)).Valida);
                v("nome con accenti e simboli: valido", c(uno.Testo(PM, 13, "Società Caffè & Figli d'Italia", "2026-11-03")).Intestatario == "Società Caffè & Figli d'Italia");
                v("nome di 80 caratteri: valido", c(uno.Testo(PM, 14, new string('x', 80), "2026-11-03")).Valida);
                v("nome con lettere fuori dal piano base: valido", c(uno.Testo(PM, 15, "Rossi \U0001F600 Srl", "2026-11-03")).Valida);
                v("ogni firma è diversa ma buona", c(uno.Testo(PM, 12, "Rossi Srl", "2026-11-03")).Valida);
                v("WeightMonitor col suo nome: valida", Licenza.Controlla(uno.Testo(WM, 16, "Rossi Srl", "2026-11-03"), WM, mie, nessunRitiro).Valida);

                // ---------- testo manomesso ----------
                string[] altri = {
                    uno.Testo(PM, 12, "Bianchi Spa", "2026-11-03"), uno.Testo(PM, 99, "Rossi Srl", "2026-11-03"),
                    uno.Testo(PM, 12, "Rossi Srl", "2030-01-01"), uno.Testo(WM, 12, "Rossi Srl", "2026-11-03") };
                foreach (var a in altri) v("manomesso: dati di un'altra licenza con questa firma", nonValida(pezzi[0] + "." + a.Split('.')[1] + "." + pezzi[2]));
                bool tuttiI = true;
                for (int i = 0; i < pezzi[1].Length; i++) {
                    var x = pezzi[1].ToCharArray(); x[i] = x[i] == 'A' ? 'B' : 'A';
                    if (!nonValida(pezzi[0] + "." + new string(x) + "." + pezzi[2])) tuttiI = false;
                }
                v("manomesso: un carattere cambiato, in ogni posizione dei dati", tuttiI);
                tuttiI = true;
                for (int i = 0; i < pezzi[2].Length; i++) {
                    var x = pezzi[2].ToCharArray(); x[i] = x[i] == 'A' ? 'B' : 'A';
                    if (!nonValida(pezzi[0] + "." + pezzi[1] + "." + new string(x))) tuttiI = false;
                }
                v("manomesso: un carattere cambiato, in ogni posizione della firma", tuttiI);
                v("manomesso: dati e firma scambiati", nonValida(pezzi[0] + "." + pezzi[2] + "." + pezzi[1]));
                v("manomesso: un pezzo in più", nonValida(buona + ".AAAA"));
                v("manomesso: firma assente", nonValida(pezzi[0] + "." + pezzi[1] + "."));
                v("manomesso: solo due pezzi", nonValida(pezzi[0] + "." + pezzi[1]));
                v("manomesso: firma di zeri", nonValida(pezzi[0] + "." + pezzi[1] + "." + Licenza.Base64Url(new byte[64])));
                v("manomesso: firma di 63 byte", nonValida(pezzi[0] + "." + pezzi[1] + "." + Licenza.Base64Url(new byte[63])));
                v("manomesso: firma col riempimento =", nonValida(buona + "=="));
                v("manomesso: inizio di un'altra versione", nonValida("NSCENT-2." + pezzi[1] + "." + pezzi[2]));
                v("manomesso: inizio in minuscolo", nonValida("nscent-1." + pezzi[1] + "." + pezzi[2]));
                v("manomesso: senza inizio", nonValida(pezzi[1] + "." + pezzi[2]));
                v("manomesso: base64 normale al posto dell'url", pezzi[1].IndexOfAny("-_".ToCharArray()) < 0 && pezzi[2].IndexOfAny("-_".ToCharArray()) < 0 || nonValida(buona.Replace('-', '+').Replace('_', '/')));
                // la stessa firma scritta in un altro modo: gli ultimi bit di base64url devono essere zero
                string ultima = pezzi[2];
                char alt = (char)(ultima[ultima.Length - 1] + 1);
                v("manomesso: la stessa firma scritta in un altro modo", nonValida(pezzi[0] + "." + pezzi[1] + "." + ultima.Substring(0, ultima.Length - 1) + alt));
                v("severo: base64url con bit in più", Licenza.DaBase64Url("QQ") != null && Licenza.DaBase64Url("QR") == null);

                // ---------- firma di un'altra chiave ----------
                v("firma di un'altra chiave: non valida", nonValida(due.Testo(PM, 12, "Rossi Srl", "2026-11-03")));
                v("firma di un'altra chiave: non dice niente del contenuto", c(due.Testo(WM, 12, "Rossi Srl", "2026-11-03")).Programma == "");
                v("firma di un'altra chiave: anche con un nome revocato non dice revocata", Licenza.Controlla(due.Testo(PM, 7, "x", "2026-11-03"), PM, mie, new[] { 7 }).Stato == StatoLicenza.NonValida);

                // ---------- licenza di un altro programma ----------
                var al = c(uno.Testo(WM, 20, "Rossi Srl", "2026-11-03"));
                v("altro programma: non è valida", !al.Valida && al.Stato == StatoLicenza.AltroProgramma);
                v("altro programma: dice quale, dai dati firmati", al.Programma == WM && al.Intestatario == "Rossi Srl");
                v("altro programma: maiuscole diverse non bastano", c(uno.Testo("projectmanager", 21, "Rossi Srl", "2026-11-03")).Stato == StatoLicenza.AltroProgramma);
                v("altro programma: un programma mai sentito", c(uno.Testo("Altro", 22, "Rossi Srl", "2026-11-03")).Stato == StatoLicenza.AltroProgramma);
                v("altro programma: chi chiama senza nome non ottiene niente", Licenza.Controlla(buona, "", mie, nessunRitiro).Stato == StatoLicenza.NonValida && Licenza.Controlla(buona, null, mie, nessunRitiro).Stato == StatoLicenza.NonValida);

                // ---------- numero revocato ----------
                var rev = Licenza.Controlla(buona, PM, mie, new[] { 3, 12 });
                v("revocato: non è valida", !rev.Valida && rev.Stato == StatoLicenza.Revocata);
                v("revocato: dice il numero", rev.Numero == 12);
                v("revocato: gli altri numeri restano buoni", Licenza.Controlla(buona, PM, mie, new[] { 3, 13, 120 }).Valida);
                v("revocato: elenco nullo vuol dire nessun ritiro", Licenza.Controlla(buona, PM, mie, null).Valida);

                // ---------- testo a caso, vuoto, lunghissimo ----------
                string[] aCaso = { null, "", "   ", "\r\n", "ciao", ".", "..", "...", "....", "NSCENT-1", "NSCENT-1.", "NSCENT-1..", "NSCENT-1...", "NSCENT-1.A.A", "NSCENT-1.AAAA.AAAA", "=====", "\0\0\0",
                    "<script>alert(1)</script>", "../../../windows/system32", "Licenza commerciale: Rossi Srl", "ProjectManager\n1\nRossi Srl\n2026-11-03", "NSCENT-1.é.è", "\uD800", "NSCENT-1.\uD800.\uDC00" };
                bool tutti = true;
                foreach (var a in aCaso) if (!nonValida(a)) tutti = false;
                v("testo a caso: " + aCaso.Length + " testi, nessuno valido e nessun errore", tutti);
                var dado = new Random(20261103);
                tutti = true;
                for (int i = 0; i < 300; i++) {
                    var by = new byte[dado.Next(1, 200)]; dado.NextBytes(by);
                    if (!nonValida(Encoding.UTF8.GetString(by)) || !nonValida("NSCENT-1." + Licenza.Base64Url(by) + "." + Licenza.Base64Url(new byte[64]))) tutti = false;
                }
                v("testo a caso: 600 tiri di dadi", tutti);
                v("testo vuoto: non valido", nonValida(""));
                v("lunghissimo: un milione di lettere", nonValida(new string('A', 1000000)));
                v("lunghissimo: dieci milioni di punti", nonValida(new string('.', 10000000)));
                v("lunghissimo: due pezzi enormi", nonValida("NSCENT-1." + new string('A', 1000000) + "." + new string('A', 1000000)));
                v("lunghissimo: una licenza buona dentro tanti spazi", nonValida(new string(' ', 1000000) + buona));
                v("lunghissimo: dati enormi con firma buona", nonValida(uno.Byte(new byte[900])));

                // ---------- elenco chiavi ----------
                v("elenco vuoto: ogni licenza è non valida", Licenza.Controlla(buona, PM, new string[0], nessunRitiro).Stato == StatoLicenza.NonValida);
                v("elenco nullo: non valida, nessun errore", Licenza.Controlla(buona, PM, null, nessunRitiro).Stato == StatoLicenza.NonValida);
                v("elenco del programma: una chiave di prova non vale", Licenza.Controlla(buona, PM).Stato == StatoLicenza.NonValida);
                v("elenco con due chiavi: vale la prima", Licenza.Controlla(buona, PM, new[] { uno.Pubblica, due.Pubblica }, nessunRitiro).Valida);
                v("elenco con due chiavi: vale la seconda", Licenza.Controlla(buona, PM, new[] { due.Pubblica, uno.Pubblica }, nessunRitiro).Valida);
                v("elenco con chiavi rotte: non fermano quella buona", Licenza.Controlla(buona, PM, new[] { "", null, "rotta", Licenza.Base64Url(new byte[64]), Licenza.Base64Url(new byte[10]), uno.Pubblica }, nessunRitiro).Valida);
                v("elenco solo di chiavi rotte: non valida", Licenza.Controlla(buona, PM, new[] { "", null, "rotta", Licenza.Base64Url(new byte[64]) }, nessunRitiro).Stato == StatoLicenza.NonValida);
                v("chiave come testo: rifiuta altro che P-256 pubblica", Licenza.ChiaveComeTesto(new byte[72]) == null && Licenza.ChiaveComeTesto(null) == null);

                // ---------- firma buona su dati sbagliati ----------
                string[] sbagliati = { "", PM, PM + "\n12", PM + "\n12\nRossi Srl", PM + "\n12\nRossi Srl\n2026-11-03\n", PM + "\n12\nRossi Srl\n2026-11-03\naltro",
                    "\n12\nRossi Srl\n2026-11-03", PM + "\n\nRossi Srl\n2026-11-03", PM + "\n12\n\n2026-11-03", PM + "\n12\nRossi Srl\n",
                    PM + "\n0\nRossi Srl\n2026-11-03", PM + "\n007\nRossi Srl\n2026-11-03", PM + "\n-1\nRossi Srl\n2026-11-03", PM + "\n+1\nRossi Srl\n2026-11-03", PM + "\nabc\nRossi Srl\n2026-11-03",
                    PM + "\n1234567890\nRossi Srl\n2026-11-03", PM + "\n\u0663\nRossi Srl\n2026-11-03",
                    PM + "\n12\n" + new string('x', 81) + "\n2026-11-03", PM + "\n12\nRossi\tSrl\n2026-11-03", PM + "\n12\nRossi\rSrl\n2026-11-03", PM + "\n12\nRossi\0Srl\n2026-11-03",
                    PM + "\n12\nRossi\u202ESrl\n2026-11-03", PM + "\n12\nRossi\u200BSrl\n2026-11-03", PM + "\n12\nRossi\u2028Srl\n2026-11-03", PM + "\n12\n Rossi Srl\n2026-11-03", PM + "\n12\nRossi Srl \n2026-11-03",
                    PM + "\n12\nRossi Srl\n2026-13-03", PM + "\n12\nRossi Srl\n2026-02-30", PM + "\n12\nRossi Srl\n03/11/2026", PM + "\n12\nRossi Srl\n2026-1-3", PM + "\n12\nRossi Srl\n2026-11-03 ", PM + "\n12\nRossi Srl\n\u0662\u0660\u0662\u0666-11-03",
                    "Project Manager\n12\nRossi Srl\n2026-11-03", new string('P', 41) + "\n12\nRossi Srl\n2026-11-03", "\uFEFF" + PM + "\n12\nRossi Srl\n2026-11-03" };
                foreach (var s in sbagliati) v("firma buona, dati sbagliati: " + Riga(s), nonValida(uno.Righe(s)));
                v("firma buona, byte che non sono UTF-8", nonValida(uno.Byte(new byte[] { 0xC3, 0x28, 0x0A, 0xFF })));
                v("firma buona, UTF-8 con un carattere mezzo", nonValida(uno.Byte(new byte[] { 0x50, 0x0A, 0x31, 0x0A, 0xED, 0xA0, 0x80, 0x0A, 0x32 })));

                // ---------- comporre ----------
                v("componi: rifiuta i campi sbagliati", new[] {
                    Licenza.Componi("", 1, "x", "2026-11-03"), Licenza.Componi("Project Manager", 1, "x", "2026-11-03"), Licenza.Componi(PM, 0, "x", "2026-11-03"), Licenza.Componi(PM, -3, "x", "2026-11-03"), Licenza.Componi(PM, 1000000000, "x", "2026-11-03"),
                    Licenza.Componi(PM, 1, "", "2026-11-03"), Licenza.Componi(PM, 1, null, "2026-11-03"), Licenza.Componi(PM, 1, new string('x', 81), "2026-11-03"), Licenza.Componi(PM, 1, "a\nb", "2026-11-03"),
                    Licenza.Componi(PM, 1, " x", "2026-11-03"), Licenza.Componi(PM, 1, "\uD800", "2026-11-03"), Licenza.Componi(PM, 1, "x", "03/11/2026"), Licenza.Componi(PM, 1, "x", null) }.All(x => x == null));

                // ---------- il campione: il formato non deve cambiare ----------
                var cam = Licenza.Controlla(CAMPIONE_TESTO, PM, new[] { CAMPIONE_CHIAVE }, nessunRitiro);
                v("campione fatto dal programmino: valido", cam.Valida);
                v("campione: i suoi campi", cam.Programma == PM && cam.Numero == 1 && cam.Intestatario == "Rossi Srl" && cam.Data == "2026-11-03");
                v("campione: non vale con le chiavi del programma", Licenza.Controlla(CAMPIONE_TESTO, PM).Stato == StatoLicenza.NonValida);

                // ---------- il file ----------
                string f = Path.Combine(temp, "dati"), lic = Path.Combine(f, Licenza.FILE);
                Func<EsitoLicenza> carica = () => Licenza.Carica(f, PM, mie, nessunRitiro);
                v("file: cartella che non c'è, assente", carica().Stato == StatoLicenza.Assente);
                v("file: togliere una licenza che non c'è non è un errore", Licenza.Togli(f));
                v("file: un testo vuoto non si scrive", !Licenza.Scrivi(f, "") && !Licenza.Scrivi(f, "   ") && !Licenza.Scrivi(f, null));
                v("file: un testo lunghissimo non si scrive", !Licenza.Scrivi(f, new string('A', 2000)));
                v("file: si scrive", Licenza.Scrivi(f, "  " + buona + "\r\n"));
                v("file: dentro c'è il testo, su una riga", File.ReadAllText(lic) == buona + "\r\n");
                v("file: niente file temporanei in giro", Directory.GetFiles(f).Length == 1);
                var ri = carica();
                v("file: si rilegge valida", ri.Valida && ri.Intestatario == "Rossi Srl");
                v("file: si sostituisce", Licenza.Scrivi(f, uno.Testo(PM, 30, "Bianchi Spa", "2026-11-03")) && carica().Intestatario == "Bianchi Spa");
                v("file: niente copia della vecchia", Directory.GetFiles(f).Length == 1);
                File.WriteAllText(lic, buona, new UTF8Encoding(true));
                v("file: col segno BOM, valida", carica().Valida);
                File.WriteAllText(lic, buona, Encoding.Unicode);
                v("file: salvato in UTF-16 dal Blocco note, valida", carica().Valida);
                File.WriteAllText(lic, "");
                v("file vuoto: non valida", carica().Stato == StatoLicenza.NonValida);
                File.WriteAllText(lic, "questa non è una licenza");
                v("file rotto: non valida", carica().Stato == StatoLicenza.NonValida);
                var sporchi = new byte[4000]; dado.NextBytes(sporchi);
                File.WriteAllBytes(lic, sporchi);
                v("file di byte a caso: non valida", carica().Stato == StatoLicenza.NonValida);
                File.WriteAllText(lic, buona + "\r\n" + buona + "\r\n");
                v("file con due licenze: non valida", carica().Stato == StatoLicenza.NonValida);
                using (var g = new FileStream(lic, FileMode.Create)) g.SetLength(50 * 1024 * 1024);
                v("file di 50 MB: non valida", carica().Stato == StatoLicenza.NonValida);
                File.WriteAllText(lic, buona);
                using (new FileStream(lic, FileMode.Open, FileAccess.Read, FileShare.None))
                    v("file chiuso da un altro programma: illeggibile", carica().Stato == StatoLicenza.Illeggibile);
                v("file: togliere funziona", Licenza.Togli(f) && carica().Stato == StatoLicenza.Assente);
                Directory.CreateDirectory(lic);
                v("una cartella al posto del file: illeggibile", carica().Stato == StatoLicenza.Illeggibile);
                v("una cartella al posto del file: non si può togliere e lo dice", !Licenza.Togli(f));
                v("una cartella al posto del file: non si scrive e lo dice", !Licenza.Scrivi(f, buona));
                v("una cartella al posto del file: nessun file temporaneo resta", !File.Exists(lic + ".nuovo"));
                Directory.Delete(lic);
                v("cartella nulla: illeggibile, nessun errore", Licenza.Carica(null, PM, mie, nessunRitiro).Stato == StatoLicenza.Illeggibile && !Licenza.Scrivi(null, buona) && !Licenza.Togli(null));
                v("cartella con caratteri vietati: illeggibile, nessun errore", Licenza.Carica("a<b>|c", PM, mie, nessunRitiro).Stato != StatoLicenza.Valida && !Licenza.Scrivi("a<b>|c", buona));

                // ---------- le frasi ----------
                var tutti5 = new[] { StatoLicenza.Assente, StatoLicenza.NonValida, StatoLicenza.AltroProgramma, StatoLicenza.Revocata, StatoLicenza.Illeggibile };
                v("scritta: «Licenza personale» per ogni stato non valido", tutti5.All(s => LicenzaQui.Scritta(new EsitoLicenza { Stato = s, Intestatario = "Rossi Srl" }) == "Licenza personale"));
                v("scritta: «Licenza commerciale: Rossi Srl» con una valida", LicenzaQui.Scritta(b) == "Licenza commerciale: Rossi Srl");
                v("frasi: ogni problema si spiega con parole semplici", tutti5.Skip(1).All(s => !string.IsNullOrEmpty(LicenzaQui.Problema(new EsitoLicenza { Stato = s }, "Questa licenza"))));
                v("frasi: niente frase di problema se non c'è problema", LicenzaQui.Problema(b, "x") == null && LicenzaQui.Problema(new EsitoLicenza(), "x") == null);
                v("frasi: dice per quale programma è", LicenzaQui.Problema(al, "Questa licenza").Contains(WM));
                v("frasi: la data all'italiana", LicenzaQui.Dettaglio(b).Contains("03/11/2026") && LicenzaQui.Dettaglio(b).Contains("12"));
                v("frasi: dice dove si compra, senza trattino lungo", LicenzaQui.VENDITA == "La licenza commerciale sarà presto in vendita su www.nscent.art/software; nel frattempo scrivi a info@nscent.art." && !(LicenzaQui.VENDITA + LicenzaQui.LIMITI).Contains("\u2014"));

                // ---------- la finestra della licenza, con le chiavi di prova ----------
                Dialogo.Silenzioso = true;
                LicenzaQui.Chiavi = mie;
                string fin = Path.Combine(temp, "finestra"), licFin = Path.Combine(fin, Licenza.FILE);
                Directory.CreateDirectory(fin);
                var w = new FinestraLicenza(fin) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                w.Show();
                v("finestra: parte da «Licenza personale»", w.TestoStato == "Licenza personale" && !w.BTogli.Enabled);
                w.BUsa.PerformClick();
                v("finestra: casella vuota, chiede di incollare", w.TestoEsito.Contains("Incolla") && !File.Exists(licFin) && !w.Cambiata);
                w.Casella.Text = "testo a caso"; w.BUsa.PerformClick();
                v("finestra: testo a caso, lo dice e non salva", w.TestoEsito.Contains("non è valida") && !File.Exists(licFin) && w.TestoStato == "Licenza personale");
                w.Casella.Text = new string('A', 100000); w.BUsa.PerformClick();
                v("finestra: testo lunghissimo, lo dice e non salva", w.TestoEsito.Contains("non è valida") && !File.Exists(licFin));
                w.Casella.Text = uno.Testo(WM, 40, "Rossi Srl", "2026-11-03"); w.BUsa.PerformClick();
                v("finestra: altro programma, lo dice e non salva", w.TestoEsito.Contains(WM) && !File.Exists(licFin));
                w.Casella.Text = due.Testo(PM, 41, "Rossi Srl", "2026-11-03"); w.BUsa.PerformClick();
                v("finestra: altra chiave, lo dice e non salva", w.TestoEsito.Contains("non è valida") && !File.Exists(licFin));
                w.Casella.Text = "\r\n " + buona + "\r\n"; w.BUsa.PerformClick();
                v("finestra: licenza buona, salvata e mostrata", File.Exists(licFin) && w.TestoStato == "Licenza commerciale: Rossi Srl" && w.Cambiata && w.BTogli.Enabled && w.Casella.Text == "");
                w.Casella.Text = "testo a caso"; w.BUsa.PerformClick();
                v("finestra: un testo sbagliato non butta via quella buona", LicenzaQui.Ora(fin).Valida && w.TestoEsito.Contains("non è valida"));
                w.ChiediTogli = () => false; w.BTogli.PerformClick();
                v("finestra: togliere chiede, e al no resta", File.Exists(licFin) && w.TestoStato.StartsWith("Licenza commerciale"));
                w.ChiediTogli = () => true; w.BTogli.PerformClick();
                v("finestra: al sì la toglie", !File.Exists(licFin) && w.TestoStato == "Licenza personale" && !w.BTogli.Enabled);
                w.Close();
                File.WriteAllText(licFin, uno.Testo(WM, 42, "Rossi Srl", "2026-11-03"));
                w = new FinestraLicenza(fin) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                w.Show();
                v("finestra: una salvata di un altro programma si dice, e si può togliere", w.TestoStato == "Licenza personale" && w.TestoDettaglio.Contains(WM) && w.BTogli.Enabled);
                w.Close();
                File.WriteAllText(licFin, "rotta");
                w = new FinestraLicenza(fin) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                w.Show();
                v("finestra: una salvata rotta si dice, e si può togliere", w.TestoStato == "Licenza personale" && w.TestoDettaglio.Contains("non è valida") && w.BTogli.Enabled);
                w.Close();
                File.Delete(licFin);

                // ---------- il programma parte comunque, con la finestra vera ----------
                Finestra.SoloFoto = true;
                Directory.CreateDirectory(Dati.Cartella);
                string licVera = Path.Combine(Dati.Cartella, Licenza.FILE);
                Func<string> parte = () => {
                    var p = new Finestra { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                    p.Show();
                    for (int i = 0; i < 4; i++) { Application.DoEvents(); Thread.Sleep(10); }
                    string t = p.Visible ? p.TestoLicenza : "non partito";
                    p.Close();
                    return t;
                };
                v("il programma parte: licenza assente, «Licenza personale»", parte() == "Licenza personale");
                File.WriteAllText(licVera, "rotta");
                v("il programma parte: licenza rotta, «Licenza personale»", parte() == "Licenza personale");
                File.WriteAllBytes(licVera, sporchi);
                v("il programma parte: file di byte a caso, «Licenza personale»", parte() == "Licenza personale");
                using (var g = new FileStream(licVera, FileMode.Create)) g.SetLength(50 * 1024 * 1024);
                v("il programma parte: file enorme, «Licenza personale»", parte() == "Licenza personale");
                File.Delete(licVera); Directory.CreateDirectory(licVera);
                v("il programma parte: una cartella al posto del file, «Licenza personale»", parte() == "Licenza personale");
                Directory.Delete(licVera);
                File.WriteAllText(licVera, buona);
                v("il programma parte: licenza buona, il nome nell'intestazione", parte() == "Licenza commerciale: Rossi Srl");
                LicenzaQui.Chiavi = new string[0];
                v("il programma parte: elenco chiavi vuoto, «Licenza personale»", parte() == "Licenza personale");
                File.Delete(licVera);
            }
        } catch (Exception ex) {
            male++; sb.AppendLine("SBAGLIA errore inatteso: " + ex.GetType().Name + ": " + ex.Message);
        } finally {
            LicenzaQui.Chiavi = chiaviVere;
            try { Directory.Delete(temp, true); } catch { }
        }
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 && tot > 0 ? 0 : 1;
    }

    static string Riga(string s) { return new string(s.Select(c => c < ' ' || c > '~' ? '?' : c).ToArray()); }
}
