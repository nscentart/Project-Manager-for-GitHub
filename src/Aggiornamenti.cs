// Aggiornamenti.cs (Project Manager 1.1, 03/11/2026): l'avviso di versione nuova, come in WeightMonitor. Una sola domanda a GitHub,
// senza chiave, 20 secondi dopo l'avvio; si spegne dalle Impostazioni. Il programma non scarica e non installa mai niente: nella
// risposta non si prende nessun indirizzo da scaricare, l'impronta si mostra soltanto e nessun file si confronta, e il browser si apre solo
// sulle pagine delle versioni della repo ufficiale, qualunque cosa dica la risposta. Se GitHub non risponde non dà fastidio: lo scrive nel log.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class VersioneNuova {
    public Version Versione;
    public string Etichetta, Note, Pagina;
    public string Impronta;         // SHA-256 in maiuscolo, nulla se non si sa
    public bool ImprontaDiversa;    // il file ne dice una, le note un'altra
}

public static class Aggiornamenti {
    public static string Api = "https://api.github.com/repos/nscentart/Project-Manager-for-GitHub/releases/latest";
    const string INDIRIZZO_BUONO = "https://github.com/nscentart/Project-Manager-for-GitHub/";
    public const string PAGINA_ULTIMA = INDIRIZZO_BUONO + "releases/latest";
    const string ALLEGATO = "ProjectManager.exe";
    public static int AttesaMs = 20000, TempoMs = 10000;
    public static Action<string> ApriBrowser;   // per le prove: niente browser vero

    // solo releases/latest e releases/tag/... della repo ufficiale: niente query, niente frammenti, niente percorsi che risalgono
    public static bool IndirizzoBuono(string url) {
        Uri u;
        return url != null && Regex.IsMatch(url, @"\A" + Regex.Escape(INDIRIZZO_BUONO) + @"releases/(latest|tag/[A-Za-z0-9._-]+)\z")
            && Uri.TryCreate(url, UriKind.Absolute, out u) && u.AbsoluteUri == url;
    }

    public static string DaDigest(object digest) {
        var m = Regex.Match(digest as string ?? "", @"\Asha256:([0-9A-Fa-f]{64})\z");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    public static List<string> DalleNote(string note) {
        return Regex.Matches(note ?? "", @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])").Cast<Match>().Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();
    }

    static Version Versione(string testo) {
        Version v;
        return Version.TryParse(testo.Split('.').Length == 2 ? testo + ".0" : testo, out v) ? v : null;
    }
    public static Version Mia() { return Versione(Programma.VERSIONE); }

    public static VersioneNuova Cerca() { return Cerca(Mia()); }

    public static VersioneNuova Cerca(Version mia) {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // TLS 1.2
        var req = (HttpWebRequest)WebRequest.Create(Api);
        req.Method = "GET";
        req.UserAgent = "ProjectManager/" + Programma.VERSIONE;
        req.Accept = "application/vnd.github+json";
        req.AllowAutoRedirect = false;
        req.Timeout = TempoMs;
        req.ReadWriteTimeout = TempoMs;   // anche un GitHub che si ferma a metà risposta deve avere una fine
        string json;
        try {
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) {
                if ((int)r.StatusCode >= 300) throw new Exception(Lingua.T("agg.github_muto", "msg", (int)r.StatusCode));
                json = sr.ReadToEnd();
            }
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h != null && h.StatusCode == HttpStatusCode.NotFound) throw new Exception(Lingua.T("agg.non_pubblica"));
            throw new Exception(Lingua.T("agg.github_muto", "msg", ex.Message.TrimEnd('.', ' ')));
        } catch (IOException ex) {
            throw new Exception(Lingua.T("agg.github_muto", "msg", ex.Message.TrimEnd('.', ' ')));
        }
        try { return DaRisposta(json, mia); }
        catch (Exception ex) { Dati.Log("aggiornamenti: " + ex.Message); throw new Exception(Lingua.T("github.risposta_github_che_non_capisco")); }
    }

    // nullo: non c'è niente da dire. Una versione senza il nostro file non è una versione che si possa prendere
    public static VersioneNuova DaRisposta(string json, Version mia) {
        var d = (Dictionary<string, object>)new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json);
        string tag = Convert.ToString(d["tag_name"]).TrimStart('v', 'V');
        var v = Versione(tag);
        if (v == null || v <= mia) return null;
        bool exe = false; string digest = null;
        foreach (var o in (IEnumerable)d["assets"]) {
            var a = (Dictionary<string, object>)o;
            if (!string.Equals(Convert.ToString(a["name"]), ALLEGATO, StringComparison.OrdinalIgnoreCase)) continue;
            exe = true;
            if (a.ContainsKey("digest")) digest = DaDigest(a["digest"]);
        }
        if (!exe) return null;
        string pagina = d.ContainsKey("html_url") ? Convert.ToString(d["html_url"]) : "";
        string note = d.ContainsKey("body") ? d["body"] as string ?? "" : "";
        var scritte = DalleNote(note);   // senza il digest, due impronte diverse nelle note non dicono quale sia quella giusta: nessuna, e l'allarme
        return new VersioneNuova {
            Versione = v, Etichetta = tag, Note = note, Pagina = IndirizzoBuono(pagina) ? pagina : PAGINA_ULTIMA,
            Impronta = digest ?? (scritte.Count == 1 ? scritte[0] : null), ImprontaDiversa = digest != null ? scritte.Any(x => x != digest) : scritte.Count > 1
        };
    }

    // spento si guarda prima e dopo l'attesa: chi l'ha spento nel frattempo non deve vedere richieste
    public static Thread AllAvvio(Func<bool> spento, Func<string> saltata, Action<VersioneNuova> avvisa) {
        if (spento()) return null;
        var t = new Thread(() => {
            try {
                Thread.Sleep(AttesaMs);
                if (spento()) return;
                var n = Cerca();
                if (n == null || n.Etichetta == saltata()) return;
                avvisa(n);
            } catch (Exception ex) { Dati.Log("aggiornamenti: " + ex.Message); }
        }) { IsBackground = true };
        t.Start();
        return t;
    }

    // l'indirizzo si ricontrolla qui, l'ultimo passo prima del browser. explorer col percorso pieno, come il resto del programma
    public static void Apri(string pagina) {
        string p = IndirizzoBuono(pagina) ? pagina : PAGINA_ULTIMA;
        if (ApriBrowser != null) { ApriBrowser(p); return; }
        try { Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), p); }
        catch (Exception ex) { Dati.Log("aggiornamenti: " + ex.Message); }
    }

    // le note arrivano in formato GitHub: qui si leggono come testo semplice, e i caratteri invisibili si vedono.
    // via ogni tag, anche uno nascosto dentro un altro; un indirizzo tra < e > non è un tag e resta
    public static string PulisciNote(string note) {
        string t = (note ?? "").Replace("\r\n", "\n").Replace("**", "").Replace("`", ""), prima;
        do {
            prima = t;
            t = Regex.Replace(t, @"<!--.*?-->|<[!?][^<>]*>|</?[A-Za-z][A-Za-z0-9-]*(\s[^<>]*)?/?>", "", RegexOptions.Singleline);
        } while (t != prima);
        var righe = t.Split('\n').Select(r => Dogana.RendiVisibile(r));
        return Regex.Replace(string.Join("\r\n", righe), @"(\r\n){3,}", "\r\n\r\n").Trim();
    }
}

// la finestra dell'avviso: tre tasti, e nessuno scarica niente. L'impronta sta in una riga fissa sopra le note: si legge senza scorrere e si seleziona
public class FinestraAvviso : Dialogo {
    public Bottone BSalta, BDopo, BPagina;
    public TextBox Note, Impronta;
    public Label Frase, Allarme;

    public FinestraAvviso(VersioneNuova n) {
        Func<int, int> S = Aspetto.S;
        Text = Lingua.T("agg.titolo");
        try { Icon = Aspetto.Icona(); } catch { }
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        BackColor = Aspetto.FONDO; ForeColor = Aspetto.TESTO; Font = Aspetto.NORMALE;
        BSalta = new Bottone { Text = Lingua.T("agg.salta"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, DialogResult = DialogResult.Ignore };
        BDopo = new Bottone { Text = Lingua.T("agg.dopo"), Fondo = Aspetto.CAPSULA, Scritta = Aspetto.TESTO, Linea = Aspetto.RIGA, DialogResult = DialogResult.Cancel };
        BPagina = new Bottone { Text = Lingua.T("agg.apri_pagina"), Fondo = Aspetto.AZZURRO, Scritta = Color.White, Linea = Color.Empty, DialogResult = DialogResult.OK };
        int ws = BSalta.GetPreferredSize(Size.Empty).Width, wd = BDopo.GetPreferredSize(Size.Empty).Width, wp = BPagina.GetPreferredSize(Size.Empty).Width;
        string riga = n.Impronta != null ? Lingua.T("agg.impronta", "h", n.Impronta) : Lingua.T("agg.impronta_manca");
        var fissa = new Font("Consolas", Aspetto.NORMALE.Size);
        int lr = TextRenderer.MeasureText(riga, fissa, new Size(9999, 99), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + S(12);
        int m = S(24), larg = Math.Max(Math.Max(S(600), m * 2 + lr), m * 2 + ws + wd + wp + S(8) + S(24)), lu = larg - m * 2;
        Func<Label, int, int, int, int> posa = (l, x, yl, w) => { int h = l.GetPreferredSize(new Size(w, 0)).Height + S(2); l.Bounds = new Rectangle(x, yl, w, h); return h; };
        var segno = new Panel { Bounds = new Rectangle(m, S(20), S(48), S(48)), BackColor = Aspetto.FONDO };
        segno.Paint += (s, e) => Aspetto.Logo(e.Graphics, new Rectangle(0, 0, segno.Width, segno.Height));
        int xt = m + S(60), lt = lu - S(60);
        var titolo = new Label { Text = Lingua.T("agg.uscita", "n", Dogana.RendiVisibile(n.Etichetta)), Font = Aspetto.GRANDE, BackColor = Aspetto.FONDO, UseMnemonic = false };
        int y = S(16) + posa(titolo, xt, S(16), lt) + S(2);
        Frase = new Label { Text = Lingua.T("agg.tu_hai", "v", Programma.VERSIONE), Font = Aspetto.NORMALE, ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, UseMnemonic = false };
        y = Math.Max(S(80), y + posa(Frase, xt, y, lt) + S(8));
        if (n.ImprontaDiversa) {
            Allarme = new Label { Text = Lingua.T("agg.impronta_diversa"), Font = Aspetto.FORTE, ForeColor = Aspetto.INCHIOSTRO, BackColor = Aspetto.ROSSO, Padding = new Padding(S(10), S(6), S(10), S(6)), UseMnemonic = false };
            y += posa(Allarme, m, y, lu) + S(8);
        }
        Impronta = new TextBox { Text = riga, ReadOnly = true, TabStop = false, BorderStyle = BorderStyle.None, Font = fissa, BackColor = Aspetto.FONDO, ForeColor = n.Impronta != null ? Aspetto.TESTO : Aspetto.AMBRA };
        Impronta.Bounds = new Rectangle(m, y, lu, Impronta.PreferredHeight);
        y += Impronta.Height + S(8);
        Note = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = Aggiornamenti.PulisciNote(n.Note), BackColor = Color.FromArgb(24, 25, 29), ForeColor = Aspetto.TESTO, BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(m, y, lu, S(240)) };
        Note.HandleCreated += delegate { try { ListaRepo.SetWindowTheme(Note.Handle, "DarkMode_Explorer", null); } catch { } };
        y += Note.Height + S(10);
        string come = Lingua.T("agg.come");
        int hc = TextRenderer.MeasureText(come, Font, new Size(lu, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + S(4);
        var spiega = new Label { Text = come, ForeColor = Aspetto.MUTO, BackColor = Aspetto.FONDO, Bounds = new Rectangle(m, y, lu, hc), UseMnemonic = false };
        y += hc + S(12);
        BPagina.Bounds = new Rectangle(m + lu - wp, y, wp, S(36));
        BDopo.Bounds = new Rectangle(BPagina.Left - S(8) - wd, y, wd, S(36));
        BSalta.Bounds = new Rectangle(m, y, ws, S(36));
        Controls.AddRange(new Control[] { segno, titolo, Frase, Impronta, Note, spiega, BSalta, BDopo, BPagina });
        if (Allarme != null) Controls.Add(Allarme);
        ClientSize = new Size(larg, y + S(36) + S(18));
        AcceptButton = BPagina; CancelButton = BDopo;
        Load += delegate { Aspetto.BarraScura(this); };
        Shown += delegate { Note.SelectionLength = 0; Impronta.SelectionLength = 0; BPagina.Focus(); };
    }
}
