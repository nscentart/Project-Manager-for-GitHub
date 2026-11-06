using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public static class GitHub {
    const string RealApiBase = "https://api.github.com";
    static string ApiBase = RealApiBase;
    static string token;
    static bool stored;
    public static string Login;

    public static bool Connected { get { return token != null; } }

    public static string SignIn(GitHubAccount c) {
        Exit();
        if (c.Origin == AccountAccess.OriginBrowser) {
            string t = CredentialStore.Read(CredentialStore.NameOf(c.Login));
            if (t == null) throw new AccessException(Language.T("github.access_no_longer_computer_reconnect", "login", c.Login));
            Use(t, c.Login);
            stored = true;
            return Login;
        }
        Use(GitFrom(c.Origin == AccountAccess.OriginGit ? c.Login : null), null);
        Login = LoginOf();
        return Login;
    }

    public static void Use(string newValue, string login) { token = newValue; Login = login; stored = false; }
    public static void Exit() { token = null; Login = null; stored = false; }

    public static void OnTestServer(int gateway) { ApiBase = "http://127.0.0.1:" + gateway; }
    public static void OnRealGitHub() { ApiBase = RealApiBase; }

    public static Client Writer() {
        if (token == null) throw new WriteException(Language.T("github.no_access_github_reconnect_account"));
        return Client.Real(token, stored);
    }

    public static string LoginOf() {
        var u = (Dictionary<string, object>)Read(ApiBase + "/user");
        return Convert.ToString(u["login"]);
    }

    public static string GitFrom(string login) {
        string git = AccountAccess.FindGit();
        if (git == null) throw new AccessException(Language.T("github.no_git_computer_connect_account"));
        var psi = new ProcessStartInfo(git, "credential fill") {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8 };
        string output;
        using (var p = Process.Start(psi)) {
            bool goodName = !string.IsNullOrEmpty(login) && Regex.IsMatch(login, "^[A-Za-z0-9-]+$");
            p.StandardInput.Write("protocol=https\nhost=github.com\n" + (goodName ? "username=" + login + "\n" : "") + "\n");
            p.StandardInput.Close();
            var err = p.StandardError.ReadToEndAsync();
            output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(5 * 60 * 1000)) { try { p.Kill(); } catch { } throw new Exception(Language.T("github.github_sign_not_come_through")); }
            err.Wait(1000);
            if (p.ExitCode != 0) throw new Exception(Language.T("github.git_credential_manager_not_give", "exitcode", p.ExitCode));
        }
        foreach (var line in output.Split('\n'))
            if (line.StartsWith("password=")) {
                string t = line.Substring(9).Trim();
                if (t.Length > 0) return t;
            }
        throw new Exception(Language.T("github.git_credential_manager_no_access"));
    }

    static object Read(string url) { string next; return Read(url, out next); }

    static object Read(string url, out string next) {
        if (!url.StartsWith(ApiBase + "/")) throw new Exception("address outside api.github.com");
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "GET";
        req.UserAgent = "ProjectManager/" + Program.Version;
        req.Accept = "application/vnd.github+json";
        req.Headers["X-GitHub-Api-Version"] = "2022-11-28";
        req.Headers["Authorization"] = "Bearer " + token;
        req.AllowAutoRedirect = false;
        req.Timeout = 30000;
        try {
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) {
                if ((int)r.StatusCode >= 300) throw new GitHubException((int)r.StatusCode, Language.T("github.moved_github_try_again_after"));
                next = Next(r.Headers["Link"]);
                return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(sr.ReadToEnd());
            }
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h == null) throw new NoNetworkException(Language.T("github.github_not_responding_check_internet"));
            int c = (int)h.StatusCode;
            if (c == 401) throw new GitHubException(c, Language.T("github.github_no_longer_recognizes_access"));
            if (c == 429 || (c == 403 && (h.Headers["Retry-After"] != null || h.Headers["X-RateLimit-Remaining"] == "0")))
                throw new GitHubException(c, Language.T("github.github_limited_requests_try_again")) { Limit = true };
            if (c == 403) throw new GitHubException(c, Language.T("github.github_not_give_permission_read"));
            if (c == 404) throw new GitHubException(c, Language.T("github.no_longer_github_dont_permission"));
            throw new GitHubException(c, Language.T("github.github_answered_error", "c", c));
        }
    }

    static string Next(string link) {
        if (string.IsNullOrEmpty(link)) return null;
        var m = Regex.Match(link, "<([^>]+)>;\\s*rel=\"next\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    static List<Dictionary<string, object>> All(string url) {
        var allRepos = new List<Dictionary<string, object>>();
        for (int page = 0; url != null && page < 100; page++) {
            string next;
            foreach (var o in (IEnumerable)Read(url, out next)) allRepos.Add((Dictionary<string, object>)o);
            url = next;
        }
        return allRepos;
    }

    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }

    static bool Admin(Dictionary<string, object> d) {
        object p; if (!d.TryGetValue("permissions", out p)) return false;
        var m = p as Dictionary<string, object>; object a;
        return m != null && m.TryGetValue("admin", out a) && a is bool && (bool)a;
    }

    public static List<string> Organizations() {
        return All(ApiBase + "/user/orgs?per_page=100").Select(d => S(d, "login")).ToList();
    }

    public static List<RepoInfo> Repo() {
        return All(ApiBase + "/user/repos?per_page=100&affiliation=owner,collaborator,organization_member&sort=full_name")
            .Select(d => new RepoInfo {
                Name = S(d, "full_name"), Description = S(d, "description"),
                Owner = S((Dictionary<string, object>)d["owner"], "login"), Branch = S(d, "default_branch"),
                Private = d.ContainsKey("private") && d["private"] is bool && (bool)d["private"],
                Organization = S((Dictionary<string, object>)d["owner"], "type") == "Organization",
                Mine = Organization.Mine(S((Dictionary<string, object>)d["owner"], "login"), Login, Admin(d)) }).ToList();
    }

    public static List<WorkItem> News(string repo, string since) {
        string queue = string.IsNullOrEmpty(since) ? "" : "&since=" + Uri.EscapeDataString(since);
        var items = new List<WorkItem>();
        foreach (var d in All(ApiBase + "/repos/" + repo + "/issues?state=all&per_page=100&sort=updated&direction=asc" + queue)) {
            int n = Convert.ToInt32(d["number"]);
            var e = new WorkItem {
                Key = "issue:" + S(d, "id"), Repo = repo, Number = n,
                Type = d.ContainsKey("pull_request") && d["pull_request"] != null ? "pull" : "issue",
                Title = S(d, "title"), Text = S(d, "body"),
                Author = d["user"] == null ? "" : S((Dictionary<string, object>)d["user"], "login"),
                Labels = string.Join(",", ((IEnumerable)d["labels"]).Cast<Dictionary<string, object>>().Select(l => S(l, "name"))),
                Url = "https://github.com/" + repo + "/issues/" + n,
                Created = S(d, "created_at"), Updated = S(d, "updated_at"), State = S(d, "state") };
            items.Add(e);
        }
        foreach (var d in All(ApiBase + "/repos/" + repo + "/issues/comments?per_page=100&sort=updated&direction=asc" + queue)) {
            var m = Regex.Match(S(d, "issue_url"), "/issues/(\\d+)$");
            int n = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            items.Add(new WorkItem {
                Key = "comment:" + S(d, "id"), Repo = repo, Number = n, Type = "comment",
                Text = S(d, "body"),
                Author = d["user"] == null ? "" : S((Dictionary<string, object>)d["user"], "login"),
                Url = "https://github.com/" + repo + "/issues/" + n + "#issuecomment-" + S(d, "id"),
                Created = S(d, "created_at"), Updated = S(d, "updated_at") });
        }
        return items;
    }

    static Dictionary<string, object> Object(object o, string k) {
        object v;
        return o is Dictionary<string, object> && ((Dictionary<string, object>)o).TryGetValue(k, out v) ? v as Dictionary<string, object> : null;
    }

    static Dictionary<string, object> CeIf(string url) {
        try { return (Dictionary<string, object>)Read(url); }
        catch (GitHubException e) { if (e.Code == 404) return null; throw; }
    }

    static string LatestCommitDate(string url) {
        try {
            foreach (var o in (IEnumerable)Read(url)) return S(Object(Object(o, "commit"), "committer") ?? new Dictionary<string, object>(), "date");
            return "";
        } catch (GitHubException e) { if (e.Code == 409) return ""; throw; }
    }

    static string HighestTag(List<string> tag) {
        string best = null; long[] vBest = null;
        foreach (var t in tag) {
            var m = Regex.Match(t, @"^v?(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var v = m.Groups[1].Value.Split('.').Select(x => { long n; return long.TryParse(x, out n) ? n : 0; }).ToArray();
            bool more = vBest == null;
            for (int i = 0; !more && i < Math.Max(v.Length, vBest.Length); i++) {
                long a = i < v.Length ? v[i] : 0, b = i < vBest.Length ? vBest[i] : 0;
                if (a != b) { more = a > b; break; }
            }
            if (more) { best = t; vBest = v; }
        }
        return best ?? tag.FirstOrDefault() ?? "";
    }

    static void TryRead(string repo, string what, Action readFacts, Action<string> error) {
        try { readFacts(); error(null); }
        catch (GitHubException ex) { DataStore.Log("overview " + repo + " " + what + ": " + ex.Message); error(ex.Message); }
        catch (Exception ex) {
            DataStore.Log("overview " + repo + " " + what + ": " + ex.Message);
            error(ex is NoNetworkException ? ex.Message : Language.T("github.github_gave_answer_dont_understand"));
        }
    }

    public static RepoOverview Overview(RepoInfo r, RepoOverview oldOverview) {
        var p = oldOverview == null ? new RepoOverview() : oldOverview.Copy();
        string b = ApiBase + "/repos/" + r.Name;
        int succeeded = 0;
        TryRead(r.Name, "release", () => {
            var d = CeIf(b + "/releases/latest");
            p.ReleaseTag = d == null ? "" : S(d, "tag_name");
            p.ReleaseDate = d == null ? "" : S(d, "published_at");
            p.ReleaseRead = true; succeeded++;
        }, e => p.ReleaseError = e);
        TryRead(r.Name, "tag", () => {
            p.LatestTag = HighestTag(((IEnumerable)Read(b + "/tags?per_page=100")).Cast<Dictionary<string, object>>().Select(x => S(x, "name")).ToList());
            p.TagRead = true; succeeded++;
        }, e => p.TagError = e);
        if (p.ReleaseError == null && string.IsNullOrEmpty(p.ReleaseTag)) { p.CommitsSinceRelease = -1; p.CommitsSinceReleaseError = null; }
        else if (!string.IsNullOrEmpty(p.ReleaseTag))
            TryRead(r.Name, "commits since the release", () => {
                string branch = string.IsNullOrEmpty(r.Branch) ? "HEAD" : r.Branch;
                var d = (Dictionary<string, object>)Read(b + "/compare/" + Uri.EscapeDataString(p.ReleaseTag) + "..." + Uri.EscapeDataString(branch) + "?per_page=1");
                p.CommitsSinceRelease = Convert.ToInt32(d["ahead_by"]); succeeded++;
            }, e => p.CommitsSinceReleaseError = e);
        TryRead(r.Name, "last commit", () => {
            p.LatestCommit = LatestCommitDate(b + "/commits?per_page=1");
            p.CommitRead = true; succeeded++;
        }, e => p.CommitError = e);
        TryRead(r.Name, "collaborators", () => {
            p.Collaborators = All(b + "/collaborators?per_page=100").Select(x => new Collaborator { Name = S(x, "login"), Role = S(x, "role_name") }).ToList();
            succeeded++;
        }, e => p.CollaboratorsError = e);
        TryRead(r.Name, "license", () => {
            var d = CeIf(b + "/license");
            var l = d == null ? null : Object(d, "license");
            p.OtherLicense = d != null && (l == null || S(l, "spdx_id") == "NOASSERTION");
            p.License = d == null ? "" : p.OtherLicense ? NameFromFile(S(d, "content")) : S(l, "name");
            p.LicenseRead = true; succeeded++;
        }, e => p.LicenseError = e);
        string path = null;
        TryRead(r.Name, "README", () => {
            var d = CeIf(b + "/readme");
            p.Readme = d != null;
            p.ReadmeCopyright = d != null && HasCopyright(S(d, "content"));
            if (d == null) p.ReadmeDate = "";
            path = d == null ? null : S(d, "path");
            p.ReadmeRead = true; succeeded++;
        }, e => p.ReadmeError = e);
        if (path != null)
            TryRead(r.Name, "README date", () => {
                p.ReadmeDate = LatestCommitDate(b + "/commits?per_page=1&path=" + Uri.EscapeDataString(path));
                succeeded++;
            }, e => p.ReadmeDateError = e);
        else p.ReadmeDateError = null;
        if (succeeded > 0) p.ReadAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        return p;
    }

    static long Tally(Dictionary<string, object> d, string k) {
        object v;
        if (!d.TryGetValue(k, out v)) throw new FormatException(k);
        long n;
        if (v is int) n = (int)v; else if (v is long) n = (long)v; else throw new FormatException(k);
        if (n < 0) throw new FormatException(k);
        return n;
    }

    static object Traffic(string url) {
        try { return Read(url); }
        catch (GitHubException e) {
            if (e.Code == 403 && !e.Limit) throw new GitHubException(403, Language.T("github.github_only_shows_numbers_people"));
            throw;
        }
    }

    static List<TrafficDay> DaysOf(Dictionary<string, object> d, string domainList) {
        var g = new List<TrafficDay>();
        foreach (var o in (IEnumerable)d[domainList]) {
            var x = (Dictionary<string, object>)o;
            string t = S(x, "timestamp");
            if (!Regex.IsMatch(t, @"^\d{4}-\d{2}-\d{2}")) throw new FormatException(domainList);
            g.Add(new TrafficDay { Day = t.Substring(0, 10), Count = Tally(x, "count"), Uniques = Tally(x, "uniques") });
        }
        return g;
    }

    static List<TrafficEntry> EntriesOf(object response, string name) {
        var l = new List<TrafficEntry>();
        foreach (var o in (IEnumerable)response) {
            var x = (Dictionary<string, object>)o;
            l.Add(new TrafficEntry { Name = S(x, name), Title = S(x, "title"), Count = Tally(x, "count"), Uniques = Tally(x, "uniques") });
        }
        return l;
    }

    public static RepoStats ReadStats(RepoInfo r, RepoStats oldStats) {
        var n = oldStats == null ? new RepoStats() : oldStats.Copy();
        string b = ApiBase + "/repos/" + r.Name;
        int succeeded = 0;
        TryRead(r.Name, "views", () => {
            var d = (Dictionary<string, object>)Traffic(b + "/traffic/views");
            var g = DaysOf(d, "views");
            long tot = Tally(d, "count"), uni = Tally(d, "uniques");
            n.ViewDays = g; n.Views = tot; n.UniqueViews = uni; n.ViewsRead = true; succeeded++;
        }, e => n.ViewsError = e);
        TryRead(r.Name, "clones", () => {
            var d = (Dictionary<string, object>)Traffic(b + "/traffic/clones");
            var g = DaysOf(d, "clones");
            long tot = Tally(d, "count"), uni = Tally(d, "uniques");
            n.CloneDays = g; n.Clones = tot; n.UniqueClones = uni; n.ClonesRead = true; succeeded++;
        }, e => n.ClonesError = e);
        TryRead(r.Name, "referrers", () => {
            n.Referrers = EntriesOf(Traffic(b + "/traffic/popular/referrers"), "referrer"); n.ReferrersRead = true; succeeded++;
        }, e => n.ReferrersError = e);
        TryRead(r.Name, "pages", () => {
            n.Pages = EntriesOf(Traffic(b + "/traffic/popular/paths"), "path"); n.PagesRead = true; succeeded++;
        }, e => n.PagesError = e);
        TryRead(r.Name, "download", () => {
            var v = new List<DownloadedRelease>();
            long tot = 0;
            foreach (var d in All(b + "/releases?per_page=100")) {
                object draft;
                if (d.TryGetValue("draft", out draft) && draft is bool && (bool)draft) continue;
                var x = new DownloadedRelease { Tag = S(d, "tag_name") };
                foreach (var o in (IEnumerable)d["assets"]) {
                    var at = (Dictionary<string, object>)o;
                    long q = Tally(at, "download_count");
                    x.Assets.Add(new DownloadedAsset { Name = S(at, "name"), Downloads = q });
                    x.Downloads += q;
                }
                tot += x.Downloads;
                v.Add(x);
            }
            n.Releases = v; n.Downloads = tot; n.DownloadsRead = true; succeeded++;
        }, e => n.DownloadsError = e);
        TryRead(r.Name, "stars and forks", () => {
            var d = (Dictionary<string, object>)Read(b);
            long stars = Tally(d, "stargazers_count"), copies = Tally(d, "forks_count"), oss = Tally(d, "subscribers_count");
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (n.RepoRead && stars != n.Stars) { n.StarsBefore = n.Stars; n.StarsChanged = today; }
            if (n.RepoRead && copies != n.Forks) { n.ForksBefore = n.Forks; n.ForksChanged = today; }
            n.Stars = stars; n.Forks = copies; n.Watchers = oss; n.RepoRead = true; succeeded++;
        }, e => n.RepoError = e);
        if (succeeded > 0) n.ReadAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        return n;
    }

    static string NameFromFile(string base64) {
        try {
            string t = Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(base64 ?? "", @"\s+", "")));
            string r = t.Split('\n').Select(x => x.Trim(' ', '\t', '\r', '#', '*', '=')).FirstOrDefault(x => x.Length > 0) ?? "";
            if (r.Length >= 3 && r.Length <= 60 && Regex.IsMatch(r, "licen[sz]", RegexOptions.IgnoreCase)) return r;
        } catch { }
        return Language.T("github.other_license");
    }

    static bool HasCopyright(string base64) {
        try {
            string t = Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(base64 ?? "", @"\s+", "")));
            return Regex.IsMatch(t, @"\bcopyright\b|©|\(c\)\s*(19|20)\d\d", RegexOptions.IgnoreCase);
        } catch { return false; }
    }
}
