using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// dove sta tutto: quelli veri o, nelle prove, una cartella temporanea
public class Luoghi {
    public string Cartella, MenuStart, Chiave, NomeExe = "ProjectManager.exe";
    public string Exe { get { return Path.Combine(Cartella, NomeExe); } }
}

public static class Installatore {
    public static readonly Luoghi Veri = new Luoghi {
        Cartella = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProjectManager"),
        MenuStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Manager.lnk"),
        Chiave = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectManager" };

    [DllImport("shell32.dll")] static extern void SHChangeNotify(int evento, uint segni, IntPtr a, IntPtr b);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string file, int da, IntPtr[] grandi, IntPtr[] piccole, uint quante);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public const string EDITORE = "NSCENT.ART di Scentoni Nicolò";

    // quante icone ha il file dentro (non quelle che Windows tira fuori da altri file): zero = l'icona generica
    public static int IconeDentro(string file) { try { return (int)ExtractIconEx(file, -1, null, null, 0); } catch { return 0; } }

    static string Marcatore { get { return Path.Combine(Dati.Cartella, "senza-installare.txt"); } }

    public static bool Uguale(string a, string b) { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
    public static bool Installato() { return Uguale(Application.ExecutablePath, Veri.Exe); }

    // la versione già installata: nulla se non c'è, vuota se è di quelle che non lo dicevano
    public static string VersioneInstallata() {
        try { return File.Exists(Veri.Exe) ? FileVersionInfo.GetVersionInfo(Veri.Exe).FileVersion ?? "" : null; } catch { return null; }
    }

    // si chiede ogni volta che c'è da decidere: mai scelto, oppure questo exe è diverso da quello installato
    public static bool DaChiedere() {
        return !Installato() && (!File.Exists(Marcatore) || VersioneInstallata() != null);
    }

    public static void SegnaSenzaInstallare() {
        try { Directory.CreateDirectory(Dati.Cartella); File.WriteAllText(Marcatore, "scelto il " + DateTime.Now.ToString("dd/MM/yyyy") + "\r\n"); }
        catch (Exception ex) { Dati.Log("scelta senza installare: " + ex.Message); }
    }

    // nullo = fatto, altrimenti il perché in parole semplici
    public static string Installa(Luoghi l) {
        try {
            // un file con lo stesso nome ma di qualcun altro non si sovrascrive
            if (File.Exists(l.Exe) && !Uguale(Application.ExecutablePath, l.Exe) && !Passaggio.ExeNostro(l.Exe)) return Lingua.T("installa.c_gia_altro_programma_stesso", "cartella", l.Cartella);
            Directory.CreateDirectory(l.Cartella);
            // un file nuovo, come fa ogni installazione: la scelta di installare l'ha appena fatta la persona
            if (!Uguale(Application.ExecutablePath, l.Exe)) File.WriteAllBytes(l.Exe, File.ReadAllBytes(Application.ExecutablePath));
            using (var k = Registry.CurrentUser.CreateSubKey(l.Chiave)) {
                k.SetValue("DisplayName", "Project Manager");
                k.SetValue("DisplayVersion", Programma.VERSIONE);
                k.SetValue("DisplayIcon", l.Exe);
                k.SetValue("Publisher", EDITORE);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("InstallLocation", l.Cartella);
                k.SetValue("UninstallString", "\"" + l.Exe + "\" --disinstalla");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(l.Exe).Length / 1024), RegistryValueKind.DWord);
            }
            Collegamento(l);
            // se un aggiornamento cambia l'exe, Windows non deve tenersi in memoria l'icona vecchia (SHCNE_ASSOCCHANGED)
            try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
            return null;
        } catch (IOException ex) {
            Dati.Log("installazione: " + ex.Message);
            return Lingua.T("installa.file_uso_chiudi_project_manager");
        } catch (Exception ex) {
            Dati.Log("installazione: " + ex.Message);
            return ex.Message;
        }
    }

    static void Collegamento(Luoghi l) {
        Directory.CreateDirectory(Path.GetDirectoryName(l.MenuStart));
        var tipo = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(tipo);
        object lnk = tipo.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { l.MenuStart });
        var t = lnk.GetType();
        foreach (var v in new[] { new[] { "TargetPath", l.Exe }, new[] { "WorkingDirectory", l.Cartella }, new[] { "IconLocation", l.Exe + ",0" }, new[] { "Description", "Project Manager" } })
            t.InvokeMember(v[0], System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { v[1] });
        t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null);
    }

    static string MetaCollegamento(string file) {
        var tipo = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(tipo);
        object lnk = tipo.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { file });
        return Convert.ToString(lnk.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, lnk, null));
    }

    static bool ApertoAltrove() {
        try { Mutex m; if (!Mutex.TryOpenExisting(Programma.COPIA_UNICA, out m)) return false; m.Close(); return true; } catch { return true; }
    }

    // da «App installate»: due domande e nessuna cancellazione dei dati senza un sì
    public static int Chiedi() {
        const string T = "Project Manager";
        if (ApertoAltrove()) { MessageBox.Show(Lingua.T("installa.project_manager_aperto_chiudilo_riprov"), T); return 1; }
        if (MessageBox.Show(Lingua.T("installa.togliere_project_manager_questo_comput"),
                            T, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 1;
        bool dati = MessageBox.Show(Lingua.T("installa.cancellare_anche_tuoi_dati_sono", "cartella", Dati.Cartella),
                                    T, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        Togli(Veri, dati, Cassaforte.PREFISSO);
        MessageBox.Show(Lingua.T("installa.fatto", "parte", (dati ? "" : Lingua.T("installa.tuoi_dati_sono_rimasti", "cartella", Dati.Cartella))), T);
        return 0;
    }

    // si cancellano solo i nostri file, per nome: un collegamento o un file messo lì da altri non fanno togliere nient'altro
    public static void Togli(Luoghi l, bool dati, string prefissoCassaforte) {
        try { Registry.CurrentUser.DeleteSubKeyTree(l.Chiave, false); } catch (Exception ex) { Dati.Log("disinstallazione, registro: " + ex.Message); }
        try { File.Delete(l.MenuStart); } catch (Exception ex) { Dati.Log("disinstallazione, menu Start: " + ex.Message); }
        try { Cassaforte.TogliTutto(prefissoCassaforte); } catch (Exception ex) { Dati.Log("disinstallazione, cassaforte: " + ex.Message); }
        if (dati) TogliDati(Dati.Cartella);
        if (Uguale(Application.ExecutablePath, l.Exe)) {
            // l'exe acceso non può cancellare sé stesso: lo fa una riga di cmd fra due secondi. rmdir senza /s: se c'è altro, resta
            string sys = Environment.SystemDirectory;
            Process.Start(new ProcessStartInfo(Path.Combine(sys, "cmd.exe"), "/c \"\"" + Path.Combine(sys, "ping.exe") + "\" -n 3 127.0.0.1 >nul & del /f /q \"" + l.Exe + "\" & rmdir \"" + l.Cartella + "\"\"")
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = sys });
        } else {
            try { File.Delete(l.Exe); Directory.Delete(l.Cartella, false); } catch (Exception ex) { Dati.Log("disinstallazione, cartella: " + ex.Message); }
        }
    }

    static void TogliDati(string c) {
        foreach (var n in new[] { "archivio.json", "archivio.json.prima", "archivio.json.nuovo", "archivio.json.rotto", "ProjectManager.log", "ProjectManager.log.vecchio", "senza-installare.txt", "passaggio-no.txt", "lingua.txt" })
            try { File.Delete(Path.Combine(c, n)); } catch (Exception ex) { Dati.Log("disinstallazione, " + n + ": " + ex.Message); }
        string p = Path.Combine(c, "pacchetti");
        try {
            if (Directory.Exists(p)) {
                // solo quelli col nome che dà il programma: gli altri non sono nostri
                foreach (var f in Directory.GetFiles(p, "*.md")) if (Pacchetto.NomeGenerato(Path.GetFileName(f))) File.Delete(f);
                if (Directory.GetFileSystemEntries(p).Length == 0) Directory.Delete(p, false);
            }
        } catch (Exception ex) { Dati.Log("disinstallazione, pacchetti: " + ex.Message); }
        try { Directory.Delete(c, false); } catch (IOException) { }   // se c'è altro, resta
    }

    // ---------- la prova: tutto in una cartella temporanea e in una chiave di prova, mai nei posti veri ----------

    public const string MARCA_PROVA = "ProjectManager-prova-";

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        string base_ = Path.Combine(Path.GetTempPath(), MARCA_PROVA + Guid.NewGuid().ToString("N"));
        string radice = @"Software\" + MARCA_PROVA + Guid.NewGuid().ToString("N");
        var l = new Luoghi { Cartella = Path.Combine(base_, "programma"), MenuStart = Path.Combine(base_, "menu", "Project Manager.lnk"), Chiave = radice + @"\Uninstall" };
        string dati = Path.Combine(base_, "dati"), pref = MARCA_PROVA + Guid.NewGuid().ToString("N") + "/";
        bool veroPrima = File.Exists(Veri.MenuStart) || File.Exists(Veri.Exe), chiavePrima = Registry.CurrentUser.OpenSubKey(Veri.Chiave) != null;
        var vv = Passaggio.Veri.Programma;
        bool vecchioPrima = File.Exists(vv.MenuStart) || File.Exists(vv.Exe), vecchiaChiavePrima = Registry.CurrentUser.OpenSubKey(vv.Chiave) != null;
        string pv = MARCA_PROVA + "vecchio-" + Guid.NewGuid().ToString("N") + "/", pn = MARCA_PROVA + "nuovo-" + Guid.NewGuid().ToString("N") + "/";
        try {
            v("i posti veri sono dell'utente, senza amministratore", Veri.Cartella.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) && Veri.MenuStart.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Programs)));
            v("installa: nessun errore", Installa(l) == null);
            v("installa: l'exe è nella sua cartella, intero", File.Exists(l.Exe) && new FileInfo(l.Exe).Length == new FileInfo(Application.ExecutablePath).Length);
            v("installa: il menu Start punta all'exe", File.Exists(l.MenuStart) && Uguale(MetaCollegamento(l.MenuStart), l.Exe));
            using (var k = Registry.CurrentUser.OpenSubKey(l.Chiave)) {
                v("installa: c'è la voce in App installate", k != null && Convert.ToString(k.GetValue("DisplayName")) == "Project Manager" && Convert.ToString(k.GetValue("DisplayVersion")) == Programma.VERSIONE);
                v("installa: la voce dice chi l'ha fatto", k != null && Convert.ToString(k.GetValue("Publisher")) == EDITORE);
                v("installa: la voce ha la data di oggi", k != null && Convert.ToString(k.GetValue("InstallDate")) == DateTime.Now.ToString("yyyyMMdd"));
                v("installa: la voce sa come togliere", k != null && Convert.ToString(k.GetValue("UninstallString")) == "\"" + l.Exe + "\" --disinstalla");
            }
            v("l'exe installato contiene una sua icona", IconeDentro(l.Exe) > 0);
            v("reinstalla sopra: nessun errore", Installa(l) == null);
            v("la versione scritta nell'exe è quella giusta", FileVersionInfo.GetVersionInfo(l.Exe).FileVersion == Programma.VERSIONE);

            // togli senza dati: l'exe installato cancella sé stesso, davvero
            Directory.CreateDirectory(Path.Combine(dati, "pacchetti"));
            File.WriteAllText(Path.Combine(dati, "archivio.json"), "{}");
            File.WriteAllText(Path.Combine(dati, "pacchetti", "mario_a_2026-11-03_1200.md"), "x");
            File.WriteAllText(Path.Combine(dati, "pacchetti", "appunti miei.md"), "x");
            File.WriteAllText(Path.Combine(dati, "non-mio.txt"), "x");
            Cassaforte.Scrivi(pref + "pippo", "pippo", "finto");
            int fine = TogliDaFuori(l, dati, pref, "no");
            v("togli senza dati: finisce bene", fine == 0);
            v("togli: l'exe se ne va, e la cartella vuota con lui", Attendi(() => !File.Exists(l.Exe) && !Directory.Exists(l.Cartella)));
            v("togli: il menu Start se ne va", !File.Exists(l.MenuStart));
            v("togli: la voce in App installate se ne va", Registry.CurrentUser.OpenSubKey(l.Chiave) == null);
            v("togli: le chiavi d'accesso se ne vanno", Cassaforte.Elenco(pref).Count == 0);
            v("togli senza dati: i dati restano", File.Exists(Path.Combine(dati, "archivio.json")) && File.Exists(Path.Combine(dati, "pacchetti", "mario_a_2026-11-03_1200.md")));

            // togli coi dati: solo i nostri, per nome
            v("installa di nuovo: nessun errore", Installa(l) == null);
            File.WriteAllText(Path.Combine(l.Cartella, "non-mio.txt"), "x");
            fine = TogliDaFuori(l, dati, pref, "si");
            v("togli coi dati: finisce bene", fine == 0);
            v("togli coi dati: i nostri se ne vanno", Attendi(() => !File.Exists(l.Exe)) && !File.Exists(Path.Combine(dati, "archivio.json")) && !File.Exists(Path.Combine(dati, "pacchetti", "mario_a_2026-11-03_1200.md")));
            v("togli coi dati: un file altrui fra i pacchetti resta", File.Exists(Path.Combine(dati, "pacchetti", "appunti miei.md")));
            string datiSoli = Path.Combine(base_, "dati-soli");
            Directory.CreateDirectory(Path.Combine(datiSoli, "pacchetti"));
            File.WriteAllText(Path.Combine(datiSoli, "pacchetti", "mario_a_2026-11-03_1200.md"), "x");
            TogliDati(datiSoli);
            v("togli coi dati: la cartella dei pacchetti vuota se ne va", !Directory.Exists(datiSoli));
            v("il nome generato si riconosce", Pacchetto.NomeGenerato(Path.GetFileName(Pacchetto.NomeFile("mario/a"))) && !Pacchetto.NomeGenerato("a.md") && !Pacchetto.NomeGenerato("note_2026.md"));
            v("togli coi dati: un file altrui nei dati resta", File.Exists(Path.Combine(dati, "non-mio.txt")));
            v("togli: un file altrui nella cartella del programma resta", File.Exists(Path.Combine(l.Cartella, "non-mio.txt")));

            ProvaPassaggio(v, base_, radice, pv, pn);

            v("la prova non ha toccato i posti veri", (File.Exists(Veri.MenuStart) || File.Exists(Veri.Exe)) == veroPrima && (Registry.CurrentUser.OpenSubKey(Veri.Chiave) != null) == chiavePrima);
            v("la prova non ha toccato i posti del vecchio nome", (File.Exists(vv.MenuStart) || File.Exists(vv.Exe)) == vecchioPrima && (Registry.CurrentUser.OpenSubKey(vv.Chiave) != null) == vecchiaChiavePrima);
        } catch (Exception ex) { v("errore: " + ex.Message, false); }
        finally {
            try { Registry.CurrentUser.DeleteSubKeyTree(radice, false); } catch { }
            try { Cassaforte.TogliTutto(pref); Cassaforte.TogliTutto(pv); Cassaforte.TogliTutto(pn); } catch { }
            try { Directory.Delete(base_, true); } catch { }
        }
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }

    // dal vecchio nome al nuovo, con nomi di prova: chiavi, dati e programma vecchio, mai sopra a quello che c'è già
    static void ProvaPassaggio(Action<string, bool> v, string base_, string radice, string pv, string pn) {
        string guid = Guid.NewGuid().ToString("N"), datiV = Path.Combine(base_, "dati-vecchi"), datiN = Path.Combine(base_, "dati-nuovi");
        var lv = new Luoghi { Cartella = Path.Combine(base_, "programma-vecchio"), MenuStart = Path.Combine(base_, "menu", "Project Monitor vecchio.lnk"), Chiave = radice + @"\UninstallVecchio", NomeExe = "ProjectMonitor.exe" };
        var vec = new Vecchio { Programma = lv, Dati = datiV, PrefissoCassaforte = pv, Mutex = MARCA_PROVA + "mutex-" + guid };
        Func<string, string> L = f => File.Exists(f) ? File.ReadAllText(f) : null;
        Installa(lv);
        Directory.CreateDirectory(Path.Combine(datiV, "pacchetti"));
        File.WriteAllText(Path.Combine(datiV, "archivio.json"), "{\"x\":1}");
        File.WriteAllText(Path.Combine(datiV, "archivio.json.prima"), "prima");
        File.WriteAllText(Path.Combine(datiV, "ProjectMonitor.log"), "riga");
        File.WriteAllText(Path.Combine(datiV, "senza-installare.txt"), "x");
        File.WriteAllText(Path.Combine(datiV, "non-mio.txt"), "x");
        File.WriteAllText(Path.Combine(datiV, "pacchetti", "a.md"), "pacchetto");
        Cassaforte.Scrivi(pv + "pippo", "Pippo", "finto-vecchio");
        Cassaforte.Scrivi(pv + "gia", "gia", "vecchio-valore");
        Cassaforte.Scrivi(pn + "gia", "gia", "nuovo-valore");

        v("passaggio: si accorge che c'è un vecchio nome da portare", Passaggio.Serve(vec, datiN, pn));
        v("passaggio: il vecchio programma spento non risulta aperto", !Passaggio.Aperto(vec));
        using (var aperto = new Mutex(true, vec.Mutex)) v("passaggio: il vecchio programma acceso risulta aperto", Passaggio.Aperto(vec));
        v("passaggio: finisce senza passi sbagliati", Passaggio.Esegui(vec, datiN, pn, true) == 0);
        v("passaggio: la chiave d'accesso è al nome nuovo, uguale", Cassaforte.Leggi(pn + "pippo") == "finto-vecchio");
        v("passaggio: la chiave vecchia se n'è andata", Cassaforte.Elenco(pv).Count == 0);
        v("passaggio: una chiave già al nome nuovo non si sovrascrive", Cassaforte.Leggi(pn + "gia") == "nuovo-valore");
        v("passaggio: archivio e registro sono al nome nuovo", L(Path.Combine(datiN, "archivio.json")) == "{\"x\":1}" && L(Path.Combine(datiN, "archivio.json.prima")) == "prima" && L(Path.Combine(datiN, "ProjectManager.log")) == "riga");
        v("passaggio: i pacchetti sono al nome nuovo", L(Path.Combine(datiN, "pacchetti", "a.md")) == "pacchetto");
        v("passaggio: dai dati vecchi se ne vanno solo i nostri", !File.Exists(Path.Combine(datiV, "archivio.json")) && !File.Exists(Path.Combine(datiV, "senza-installare.txt")) && File.Exists(Path.Combine(datiV, "non-mio.txt")));
        v("passaggio: il programma vecchio se ne va (exe, collegamento, voce)", !File.Exists(lv.Exe) && !Directory.Exists(lv.Cartella) && !File.Exists(lv.MenuStart) && Registry.CurrentUser.OpenSubKey(lv.Chiave) == null);
        v("passaggio: finito, non c'è più niente da portare", !Passaggio.Serve(vec, datiN, pn));
        v("passaggio: rifarlo non cambia niente", Passaggio.Esegui(vec, datiN, pn, true) == 0 && L(Path.Combine(datiN, "archivio.json")) == "{\"x\":1}");

        File.WriteAllText(Path.Combine(datiV, "archivio.json"), "{\"x\":2}");
        v("passaggio: un archivio già al nome nuovo non si sovrascrive", Passaggio.Esegui(vec, datiN, pn, false) == 1 && L(Path.Combine(datiN, "archivio.json")) == "{\"x\":1}" && L(Path.Combine(datiV, "archivio.json")) == "{\"x\":2}");

        // un altro programma che si chiama come il nostro vecchio nome
        string datiA = Path.Combine(base_, "dati-altrui");
        var la = new Luoghi { Cartella = Path.Combine(base_, "programma-altrui"), MenuStart = Path.Combine(base_, "menu", "Altro Monitor.lnk"), Chiave = radice + @"\UninstallAltrui", NomeExe = "ProjectMonitor.exe" };
        var va = new Vecchio { Programma = la, Dati = null, PrefissoCassaforte = pv, Mutex = MARCA_PROVA + "mutex-altrui-" + guid };
        Directory.CreateDirectory(la.Cartella);
        File.WriteAllText(la.Exe, "un altro programma");
        File.WriteAllText(la.MenuStart, "x");
        using (var k = Registry.CurrentUser.CreateSubKey(la.Chiave)) k.SetValue("Publisher", "Altra Ditta");
        v("passaggio: chi ha fatto la voce non siamo noi, non è nostro", !Passaggio.Suo(la));
        v("passaggio: un programma altrui non è da portare", !Passaggio.Serve(va, datiA, pn));
        Cassaforte.Scrivi(pv + "altrui", "Altrui", "finto-altrui");
        v("passaggio: un accesso col vecchio nome sì", Passaggio.Serve(va, datiA, pn));
        string chiesto = null;
        v("passaggio: rispondendo no finisce bene", Passaggio.Decidi(va, datiA, pn, true, d => { chiesto = d; return false; }) == 0);
        v("passaggio: la domanda dice cosa ha trovato e come dire di no", chiesto != null && chiesto.Contains("1 accesso salvato") && chiesto.Contains("altro programma") && chiesto.Contains("rispondi No") && !chiesto.Contains("vecchia copia"));
        v("passaggio: col no non tocca niente", Cassaforte.Leggi(pv + "altrui") == "finto-altrui" && Cassaforte.Leggi(pn + "altrui") == null && File.Exists(la.Exe));
        v("passaggio: col no non lo chiede più", Passaggio.GiaRifiutato(datiA));
        v("passaggio: senza no si può chiedere", !Passaggio.GiaRifiutato(datiN));
        v("passaggio: col sì porta l'accesso", Passaggio.Decidi(va, datiA, pn, true, d => true) == 0 && Cassaforte.Leggi(pn + "altrui") == "finto-altrui");
        v("passaggio: ma il programma altrui resta (exe, collegamento, voce)", File.Exists(la.Exe) && File.Exists(la.MenuStart) && Registry.CurrentUser.OpenSubKey(la.Chiave) != null);
        Registry.CurrentUser.DeleteSubKeyTree(la.Chiave, false);
        v("passaggio: senza voce e con un exe altrui, non è nostro", !Passaggio.Suo(la));
        File.Copy(Application.ExecutablePath, la.Exe, true);
        v("passaggio: senza voce e con l'exe nostro, è nostro", Passaggio.Suo(la));
        File.WriteAllText(la.Exe, "un altro programma");
        using (var k = Registry.CurrentUser.CreateSubKey(la.Chiave)) k.SetValue("Publisher", EDITORE);
        v("passaggio: voce nostra ma exe altrui, non è nostro", !Passaggio.Suo(la));
        var lf = new Luoghi { Cartella = Path.Combine(base_, "programma-omonimo"), MenuStart = Path.Combine(base_, "menu", "Omonimo.lnk"), Chiave = radice + @"\UninstallOmonimo" };
        Directory.CreateDirectory(lf.Cartella);
        File.WriteAllText(lf.Exe, "un altro programma");
        string noInstalla = Installa(lf);
        v("installa: un exe altrui con lo stesso nome non si sovrascrive", noInstalla != null && noInstalla.Contains("non lo sovrascrivo") && L(lf.Exe) == "un altro programma" && Registry.CurrentUser.OpenSubKey(lf.Chiave) == null && !File.Exists(lf.MenuStart));
    }

    static int TogliDaFuori(Luoghi l, string dati, string pref, string coiDati) {
        var psi = new ProcessStartInfo(l.Exe, "--prova-togli \"" + l.Cartella + "\" \"" + l.MenuStart + "\" \"" + l.Chiave + "\" \"" + pref + "\" " + coiDati) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() };
        psi.EnvironmentVariables["GPM_DATI"] = dati;
        using (var p = Process.Start(psi)) { p.WaitForExit(30000); return p.ExitCode; }
    }

    static bool Attendi(Func<bool> fatto) {
        for (int i = 0; i < 40; i++) { if (fatto()) return true; Thread.Sleep(250); }
        return false;
    }
}

// la prima domanda alla prima apertura: parte dalla lingua di Windows e la finestra cambia lingua mentre si sceglie
public class SceltaLingua : Dialogo {
    public string Codice;
    readonly Label domanda;
    readonly Bottone avanti;

    public SceltaLingua(string proposta) {
        using (var g = CreateGraphics()) Aspetto.K = g.DpiX / 96f;
        Func<int, int> S = Aspetto.S;
        Codice = Lingua.Valido(proposta) ? proposta : Lingua.RIPIEGO;
        Lingua.Usa(Codice);
        Text = "Project Manager";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        ClientSize = new Size(S(340), S(310));
        try { Icon = Aspetto.Icona(); } catch { }
        domanda = new Label { Font = Aspetto.GRANDE, Bounds = new Rectangle(S(24), S(16), S(292), S(36)), UseMnemonic = false };
        var elenco = new ListBox { BorderStyle = BorderStyle.None, BackColor = Aspetto.CAPSULA, ForeColor = Aspetto.TESTO, IntegralHeight = false, ItemHeight = S(32),
                                   DrawMode = DrawMode.OwnerDrawFixed, Bounds = new Rectangle(S(24), S(58), S(292), 5 * S(32) + S(2)) };
        foreach (var c in Lingua.CODICI) elenco.Items.Add(Lingua.NomeDi(c));
        elenco.DrawItem += (o, e) => {
            if (e.Index < 0) return;
            bool scelta = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(scelta ? Aspetto.AZZURRO : Aspetto.CAPSULA)) e.Graphics.FillRectangle(b, e.Bounds);
            Aspetto.T(e.Graphics, (string)elenco.Items[e.Index], Aspetto.NORMALE, e.Bounds.X + S(10), e.Bounds.Y, e.Bounds.Width - S(20), e.Bounds.Height, scelta ? Color.White : Aspetto.TESTO);
        };
        avanti = new Bottone { Fondo = Aspetto.AZZURRO, Scritta = Color.White, Linea = Color.Empty, Bounds = new Rectangle(S(24), S(58) + 5 * S(32) + S(14), S(292), S(40)), DialogResult = DialogResult.OK };
        AcceptButton = avanti;
        elenco.SelectedIndex = Array.IndexOf(Lingua.CODICI, Codice);
        elenco.SelectedIndexChanged += delegate {
            if (elenco.SelectedIndex < 0) return;
            Codice = Lingua.CODICI[elenco.SelectedIndex];
            Lingua.Usa(Codice);
            Testi();
        };
        Controls.AddRange(new Control[] { domanda, elenco, avanti });
        Load += delegate { Aspetto.BarraScura(this); };
        Testi();
    }

    void Testi() { domanda.Text = Lingua.T("lingua.scegli"); avanti.Text = Lingua.T("lingua.continua"); avanti.Invalidate(); }
}

// la finestra della prima apertura, che è anche quella di ogni aggiornamento a mano
public class Benvenuto : Dialogo {
    public static string CartellaPerFoto;   // nelle foto, al posto del percorso vero con il nome dell'utente
    public enum Scelta { Annulla, Installa, Senza }
    public Scelta Risposta = Scelta.Annulla;

    public Benvenuto(string gia) {
        using (var g = CreateGraphics()) Aspetto.K = g.DpiX / 96f;
        Func<int, int> S = Aspetto.S;
        Text = "Project Manager " + Programma.VERSIONE;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        try { Icon = Aspetto.Icona(); } catch { }
        string spiega = Lingua.T("installa.installa_copia_programma_mette_nel", "parte", (gia == null ? "" : Lingua.T("installa.questo_computer_c_gia_versione", "parte", (gia.Length > 0 ? gia : Lingua.T("installa.precedente")), "versione", Programma.VERSIONE)), "cartella", CartellaPerFoto ?? Installatore.Veri.Cartella);
        int alt = TextRenderer.MeasureText(spiega, Font, new Size(S(512), 0), TextFormatFlags.WordBreak).Height + S(10);
        var logo = new Panel { Bounds = new Rectangle(S(24), S(22), S(52), S(52)), BackColor = Aspetto.FONDO };
        logo.Paint += (o, e) => Aspetto.Logo(e.Graphics, new Rectangle(0, 0, logo.Width - 1, logo.Height - 1));
        var titolo = new Label { Text = "Project Manager", Font = Aspetto.GRANDE, Bounds = new Rectangle(S(88), S(20), S(448), S(32)), UseMnemonic = false };
        var sotto = new Label { Text = Lingua.T("installa.tue_repo_github_segnalazioni_posto"), ForeColor = Aspetto.MUTO, Bounds = new Rectangle(S(90), S(52), S(446), S(22)), UseMnemonic = false };
        var testo = new Label { Text = spiega, Bounds = new Rectangle(S(24), S(92), S(512), alt), UseMnemonic = false };
        int yb = S(100) + alt, hb = S(38);
        ClientSize = new Size(S(560), yb + hb + S(22));
        var senza = new Bottone { Text = Lingua.T("installa.usa_senza_installare"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, Bounds = new Rectangle(S(24), yb, S(180), hb) };
        var annulla = new Bottone { Text = Lingua.T("comune.annulla"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, Bounds = new Rectangle(S(536) - S(100), yb, S(100), hb), DialogResult = DialogResult.Cancel };
        var installa = new Bottone { Text = Lingua.T("comune.installa"), Fondo = Aspetto.AZZURRO, Scritta = Color.White, Linea = Color.Empty, Bounds = new Rectangle(annulla.Left - S(10) - S(120), yb, S(120), hb) };
        installa.Click += delegate { Risposta = Scelta.Installa; DialogResult = DialogResult.OK; };
        senza.Click += delegate { Risposta = Scelta.Senza; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { logo, titolo, sotto, testo, senza, installa, annulla });
        AcceptButton = installa; CancelButton = annulla;
        Load += delegate { Aspetto.BarraScura(this); };
    }
}
