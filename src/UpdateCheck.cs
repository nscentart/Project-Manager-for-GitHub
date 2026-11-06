using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public static class UpdateCheck {
    public static string Api = "https://api.github.com/repos/nscentart/Project-Manager-for-GitHub/releases/latest";
    const string GoodAddressPattern = "https://github.com/nscentart/Project-Manager-for-GitHub/";
    public const string LastPage = GoodAddressPattern + "releases/latest";
    const string Asset = "ProjectManager.exe";
    public static int WaitMs = 20000, TimeMs = 10000;
    public static Action<string> OpenBrowser;

    public static bool GoodAddress(string url) {
        Uri u;
        return url != null && Regex.IsMatch(url, @"\A" + Regex.Escape(GoodAddressPattern) + @"releases/(latest|tag/[A-Za-z0-9._-]+)\z")
            && Uri.TryCreate(url, UriKind.Absolute, out u) && u.AbsoluteUri == url;
    }

    public static string DigestFrom(object digest) {
        var m = Regex.Match(digest as string ?? "", @"\Asha256:([0-9A-Fa-f]{64})\z");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    public static List<string> FromNotes(string notes) {
        return Regex.Matches(notes ?? "", @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])").Cast<Match>().Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();
    }

    static Version ParseVersion(string text) {
        Version v;
        return Version.TryParse(text.Split('.').Length == 2 ? text + ".0" : text, out v) ? v : null;
    }
    public static Version Mine() { return ParseVersion(Program.Version); }

    public static NewVersion Search() { return Search(Mine()); }

    public static NewVersion Search(Version ownVersion) {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        var req = (HttpWebRequest)WebRequest.Create(Api);
        req.Method = "GET";
        req.UserAgent = "ProjectManager/" + Program.Version;
        req.Accept = "application/vnd.github+json";
        req.AllowAutoRedirect = false;
        req.Timeout = TimeMs;
        req.ReadWriteTimeout = TimeMs;
        string json;
        try {
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) {
                if ((int)r.StatusCode >= 300) throw new Exception(Language.T("updates.github_not_responding", "msg", (int)r.StatusCode));
                json = sr.ReadToEnd();
            }
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h != null && h.StatusCode == HttpStatusCode.NotFound) throw new Exception(Language.T("updates.releases_page_github_not_public"));
            throw new Exception(Language.T("updates.github_not_responding", "msg", ex.Message.TrimEnd('.', ' ')));
        } catch (IOException ex) {
            throw new Exception(Language.T("updates.github_not_responding", "msg", ex.Message.TrimEnd('.', ' ')));
        }
        try { return ResponseFrom(json, ownVersion); }
        catch (Exception ex) { DataStore.Log("updates: " + ex.Message); throw new Exception(Language.T("github.github_gave_answer_dont_understand")); }
    }

    public static NewVersion ResponseFrom(string json, Version ownVersion) {
        var d = (Dictionary<string, object>)new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json);
        string tag = Convert.ToString(d["tag_name"]).TrimStart('v', 'V');
        var v = ParseVersion(tag);
        if (v == null || v <= ownVersion) return null;
        bool exe = false; string digest = null;
        foreach (var o in (IEnumerable)d["assets"]) {
            var a = (Dictionary<string, object>)o;
            if (!string.Equals(Convert.ToString(a["name"]), Asset, StringComparison.OrdinalIgnoreCase)) continue;
            exe = true;
            if (a.ContainsKey("digest")) digest = DigestFrom(a["digest"]);
        }
        if (!exe) return null;
        string page = d.ContainsKey("html_url") ? Convert.ToString(d["html_url"]) : "";
        string notes = d.ContainsKey("body") ? d["body"] as string ?? "" : "";
        var labels = FromNotes(notes);
        return new NewVersion {
            Version = v, Label = tag, Note = notes, Page = GoodAddress(page) ? page : LastPage,
            Fingerprint = digest ?? (labels.Count == 1 ? labels[0] : null), DifferentFingerprint = digest != null ? labels.Any(x => x != digest) : labels.Count > 1
        };
    }

    public static Thread CheckAtStartup(Func<bool> disabled, Func<string> skipped, Action<NewVersion> warn) {
        if (disabled()) return null;
        var t = new Thread(() => {
            try {
                Thread.Sleep(WaitMs);
                if (disabled()) return;
                var n = Search();
                if (n == null || n.Label == skipped()) return;
                warn(n);
            } catch (Exception ex) { DataStore.Log("updates: " + ex.Message); }
        }) { IsBackground = true };
        t.Start();
        return t;
    }

    public static void Open(string page) {
        string p = GoodAddress(page) ? page : LastPage;
        if (OpenBrowser != null) { OpenBrowser(p); return; }
        try { Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), p); }
        catch (Exception ex) { DataStore.Log("updates: " + ex.Message); }
    }

    public static string CleanNotes(string notes) {
        string t = (notes ?? "").Replace("\r\n", "\n").Replace("**", "").Replace("`", ""), before;
        do {
            before = t;
            t = Regex.Replace(t, @"<!--.*?-->|<[!?][^<>]*>|</?[A-Za-z][A-Za-z0-9-]*(\s[^<>]*)?/?>", "", RegexOptions.Singleline);
        } while (t != before);
        var lines = t.Split('\n').Select(r => Screener.MakeVisible(r));
        return Regex.Replace(string.Join("\r\n", lines), @"(\r\n){3,}", "\r\n\r\n").Trim();
    }
}
