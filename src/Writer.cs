using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public static class Writer {
    public const int MaxFile = 400;
    public const long MaxFileByteSize = 100L * 1024 * 1024, MaxByteSize = 500L * 1024 * 1024, MaxAsset = 1024L * 1024 * 1024;
    const long MaxToScan = 100L * 1024 * 1024, MaxRead = 300L * 1024 * 1024;
    const int MaxScanned = 1000, MaxBranches = 300, MaxPages = 30;
    public static int MaxHistoryCommit = 1000, MaxHistoryFile = 2000, MaxRequests = 4000;

    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
    static bool B(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is bool && (bool)v; }
    static long N(Dictionary<string, object> d, string k) { long n; return long.TryParse(S(d, k), out n) ? n : 0; }
    static Dictionary<string, object> O(Dictionary<string, object> d, string k) { object v; return (d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null) ?? new Dictionary<string, object>(); }
    static Dictionary<string, object> D(params object[] kv) { var d = new Dictionary<string, object>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1]; return d; }

    public static bool ValidRepoName(string n) { return !string.IsNullOrEmpty(n) && n.Length <= 100 && Regex.IsMatch(n, @"^[A-Za-z0-9._-]+$") && n.Trim('.').Length > 0 && !n.EndsWith(".git", StringComparison.OrdinalIgnoreCase); }
    public static bool ValidLogin(string n) { return !string.IsNullOrEmpty(n) && n.Length <= 39 && Regex.IsMatch(n, @"^[A-Za-z0-9-]+$"); }
    static bool ValidRepo(string r) { var p = (r ?? "").Split('/'); return p.Length == 2 && WideValidLogin(p[0]) && Regex.IsMatch(p[1], @"^[A-Za-z0-9._-]+$") && p[1].Trim('.').Length > 0; }
    static bool WideValidLogin(string n) { return Regex.IsMatch(n, @"^[A-Za-z0-9._-]+$"); }
    static string SafeRepo(string r) { if (!ValidRepo(r)) throw new WriteException(Language.T("writes.repos_name_not_valid_request")); return r; }

    public static bool Workflow(string path) {
        string p = (path ?? "").Replace('\\', '/').TrimStart('/');
        return p.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) || p.Equals(".github/workflows", StringComparison.OrdinalIgnoreCase);
    }

    public static string SafePath(string path) {
        string p = (path ?? "").Trim().Replace('\\', '/');
        if (p.Length == 0 || p.StartsWith("/") || p.Any(char.IsControl)) throw new WriteException(Language.T("writes.path_not_valid"));
        foreach (var s in p.Split('/'))
            if (s.Length == 0 || s == "." || s == ".." || s.Equals(".git", StringComparison.OrdinalIgnoreCase)) throw new WriteException(Language.T("writes.path_not_valid"));
        return p;
    }

    static string Segments(string p) { return string.Join("/", p.Split('/').Select(Uri.EscapeDataString)); }

    static void Permission(string path) { if (Workflow(path)) throw new WriteException(403, Phrases.WorkflowBlocked); }

    static void StopFindIf(List<Finding> found, string what) {
        if (found.Count > 0) { DataStore.Log("secrets check: stopped, " + found.Count + " things found"); throw new WriteException(what, found); }
    }
    static string Stopped { get { return Language.T("writes.stopping_seems_something_must_not"); } }

    static Dictionary<string, object> Signature(Client c) {
        var d = c.Call("GET", "/user", null).D;
        long id = N(d, "id"); string login = S(d, "login");
        if (id <= 0 || !WideValidLogin(login)) throw new WriteException(Language.T("writes.github_didnt_tell_who_without"));
        return D("name", login, "email", id + "+" + login + "@users.noreply.github.com");
    }

    static RepoFacts Read(Dictionary<string, object> d) {
        var p = O(d, "permissions"); var o = O(d, "owner");
        return new RepoFacts {
            Id = N(d, "id"), Name = S(d, "full_name"), Owner = S(o, "login"), Organization = S(o, "type") == "Organization", Description = S(d, "description"), Website = S(d, "homepage"), Wiki = B(d, "has_wiki"),
            Branch = S(d, "default_branch"), Private = B(d, "private"), Archived = B(d, "archived"), Last = S(d, "pushed_at"), ByteSize = N(d, "size"),
            Stars = (int)N(d, "stargazers_count"), Forks = (int)N(d, "forks_count"), Reports = (int)N(d, "open_issues_count"),
            Admin = B(p, "admin"), Maintain = B(p, "maintain"), Push = B(p, "push"), Triage = B(p, "triage"), Pull = B(p, "pull") };
    }

    public static RepoFacts Done(Client c, string repo) { return Read(c.Call("GET", "/repos/" + SafeRepo(repo), null).D); }

    static RepoFacts ReloadFacts(Client c, RepoFacts seen, RepoAction a) {
        if (seen == null) throw new WriteException(Language.T("writes.first_need_read_repo_github"));
        var f = Done(c, seen.Name);
        if (f.Id != seen.Id || f.Name != seen.Name) throw new WriteException(Language.T("writes.repo_changed_github_meantime_nothing"));
        if (f.Private != seen.Private) throw new WriteException(Language.T("writes.while_looking_repo_became_nothing", "part", (f.Private ? Language.T("common.private") : Language.T("common.public"))));
        Permissions.Allow(a, f);
        return f;
    }

    static RepoFacts Reconfirm(Client c, RepoFacts seen, string written, RepoAction a) {
        var f = ReloadFacts(c, seen, a);
        if (written != f.Name) throw new WriteException(Language.T("writes.name_typed_not_repos_name"));
        return f;
    }

    public static List<Owner> Owners(Client c, string login) {
        var l = new List<Owner> { new Owner { Name = login } };
        foreach (var m in c.Call("GET", "/user/memberships/orgs?state=active&per_page=100", null).L) {
            string org = S(O(m, "organization"), "login");
            if (!ValidRepo(org + "/x")) continue;
            var p = new Owner { Name = org, Organization = true };
            if (S(m, "role") != "admin")
                try {
                    var d = c.Call("GET", "/orgs/" + org, null).D;
                    if ((d.ContainsKey("members_can_create_private_repositories") && !B(d, "members_can_create_private_repositories"))
                        || (d.ContainsKey("members_can_create_repositories") && !B(d, "members_can_create_repositories")))
                        p.Because = Language.T("writes.organization_members_cant_create_private");
                } catch (WriteException) { }
            l.Add(p);
        }
        return l;
    }

    static void FreeName(Client c, string repo) {
        try { c.Call("GET", "/repos/" + SafeRepo(repo), null); }
        catch (WriteException e) {
            if (e.Code == 404) return;
            if (e.Code >= 300 && e.Code < 400) throw new WriteException(e.Code, Language.T("writes.name_belonged_repo_renamed_moved", "repo", repo));
            throw;
        }
        throw new WriteException(Language.T("writes.repo_name_already_exists_choose"));
    }

    public static RepoFacts CreateRepo(Client c, string login, Owner place, string name, string description, bool withReadme) {
        if (!ValidRepoName(name)) throw new WriteException(Language.T("common.name_contain_letters_without_accents"));
        if (place == null) throw new WriteException(Language.T("writes.choose_who_own_repo"));
        if (place.Because != null) throw new WriteException(403, place.Because);
        StopFindIf(Screener.CheckText(Language.T("writes.description"), description), Stopped);
        string via = place.Organization ? "/orgs/" + SafeRepo(place.Name + "/x").Split('/')[0] + "/repos" : "/user/repos";
        FreeName(c, place.Name + "/" + name);
        if (withReadme) Signature(c);
        var f = Read(c.Call("POST", via, D("name", name, "description", description ?? "", "private", true, "auto_init", false)).D);
        if (!f.Private) throw new WriteException(Language.T("writes.github_says_repo_not_private"));
        if (withReadme)
            try { WriteFile(c, f, "README.md", "# " + name + "\n" + (string.IsNullOrWhiteSpace(description) ? "" : "\n" + description + "\n"), null, Language.T("writes.first_readme")); }
            catch (WriteException e) { f.ShowNote = Language.T("writes.repo_created_private_readme_not", "error", e.Message); }
        return f;
    }

    public static void ChangeDescription(Client c, RepoFacts seen, string text) {
        var f = ReloadFacts(c, seen, RepoAction.Description);
        StopFindIf(Screener.CheckText(Language.T("writes.description"), text), Stopped);
        c.Call("PATCH", "/repos/" + f.Name, D("description", text ?? ""));
    }

    public static bool ValidWebsite(string url) {
        string u = url ?? "";
        if (u.Length == 0) return true;
        if (u.Length > 255 || !u.StartsWith("https://", StringComparison.Ordinal) || u.Any(x => char.IsWhiteSpace(x) || char.IsControl(x) || "<>\"'`".IndexOf(x) >= 0)) return false;
        Uri a;
        if (!Uri.TryCreate(u, UriKind.Absolute, out a) || a.Scheme != Uri.UriSchemeHttps || a.UserInfo.Length > 0) return false;
        string h = a.Host;
        return h.IndexOf('.') > 0 && !h.EndsWith(".");
    }

    public static void ChangeWebsite(Client c, RepoFacts seen, string url) {
        string u = (url ?? "").Trim();
        if (!ValidWebsite(u)) throw new WriteException(Language.T("writes.website_must_address_starting_https"));
        var f = ReloadFacts(c, seen, RepoAction.Description);
        StopFindIf(Screener.CheckText(Language.T("writes.website"), u), Stopped);
        c.Call("PATCH", "/repos/" + f.Name, D("homepage", u));
    }

    public static string Rename(Client c, RepoFacts seen, string written, string newName) {
        if (!ValidRepoName(newName)) throw new WriteException(Language.T("common.name_contain_letters_without_accents"));
        var f = Reconfirm(c, seen, written, RepoAction.Name);
        string n = S(c.Call("PATCH", "/repos/" + f.Name, D("name", newName)).D, "full_name");
        return ValidRepo(n) ? n : f.Owner + "/" + newName;
    }

    public static void ArchiveRepo(Client c, RepoFacts seen, bool archive) {
        var f = ReloadFacts(c, seen, RepoAction.Archive);
        c.Call("PATCH", "/repos/" + f.Name, D("archived", archive));
    }

    public static void MakePrivate(Client c, RepoFacts seen) {
        var f = ReloadFacts(c, seen, RepoAction.Visibility);
        c.Call("PATCH", "/repos/" + f.Name, D("private", true));
    }

    public static void MakePublic(Client c, RepoFacts seen, string written, PublishCheck k) {
        var f = Reconfirm(c, seen, written, RepoAction.Visibility);
        if (k == null || k.RepoId != f.Id) throw new WriteException(Language.T("writes.before_making_public_check_files"));
        StopIfNotClean(k);
        StopFindIf(Screener.CheckText(Language.T("writes.description"), f.Description), PublishStopped);
        StopFindIf(Screener.CheckText(Language.T("writes.website"), f.Website), PublishStopped);
        bool tooMany;
        var tipsNow = Tips(c, f, out tooMany);
        bool otherTooMany = false;
        if (!tooMany) tipsNow.AddRange(OtherTips(c, f, out otherTooMany));
        string now = BranchesFingerprint(tipsNow);
        if (tooMany || otherTooMany || now != k.Sha) throw new WriteException(Language.T("writes.files_changed_after_check_nothing"));
        if (B(c.Call("PATCH", "/repos/" + f.Name, D("private", false)).D, "private")) throw new WriteException(Language.T("writes.github_says_repo_still_private"));
    }

    static string PublishStopped { get { return Language.T("writes.stopping_something_repo_must_not"); } }
    static string InHistory { get { return Language.T("writes.history_commit"); } }
    static string InMessage { get { return Language.T("writes.commit_message"); } }
    static string HistoryStopped { get { return Language.T("writes.what_history_stays_readable_even"); } }

    public static void StopIfNotClean(PublishCheck k) {
        if (k.Findings.Count > 0) throw new WriteException(PublishStopped + (k.Findings.Any(x => x.File.Contains(InHistory) || x.File.StartsWith(InMessage)) ? HistoryStopped : ""), k.Findings);
        if (k.Incomplete != null) throw new WriteException(k.Incomplete);
        if (k.NotScanned.Count > 0) throw new WriteException(Language.T("writes.stopping_could_not_read_files", "part", (k.NotScanned.Any(x => x.Contains(InHistory)) ? HistoryStopped : "")), k.NotScanned.Select(x => new Finding { File = x, What = Language.T("writes.could_not_read") }).ToList());
    }

    public static int TransferWaitMs = 500;

    public static string Transfer(Client c, RepoFacts seen, string written, string newOwner) {
        if (!ValidLogin(newOwner)) throw new WriteException(Language.T("writes.account_name_not_valid"));
        var f = Reconfirm(c, seen, written, RepoAction.Transfer);
        string n = S(c.Call("POST", "/repos/" + f.Name + "/transfer", D("new_owner", newOwner)).D, "full_name");
        if (ValidRepo(n) && n != f.Name) return n;
        return MovedA(c, f, newOwner + "/" + f.Name.Substring(f.Name.IndexOf('/') + 1));
    }

    static string MovedA(Client c, RepoFacts f, string newName) {
        if (!ValidRepo(newName)) return null;
        for (int i = 0; i < 4; i++) {
            if (i > 0) System.Threading.Thread.Sleep(TransferWaitMs * i);
            try {
                var d = c.Call("GET", "/repos/" + newName, null).D;
                string n = S(d, "full_name");
                return ValidRepo(n) && N(d, "id") == f.Id ? n : null;
            } catch (WriteException e) { if (e.Code != 404) return null; }
            catch (Exception) { return null; }
        }
        return null;
    }

    static string Head(Client c, RepoFacts f) {
        try { return S(O(c.Call("GET", "/repos/" + f.Name + "/git/ref/heads/" + Segments(f.Branch), null).D, "object"), "sha"); }
        catch (WriteException e) { if (e.Code == 404 || e.Code == 409) return null; throw; }
    }

    static List<RepoFile> Tree(Client c, RepoFacts f, string sha, out bool cut) {
        cut = false;
        var l = new List<RepoFile>();
        if (sha == null) return l;
        var d = c.Call("GET", "/repos/" + f.Name + "/git/trees/" + sha + "?recursive=1", null).D;
        cut = B(d, "truncated") || !d.ContainsKey("tree");
        foreach (var o in ((IEnumerable)(d.ContainsKey("tree") ? d["tree"] : new object[0])).Cast<object>().OfType<Dictionary<string, object>>())
            if (S(o, "type") == "blob") l.Add(new RepoFile { Path = S(o, "path"), Sha = S(o, "sha"), ByteSize = N(o, "size") });
        return l;
    }

    public static List<RepoFile> FileList(Client c, RepoFacts f) { bool t; return Tree(c, f, Head(c, f), out t); }

    static byte[] Base64(string s) { return Convert.FromBase64String(Regex.Replace(s ?? "", @"\s+", "")); }

    static List<KeyValuePair<string, string>> Tips(Client c, RepoFacts f, out bool tooMany) {
        tooMany = false;
        var l = new List<KeyValuePair<string, string>>();
        for (int p = 1; ; p++) {
            List<Dictionary<string, object>> page;
            try { page = c.Call("GET", "/repos/" + f.Name + "/branches?per_page=100&page=" + p, null).L.ToList(); }
            catch (WriteException e) { if (e.Code == 404 || e.Code == 409) return l; throw; }
            foreach (var b in page) l.Add(new KeyValuePair<string, string>(S(b, "name"), S(O(b, "commit"), "sha")));
            if (l.Count > MaxBranches) { tooMany = true; return l; }
            if (page.Count < 100) return l;
        }
    }

    static string BranchesFingerprint(List<KeyValuePair<string, string>> tips) { return string.Join("\n", tips.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value)); }

    static byte[] ReadBlob(Client c, RepoFacts f, RepoFile x, ref long bytesRead) {
        if (x.ByteSize > MaxToScan || bytesRead + x.ByteSize > MaxRead) return null;
        bytesRead += x.ByteSize;
        try {
            var d = c.Call("GET", "/repos/" + f.Name + "/git/blobs/" + x.Sha, null).D;
            if (S(d, "encoding") != "base64") return null;
            var b = Base64(S(d, "content"));
            return x.ByteSize > 0 && b.Length != x.ByteSize ? null : b;
        }
        catch (WriteException e) { if (e.Code == 403 || e.Code == 404 || e.Code == 413 || e.Code == 422 || (e.Code == 0 && x.ByteSize > 5L * 1024 * 1024)) return null; throw; }
        catch (FormatException) { return null; }
        catch (OutOfMemoryException) { return null; }
    }

    static string TooMany { get { return Language.T("writes.repo_too_many_files_branches"); } }
    static string TextsTooMany { get { return Language.T("writes.repo_too_many_reports_releases"); } }
    static string HistoryNotRead { get { return Language.T("writes.github_didnt_let_read_all"); } }
    static string HistoryTooMuch() { return Language.T("common.repos_history_too_long_check", "max_history_commits", MaxHistoryCommit, "max_history_files", MaxHistoryFile); }

    static string Short(string sha) { return sha.Length > 7 ? sha.Substring(0, 7) : sha; }

    static bool EachPage(Client c, string path, Action<Dictionary<string, object>> each) {
        for (int p = 1; p <= MaxPages; p++) {
            List<Dictionary<string, object>> l;
            try { l = c.Call("GET", path + "per_page=100&page=" + p, null).L.ToList(); }
            catch (WriteException e) { if (e.Code == 404 || e.Code == 410) return true; throw; }
            foreach (var x in l) each(x);
            if (l.Count < 100) return true;
        }
        return false;
    }

    static List<KeyValuePair<string, string>> OtherTips(Client c, RepoFacts f, out bool tooMany) {
        var l = new List<KeyValuePair<string, string>>();
        bool tag = EachPage(c, "/repos/" + f.Name + "/tags?", x => l.Add(new KeyValuePair<string, string>(Language.T("writes.tag", "text", S(x, "name")), S(O(x, "commit"), "sha"))));
        bool pull = EachPage(c, "/repos/" + f.Name + "/pulls?state=all&", x => l.Add(new KeyValuePair<string, string>(Language.T("writes.pull", "text", N(x, "number")), S(O(x, "head"), "sha"))));
        tooMany = !tag || !pull;
        return l;
    }

    public static PublishCheck CheckForPublish(Client c, RepoFacts seen, Action<string> step) {
        var f = ReloadFacts(c, seen, RepoAction.Visibility);
        var k = new PublishCheck { RepoId = f.Id, Wiki = f.Wiki };
        k.Findings.AddRange(Screener.CheckText(Language.T("writes.description"), f.Description));
        k.Findings.AddRange(Screener.CheckText(Language.T("writes.website"), f.Website));
        bool tooMany;
        var tips = Tips(c, f, out tooMany);
        k.Sha = BranchesFingerprint(tips);
        k.Branches = tips.Count;
        if (tooMany) { k.Incomplete = TooMany; return k; }
        bool otherTooMany;
        var other = OtherTips(c, f, out otherTooMany);
        k.Sha = BranchesFingerprint(tips.Concat(other).ToList());
        if (otherTooMany) { k.Incomplete = TextsTooMany; return k; }
        if (other.Any(x => x.Value.Length == 0)) { k.Incomplete = HistoryNotRead; return k; }
        if ((Head(c, f) != null && tips.Count == 0) || tips.Any(x => x.Value.Length == 0)) { k.Incomplete = Language.T("writes.github_didnt_tell_where_branches"); return k; }
        var contents = new Dictionary<string, List<Finding>>();
        var names = new HashSet<string>();
        var pairs = new HashSet<string>();
        long bytesRead = 0;
        foreach (var g in tips.GroupBy(x => x.Value).OrderByDescending(x => x.Any(p => p.Key == f.Branch))) {
            string branch = g.Any(p => p.Key == f.Branch) ? null : g.First().Key;
            bool cut;
            var file = Tree(c, f, g.Key, out cut);
            if (cut) { k.Incomplete = TooMany; return k; }
            foreach (var x in file) {
                string et = branch == null ? x.Path : Language.T("writes.branch", "path", x.Path, "branch", branch);
                if (names.Add(x.Path)) foreach (var r in Screener.CheckName(x.Path)) { r.File = et; k.Findings.Add(r); }
                if (!pairs.Add(x.Path + "\n" + x.Sha)) continue;
                if (step != null) step(Language.T("writes.checking_files", "label", et));
                List<Finding> inside;
                if (!contents.TryGetValue(x.Sha, out inside)) {
                    if (contents.Count >= MaxScanned) { k.Incomplete = TooMany; return k; }
                    var b = ReadBlob(c, f, x, ref bytesRead);
                    inside = b == null ? null : Screener.CheckContent(et, b);
                    contents[x.Sha] = inside;
                    if (b != null) foreach (var a in Screener.Notices(et, b)) { a.File = et; k.Notices.Add(a); }
                }
                if (inside == null) { k.NotScanned.Add(et); continue; }
                k.Scanned++;
                foreach (var r in inside) k.Findings.Add(new Finding { File = et, Line = r.Line, What = r.What });
            }
        }
        ScanHistory(c, f, k, tips.Concat(other).Select(x => x.Value).Distinct().ToList(), new HashSet<string>(tips.Select(x => x.Value)), contents, names, step);
        if (k.Incomplete == null) Count(c, f, k, step);
        return k;
    }

    static void ScanHistory(Client c, RepoFacts f, PublishCheck k, List<string> tips, HashSet<string> branches, Dictionary<string, List<Finding>> contents, HashSet<string> names, Action<string> step) {
        var known = new HashSet<string>();
        var joinedList = new List<KeyValuePair<string, string>>();
        int requests = contents.Count;
        foreach (var tip in tips) {
            if (known.Contains(tip)) continue;
            var missingSet = new HashSet<string> { tip };
            for (int p = 1; missingSet.Count > 0; p++) {
                if (step != null) step(Language.T("writes.checking_history_commits_far", "known", known.Count));
                if (++requests > MaxRequests) { k.Incomplete = HistoryTooMuch(); return; }
                List<Dictionary<string, object>> page;
                try { page = c.Call("GET", "/repos/" + f.Name + "/commits?sha=" + tip + "&per_page=100&page=" + p, null).L.ToList(); }
                catch (WriteException e) { if (e.Code == 404 || e.Code == 409 || e.Code == 422) { k.Incomplete = HistoryNotRead; return; } throw; }
                foreach (var x in page) {
                    string sha = S(x, "sha");
                    missingSet.Remove(sha);
                    if (!known.Add(sha)) continue;
                    if (known.Count > MaxHistoryCommit) { k.Incomplete = HistoryTooMuch(); return; }
                    var cm = O(x, "commit");
                    string tree = S(O(cm, "tree"), "sha");
                    var parents = x.ContainsKey("parents") ? x["parents"] as IEnumerable : null;
                    if (sha.Length == 0 || tree.Length == 0 || parents == null) { k.Incomplete = HistoryNotRead; return; }
                    foreach (var g in parents.Cast<object>().OfType<Dictionary<string, object>>()) if (!known.Contains(S(g, "sha"))) missingSet.Add(S(g, "sha"));
                    joinedList.Add(new KeyValuePair<string, string>(sha, tree));
                    k.Findings.AddRange(Screener.CheckText(InMessage + Short(sha), S(cm, "message")));
                    if (RealEmail(S(O(cm, "author"), "email")) || RealEmail(S(O(cm, "committer"), "email"))) k.PlainEmail++;
                }
                if (missingSet.Count > 0 && page.Count < 100) { k.Incomplete = HistoryNotRead; return; }
            }
        }
        k.Commit = joinedList.Count;
        var trees = new HashSet<string>(joinedList.Where(x => branches.Contains(x.Key)).Select(x => x.Value));
        var found = new HashSet<string>();
        long bytesRead = 0;
        int newItems = 0, done = 0;
        foreach (var e in joinedList) {
            done++;
            if (branches.Contains(e.Key) || !trees.Add(e.Value)) continue;
            if (step != null) step(Language.T("writes.checking_history_commit", "done", done, "list", joinedList.Count));
            if (++requests > MaxRequests) { k.Incomplete = HistoryTooMuch(); return; }
            bool cut;
            var file = Tree(c, f, e.Value, out cut);
            if (cut) { k.Incomplete = HistoryTooMuch(); return; }
            string place = InHistory + Short(e.Key) + ")";
            foreach (var x in file) if (names.Add(x.Path)) foreach (var r in Screener.CheckName(x.Path)) { r.File = x.Path + place; k.Findings.Add(r); }
            var toRead = file.Where(x => !contents.ContainsKey(x.Sha)).GroupBy(x => x.Sha).Select(g => g.First()).ToList();
            newItems += toRead.Count; requests += toRead.Count;
            if (newItems > MaxHistoryFile || requests > MaxRequests) { k.Incomplete = HistoryTooMuch(); return; }
            foreach (var x in toRead) {
                if (bytesRead + x.ByteSize > MaxRead) { k.Incomplete = HistoryTooMuch(); return; }
                string et = x.Path + place;
                var b = ReadBlob(c, f, x, ref bytesRead);
                var inside = b == null ? null : Screener.CheckContent(et, b);
                contents[x.Sha] = inside;
                if (inside == null) { k.NotScanned.Add(et); continue; }
                k.HistoryScanned++;
                foreach (var a in Screener.Notices(et, b)) if (found.Add(x.Path + "\n" + a.What)) { a.File = et; k.Notices.Add(a); }
                foreach (var r in inside) if (found.Add(x.Path + "\n" + r.What)) k.Findings.Add(new Finding { File = et, Line = r.Line, What = r.What });
            }
        }
    }

    static void Count(Client c, RepoFacts f, PublishCheck k, Action<string> step) {
        if (step != null) step(Language.T("writes.checking_reports_pull_requests_releases"));
        string base_ = "/repos/" + f.Name;
        Action<string, string> search = (place, text) => k.Findings.AddRange(Screener.CheckText(place, text));
        Func<string, string> lastSegment = u => u.Substring(u.LastIndexOf('/') + 1);
        var stats = new List<string>();
        bool allRead = EachPage(c, base_ + "/issues?state=all&", x => {
            bool pull = x.ContainsKey("pull_request");
            if (pull) { k.Pull++; stats.Add(N(x, "number").ToString()); } else k.Issue++;
            string who = (pull ? Language.T("writes.pull_request") : Language.T("writes.report")) + N(x, "number");
            search(Language.T("writes.title", "who", who), S(x, "title"));
            search(Language.T("writes.text", "who", who), S(x, "body"));
        });
        allRead = EachPage(c, base_ + "/issues/comments?", x => { k.Comments++; search(Language.T("writes.comment_on", "text", lastSegment(S(x, "issue_url"))), S(x, "body")); }) && allRead;
        allRead = EachPage(c, base_ + "/pulls/comments?", x => { k.Comments++; search(Language.T("writes.review_comment", "text", lastSegment(S(x, "pull_request_url"))), S(x, "body")); }) && allRead;
        allRead = EachPage(c, base_ + "/comments?", x => { k.Comments++; search(Language.T("writes.comment_commit", "text", Short(S(x, "commit_id"))), S(x, "body")); }) && allRead;
        if (stats.Count > MaxRequests / 4) allRead = false;
        else foreach (var n in stats) {
            if (step != null) step(Language.T("writes.checking_reviews_pull_request", "n", n));
            allRead = EachPage(c, base_ + "/pulls/" + n + "/reviews?", x => { if (S(x, "body").Length == 0) return; k.Comments++; search(Language.T("writes.review_of", "n", n), S(x, "body")); }) && allRead;
        }
        allRead = EachPage(c, base_ + "/releases?", x => {
            if (B(x, "draft")) return;
            k.Release++;
            string who = Language.T("writes.release", "text", S(x, "tag_name"));
            search(who, S(x, "tag_name"));
            search(Language.T("writes.title", "who", who), S(x, "name"));
            search(Language.T("writes.notes", "who", who), S(x, "body"));
            object assets;
            if (!x.TryGetValue("assets", out assets) || !(assets is IEnumerable)) return;
            foreach (var a in ((IEnumerable)assets).Cast<object>().OfType<Dictionary<string, object>>()) {
                k.Assets++;
                foreach (var r in Screener.CheckName(S(a, "name"))) { r.File = Language.T("writes.attachment", "who", who, "to", S(a, "name")); k.Findings.Add(r); }
            }
        }) && allRead;
        if (!allRead) k.Incomplete = TextsTooMany;
    }

    static bool RealEmail(string e) { return e.Length > 0 && !e.EndsWith("@users.noreply.github.com", StringComparison.OrdinalIgnoreCase) && !e.Equals("noreply@github.com", StringComparison.OrdinalIgnoreCase); }

    public static RepoFile ReadFile(Client c, RepoFacts f, string path) {
        string p = SafePath(path);
        var d = c.Call("GET", "/repos/" + f.Name + "/contents/" + Segments(p) + "?ref=" + Uri.EscapeDataString(f.Branch), null).D;
        if (S(d, "type") != "file" || S(d, "encoding") != "base64") throw new WriteException(Language.T("writes.file_too_large_edit_isnt"));
        var b = Base64(S(d, "content"));
        if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) throw new WriteException(Language.T("writes.file_written_utf_16_editing"));
        if (Screener.Binary(b)) throw new WriteException(Language.T("writes.not_text_file_only_text"));
        bool bom = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(b, bom ? 3 : 0, b.Length - (bom ? 3 : 0)); }
        catch (ArgumentException) { throw new WriteException(Language.T("writes.file_not_utf_8_editing")); }
        int doubles = Regex.Matches(text, "\r\n").Count, newlines = text.Count(x => x == '\n');
        return new RepoFile { Path = p, Sha = S(d, "sha"), ByteSize = b.Length, Text = text, Bom = bom, LineEnd = doubles > newlines - doubles ? "\r\n" : "\n" };
    }

    public static void WriteFile(Client c, RepoFacts seen, string path, string text, string sha, string message, bool bom = false) {
        string p = SafePath(path);
        Permission(p);
        var f = ReloadFacts(c, seen, RepoAction.Content);
        var b = new UTF8Encoding(false).GetBytes(text ?? "");
        if (bom) b = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(b).ToArray();
        StopFindIf(Screener.CheckFile(p, b), Stopped);
        string m = string.IsNullOrWhiteSpace(message) ? Language.T("writes.edit", "p", p) : message;
        StopFindIf(Screener.CheckText(Language.T("writes.message"), m), Stopped);
        var who = Signature(c);
        var body = D("message", m, "content", Convert.ToBase64String(b), "author", who, "committer", who);
        if (!string.IsNullOrEmpty(sha) || Head(c, f) != null) body["branch"] = f.Branch;
        if (!string.IsNullOrEmpty(sha)) body["sha"] = sha;
        c.Call("PUT", "/repos/" + f.Name + "/contents/" + Segments(p), body);
    }

    public static void DeleteFile(Client c, RepoFacts seen, string path, string sha, string message) {
        string p = SafePath(path);
        Permission(p);
        var f = ReloadFacts(c, seen, RepoAction.Content);
        if (string.IsNullOrEmpty(sha)) throw new WriteException(Language.T("writes.first_need_read_file_github"));
        string m = string.IsNullOrWhiteSpace(message) ? Language.T("writes.delete", "p", p) : message;
        StopFindIf(Screener.CheckText(Language.T("writes.message"), m), Stopped);
        var who = Signature(c);
        c.Call("DELETE", "/repos/" + f.Name + "/contents/" + Segments(p), D("message", m, "sha", sha, "branch", f.Branch, "author", who, "committer", who));
    }

    class IgnoreRules {
        readonly string releaseBase;
        readonly List<KeyValuePair<Regex, bool[]>> rules = new List<KeyValuePair<Regex, bool[]>>();
        public IgnoreRules(string text, string folder) {
            releaseBase = folder;
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n')) {
                string r = raw.Trim();
                if (r.Length == 0 || r[0] == '#') continue;
                bool neg = r[0] == '!'; if (neg) r = r.Substring(1);
                bool dir = r.EndsWith("/"); r = r.TrimEnd('/');
                bool monoFont = r.Contains("/"); r = r.TrimStart('/');
                if (r.Length == 0) continue;
                var sb = new StringBuilder(monoFont ? "^" : "(^|/)");
                for (int i = 0; i < r.Length; i++) {
                    if (r[i] == '*' && i + 1 < r.Length && r[i + 1] == '*') { i++; if (i + 1 < r.Length && r[i + 1] == '/') { i++; sb.Append("(.*/)?"); } else sb.Append(".*"); }
                    else if (r[i] == '*') sb.Append("[^/]*");
                    else if (r[i] == '?') sb.Append("[^/]");
                    else if (r[i] == '\\' && i + 1 < r.Length) sb.Append(Regex.Escape(r[++i].ToString()));
                    else if (r[i] == '[') i = CharClass(r, i, sb);
                    else sb.Append(Regex.Escape(r[i].ToString()));
                }
                sb.Append("$");
                try { rules.Add(new KeyValuePair<Regex, bool[]>(new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), new[] { neg, dir })); } catch (ArgumentException) { }
            }
        }
        static int CharClass(string r, int i, StringBuilder sb) {
            int j = i + 1;
            if (j < r.Length && (r[j] == '!' || r[j] == '^')) j++;
            if (j < r.Length && r[j] == ']') j++;
            while (j < r.Length && r[j] != ']') j++;
            if (j >= r.Length) { sb.Append("\\["); return i; }
            string inside = r.Substring(i + 1, j - i - 1);
            bool no = inside[0] == '!' || inside[0] == '^';
            if (no) inside = inside.Substring(1);
            sb.Append("(?!/)[").Append(no ? "^" : "").Append(inside.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]")).Append("]");
            return j;
        }
        public bool? Describe(string path, bool folder) {
            if (!path.StartsWith(releaseBase, StringComparison.OrdinalIgnoreCase)) return null;
            string p = path.Substring(releaseBase.Length);
            bool? s = null;
            foreach (var r in rules) if ((!r.Value[1] || folder) && r.Key.IsMatch(p)) s = !r.Value[0];
            return s;
        }
    }

    static bool Skip(List<IgnoreRules> stack, string path, bool folder) {
        for (int i = stack.Count - 1; i >= 0; i--) { var d = stack[i].Describe(path, folder); if (d.HasValue) return d.Value; }
        return false;
    }

    static readonly HashSet<string> ConfirmExtensions = new HashSet<string>(new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb", ".bak", ".dump" }, StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> NotReadable = new HashSet<string>(new[] { ".pdf", ".doc", ".docx", ".docm", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx", ".pptm", ".odt", ".ods", ".odp", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" }, StringComparer.OrdinalIgnoreCase);

    public static Folder Prepare(string root, Action<string> step) {
        var k = new Folder { Root = root };
        var stack = new List<IgnoreRules>();
        string ex = Path.Combine(root, ".git", "info", "exclude");
        if (File.Exists(ex)) stack.Add(new IgnoreRules(File.ReadAllText(ex), ""));
        Descend(k, root, "", stack, step);
        if (k.File.Count > MaxFile) k.Rejected.Add(Language.T("writes.thats_files_github_limits_writes", "file", k.File.Count, "max_files", MaxFile));
        if (k.ByteSize > MaxByteSize) k.Rejected.Add(Language.T("writes.folder_too_big_upload_most", "max_size", MaxByteSize / 1024 / 1024));
        return k;
    }

    static void Descend(Folder k, string dir, string release, List<IgnoreRules> stack, Action<string> step) {
        string gi = Path.Combine(dir, ".gitignore");
        bool rules = File.Exists(gi) && (File.GetAttributes(gi) & FileAttributes.ReparsePoint) == 0;
        if (rules) stack.Add(new IgnoreRules(File.ReadAllText(gi), release));
        foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
            string name = Path.GetFileName(d), r = release + name;
            if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)) { k.Skipped.Add(r + "/"); continue; }
            if (Skip(stack, r, true)) { k.Skipped.Add(r + "/"); continue; }
            if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) { k.Rejected.Add(Language.T("writes.link_another_folder", "r", r)); continue; }
            Descend(k, d, r + "/", stack, step);
        }
        foreach (var f in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
            string name = Path.GetFileName(f), r = release + name;
            if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)) { k.Skipped.Add(r); continue; }
            if (Skip(stack, r, false)) { k.Skipped.Add(r); continue; }
            if (step != null) step(r);
            if (Workflow(r)) { k.Rejected.Add(r + ": " + Phrases.WorkflowBlocked); continue; }
            var i = new FileInfo(f);
            if ((i.Attributes & FileAttributes.ReparsePoint) != 0) { k.Rejected.Add(Language.T("writes.link_another_file", "r", r)); continue; }
            if (i.Length > MaxFileByteSize) { k.Rejected.Add(Language.T("writes.larger_100_mb_github_not", "r", r)); continue; }
            var b = File.ReadAllBytes(f);
            k.Findings.AddRange(Screener.CheckFile(r, b));
            k.Notices.AddRange(Screener.Notices(r, b));
            string extension = Path.GetExtension(name);
            if (ConfirmExtensions.Contains(extension)) k.ToConfirm.Add(r);
            else if (NotReadable.Contains(extension)) k.NotReadable.Add(r);
            k.File.Add(new FileToUpload { Path = r, Full = f, ByteSize = i.Length });
            k.ByteSize += i.Length;
        }
        if (rules) stack.RemoveAt(stack.Count - 1);
    }

    public static void LeaveOut(Folder k) {
        var via = new HashSet<string>(k.ToConfirm.Where(x => !k.Confirmed.Contains(x)));
        foreach (var x in k.File.Where(z => via.Contains(z.Path))) { k.ByteSize -= x.ByteSize; k.Skipped.Add(x.Path); }
        k.File.RemoveAll(z => via.Contains(z.Path));
        k.Notices.RemoveAll(z => via.Contains(z.File));
        k.ToConfirm.RemoveAll(via.Contains);
    }

    public static int AlreadyPresent(Client c, RepoFacts f, Folder k) { return Replaced(c, f, k).Count; }

    public static List<string> Replaced(Client c, RepoFacts f, Folder k) {
        var up = new HashSet<string>(FileList(c, f).Select(x => x.Path));
        return k.File.Where(x => up.Contains(x.Path)).Select(x => x.Path).ToList();
    }

    public static UploadResult LoadFolder(Client c, RepoFacts seen, Folder k, string message, Action<int, int> advance, Action<int> sleeps, string written = null) {
        if (k == null || k.File.Count == 0) throw new WriteException(Language.T("writes.nothing_upload"));
        if (k.Findings.Count > 0) throw new WriteException(Stopped, k.Findings);
        if (k.Rejected.Count > 0) throw new WriteException(k.Rejected[0]);
        var withoutYes = k.ToConfirm.Where(x => !k.Confirmed.Contains(x)).ToList();
        if (withoutYes.Count > 0) throw new WriteException(Language.T("writes.cant_look_inside_archives_databases", "list", string.Join(", ", withoutYes.Take(10)), "part", (withoutYes.Count > 10 ? Language.T("common.and_more", "part", (withoutYes.Count - 10)) : "")));
        var f = ReloadFacts(c, seen, RepoAction.Content);
        if (!f.Private && written != f.Name) throw new WriteException(Language.T("writes.repo_public_upload_type_name"));
        string msg = string.IsNullOrWhiteSpace(message) ? Language.N("writes.upload_files", k.File.Count) : message;
        StopFindIf(Screener.CheckText(Language.T("writes.message"), msg), Stopped);
        var who = Signature(c);
        var file = k.File.ToList();
        var result = new UploadResult { File = file.Count, Steps = 1 };
        string parent = Head(c, f);

        string alreadyOn = null;
        if (parent == null) {
            result.WasEmpty = true;
            foreach (var x in file) ReadFileAgain(x);
            var first = file.FirstOrDefault(x => x.Path.Equals("README.md", StringComparison.OrdinalIgnoreCase)) ?? file.OrderBy(x => x.ByteSize).First();
            var data = ReadFileAgain(first);
            parent = S(O(c.Call("PUT", "/repos/" + f.Name + "/contents/" + Segments(SafePath(first.Path)), D("message", msg, "content", Convert.ToBase64String(data), "author", who, "committer", who)).D, "commit"), "sha");
            file.Remove(first);
            alreadyOn = first.Path;
            if (file.Count == 0) { result.Commit = parent; return result; }
            result.Steps = 2;
        }

        try {
            var entries = new List<object>();
            int done = 0;
            foreach (var x in file) {
                var data = ReadFileAgain(x);
                string sha = S(c.Call("POST", "/repos/" + f.Name + "/git/blobs", D("content", Convert.ToBase64String(data), "encoding", "base64")).D, "sha");
                entries.Add(D("path", SafePath(x.Path), "mode", "100644", "type", "blob", "sha", sha));
                if (advance != null) advance(++done, file.Count);
                if (sleeps != null && file.Count > 50) sleeps(800);
            }
            string treeBase = S(O(c.Call("GET", "/repos/" + f.Name + "/git/commits/" + parent, null).D, "tree"), "sha");
            string tree = S(c.Call("POST", "/repos/" + f.Name + "/git/trees", D("base_tree", treeBase, "tree", entries)).D, "sha");
            string commit = S(c.Call("POST", "/repos/" + f.Name + "/git/commits", D("message", msg, "tree", tree, "parents", new[] { parent }, "author", who, "committer", who)).D, "sha");
            c.Call("PATCH", "/repos/" + f.Name + "/git/refs/heads/" + Segments(f.Branch), D("sha", commit, "force", false));
            result.Commit = commit;
            return result;
        }
        catch (WriteException e) {
            if (alreadyOn == null) throw;
            throw new WriteException(e.Code, Language.T("writes.however_file_already_repo_github", "error", e.Message, "already_on", alreadyOn)) { Findings = e.Findings };
        }
    }

    static byte[] ReadFileAgain(FileToUpload x) {
        byte[] b;
        try { b = File.ReadAllBytes(x.Full); } catch (IOException) { throw new WriteException(Language.T("writes.no_longer_read_moved_nothing", "path", x.Path)); }
        StopFindIf(Screener.CheckFile(x.Path, b), Stopped);
        return b;
    }

    static ReleaseInfo Rel(Dictionary<string, object> d) {
        var r = new ReleaseInfo { Id = N(d, "id"), Tag = S(d, "tag_name"), Title = S(d, "name"), Note = S(d, "body"), Branch = S(d, "target_commitish"), Data = S(d, "published_at"), Draft = B(d, "draft"), Preview = B(d, "prerelease") };
        object a;
        if (d.TryGetValue("assets", out a) && a is IEnumerable)
            foreach (var x in ((IEnumerable)a).Cast<object>().OfType<Dictionary<string, object>>()) r.Assets.Add(new AssetInfo { Name = S(x, "name"), ByteSize = N(x, "size") });
        return r;
    }

    public static List<ReleaseInfo> Release(Client c, RepoFacts f) { return c.Call("GET", "/repos/" + f.Name + "/releases?per_page=100", null).L.Select(Rel).ToList(); }
    public static ReleaseInfo OneRelease(Client c, RepoFacts f, long id) { return Rel(c.Call("GET", "/repos/" + f.Name + "/releases/" + id, null).D); }

    public static string Last(Client c, RepoFacts f) {
        try { return S(c.Call("GET", "/repos/" + f.Name + "/releases/latest", null).D, "tag_name"); }
        catch (WriteException e) { if (e.Code == 404) return ""; throw; }
    }

    static void ValidTag(string tag) {
        if (string.IsNullOrWhiteSpace(tag) || !Regex.IsMatch(tag, @"^[A-Za-z0-9][A-Za-z0-9._\-]*$")) throw new WriteException(Language.T("writes.tag_versions_bookmark_example_v1"));
    }

    public static ReleaseInfo CreateDraft(Client c, RepoFacts seen, string tag, string title, string notes) {
        ValidTag(tag);
        var f = ReloadFacts(c, seen, RepoAction.Release);
        StopFindIf(Screener.CheckText(Language.T("writes.release_title_notes"), (title ?? "") + "\n" + (notes ?? "")), Stopped);
        return Rel(c.Call("POST", "/repos/" + f.Name + "/releases", D("tag_name", tag, "name", title ?? "", "body", notes ?? "", "target_commitish", f.Branch, "draft", true)).D);
    }

    public static void AttachAsset(Client c, RepoFacts seen, long id, string file) {
        var f = ReloadFacts(c, seen, RepoAction.Release);
        var i = new FileInfo(file);
        if (!i.Exists) throw new WriteException(Language.T("writes.file_no_longer"));
        if (i.Length == 0 || i.Length > MaxAsset) throw new WriteException(Language.T("writes.attach_files_1_byte_1"));
        var b = File.ReadAllBytes(file);
        StopFindIf(Screener.CheckFile(i.Name, b), Stopped);
        var r = c.Load("/repos/" + f.Name + "/releases/" + id + "/assets?name=" + Uri.EscapeDataString(i.Name), b).D;
        if (N(r, "size") != b.Length) throw new WriteException(Language.T("writes.github_says_received_file_different"));
    }

    public static void EditRelease(Client c, RepoFacts seen, long id, string tag, string title, string notes, string written) {
        ValidTag(tag);
        var f = ReloadFacts(c, seen, RepoAction.Release);
        var now = OneRelease(c, f, id);
        if (!now.Draft) Reconfirm(c, seen, written, RepoAction.Release);
        StopFindIf(Screener.CheckText(Language.T("writes.release_title_notes"), (title ?? "") + "\n" + (notes ?? "")), Stopped);
        var body = D("tag_name", tag, "name", title ?? "", "body", notes ?? "");
        if (!now.Draft) {
            long latestId = 0;
            try { latestId = N(c.Call("GET", "/repos/" + f.Name + "/releases/latest", null).D, "id"); }
            catch (WriteException e) { if (e.Code != 404) throw; }
            if (latestId != id) body["make_latest"] = "false";
        }
        c.Call("PATCH", "/repos/" + f.Name + "/releases/" + id, body);
    }

    public static int PublishAttempts = 6, PublishWaitMs = 20000;

    public static void PublishWithRetry(Client c, RepoFacts seen, string written, long id, bool lastAs, Action<string> step, Action check = null) {
        bool attempted = false, wasMissing = false;
        try {
            for (int i = 1; i <= PublishAttempts; i++) {
                try {
                    if (i > 1 && !OneRelease(c, seen, id).Draft) return;
                    Publish(c, seen, written, id, lastAs, () => { if (check != null) check(); attempted = true; });
                    return;
                } catch (WriteException e) { if (e.Code != 404) throw; }
                if (i == PublishAttempts) break;
                bool isGone = Missing(c, seen, id);
                if (isGone && wasMissing) throw new WriteException(Language.T("writes.release_no_longer_github_nothing"));
                wasMissing = isGone;
                for (int stay = PublishWaitMs; stay > 0; stay -= 1000) {
                    int sec = (stay + 999) / 1000;
                    step(isGone ? Language.T("writes.github_cant_find_release_may", "sec", sec) : Language.T("writes.github_cant_find_repo_yet", "n", i + 1, "total", PublishAttempts, "sec", sec));
                    System.Threading.Thread.Sleep(Math.Min(1000, stay));
                }
            }
        } catch (CheckStoppedException) {
            if (!attempted) throw;
            ReleaseInfo readRelease;
            try { readRelease = OneRelease(c, seen, id); }
            catch (WriteException) { throw new WriteException(Language.T("writes.github_wont_let_read_release")); }
            if (readRelease.Draft) throw new WriteException(Language.T("writes.stopped_read_again_release_still"));
            return;
        }
        List<ReleaseInfo> allReleases;
        try { allReleases = Release(c, seen); }
        catch (WriteException e) { if (e.Code != 404) throw; throw new WriteException(attempted ? Language.T("writes.github_wont_let_read_release") : Language.T("writes.github_still_cant_find_repo")); }
        var now = allReleases.FirstOrDefault(x => x.Id == id);
        if (now == null) throw new WriteException(Language.T("writes.release_no_longer_github_nothing"));
        if (now.Draft) throw new WriteException(Language.T("writes.release_still_draft_github_isnt"));
    }

    static bool Missing(Client c, RepoFacts seen, long id) {
        try { return !Release(c, seen).Any(x => x.Id == id); }
        catch (WriteException) { return false; }
    }

    public static void Publish(Client c, RepoFacts seen, string written, long id, bool lastAs, Action beforeWrite = null) {
        var f = Reconfirm(c, seen, written, RepoAction.Release);
        if (!OneRelease(c, f, id).Draft) throw new WriteException(Language.T("writes.release_already_published"));
        if (beforeWrite != null) beforeWrite();
        c.Call("PATCH", "/repos/" + f.Name + "/releases/" + id, D("draft", false, "make_latest", lastAs ? "true" : "false"));
    }

    public static void DeleteRelease(Client c, RepoFacts seen, string written, long id) {
        var f = Reconfirm(c, seen, written, RepoAction.Release);
        c.Call("DELETE", "/repos/" + f.Name + "/releases/" + id, null);
    }

    public static void AccountSame(string chosen, string real, Action exit) {
        if (AccountAccess.Equal(chosen, real)) return;
        exit();
        throw new WriteException(Language.T("writes.chosen_account_access_found_belongs", "chosen", chosen, "real", real));
    }

    public static void CheckKey(Client c) {
        try { c.Call("GET", "/user", null); }
        catch (WriteException e) { throw new AccessException(c.Discarded ? e.Message : Language.T("writes.could_not_check_access_not", "error", e.Message)); }
    }

    public static List<RepoPerson> Collaborators(Client c, RepoFacts f) {
        var l = c.Call("GET", "/repos/" + f.Name + "/collaborators?affiliation=direct&per_page=100", null).L.Select(x => new RepoPerson { Name = S(x, "login"), Role = S(x, "role_name") }).ToList();
        l.AddRange(c.Call("GET", "/repos/" + f.Name + "/invitations?per_page=100", null).L.Select(x => new RepoPerson { Name = S(O(x, "invitee"), "login"), Role = S(x, "permissions"), Invitation = N(x, "id") }));
        return l;
    }

    public static string AccountType(Client c, string login) {
        if (!ValidLogin(login)) throw new WriteException(Language.T("writes.account_name_not_valid"));
        return S(c.Call("GET", "/users/" + login, null).D, "type");
    }

    public static string RoleOf(Client c, RepoFacts f, string login) {
        if (!ValidLogin(login)) throw new WriteException(Language.T("writes.account_name_not_valid"));
        return S(c.Call("GET", "/repos/" + f.Name + "/collaborators/" + login + "/permission", null).D, "role_name");
    }

    public static readonly string[] Roles = { "pull", "triage", "push", "maintain", "admin" };

    public static bool AddCollaborator(Client c, RepoFacts seen, string written, string login, string role) {
        if (!ValidLogin(login)) throw new WriteException(Language.T("writes.account_name_not_valid"));
        var f = Reconfirm(c, seen, written, RepoAction.Collaborators);
        if (f.Organization && !Roles.Contains(role)) throw new WriteException(Language.T("writes.choose_role"));
        return c.Call("PUT", "/repos/" + f.Name + "/collaborators/" + login, f.Organization ? D("permission", role) : D()).Code == 201;
    }

    public static void ChangeRole(Client c, RepoFacts seen, string written, string login, string role) {
        if (!ValidLogin(login)) throw new WriteException(Language.T("writes.account_name_not_valid"));
        var f = Reconfirm(c, seen, written, RepoAction.Collaborators);
        if (!f.Organization) throw new WriteException(Language.T("writes.personal_repos_github_only_one"));
        if (!Roles.Contains(role)) throw new WriteException(Language.T("writes.choose_role"));
        c.Call("PUT", "/repos/" + f.Name + "/collaborators/" + login, D("permission", role));
    }

    public static void RemoveCollaborator(Client c, RepoFacts seen, string written, string login) {
        if (!ValidLogin(login)) throw new WriteException(Language.T("writes.account_name_not_valid"));
        var f = Reconfirm(c, seen, written, RepoAction.Collaborators);
        c.Call("DELETE", "/repos/" + f.Name + "/collaborators/" + login, null);
    }

    public static void WithdrawInvitation(Client c, RepoFacts seen, string written, long invitation) {
        var f = Reconfirm(c, seen, written, RepoAction.Collaborators);
        c.Call("DELETE", "/repos/" + f.Name + "/invitations/" + invitation, null);
    }

    public static void DeleteRepo(Client reader, Client disposable, string login, RepoFacts seen, string written) {
        try {
            var f = Reconfirm(reader, seen, written, RepoAction.Deletion);
            string who = S(disposable.Call("GET", "/user", null).D, "login");
            if (!Organization.Equal(who, login)) throw new WriteException(Language.T("writes.browser_authorized_account_using_deleted", "who", Screener.MakeVisible(who), "login", login));
            if (!disposable.HasPermission("delete_repo")) throw new WriteException(Language.T("writes.permission_delete_not_given_github"));
            disposable.Call("DELETE", "/repos/" + f.Name, null);
        } finally { disposable.Discard(); }
    }

    public static void RenameInArchive(Archive a, string oldName, string newName) {
        foreach (var r in a.Repo.Where(x => x.Name == oldName)) {
            r.Name = newName;
            r.Owner = newName.Substring(0, newName.IndexOf('/'));
        }
        foreach (var e in a.Items.Where(x => x.Repo == oldName)) {
            e.Repo = newName;
            string before = "https://github.com/" + oldName + "/";
            if (e.Url != null && e.Url.StartsWith(before)) e.Url = "https://github.com/" + newName + "/" + e.Url.Substring(before.Length);
        }
    }

    public static void MarkDeleted(Archive a, string name) { foreach (var r in a.Repo.Where(x => x.Name == name)) { r.Missing = true; r.Followed = false; } }
}
