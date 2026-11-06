using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class Screener {
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const int Long = 20000;

    static readonly string[][] Sentences = {
        new[] { "ai_orders", "screener.asks_ignore_instructions_rules",
                @"\b(ignore|disregard|forget|override|bypass|skip)\b.{0,40}\b(instructions?|prompts?|rules|guidelines|previous|above|system|guardrails?)\b" },
        new[] { "ai_orders", "screener.asks_ignore_instructions_rules",
                @"\b(ignora|ignorate|ignorare|dimentica|dimenticate|dimenticare|trascura|scavalca|salta)\b.{0,40}\b(istruzion\w*|comand\w*|regol\w*|prompt|precedent\w*|sopra|sistema|limiti)\b" },
        new[] { "ai_role", "screener.tries_change_readers_role",
                @"\b(you are now|from now on,? you|act as|pretend (to be|you are)|roleplay as|sei ora|adesso sei|da ora in poi|fingi di essere|comportati come|fai finta di essere)\b" },
        new[] { "ai_markers", "screener.contains_ai_prompt_markers",
                @"(<\|?(system|im_start|im_end|assistant|user|endoftext)\|?>|\[/?inst\]|\bsystem prompt\b|###\s*(instruction|system|response)|^\s*(assistant|system|human|user)\s*:|</?(system|instructions?|prompt|tool_call|function_call)>)" },
        new[] { "ai_mention", "screener.addresses_names_ai",
                @"\b(claude|anthropic|chatgpt|openai|gpt-?\d\w*|copilot|gemini|llama|mistral|llm|ai agent|agente ai|language model|modello linguistico|assistente (ai|virtuale)|ai assistant|coding agent|cursor ai)\b" },
        new[] { "orders", "screener.asks_run_something",
                @"\b(run|execute|eval|paste and run|esegui|eseguite|eseguire|lancia|lanciate|avvia|incolla ed esegui)\b.{0,30}\b(command|commands|comando|comandi|script|scripts|code|codice|following|seguente|seguenti|terminal|terminale|powershell|prompt)\b" },
        new[] { "exfiltration", "screener.asks_send_data_somewhere",
                @"\b(send|post|upload|forward|exfiltrate|leak|transmit|invia|inviare|inviate|manda|mandare|mandate|carica|caricare|inoltra|inoltrare|trasmetti)\b.{0,60}\b(to|a|al|alla|su|verso|all')\b.{0,40}(https?://|server|webhook|endpoint|e-?mail|indirizzo|url|discord|telegram|pastebin)" },
        new[] { "secrets", "screener.talks_about_secrets_keys_credentials",
                @"\b(api[_ -]?keys?|access[_ -]?tokens?|tokens? (di|del|of)|secrets?|password|passwd|credentials?|credenziali|\.env\b|ssh[_ -]?keys?|id_rsa|private key|chiave privata|chiavi private|cookies?|session id|wallet|seed phrase)\b" },
        new[] { "secrecy", "screener.asks_act_secret",
                @"\b(do not tell|don't tell|dont tell|don't mention|do not mention|without telling|secretly|silently|non dire|non dirlo|non dirglielo|senza dirlo|di nascosto|segretamente|non menzionare|senza avvisare)\b" },
        new[] { "commands", "screener.contains_terminal_commands",
                @"(\b(curl|wget|powershell|pwsh|cmd\.exe|cmd /c|bash -c|sh -c|invoke-webrequest|invoke-restmethod|invoke-expression|iwr|irm|iex|certutil|bitsadmin|mshta|rundll32|regsvr32|start-process|set-executionpolicy|reg add|schtasks|npm (i|install)|npx|pip install|git clone|chmod \+x|sudo|rm -rf|del /[fsq]|format [a-z]:|base64 -d|frombase64string|downloadstring|downloadfile)\b|\|\s*(sh|bash|iex|powershell)\b)" },
        new[] { "bot_mention", "screener.calls_bot_agent",
                @"(^|[\s(])@(claude|copilot|github-actions|dependabot|chatgpt|openai|gemini|codex|devin|cursor|sweep|coderabbitai|codeant|qodo|sourcery|ellipsis|greptile)\b" },
    };

    const string NotFr = @"(?<!\b(programme|logiciel|application|appli|outil|script|syst[eè]me|bouton|il|elle|on|qui|ça|cela|je)\s)(?<!\bj')";
    const string NotEs = @"(?<!\b(programa|aplicaci[oó]n|app|herramienta|script|sistema|bot[oó]n|[eé]l|ella|que|se|yo)\s)";
    const string NotDe = @"(?<!\b(programm|app|anwendung|tool|skript|script|system|es|er|ich)\s)";
    static readonly string[][] OtherSentences = {
        new[] { "ai_orders", "screener.asks_ignore_instructions_rules",
                NotFr + @"\b(ignore[zr]?|oublie[zr]?|ne tiens? pas compte|ne tenez pas compte|contourne[zr]?|outrepasse[zr]?)\b.{0,40}\b(instructions?|consignes?|directives?|r[eè]gles|prompts?|ce qui pr[eé]c[eè]de)\b" },
        new[] { "ai_orders", "screener.asks_ignore_instructions_rules",
                NotEs + @"\b(ignora|ignore|ignorad|ignoren|ignorar|olvida|olvide|olvidad|olviden|olvidar|omite|omita|omitid|omitir|s[aá]ltate|haz caso omiso)\b.{0,40}\b(instrucci[oó]n\w*|[oó]rdenes|reglas|normas|indicaciones|directrices|prompts?|todo lo anterior)\b" },
        new[] { "ai_orders", "screener.asks_ignore_instructions_rules",
                NotDe + @"\b(ignoriere|ignorier|ignorieren|ignoriert (alle|jede|s[aä]mtliche)|vergiss|vergesst|vergessen|missachte|missachten|[uü]bergehe|[uü]bergehen|umgehe|umgehen)\b.{0,40}\b(anweisung\w*|instruktion\w*|anordnung\w*|regeln|vorgaben|richtlinien|prompts?)\b" },
        new[] { "ai_orders", "screener.asks_ignore_instructions_rules",
                @"\b(anweisung\w*|instruktion\w*|vorgaben|richtlinien)\b.{0,40}\b(ignorieren|vergessen|missachten|[uü]bergehen|umgehen)\b" },
        new[] { "ai_role", "screener.tries_change_readers_role",
                @"\b((tu es|vous [eê]tes) maintenant (un|une|le|la|mon|ma|dan)|d[eé]sormais,? (tu|vous)|[aà] partir de maintenant,? (tu|vous)|agis comme|agissez comme|fais semblant d'[eê]tre|faites semblant d'[eê]tre|pr[eé]tends [eê]tre|joue le r[oô]le|jouez le r[oô]le)\b" },
        new[] { "ai_role", "screener.tries_change_readers_role",
                @"\b(ahora eres|ahora es usted|desde ahora eres|a partir de ahora,? (eres|act[uú]a|responde)|act[uú]a como|actuad como|act[uú]e como|finge ser|fingid ser|hazte pasar por|comp[oó]rtate como)\b" },
        new[] { "ai_role", "screener.tries_change_readers_role",
                @"\b((du bist|sie sind) (jetzt|nun) (ein|eine|der|die|mein|meine|dan)|von nun an (bist|sollst|antworte\w*)|ab jetzt bist|ab sofort bist|verhalte dich (wie|als)|tu so,? als|gib dich als|agiere als|spiele die rolle|handle als)\b" },
        new[] { "ai_markers", "screener.contains_ai_prompt_markers",
                @"\b(prompt (du )?syst[eè]me|prompt de sistema|prompt del sistema|prompt di sistema|system-?prompt\w*|systemanweisung\w*)\b" },
        new[] { "ai_mention", "screener.addresses_names_ai",
                @"\b(agent (d')?ia|assistant (d')?ia|assistant virtuel|mod[eè]le de langage|agente (de )?ia|asistente (de )?ia|asistente virtual|modelo de lenguaje|ki-agent\w*|ki-assistent\w*|sprachmodell\w*)\b" },
        new[] { "orders", "screener.asks_run_something",
                NotFr + @"\b(ex[eé]cutez?|lancez?|collez? et ex[eé]cutez?)\b.{0,30}\b(commandes?|scripts?|code|suivant\w*|terminal|powershell|invite de commandes)\b" },
        new[] { "orders", "screener.asks_run_something",
                NotEs + @"\b(ejecuta|ejecute|ejecutad|ejecuten|lanza|lanzad|pega y ejecuta)\b.{0,30}\b(comandos?|[oó]rdenes|scripts?|c[oó]digo|siguientes?|terminal|powershell)\b" },
        new[] { "orders", "screener.asks_run_something",
                NotDe + @"\b(f[uü]hre|f[uü]hren sie|starte|starten sie)\b.{0,30}\b(befehl\w*|kommando\w*|skript\w*|script\w*|code|folgend\w*|terminal|powershell)\b" },
        new[] { "orders", "screener.asks_run_something",
                @"\b(befehl\w*|kommando\w*|skript\w*|script\w*)\b(?:(?!\bnicht\b).){0,30}\bausf[uü]hren\b" },
        new[] { "exfiltration", "screener.asks_send_data_somewhere",
                NotFr + @"\b(envoie|envoyez|transmets|transmettez|t[eé]l[eé]verse[z]?|publie[z]?|transf[eè]re[z]?)\b.{0,60}\b([aà]|au|aux|sur|vers)\b.{0,40}(https?://|serveur|webhook|endpoint|e-?mail|courriel|adresse|url|discord|telegram|pastebin)" },
        new[] { "exfiltration", "screener.asks_send_data_somewhere",
                NotEs + @"\b(env[ií]a|env[ií]e|enviad|manda|mande|mandad|sube|suba|subid|reenv[ií]a|transmite)\b.{0,60}\b(a|al|en|hacia)\b.{0,40}(https?://|servidor|webhook|endpoint|e-?mail|correo|direcci[oó]n|url|discord|telegram|pastebin)" },
        new[] { "exfiltration", "screener.asks_send_data_somewhere",
                NotDe + @"\b(sende|senden sie|schicke?|schicken sie|lade|laden sie|[uü]bertrage|[uü]bertragen sie|leite|leiten sie)\b.{0,60}\b(an|auf|zu|nach)\b.{0,40}(https?://|server|webhook|endpoint|e-?mail|adresse|url|discord|telegram|pastebin)" },
        new[] { "secrets", "screener.talks_about_secrets_keys_credentials",
                @"(\b(cl[eé]s? (d')?api|jetons? (d'acc[eè]s|api)|mots? de passe|identifiants|cl[eé]s? priv[eé]es?|cl[eé]s? ssh|phrase de r[eé]cup[eé]ration)\b|\b(claves? (de )?api|contrase[nñ]as?|credenciales|claves? privadas?|claves? ssh|tokens? de acceso|frase semilla)\b|\b(api-schl[uü]ssel\w*|passw[oö]rt\w*|kennw[oö]rt\w*|zugangsdaten|anmeldedaten|privater? schl[uü]ssel\w*|geheimer? schl[uü]ssel\w*|ssh-schl[uü]ssel\w*|zugriffstoken\w*)\b)" },
        new[] { "secrecy", "screener.asks_act_secret",
                @"\b(ne (le )?dis (rien|pas)|ne dites (rien|pas)|sans le dire|en secret|secr[eè]tement|ne mentionne pas|ne mentionnez pas|no (le |se lo |lo )?digas|sin decir(lo|le|selo)|en secreto|secretamente|a escondidas|no menciones|no mencione|sag (es )?(niemandem|keinem|nichts)|sage (es )?(niemandem|keinem|nichts)|sagen sie (es )?(niemandem|keinem|nichts)|ohne (es )?zu (sagen|erw[aä]hnen)|heimlich|im geheimen|erw[aä]hne (das |es )?nicht|erw[aä]hnen sie (das |es )?nicht)\b" },
    };

    static readonly string[] Attached = {
        "ignorepreviousinstructions", "ignoreallpreviousinstructions", "ignoretheaboveinstructions", "ignoreyourinstructions",
        "disregardpreviousinstructions", "forgetyourinstructions", "systemprompt", "youarenow",
        "ignoraleistruzioni", "ignoratutteleistruzioni", "dimenticaleistruzioni", "ignoraiprecedenti", "seiora",
        "ignorelesinstructions", "ignorezlesinstructions", "ignoreztouteslesinstructions", "ignoretouteslesinstructions", "oublielesinstructions", "oubliezlesinstructions", "oublieztouteslesinstructions", "oublietouteslesinstructions", "tuesmaintenant",
        "ignoralasinstrucciones", "ignoratodaslasinstrucciones", "ignorelasinstrucciones", "olvidalasinstrucciones", "olvidatodaslasinstrucciones", "ahoraeres",
        "ignorierealleanweisungen", "ignorierediebisherigenanweisungen", "ignorierealleneingaben", "vergissalleanweisungen", "vergissdeineanweisungen", "dubistjetzt" };

    static readonly HashSet<string> TagOk = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "b", "i", "em", "strong", "br", "code", "pre", "kbd", "sub", "sup", "p", "ul", "ol", "li", "blockquote",
        "h1", "h2", "h3", "h4", "h5", "h6", "table", "tr", "td", "th", "thead", "tbody", "hr", "s", "del", "ins", "img", "a",
        "utente", "computer" };

    static readonly HashSet<string> ExecutableLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "powershell", "ps1", "ps", "pwsh", "bash", "sh", "shell", "zsh", "cmd", "bat", "batch", "console", "terminal",
        "python", "py", "js", "javascript", "node", "vbs", "vbscript", "applescript", "csharp", "cs", "c#", "ruby", "perl", "php" };

    public static List<Reason> Examine(string title, string text, IEnumerable<string> trustedDomains) {
        var reasons = new List<Reason>();
        string all = (title ?? "") + "\n" + (text ?? "");
        if (all.Length > Long) { Add(reasons, "too_long", Language.T("screener.text_too_long_characters_not", "all", all.Length), ""); return reasons; }

        Invisible(all, reasons);
        Homoglyphs(all, reasons);

        string clean = CleanUp(all);
        foreach (var f in Sentences.Concat(OtherSentences)) {
            var m = Regex.Match(clean, f[2], O | RegexOptions.Multiline);
            if (m.Success) Add(reasons, f[0], Language.T(f[1]), Around(clean, m.Index, m.Length));
        }
        string attached = new string(clean.Where(char.IsLetter).ToArray());
        foreach (var a in Attached)
            if (attached.Contains(a) && !reasons.Any(x => x.Code == "ai_orders" || x.Code == "ai_role" || x.Code == "ai_markers"))
                Add(reasons, "ai_orders", Language.T("screener.sentence_ai_written_broken_up"), a);
        var spaced = Regex.Match(clean, @"(?<![\w])(\w[ .\-_*]){8,}\w(?![\w])");
        if (spaced.Success) Add(reasons, "spaced", Language.T("screener.letters_separated_one_one_avoid"), Short(spaced.Value));

        string outside = OutsideCode(text ?? "");
        Html(outside, reasons);
        Markdown(outside, reasons);
        CodeBlocks(text ?? "", reasons);
        Link(all, trustedDomains, reasons);
        Encoded(all, reasons);
        return reasons;
    }

    static void Add(List<Reason> reasons, string code, string explanation, string extracted) {
        if (reasons.Any(m => m.Code == code && m.Explanation == explanation)) return;
        reasons.Add(new Reason { Code = code, Explanation = explanation, Extracted = MakeVisible(Short(extracted)) });
    }

    static string Short(string s) {
        s = Regex.Replace(s ?? "", @"\s+", " ").Trim();
        return s.Length > 120 ? s.Substring(0, 117) + "..." : s;
    }

    static string Around(string s, int i, int n) {
        int a = Math.Max(0, i - 30), b = Math.Min(s.Length, i + n + 30);
        return (a > 0 ? "..." : "") + s.Substring(a, b - a) + (b < s.Length ? "..." : "");
    }

    static IEnumerable<int> Points(string s) {
        for (int i = 0; i < s.Length; i++) {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
            else yield return s[i];
        }
    }

    static bool Emoji(int c) { return c >= 0x1F000 || (c >= 0x2190 && c <= 0x2BFF) || c == 0x00A9 || c == 0x00AE || (c >= 0x2000 && c <= 0x2BFF); }

    static bool Hidden(int c, int before, int after) {
        if (c == '\n' || c == '\r' || c == '\t') return false;
        if ((c == 0xFE0F || c == 0xFE0E) && Emoji(before)) return false;
        if (c == 0x200D && Emoji(before) && Emoji(after)) return false;
        if (c >= 0xE0000 && c <= 0xE007F) return true;
        if ((c >= 0xFE00 && c <= 0xFE0F) || (c >= 0xE0100 && c <= 0xE01EF)) return true;
        if (c == 0x3164 || c == 0x115F || c == 0x1160 || c == 0xFFA0 || c == 0x2800 || c == 0x180E) return true;
        if (c > 0xFFFF) {
            var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(c), 0);
            return cat == UnicodeCategory.Format || cat == UnicodeCategory.PrivateUse;
        }
        var k = CharUnicodeInfo.GetUnicodeCategory((char)c);
        return k == UnicodeCategory.Format || k == UnicodeCategory.Control || k == UnicodeCategory.PrivateUse
            || k == UnicodeCategory.Surrogate || k == UnicodeCategory.LineSeparator || k == UnicodeCategory.ParagraphSeparator;
    }

    static void Invisible(string s, List<Reason> reasons) {
        var p = Points(s).ToList();
        var found = new SortedSet<int>();
        int tag = 0;
        for (int i = 0; i < p.Count; i++)
            if (Hidden(p[i], i > 0 ? p[i - 1] : 0, i + 1 < p.Count ? p[i + 1] : 0)) { found.Add(p[i]); if (p[i] >= 0xE0000 && p[i] <= 0xE007F) tag++; }
        if (found.Count == 0) return;
        string joinedList = string.Join(" ", found.Take(8).Select(c => "U+" + c.ToString("X4")));
        if (tag > 0) {
            var hidden = new string(p.Where(c => c >= 0xE0020 && c <= 0xE007E).Select(c => (char)(c - 0xE0000)).ToArray());
            Add(reasons, "invisible", Language.T("screener.ascii_text_hidden_invisible_characters"), hidden);
        }
        Add(reasons, "invisible", Language.T("screener.invisible_characters", "list", joinedList), "");
    }

    public static string MakeVisible(string s) {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var p = Points(s).ToList();
        var sb = new StringBuilder();
        for (int i = 0; i < p.Count; i++) {
            if (Hidden(p[i], i > 0 ? p[i - 1] : 0, i + 1 < p.Count ? p[i + 1] : 0)) sb.Append("{U+" + p[i].ToString("X4") + "}");
            else sb.Append(char.ConvertFromUtf32(p[i]));
        }
        return sb.ToString();
    }

    static int Writer(char c) {
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= 0xC0 && c <= 0x24F)) return 1;
        if (c >= 0x400 && c <= 0x52F) return 2;
        if (c >= 0x370 && c <= 0x3FF) return 3;
        return 0;
    }

    static void Homoglyphs(string s, List<Reason> reasons) {
        foreach (Match m in Regex.Matches(s, @"\w{3,}")) {
            var types = new HashSet<int>(m.Value.Select(Writer).Where(x => x > 0));
            if (types.Count <= 1) continue;
            var sb = new StringBuilder();
            foreach (char ch in m.Value) {
                int sc = Writer(ch);
                sb.Append(sc == 2 ? "{" + ch + " " + Language.T("screener.cyrillic") + "}" : sc == 3 ? "{" + ch + " " + Language.T("screener.greek") + "}" : ch.ToString());
            }
            Add(reasons, "homoglyphs", Language.T("screener.word_mixing_letters_different_alphabets"), sb.ToString());
            return;
        }
    }

    static string CleanUp(string s) {
        var p = Points(s).ToList();
        var sb = new StringBuilder();
        for (int i = 0; i < p.Count; i++)
            if (!Hidden(p[i], i > 0 ? p[i - 1] : 0, i + 1 < p.Count ? p[i + 1] : 0)) sb.Append(char.ConvertFromUtf32(p[i]));
        string r;
        try { r = sb.ToString().Normalize(NormalizationForm.FormKC); } catch { r = sb.ToString(); }
        r = r.ToLowerInvariant().Replace('’', '\'').Replace('`', ' ');
        return Regex.Replace(r, @"[ \t]+", " ");
    }

    static readonly Regex Fence = new Regex(@"^[ ]{0,3}(`{3,}|~{3,})[^\n]*\n[\s\S]*?(^[ ]{0,3}\1[ \t]*$|\z)", RegexOptions.Multiline);

    static string OutsideCode(string s) {
        s = s.Replace("\r\n", "\n");
        s = Fence.Replace(s, "\n");
        return Regex.Replace(s, @"`+[^`\n]*`+", " ");
    }

    static void Html(string s, List<Reason> reasons) {
        var c = Regex.Match(s, @"<!--[\s\S]*?(-->|\z)");
        if (c.Success) Add(reasons, "hidden_html", Language.T("screener.html_comment_not_visible_github"), c.Value);
        foreach (Match m in Regex.Matches(s, @"<\s{0,20}/?\s{0,20}([a-zA-Z][a-zA-Z0-9-]{0,60})\b[^>]{0,2000}>")) {
            string tag = m.Groups[1].Value;
            if (Regex.IsMatch(m.Value, @"\b(hidden|style\s*=|on[a-z]+\s*=|display\s*:|font-size\s*:\s*0|color\s*:)", O))
                Add(reasons, "hidden_html", Language.T("screener.html_hides_colors_text"), m.Value);
            else if (tag.Equals("details", StringComparison.OrdinalIgnoreCase) || tag.Equals("summary", StringComparison.OrdinalIgnoreCase))
                Add(reasons, "hidden_html", Language.T("screener.text_folded_details_github_stays"), m.Value);
            else if (!TagOk.Contains(tag))
                Add(reasons, "html", Language.T("screener.html_outside_simple_formatting_tags", "tag", tag), m.Value);
        }
    }

    static void Markdown(string s, List<Reason> reasons) {
        var rif = Regex.Match(s, @"^[ ]{0,3}\[[^\]\n]*\]:\s*\S.*$", RegexOptions.Multiline);
        if (rif.Success) Add(reasons, "hidden_markdown", Language.T("screener.markdown_reference_line_not_visible"), rif.Value);
        var alt = Regex.Match(s, @"!\[([^\]\n]{60})");
        if (alt.Success) Add(reasons, "hidden_markdown", Language.T("screener.long_text_hidden_image_description"), alt.Groups[1].Value);
    }

    static void CodeBlocks(string s, List<Reason> reasons) {
        foreach (Match m in Regex.Matches(s.Replace("\r\n", "\n"), @"^[ ]{0,3}(`{3,}|~{3,})[ \t]*([^\s`]*)", RegexOptions.Multiline)) {
            string language = m.Groups[2].Value.Trim();
            if (ExecutableLanguages.Contains(language)) { Add(reasons, "code", Language.T("screener.block_code_run", "language", language), m.Value); return; }
        }
    }

    static readonly string[] Shorteners = { "bit.ly", "tinyurl.com", "t.co", "goo.gl", "ow.ly", "is.gd", "buff.ly", "rebrand.ly", "cutt.ly", "shorturl.at", "rb.gy", "tiny.cc" };

    static readonly Regex Download = new Regex(@"^(?:[a-z][a-z0-9+.\-]{0,20}://|www\.)[^/?#\s]{1,300}/[^?#\s]{0,2000}\.(exe|msi|msix|appx|bat|cmd|com|scr|pif|ps1|vbs|vbe|wsf|hta|jar|lnk|reg|dll|iso|img|dmg|pkg|deb|rpm|apk|zip|7z|rar|tar|gz|tgz|bz2|xz)(?:[?#]|$)", O);

    static bool Trusted(string host, IEnumerable<string> trusted) {
        host = host.ToLowerInvariant().TrimEnd('.');
        return trusted.Any(f => host == f || host.EndsWith("." + f));
    }

    static string Host(string url) {
        var m = Regex.Match(url, @"^[a-z][a-z0-9+.\-]*://(?:([^/?#@\s]*)@)?([^/?#:\s]+)", O);
        return m.Success ? m.Groups[2].Value : "";
    }

    static void Link(string s, IEnumerable<string> trusted, List<Reason> reasons) {
        var domains = trusted.Select(f => f.Trim().ToLowerInvariant()).Where(f => f.Length > 0).ToList();
        var schema = Regex.Match(s, @"\b(javascript|vbscript|data|file|ms-[a-z\-]+|search-ms|smb):[^\s)>\]]*", O);
        if (schema.Success && !Regex.IsMatch(schema.Value, @"^data:\s*$", O))
            Add(reasons, "dangerous_link", Language.T("screener.link_opens_program_file_instead"), schema.Value);
        foreach (Match m in Regex.Matches(s, @"\b(?:https?|ftp)://[^\s<>()\[\]""'`]+|\bwww\.[^\s<>()\[\]""'`]+", O)) {
            string url = m.Value.TrimEnd('.', ',', ';', ':', '!', '?');
            string host = Host(url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + url : url);
            if (Download.IsMatch(url)) Add(reasons, "executable_link", Language.T("screener.link_program_archive_download"), url);
            if (url.Contains("@") && Regex.IsMatch(url, @"://[^/]*@")) Add(reasons, "deceptive_link", Language.T("screener.link_user_name_front_site"), url);
            else if (Regex.IsMatch(host, @"^\d{1,3}(\.\d{1,3}){3}$|^\[")) Add(reasons, "deceptive_link", Language.T("screener.link_ip_address_instead_site"), url);
            else if (host.StartsWith("xn--") || host.Contains(".xn--") || host.Any(ch => ch > 127)) Add(reasons, "deceptive_link", Language.T("screener.link_site_name_non_latin"), url);
            else if (Shorteners.Contains(host.ToLowerInvariant())) Add(reasons, "external_link", Language.T("screener.shortened_link_cant_see_where"), url);
            else if (!Trusted(host, domains)) Add(reasons, "external_link", Language.T("screener.link_site_outside_trusted_list", "host", host), url);
        }
        foreach (Match m in Regex.Matches(s, @"\[([^\]\n]{1,300})\]\(\s{0,20}<?([^)\s>]{1,2000})", O)) {
            var seenMatch = Regex.Match(m.Groups[1].Value, @"(?:https?://)?((?:[a-z0-9\-]+\.)+[a-z]{2,})", O);
            if (!seenMatch.Success) continue;
            string real = Host(m.Groups[2].Value);
            if (real.Length > 0 && !real.Equals(seenMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase)
                && !real.EndsWith("." + seenMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                Add(reasons, "deceptive_link", Language.T("screener.link_text_shows_one_site", "real", real), m.Value);
        }
    }

    static void Encoded(string s, List<Reason> reasons) {
        var b64 = Regex.Match(s, @"[A-Za-z0-9+/]{80,}={0,2}");
        if (b64.Success && Regex.IsMatch(b64.Value, "[a-z]") && Regex.IsMatch(b64.Value, "[A-Z]"))
            Add(reasons, "encoded", Language.T("screener.long_piece_base64_may_hide"), b64.Value);
        var pct = Regex.Match(s, @"(%[0-9a-fA-F]{2}){12,}");
        if (pct.Success) Add(reasons, "encoded", Language.T("screener.text_hidden_percent_codes"), pct.Value);
        var esc = Regex.Match(s, @"(\\u[0-9a-fA-F]{4}|\\x[0-9a-fA-F]{2}){6,}");
        if (esc.Success) Add(reasons, "encoded", Language.T("screener.text_hidden_u_x_codes"), esc.Value);
        var ent = Regex.Match(s, @"(&#x?[0-9a-fA-F]+;){5,}");
        if (ent.Success) Add(reasons, "encoded", Language.T("screener.text_hidden_html_entities"), ent.Value);
    }

    const RegexOptions IC = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const string AfterCopy = @"(\.(bak|old|orig|backup|save|tmp|copy|\d+))?";
    const string Data = @"\.(txt|csv|json|ya?ml|xml|ini|toml|cfg|conf|xlsx?|docx?|ods|odt|kdbx?|db|sqlite|zip|7z|rar|bak|dat)";

    static readonly Regex SecretName = new Regex(@"^((?!(example|sample|template|dist)\.env$)(.+)?\.env(\.(?!example$|sample$|template$|dist$)[^.]+)*"
        + @"|id_(rsa|dsa|ecdsa|ed25519)[\w\-]*(?<!\.pub)(\.(?!pub$)[^.]+)*"
        + @"|.+\.(pem|key|pfx|p12|ppk|jks|keystore|kdbx|ovpn)" + AfterCopy
        + @"|(credentials|credenziali|secrets?|segreti|token|chiavi|passwords?|passw[oö]rter|passwort|kennw[oö]rter|contrase[nñ]as|motsdepasse|mots-de-passe)(\.(json|ya?ml|txt|ini|xml|toml|csv))?" + AfterCopy
        + @"|.*(password|passwd|passwort|kennwort|contrase[nñ]a|motdepasse|mot-de-passe|mots-de-passe|credential|credenzial|zugangsdaten|identifiants|credenciales).*" + Data
        + @"|wallet\.dat|login data|(.*[_\-.])?(backup|dump)([_\-.].*)?\.sql(\.gz)?"
        + @"|\.npmrc|\.pypirc|\.netrc|_netrc|\.git-credentials|\.htpasswd|\.pgpass)$", IC);

    static readonly string[][] Keys = {
        new[] { "github", @"\b(gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,})\b" },
        new[] { "gitlab", @"\bglpat-[0-9A-Za-z_\-]{20,}" },
        new[] { "npm", @"\bnpm_[A-Za-z0-9]{36,}\b" },
        new[] { "huggingface", @"\bhf_[A-Za-z0-9]{30,}\b" },
        new[] { "sendgrid", @"\bSG\.[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{30,}" },
        new[] { "discord", @"discord(app)?\.com/api/webhooks/\d{5,}/[A-Za-z0-9_\-]{20,}" },
        new[] { "telegram", @"\b\d{8,12}:AA[A-Za-z0-9_\-]{30,}" },
        new[] { "private", @"-----BEGIN [A-Z ]{0,40}PRIVATE KEY( BLOCK)?-----" },
        new[] { "amazon", @"\b(AKIA|ASIA)[0-9A-Z]{16}\b" },
        new[] { "google", @"\bAIza[0-9A-Za-z_\-]{35}\b" },
        new[] { "slack", @"\bxox[abprs]-[0-9A-Za-z\-]{10,}\b" },
        new[] { "payment", @"\b[sr]k_live_[0-9A-Za-z]{16,}\b" },
        new[] { "ai", @"\bsk-(ant-)?[A-Za-z0-9_\-]{32,}\b" },
        new[] { "jwt", @"\beyJ[A-Za-z0-9_\-]{10,}\.eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\b" } };

    static string Secret { get { return Language.T("common.looks_like_password_key_written"); } }
    static string Variable { get { return Language.T("screener.looks_like_variable_password_key"); } }
        static string AccessUrl { get { return Language.T("screener.looks_like_address_name_password"); } }
    static string Connection { get { return Language.T("screener.looks_like_connection_string_password"); } }
    static string PersonalPathMessage { get { return Language.T("common.contains_personal_path_computer"); } }

    static readonly Regex Assigned = new Regex(@"(?<k>password|passwd|passwort|kennwort|contrase[nñ]a|mot[_\-]?de[_\-]?passe|pwd|secret|api[_\-]?key|access[_\-]?token|auth[_\-]?token|token|access[_\-]?key|private[_\-]?key|(?<![A-Za-z])pass(?=[_\-.""'\s:=]))"
        + @"(?<s>[_\-.][\w\-.]{0,40}|key|value|hash|s)?[""']?\s{0,8}[:=]\s{0,8}(?:""(?<v>[^""\s]{8,})""|'(?<v>[^'\s]{8,})'|`(?<v>[^`\s]{8,})`|(?<v>[^\s""'`,;]{8,}))", IC);
    static readonly Regex NotValue = new Regex(@"[_\-.](name|field|file|path|url|uri|id|label|policy|length|min|max|count|prompt|hint|message|msg|regex|pattern|env|var|ref|type|title|text)$", IC);
    static readonly Regex EnvironmentPattern = new Regex(@"^[ \t]{0,40}(export[ \t]{1,8})?(?<n>[A-Za-z0-9_]{0,60}(PASSWORD|PASSWD|PASSWORT|KENNWORT|SECRET|TOKEN|API_?KEY)[A-Za-z0-9_]{0,60}|([A-Za-z0-9_]{0,60}_)?(PASS|PWD)(_[A-Za-z0-9_]{0,60})?)=(?<v>[^\s#]{8,})", RegexOptions.Multiline | IC);
    static readonly Regex UrlWithPassword = new Regex(@"://(?<u>[^/\s:@]{0,100}):(?<v>[^@\s/]{1,200})@(?<h>[^/\s:?#]{1,255})", RegexOptions.CultureInvariant);
    static readonly Regex ConnectionPassword = new Regex(@"\b(Password|Pwd)\s{0,8}=\s{0,8}(?<v>[^;\s""'<>{}$%]{4,})\s{0,8}(;|$|[""'])", RegexOptions.Multiline | IC);
    static readonly Regex ConnectionKeyPattern = new Regex(@"\b(server|data source|host|user id|uid|database|initial catalog)\s{0,8}=", IC);
    static readonly Regex FakeHost = new Regex(@"^(localhost|127\.0\.0\.1|host|hostname|server|db|database|(.*\.)?example\.(com|org|net)|(.*\.)?esempio\.\w+|.*\.local)$", IC);
    const string SecretNames = @"(key|api|apikey|access|auth|private|secret|segreto|token|password|passwd|pwd|pass|value|valore|chiave|user|utente|account|app|client|id|db|database|here|qui)";
    static readonly Regex Fake = new Regex(@"^(x+|\*+|\.+|(changeme|cambiami)\d{0,4}"
        + @"|(example|esempio|sample|placeholder|segnaposto|dummy|fake|finto|finta|test|testing|prova|demo|foo|foobar|bar|baz)([_\-.]?" + SecretNames + @"){0,3}[_\-.]?\d{0,4}"
        + @"|((la|il)[_\-])?(your|my|tu[oa]|mi[oa])[_\-]?([a-z]{1,20}[_\-]){0,4}" + SecretNames + @"([_\-](here|qui))?\d{0,4}"
        + @"|(pass|password|passwd|pwd|secret|segreto|token)\d{0,4}|abc123(def(456)?)?\d{0,4}|123456\d{0,6}|qwerty\d{0,6}|null|none|true|false"
        + @"|base64(url|encoded|encode|string)?|sha-?\d+|md5(sum)?|utf-?8"
        + @"|\$\{.*\}|\$\(.*\)|\$[A-Za-z_][\w:.]*|<[^>]*>|%[\w.]+%|%[sdrvqx]|%\(\w+\)[sd]|\{\{?[^}]*\}\}?)$", IC);
    static readonly Regex Postpone = new Regex(@"\$[A-Za-z_{(]|%[A-Za-z_]\w*%|%[sdrvqx]\b|%\(", IC);
    static readonly Regex Chain = new Regex(@"^[A-Za-z_]\w*(\.[A-Za-z_]\w*)+$");
    static readonly Regex PathPattern = new Regex(@"\b[A-Za-z]:[\\/]+Users[\\/]+(?!Public\b|Default\b|All Users\b)[^\\/\s""'<>%$\0]{1,100}|(?<![\w.\-/])/(home|Users)/(?!user\b|utente\b|name\b|nome\b|you\b|example\b|esempio\b|runner\b)[A-Za-z0-9._\-]{1,100}/", IC);
    static readonly Regex Email = new Regex(@"\b(?<u>[A-Za-z0-9._%+\-]{1,64})@(?<h>[A-Za-z0-9\-]{1,63}(\.[A-Za-z0-9\-]{1,63}){0,6}\.[A-Za-z]{2,24})\b", RegexOptions.CultureInvariant);
    static readonly Regex NotEmail = new Regex(@"\.(png|jpe?g|gif|svg|webp|ico)$", IC);
    static readonly Regex TaxCode = new Regex(@"\b[A-Z]{6}\d{2}[A-EHLMPR-T]\d{2}[A-Z]\d{3}[A-Z]\b", RegexOptions.CultureInvariant);

    static bool LooksLikeValue(string v) {
        if (Fake.IsMatch(v) || v.IndexOfAny(new[] { '(', ')', '[', ']', '{', '}', '<', '>' }) >= 0 || Postpone.IsMatch(v)) return false;
        if (Chain.IsMatch(v) || Regex.IsMatch(v, @"^([A-Za-z]:[\\/]|\.{0,2}/|\w+://)")) return false;
        if (!v.Any(char.IsLetter)) return false;
        return v.Any(char.IsDigit) || v.IndexOfAny(new[] { '!', '@', '#', '&', '*', '+', '=', '^', '~' }) >= 0 || (v.Length >= 32 && v.All(char.IsLetterOrDigit));
    }

    class Detection { public int From, Length; public string What; }

    static void SearchShape(string text, List<Detection> l) {
        foreach (var k in Keys)
            foreach (Match m in Regex.Matches(text, k[1], RegexOptions.CultureInvariant)) l.Add(new Detection { From = m.Index, Length = m.Length, What = Language.T("screener.looks." + k[0]) });
        foreach (Match m in PathPattern.Matches(text)) l.Add(new Detection { From = m.Index, Length = m.Length, What = PersonalPathMessage });
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length >= 4)
            for (int i = text.IndexOf(profile, StringComparison.OrdinalIgnoreCase); i >= 0; i = text.IndexOf(profile, i + profile.Length, StringComparison.OrdinalIgnoreCase))
                l.Add(new Detection { From = i, Length = profile.Length, What = PersonalPathMessage });
    }

    static List<Detection> Search(string text) {
        var l = new List<Detection>();
        SearchShape(text, l);
        foreach (Match m in Assigned.Matches(text)) {
            string v = m.Groups["v"].Value, name = m.Groups["k"].Value.ToLowerInvariant(), after = m.Groups["s"].Value;
            if (after.Length > 0 && NotValue.IsMatch(after)) continue;
            bool quotes = m.Value.EndsWith("\"") || m.Value.EndsWith("'") || m.Value.EndsWith("`");
            bool dry = quotes && after.Length == 0 && Regex.IsMatch(name, @"^(password|passwd|passwort|kennwort|contrase[nñ]a|mot[_\-]?de[_\-]?passe|pwd|secret|api[_\-]?key|access[_\-]?token|auth[_\-]?token)$");
            if (dry ? !Fake.IsMatch(v) : LooksLikeValue(v)) l.Add(new Detection { From = m.Groups["v"].Index, Length = v.Length, What = Secret });
        }
        foreach (Match m in EnvironmentPattern.Matches(text)) {
            string v = m.Groups["v"].Value, name = m.Groups["n"].Value;
            if (Fake.IsMatch(v) || v.IndexOfAny(new[] { '(', '{', '<' }) >= 0 || Postpone.IsMatch(v)) continue;
            if (Regex.IsMatch(name, @"(^|_)(PASS|PWD)(_|$)", IC) && !LooksLikeValue(v)) continue;
            if (name == name.ToUpperInvariant() || !Regex.IsMatch(v, @"^[A-Z]?[a-z]+$")) l.Add(new Detection { From = m.Groups["v"].Index, Length = v.Length, What = Variable });
        }
        foreach (Match m in UrlWithPassword.Matches(text)) {
            string v = m.Groups["v"].Value;
            if (!Fake.IsMatch(v) && !FakeHost.IsMatch(m.Groups["h"].Value) && v.IndexOfAny(new[] { '{', '<' }) < 0 && !Postpone.IsMatch(v)) l.Add(new Detection { From = m.Groups["v"].Index, Length = v.Length, What = AccessUrl });
        }
        foreach (Match m in ConnectionPassword.Matches(text)) {
            string v = m.Groups["v"].Value;
            int a = text.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1, b = text.IndexOf('\n', m.Index);
            string line = text.Substring(a, (b < 0 ? text.Length : b) - a);
            if (!Fake.IsMatch(v) && ConnectionKeyPattern.IsMatch(line)) l.Add(new Detection { From = m.Groups["v"].Index, Length = v.Length, What = Connection });
        }
        return l;
    }

    static int LineOf(string s, int index) { int n = 1; for (int i = 0; i < index; i++) if (s[i] == '\n') n++; return n; }

    public static List<Finding> CheckName(string path) {
        var l = new List<Finding>();
        var pieces = (path ?? "").Replace('\\', '/').Split('/');
        if (SecretName.IsMatch(pieces[pieces.Length - 1])) l.Add(new Finding { File = path, What = Language.T("common.name_says_file_keys_passwords") });
        if (pieces.Take(pieces.Length - 1).Any(x => x.Equals(".ssh", StringComparison.OrdinalIgnoreCase) || x.Equals(".aws", StringComparison.OrdinalIgnoreCase) || x.Equals(".gnupg", StringComparison.OrdinalIgnoreCase)))
            l.Add(new Finding { File = path, What = Language.T("screener.folder_keys") });
        string queue = pieces.Length < 2 ? "" : pieces[pieces.Length - 2] + "/" + pieces[pieces.Length - 1];
        if (queue.Equals(".docker/config.json", StringComparison.OrdinalIgnoreCase) || queue.Equals(".kube/config", StringComparison.OrdinalIgnoreCase))
            l.Add(new Finding { File = path, What = Language.T("screener.programs_sign_file") });
        return l;
    }

    public static List<Finding> CheckText(string path, string text) {
        text = text ?? "";
        return Search(text).OrderBy(x => x.From).GroupBy(x => x.What).Select(g => new Finding { File = path, Line = LineOf(text, g.First().From), What = g.Key }).ToList();
    }

    public static string Mask(string text, out int howMany) {
        howMany = 0;
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var sb = new StringBuilder();
        int a = 0;
        foreach (var t in Search(text).OrderBy(x => x.From)) {
            if (t.From < a) continue;
            sb.Append(text, a, t.From - a).Append(Language.Fixed("screener.covered"));
            a = t.From + t.Length;
            howMany++;
        }
        return sb.Append(text, a, text.Length - a).ToString();
    }

    public static List<Finding> Notices(string path, byte[] b) {
        var l = new List<Finding>();
        if (b == null) return l;
        var u = Binary(b) ? Utf16(b) : null;
        if (Binary(b) && u == null) return l;
        string text = u != null ? u.GetString(b) : Text(b);
        foreach (Match m in Email.Matches(text)) {
            string h = m.Groups["h"].Value.ToLowerInvariant(), n = m.Groups["u"].Value.ToLowerInvariant();
            if (h.EndsWith("noreply.github.com") || FakeHost.IsMatch(h) || NotEmail.IsMatch(h) || n == "git" || n == "noreply" || n.StartsWith("no-reply")) continue;
            l.Add(new Finding { File = path, Line = LineOf(text, m.Index), What = Language.T("screener.contains_email_address") });
            break;
        }
        var cf = TaxCode.Match(text);
        if (cf.Success) l.Add(new Finding { File = path, Line = LineOf(text, cf.Index), What = Language.T("screener.contains_italian_tax_code") });
        return l;
    }

    public static bool Binary(byte[] b) {
        if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) return false;
        for (int i = 0; i < Math.Min(b.Length, 8192); i++) if (b[i] == 0) return true;
        return false;
    }

    public static string Text(byte[] b) {
        using (var r = new System.IO.StreamReader(new System.IO.MemoryStream(b), Encoding.UTF8, true)) return r.ReadToEnd();
    }

    static Encoding Utf16(byte[] b) {
        int n = Math.Min(b.Length, 8192) & ~1, even = 0, odd = 0;
        if (n < 4) return null;
        for (int i = 0; i < n; i += 2) { if (b[i] == 0) even++; if (b[i + 1] == 0) odd++; }
        int pairs = n / 2;
        if (odd * 10 > pairs * 4 && even * 20 < pairs) return Encoding.Unicode;
        if (even * 10 > pairs * 4 && odd * 20 < pairs) return Encoding.BigEndianUnicode;
        return null;
    }

    const int Piece = 32 * 1024 * 1024, Overlapping = 4096;

    static void SearchInBinary(string path, byte[] b, List<Finding> l) {
        var latin = Encoding.GetEncoding(28591);
        var seen = new HashSet<string>();
        for (long source = 0; source < b.Length; source += Piece - Overlapping) {
            int howMany = (int)Math.Min(Piece, b.Length - source), n = 0;
            var withoutZeros = new byte[howMany];
            for (int i = 0; i < howMany; i++) if (b[source + i] != 0) withoutZeros[n++] = b[source + i];
            var found = new List<Detection>();
            SearchShape(latin.GetString(b, (int)source, howMany), found);
            SearchShape(latin.GetString(withoutZeros, 0, n), found);
            foreach (var t in found) if (seen.Add(t.What)) l.Add(new Finding { File = path, What = t.What });
            if (source + howMany >= b.Length) break;
        }
    }

    public static List<Finding> CheckContent(string path, byte[] b) {
        var l = new List<Finding>();
        if (b == null) return l;
        var u = Binary(b) ? Utf16(b) : null;
        if (u != null) return CheckText(path, u.GetString(b));
        if (Binary(b)) SearchInBinary(path, b, l);
        else l = CheckText(path, Text(b));
        return l;
    }

    public static List<Finding> CheckFile(string path, byte[] b) {
        var l = CheckName(path);
        l.AddRange(CheckContent(path, b));
        return l;
    }
}
