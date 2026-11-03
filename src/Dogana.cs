using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class Motivo {
    public string Codice, Spiegazione, Estratto;
    public override string ToString() { return Spiegazione + (string.IsNullOrEmpty(Estratto) ? "" : ": «" + Estratto + "»"); }
}

// quello che il controllo prima di una scrittura ha trovato: dice dove e che cosa, mai il valore
public class Riscontro {
    public string File, Cosa;
    public int Riga;
    public override string ToString() { return File + (Riga > 0 ? Lingua.T("dogana.riga", "riga", Riga) : "") + ": " + Cosa; }
}

public static class Dogana {
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const int LUNGO = 20000;   // oltre non si passa alle regole: si legge a mano

    // [codice, chiave della spiegazione, espressione] sul testo ripulito (minuscolo, senza invisibili, NFKC, spazi singoli)
    static readonly string[][] FRASI = {
        new[] { "ordini_ai", "dogana.frase.ordini_ai",
                @"\b(ignore|disregard|forget|override|bypass|skip)\b.{0,40}\b(instructions?|prompts?|rules|guidelines|previous|above|system|guardrails?)\b" },
        new[] { "ordini_ai", "dogana.frase.ordini_ai",
                @"\b(ignora|ignorate|ignorare|dimentica|dimenticate|dimenticare|trascura|scavalca|salta)\b.{0,40}\b(istruzion\w*|comand\w*|regol\w*|prompt|precedent\w*|sopra|sistema|limiti)\b" },
        new[] { "ruolo_ai", "dogana.frase.ruolo_ai",
                @"\b(you are now|from now on,? you|act as|pretend (to be|you are)|roleplay as|sei ora|adesso sei|da ora in poi|fingi di essere|comportati come|fai finta di essere)\b" },
        new[] { "marcatori_ai", "dogana.frase.marcatori_ai",
                @"(<\|?(system|im_start|im_end|assistant|user|endoftext)\|?>|\[/?inst\]|\bsystem prompt\b|###\s*(instruction|system|response)|^\s*(assistant|system|human|user)\s*:|</?(system|instructions?|prompt|tool_call|function_call)>)" },
        new[] { "nomina_ai", "dogana.frase.nomina_ai",
                @"\b(claude|anthropic|chatgpt|openai|gpt-?\d\w*|copilot|gemini|llama|mistral|llm|ai agent|agente ai|language model|modello linguistico|assistente (ai|virtuale)|ai assistant|coding agent|cursor ai)\b" },
        new[] { "ordini", "dogana.frase.ordini",
                @"\b(run|execute|eval|paste and run|esegui|eseguite|eseguire|lancia|lanciate|avvia|incolla ed esegui)\b.{0,30}\b(command|commands|comando|comandi|script|scripts|code|codice|following|seguente|seguenti|terminal|terminale|powershell|prompt)\b" },
        new[] { "esfiltrazione", "dogana.frase.esfiltrazione",
                @"\b(send|post|upload|forward|exfiltrate|leak|transmit|invia|inviare|inviate|manda|mandare|mandate|carica|caricare|inoltra|inoltrare|trasmetti)\b.{0,60}\b(to|a|al|alla|su|verso|all')\b.{0,40}(https?://|server|webhook|endpoint|e-?mail|indirizzo|url|discord|telegram|pastebin)" },
        new[] { "segreti", "dogana.frase.segreti",
                @"\b(api[_ -]?keys?|access[_ -]?tokens?|tokens? (di|del|of)|secrets?|password|passwd|credentials?|credenziali|\.env\b|ssh[_ -]?keys?|id_rsa|private key|chiave privata|chiavi private|cookies?|session id|wallet|seed phrase)\b" },
        new[] { "segretezza", "dogana.frase.segretezza",
                @"\b(do not tell|don't tell|dont tell|don't mention|do not mention|without telling|secretly|silently|non dire|non dirlo|non dirglielo|senza dirlo|di nascosto|segretamente|non menzionare|senza avvisare)\b" },
        new[] { "comandi", "dogana.frase.comandi",
                @"(\b(curl|wget|powershell|pwsh|cmd\.exe|cmd /c|bash -c|sh -c|invoke-webrequest|invoke-restmethod|invoke-expression|iwr|irm|iex|certutil|bitsadmin|mshta|rundll32|regsvr32|start-process|set-executionpolicy|reg add|schtasks|npm (i|install)|npx|pip install|git clone|chmod \+x|sudo|rm -rf|del /[fsq]|format [a-z]:|base64 -d|frombase64string|downloadstring|downloadfile)\b|\|\s*(sh|bash|iex|powershell)\b)" },
        new[] { "menzione_bot", "dogana.frase.menzione_bot",
                @"(^|[\s(])@(claude|copilot|github-actions|dependabot|chatgpt|openai|gemini|codex|devin|cursor|sweep|coderabbitai|codeant|qodo|sourcery|ellipsis|greptile)\b" },
    };

    // le stesse frasi in francese, spagnolo e tedesco. Stessi codici e stesse spiegazioni. In queste lingue l'ordine e la terza persona
    // si scrivono spesso uguali («ignore», «ignora»): «le programme ignore les règles» è un bug, non un ordine, e i soggetti qui sotto lo escludono.
    // Non si cercano parole che stanno nelle segnalazioni normali («sans prévenir», «sin avisar», «Sie sind jetzt angemeldet»)
    const string NON_FR = @"(?<!\b(programme|logiciel|application|appli|outil|script|syst[eè]me|bouton|il|elle|on|qui|ça|cela|je)\s)(?<!\bj')";
    const string NON_ES = @"(?<!\b(programa|aplicaci[oó]n|app|herramienta|script|sistema|bot[oó]n|[eé]l|ella|que|se|yo)\s)";
    const string NON_DE = @"(?<!\b(programm|app|anwendung|tool|skript|script|system|es|er|ich)\s)";
    static readonly string[][] FRASI_ALTRE = {
        new[] { "ordini_ai", "dogana.frase.ordini_ai",
                NON_FR + @"\b(ignore[zr]?|oublie[zr]?|ne tiens? pas compte|ne tenez pas compte|contourne[zr]?|outrepasse[zr]?)\b.{0,40}\b(instructions?|consignes?|directives?|r[eè]gles|prompts?|ce qui pr[eé]c[eè]de)\b" },
        new[] { "ordini_ai", "dogana.frase.ordini_ai",
                NON_ES + @"\b(ignora|ignore|ignorad|ignoren|ignorar|olvida|olvide|olvidad|olviden|olvidar|omite|omita|omitid|omitir|s[aá]ltate|haz caso omiso)\b.{0,40}\b(instrucci[oó]n\w*|[oó]rdenes|reglas|normas|indicaciones|directrices|prompts?|todo lo anterior)\b" },
        new[] { "ordini_ai", "dogana.frase.ordini_ai",
                NON_DE + @"\b(ignoriere|ignorier|ignorieren|ignoriert (alle|jede|s[aä]mtliche)|vergiss|vergesst|vergessen|missachte|missachten|[uü]bergehe|[uü]bergehen|umgehe|umgehen)\b.{0,40}\b(anweisung\w*|instruktion\w*|anordnung\w*|regeln|vorgaben|richtlinien|prompts?)\b" },
        new[] { "ordini_ai", "dogana.frase.ordini_ai",
                @"\b(anweisung\w*|instruktion\w*|vorgaben|richtlinien)\b.{0,40}\b(ignorieren|vergessen|missachten|[uü]bergehen|umgehen)\b" },
        new[] { "ruolo_ai", "dogana.frase.ruolo_ai",
                @"\b((tu es|vous [eê]tes) maintenant (un|une|le|la|mon|ma|dan)|d[eé]sormais,? (tu|vous)|[aà] partir de maintenant,? (tu|vous)|agis comme|agissez comme|fais semblant d'[eê]tre|faites semblant d'[eê]tre|pr[eé]tends [eê]tre|joue le r[oô]le|jouez le r[oô]le)\b" },
        new[] { "ruolo_ai", "dogana.frase.ruolo_ai",
                @"\b(ahora eres|ahora es usted|desde ahora eres|a partir de ahora,? (eres|act[uú]a|responde)|act[uú]a como|actuad como|act[uú]e como|finge ser|fingid ser|hazte pasar por|comp[oó]rtate como)\b" },
        new[] { "ruolo_ai", "dogana.frase.ruolo_ai",
                @"\b((du bist|sie sind) (jetzt|nun) (ein|eine|der|die|mein|meine|dan)|von nun an (bist|sollst|antworte\w*)|ab jetzt bist|ab sofort bist|verhalte dich (wie|als)|tu so,? als|gib dich als|agiere als|spiele die rolle|handle als)\b" },
        new[] { "marcatori_ai", "dogana.frase.marcatori_ai",
                @"\b(prompt (du )?syst[eè]me|prompt de sistema|prompt del sistema|prompt di sistema|system-?prompt\w*|systemanweisung\w*)\b" },
        new[] { "nomina_ai", "dogana.frase.nomina_ai",
                @"\b(agent (d')?ia|assistant (d')?ia|assistant virtuel|mod[eè]le de langage|agente (de )?ia|asistente (de )?ia|asistente virtual|modelo de lenguaje|ki-agent\w*|ki-assistent\w*|sprachmodell\w*)\b" },
        new[] { "ordini", "dogana.frase.ordini",
                NON_FR + @"\b(ex[eé]cutez?|lancez?|collez? et ex[eé]cutez?)\b.{0,30}\b(commandes?|scripts?|code|suivant\w*|terminal|powershell|invite de commandes)\b" },
        new[] { "ordini", "dogana.frase.ordini",
                NON_ES + @"\b(ejecuta|ejecute|ejecutad|ejecuten|lanza|lanzad|pega y ejecuta)\b.{0,30}\b(comandos?|[oó]rdenes|scripts?|c[oó]digo|siguientes?|terminal|powershell)\b" },
        new[] { "ordini", "dogana.frase.ordini",
                NON_DE + @"\b(f[uü]hre|f[uü]hren sie|starte|starten sie)\b.{0,30}\b(befehl\w*|kommando\w*|skript\w*|script\w*|code|folgend\w*|terminal|powershell)\b" },
        new[] { "ordini", "dogana.frase.ordini",
                @"\b(befehl\w*|kommando\w*|skript\w*|script\w*)\b(?:(?!\bnicht\b).){0,30}\bausf[uü]hren\b" },
        new[] { "esfiltrazione", "dogana.frase.esfiltrazione",
                NON_FR + @"\b(envoie|envoyez|transmets|transmettez|t[eé]l[eé]verse[z]?|publie[z]?|transf[eè]re[z]?)\b.{0,60}\b([aà]|au|aux|sur|vers)\b.{0,40}(https?://|serveur|webhook|endpoint|e-?mail|courriel|adresse|url|discord|telegram|pastebin)" },
        new[] { "esfiltrazione", "dogana.frase.esfiltrazione",
                NON_ES + @"\b(env[ií]a|env[ií]e|enviad|manda|mande|mandad|sube|suba|subid|reenv[ií]a|transmite)\b.{0,60}\b(a|al|en|hacia)\b.{0,40}(https?://|servidor|webhook|endpoint|e-?mail|correo|direcci[oó]n|url|discord|telegram|pastebin)" },
        new[] { "esfiltrazione", "dogana.frase.esfiltrazione",
                NON_DE + @"\b(sende|senden sie|schicke?|schicken sie|lade|laden sie|[uü]bertrage|[uü]bertragen sie|leite|leiten sie)\b.{0,60}\b(an|auf|zu|nach)\b.{0,40}(https?://|server|webhook|endpoint|e-?mail|adresse|url|discord|telegram|pastebin)" },
        new[] { "segreti", "dogana.frase.segreti",
                @"(\b(cl[eé]s? (d')?api|jetons? (d'acc[eè]s|api)|mots? de passe|identifiants|cl[eé]s? priv[eé]es?|cl[eé]s? ssh|phrase de r[eé]cup[eé]ration)\b|\b(claves? (de )?api|contrase[nñ]as?|credenciales|claves? privadas?|claves? ssh|tokens? de acceso|frase semilla)\b|\b(api-schl[uü]ssel\w*|passw[oö]rt\w*|kennw[oö]rt\w*|zugangsdaten|anmeldedaten|privater? schl[uü]ssel\w*|geheimer? schl[uü]ssel\w*|ssh-schl[uü]ssel\w*|zugriffstoken\w*)\b)" },
        new[] { "segretezza", "dogana.frase.segretezza",
                @"\b(ne (le )?dis (rien|pas)|ne dites (rien|pas)|sans le dire|en secret|secr[eè]tement|ne mentionne pas|ne mentionnez pas|no (le |se lo |lo )?digas|sin decir(lo|le|selo)|en secreto|secretamente|a escondidas|no menciones|no mencione|sag (es )?(niemandem|keinem|nichts)|sage (es )?(niemandem|keinem|nichts)|sagen sie (es )?(niemandem|keinem|nichts)|ohne (es )?zu (sagen|erw[aä]hnen)|heimlich|im geheimen|erw[aä]hne (das |es )?nicht|erw[aä]hnen sie (das |es )?nicht)\b" },
    };

    // le stesse frasi scritte attaccate, per «i g n o r e» o «ign.ore»: si cercano nel testo senza niente fra le lettere
    static readonly string[] ATTACCATE = {
        "ignorepreviousinstructions", "ignoreallpreviousinstructions", "ignoretheaboveinstructions", "ignoreyourinstructions",
        "disregardpreviousinstructions", "forgetyourinstructions", "systemprompt", "youarenow",
        "ignoraleistruzioni", "ignoratutteleistruzioni", "dimenticaleistruzioni", "ignoraiprecedenti", "seiora",
        "ignorelesinstructions", "ignorezlesinstructions", "ignoreztouteslesinstructions", "ignoretouteslesinstructions", "oublielesinstructions", "oubliezlesinstructions", "oublieztouteslesinstructions", "oublietouteslesinstructions", "tuesmaintenant",
        "ignoralasinstrucciones", "ignoratodaslasinstrucciones", "ignorelasinstrucciones", "olvidalasinstrucciones", "olvidatodaslasinstrucciones", "ahoraeres",
        "ignorierealleanweisungen", "ignorierediebisherigenanweisungen", "ignorierealleneingaben", "vergissalleanweisungen", "vergissdeineanweisungen", "dubistjetzt" };

    static readonly HashSet<string> TAG_OK = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "b", "i", "em", "strong", "br", "code", "pre", "kbd", "sub", "sup", "p", "ul", "ol", "li", "blockquote",
        "h1", "h2", "h3", "h4", "h5", "h6", "table", "tr", "td", "th", "thead", "tbody", "hr", "s", "del", "ins", "img", "a",
        "utente", "computer" };  // gli ultimi due sono i segnaposto dei rapporti di WeightMonitor

    static readonly HashSet<string> LINGUAGGI_ESEGUIBILI = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "powershell", "ps1", "ps", "pwsh", "bash", "sh", "shell", "zsh", "cmd", "bat", "batch", "console", "terminal",
        "python", "py", "js", "javascript", "node", "vbs", "vbscript", "applescript", "csharp", "cs", "c#", "ruby", "perl", "php" };

    public static List<Motivo> Esamina(string titolo, string testo, IEnumerable<string> dominiFidati) {
        var motivi = new List<Motivo>();
        string tutto = (titolo ?? "") + "\n" + (testo ?? "");
        // le regole su un testo enorme e fatto apposta possono bloccare il programma: si salta tutto e va in quarantena
        if (tutto.Length > LUNGO) { Aggiungi(motivi, "lungo", Lingua.T("dogana.testo_troppo_lungo_caratteri_non", "tutto", tutto.Length), ""); return motivi; }

        Invisibili(tutto, motivi);
        Omoglifi(tutto, motivi);

        string pulito = Ripulisci(tutto);
        foreach (var f in FRASI.Concat(FRASI_ALTRE)) {
            var m = Regex.Match(pulito, f[2], O | RegexOptions.Multiline);
            if (m.Success) Aggiungi(motivi, f[0], Lingua.T(f[1]), Intorno(pulito, m.Index, m.Length));
        }
        string attaccato = new string(pulito.Where(char.IsLetter).ToArray());
        foreach (var a in ATTACCATE)
            if (attaccato.Contains(a) && !motivi.Any(x => x.Codice == "ordini_ai" || x.Codice == "ruolo_ai" || x.Codice == "marcatori_ai"))
                Aggiungi(motivi, "ordini_ai", Lingua.T("dogana.frase_ai_scritta_spezzata_mascherata"), a);
        var spaziate = Regex.Match(pulito, @"(?<![\w])(\w[ .\-_*]){8,}\w(?![\w])");
        if (spaziate.Success) Aggiungi(motivi, "spaziate", Lingua.T("dogana.lettere_separate_non_farsi_riconoscere"), Corto(spaziate.Value));

        string fuori = FuoriDalCodice(testo ?? "");
        Html(fuori, motivi);
        Markdown(fuori, motivi);
        BlocchiDiCodice(testo ?? "", motivi);
        Link(tutto, dominiFidati, motivi);
        Codificati(tutto, motivi);
        return motivi;
    }

    static void Aggiungi(List<Motivo> motivi, string codice, string spiegazione, string estratto) {
        if (motivi.Any(m => m.Codice == codice && m.Spiegazione == spiegazione)) return;
        motivi.Add(new Motivo { Codice = codice, Spiegazione = spiegazione, Estratto = RendiVisibile(Corto(estratto)) });
    }

    static string Corto(string s) {
        s = Regex.Replace(s ?? "", @"\s+", " ").Trim();
        return s.Length > 120 ? s.Substring(0, 117) + "..." : s;
    }

    static string Intorno(string s, int i, int n) {
        int a = Math.Max(0, i - 30), b = Math.Min(s.Length, i + n + 30);
        return (a > 0 ? "..." : "") + s.Substring(a, b - a) + (b < s.Length ? "..." : "");
    }

    // ---------- caratteri invisibili ----------

    static IEnumerable<int> Punti(string s) {
        for (int i = 0; i < s.Length; i++) {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
            else yield return s[i];
        }
    }

    static bool Emoji(int c) { return c >= 0x1F000 || (c >= 0x2190 && c <= 0x2BFF) || c == 0x00A9 || c == 0x00AE || (c >= 0x2000 && c <= 0x2BFF); }

    // un carattere che non si vede ma c'è; i variatori e il «giunto» dopo un'emoji sono normali e passano
    static bool Nascosto(int c, int prima, int dopo) {
        if (c == '\n' || c == '\r' || c == '\t') return false;
        if ((c == 0xFE0F || c == 0xFE0E) && Emoji(prima)) return false;
        if (c == 0x200D && Emoji(prima) && Emoji(dopo)) return false;
        if (c >= 0xE0000 && c <= 0xE007F) return true;     // caratteri «tag»: testo ASCII invisibile
        if ((c >= 0xFE00 && c <= 0xFE0F) || (c >= 0xE0100 && c <= 0xE01EF)) return true;  // variatori fuori posto
        if (c == 0x3164 || c == 0x115F || c == 0x1160 || c == 0xFFA0 || c == 0x2800 || c == 0x180E) return true;  // riempitivi vuoti
        if (c > 0xFFFF) {
            var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(c), 0);
            return cat == UnicodeCategory.Format || cat == UnicodeCategory.PrivateUse;
        }
        var k = CharUnicodeInfo.GetUnicodeCategory((char)c);
        return k == UnicodeCategory.Format || k == UnicodeCategory.Control || k == UnicodeCategory.PrivateUse
            || k == UnicodeCategory.Surrogate || k == UnicodeCategory.LineSeparator || k == UnicodeCategory.ParagraphSeparator;
    }

    static void Invisibili(string s, List<Motivo> motivi) {
        var p = Punti(s).ToList();
        var trovati = new SortedSet<int>();
        int tag = 0;
        for (int i = 0; i < p.Count; i++)
            if (Nascosto(p[i], i > 0 ? p[i - 1] : 0, i + 1 < p.Count ? p[i + 1] : 0)) { trovati.Add(p[i]); if (p[i] >= 0xE0000 && p[i] <= 0xE007F) tag++; }
        if (trovati.Count == 0) return;
        string elenco = string.Join(" ", trovati.Take(8).Select(c => "U+" + c.ToString("X4")));
        if (tag > 0) {
            var nascosto = new string(p.Where(c => c >= 0xE0020 && c <= 0xE007E).Select(c => (char)(c - 0xE0000)).ToArray());
            Aggiungi(motivi, "invisibili", Lingua.T("dogana.testo_ascii_nascosto_caratteri_invisib"), nascosto);
        }
        Aggiungi(motivi, "invisibili", Lingua.T("dogana.caratteri_invisibili", "elenco", elenco), "");
    }

    // {U+200B} al posto di ogni carattere che non si vede: in quarantena si vede tutto
    public static string RendiVisibile(string s) {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var p = Punti(s).ToList();
        var sb = new StringBuilder();
        for (int i = 0; i < p.Count; i++) {
            if (Nascosto(p[i], i > 0 ? p[i - 1] : 0, i + 1 < p.Count ? p[i + 1] : 0)) sb.Append("{U+" + p[i].ToString("X4") + "}");
            else sb.Append(char.ConvertFromUtf32(p[i]));
        }
        return sb.ToString();
    }

    // ---------- omoglifi: una parola con lettere latine e cirilliche o greche insieme ----------

    static int Scrittura(char c) {
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= 0xC0 && c <= 0x24F)) return 1;
        if (c >= 0x400 && c <= 0x52F) return 2;
        if (c >= 0x370 && c <= 0x3FF) return 3;
        return 0;
    }

    static void Omoglifi(string s, List<Motivo> motivi) {
        foreach (Match m in Regex.Matches(s, @"\w{3,}")) {
            var tipi = new HashSet<int>(m.Value.Select(Scrittura).Where(x => x > 0));
            if (tipi.Count <= 1) continue;
            // si segnano le lettere che non sono latine: «esempio{а cirillico}»
            var sb = new StringBuilder();
            foreach (char ch in m.Value) {
                int sc = Scrittura(ch);
                sb.Append(sc == 2 ? "{" + ch + " " + Lingua.T("dogana.cirillico") + "}" : sc == 3 ? "{" + ch + " " + Lingua.T("dogana.greco") + "}" : ch.ToString());
            }
            Aggiungi(motivi, "omoglifi", Lingua.T("dogana.parola_lettere_alfabeti_diversi_mescol"), sb.ToString());
            return;
        }
    }

    static string Ripulisci(string s) {
        var p = Punti(s).ToList();
        var sb = new StringBuilder();
        for (int i = 0; i < p.Count; i++)
            if (!Nascosto(p[i], i > 0 ? p[i - 1] : 0, i + 1 < p.Count ? p[i + 1] : 0)) sb.Append(char.ConvertFromUtf32(p[i]));
        string r;
        try { r = sb.ToString().Normalize(NormalizationForm.FormKC); } catch { r = sb.ToString(); }
        r = r.ToLowerInvariant().Replace('’', '\'').Replace('`', ' ');
        return Regex.Replace(r, @"[ \t]+", " ");
    }

    // ---------- Markdown e HTML ----------

    static readonly Regex RECINTO = new Regex(@"^[ ]{0,3}(`{3,}|~{3,})[^\n]*\n[\s\S]*?(^[ ]{0,3}\1[ \t]*$|\z)", RegexOptions.Multiline);

    // il testo che GitHub mostra come testo normale: senza blocchi di codice e senza `codice in riga`
    static string FuoriDalCodice(string s) {
        s = s.Replace("\r\n", "\n");
        s = RECINTO.Replace(s, "\n");
        return Regex.Replace(s, @"`+[^`\n]*`+", " ");
    }

    static void Html(string s, List<Motivo> motivi) {
        var c = Regex.Match(s, @"<!--[\s\S]*?(-->|\z)");
        if (c.Success) Aggiungi(motivi, "html_nascosto", Lingua.T("dogana.commento_html_github_non_si"), c.Value);
        foreach (Match m in Regex.Matches(s, @"<\s{0,20}/?\s{0,20}([a-zA-Z][a-zA-Z0-9-]{0,60})\b[^>]{0,2000}>")) {
            string tag = m.Groups[1].Value;
            if (Regex.IsMatch(m.Value, @"\b(hidden|style\s*=|on[a-z]+\s*=|display\s*:|font-size\s*:\s*0|color\s*:)", O))
                Aggiungi(motivi, "html_nascosto", Lingua.T("dogana.html_che_nasconde_colora_testo"), m.Value);
            else if (tag.Equals("details", StringComparison.OrdinalIgnoreCase) || tag.Equals("summary", StringComparison.OrdinalIgnoreCase))
                Aggiungi(motivi, "html_nascosto", Lingua.T("dogana.testo_piegato_details_github_resta"), m.Value);
            else if (!TAG_OK.Contains(tag))
                Aggiungi(motivi, "html", Lingua.T("dogana.html_fuori_dai_tag_formattazione", "tag", tag), m.Value);
        }
    }

    static void Markdown(string s, List<Motivo> motivi) {
        var rif = Regex.Match(s, @"^[ ]{0,3}\[[^\]\n]*\]:\s*\S.*$", RegexOptions.Multiline);
        if (rif.Success) Aggiungi(motivi, "md_nascosto", Lingua.T("dogana.riga_riferimento_markdown_github_non"), rif.Value);
        var alt = Regex.Match(s, @"!\[([^\]\n]{60})");
        if (alt.Success) Aggiungi(motivi, "md_nascosto", Lingua.T("dogana.testo_lungo_nascosto_nella_descrizione"), alt.Groups[1].Value);
    }

    static void BlocchiDiCodice(string s, List<Motivo> motivi) {
        foreach (Match m in Regex.Matches(s.Replace("\r\n", "\n"), @"^[ ]{0,3}(`{3,}|~{3,})[ \t]*([^\s`]*)", RegexOptions.Multiline)) {
            string lingua = m.Groups[2].Value.Trim();
            if (LINGUAGGI_ESEGUIBILI.Contains(lingua)) { Aggiungi(motivi, "codice", Lingua.T("dogana.blocco_codice_eseguire", "lingua", lingua), m.Value); return; }
        }
    }

    // ---------- link ----------

    static readonly string[] ACCORCIATORI = { "bit.ly", "tinyurl.com", "t.co", "goo.gl", "ow.ly", "is.gd", "buff.ly", "rebrand.ly", "cutt.ly", "shorturl.at", "rb.gy", "tiny.cc" };

    static readonly Regex SCARICA = new Regex(@"^(?:[a-z][a-z0-9+.\-]{0,20}://|www\.)[^/?#\s]{1,300}/[^?#\s]{0,2000}\.(exe|msi|msix|appx|bat|cmd|com|scr|pif|ps1|vbs|vbe|wsf|hta|jar|lnk|reg|dll|iso|img|dmg|pkg|deb|rpm|apk|zip|7z|rar|tar|gz|tgz|bz2|xz)(?:[?#]|$)", O);

    static bool Fidato(string host, IEnumerable<string> fidati) {
        host = host.ToLowerInvariant().TrimEnd('.');
        return fidati.Any(f => host == f || host.EndsWith("." + f));
    }

    static string Host(string url) {
        var m = Regex.Match(url, @"^[a-z][a-z0-9+.\-]*://(?:([^/?#@\s]*)@)?([^/?#:\s]+)", O);
        return m.Success ? m.Groups[2].Value : "";
    }

    static void Link(string s, IEnumerable<string> fidati, List<Motivo> motivi) {
        var lista = fidati.Select(f => f.Trim().ToLowerInvariant()).Where(f => f.Length > 0).ToList();
        var schema = Regex.Match(s, @"\b(javascript|vbscript|data|file|ms-[a-z\-]+|search-ms|smb):[^\s)>\]]*", O);
        if (schema.Success && !Regex.IsMatch(schema.Value, @"^data:\s*$", O))
            Aggiungi(motivi, "link_pericoloso", Lingua.T("dogana.link_che_apre_programma_file"), schema.Value);
        foreach (Match m in Regex.Matches(s, @"\b(?:https?|ftp)://[^\s<>()\[\]""'`]+|\bwww\.[^\s<>()\[\]""'`]+", O)) {
            string url = m.Value.TrimEnd('.', ',', ';', ':', '!', '?');
            string host = Host(url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + url : url);
            // anche su un sito fidato: su github.com un file può metterlo chiunque. Conta il percorso, non il sito (.com, .zip e .sh sono anche finali di siti)
            if (SCARICA.IsMatch(url)) Aggiungi(motivi, "link_eseguibile", Lingua.T("dogana.link_programma_archivio_scaricare"), url);
            if (url.Contains("@") && Regex.IsMatch(url, @"://[^/]*@")) Aggiungi(motivi, "link_ingannevole", Lingua.T("dogana.link_nome_utente_davanti_al"), url);
            else if (Regex.IsMatch(host, @"^\d{1,3}(\.\d{1,3}){3}$|^\[")) Aggiungi(motivi, "link_ingannevole", Lingua.T("dogana.link_indirizzo_ip_invece_che"), url);
            else if (host.StartsWith("xn--") || host.Contains(".xn--") || host.Any(ch => ch > 127)) Aggiungi(motivi, "link_ingannevole", Lingua.T("dogana.link_nome_sito_caratteri_non"), url);
            else if (ACCORCIATORI.Contains(host.ToLowerInvariant())) Aggiungi(motivi, "link_esterno", Lingua.T("dogana.link_accorciato_non_si_vede"), url);
            else if (!Fidato(host, lista)) Aggiungi(motivi, "link_esterno", Lingua.T("dogana.link_sito_fuori_dalla_lista", "host", host), url);
        }
        // [github.com/x](https://altrove): il testo del link dice un sito, la destinazione è un altro
        foreach (Match m in Regex.Matches(s, @"\[([^\]\n]{1,300})\]\(\s{0,20}<?([^)\s>]{1,2000})", O)) {
            var visto = Regex.Match(m.Groups[1].Value, @"(?:https?://)?((?:[a-z0-9\-]+\.)+[a-z]{2,})", O);
            if (!visto.Success) continue;
            string vero = Host(m.Groups[2].Value);
            if (vero.Length > 0 && !vero.Equals(visto.Groups[1].Value, StringComparison.OrdinalIgnoreCase)
                && !vero.EndsWith("." + visto.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                Aggiungi(motivi, "link_ingannevole", Lingua.T("dogana.testo_del_link_mostra_sito", "vero", vero), m.Value);
        }
    }

    // ---------- testo codificato ----------

    static void Codificati(string s, List<Motivo> motivi) {
        var b64 = Regex.Match(s, @"[A-Za-z0-9+/]{80,}={0,2}");
        if (b64.Success && Regex.IsMatch(b64.Value, "[a-z]") && Regex.IsMatch(b64.Value, "[A-Z]"))
            Aggiungi(motivi, "codificato", Lingua.T("dogana.lungo_pezzo_base64_puo_nascondere"), b64.Value);
        var pct = Regex.Match(s, @"(%[0-9a-fA-F]{2}){12,}");
        if (pct.Success) Aggiungi(motivi, "codificato", Lingua.T("dogana.testo_nascosto_codici_percentuali"), pct.Value);
        var esc = Regex.Match(s, @"(\\u[0-9a-fA-F]{4}|\\x[0-9a-fA-F]{2}){6,}");
        if (esc.Success) Aggiungi(motivi, "codificato", Lingua.T("dogana.testo_nascosto_codici_u_x"), esc.Value);
        var ent = Regex.Match(s, @"(&#x?[0-9a-fA-F]+;){5,}");
        if (ent.Success) Aggiungi(motivi, "codificato", Lingua.T("dogana.testo_nascosto_entita_html"), ent.Value);
    }

    // ---------- prima di scrivere su GitHub: segreti e percorsi personali ----------
    // Non si ferma mai su parole generiche: cerca file di chiavi e valori che sembrano veri. Il valore trovato non si mostra
    // e non si scrive nel log: si dice solo il file, la riga e che cosa sembra.

    const RegexOptions IC = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const string DOPO_COPIA = @"(\.(bak|old|orig|backup|save|tmp|copy|\d+))?";
    const string DATI = @"\.(txt|csv|json|ya?ml|xml|ini|toml|cfg|conf|xlsx?|docx?|ods|odt|kdbx?|db|sqlite|zip|7z|rar|bak|dat)";

    // password e credential nel nome contano solo sui file di dati: PasswordHelper.cs e reset-password.html sono programmi
    static readonly Regex NOME_SEGRETO = new Regex(@"^((?!(example|sample|template|dist)\.env$)(.+)?\.env(\.(?!example$|sample$|template$|dist$)[^.]+)*"
        + @"|id_(rsa|dsa|ecdsa|ed25519)[\w\-]*(?<!\.pub)(\.(?!pub$)[^.]+)*"
        + @"|.+\.(pem|key|pfx|p12|ppk|jks|keystore|kdbx|ovpn)" + DOPO_COPIA
        + @"|(credentials|credenziali|secrets?|segreti|token|chiavi|passwords?|passw[oö]rter|passwort|kennw[oö]rter|contrase[nñ]as|motsdepasse|mots-de-passe)(\.(json|ya?ml|txt|ini|xml|toml|csv))?" + DOPO_COPIA
        + @"|.*(password|passwd|passwort|kennwort|contrase[nñ]a|motdepasse|mot-de-passe|mots-de-passe|credential|credenzial|zugangsdaten|identifiants|credenciales).*" + DATI
        + @"|wallet\.dat|login data|(.*[_\-.])?(backup|dump)([_\-.].*)?\.sql(\.gz)?"
        + @"|\.npmrc|\.pypirc|\.netrc|_netrc|\.git-credentials|\.htpasswd|\.pgpass)$", IC);

    // [chiave di ciò che sembra, espressione]: qui non si ignorano le maiuscole, le chiavi vere hanno una forma esatta
    static readonly string[][] CHIAVI = {
        new[] { "github", @"\b(gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,})\b" },
        new[] { "gitlab", @"\bglpat-[0-9A-Za-z_\-]{20,}" },
        new[] { "npm", @"\bnpm_[A-Za-z0-9]{36,}\b" },
        new[] { "huggingface", @"\bhf_[A-Za-z0-9]{30,}\b" },
        new[] { "sendgrid", @"\bSG\.[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{30,}" },
        new[] { "discord", @"discord(app)?\.com/api/webhooks/\d{5,}/[A-Za-z0-9_\-]{20,}" },
        new[] { "telegram", @"\b\d{8,12}:AA[A-Za-z0-9_\-]{30,}" },
        new[] { "privata", @"-----BEGIN [A-Z ]{0,40}PRIVATE KEY( BLOCK)?-----" },
        new[] { "amazon", @"\b(AKIA|ASIA)[0-9A-Z]{16}\b" },
        new[] { "google", @"\bAIza[0-9A-Za-z_\-]{35}\b" },
        new[] { "slack", @"\bxox[abprs]-[0-9A-Za-z\-]{10,}\b" },
        new[] { "pagamento", @"\b[sr]k_live_[0-9A-Za-z]{16,}\b" },
        new[] { "ai", @"\bsk-(ant-)?[A-Za-z0-9_\-]{32,}\b" },
        new[] { "jwt", @"\beyJ[A-Za-z0-9_\-]{10,}\.eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\b" } };

    static string SEGRETO { get { return Lingua.T("comune.sembra_password_chiave_scritta_nel"); } }
    static string VARIABILE { get { return Lingua.T("dogana.sembra_variabile_dentro_password_chiav"); } }
        static string URL_ACCESSO { get { return Lingua.T("dogana.sembra_indirizzo_dentro_nome_password"); } }
    static string CONNESSIONE { get { return Lingua.T("dogana.sembra_stringa_connessione_password"); } }
    static string PERSONALE { get { return Lingua.T("comune.contiene_percorso_personale_computer"); } }

    // un nome che tiene un segreto, anche composto (SECRET_KEY, db_password, clientSecret). «pass» è corto: conta solo staccato, così bypass e compass non c'entrano
    static readonly Regex ASSEGNATO = new Regex(@"(?<k>password|passwd|passwort|kennwort|contrase[nñ]a|mot[_\-]?de[_\-]?passe|pwd|secret|api[_\-]?key|access[_\-]?token|auth[_\-]?token|token|access[_\-]?key|private[_\-]?key|(?<![A-Za-z])pass(?=[_\-.""'\s:=]))"
        + @"(?<s>[_\-.][\w\-.]{0,40}|key|value|hash|s)?[""']?\s{0,8}[:=]\s{0,8}(?:""(?<v>[^""\s]{8,})""|'(?<v>[^'\s]{8,})'|`(?<v>[^`\s]{8,})`|(?<v>[^\s""'`,;]{8,}))", IC);
    // password_policy, secret_name: parlano del segreto, non lo contengono
    static readonly Regex NON_VALORE = new Regex(@"[_\-.](name|field|file|path|url|uri|id|label|policy|length|min|max|count|prompt|hint|message|msg|regex|pattern|env|var|ref|type|title|text)$", IC);
    static readonly Regex AMBIENTE = new Regex(@"^[ \t]{0,40}(export[ \t]{1,8})?(?<n>[A-Za-z0-9_]{0,60}(PASSWORD|PASSWD|PASSWORT|KENNWORT|SECRET|TOKEN|API_?KEY)[A-Za-z0-9_]{0,60}|([A-Za-z0-9_]{0,60}_)?(PASS|PWD)(_[A-Za-z0-9_]{0,60})?)=(?<v>[^\s#]{8,})", RegexOptions.Multiline | IC);
    static readonly Regex URL_CON_PASSWORD = new Regex(@"://(?<u>[^/\s:@]{0,100}):(?<v>[^@\s/]{1,200})@(?<h>[^/\s:?#]{1,255})", RegexOptions.CultureInvariant);
    static readonly Regex PASSWORD_CONNESSIONE = new Regex(@"\b(Password|Pwd)\s{0,8}=\s{0,8}(?<v>[^;\s""'<>{}$%]{4,})\s{0,8}(;|$|[""'])", RegexOptions.Multiline | IC);
    static readonly Regex CHIAVE_CONNESSIONE = new Regex(@"\b(server|data source|host|user id|uid|database|initial catalog)\s{0,8}=", IC);
    static readonly Regex HOST_FINTO = new Regex(@"^(localhost|127\.0\.0\.1|host|hostname|server|db|database|(.*\.)?example\.(com|org|net)|(.*\.)?esempio\.\w+|.*\.local)$", IC);
    // un segnaposto è la parola da sola, con al massimo pochi numeri o un separatore e il nome di ciò che sostituisce (test, prova123, example_key).
    // Se dopo la parola c'è altro non è più un segnaposto: provaprova123 e demo2024!x sono valori
    const string COSA = @"(key|api|apikey|access|auth|private|secret|segreto|token|password|passwd|pwd|pass|value|valore|chiave|user|utente|account|app|client|id|db|database|here|qui)";
    static readonly Regex FINTO = new Regex(@"^(x+|\*+|\.+|(changeme|cambiami)\d{0,4}"
        + @"|(example|esempio|sample|placeholder|segnaposto|dummy|fake|finto|finta|test|testing|prova|demo|foo|foobar|bar|baz)([_\-.]?" + COSA + @"){0,3}[_\-.]?\d{0,4}"
        + @"|((la|il)[_\-])?(your|my|tu[oa]|mi[oa])[_\-]?([a-z]{1,20}[_\-]){0,4}" + COSA + @"([_\-](here|qui))?\d{0,4}"
        + @"|(pass|password|passwd|pwd|secret|segreto|token)\d{0,4}|abc123(def(456)?)?\d{0,4}|123456\d{0,6}|qwerty\d{0,6}|null|none|true|false"
        + @"|base64(url|encoded|encode|string)?|sha-?\d+|md5(sum)?|utf-?8"
        + @"|\$\{.*\}|\$\(.*\)|\$[A-Za-z_][\w:.]*|<[^>]*>|%[\w.]+%|%[sdrvqx]|%\(\w+\)[sd]|\{\{?[^}]*\}\}?)$", IC);
    // il richiamo di una variabile o di un modello. Un $ o un % dentro un valore qualsiasi non basta: Xy$12345 è una password, p%40ss anche
    static readonly Regex RIMANDO = new Regex(@"\$[A-Za-z_{(]|%[A-Za-z_]\w*%|%[sdrvqx]\b|%\(", IC);
    static readonly Regex CATENA = new Regex(@"^[A-Za-z_]\w*(\.[A-Za-z_]\w*)+$");   // request.headers, process.env.TOKEN
    static readonly Regex PERCORSO = new Regex(@"\b[A-Za-z]:[\\/]+Users[\\/]+(?!Public\b|Default\b|All Users\b)[^\\/\s""'<>%$\0]{1,100}|(?<![\w.\-/])/(home|Users)/(?!user\b|utente\b|name\b|nome\b|you\b|example\b|esempio\b|runner\b)[A-Za-z0-9._\-]{1,100}/", IC);
    static readonly Regex EMAIL = new Regex(@"\b(?<u>[A-Za-z0-9._%+\-]{1,64})@(?<h>[A-Za-z0-9\-]{1,63}(\.[A-Za-z0-9\-]{1,63}){0,6}\.[A-Za-z]{2,24})\b", RegexOptions.CultureInvariant);
    static readonly Regex NON_EMAIL = new Regex(@"\.(png|jpe?g|gif|svg|webp|ico)$", IC);
    static readonly Regex CODICE_FISCALE = new Regex(@"\b[A-Z]{6}\d{2}[A-EHLMPR-T]\d{2}[A-Z]\d{3}[A-Z]\b", RegexOptions.CultureInvariant);

    // senza virgolette vale solo quello che somiglia a un valore vero: una parola sola, il richiamo di una funzione, un percorso o un segnaposto non fermano
    static bool SembraValore(string v) {
        if (FINTO.IsMatch(v) || v.IndexOfAny(new[] { '(', ')', '[', ']', '{', '}', '<', '>' }) >= 0 || RIMANDO.IsMatch(v)) return false;
        if (CATENA.IsMatch(v) || Regex.IsMatch(v, @"^([A-Za-z]:[\\/]|\.{0,2}/|\w+://)")) return false;
        if (!v.Any(char.IsLetter)) return false;
        return v.Any(char.IsDigit) || v.IndexOfAny(new[] { '!', '@', '#', '&', '*', '+', '=', '^', '~' }) >= 0 || (v.Length >= 32 && v.All(char.IsLetterOrDigit));
    }

    class Trovato { public int Da, Quanto; public string Cosa; }

    // quello che si riconosce dalla forma esatta: vale anche dentro i file binari
    static void CercaForma(string testo, List<Trovato> l) {
        foreach (var k in CHIAVI)
            foreach (Match m in Regex.Matches(testo, k[1], RegexOptions.CultureInvariant)) l.Add(new Trovato { Da = m.Index, Quanto = m.Length, Cosa = Lingua.T("dogana.sembra." + k[0]) });
        foreach (Match m in PERCORSO.Matches(testo)) l.Add(new Trovato { Da = m.Index, Quanto = m.Length, Cosa = PERSONALE });
        string profilo = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profilo.Length >= 4)
            for (int i = testo.IndexOf(profilo, StringComparison.OrdinalIgnoreCase); i >= 0; i = testo.IndexOf(profilo, i + profilo.Length, StringComparison.OrdinalIgnoreCase))
                l.Add(new Trovato { Da = i, Quanto = profilo.Length, Cosa = PERSONALE });
    }

    // tutti i valori che sembrano segreti, con la posizione: chi chiama dice dove sono o li copre. Il valore non esce mai da qui
    static List<Trovato> Cerca(string testo) {
        var l = new List<Trovato>();
        CercaForma(testo, l);
        foreach (Match m in ASSEGNATO.Matches(testo)) {
            string v = m.Groups["v"].Value, nome = m.Groups["k"].Value.ToLowerInvariant(), dopo = m.Groups["s"].Value;
            if (dopo.Length > 0 && NON_VALORE.IsMatch(dopo)) continue;
            bool virgolette = m.Value.EndsWith("\"") || m.Value.EndsWith("'") || m.Value.EndsWith("`");
            // fra virgolette, col nome esatto di una password: basta che non sia un segnaposto. Tutto il resto deve somigliare a un valore vero
            bool secco = virgolette && dopo.Length == 0 && Regex.IsMatch(nome, @"^(password|passwd|passwort|kennwort|contrase[nñ]a|mot[_\-]?de[_\-]?passe|pwd|secret|api[_\-]?key|access[_\-]?token|auth[_\-]?token)$");
            if (secco ? !FINTO.IsMatch(v) : SembraValore(v)) l.Add(new Trovato { Da = m.Groups["v"].Index, Quanto = v.Length, Cosa = SEGRETO });
        }
        foreach (Match m in AMBIENTE.Matches(testo)) {
            string v = m.Groups["v"].Value, nome = m.Groups["n"].Value;
            if (FINTO.IsMatch(v) || v.IndexOfAny(new[] { '(', '{', '<' }) >= 0 || RIMANDO.IsMatch(v)) continue;
            // PWD è anche la cartella di lavoro, e in minuscolo una parola sola può essere un'etichetta (password=obbligatoria)
            if (Regex.IsMatch(nome, @"(^|_)(PASS|PWD)(_|$)", IC) && !SembraValore(v)) continue;
            if (nome == nome.ToUpperInvariant() || !Regex.IsMatch(v, @"^[A-Z]?[a-z]+$")) l.Add(new Trovato { Da = m.Groups["v"].Index, Quanto = v.Length, Cosa = VARIABILE });
        }
        foreach (Match m in URL_CON_PASSWORD.Matches(testo)) {
            string v = m.Groups["v"].Value;
            if (!FINTO.IsMatch(v) && !HOST_FINTO.IsMatch(m.Groups["h"].Value) && v.IndexOfAny(new[] { '{', '<' }) < 0 && !RIMANDO.IsMatch(v)) l.Add(new Trovato { Da = m.Groups["v"].Index, Quanto = v.Length, Cosa = URL_ACCESSO });
        }
        foreach (Match m in PASSWORD_CONNESSIONE.Matches(testo)) {
            string v = m.Groups["v"].Value;
            int a = testo.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1, b = testo.IndexOf('\n', m.Index);
            string riga = testo.Substring(a, (b < 0 ? testo.Length : b) - a);
            if (!FINTO.IsMatch(v) && CHIAVE_CONNESSIONE.IsMatch(riga)) l.Add(new Trovato { Da = m.Groups["v"].Index, Quanto = v.Length, Cosa = CONNESSIONE });
        }
        return l;
    }

    static int RigaDi(string s, int indice) { int n = 1; for (int i = 0; i < indice; i++) if (s[i] == '\n') n++; return n; }

    public static List<Riscontro> ControllaNome(string percorso) {
        var l = new List<Riscontro>();
        var pezzi = (percorso ?? "").Replace('\\', '/').Split('/');
        if (NOME_SEGRETO.IsMatch(pezzi[pezzi.Length - 1])) l.Add(new Riscontro { File = percorso, Cosa = Lingua.T("comune.nome_dice_che_file_chiavi") });
        if (pezzi.Take(pezzi.Length - 1).Any(x => x.Equals(".ssh", StringComparison.OrdinalIgnoreCase) || x.Equals(".aws", StringComparison.OrdinalIgnoreCase) || x.Equals(".gnupg", StringComparison.OrdinalIgnoreCase)))
            l.Add(new Riscontro { File = percorso, Cosa = Lingua.T("dogana.sta_cartella_chiavi") });
        string coda = pezzi.Length < 2 ? "" : pezzi[pezzi.Length - 2] + "/" + pezzi[pezzi.Length - 1];
        if (coda.Equals(".docker/config.json", StringComparison.OrdinalIgnoreCase) || coda.Equals(".kube/config", StringComparison.OrdinalIgnoreCase))
            l.Add(new Riscontro { File = percorso, Cosa = Lingua.T("dogana.file_accesso_programma") });
        return l;
    }

    // una riga per ogni tipo di cosa trovata, alla prima volta che compare
    public static List<Riscontro> ControllaTesto(string percorso, string testo) {
        testo = testo ?? "";
        return Cerca(testo).OrderBy(x => x.Da).GroupBy(x => x.Cosa).Select(g => new Riscontro { File = percorso, Riga = RigaDi(testo, g.First().Da), Cosa = g.Key }).ToList();
    }

    // copre i valori e dice quanti sono: serve ai testi scritti da altri, che possono contenere segreti di chi li ha scritti
    public static string Maschera(string testo, out int quanti) {
        quanti = 0;
        if (string.IsNullOrEmpty(testo)) return testo ?? "";
        var sb = new StringBuilder();
        int a = 0;
        foreach (var t in Cerca(testo).OrderBy(x => x.Da)) {
            if (t.Da < a) continue;
            sb.Append(testo, a, t.Da - a).Append("[coperto da Project Manager]");
            a = t.Da + t.Quanto;
            quanti++;
        }
        return sb.Append(testo, a, testo.Length - a).ToString();
    }

    // email e codici fiscali non fermano niente: in una repo può essere giusto che ci siano, ma chi carica deve saperlo. Il valore non si dice
    public static List<Riscontro> Avvisi(string percorso, byte[] b) {
        var l = new List<Riscontro>();
        if (b == null) return l;
        var u = Binario(b) ? Utf16(b) : null;
        if (Binario(b) && u == null) return l;
        string testo = u != null ? u.GetString(b) : Testo(b);
        foreach (Match m in EMAIL.Matches(testo)) {
            string h = m.Groups["h"].Value.ToLowerInvariant(), n = m.Groups["u"].Value.ToLowerInvariant();
            if (h.EndsWith("noreply.github.com") || HOST_FINTO.IsMatch(h) || NON_EMAIL.IsMatch(h) || n == "git" || n == "noreply" || n.StartsWith("no-reply")) continue;
            l.Add(new Riscontro { File = percorso, Riga = RigaDi(testo, m.Index), Cosa = Lingua.T("dogana.contiene_indirizzo_email") });
            break;
        }
        var cf = CODICE_FISCALE.Match(testo);
        if (cf.Success) l.Add(new Riscontro { File = percorso, Riga = RigaDi(testo, cf.Index), Cosa = Lingua.T("dogana.contiene_codice_fiscale") });
        return l;
    }

    public static bool Binario(byte[] b) {
        if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) return false;   // testo UTF-16 col suo segno
        for (int i = 0; i < Math.Min(b.Length, 8192); i++) if (b[i] == 0) return true;
        return false;
    }

    public static string Testo(byte[] b) {
        using (var r = new System.IO.StreamReader(new System.IO.MemoryStream(b), Encoding.UTF8, true)) return r.ReadToEnd();
    }

    // testo UTF-16 senza il suo segno: uno zero ogni due caratteri, che alle chiavi fa perdere la forma. Nullo se non lo è
    static Encoding Utf16(byte[] b) {
        int n = Math.Min(b.Length, 8192) & ~1, pari = 0, dispari = 0;
        if (n < 4) return null;
        for (int i = 0; i < n; i += 2) { if (b[i] == 0) pari++; if (b[i + 1] == 0) dispari++; }
        int coppie = n / 2;
        if (dispari * 10 > coppie * 4 && pari * 20 < coppie) return Encoding.Unicode;
        if (pari * 10 > coppie * 4 && dispari * 20 < coppie) return Encoding.BigEndianUnicode;
        return null;
    }

    // nei file binari valgono solo le forme esatte. Due giri: i byte così come sono e senza gli zeri, cioè il testo UTF-16 in
    // qualunque punto del file. I file grandi si leggono a pezzi che si sovrappongono
    const int PEZZO = 32 * 1024 * 1024, SOVRAPPOSTI = 4096;

    static void CercaNelBinario(string percorso, byte[] b, List<Riscontro> l) {
        var latino = Encoding.GetEncoding(28591);
        var visti = new HashSet<string>();
        for (long da = 0; da < b.Length; da += PEZZO - SOVRAPPOSTI) {
            int quanti = (int)Math.Min(PEZZO, b.Length - da), n = 0;
            var senzaZeri = new byte[quanti];
            for (int i = 0; i < quanti; i++) if (b[da + i] != 0) senzaZeri[n++] = b[da + i];
            var trovati = new List<Trovato>();
            CercaForma(latino.GetString(b, (int)da, quanti), trovati);
            CercaForma(latino.GetString(senzaZeri, 0, n), trovati);
            foreach (var t in trovati) if (visti.Add(t.Cosa)) l.Add(new Riscontro { File = percorso, Cosa = t.Cosa });
            if (da + quanti >= b.Length) break;
        }
    }

    public static List<Riscontro> ControllaContenuto(string percorso, byte[] b) {
        var l = new List<Riscontro>();
        if (b == null) return l;
        var u = Binario(b) ? Utf16(b) : null;
        if (u != null) return ControllaTesto(percorso, u.GetString(b));
        if (Binario(b)) CercaNelBinario(percorso, b, l);
        else l = ControllaTesto(percorso, Testo(b));
        return l;
    }

    // il controllo di un file intero: il nome e quello che c'è dentro
    public static List<Riscontro> ControllaFile(string percorso, byte[] b) {
        var l = ControllaNome(percorso);
        l.AddRange(ControllaContenuto(percorso, b));
        return l;
    }
}
