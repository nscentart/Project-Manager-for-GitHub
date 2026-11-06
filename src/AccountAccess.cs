using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

public static partial class AccountAccess {
    public const string ClientId = "Ov23liD9FyB7r5v9jZTH";
    public const string Permissions = "repo read:org";
    public const string DeletePermission = "repo delete_repo";
    public const string WithdrawPage = "https://github.com/settings/connections/applications/" + ClientId;
    const string CodeUrl = "https://github.com/login/device/code", TokenUrl = "https://github.com/login/oauth/access_token";
    public const string DefaultVerificationUrl = "https://github.com/login/device";
    public const string OriginBrowser = "browser", OriginGit = "git", OriginOld = "legacy";

    public static bool Equal(string a, string b) { return Organization.Equal(a, b); }

    public static GitHubAccount Active(Archive a) { return a.Accounts.FirstOrDefault(c => Equal(c.Login, a.Login)); }

    public static bool Migrate(Archive a) {
        if (a.Accounts == null) a.Accounts = new List<GitHubAccount>();
        if (a.Accounts.Count > 0 || string.IsNullOrEmpty(a.Login)) return false;
        a.Accounts.Add(new GitHubAccount { Login = a.Login, Origin = OriginOld });
        foreach (var r in a.Repo) if (r.Accounts == null) r.Accounts = new List<string> { a.Login };
        return true;
    }

    public static GitHubAccount Add(Archive a, string login, string origin) {
        var c = a.Accounts.FirstOrDefault(x => Equal(x.Login, login));
        if (c == null) { c = new GitHubAccount { Login = login }; a.Accounts.Add(c); }
        c.Login = login; c.Origin = origin;
        a.Login = login; a.Filter = null;
        return c;
    }

    public static void Rename(Archive a, string oldName, string newName) {
        var c = a.Accounts.FirstOrDefault(x => Equal(x.Login, oldName));
        if (c != null) {
            if (a.Accounts.Any(x => x != c && Equal(x.Login, newName))) a.Accounts.Remove(c);
            else c.Login = newName;
        }
        foreach (var r in a.Repo.Where(x => x.Accounts != null)) {
            bool had = r.Accounts.RemoveAll(x => Equal(x, oldName)) > 0;
            if (had && !r.Accounts.Any(x => Equal(x, newName))) r.Accounts.Add(newName);
        }
        if (Equal(a.Login, oldName)) a.Login = newName;
    }

    public static void Disconnect(Archive a, string login) {
        a.Accounts.RemoveAll(x => Equal(x.Login, login));
        foreach (var r in a.Repo.Where(x => x.Accounts != null)) r.Accounts.RemoveAll(x => Equal(x, login));
        if (Equal(a.Login, login)) { a.Login = a.Accounts.Count > 0 ? a.Accounts[0].Login : null; a.Filter = null; }
    }

    public static int Join(Archive a, string login, List<RepoInfo> newOnes, Func<RepoInfo, bool> followNew) {
        foreach (var n in newOnes) {
            var v = a.Repo.FirstOrDefault(x => Equal(x.Name, n.Name));
            if (v == null) { n.Followed = followNew(n); n.Accounts = new List<string> { login }; a.Repo.Add(n); continue; }
            v.Private = n.Private; v.Description = n.Description; v.Owner = n.Owner; v.Mine = n.Mine; v.Organization = n.Organization; v.Branch = n.Branch; v.Missing = false;
            if (v.Accounts == null) v.Accounts = new List<string>();
            if (!v.Accounts.Any(x => Equal(x, login))) v.Accounts.Add(login);
        }
        int missingRepos = 0;
        foreach (var v in a.Repo) {
            if (newOnes.Any(n => Equal(n.Name, v.Name))) continue;
            if (v.Accounts == null) v.Accounts = new List<string> { login };
            if (!v.Accounts.Any(x => Equal(x, login))) continue;
            if (v.Accounts.Count > 1) { v.Accounts.RemoveAll(x => Equal(x, login)); continue; }
            v.Missing = true; missingRepos++;
        }
        return missingRepos;
    }

    public class Removal { public List<string> Repo = new List<string>(); public int Texts, Decisions, HistoryDays; }

    public static Removal CountRemoval(Archive a, HistoryArchive s, IEnumerable<string> names) {
        var t = new Removal();
        foreach (var n in names) {
            var r = a.Repo.FirstOrDefault(x => x.Missing && Equal(x.Name, n));
            if (r == null || t.Repo.Contains(r.Name)) continue;
            t.Repo.Add(r.Name);
            t.HistoryDays += History.DaysHowMany(s, r.Name);
        }
        var keys = new HashSet<string>(a.Items.Where(e => t.Repo.Any(n => Equal(n, e.Repo))).Select(e => e.Key));
        t.Texts = keys.Count;
        t.Decisions = a.Decisions.Count(d => keys.Contains(d.Key));
        return t;
    }

    public static Removal RemoveMissing(Archive a, HistoryArchive s, IEnumerable<string> names) {
        var t = CountRemoval(a, s, names);
        var keys = new HashSet<string>(a.Items.Where(e => t.Repo.Any(n => Equal(n, e.Repo))).Select(e => e.Key));
        a.Items.RemoveAll(e => keys.Contains(e.Key));
        a.Decisions.RemoveAll(d => keys.Contains(d.Key));
        a.Repo.RemoveAll(r => r.Missing && t.Repo.Contains(r.Name));
        foreach (var n in t.Repo) History.Remove(s, n);
        return t;
    }

    public static bool Sees(RepoInfo r, string login) { return r.Accounts == null || r.Accounts.Any(x => Equal(x, login)); }

    static string RepoOwnerLogin(RepoInfo r) {
        if (!string.IsNullOrEmpty(r.Owner)) return r.Owner;
        int i = (r.Name ?? "").IndexOf('/');
        return i > 0 ? r.Name.Substring(0, i) : "";
    }

    public const string FilterAll = "", FilterPersonal = "personal", FilterOthers = "others", OrgFilterPrefix = "org:";

    public static bool Pass(RepoInfo r, string login, string filter) {
        if (!Sees(r, login)) return false;
        if (string.IsNullOrEmpty(filter)) return true;
        if (filter == FilterPersonal) return !r.Organization && Equal(RepoOwnerLogin(r), login);
        if (filter == FilterOthers) return !r.Organization && !Equal(RepoOwnerLogin(r), login);
        return filter.StartsWith(OrgFilterPrefix) && r.Organization && Equal(RepoOwnerLogin(r), filter.Substring(OrgFilterPrefix.Length));
    }

    public static List<RepoInfo> VisibleRepos(Archive a) {
        var f = a.Filter;
        if (!Filters(a).Any(x => x[0] == (f ?? FilterAll))) f = FilterAll;
        return a.Repo.Where(r => Pass(r, a.Login, f)).ToList();
    }

    public static List<string[]> Filters(Archive a) {
        var visibleRepos = a.Repo.Where(r => Sees(r, a.Login)).ToList();
        var l = new List<string[]> { new[] { FilterAll, Language.T("common.all_repos") } };
        if (visibleRepos.Any(r => Pass(r, a.Login, FilterPersonal))) l.Add(new[] { FilterPersonal, Language.T("accounts.personal_only") });
        foreach (var o in visibleRepos.Where(r => r.Organization).Select(RepoOwnerLogin).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            l.Add(new[] { OrgFilterPrefix + o, Language.T("accounts.organization", "o", o) });
        if (visibleRepos.Any(r => Pass(r, a.Login, FilterOthers))) l.Add(new[] { FilterOthers, Language.T("accounts.other_peoples") });
        return l;
    }

    static Dictionary<string, object> Response(string body) {
        try {
            var d = new JavaScriptSerializer().DeserializeObject(body ?? "") as Dictionary<string, object>;
            if (d != null) return d;
        } catch { }
        throw new AccessException(Language.T("common.github_gave_answer_dont_understand"));
    }

    static string Field(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
    static int Number(Dictionary<string, object> d, string k, int otherwise) { int n; return int.TryParse(Field(d, k), out n) && n > 0 ? n : otherwise; }

    static string Explain(string error) {
        switch (error) {
            case "access_denied": return Language.T("accounts.github_chose_not_authorize_no");
            case "expired_token": case "token_expired": return Language.T("accounts.code_expired_start_again_youll");
            case "device_flow_disabled": return Language.T("accounts.sign_code_not_active_program");
            case "incorrect_client_credentials": return Language.T("accounts.github_not_recognize_program");
            default: return Language.T("accounts.github_not_give_access_try");
        }
    }

    public static DeviceCode Ask(string clientId, Func<string, string, string> post) { return Ask(clientId, Permissions, post); }

    public static DeviceCode Ask(string clientId, string permissions, Func<string, string, string> post) {
        var d = Response(post(CodeUrl, "client_id=" + Uri.EscapeDataString(clientId) + "&scope=" + Uri.EscapeDataString(permissions)));
        if (Field(d, "error").Length > 0) throw new AccessException(Explain(Field(d, "error")));
        var c = new DeviceCode { Code = Field(d, "device_code"), ForUser = Field(d, "user_code"), Page = Field(d, "verification_uri"),
                                        Expires = Number(d, "expires_in", 900), Interval = Number(d, "interval", 5) };
        if (c.Code.Length == 0 || c.ForUser.Length == 0) throw new AccessException(Language.T("common.github_gave_answer_dont_understand"));
        if (!c.Page.StartsWith("https://github.com/")) c.Page = DefaultVerificationUrl;
        return c;
    }

    public static string Wait(DeviceCode c, string clientId, Func<string, string, string> post, Action<int> sleeps, Func<bool> cancelled) {
        string body = "client_id=" + Uri.EscapeDataString(clientId) + "&device_code=" + Uri.EscapeDataString(c.Code) + "&grant_type=" + Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:device_code");
        int step = Math.Max(1, c.Interval), elapsed = 0, failures = 0;
        while (elapsed < c.Expires) {
            sleeps(step);
            elapsed += step;
            if (cancelled()) return null;
            string text;
            try { text = post(TokenUrl, body); failures = 0; }
            catch (AccessException) { throw; }
            catch (Exception) {
                if (++failures >= 5) throw new AccessException(Language.T("common.github_not_responding_check_internet"));
                step += 5;
                continue;
            }
            var d = Response(text);
            string t = Field(d, "access_token");
            if (t.Length > 0) return t;
            string e = Field(d, "error");
            if (e == "authorization_pending") continue;
            if (e == "slow_down") { step = Number(d, "interval", step + 5); continue; }
            throw new AccessException(Explain(e));
        }
        throw new AccessException(Explain("expired_token"));
    }

    public static string Post(string url, string body) {
        if (url != CodeUrl && url != TokenUrl) throw new Exception("address outside the GitHub login");
        return PostA(url, body);
    }

    static string PostA(string url, string body) {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "POST";
        req.ContentType = "application/x-www-form-urlencoded";
        req.Accept = "application/json";
        req.UserAgent = "ProjectManager/" + Program.Version;
        req.AllowAutoRedirect = false;
        req.Timeout = 30000;
        var b = Encoding.ASCII.GetBytes(body);
        req.ContentLength = b.Length;
        try {
            using (var s = req.GetRequestStream()) s.Write(b, 0, b.Length);
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h == null) throw new NoNetworkException(Language.T("accounts.github_not_responding"));
            if ((int)h.StatusCode == 404) throw new AccessException(Explain("incorrect_client_credentials"));
            throw new AccessException(Explain(""));
        }
    }

    public static string FindGit() {
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
            try {
                string c = d.Trim().Trim('"');
                if (c.Length == 0 || !Path.IsPathRooted(c)) continue;
                string f = Path.Combine(c, "git.exe");
                if (File.Exists(f)) return f;
            } catch (ArgumentException) { }
        }
        return null;
    }
}
