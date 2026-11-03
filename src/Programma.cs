using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Project Manager")]
[assembly: AssemblyProduct("Project Manager")]
[assembly: AssemblyFileVersion(Programma.VERSIONE)]
[assembly: AssemblyInformationalVersion(Programma.VERSIONE)]

public static class Programma {
    public const string VERSIONE = "1.1";
    public const string COPIA_UNICA = "ProjectManager-una-copia";

    // un file dentro l'exe: i testi delle lingue
    public static Stream Risorsa(string nome) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(nome); }

    [STAThread]
    static int Main(string[] args) {
        // --lingua xx, con qualunque altro comando: vale solo per questa volta e non si scrive. Le prove senza --lingua girano in italiano
        string lingua = null;
        int qui = Array.IndexOf(args, "--lingua");
        if (qui >= 0 && qui + 1 < args.Length) {
            if (!Lingua.Valido(args[qui + 1])) return 2;
            lingua = args[qui + 1];
            Lingua.Usa(lingua);
            args = args.Where((x, i) => i != qui && i != qui + 1).ToArray();
        }
        if (args.Length == 3 && args[0] == "--prova-lingue") return ProvaLingue.Tabelle(args[1], args[2]);
        if (args.Length == 3 && args[0] == "--prova-dogana") return ProvaDogana(args[1], args[2]);
        if (args.Length == 2 && args[0] == "--prova-organizzazione") return Organizzazione.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-panoramica") return PaginaPanoramica.Prova(args[1]);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && args[0] == "--prova-accessi") return Accessi.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-scrittura") return ProvaScrittura.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-installazione") return Installatore.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-guidata") return Guidata.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-licenza") return ProvaLicenza.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-numeri") return ProvaNumeri.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-sparite") return ProvaSparite.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--togli-sparite-copia") return ProvaSparite.SuCopia(args[1]);
        if (args.Length == 2 && args[0] == "--prova-avviso") return ProvaAvviso.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-riprova") return ProvaRiprova.Prova(args[1]);
        if (args.Length == 2 && args[0] == "--prova-lingue-vive") return ProvaLingue.Vive(args[1]);
        if (args.Length == 2 && args[0] == "--prova-lingua-comando") return ProvaLingue.DaComando(args[1], lingua);
        if (args.Length == 2 && args[0] == "--foto") return Foto.Tutte(args[1], null);
        if (args.Length == 3 && args[0] == "--foto") return Lingua.Valido(args[2]) ? Foto.Tutte(args[1], args[2]) : 2;
        if (args.Length == 3 && args[0] == "--prova-repo") return ProvaRepo(args[1], args[2]);
        if (args.Length == 6 && args[0] == "--prova-togli") return ProvaTogli(args);
        bool datiQui = File.Exists(Path.Combine(Dati.Cartella, "archivio.json"));
        Lingua.Inizia(lingua, Dati.Cartella, datiQui);
        if (args.Length == 1 && args[0] == "--disinstalla") return Installatore.Chiedi();
        bool primo, senzaChiedere = args.Length == 1 && args[0] == "--portatile";
        string dopo = null;
        using (var m = new Mutex(true, COPIA_UNICA, out primo)) {
            if (!primo) { MessageBox.Show(Lingua.T("programma.project_manager_gia_aperto_installare"), "Project Manager"); return 0; }
            Application.ThreadException += (s, e) => { Dati.Log("errore: " + e.Exception); MessageBox.Show(e.Exception.Message, "Project Manager"); };
            var vecchio = Passaggio.PerOra();
            bool passaggio = !Passaggio.GiaRifiutato(Dati.Cartella) && Passaggio.Serve(vecchio, Dati.Cartella, Cassaforte.PREFISSO);
            if (passaggio && Passaggio.Aperto(vecchio)) { MessageBox.Show(Lingua.T("programma.project_monitor_vecchio_nome_ancora"), "Project Manager"); return 0; }
            // i dati che arrivano dal vecchio nome sono di una versione solo italiana. Chi non ha niente sceglie la lingua, prima di tutto il resto
            if (lingua == null && passaggio && !datiQui && Lingua.DaScegliere(Dati.Cartella, false)) Lingua.Usa(Lingua.PARTENZA);
            if (lingua == null && Lingua.DaScegliere(Dati.Cartella, datiQui || passaggio)) {
                using (var q = new SceltaLingua(Lingua.Codice)) {
                    if (q.ShowDialog() != DialogResult.OK) return 0;
                    Lingua.Scegli(q.Codice, Dati.Cartella);
                }
            }
            if (!senzaChiedere && Installatore.DaChiedere()) {
                var scelta = Benvenuto.Scelta.Annulla;
                using (var b = new Benvenuto(Installatore.VersioneInstallata())) { b.ShowDialog(); scelta = b.Risposta; }
                if (scelta == Benvenuto.Scelta.Annulla) return 0;
                if (scelta == Benvenuto.Scelta.Senza) Installatore.SegnaSenzaInstallare();
                else {
                    string errore = Installatore.Installa(Installatore.Veri);
                    if (errore != null) { MessageBox.Show(Lingua.T("programma.non_sono_riuscito_installarlo", "errore", errore), "Project Manager", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
                    dopo = Installatore.Veri.Exe;
                }
            }
            if (passaggio) Passaggio.Decidi(vecchio, Dati.Cartella, Cassaforte.PREFISSO, dopo != null, domanda => MessageBox.Show(domanda, "Project Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
            // la lingua cambiata nelle Impostazioni vale subito: la finestra si richiude e si riapre con i testi nuovi
            if (dopo == null) do { Finestra.RiapriPerLingua = false; Application.Run(new Finestra()); } while (Finestra.RiapriPerLingua);
        }
        // installato: riparte la copia installata, a Mutex lasciato
        if (dopo != null) try { Process.Start(new ProcessStartInfo(dopo) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(dopo) }); } catch (Exception ex) { Dati.Log("avvio dopo l'installazione: " + ex.Message); }
        return 0;
    }

    // usata solo da --prova-installazione: la chiave e le chiavi d'accesso devono avere il nome di prova, o non si tocca niente
    static int ProvaTogli(string[] a) {
        if (!a[3].Contains(Installatore.MARCA_PROVA) || !a[4].StartsWith(Installatore.MARCA_PROVA)) return 2;
        Installatore.Togli(new Luoghi { Cartella = a[1], MenuStart = a[2], Chiave = a[3] }, a[5] == "si", a[4]);
        return 0;
    }

    // la repo di questo programma deve passare dai suoi stessi controlli: si dice solo dove, mai il valore
    static int ProvaRepo(string cartella, string esito) {
        var k = Scrittura.Prepara(cartella, null);
        var sb = new StringBuilder();
        foreach (var r in k.Riscontri) sb.AppendLine("SBAGLIA " + r);
        foreach (var r in k.Rifiutati) sb.AppendLine("SBAGLIA " + r);
        // explorer si chiama solo col percorso pieno
        string senzaPercorso = "Process.Start(" + "\"explorer";
        var nudi = Directory.GetFiles(Path.Combine(cartella, "src"), "*.cs").Where(x => File.ReadAllText(x).Contains(senzaPercorso)).ToList();
        foreach (var x in nudi) sb.AppendLine("SBAGLIA " + Path.GetFileName(x) + ": chiama explorer senza il percorso pieno");
        sb.AppendLine(k.File.Count + " file controllati, " + (k.Riscontri.Count + k.Rifiutati.Count + nudi.Count) + " fermati");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return k.Bloccata || k.File.Count == 0 || nudi.Count > 0 ? 1 : 0;
    }

    // ogni file .txt: la prima riga è «ATTESO: pulito» o «ATTESO: sospetto», il resto è il testo (titolo = nome del file)
    static int ProvaDogana(string cartella, string esito) {
        var sb = new StringBuilder();
        int sbagliati = 0, tot = 0;
        var fidati = Dati.FIDATI;
        foreach (var f in Directory.GetFiles(cartella, "*.txt").OrderBy(x => x)) {
            var righe = File.ReadAllText(f, Encoding.UTF8).Replace("\r\n", "\n").Split(new[] { '\n' }, 2);
            // «ATTESO: sospetto» oppure «ATTESO: sospetto <codice>», quando deve scattare proprio quella regola
            var prima = righe[0].Trim().Split(' ');
            bool atteso = prima.Length > 1 && prima[1] == "sospetto";
            string regola = prima.Length > 2 ? prima[2] : null;
            var motivi = Dogana.Esamina(regola != null ? "" : Path.GetFileNameWithoutExtension(f), righe.Length > 1 ? righe[1] : "", fidati);
            bool ok = (motivi.Count > 0) == atteso && (regola == null || motivi.Any(x => x.Codice == regola));
            tot++; if (!ok) sbagliati++;
            sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + Path.GetFileName(f) + " (atteso " + (atteso ? "sospetto" : "pulito") + ")");
            foreach (var m in motivi) sb.AppendLine("          " + m.Codice + ": " + m);
        }
        sb.AppendLine(tot - sbagliati + " su " + tot + " giusti");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return sbagliati == 0 && tot > 0 ? 0 : 1;   // nessun caso = rosso: una prova vuota non prova niente
    }
}
