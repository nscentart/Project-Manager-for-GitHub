using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public partial class Wizard {

    public class FakeGateway : IGateway {
        public bool WithBrowser = true, WithGit = true;
        public string FakeLogin = "pippo", LoginGit = "pippo", Error;
        public List<RepoInfo> FakeRepos = new List<RepoInfo>();
        public List<string> Org = new List<string>();
        public bool BrokenOrg;
        public ManualResetEvent Via = new ManualResetEvent(false);
        public Dictionary<string, string> Stored = new Dictionary<string, string>();
        public string Copied, State;
        public const string Token = "gho_fake_for_this_test";
        public bool Browser { get { return WithBrowser; } }
        public bool Git { get { return WithGit; } }
        public DeviceCode Ask() { return new DeviceCode { Code = "dc", ForUser = "ABCD-1234", Page = AccountAccess.DefaultVerificationUrl, Expires = 900, Interval = 5 }; }
        public string Wait(DeviceCode c, Func<bool> cancelled) {
            Via.WaitOne(10000);
            if (Error != null) throw new AccessException(Error);
            return Token;
        }
        public string GitFrom() { return Token + "-git"; }
        public string LoginOf(string token) { return token.EndsWith("-git") ? LoginGit : FakeLogin; }
        public List<RepoInfo> RepoOf(string token, string login) { return FakeRepos.Select(r => new RepoInfo { Name = r.Name, Owner = r.Owner, Organization = r.Organization, Mine = r.Mine, Private = r.Private }).ToList(); }
        public List<string> OrganizationsOf(string token, string login) { if (BrokenOrg) throw new Exception("403"); return Org; }
        public void Store(string login, string token) { Stored[login] = token; }
        public void Copy(string text) { Copied = text; }
        public void Open(string page) { State = page; }
    }

    static bool Until(Func<bool> done) {
        for (int i = 0; i < 400; i++) { Application.DoEvents(); if (done()) return true; Thread.Sleep(25); }
        return false;
    }

    static Wizard Open(Archive a, IGateway p, bool before, Action save) {
        var g = new Wizard(a, p, before, save) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        g.Show();
        return g;
    }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        Silent = true;
        var js = new JavaScriptSerializer();
        string temp = Path.Combine(Path.GetTempPath(), "gpm-test-wizard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            var a = new Archive(); int saved = 0;
            Action save = () => saved++;
            var p = new FakeGateway { FakeRepos = new List<RepoInfo> {
                new RepoInfo { Name = "pippo/single", Owner = "pippo", Mine = true },
                new RepoInfo { Name = "pippo/pair", Owner = "pippo", Mine = true, Private = true },
                new RepoInfo { Name = "company/app", Owner = "company", Organization = true, Mine = false },
                new RepoInfo { Name = "tizio/x", Owner = "tizio" } }, Org = new List<string> { "company", "empty-org" } };

            var g = Open(a, p, true, save);
            v("opens on the first step", g.StartPanel.Visible && g.BBrowser.Visible && g.BGit.Visible);
            v("no notice covers the buttons", !g.Notice.Bounds.IntersectsWith(g.BBrowser.Bounds) && !g.Notice.Bounds.IntersectsWith(g.OpenButton.Bounds));
            g.BBrowser.PerformClick();
            v("the code is shown", Until(() => g.CodePanel.Visible));
            v("no account until you authorize", a.Accounts.Count == 0);
            g.OpenButton.PerformClick();
            v("the button copies the code and opens GitHub", p.Copied == "ABCD-1234" && p.State == AccountAccess.DefaultVerificationUrl);
            p.Via.Set();
            v("after the authorization the repos are chosen", Until(() => g.RepoPanel.Visible));
            v("the account is connected", a.Accounts.Count == 1 && a.Login == "pippo" && a.Accounts[0].Origin == AccountAccess.OriginBrowser && g.Connected == "pippo");
            v("the token goes to Windows", p.Stored.ContainsKey("pippo") && p.Stored["pippo"] == FakeGateway.Token);
            v("the token is not in the archive", !js.Serialize(a).Contains(FakeGateway.Token));
            v("yours are proposed, not those of others", g.RepoListView.Items.Count == 4 && g.RepoListView.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => i.Text).OrderBy(x => x).SequenceEqual(new[] { "pippo/pair", "pippo/single" }));
            v("closing now follows nothing", a.Repo.Count(x => x.Followed) == 0);
            v("on the repos the notice does not cover the note", !g.Notice.Visible);
            v("the organization without repos is flagged", g.lOrg.Text.Contains("empty-org") && !g.lOrg.Text.Contains("company"));
            var org = g.filters.FindIndex(f => f[0] == AccountAccess.OrgFilterPrefix + "company");
            v("the filter has the organization", org > 0);
            g.filterCombo.SelectedIndex = org;
            Application.DoEvents();
            v("the filter shows only the organization", g.RepoListView.Items.Count == 1 && g.RepoListView.Items[0].Text == "company/app");
            g.AllButton.PerformClick();
            v("'All' applies to those shown", g.ticks.Count == 3 && g.ticks.Contains("company/app"));
            g.filterCombo.SelectedIndex = 0;
            Application.DoEvents();
            g.RepoListView.Items.Cast<ListViewItem>().First(i => i.Text == "pippo/pair").Checked = false;
            g.NextButton.PerformClick();
            v("there are two steps: after the repos it is finished, without asking for the folder", Until(() => !g.Visible));
            v("the repos folder stays empty, it is chosen from the Settings", a.ReposFolder == "" && DataStore.ReposFolder == "");
            v("the choices are saved", a.Repo.Where(x => x.Followed).Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "company/app", "pippo/single" }));
            a.ReposFolder = temp; DataStore.ReposFolder = temp;

            var q = new FakeGateway { FakeLogin = "caio", FakeRepos = new List<RepoInfo> { new RepoInfo { Name = "company/app", Owner = "company", Organization = true }, new RepoInfo { Name = "caio/d", Owner = "caio", Mine = true } } };
            q.Via.Set();
            g = Open(a, q, false, save);
            g.BBrowser.PerformClick();
            v("other account: it gets to the repos", Until(() => g.RepoPanel.Visible));
            v("other account: it is active, the first stays", a.Login == "caio" && a.Accounts.Count == 2);
            v("other account: the shared repo stays as it was, only one", a.Repo.Count(x => x.Name == "company/app") == 1 && g.ticks.Contains("company/app") && g.ticks.Contains("caio/d"));
            g.NextButton.PerformClick();
            v("other account: the chosen folder stays, finished", Until(() => !g.Visible) && a.ReposFolder == temp);
            v("other account: the repos of the first are not missing", !a.Repo.Any(x => x.Missing));

            var r = new FakeGateway { LoginGit = "caio" };
            g = Open(a, r, false, save);
            g.BGit.PerformClick();
            v("Git with an account already connected: it says so", Until(() => g.StartPanel.Visible && g.Notice.Text.Contains("already connected")));
            v("Git: stores nothing", r.Stored.Count == 0);
            g.Close();

            var s = new FakeGateway { LoginGit = "sempronio", BrokenOrg = true };
            g = Open(a, s, false, save);
            g.BGit.PerformClick();
            v("Git: new account connected, even if the organizations cannot be read", Until(() => g.RepoPanel.Visible) && a.Accounts.Any(x => x.Login == "sempronio" && x.Origin == AccountAccess.OriginGit));
            v("Git: the key stays with Git", s.Stored.Count == 0);
            v("no repo: it says so", g.accountsLabel.Text.Contains("no repos"));
            g.Close();
            v("closing midway the account stays connected", a.Accounts.Any(x => x.Login == "sempronio"));

            var n = new FakeGateway { Error = "On GitHub you chose not to authorize: no account connected." };
            var b = new Archive();
            g = Open(b, n, true, save);
            g.BBrowser.PerformClick();
            Until(() => g.CodePanel.Visible);
            n.Via.Set();
            v("denied: it goes back and says so", Until(() => g.StartPanel.Visible && g.Notice.Text.Contains("not to authorize")) && b.Accounts.Count == 0 && n.Stored.Count == 0);
            g.Close();
            var m = new FakeGateway();
            g = Open(b, m, true, save);
            g.BBrowser.PerformClick();
            Until(() => g.CodePanel.Visible);
            g.BackButton.PerformClick();
            m.Via.Set();
            for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(25); }
            v("cancelled: the authorization that arrives later does not count", g.StartPanel.Visible && b.Accounts.Count == 0 && m.Stored.Count == 0);
            g.Close();

            g = Open(new Archive(), new FakeGateway { WithBrowser = false }, true, save);
            v("without client id: only Git is offered", !g.BBrowser.Visible && g.BGit.Visible);
            g.Close();
            g = Open(new Archive(), new FakeGateway { WithBrowser = false, WithGit = false }, true, save);
            v("without client id and without Git: no button that does not work", !g.BBrowser.Visible && !g.BGit.Visible);
            g.Close();
        } catch (Exception ex) { v("error: " + ex, false); }
        finally { try { Directory.Delete(temp, true); } catch { } }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }
}
