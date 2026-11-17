using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static class Language {
    public static readonly string[] Codes = { "it", "en", "fr", "es", "de" };
    static readonly string[] Names = { "Italiano", "English", "Français", "Español", "Deutsch" };
    static readonly string[] Cultures = { "it-IT", "en-US", "fr-FR", "es-ES", "de-DE" };
    public const string DefaultCode = "it", Fallback = "en";

    static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
    static volatile string current = DefaultCode;
    static volatile CultureInfo culture = new CultureInfo("it-IT");

    public static string Code { get { return current; } }
    public static bool Valid(string code) { return code != null && Array.IndexOf(Codes, code) >= 0; }
    public static string NameOf(string code) { int i = Array.IndexOf(Codes, code); return i < 0 ? code : Names[i]; }

    public static string WindowsFrom() { return CultureFrom(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName); }
    public static string CultureFrom(string twoLetter) { return Valid(twoLetter) ? twoLetter : Fallback; }

    public static string ChoiceFile(string folder) { return Path.Combine(folder, DataStore.LanguageFile); }

    static string Saved(string folder) {
        try {
            string f = Legacy.Existing(ChoiceFile(folder), Path.Combine(folder, Legacy.LanguageFile));
            if (!File.Exists(f)) return null;
            string v = File.ReadAllText(f).Trim();
            return Valid(v) ? v : "";
        } catch (Exception ex) { DataStore.Log("language: " + ex.GetType().Name); return ""; }
    }

    public static bool ToChoose(string folder, bool dataOfBefore) { return Saved(folder) == null && !dataOfBefore; }

    public static void Start(string commandFrom, string folder, bool dataOfBefore) {
        if (Valid(commandFrom)) { Use(commandFrom); return; }
        string saved = Saved(folder);
        Use(Valid(saved) ? saved : saved != null || dataOfBefore ? DefaultCode : WindowsFrom());
    }

    public static void Use(string code) {
        if (!Valid(code)) return;
        culture = new CultureInfo(Cultures[Array.IndexOf(Codes, code)]);
        current = code;
    }

    public static void Choose(string code, string folder) {
        if (!Valid(code)) return;
        Use(code);
        try {
            Directory.CreateDirectory(folder);
            File.WriteAllText(ChoiceFile(folder), code, new UTF8Encoding(false));
        } catch (Exception ex) { DataStore.Log("language: " + ex.GetType().Name); }
    }

    static Dictionary<string, string> Table(string code) {
        lock (tables) {
            Dictionary<string, string> t;
            if (tables.TryGetValue(code, out t)) return t;
            t = new Dictionary<string, string>();
            try {
                using (var s = Program.Resource("locales." + code + ".txt"))
                    if (s != null) using (var r = new StreamReader(s, new UTF8Encoding(false))) t = Read(r.ReadToEnd());
            } catch (Exception ex) { DataStore.Log("language " + code + ": " + ex.Message); }
            tables[code] = t;
            return t;
        }
    }

    public static Dictionary<string, string> Read(string text) {
        var t = new Dictionary<string, string>();
        foreach (var line in text.Split('\n')) {
            string r = line.TrimEnd('\r');
            if (r.Length == 0 || r[0] == '#') continue;
            int i = r.IndexOf('=');
            if (i <= 0) continue;
            t[r.Substring(0, i)] = r.Substring(i + 1).Replace("\\n", "\n");
        }
        return t;
    }

    static string Search(string key) {
        string v;
        if (Table(current).TryGetValue(key, out v)) return v;
        if (Table(Fallback).TryGetValue(key, out v)) return v;
        if (Table(DefaultCode).TryGetValue(key, out v)) return v;
        lock (notFound) notFound.Add(key);
        return key;
    }

    static readonly HashSet<string> notFound = new HashSet<string>();
    public static List<string> NotFound() { lock (notFound) return new List<string>(notFound); }
    public static void EmptyNotFound() { lock (notFound) notFound.Clear(); }

    public static string T(string key, params object[] pairs) { return Fill(Search(key), pairs); }

    public static string N(string key, long n, params object[] pairs) {
        bool one = current == "fr" ? n == 0 || n == 1 : n == 1;
        var args = new object[pairs.Length + 2];
        args[0] = "n"; args[1] = Number(n);
        Array.Copy(pairs, 0, args, 2, pairs.Length);
        return T(key + (one ? ".one" : ".other"), args);
    }

    public static string Fixed(string key, params object[] pairs) {
        string v;
        return Fill(Table(DefaultCode).TryGetValue("fixed." + key, out v) ? v : key, pairs);
    }

    public static string Number(long n) { return n.ToString("N0", culture); }

    public static string Fill(string text, object[] pairs) {
        if (pairs == null || pairs.Length < 2 || text.IndexOf('{') < 0) return text;
        var sb = new StringBuilder(text.Length + 16);
        int i = 0;
        while (i < text.Length) {
            char c = text[i];
            int end = c == '{' ? text.IndexOf('}', i + 1) : -1;
            if (end < 0) { sb.Append(c); i++; continue; }
            string name = text.Substring(i + 1, end - i - 1);
            bool put = false;
            for (int k = 0; k + 1 < pairs.Length; k += 2)
                if (string.Equals(pairs[k] as string, name)) { sb.Append(Convert.ToString(pairs[k + 1], CultureInfo.InvariantCulture)); put = true; break; }
            if (put) i = end + 1; else { sb.Append(c); i++; }
        }
        return sb.ToString();
    }

    public static IEnumerable<string> Placeholder(string text) {
        int i = 0;
        while ((i = text.IndexOf('{', i)) >= 0) {
            int end = text.IndexOf('}', i);
            if (end < 0) yield break;
            yield return text.Substring(i + 1, end - i - 1);
            i = end + 1;
        }
    }
    public static IEnumerable<string> Keys(string code) { return Table(code).Keys; }
    public static string Raw(string code, string key) { string v; return Table(code).TryGetValue(key, out v) ? v : null; }
    public static Dictionary<string, string> TableForTest(string code) { return Table(code); }
}
