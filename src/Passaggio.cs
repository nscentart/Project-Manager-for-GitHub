using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

public class Vecchio {
    public Luoghi Programma;
    public string Dati, PrefissoCassaforte, Mutex;
}

public static class Passaggio {
    public static readonly Vecchio Veri = new Vecchio {
        Programma = new Luoghi {
            Cartella = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProjectMonitor"),
            MenuStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Monitor.lnk"),
            Chiave = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectMonitor", NomeExe = "ProjectMonitor.exe" },
        Dati = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMonitor"),
        PrefissoCassaforte = "ProjectMonitor/",
        Mutex = "ProjectMonitor-una-copia" };

    // i file del programma vecchio nella cartella dei dati; il registro delle azioni cambia nome, il resto resta uguale
    static readonly string[][] FILE_DATI = {
        new[] { "archivio.json", "archivio.json" }, new[] { "archivio.json.prima", "archivio.json.prima" }, new[] { "archivio.json.rotto", "archivio.json.rotto" },
        new[] { "ProjectMonitor.log", "ProjectManager.log" }, new[] { "ProjectMonitor.log.vecchio", "ProjectManager.log.vecchio" } };

    // tutti i nomi che ha avuto il programma. La prima versione non scriveva chi l'ha fatto nella voce di App installate: resta solo questo
    static readonly string[] PRODOTTI = { "GitHub Project Monitor", "Project Monitor", "Project Manager" };

    public static bool ExeNostro(string exe) {
        try { return PRODOTTI.Contains(FileVersionInfo.GetVersionInfo(exe).ProductName); } catch { return false; }
    }

    // la copia del programma a questo nome è davvero la nostra? Nel dubbio no: resta dov'è
    public static bool Suo(Luoghi l) {
        try {
            using (var k = Registry.CurrentUser.OpenSubKey(l.Chiave)) {
                string chi = k == null ? "" : Convert.ToString(k.GetValue("Publisher"));
                if (!string.IsNullOrEmpty(chi)) return chi == Installatore.EDITORE && (!File.Exists(l.Exe) || ExeNostro(l.Exe));
            }
            return File.Exists(l.Exe) && ExeNostro(l.Exe);
        } catch { return false; }
    }

    const string RIFIUTATO = "passaggio-no.txt";
    public static bool GiaRifiutato(string datiNuovi) { return File.Exists(Path.Combine(datiNuovi, RIFIUTATO)); }

    public static string Domanda(Vecchio v, string datiNuovi) {
        var cose = new List<string>();
        int n = Cassaforte.Elenco(v.PrefissoCassaforte).Count;
        if (n > 0) cose.Add(Lingua.N("passaggio.accessi", n));
        if (v.Dati != null && !Installatore.Uguale(v.Dati, datiNuovi) && File.Exists(Path.Combine(v.Dati, "archivio.json"))) cose.Add(Lingua.T("passaggio.dati"));
        if (v.Programma != null && Suo(v.Programma)) cose.Add(Lingua.T("passaggio.vecchia_copia_del_programma_che"));
        return Lingua.T("passaggio.questo_computer_ho_trovato_col", "cose", string.Join(", ", cose));
    }

    // si chiede prima di toccare: vale anche per chi usa il programma senza installarlo. Ritorna il numero di passi andati male
    public static int Decidi(Vecchio v, string datiNuovi, string prefissoNuovo, bool togliProgramma, Func<string, bool> chiedi) {
        if (chiedi(Domanda(v, datiNuovi))) return Esegui(v, datiNuovi, prefissoNuovo, togliProgramma);
        try { Directory.CreateDirectory(datiNuovi); File.WriteAllText(Path.Combine(datiNuovi, RIFIUTATO), "scelto il " + DateTime.Now.ToString("dd/MM/yyyy") + "\r\n"); }
        catch (Exception ex) { Dati.Log("passaggio dal vecchio nome, scelta: " + ex.Message); }
        return 0;
    }

    // i posti del vecchio nome per questo avvio: se i dati stanno in GPM_DATI li ha scelti la persona e non si spostano
    public static Vecchio PerOra() {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPM_DATI"))) return Veri;
        return new Vecchio { Programma = Veri.Programma, Dati = null, PrefissoCassaforte = Veri.PrefissoCassaforte, Mutex = Veri.Mutex };
    }

    public static bool Aperto(Vecchio v) {
        try { Mutex m; if (!Mutex.TryOpenExisting(v.Mutex, out m)) return false; m.Close(); return true; } catch { return true; }
    }

    // c'è qualcosa del vecchio nome da portare o da togliere?
    public static bool Serve(Vecchio v, string datiNuovi, string prefissoNuovo) {
        try {
            if (v.Programma != null && Suo(v.Programma)) return true;
            if (Cassaforte.Elenco(v.PrefissoCassaforte).Count > 0) return true;
            return v.Dati != null && !Installatore.Uguale(v.Dati, datiNuovi) && File.Exists(Path.Combine(v.Dati, "archivio.json"));
        } catch (Exception ex) { Dati.Log("passaggio dal vecchio nome, controllo: " + ex.Message); return false; }
    }

    // porta chiavi e dati al nome nuovo (mai sopra quello che c'è già) e, se si sta installando, toglie il programma vecchio.
    // Chi chiama ha già controllato che il vecchio programma non sia aperto. Ritorna il numero di passi andati male.
    public static int Esegui(Vecchio v, string datiNuovi, string prefissoNuovo, bool togliProgramma) {
        int male = 0;
        try {
            foreach (var nome in Cassaforte.Elenco(v.PrefissoCassaforte)) {
                string nuovo = prefissoNuovo + nome.Substring(v.PrefissoCassaforte.Length);
                if (Cassaforte.Leggi(nuovo) != null || Cassaforte.Copia(nome, nuovo)) Cassaforte.Cancella(nome);
                else { male++; Dati.Log("passaggio dal vecchio nome: una chiave d'accesso non si è potuta portare"); }
            }
        } catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, chiavi: " + ex.Message); }
        if (v.Dati != null && Directory.Exists(v.Dati) && !Installatore.Uguale(v.Dati, datiNuovi)) male += PortaDati(v.Dati, datiNuovi);
        if (togliProgramma && v.Programma != null && Suo(v.Programma)) male += TogliProgramma(v.Programma);
        return male;
    }

    static int PortaDati(string da, string a) {
        int male = 0;
        try {
            Directory.CreateDirectory(a);
            foreach (var f in FILE_DATI) {
                string s = Path.Combine(da, f[0]), d = Path.Combine(a, f[1]);
                if (!File.Exists(s)) continue;
                try { if (File.Exists(d)) { if (f[0] != "archivio.json") File.Delete(s); else male++; } else File.Move(s, d); }
                catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, " + f[0] + ": " + ex.Message); }
            }
            try { File.Delete(Path.Combine(da, "senza-installare.txt")); } catch { }   // la scelta riguardava il programma vecchio
            string ps = Path.Combine(da, "pacchetti"), pd = Path.Combine(a, "pacchetti");
            if (Directory.Exists(ps)) {
                Directory.CreateDirectory(pd);
                foreach (var f in Directory.GetFiles(ps, "*.md")) {
                    string d = Path.Combine(pd, Path.GetFileName(f));
                    try { if (File.Exists(d)) File.Delete(f); else File.Move(f, d); } catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, pacchetto: " + ex.Message); }
                }
                try { Directory.Delete(ps, false); } catch (IOException) { }
            }
            try { Directory.Delete(da, false); } catch (IOException) { }   // se c'è altro, resta
        } catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, dati: " + ex.Message); }
        return male;
    }

    // solo i nostri, per nome: la voce in App installate, il collegamento, l'exe e la cartella se resta vuota
    static int TogliProgramma(Luoghi l) {
        int male = 0;
        try { Registry.CurrentUser.DeleteSubKeyTree(l.Chiave, false); } catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, registro: " + ex.Message); }
        try { File.Delete(l.MenuStart); } catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, menu Start: " + ex.Message); }
        try { File.Delete(l.Exe); Directory.Delete(l.Cartella, false); } catch (DirectoryNotFoundException) { } catch (IOException ex) { Dati.Log("passaggio dal vecchio nome, cartella: " + ex.Message); }
        catch (Exception ex) { male++; Dati.Log("passaggio dal vecchio nome, programma: " + ex.Message); }
        return male;
    }
}
