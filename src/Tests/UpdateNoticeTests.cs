using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class UpdateNoticeTests {
    const string RequestPath = "/repos/nscentart/Project-Manager-for-GitHub/releases/latest";
    const string Base = "https://github.com/nscentart/Project-Manager-for-GitHub/";

    static string Response(string tag, string page, string asset) {
        return "{\"tag_name\":\"" + tag + "\",\"body\":\"note\",\"html_url\":\"" + page + "\",\"assets\":[{\"name\":\"" + asset + "\",\"browser_download_url\":\"https://evil.example/x.exe\",\"digest\":\"sha256:abc\"}]}";
    }

    class BrokenServer : IDisposable {
        readonly TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        readonly ManualResetEvent end = new ManualResetEvent(false);
        public readonly int Gateway;
        public BrokenServer(string response, bool stay) {
            l.Start();
            Gateway = ((IPEndPoint)l.LocalEndpoint).Port;
            new Thread(() => {
                try {
                    using (var c = l.AcceptTcpClient()) {
                        var st = c.GetStream();
                        st.Read(new byte[8192], 0, 8192);
                        var b = Encoding.ASCII.GetBytes(response);
                        st.Write(b, 0, b.Length);
                        st.Flush();
                        if (stay) end.WaitOne(30000);
                    }
                } catch { }
            }) { IsBackground = true }.Start();
        }
        public void Dispose() { end.Set(); l.Stop(); }
    }

    static void Render(Form f) {
        f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000);
        f.Show();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(10); }
    }

    static bool Until(Func<bool> c) {
        for (int i = 0; i < 600; i++) { if (c()) return true; Application.DoEvents(); Thread.Sleep(10); }
        return c();
    }

    static IEnumerable<Control> All(Control c) { foreach (Control x in c.Controls) { yield return x; foreach (var y in All(x)) yield return y; } }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL a test data folder is needed\n"); return 1; }
        Language.Use("en");
        Dialog.Silent = true;
        MainWindow.ScreenshotOnly = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.K = g.DpiX / 96f;
        string realApi = UpdateCheck.Api; int realWait = UpdateCheck.WaitMs, realTime = UpdateCheck.TimeMs;
        float realScale = Theme.K;
        var ownVersion = new Version("1.0.0");
        try {
            foreach (var good in new[] { Base + "releases/tag/v0.6", Base + "releases/tag/v1.1", Base + "releases/latest" }) v("good address: " + good, UpdateCheck.GoodAddress(good));
            foreach (var bad in new[] {
                "https://github.com/nscentart/Project-Manager-for-GitHubFake/releases/download/v1/x.exe", "https://github.com/other/Project-Manager-for-GitHub/x.exe",
                "http://github.com/nscentart/Project-Manager-for-GitHub/x", "https://github.com.fake.test/nscentart/Project-Manager-for-GitHub/x",
                "https://github.com@fake.test/nscentart/Project-Manager-for-GitHub/x", Base + "../Other/x.exe", Base + "%2e%2e/Other/x.exe", Base + "\" --other",
                Base + "a b", Base + "?x=1", Base + "#x", Base + "a\\b", "HTTPS://GITHUB.COM/nscentart/Project-Manager-for-GitHub/x", "file:///C:/x.exe", "", null,
                Base, Base + "issues", Base + "tags", Base + "releases", Base + "releases/", Base + "releases/tag/", Base + "releases/tag/v1.1/", Base + "releases/tag/v1.1/x",
                Base + "releases/tag/..", Base + "releases/tag/.", Base + "releases/tag/%2e%2e", Base + "releases/tag/v1.1?x=1", Base + "releases/tag/v1.1#x", Base + "releases/tag/v1.1\n",
                Base + "releases/latest/", Base + "releases/latest/download/ProjectManager.exe", Base + "releases/latestx", Base + "releases/download/v1.1/ProjectManager.exe",
                Base + "RELEASES/latest", "https://github.com/nscentart/project-manager-for-github/releases/latest", "https://www.github.com/nscentart/Project-Manager-for-GitHub/releases/latest" })
                v("rejected address: " + (bad ?? "(null)"), !UpdateCheck.GoodAddress(bad));

            var n = UpdateCheck.ResponseFrom(Response("v1.1", Base + "releases/tag/v1.1", "ProjectManager.exe"), ownVersion);
            v("v1.1: notified, with its page", n != null && n.Label == "1.1" && n.Version == new Version("1.1.0") && n.Page == Base + "releases/tag/v1.1" && n.Note == "note");
            v("v1.0: equal, nothing", UpdateCheck.ResponseFrom(Response("v1.0", Base + "releases/tag/v1.0", "ProjectManager.exe"), ownVersion) == null);
            v("v0.9: older, nothing", UpdateCheck.ResponseFrom(Response("v0.9", Base + "releases/tag/v0.9", "ProjectManager.exe"), ownVersion) == null);
            v("meaningless label: nothing", UpdateCheck.ResponseFrom(Response("latest", Base + "releases/tag/x", "ProjectManager.exe"), ownVersion) == null);
            v("without our file: nothing", UpdateCheck.ResponseFrom(Response("v1.1", Base + "releases/tag/v1.1", "Other.exe"), ownVersion) == null);
            foreach (var outside in new[] { "https://evil.example/x.exe", "https://github.com/nscentart/Project-Manager-for-GitHub/../../other/x.exe", "https://github.com/nscentart/Project-Manager-for-GitHub/releases/download/v1.1/x.exe?a=1", "", "javascript:alert(1)", Base + "releases/download/v1.1/ProjectManager.exe", Base + "issues", Base })
                v("page outside the repo: the official page (" + outside + ")", UpdateCheck.ResponseFrom(Response("v1.1", outside, "ProjectManager.exe"), ownVersion).Page == UpdateCheck.LastPage);
            v("no page: the official page", UpdateCheck.ResponseFrom("{\"tag_name\":\"v1.1\",\"assets\":[{\"name\":\"ProjectManager.exe\"}]}", ownVersion).Page == UpdateCheck.LastPage);
            v("no address to download from is read", typeof(NewVersion).GetField("Url") == null);
            var methods = typeof(UpdateCheck).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Select(x => x.Name).ToList();
            v("there is nothing that downloads, installs or compares a file", !methods.Any(x => x.Contains("Download") || x.Contains("Install") || x.Contains("Download") || x.Contains("Compare") || x.Contains("Hash")));

            string H = string.Concat(Enumerable.Repeat("3F9A1C7E", 8)), H2 = string.Concat(Enumerable.Repeat("0B6D24F8", 8)), h = H.ToLowerInvariant();
            Func<string, string> between = x => "\"" + x + "\"";
            Func<string, string, NewVersion> withFingerprint = (digest, body) => UpdateCheck.ResponseFrom("{\"tag_name\":\"v1.1\",\"body\":" + body + ",\"html_url\":\"" + Base + "releases/tag/v1.1\",\"assets\":[{\"name\":\"Other.exe\",\"digest\":\"sha256:" + H2 + "\"},{\"name\":\"ProjectManager.exe\"" + (digest == null ? "" : ",\"digest\":" + digest) + "}]}", ownVersion);
            Func<string, string, string, bool, bool> fingerprint = (digest, body, wait, different) => { var x = withFingerprint(digest, body); return x.Fingerprint == wait && x.DifferentFingerprint == different; };
            string dig = between("sha256:" + h);
            v("fingerprint: from the digest, in uppercase", fingerprint(dig, between("note"), H, false));
            v("fingerprint: digest and notes equal, no alarm", fingerprint(dig, between("x `" + H + "` y"), H, false));
            v("fingerprint: equal even with different letter case", fingerprint(between("sha256:" + H), between(h), H, false));
            v("fingerprint: the same one five times in the notes, no alarm", fingerprint(dig, between(string.Join(" ", Enumerable.Repeat(H, 5))), H, false));
            v("fingerprint: no digest, from the notes", fingerprint(null, between("x `" + H + "` y"), H, false));
            v("fingerprint: no digest, the same one five times in the notes", fingerprint(null, between(string.Join(" ", Enumerable.Repeat(h, 5))), H, false));
            v("fingerprint: neither of the two, not available", fingerprint(null, between("note"), null, false));
            v("fingerprint: the digest of another file does not count", withFingerprint(null, between("note")).Fingerprint == null);
            foreach (var bad in new[] { between("sha256:abc"), between("md5:" + h), between("sha256:" + h.Substring(1)), between("sha256:" + h + "0"), between(" sha256:" + h), between("SHA256:" + h), between(h), between("sha256:" + h.Substring(1) + "g"), between(""), "12", "true", "null", "[]", "{}" }) {
                v("bad digest, counts as absent: " + (bad.Length > 24 ? bad.Substring(0, 24) + "..." : bad), fingerprint(bad, between(H), H, false));
                v("bad digest and empty notes: not available, " + (bad.Length > 24 ? bad.Substring(0, 24) + "..." : bad), fingerprint(bad, "null", null, false));
            }
            foreach (var body in new[] { "null", "12", "[]", "{}", between(""), between(h.Substring(1)), between(h + "0"), between("sha256:abc") }) v("bad notes: not available, " + (body.Length > 24 ? body.Substring(0, 24) + "..." : body), fingerprint(null, body, null, false));
            v("two different fingerprints in the notes, no digest: not available, and the alarm", fingerprint(null, between(H + " " + H2), null, true));
            v("digest and notes differ: alarm, the digest is shown", fingerprint(dig, between("x `" + H2 + "` y"), H, true));
            v("right digest but one of the notes differs: alarm", fingerprint(dig, between(H + " " + H + " " + H2), H, true));

            string startBody = "{\"tag_name\":\"v1.1\",\"bo";
            Func<string, bool, string> broken = (response, stay) => {
                using (var sr = new BrokenServer(response, stay)) {
                    UpdateCheck.Api = "http://127.0.0.1:" + sr.Gateway + RequestPath;
                    try { UpdateCheck.Search(ownVersion); return null; } catch (Exception e) { return e.Message; }
                }
            };
            UpdateCheck.TimeMs = 500;
            string understand = Language.T("github.github_gave_answer_dont_understand");
            string closed = broken("HTTP/1.1 200 OK\r\nContent-Length: 5000\r\nConnection: close\r\n\r\n" + startBody, false);
            v("halfway, closes with the declared length: says so", closed != null && (closed.StartsWith("GitHub is not responding (") || closed == understand));
            v("halfway, closes without length: says so", broken("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n" + startBody, false) == understand);
            string chunked = broken("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n10\r\n" + startBody.Substring(0, 16) + "\r\n", false);
            v("halfway, closes between one chunk and the next: says so", chunked != null && (chunked.StartsWith("GitHub is not responding (") || chunked == understand));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string stopped = broken("HTTP/1.1 200 OK\r\nContent-Length: 5000\r\n\r\n" + startBody, true);
            v("halfway, stays still: after the maximum time says so", stopped != null && stopped.StartsWith("GitHub is not responding (") && clock.ElapsedMilliseconds < 8000);
            clock.Restart();
            string muted = broken("", true);
            v("says nothing: after the maximum time says so", muted != null && muted.StartsWith("GitHub is not responding (") && clock.ElapsedMilliseconds < 8000);
            v("the real maximum time is 10 seconds, even for those that stop halfway", realTime == 10000);
            UpdateCheck.TimeMs = realTime;

            string clean = UpdateCheck.CleanNotes("**Title**\r\n\r\n\r\n\r\n<details><summary>Italiano</summary>\r\n`abc123`\r\n</details>\u202Ex");
            v("notes: they read as text", clean.StartsWith("Title\r\n\r\n") && clean.Contains("Italiano") && clean.Contains("abc123") && !clean.Contains("**") && !clean.Contains("`") && !clean.Contains("<details>"));
            v("notes: invisible characters become visible", clean.Contains("{U+202E}") && !clean.Contains("\u202E"));
            foreach (var tag in new[] { "<details>", "</details>", "<summary>", "</summary>", "<details open>", "<DETAILS open>", "<b>", "</b>", "<br>", "<br/>", "<br />", "<a href=\"https://evil.example/x.exe\">", "</a>", "<img src=x onerror=y>", "<h2>", "<table>", "<!-- note -->", "<!-- on\ntwo lines -->", "<!doctype html>", "<?xml version=\"1.0\"?>", "<a\nhref=\"x\">", "<de<b>tails>", "<de**tails>", "<de`tails>" })
                v("note: via the tag " + tag.Replace("\n", " "), UpdateCheck.CleanNotes("before " + tag + " after") == "before  after");
            string dirty = UpdateCheck.CleanNotes("<details>\n<summary>**Italiano**</summary>\n\n**Download** `ProjectManager.exe`: one < two, two > one, info@nscent.art\n<https://www.nscent.art/software>\n</details>");
            v("notes: the text, the comparisons and the addresses stay", dirty.StartsWith("Italiano\r\n") && dirty.Contains("Download ProjectManager.exe: one < two, two > one, info@nscent.art") && dirty.Contains("https://www.nscent.art/software"));
            v("notes: no tags, no **, no backticks", !dirty.Contains("details") && !dirty.Contains("summary") && !dirty.Contains("**") && !dirty.Contains("`"));
            v("notes: null", UpdateCheck.CleanNotes(null) == "");

            using (var s = new FakeServer()) {
                UpdateCheck.Api = "http://127.0.0.1:" + s.Gateway + RequestPath;
                UpdateCheck.WaitMs = 1;
                Action<string> responds = t => s.Force("GET", RequestPath, 200, t, null);

                responds(Response("v1.1", Base + "releases/tag/v1.1", "ProjectManager.exe"));
                var equal = UpdateCheck.Search(new Version("1.1.0"));
                v("server: same version, nothing", equal == null);
                var reg = s.RequestLog.ToList();
                v("server: one request only", reg.Count == 1 && reg[0].Method == "GET" && reg[0].Path == RequestPath);
                v("server: no key", string.IsNullOrEmpty(reg[0].Auth));
                responds(Response("v1.1", Base + "releases/tag/v1.1", "ProjectManager.exe"));
                var fake = UpdateCheck.Search(new Version("0.9.0"));
                v("server: older, finds the version", fake != null && fake.Label == "1.1");
                responds(Response("v1.1", Base + "releases/tag/v1.1", "ProjectManager.exe"));
                v("server: newer, nothing", UpdateCheck.Search(new Version("9.0.0")) == null);
                int beforeFingerprint = s.RequestLog.Count;
                responds("{\"tag_name\":\"v1.1\",\"body\":\"`" + H2 + "`\",\"html_url\":\"" + Base + "releases/tag/v1.1\",\"assets\":[{\"name\":\"ProjectManager.exe\",\"digest\":\"sha256:" + h + "\",\"browser_download_url\":\"http://127.0.0.1:" + s.Gateway + "/download/ProjectManager.exe\"}]}");
                var withDigest = UpdateCheck.Search(new Version("0.9.0"));
                v("server: fingerprint read and alarm, with one request only", withDigest.Fingerprint == H && withDigest.DifferentFingerprint && s.RequestLog.Count == beforeFingerprint + 1);
                v("server: no file downloaded", s.RequestLog.All(x => x.Path == RequestPath));
                v("server: no write", s.Writes == 0);

                Func<int, string, string> fails = (code, text) => {
                    s.Force("GET", RequestPath, code, text, null);
                    try { UpdateCheck.Search(ownVersion); return null; } catch (Exception e) { return e.Message; }
                };
                v("404: the page is not public", fails(404, "{}") == Language.T("updates.releases_page_github_not_public"));
                string e500 = fails(500, "{}");
                v("500: says GitHub is not responding", e500 != null && e500.StartsWith("GitHub is not responding ("));
                v("broken JSON: says so", fails(200, "{broken") == Language.T("github.github_gave_answer_dont_understand"));
                v("JSON without version: says so", fails(200, "{\"body\":\"x\"}") == Language.T("github.github_gave_answer_dont_understand"));
                v("JSON list: says so", fails(200, "[]") == Language.T("github.github_gave_answer_dont_understand"));
                string e302 = fails(302, "{}");
                v("a redirect is not followed", e302 != null && e302.StartsWith("GitHub is not responding ("));

                var currentVersion = UpdateCheck.Mine();
                string created = "v" + (currentVersion.Major + 1) + "." + currentVersion.Minor;
                Func<bool, string, string, NewVersion> startCheck = (disabled, skipped, response) => {
                    if (response != null) s.Force("GET", RequestPath, 200, response, null);
                    NewVersion view = null;
                    var t = UpdateCheck.CheckAtStartup(() => disabled, () => skipped, x => view = x);
                    if (t != null) t.Join(15000);
                    return view;
                };
                int before = s.RequestLog.Count;
                v("off: no window", startCheck(true, null, null) == null);
                v("off: asks nothing", s.RequestLog.Count == before);
                bool switchedOff = false;
                var ts = UpdateCheck.CheckAtStartup(() => { bool x = switchedOff; switchedOff = true; return x; }, () => null, x => { });
                ts.Join(15000);
                v("off while waiting: asks nothing", s.RequestLog.Count == before);
                v("startup, newer: notifies", startCheck(false, null, Response(created, Base + "releases/latest", "ProjectManager.exe")) != null);
                v("startup, equal: no window", startCheck(false, null, Response("v" + Program.Version, Base + "releases/latest", "ProjectManager.exe")) == null);
                v("startup, older: no window", startCheck(false, null, Response("v0.1", Base + "releases/latest", "ProjectManager.exe")) == null);
                v("startup, skipped: no window", startCheck(false, created.TrimStart('v'), Response(created, Base + "releases/latest", "ProjectManager.exe")) == null);
                v("startup, another one skipped: notifies", startCheck(false, "0.1", Response(created, Base + "releases/latest", "ProjectManager.exe")) != null);
                string logFile = Path.Combine(DataStore.Folder, "ProjectManager.log");
                File.Delete(logFile);
                s.Force("GET", RequestPath, 500, "{}", null);
                v("startup, GitHub silent: no window", startCheck(false, null, null) == null);
                string logAfter = File.Exists(logFile) ? File.ReadAllText(logFile) : "";
                v("startup, GitHub silent: writes it to the log", logAfter.Contains("updates:"));

                string openedUrl = null;
                UpdateCheck.OpenBrowser = x => openedUrl = x;
                UpdateCheck.Open(Base + "releases/tag/v1.2");
                v("open: the good page", openedUrl == Base + "releases/tag/v1.2");
                foreach (var bad in new[] { "https://evil.example/x.exe", "", null, Base + "?x=1", Base, Base + "issues", Base + "releases/download/v1.2/ProjectManager.exe", Base + "releases/tag/v1.2/x" }) {
                    openedUrl = null;
                    UpdateCheck.Open(bad);
                    v("open: a bad address does not reach the browser (" + (bad ?? "null") + ")", openedUrl == UpdateCheck.LastPage);
                }

                string longTexts = string.Join("\n\n", Enumerable.Repeat("**Note** `abc` a line of notes long enough to wrap in the box, like the real ones in five languages", 60));
                var example = new NewVersion { Version = new Version(1, 2, 0), Label = "1.2", Page = Base + "releases/tag/v1.2", Note = "**Note**\n\n`abc`", Fingerprint = H };
                var states = new[] {
                    new KeyValuePair<string, NewVersion>("with fingerprint", example),
                    new KeyValuePair<string, NewVersion>("long notes", new NewVersion { Version = example.Version, Label = "1.2", Page = example.Page, Note = longTexts, Fingerprint = H }),
                    new KeyValuePair<string, NewVersion>("different fingerprint", new NewVersion { Version = example.Version, Label = "1.2", Page = example.Page, Note = example.Note, Fingerprint = H, DifferentFingerprint = true }),
                    new KeyValuePair<string, NewVersion>("without fingerprint", new NewVersion { Version = example.Version, Label = "1.2", Page = example.Page, Note = example.Note }),
                    new KeyValuePair<string, NewVersion>("two in the notes", new NewVersion { Version = example.Version, Label = "1.2", Page = example.Page, Note = example.Note, DifferentFingerprint = true })
                };
                var sentence100 = new Dictionary<string, int>();
                foreach (var scale in new[] { 1f, 1.5f }) {
                    Theme.Simulate(scale);
                    foreach (var c in Language.Codes) {
                        Language.Use(c);
                        foreach (var st in states) {
                            var x = st.Value;
                            string t = c + " " + (int)(scale * 100) + "% " + st.Key + ": ";
                            using (var f = new UpdateNoticeDialog(x)) {
                                Render(f);
                                var fixedControls = f.Controls.Cast<Control>().ToList();
                                v(t + "three buttons, not overlapping, inside the window", f.SkipButton.Right < f.LaterButton.Left && f.LaterButton.Right < f.PageButton.Left && f.PageButton.Right <= f.ClientSize.Width && f.PageButton.Bottom <= f.ClientSize.Height);
                                v(t + "the text of each button fits", new[] { f.SkipButton, f.LaterButton, f.PageButton }.All(b => TextRenderer.MeasureText(b.Text, b.Font).Width < b.Width));
                                v(t + "each button gives its own answer", f.SkipButton.DialogResult == DialogResult.Ignore && f.LaterButton.DialogResult == DialogResult.Cancel && f.PageButton.DialogResult == DialogResult.OK && f.AcceptButton == f.PageButton && f.CancelButton == f.LaterButton);
                                v(t + "the notes are read-only", f.Note.ReadOnly && f.Note.Text.Contains("abc") && !f.Note.Text.Contains("**"));
                                var labels = All(f).OfType<Label>().ToList();
                                v(t + "says the new version and yours", labels.Any(y => y.Text.Contains("1.2")) && labels.Any(y => y.Text.Contains(Program.Version)));
                                v(t + "the labels are not cut", labels.All(y => y.GetPreferredSize(new Size(y.Width, 0)).Height <= y.Height && TextRenderer.MeasureText(y.Text, y.Font, new Size(y.Width - y.Padding.Horizontal, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height + y.Padding.Vertical <= y.Height));
                                v(t + "nothing overlaps and nothing leaves the window", fixedControls.All(y => y.Left >= 0 && y.Top >= 0 && y.Right <= f.ClientSize.Width && y.Bottom <= f.ClientSize.Height) && !fixedControls.Any(y => fixedControls.Any(z => z != y && y.Bounds.IntersectsWith(z.Bounds))));
                                v(t + "the sentence under the title is whole and is not cut", f.Sentence.Text == Language.T("updates.version_program_doesnt_download_install", "v", Program.Version) && f.Sentence.GetPreferredSize(new Size(f.Sentence.Width, 0)).Height <= f.Sentence.Height && f.Sentence.Bottom <= f.Fingerprint.Top);
                                int sentenceWidth = TextRenderer.MeasureText(f.Sentence.Text, f.Sentence.Font).Width;
                                if (scale == 1f && st.Key == "with fingerprint") sentence100[c] = sentenceWidth;
                                if (scale != 1f && st.Key == "with fingerprint") v(t + "the font is really one and a half times larger", Math.Abs((float)sentenceWidth / sentence100[c] - 1.5f) < 0.2f);
                                v(t + "the fingerprint sits in a fixed line above the notes", f.Fingerprint.Parent == f && f.Fingerprint.Visible && !f.Fingerprint.Multiline && f.Fingerprint.ReadOnly && f.Fingerprint.Bottom <= f.Note.Top);
                                if (x.Fingerprint != null) {
                                    int startIndex = f.Fingerprint.Text.IndexOf(H);
                                    v(t + "the line shows the fingerprint", f.Fingerprint.Text == Language.T("updates.sha_256_fingerprint", "h", H) && startIndex > 0 && f.Fingerprint.Text.Length == startIndex + 64);
                                    f.Fingerprint.Select(startIndex, 64);
                                    v(t + "the fingerprint can be selected", f.Fingerprint.SelectedText == H);
                                } else v(t + "the line says it is not available", f.Fingerprint.Text == Language.T("updates.fingerprint_not_available_see_page"));
                                f.Fingerprint.SelectionLength = 0;
                                var lineEnd = f.Fingerprint.GetPositionFromCharIndex(f.Fingerprint.TextLength - 1);
                                int lastWidth = TextRenderer.MeasureText("W", f.Fingerprint.Font, new Size(999, 99), TextFormatFlags.NoPadding).Width;
                                v(t + "the whole line is visible without scrolling", f.Fingerprint.GetPositionFromCharIndex(0).X >= 0 && lineEnd.X + lastWidth <= f.Fingerprint.ClientSize.Width && f.Fingerprint.Height >= TextRenderer.MeasureText("W", f.Fingerprint.Font).Height - Theme.S(4));
                                if (x.DifferentFingerprint) {
                                    v(t + "the alarm is visible, above the fingerprint", f.Alarm != null && f.Alarm.Visible && f.Alarm.Parent == f && f.Alarm.Text == Language.T("updates.fingerprint_differs_between_file_notes") && f.Alarm.Bottom <= f.Fingerprint.Top && f.Alarm.BackColor == Theme.Red);
                                    v(t + "the page can be opened all the same", f.PageButton.Enabled && f.PageButton.DialogResult == DialogResult.OK);
                                } else v(t + "no alarm", f.Alarm == null);
                                f.Close();
                            }
                        }
                    }
                }
                Theme.Simulate(realScale);
                Language.Use("en");

                DataStore.Save(new Archive { Login = "mario", TrustedDomains = DataStore.DefaultTrustedDomains.ToList(), ReposFolder = "" });
                var window = new MainWindow();
                Render(window);
                openedUrl = null;
                MainWindow.NoticeResponse = x => DialogResult.Cancel;
                window.ShowNotice(example, window);
                v("later: remembers nothing and opens nothing", DataStore.Load().SkippedVersion == null && openedUrl == null);
                MainWindow.NoticeResponse = x => DialogResult.OK;
                window.ShowNotice(example, window);
                v("open the page: only the page", openedUrl == example.Page && DataStore.Load().SkippedVersion == null);
                openedUrl = null;
                MainWindow.NoticeResponse = x => DialogResult.Ignore;
                window.ShowNotice(example, window);
                v("skip this one: opens nothing", openedUrl == null);
                v("skip this one: stays after reopening", DataStore.Load().SkippedVersion == "1.2");

                Func<Func<UpdateNoticeDialog, ThemedButton>, Func<Form, DialogResult>> click = which => form => {
                    var av = (UpdateNoticeDialog)form;
                    av.ShowInTaskbar = false; av.StartPosition = FormStartPosition.Manual; av.Location = new Point(-20000, -20000);
                    var tm = new System.Windows.Forms.Timer { Interval = 50 };
                    tm.Tick += delegate { tm.Stop(); which(av).PerformClick(); };
                    tm.Start();
                    var r = av.ShowDialog();
                    tm.Dispose();
                    return r;
                };
                var alarm = new NewVersion { Version = new Version(1, 3, 0), Label = "1.3", Page = Base + "releases/tag/v1.3", Note = "x", Fingerprint = H, DifferentFingerprint = true };
                openedUrl = null;
                MainWindow.NoticeResponse = click(q => q.LaterButton);
                window.ShowNotice(alarm, window);
                v("real window, later: does not open and does not remember", openedUrl == null && DataStore.Load().SkippedVersion == "1.2");
                MainWindow.NoticeResponse = click(q => q.PageButton);
                window.ShowNotice(alarm, window);
                v("real window, different fingerprint: the page opens all the same, only the page", openedUrl == alarm.Page && DataStore.Load().SkippedVersion == "1.2");
                openedUrl = null;
                MainWindow.NoticeResponse = click(q => q.SkipButton);
                window.ShowNotice(alarm, window);
                v("real window, skip: remembers and does not open", openedUrl == null && DataStore.Load().SkippedVersion == "1.3");
                DataStore.Save(new Archive { Login = "mario", TrustedDomains = DataStore.DefaultTrustedDomains.ToList(), ReposFolder = "", SkippedVersion = "1.2" });
                window.Close();
                window = new MainWindow();
                Render(window);

                CheckBox tick = null; ThemedButton now = null; Label says = null; string afterDown = null, afterEqual = null, afterNew = null; bool asked = false;
                MainWindow.SettingsScreenshot = x => {
                    Render(x);
                    tick = All(x).OfType<CheckBox>().FirstOrDefault(k => k.Text == Language.T("updates.check_startup"));
                    now = All(x).OfType<ThemedButton>().FirstOrDefault(k => k.Text == Language.T("updates.check_now"));
                    if (now == null) return;
                    says = All(x).OfType<Label>().First(k => k.Bounds.Top == now.Top + Theme.S(36));
                    Func<string> ask = () => { says.Text = ""; now.PerformClick(); Until(() => now.Enabled && says.Text != Language.T("updates.checking")); return says.Text; };
                    s.Force("GET", RequestPath, 500, "{}", null);
                    afterDown = ask();
                    s.Force("GET", RequestPath, 200, Response("v" + Program.Version, Base + "releases/latest", "ProjectManager.exe"), null);
                    afterEqual = ask();
                    MainWindow.NoticeResponse = y => { asked = true; return DialogResult.Cancel; };
                    s.Force("GET", RequestPath, 200, Response(created, Base + "releases/latest", "ProjectManager.exe"), null);
                    afterNew = ask();
                    Until(() => asked);
                };
                var imp = new MainWindow();
                Render(imp);
                imp.OpenSettings();
                MainWindow.SettingsScreenshot = null;
                v("settings: the entry is there, and it is on", tick != null && tick.Checked);
                v("settings: the manual check is there", now != null);
                v("manual check, GitHub silent: answers with the reason", afterDown != null && afterDown.StartsWith("Can't check for updates:"));
                v("manual check, equal: 'you already have the latest'", afterEqual == Language.T("updates.already_latest_version_project_manager", "v", Program.Version));
                v("manual check, newer: opens the notice", asked);
                v("manual check: answers also on the skipped version", DataStore.Load().SkippedVersion == "1.2" && asked && afterNew != null);
                imp.Close(); window.Close();

                int noticeWait = MainWindow.NoticeWaitMs, appeared = 0;
                MainWindow.NoticeWaitMs = 20;
                MainWindow.NoticeResponse = y => { appeared++; return DialogResult.Cancel; };
                var beforeLanguage = new MainWindow();
                Render(beforeLanguage);
                beforeLanguage.Enabled = false;
                beforeLanguage.WaitNotice(new NewVersion { Version = new Version(1, 2, 0), Label = "1.2", Page = Base + "releases/latest", Note = "x", Fingerprint = H });
                for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(5); }
                v("busy window: the notice waits", appeared == 0);
                beforeLanguage.Enabled = true;
                v("language change: closes the window", beforeLanguage.ChangeLanguage("de") && beforeLanguage.IsDisposed);
                var afterLanguage = new MainWindow();
                Render(afterLanguage);
                v("language change: the notice appears on the new window", Until(() => appeared == 1));
                for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(5); }
                v("language change: only once", appeared == 1);
                afterLanguage.Close();
                MainWindow.NoticeWaitMs = noticeWait; MainWindow.ReopenForLanguage = false;
                Language.Choose("it", DataStore.Folder);

                var withOff = new Archive { UpdateNoticeOff = true };
                v("off: stays after reopening", DataStore.Json().Deserialize<Archive>(DataStore.Json().Serialize(withOff)).UpdateNoticeOff);
            }
        } catch (Exception e) {
            v("exception: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
        } finally {
            UpdateCheck.Api = realApi; UpdateCheck.WaitMs = realWait; UpdateCheck.TimeMs = realTime; UpdateCheck.OpenBrowser = null;
            Theme.Simulate(realScale);
            MainWindow.NoticeResponse = null; MainWindow.SettingsScreenshot = null; MainWindow.NoticeWaitMs = 5000;
        }
        v("the real address is that of the official repo", UpdateCheck.Api == "https://api.github.com" + RequestPath && realWait == 20000);
        v("the scale is back to the real one", Theme.K == realScale && Theme.Normal.Size == 9.5f);
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }
}
