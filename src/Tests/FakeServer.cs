using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class FakeServer : IDisposable {
    public class Request {
        public string Method, Path, Auth, Body;
        public Dictionary<string, object> D { get { try { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(Body) as Dictionary<string, object> ?? new Dictionary<string, object>(); } catch (ArgumentException) { return new Dictionary<string, object>(); } } }
    }
    public class FakeRelease { public long Id; public string Tag, Title = "", Note = "", Last; public bool Draft; public List<KeyValuePair<string, long>> Assets = new List<KeyValuePair<string, long>>(); public Dictionary<string, long> Downloaded = new Dictionary<string, long>(); }
    public class FakeCommit { public string Sha, Tree, Message = "", Email = ""; public Dictionary<string, string> File; public List<string> Parents = new List<string>(); }
    public class FakeBranch { public string Sha; public Dictionary<string, string> File = new Dictionary<string, string>(); }
    public class FakeRepo {
        public long Id; public string Name, Type = "User", Role = "admin", Description = "", Head, Website = ""; public bool Private, Archived, Wiki = true; public int Commit, Stars, Forks, Watchers;
        public Dictionary<string, object> Traffic = new Dictionary<string, object>();
        public Dictionary<string, string> File = new Dictionary<string, string>();
        public List<FakeRelease> Release = new List<FakeRelease>();
        public Dictionary<string, string> People = new Dictionary<string, string>();
        public Dictionary<long, string> Invitations = new Dictionary<long, string>();
        public Dictionary<string, FakeBranch> Branches = new Dictionary<string, FakeBranch>();
        public Dictionary<string, long> FakeByteSize = new Dictionary<string, long>();
        public List<string> EmailCommit = new List<string>();
        public int ExtraBranches, Issue, Pull;
        public bool Cut;
        public Dictionary<string, FakeCommit> History = new Dictionary<string, FakeCommit>();
        public List<string> BelowTip = new List<string>();
        public Dictionary<string, List<string>> ParentsOf = new Dictionary<string, List<string>>();
        public Dictionary<string, List<object>> Lists = new Dictionary<string, List<object>>();
    }
    class ForcedResponse { public int Code, Times = 1; public string Text; public Dictionary<string, string> Headers; }

    readonly TcpListener l = new TcpListener(IPAddress.Loopback, 0);
    readonly Dictionary<string, ForcedResponse> forced = new Dictionary<string, ForcedResponse>();
    readonly Dictionary<string, byte[]> blob = new Dictionary<string, byte[]>();
    readonly Dictionary<string, List<object>> trees = new Dictionary<string, List<object>>();
    readonly Dictionary<string, string> commit = new Dictionary<string, string>();
    int count = 1000;
    volatile bool finished;
    public readonly int Gateway;
    public readonly List<Request> RequestLog = new List<Request>();
    public readonly Dictionary<string, FakeRepo> Repo = new Dictionary<string, FakeRepo>();
    public readonly Dictionary<string, string> Permissions = new Dictionary<string, string>();
    public readonly Dictionary<string, string> Who = new Dictionary<string, string>();
    public readonly Dictionary<string, Dictionary<string, object>> Org = new Dictionary<string, Dictionary<string, object>>();
    public string UploadUrl;

    public FakeServer() {
        l.Start();
        Gateway = ((IPEndPoint)l.LocalEndpoint).Port;
        new Thread(Turn) { IsBackground = true }.Start();
    }

    public void Dispose() { finished = true; l.Stop(); }

    public int TransferDelay;
    readonly Dictionary<string, int> hidden = new Dictionary<string, int>();

    public int Writes { get { lock (this) return RequestLog.Count(x => x.Method != "GET"); } }
    public List<Request> Of(string method, string path) { lock (this) return RequestLog.Where(x => x.Method == method && x.Path.Split('?')[0] == path).ToList(); }
    public Request Last { get { lock (this) return RequestLog.LastOrDefault(x => x.Method != "GET"); } }

    public FakeRepo New(string name, string type, string role, bool isPrivate) {
        lock (this) {
            var r = new FakeRepo { Id = ++count, Name = name, Type = type, Role = role, Private = isPrivate };
            Repo[name] = r;
            return r;
        }
    }

    public void Put(FakeRepo r, string path, byte[] data) {
        lock (this) {
            string b = "b" + (++count); blob[b] = data;
            r.File[path] = b;
            NewCommit(r);
        }
    }

    public void PutBranch(FakeRepo r, string branch, string path, byte[] data) {
        lock (this) {
            string b = "b" + (++count); blob[b] = data;
            FakeBranch x;
            if (!r.Branches.TryGetValue(branch, out x)) r.Branches[branch] = x = new FakeBranch();
            x.File[path] = b;
            x.Sha = "r" + (++count);
        }
    }

    public string Blob(byte[] data) { lock (this) { string b = "b" + (++count); blob[b] = data; return b; } }

    public string OldInstall(FakeRepo r, string message, string email, Dictionary<string, string> file, params string[] parents) {
        lock (this) {
            string sha = "o" + (++count);
            r.History[sha] = new FakeCommit { Sha = sha, Tree = "a" + sha, Message = message, Email = email, File = file, Parents = parents.ToList() };
            return sha;
        }
    }

    static object[] Page(Request q, IEnumerable<object> all) {
        var mp = Regex.Match(q.Path, @"[?&]page=(\d+)"); var mn = Regex.Match(q.Path, @"[?&]per_page=(\d+)");
        if (!mn.Success) return all.ToArray();
        int n = int.Parse(mn.Groups[1].Value), p = mp.Success ? int.Parse(mp.Groups[1].Value) : 1;
        return all.Skip((p - 1) * n).Take(n).ToArray();
    }

    string NewCommit(FakeRepo r) { r.Commit++; string c = "c" + (++count); commit[c] = "t" + (++count); r.Head = c; return c; }

    public void Force(string method, string path, int code, string text, Dictionary<string, string> headers) { ForceTimes(method, path, code, text, headers, 1); }
    public void ForceTimes(string method, string path, int code, string text, Dictionary<string, string> headers, int times) { lock (this) forced[method + " " + path] = new ForcedResponse { Code = code, Text = text, Headers = headers, Times = times }; }

    void Turn() {
        while (!finished) {
            TcpClient c;
            try { c = l.AcceptTcpClient(); } catch (SocketException) { return; } catch (ObjectDisposedException) { return; }
            var x = c;
            ThreadPool.QueueUserWorkItem(_ => { try { Serve(x); } catch (IOException) { } catch (SocketException) { } finally { x.Close(); } });
        }
    }

    void Serve(TcpClient c) {
        var s = c.GetStream();
        var data = new MemoryStream(); var b = new byte[65536]; int end = -1;
        while (end < 0) {
            int n = s.Read(b, 0, b.Length);
            if (n <= 0) return;
            data.Write(b, 0, n);
            var a = data.ToArray();
            for (int i = 3; i < a.Length; i++) if (a[i - 3] == 13 && a[i - 2] == 10 && a[i - 1] == 13 && a[i] == 10) { end = i + 1; break; }
        }
        string head = Encoding.ASCII.GetString(data.ToArray(), 0, end);
        var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
        var p = lines[0].Split(' ');
        var intest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in lines.Skip(1)) { int k = r.IndexOf(':'); if (k > 0) intest[r.Substring(0, k)] = r.Substring(k + 1).Trim(); }
        int lung = intest.ContainsKey("Content-Length") ? int.Parse(intest["Content-Length"]) : 0;
        while (data.Length < end + lung) { int n = s.Read(b, 0, b.Length); if (n <= 0) break; data.Write(b, 0, n); }
        var body = new byte[lung];
        Array.Copy(data.ToArray(), end, body, 0, Math.Min(lung, (int)data.Length - end));
        var q = new Request { Method = p[0], Path = p[1], Auth = intest.ContainsKey("Authorization") ? intest["Authorization"] : "", Body = Encoding.UTF8.GetString(body) };
        int code = 200; string json = "{}"; Dictionary<string, string> extra = null;
        string token = q.Auth.StartsWith("Bearer ") ? q.Auth.Substring(7) : "";
        lock (this) {
            RequestLog.Add(q);
            ForcedResponse f;
            string key = q.Method + " " + q.Path.Split('?')[0];
            if (forced.TryGetValue(key, out f)) { if (--f.Times <= 0) forced.Remove(key); code = f.Code; json = f.Text; extra = f.Headers; }
            else {
                object o;
                link = null;
                try { code = Via(q, token, body, out o); } catch (KeyNotFoundException) { code = 404; o = D("message", "Not Found"); }
                if (link != null) extra = new Dictionary<string, string> { { "Link", link } };
                json = o == null ? "" : new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(o);
            }
            if (Permissions.ContainsKey(token)) { extra = extra ?? new Dictionary<string, string>(); extra["X-OAuth-Scopes"] = Permissions[token]; }
        }
        var output = Encoding.UTF8.GetBytes(json);
        var sb = new StringBuilder("HTTP/1.1 " + code + " Fake\r\nContent-Type: application/json\r\nContent-Length: " + output.Length + "\r\nConnection: close\r\n");
        if (extra != null) foreach (var e in extra) sb.Append(e.Key + ": " + e.Value + "\r\n");
        sb.Append("\r\n");
        var t = Encoding.ASCII.GetBytes(sb.ToString());
        s.Write(t, 0, t.Length);
        s.Write(output, 0, output.Length);
    }

    public static Dictionary<string, object> D(params object[] kv) { var d = new Dictionary<string, object>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1]; return d; }

    static object Json(FakeRepo r) {
        bool am = r.Role == "admin", ge = am || r.Role == "maintain", sc = ge || r.Role == "write", tr = sc || r.Role == "triage";
        return D("id", r.Id, "full_name", r.Name, "owner", D("login", r.Name.Split('/')[0], "type", r.Type), "description", r.Description, "homepage", r.Website, "has_wiki", r.Wiki, "default_branch", "main", "private", r.Private, "archived", r.Archived,
            "stargazers_count", r.Stars, "forks_count", r.Forks, "subscribers_count", r.Watchers, "permissions", D("admin", am, "maintain", ge, "push", sc, "triage", tr, "pull", true));
    }

    object Json(FakeRelease r) {
        return D("id", r.Id, "tag_name", r.Tag, "name", r.Title, "body", r.Note, "draft", r.Draft, "prerelease", false, "upload_url", UploadUrl ?? "",
            "assets", r.Assets.Select(a => (object)D("name", a.Key, "size", a.Value, "download_count", r.Downloaded.ContainsKey(a.Key) ? r.Downloaded[a.Key] : 0L)).ToArray());
    }

    string link;
    public string FakeLink;

    int Via(Request q, string token, byte[] data, out object o) {
        o = D();
        string full = q.Path.Split('?')[0], m = q.Method;
        var d = q.D;
        if (full == "/user") { o = D("login", Who.ContainsKey(token) ? Who[token] : "nobody", "id", 4242); return 200; }
        if (full == "/user/memberships/orgs") { o = Org.Select(x => (object)D("organization", D("login", x.Key), "role", x.Value.ContainsKey("role") ? x.Value["role"] : "member")).ToArray(); return 200; }
        if (full.StartsWith("/orgs/") && m == "GET") { o = Org[full.Substring(6)]; return 200; }
        if (full.StartsWith("/users/")) { string u = full.Substring(7); o = D("login", u, "type", Org.ContainsKey(u) ? "Organization" : "User"); return 200; }
        if (m == "POST" && (full == "/user/repos" || (full.StartsWith("/orgs/") && full.EndsWith("/repos")))) {
            string ch = full == "/user/repos" ? (Who.ContainsKey(token) ? Who[token] : "nobody") : full.Split('/')[2], name = ch + "/" + d["name"];
            if (Repo.ContainsKey(name)) { o = D("message", "name already exists on this account"); return 422; }
            var nu = New(name, full == "/user/repos" ? "User" : "Organization", "admin", !d.ContainsKey("private") || (bool)d["private"]);
            nu.Description = Convert.ToString(d["description"]);
            if (d.ContainsKey("auto_init") && (bool)d["auto_init"]) Put(nu, "README.md", Encoding.UTF8.GetBytes("# " + d["name"]));
            o = Json(nu);
            return 201;
        }
        if (full.StartsWith("/uploads/")) full = full.Substring("/uploads".Length);
        var seg = full.Split('/');
        if (seg.Length < 4 || seg[1] != "repos") return 404;
        string nameRead = seg[2] + "/" + seg[3];
        int again;
        if (m == "GET" && seg.Length == 4 && hidden.TryGetValue(nameRead, out again) && again > 0) { hidden[nameRead] = again - 1; o = D("message", "Not Found"); return 404; }
        var r = Repo[nameRead];
        string base_ = "/repos/" + r.Name, rest = full.Substring(base_.Length);
        if (m == "GET" && rest.StartsWith("/traffic/")) {
            object t;
            if (r.Traffic.TryGetValue(rest, out t)) { o = t; return 200; }
            o = D("message", "Must have push access to repository");
            return 403;
        }
        if (rest == "") {
            if (m == "GET") { o = Json(r); return 200; }
            if (m == "DELETE") {
                if (!Permissions.ContainsKey(token) || !Permissions[token].Contains("delete_repo")) { o = D("message", "Must have admin rights to Repository."); return 403; }
                Repo.Remove(r.Name); o = null; return 204;
            }
            if (m == "PATCH") {
                if (d.ContainsKey("description")) r.Description = Convert.ToString(d["description"]);
                if (d.ContainsKey("homepage")) r.Website = Convert.ToString(d["homepage"]);
                if (d.ContainsKey("archived")) r.Archived = (bool)d["archived"];
                if (d.ContainsKey("private")) r.Private = (bool)d["private"];
                if (d.ContainsKey("name")) { Repo.Remove(r.Name); r.Name = seg[2] + "/" + d["name"]; Repo[r.Name] = r; }
                o = Json(r);
                return 200;
            }
        }
        if (rest == "/transfer" && m == "POST") {
            string a = Convert.ToString(d["new_owner"]);
            o = Json(r);
            if (Org.ContainsKey(a)) {
                Repo.Remove(r.Name); r.Name = a + "/" + seg[3]; r.Type = "Organization"; Repo[r.Name] = r;
                if (TransferDelay > 0) hidden[r.Name] = TransferDelay;
            }
            return 202;
        }
        if (rest == "/git/ref/heads/main") { if (r.Head == null) { o = D("message", "Git Repository is empty."); return 409; } o = D("object", D("sha", r.Head)); return 200; }
        if (rest == "/branches") {
            var tips = new List<object>();
            if (r.Head != null) tips.Add(D("name", "main", "commit", D("sha", r.Head)));
            foreach (var x in r.Branches) tips.Add(D("name", x.Key, "commit", D("sha", x.Value.Sha)));
            for (int i = 0; i < r.ExtraBranches; i++) tips.Add(D("name", "other" + i, "commit", D("sha", r.Head)));
            o = Page(q, tips);
            return 200;
        }
        if (rest == "/issues") {
            var labels = r.Lists.ContainsKey("/issues") ? r.Lists["/issues"] : new List<object>();
            o = Page(q, labels.Concat(Enumerable.Range(1, r.Issue).Select(x => (object)D("number", x))).Concat(Enumerable.Range(1, r.Pull).Select(x => (object)D("number", x, "pull_request", D()))));
            return 200;
        }
        if (Regex.IsMatch(rest, @"^/pulls/\d+/reviews$")) { o = Page(q, r.Lists.ContainsKey(rest) ? r.Lists[rest] : new List<object>()); return 200; }
        if (rest == "/tags" || rest == "/pulls" || rest == "/issues/comments" || rest == "/pulls/comments" || rest == "/comments") { o = Page(q, r.Lists.ContainsKey(rest) ? r.Lists[rest] : new List<object>()); return 200; }
        if (rest == "/commits") {
            var ms = Regex.Match(q.Path, @"[?&]sha=([^&]+)");
            var commits = new List<object>(); var seen = new HashSet<string>(); var toSee = new Queue<string>();
            toSee.Enqueue(ms.Success ? ms.Groups[1].Value : r.Head);
            while (toSee.Count > 0) {
                string sha = toSee.Dequeue();
                if (!seen.Add(sha)) continue;
                FakeCommit cf; r.History.TryGetValue(sha, out cf);
                List<string> gen;
                string post = cf != null ? cf.Email : "";
                if (cf != null) gen = cf.Parents;
                else if (sha.StartsWith("email")) { gen = new List<string>(); post = r.EmailCommit[int.Parse(sha.Substring(5))]; }
                else if (r.ParentsOf.TryGetValue(sha, out gen)) { }
                else gen = r.BelowTip.Concat(sha == r.Head ? r.EmailCommit.Select((e, i) => "email" + i) : new string[0]).ToList();
                commits.Add(D("sha", sha, "parents", gen.Select(x => (object)D("sha", x)).ToArray(),
                    "commit", D("tree", D("sha", cf != null ? cf.Tree : sha), "message", cf != null ? cf.Message : "", "author", D("email", post), "committer", D("email", "noreply@github.com"))));
                foreach (var x in gen) toSee.Enqueue(x);
            }
            o = Page(q, commits);
            return 200;
        }
        if (rest.StartsWith("/git/trees/") && m == "GET") {
            string tip = rest.Substring(11);
            var inside = r.History.Values.Where(x => x.Tree == tip).Select(x => x.File).FirstOrDefault() ?? r.Branches.Values.Where(x => x.Sha == tip).Select(x => x.File).FirstOrDefault() ?? r.File;
            o = D("truncated", r.Cut, "tree", inside.Select(x => (object)D("path", x.Key, "type", "blob", "sha", x.Value, "size", r.FakeByteSize.ContainsKey(x.Key) ? r.FakeByteSize[x.Key] : (long)blob[x.Value].Length)).ToArray());
            return 200;
        }
        if (rest.StartsWith("/git/blobs/")) { o = D("content", Convert.ToBase64String(blob[rest.Substring(11)]), "encoding", "base64"); return 200; }
        if (rest == "/git/blobs" && m == "POST") { string s = "b" + (++count); blob[s] = Convert.FromBase64String(Convert.ToString(d["content"])); o = D("sha", s); return 201; }
        if (rest.StartsWith("/git/commits/")) { o = D("tree", D("sha", commit[rest.Substring(13)])); return 200; }
        if (rest == "/git/trees" && m == "POST") { string s = "t" + (++count); trees[s] = ((IEnumerable)d["tree"]).Cast<object>().ToList(); o = D("sha", s); return 201; }
        if (rest == "/git/commits" && m == "POST") { string s = "c" + (++count); commit[s] = Convert.ToString(d["tree"]); o = D("sha", s); return 201; }
        if (rest == "/git/refs/heads/main" && m == "PATCH") {
            string s = Convert.ToString(d["sha"]);
            foreach (var v in trees[commit[s]].OfType<Dictionary<string, object>>()) r.File[Convert.ToString(v["path"])] = Convert.ToString(v["sha"]);
            r.Head = s; r.Commit++;
            o = D("object", D("sha", s));
            return 200;
        }
        if (rest.StartsWith("/contents/")) {
            string f = Uri.UnescapeDataString(rest.Substring(10));
            if (m == "GET") { o = D("type", "file", "encoding", "base64", "sha", r.File[f], "content", Convert.ToBase64String(blob[r.File[f]])); return 200; }
            if (m == "PUT") {
                if (r.File.ContainsKey(f) && (!d.ContainsKey("sha") || Convert.ToString(d["sha"]) != r.File[f])) { o = D("message", "does not match"); return 409; }
                string s = "b" + (++count); blob[s] = Convert.FromBase64String(Convert.ToString(d["content"])); r.File[f] = s;
                o = D("commit", D("sha", NewCommit(r)));
                return 201;
            }
            if (m == "DELETE") { r.File.Remove(f); NewCommit(r); o = D("commit", D("sha", r.Head)); return 200; }
        }
        if (rest == "/releases/latest") { var u = r.Release.LastOrDefault(x => x.Last == "true"); if (u == null) { o = D("message", "Not Found"); return 404; } o = Json(u); return 200; }
        if (rest == "/releases") {
            if (m == "GET") {
                o = Page(q, r.Release.Select(x => Json(x)));
                var mper = Regex.Match(q.Path, @"[?&]per_page=(\d+)"); var mpag = Regex.Match(q.Path, @"[?&]page=(\d+)");
                int page = mpag.Success ? int.Parse(mpag.Groups[1].Value) : 1;
                if (mper.Success && page * int.Parse(mper.Groups[1].Value) < r.Release.Count) link = FakeLink ?? ("<http://127.0.0.1:" + Gateway + "/repos/" + r.Name + "/releases?per_page=" + mper.Groups[1].Value + "&page=" + (page + 1) + ">; rel=\"next\"");
                return 200;
            }
            string tag = Convert.ToString(d["tag_name"]);
            if (r.Release.Any(x => x.Tag == tag)) { o = D("message", "Validation Failed", "errors", new object[] { D("resource", "Release", "code", "already_exists", "field", "tag_name") }); return 422; }
            var n = new FakeRelease { Id = ++count, Tag = tag, Title = Convert.ToString(d["name"]), Note = Convert.ToString(d["body"]), Draft = d.ContainsKey("draft") && (bool)d["draft"] };
            r.Release.Add(n);
            o = Json(n);
            return 201;
        }
        var mr = Regex.Match(rest, @"^/releases/(\d+)(/assets)?$");
        if (mr.Success) {
            var x = r.Release.FirstOrDefault(z => z.Id == long.Parse(mr.Groups[1].Value));
            if (x == null) { o = D("message", "Not Found"); return 404; }
            if (mr.Groups[2].Success) {
                string name = Uri.UnescapeDataString(q.Path.Substring(q.Path.IndexOf("name=") + 5));
                x.Assets.Add(new KeyValuePair<string, long>(name, data.Length));
                o = D("name", name, "size", data.Length);
                return 201;
            }
            if (m == "GET") { o = Json(x); return 200; }
            if (m == "DELETE") { r.Release.Remove(x); o = null; return 204; }
            if (m == "PATCH") {
                if (d.ContainsKey("tag_name")) x.Tag = Convert.ToString(d["tag_name"]);
                if (d.ContainsKey("name")) x.Title = Convert.ToString(d["name"]);
                if (d.ContainsKey("body")) x.Note = Convert.ToString(d["body"]);
                if (d.ContainsKey("draft")) x.Draft = (bool)d["draft"];
                if (d.ContainsKey("make_latest")) x.Last = Convert.ToString(d["make_latest"]);
                o = Json(x);
                return 200;
            }
        }
        if (rest == "/collaborators") { o = r.People.Select(x => (object)D("login", x.Key, "role_name", x.Value)).ToArray(); return 200; }
        if (rest == "/invitations") { o = r.Invitations.Select(x => (object)D("id", x.Key, "invitee", D("login", x.Value), "permissions", "write")).ToArray(); return 200; }
        var mi = Regex.Match(rest, @"^/invitations/(\d+)$");
        if (mi.Success && m == "DELETE") { r.Invitations.Remove(long.Parse(mi.Groups[1].Value)); o = null; return 204; }
        var mp = Regex.Match(rest, @"^/collaborators/([^/]+)(/permission)?$");
        if (mp.Success) {
            string who = mp.Groups[1].Value;
            if (mp.Groups[2].Success) { o = D("role_name", r.People[who]); return 200; }
            if (m == "DELETE") { r.People.Remove(who); o = null; return 204; }
            if (m == "PUT") {
                if (r.People.ContainsKey(who)) { r.People[who] = d.ContainsKey("permission") ? Convert.ToString(d["permission"]) : "write"; o = null; return 204; }
                r.Invitations[++count] = who;
                o = D("id", count);
                return 201;
            }
        }
        o = D("message", "Not Found");
        return 404;
    }
}
