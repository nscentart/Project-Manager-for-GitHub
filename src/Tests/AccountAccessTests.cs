using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

public static partial class AccountAccess {

    static RepoInfo R(string name, string prop, bool org, bool mine) { return new RepoInfo { Name = name, Owner = prop, Organization = org, Mine = mine }; }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };

        const string OldJson = "{\"Login\":\"pippo\",\"UltimoAggiornamento\":\"01/11/2026 20:00\",\"Modello\":\"m\",\"DominiFidati\":[\"github.com\",\"esempio.it\"],"
            + "\"Repo\":[{\"Nome\":\"pippo/a\",\"Proprietario\":\"pippo\",\"Seguita\":true,\"Mia\":true,\"Dal\":\"2026-11-01T10:00:00Z\"},{\"Nome\":\"pippo/b\",\"Proprietario\":\"pippo\",\"Seguita\":false,\"Mia\":true},"
            + "{\"Nome\":\"company/c\",\"Proprietario\":\"company\",\"Organizzazione\":true,\"Seguita\":true,\"Mia\":true}],"
            + "\"Elementi\":[{\"Chiave\":\"issue:1\",\"Repo\":\"pippo/a\",\"Numero\":1,\"Titolo\":\"t\"}],\"Decisioni\":[{\"Chiave\":\"issue:1\",\"Stato\":\"fatto\"}]}";
        var js = new JavaScriptSerializer();
        var a = Legacy.ArchiveFromJson(OldJson);
        v("archive 0.3: it is recognized", Migrate(a));
        v("archive 0.3: it becomes the active account", a.Accounts.Count == 1 && Active(a) != null && Active(a).Origin == OriginOld);
        v("archive 0.3: followed repos, texts and decisions stay", a.Repo.Count(x => x.Followed) == 2 && a.Repo[0].Since != null && a.Items.Count == 1 && a.Decisions.Count == 1);
        v("archive 0.3: trusted sites stay", a.TrustedDomains.Contains("esempio.it"));
        v("archive 0.3: all repos are visible", VisibleRepos(a).Count == 3);
        v("archive 0.3: the second time nothing changes", !Migrate(a) && a.Accounts.Count == 1);
        var reloaded = js.Deserialize<Archive>(js.Serialize(a));
        v("saved and read back it stays the same", !Migrate(reloaded) && reloaded.Accounts.Count == 1 && VisibleRepos(reloaded).Count == 3);
        var newValue = new Archive();
        v("new archive: no account", !Migrate(newValue) && newValue.Accounts.Count == 0 && Active(newValue) == null);

        Rename(a, "pippo", "pippo2");
        v("rename: the account and the repos follow the right name", a.Login == "pippo2" && Active(a) != null && Sees(a.Repo[0], "pippo2") && !Sees(a.Repo[0], "pippo"));
        Rename(a, "pippo2", "pippo");

        Add(a, "caio", OriginBrowser);
        var missingRepos = Join(a, "caio", new List<RepoInfo> { R("company/c", "company", true, false), R("caio/d", "caio", false, true) }, x => false);
        v("second account: does not make the repos of the first go missing", missingRepos == 0 && a.Repo.Count(x => x.Missing) == 0);
        v("second account: it is the active one", a.Login == "caio" && a.Accounts.Count == 2);
        v("second account: sees only its own", VisibleRepos(a).Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "caio/d", "company/c" }));
        v("shared repo: one is left, followed as before", a.Repo.Count(x => x.Name == "company/c") == 1 && a.Repo.First(x => x.Name == "company/c").Followed);
        a.Login = "pippo";
        v("account switch: its own are visible again", VisibleRepos(a).Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "company/c", "pippo/a", "pippo/b" }));

        var f = Filters(a);
        v("filter: all, personal and the organization", f.Select(x => x[0]).SequenceEqual(new[] { FilterAll, FilterPersonal, OrgFilterPrefix + "company" }));
        a.Filter = FilterPersonal;
        v("personal filter", VisibleRepos(a).Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "pippo/a", "pippo/b" }));
        a.Filter = OrgFilterPrefix + "COMPANY";
        v("organization filter, ignoring case", Pass(a.Repo.First(x => x.Name == "company/c"), "pippo", a.Filter) && !Pass(a.Repo[0], "pippo", a.Filter));
        a.Filter = OrgFilterPrefix + "no-longer-exists";
        v("filter on an organization that was left: back to all", VisibleRepos(a).Count == 3);
        a.Filter = null;
        a.Repo.Add(new RepoInfo { Name = "tizio/x", Owner = "tizio", Accounts = new List<string> { "pippo" } });
        v("filter: repos of other people appear", Filters(a).Any(x => x[0] == FilterOthers) && Pass(a.Repo.Last(), "pippo", FilterOthers) && !Pass(a.Repo.Last(), "pippo", FilterPersonal));
        a.Repo.RemoveAll(x => x.Name == "tizio/x");

        missingRepos = Join(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true), R("pippo/b", "pippo", false, true) }, x => false);
        v("missing: the other account still sees it, so it is not missing", missingRepos == 0 && !a.Repo.First(x => x.Name == "company/c").Missing && !Sees(a.Repo.First(x => x.Name == "company/c"), "pippo"));
        missingRepos = Join(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true) }, x => false);
        v("missing: nobody sees it anymore", missingRepos == 1 && a.Repo.First(x => x.Name == "pippo/b").Missing && Sees(a.Repo.First(x => x.Name == "pippo/b"), "pippo"));
        Join(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true), R("pippo/b", "pippo", false, true) }, x => false);
        v("missing: if it comes back, it comes back", !a.Repo.First(x => x.Name == "pippo/b").Missing);
        Join(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true), R("pippo/b", "pippo", false, true), R("pippo/n1", "pippo", false, true), R("other/n2", "other", false, false) }, x => x.Mine);
        v("new repo: followed as the caller says", a.Repo.First(x => x.Name == "pippo/n1").Followed && !a.Repo.First(x => x.Name == "other/n2").Followed);

        int texts = a.Items.Count;
        Disconnect(a, "pippo");
        v("disconnect: switches to the other account", a.Login == "caio" && a.Accounts.Count == 1);
        v("disconnect: the repos stay hidden, the texts stay", a.Repo.Any(x => x.Name == "pippo/a") && !Sees(a.Repo.First(x => x.Name == "pippo/a"), "caio") && a.Items.Count == texts);
        Add(a, "pippo", OriginBrowser);
        Join(a, "pippo", new List<RepoInfo> { R("pippo/a", "pippo", false, true) }, x => false);
        v("reconnect: the repo is followed again, where it was", a.Repo.First(x => x.Name == "pippo/a").Followed && Sees(a.Repo.First(x => x.Name == "pippo/a"), "pippo"));
        Disconnect(a, "pippo"); Disconnect(a, "caio");
        v("disconnect the last one: no active account", a.Login == null && Active(a) == null);

        var calls = new List<string>();
        Func<string[], Func<string, string, string>> network = responses => {
            int i = 0;
            return (url, body) => {
                calls.Add(url + " " + body);
                string r = responses[Math.Min(i++, responses.Length - 1)];
                if (r == "DOWN") throw new Exception("network");
                return r;
            };
        };
        const string DeviceCodeJson = "{\"device_code\":\"dc1\",\"user_code\":\"ABCD-1234\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":900,\"interval\":5}";
        const string Fake = "gho_fake_for_this_test";
        var c = Ask("id1", network(new[] { DeviceCodeJson }));
        v("code: it arrives", c.ForUser == "ABCD-1234" && c.Code == "dc1" && c.Interval == 5 && c.Expires == 900);
        v("code: requested with the right permissions", calls[0].StartsWith("https://github.com/login/device/code client_id=id1&scope=repo%20read%3Aorg"));
        v("code: a page outside github.com does not open", Ask("id1", network(new[] { "{\"device_code\":\"d\",\"user_code\":\"U\",\"verification_uri\":\"https://evil.example.com/\"}" })).Page == DefaultVerificationUrl);
        v("code: fake github.com in the name does not pass", Ask("id1", network(new[] { "{\"device_code\":\"d\",\"user_code\":\"U\",\"verification_uri\":\"https://github.com.evil.example/\"}" })).Page == DefaultVerificationUrl);
        Func<Action, string> error = act => { try { act(); return null; } catch (AccessException ex) { return ex.Message; } };
        v("code: program not enabled", error(() => Ask("id1", network(new[] { "{\"error\":\"device_flow_disabled\"}" }))) != null);
        v("code: response that is not JSON", error(() => Ask("id1", network(new[] { "<html>" }))) != null);

        var slept = new List<int>();
        Action<int> sleeps = slept.Add;
        Func<bool> never = () => false;
        calls.Clear();
        string tok = Wait(c, "id1", network(new[] { "{\"error\":\"authorization_pending\"}", "{\"error\":\"slow_down\",\"interval\":10}", "{\"access_token\":\"" + Fake + "\",\"token_type\":\"bearer\"}" }), sleeps, never);
        v("wait: after the wait the access arrives", tok == Fake);
        v("wait: slows down when GitHub asks", slept.SequenceEqual(new[] { 5, 5, 10 }));
        v("wait: the right type is requested", calls[0].Contains("device_code=dc1") && calls[0].Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code"));
        slept.Clear();
        tok = Wait(c, "id1", network(new[] { "{\"error\":\"slow_down\"}", "{\"access_token\":\"" + Fake + "\"}" }), sleeps, never);
        v("wait: slows down by 5 if it does not say how much", tok == Fake && slept.SequenceEqual(new[] { 5, 10 }));
        var msg = error(() => Wait(c, "id1", network(new[] { "{\"error\":\"access_denied\",\"error_description\":\"outside text\"}" }), sleeps, never));
        v("wait: if you say no on GitHub it says so, in our own words", msg != null && msg.Contains("not to authorize") && !msg.Contains("outside text"));
        v("wait: code expired on GitHub", error(() => Wait(c, "id1", network(new[] { "{\"error\":\"expired_token\"}" }), sleeps, never)).Contains("expired"));
        v("wait: code expires by itself, it does not run forever", error(() => Wait(new DeviceCode { Code = "d", Expires = 20, Interval = 5 }, "id1", network(new[] { "{\"error\":\"authorization_pending\"}" }), sleeps, never)).Contains("expired"));
        v("wait: cancelled", Wait(c, "id1", network(new[] { "{\"error\":\"authorization_pending\"}" }), sleeps, () => true) == null);
        v("wait: the network drops for a moment and recovers", Wait(c, "id1", network(new[] { "DOWN", "DOWN", "{\"access_token\":\"" + Fake + "\"}" }), sleeps, never) == Fake);
        msg = error(() => Wait(c, "id1", network(new[] { "DOWN" }), sleeps, never));
        v("wait: without network it stops and says so", msg != null && msg.Contains("connection"));
        msg = error(() => Wait(c, "id1", network(new[] { "{\"error\":\"something\",\"access_tokn\":\"" + Fake + "\"}" }), sleeps, never));
        v("wait: no message contains the text of the response", msg != null && !msg.Contains(Fake));

        var fake = new TcpListener(IPAddress.Loopback, 0);
        fake.Start();
        int gateway = ((IPEndPoint)fake.LocalEndpoint).Port;
        string received = "";
        var server = new Thread(() => {
            using (var s = fake.AcceptTcpClient()) {
                var st = s.GetStream(); st.ReadTimeout = 5000;
                var buf = new byte[4096]; int n; var sbr = new StringBuilder();
                while (!sbr.ToString().Contains("\r\n\r\n") || !sbr.ToString().EndsWith("scope=x")) {
                    n = st.Read(buf, 0, buf.Length); if (n <= 0) break;
                    sbr.Append(Encoding.ASCII.GetString(buf, 0, n));
                }
                received = sbr.ToString();
                var r = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 8\r\nConnection: close\r\n\r\n{\"a\":22}");
                st.Write(r, 0, r.Length);
            }
        }) { IsBackground = true };
        server.Start();
        string onThread = null;
        try { onThread = PostA("http://127.0.0.1:" + gateway + "/login", "client_id=i&scope=x"); } catch (Exception ex) { sb.AppendLine("   " + ex.Message); }
        server.Join(5000); fake.Stop();
        v("on threads: the response arrives", onThread == "{\"a\":22}");
        v("on threads: POST with JSON and form", received.StartsWith("POST /login HTTP/1.1") && received.Contains("Accept: application/json") && received.Contains("Content-Type: application/x-www-form-urlencoded") && received.EndsWith("client_id=i&scope=x"));
        v("address outside the GitHub login: refused", error2(() => Post("https://evil.example.com/", "x")));

        string pref = "ProjectManager-test-" + Guid.NewGuid().ToString("N") + "/";
        try {
            CredentialStore.Write(pref + "single", "single", Fake);
            CredentialStore.Write(pref + "double", "double", Fake + "2");
            v("credential store: it reads back the same", CredentialStore.Read(pref + "single") == Fake && CredentialStore.Read(pref + "double") == Fake + "2");
            v("credential store: lists only its own", CredentialStore.ListNames(pref).OrderBy(x => x).SequenceEqual(new[] { pref + "double", pref + "single" }));
            CredentialStore.Write(pref + "single", "single", "replaced");
            v("credential store: it gets replaced", CredentialStore.Read(pref + "single") == "replaced");
            CredentialStore.Delete(pref + "single");
            v("credential store: once deleted it is gone", CredentialStore.Read(pref + "single") == null);
            CredentialStore.RemoveAll(pref);
            v("credential store: remove everything", CredentialStore.ListNames(pref).Count == 0 && CredentialStore.Read(pref + "double") == null);
            v("credential store: what is not there is null", CredentialStore.Read(pref + "never") == null);
        } catch (Exception ex) { v("credential store: " + ex.Message, false); }
        finally { try { CredentialStore.RemoveAll(pref); } catch { } }
        v("credential store: the account name is not case sensitive", CredentialStore.NameOf("Pippo") == CredentialStore.NameOf("pippo") && CredentialStore.NameOf("pippo").StartsWith(CredentialStore.Prefix));

        v("new trusted sites: only GitHub", DataStore.DefaultTrustedDomains.OrderBy(x => x).SequenceEqual(new[] { "github.com", "githubusercontent.com" }));
        v("site outside the list: quarantine", Screener.Examine("t", "see https://esempio.it/x", DataStore.DefaultTrustedDomains).Count > 0);
        v("site chosen by the user: passes", Screener.Examine("t", "see https://esempio.it/x", new[] { "github.com", "esempio.it" }).Count == 0);
        string folderBefore = DataStore.ReposFolder;
        DataStore.ReposFolder = "";
        v("no repo folder: handovers are not counted", DataStore.PendingHandovers("pippo/a") == -1);
        DataStore.ReposFolder = folderBefore;
        string g = FindGit();
        v("git: if present, it is in PATH and not next to the program", g == null || !string.Equals(Path.GetDirectoryName(g), Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), StringComparison.OrdinalIgnoreCase));

        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }

    static bool error2(Action act) { try { act(); return false; } catch (Exception) { return true; } }
}
