using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

public class Client {
    const string ApiBase = "https://api.github.com";
    const string UploadsUrl = "https://uploads.github.com";
    static readonly string[] Forbidden = { "delete_repo", "admin:org", "workflow" };

    readonly string root, loadRoot;
    readonly bool saved;
    string token;
    public string Permissions;

    Client(string r, string c, string t, bool s) { root = r; loadRoot = c; token = t; saved = s; }
    public static Client Real(string token, bool saved) { return new Client(ApiBase, UploadsUrl, token, saved); }
    public static Client Fake(int gateway, string token, bool saved) { string b = "http://127.0.0.1:" + gateway; return new Client(b, b + "/uploads", token, saved); }

    public void Discard() { token = null; }
    public bool Discarded { get { return token == null; } }

    public static bool HasForbidden(string permissions) { return Pieces(permissions).Any(x => Forbidden.Contains(x)); }
    public bool HasPermission(string p) { return Pieces(Permissions).Contains(p); }
    static string[] Pieces(string s) { return (s ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray(); }

    public Response Call(string method, string path, object body) {
        byte[] b = body == null ? null : Encoding.UTF8.GetBytes(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(body));
        return Send(root, method, path, b, "application/json", 60000);
    }

    public Response Load(string path, byte[] data) { return Send(loadRoot, "POST", path, data, "application/octet-stream", 30 * 60000); }

    Response Send(string website, string method, string path, byte[] body, string type, int wait) {
        if (token == null) throw new WriteException(Language.T("writes.access_already_been_used_thrown"));
        if (!path.StartsWith("/") || path.StartsWith("//") || path.Contains("://") || path.Contains("\\")) throw new WriteException(Language.T("writes.invalid_address_request_not_sent"));
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        var req = (HttpWebRequest)WebRequest.Create(website + path);
        req.Method = method;
        req.UserAgent = "ProjectManager/" + Program.Version;
        req.Accept = "application/vnd.github+json";
        req.Headers["X-GitHub-Api-Version"] = "2022-11-28";
        req.Headers["Authorization"] = "Bearer " + token;
        req.AllowAutoRedirect = false;
        req.ServicePoint.Expect100Continue = false;
        req.Timeout = wait; req.ReadWriteTimeout = wait;
        string place = path.Split('?')[0];
        try {
            if (body != null || method != "GET") {
                req.ContentType = type;
                req.ContentLength = body == null ? 0 : body.Length;
                if (body != null) using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            }
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) {
                string text = sr.ReadToEnd();
                ReadPermissions(r.Headers["X-OAuth-Scopes"]);
                int c = (int)r.StatusCode;
                if (method != "GET") DataStore.Log("write " + method + " " + place + ": " + c);
                if (c >= 300) throw new WriteException(c, method == "GET" ? Language.T("writes.repo_renamed_moved_github_press")
                                                            : Language.T("writes.repo_renamed_moved_github_wrote"));
                return new Response { Code = c, Json = text.Length == 0 ? null : new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(text) };
            }
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h == null) throw new WriteException(Language.T("common.github_not_responding_check_internet"));
            string text = "";
            try { using (var sr = new StreamReader(h.GetResponseStream(), Encoding.UTF8)) text = sr.ReadToEnd(); } catch { }
            int c = (int)h.StatusCode;
            if (method != "GET" || (c != 404 && c != 409)) DataStore.Log("write " + method + " " + place + ": " + c);
            ReadPermissions(h.Headers["X-OAuth-Scopes"]);
            throw new WriteException(c, Phrases.Of(c, text, h.Headers["Retry-After"] != null || h.Headers["X-RateLimit-Remaining"] == "0"));
        } catch (ArgumentException) { throw new WriteException(Language.T("common.github_gave_answer_dont_understand")); }
    }

    void ReadPermissions(string saidFromGitHub) {
        if (saidFromGitHub == null) return;
        Permissions = saidFromGitHub;
        if (saved && HasForbidden(saidFromGitHub)) {
            Discard();
            throw new WriteException(Language.T("writes.accounts_key_more_permissions_program"));
        }
    }
}
