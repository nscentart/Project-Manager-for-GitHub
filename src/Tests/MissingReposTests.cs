using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class MissingReposTests {
    static void Render(Form f) {
        f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000);
        f.Show();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(10); }
    }

    static void Draw(MainWindow f) {
        var p = f.Overview;
        using (var b = new Bitmap(Math.Max(1, p.Width), Math.Max(1, p.Height))) p.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
        Application.DoEvents();
    }

    static void HandleClick(MainWindow f, OverviewPage.HitTarget t) {
        Draw(f);
        var z = f.Overview.ZoneOf(t);
        f.Overview.HandleClick(new Point(z.X + z.Width / 2, z.Y + z.Height / 2));
        Application.DoEvents();
    }

    static List<RepoRow> Lines(MainWindow f) { return f.Column.Items.Cast<RepoRow>().ToList(); }
    static int Group(MainWindow f) { return Lines(f).FindIndex(r => r.Group); }

    static Archive Example() {
        Func<string, bool, RepoInfo> repo = (n, via) => new RepoInfo { Name = n, Owner = n.Split('/')[0], Mine = true, Missing = via, Accounts = new List<string> { "mario" } };
        return new Archive {
            Login = "mario", TrustedDomains = DataStore.DefaultTrustedDomains.ToList(), ReposFolder = "",
            Accounts = new List<GitHubAccount> { new GitHubAccount { Login = "mario", Origin = AccountAccess.OriginBrowser } },
            Repo = new List<RepoInfo> { repo("mario/live", false), repo("mario/other", false), repo("mario/via1", true), repo("mario/via2", true), repo("mario/via3", true) },
            Items = new List<WorkItem> {
                new WorkItem { Key = "issue:1", Repo = "mario/via1", Number = 1, Type = "issue", Title = "a", Author = "x", Text = "a", Url = "https://github.com/mario/via1/issues/1", Created = "2026-10-01T00:00:00Z", Updated = "2026-10-01T00:00:00Z", State = "open" },
                new WorkItem { Key = "comment:2", Repo = "mario/via1", Number = 1, Type = "comment", Title = "a", Author = "x", Text = "b", Url = "https://github.com/mario/via1/issues/1", Created = "2026-10-01T00:00:00Z", Updated = "2026-10-01T00:00:00Z", State = "open" },
                new WorkItem { Key = "issue:3", Repo = "mario/live", Number = 3, Type = "issue", Title = "c", Author = "x", Text = "c", Url = "https://github.com/mario/viva/issues/3", Created = "2026-10-01T00:00:00Z", Updated = "2026-10-01T00:00:00Z", State = "open" } },
            Decisions = new List<Decision> { new Decision { Key = "issue:1", Fingerprint = "x", State = "ignored" }, new Decision { Key = "issue:3", Fingerprint = "y", State = "ignored" } } };
    }

    static HistoryArchive ExampleHistory() {
        var s = new HistoryArchive();
        s.Repo["mario/via1"] = new List<HistoryDay> { new HistoryDay { Day = "2026-10-01", Views = 1 }, new HistoryDay { Day = "2026-10-02", Views = 2 } };
        s.Repo["mario/live"] = new List<HistoryDay> { new HistoryDay { Day = "2026-10-01", Views = 1 } };
        return s;
    }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL a test data folder is needed\n"); return 1; }
        Language.Use("en");
        Dialog.Silent = true;
        MainWindow.ScreenshotOnly = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try {
            var a = Example(); var s = ExampleHistory();
            var count = AccountAccess.CountRemoval(a, s, new[] { "mario/via1", "MARIO/VIA1", "mario/via2" });
            v("count: once per repo", count.Repo.Count == 2);
            v("count: texts, decisions and days", count.Texts == 2 && count.Decisions == 1 && count.HistoryDays == 2);
            v("count: changes nothing", a.Repo.Count == 5 && a.Items.Count == 3 && a.Decisions.Count == 2 && s.Repo.Count == 2);
            var liveRemoval = AccountAccess.CountRemoval(a, s, new[] { "mario/live" });
            v("count: a live one cannot be removed", liveRemoval.Repo.Count == 0 && liveRemoval.Texts == 0);
            var done = AccountAccess.RemoveMissing(a, s, new[] { "mario/via1", "mario/live", "mario/nonexistent" });
            v("remove: only the missing one", done.Repo.SequenceEqual(new[] { "mario/via1" }) && a.Repo.Any(r => r.Name == "mario/live"));
            v("remove: the row goes away", a.Repo.Count == 4 && !a.Repo.Any(r => r.Name == "mario/via1"));
            v("remove: its texts go away, the others stay", a.Items.Count == 1 && a.Items[0].Repo == "mario/live");
            v("remove: its decisions go away, the others stay", a.Decisions.Count == 1 && a.Decisions[0].Key == "issue:3");
            v("remove: its history goes away, the others stay", !s.Repo.ContainsKey("mario/via1") && s.Repo.ContainsKey("mario/live"));

            Func<string, RepoInfo> GitHubFrom = n => new RepoInfo { Name = n, Owner = n.Split('/')[0], Mine = true };
            var newOnes = new List<RepoInfo> { GitHubFrom("mario/live"), GitHubFrom("mario/other"), GitHubFrom("mario/via1"), GitHubFrom("mario/via2") };
            int missingCount = AccountAccess.Join(a, "mario", newOnes, n => n.Mine);
            var reappears = a.Repo.First(r => r.Name == "mario/via1");
            v("return: a removed one comes back by itself, as new", !reappears.Missing && reappears.Since == null && reappears.Stats == null && reappears.Followed);
            v("return: one only marked is no longer missing", !a.Repo.First(r => r.Name == "mario/via2").Missing);
            v("return: one that is still missing stays missing", a.Repo.First(r => r.Name == "mario/via3").Missing && missingCount == 1);

            a = Example();
            DataStore.Save(a); History.Save(ExampleHistory());
            var f = new MainWindow();
            Render(f);
            var states = Lines(f);
            v("the group is closed at the start", !f.MissingForTest);
            int g = Group(f);
            v("on the left: All, the live ones, then the group", states.Count == 4 && g == 3);
            v("the group says how many there are", states[g].Name == "Gone (3)" && states[g].R == null);
            Draw(f);
            v("table: when closed only the live ones show", f.Overview.NamesInTable().Count == 2 && f.Overview.NamesInTable().All(n => n == "mario/live" || n == "mario/other"));
            v("table: the group row is there", !f.Overview.ZoneOf(OverviewPage.HitTarget.Group).IsEmpty);
            v("table: 'Remove all' is there even when closed", !f.Overview.ZoneOf(OverviewPage.HitTarget.RemoveAll).IsEmpty);

            f.Column.SelectedIndex = 0;
            f.Column.ClickIndex(g);
            Application.DoEvents();
            v("click on the left: it opens", f.MissingForTest && Lines(f).Count == 7);
            v("click on the left: the selection does not move", f.Column.SelectedIndex == 0 && f.ChoiceForTest == null);
            v("click on the left: the row says open", Lines(f)[g].Open);
            f.Column.ClickIndex(g);
            Application.DoEvents();
            v("click on the left: it closes again", !f.MissingForTest && Lines(f).Count == 4);

            HandleClick(f, OverviewPage.HitTarget.Group);
            v("click in the table: it also opens on the left", f.MissingForTest && Lines(f).Count == 7);
            Draw(f);
            v("table: when open all of them are there", f.Overview.NamesInTable().Count == 5);
            HandleClick(f, OverviewPage.HitTarget.Group);
            v("click in the table: it closes again", !f.MissingForTest);

            f.Column.SelectedIndex = 2;
            Application.DoEvents();
            f.Column.SelectedIndex = g;
            Application.DoEvents();
            v("down arrow on the group: enters the first missing one", f.MissingForTest && f.ChoiceForTest != null && f.ChoiceForTest.Missing);
            f.Column.SelectedIndex = g;
            Application.DoEvents();
            v("up arrow on the group: goes back to the last live one", f.ChoiceForTest != null && !f.ChoiceForTest.Missing);
            f.Column.SelectedIndex = 0;
            Application.DoEvents();

            f.RepoScreenshot("mario/via1");
            Application.DoEvents();
            v("the missing one chosen: the group is open", f.MissingForTest && f.ChoiceForTest.Name == "mario/via1");
            Draw(f);
            v("page of the missing one: the button is there", !f.Overview.ZoneOf(OverviewPage.HitTarget.RemoveThis).IsEmpty);
            f.Column.ClickIndex(Group(f));
            Application.DoEvents();
            v("closing with a missing one chosen: back to All", !f.MissingForTest && f.ChoiceForTest == null);
            f.RepoScreenshot("mario/via1");
            Application.DoEvents();

            RemoveDialog view = null; string story = null; bool cancelBefore = false, noSend = false;
            MainWindow.RemoveResponse = x => { view = (RemoveDialog)x; story = view.Story; cancelBefore = view.CancelButton == view.CancelChoiceButton; noSend = view.AcceptButton == null; return DialogResult.Cancel; };
            HandleClick(f, OverviewPage.HitTarget.RemoveThis);
            v("confirmation: it opens", view != null);
            v("confirmation: nothing changes on GitHub", story != null && story.Contains("Nothing changes on GitHub"));
            v("confirmation: how many texts and how much history", story != null && story.Contains("2 downloaded texts") && story.Contains("1 decision made") && story.Contains("2 days of history"));
            v("confirmation: if it comes back, it comes back by itself", story != null && story.Contains("returns to the list by itself"));
            v("confirmation: Cancel by default, Enter does not remove", cancelBefore && noSend);
            v("cancel: removes nothing", DataStore.Load().Repo.Count == 5 && DataStore.Load().Items.Count == 3);
            v("cancel: the page stays", f.ChoiceForTest != null && f.ChoiceForTest.Name == "mario/via1");

            int busyOpen = 0;
            MainWindow.RemoveResponse = x => { busyOpen++; return DialogResult.OK; };
            f.BusyForTest(true);
            HandleClick(f, OverviewPage.HitTarget.RemoveThis);
            v("while updating: refused", busyOpen == 0 && f.State.Text == Language.T("common.wait_current_work_finish"));
            v("while updating: removes nothing", DataStore.Load().Repo.Count == 5);
            f.BusyForTest(false);

            MainWindow.RemoveResponse = x => DialogResult.OK;
            HandleClick(f, OverviewPage.HitTarget.RemoveThis);
            var after = DataStore.Load();
            v("confirmation: the repo goes away and it is saved", after.Repo.Count == 4 && !after.Repo.Any(r => r.Name == "mario/via1"));
            v("confirmation: the texts go away and it is saved", after.Items.Count == 1 && after.Decisions.Count == 1);
            v("confirmation: the history goes away and it is saved", History.DaysHowMany(History.Load(), "mario/via1") == 0 && History.DaysHowMany(History.Load(), "mario/live") == 1);
            v("confirmation: the choice does not stay on a removed repo", f.ChoiceForTest == null);
            v("confirmation: the message", f.State.Text == "1 repo removed from the list.");

            f.MissingScreenshot(false);
            int howMany = 0;
            MainWindow.RemoveResponse = x => { howMany = ((RemoveDialog)x).RemoveButton.Text == "Remove all" ? 1 : 0; return DialogResult.OK; };
            HandleClick(f, OverviewPage.HitTarget.RemoveAll);
            after = DataStore.Load();
            v("remove all: exactly the live ones remain", after.Repo.Count == 2 && after.Repo.All(r => !r.Missing));
            v("remove all: the button is named like that", howMany == 1);
            v("remove all: the group disappears", Group(f) < 0 && Lines(f).Count == 3 && !f.MissingForTest);
            Draw(f);
            v("remove all: the group does not stay in the table", f.Overview.ZoneOf(OverviewPage.HitTarget.Group).IsEmpty && f.Overview.ZoneOf(OverviewPage.HitTarget.RemoveAll).IsEmpty);
            f.Close();

            a = Example();
            DataStore.Save(a); History.Save(ExampleHistory());
            f = new MainWindow();
            Render(f);
            f.MissingScreenshot(true);
            int asked = 0;
            var answer = DialogResult.Cancel;
            MainWindow.RemoveResponse = x => { asked++; return answer; };
            Func<string, int> rowOf = n => Lines(f).FindIndex(r => r.R != null && r.R.Name == n);
            var rowMenu = f.RowMenu(rowOf("mario/via2"));
            v("right-click on a missing repo: a menu with one entry", rowMenu != null && rowMenu.Items.Count == 1 && rowMenu.Items[0].Text == Language.T("missing.remove_menu") && rowMenu.Items[0].Text.EndsWith("..."));
            rowMenu.Items[0].PerformClick();
            v("menu on a missing repo: it asks, and with Cancel nothing is removed", asked == 1 && DataStore.Load().Repo.Count == 5);
            answer = DialogResult.OK;
            rowMenu.Items[0].PerformClick();
            after = DataStore.Load();
            v("menu on a missing repo: with yes only that one goes away", asked == 2 && after.Repo.Count == 4 && !after.Repo.Any(r => r.Name == "mario/via2") && after.Repo.Any(r => r.Name == "mario/via1") && after.Repo.Any(r => r.Name == "mario/via3"));
            v("menu on a live repo and on the All row: none", f.RowMenu(rowOf("mario/live")) == null && f.RowMenu(0) == null && f.RowMenu(-1) == null);

            answer = DialogResult.Cancel;
            f.RepoScreenshot("mario/live");
            Application.DoEvents();
            int beforeDelete = asked;
            f.Column.PressDelete();
            v("Delete key on a live repo: nothing happens, no question", asked == beforeDelete && DataStore.Load().Repo.Count == 4);
            f.RepoScreenshot("mario/via1");
            Application.DoEvents();
            f.Column.PressDelete();
            v("Delete key on a missing repo: it asks, and with Cancel nothing is removed", asked == beforeDelete + 1 && DataStore.Load().Repo.Count == 4);
            answer = DialogResult.OK;
            f.Column.PressDelete();
            after = DataStore.Load();
            v("Delete key on a missing repo: with yes only that one goes away", asked == beforeDelete + 2 && after.Repo.Count == 3 && !after.Repo.Any(r => r.Name == "mario/via1") && after.Repo.Any(r => r.Name == "mario/via3") && after.Repo.Any(r => r.Name == "mario/live"));

            f.RepoScreenshot("mario/via3");
            Application.DoEvents();
            f.TabScreenshot(1);
            Application.DoEvents();
            var manage = f.ManagementForTest;
            var removeButton = manage.ButtonOf(Language.T("missing.remove_list"));
            v("management of a missing repo: the removal button is enabled with a sentence", removeButton.Enabled && manage.TextOf(removeButton).Length > 10);
            v("management of a missing repo: the other actions stay disabled", !manage.ButtonOf(Language.T("common.rename")).Enabled && !manage.ButtonOf(Language.T("management.delete_repo")).Enabled && !manage.ButtonOf(Language.T("management.transfer")).Enabled);
            answer = DialogResult.Cancel;
            int beforeButton = asked;
            removeButton.PerformClick();
            v("management of a missing repo: it asks, and with Cancel nothing is removed", asked == beforeButton + 1 && DataStore.Load().Repo.Any(r => r.Name == "mario/via3"));
            answer = DialogResult.OK;
            removeButton.PerformClick();
            after = DataStore.Load();
            v("management of a missing repo: with yes it goes away", asked == beforeButton + 2 && !after.Repo.Any(r => r.Name == "mario/via3") && after.Repo.Count == 2);
            f.Close();

            a = Example();
            DataStore.Save(a); History.Save(ExampleHistory());
            f = new MainWindow();
            Render(f);
            answer = DialogResult.Cancel; asked = 0;
            MainWindow.RemoveResponse = x => { asked++; return answer; };
            var groupMenu = f.RowMenu(Group(f));
            v("right-click on the group: a menu with one entry", groupMenu != null && groupMenu.Items.Count == 1 && groupMenu.Items[0].Text == Language.T("missing.remove_all_menu") && groupMenu.Items[0].Text.EndsWith("..."));
            groupMenu.Items[0].PerformClick();
            v("menu on the group: it asks, and with Cancel nothing is removed", asked == 1 && DataStore.Load().Repo.Count == 5);
            answer = DialogResult.OK;
            groupMenu.Items[0].PerformClick();
            after = DataStore.Load();
            v("menu on the group: with yes only the missing ones go away", asked == 2 && after.Repo.Count == 2 && after.Repo.All(r => !r.Missing) && after.Repo.Any(r => r.Name == "mario/live") && after.Repo.Any(r => r.Name == "mario/other"));
            f.Close();
            MainWindow.RemoveResponse = null;

            a = Example();
            a.Accounts.Add(new GitHubAccount { Login = "other", Origin = AccountAccess.OriginBrowser });
            a.Repo.First(r => r.Name == "mario/via3").Accounts = new List<string> { "other" };
            DataStore.Save(a);
            f = new MainWindow();
            Render(f);
            MainWindow.RemoveResponse = x => DialogResult.OK;
            HandleClick(f, OverviewPage.HitTarget.RemoveAll);
            after = DataStore.Load();
            v("remove all: a missing one of another account is not touched", after.Repo.Any(r => r.Name == "mario/via3") && !after.Repo.Any(r => r.Name == "mario/via1"));
            f.Close();
            MainWindow.RemoveResponse = null;

            a = Example();
            for (int i = 0; i < 80; i++) a.Repo.Add(new RepoInfo { Name = "mario/many" + i.ToString("00"), Owner = "mario", Mine = true, Accounts = new List<string> { "mario" } });
            DataStore.Save(a);
            f = new MainWindow();
            Render(f);
            f.Column.TopIndex = Group(f);
            Application.DoEvents();
            int top = f.Column.TopIndex;
            v("many repos: the column can scroll", top > 10);
            f.Column.ClickIndex(Group(f));
            Application.DoEvents();
            v("many repos: opening the group the column does not go back to the top", f.MissingForTest && f.Column.TopIndex == top);
            f.Column.ClickIndex(Group(f));
            Application.DoEvents();
            v("many repos: closing the group neither", !f.MissingForTest && f.Column.TopIndex == top);
            f.Close();

            using (var srv = new FakeServer()) {
                const string FakeKey = "fake-key-for-missing";
                srv.Who[FakeKey] = "mario"; srv.Permissions[FakeKey] = "repo, read:org";
                GitHub.Use(FakeKey, "mario");
                GitHub.OnTestServer(srv.Gateway);
                Func<string, FakeServer.FakeRepo> create = n => { var x = srv.New(n, "User", "admin", false); srv.Put(x, "README.md", Encoding.UTF8.GetBytes("# x")); return x; };
                var rReal = create("mario/real");
                rReal.Stars = 4;
                rReal.Traffic["/traffic/views"] = FakeServer.D("count", 10, "uniques", 4, "views", new object[] { FakeServer.D("timestamp", "2026-10-20T00:00:00Z", "count", 10, "uniques", 4) });
                rReal.Traffic["/traffic/clones"] = FakeServer.D("count", 1, "uniques", 1, "clones", new object[] { FakeServer.D("timestamp", "2026-10-20T00:00:00Z", "count", 1, "uniques", 1) });
                rReal.Traffic["/traffic/popular/referrers"] = new object[0];
                rReal.Traffic["/traffic/popular/paths"] = new object[0];
                create("mario/mute");
                create("mario/broken");
                create("mario/returns");
                srv.ForceTimes("GET", "/repos/mario/broken/releases", 500, "{}", null, 999);
                Func<string[], string> joinedList = names => "[" + string.Join(",", names.Select(x => "{\"full_name\":\"" + x + "\",\"description\":\"\",\"default_branch\":\"main\",\"private\":false,\"owner\":{\"login\":\"mario\",\"type\":\"User\"},\"permissions\":{\"admin\":true}}")) + "]";
                DataStore.Save(new Archive { Login = "mario", TrustedDomains = DataStore.DefaultTrustedDomains.ToList(), ReposFolder = "", Accounts = new List<GitHubAccount> { new GitHubAccount { Login = "mario", Origin = AccountAccess.OriginBrowser } },
                    Repo = new List<RepoInfo> { new RepoInfo { Name = "mario/returns", Owner = "mario", Mine = true, Missing = true, Accounts = new List<string> { "mario" } } } });
                foreach (var x in Directory.GetFiles(DataStore.Folder, "history.json*")) File.Delete(x);
                f = new MainWindow();
                Render(f);
                Action<string[]> update = names => {
                    srv.Force("GET", "/user/repos", 200, joinedList(names), null);
                    f.UpdateForTest();
                    for (int i = 0; i < 1500 && f.BusyNow; i++) { Application.DoEvents(); Thread.Sleep(10); }
                    Application.DoEvents();
                };
                update(new[] { "mario/real", "mario/mute", "mario/broken", "mario/returns" });
                v("Update: finishes", !f.BusyNow);
                after = DataStore.Load();
                Func<string, RepoInfo> of = n => after.Repo.FirstOrDefault(r => r.Name == n);
                v("Update: a missing repo that comes back returns by itself", !of("mario/returns").Missing);
                v("Update: the stats stay in the archive", of("mario/real").Stats.Views == 10 && of("mario/real").Stats.Stars == 4);
                var onDisk = History.Copy(History.Load(), "mario/real");
                v("Update: the history is written on the disk", onDisk.Any(x => x.Day == "2026-10-20" && x.Views == 10) && onDisk.Any(x => x.Day == DateTime.UtcNow.ToString("yyyy-MM-dd") && x.Downloads == 0) && !File.Exists(Path.Combine(DataStore.Folder, "history.json.new")));
                v("Update: 403 on traffic: not available, the rest is there", of("mario/mute").Stats.ViewsError == Language.T("github.github_only_shows_numbers_people") && !of("mario/mute").Stats.ViewsRead && of("mario/mute").Stats.RepoRead);
                v("Update: 500 on releases: the rest is there", of("mario/broken").Stats.DownloadsError != null && of("mario/broken").Stats.RepoRead && OverviewPage.AllRead(of("mario/broken").Overview));
                v("Update: the stats that are missing do not make the read fail", f.State.Text.StartsWith("Updated") && !f.State.Text.Contains("failed") && !f.State.Text.Contains("partly read"));
                Draw(f);
                v("table: after Update all of them are there", f.Overview.NamesInTable().Count == 4);

                update(new[] { "mario/real", "mario/mute", "mario/broken" });
                after = DataStore.Load();
                v("Update: one that is no longer there goes in the group", of("mario/returns").Missing && Group(f) >= 0 && Lines(f)[Group(f)].Name == "Gone (1)");
                v("Update: the stats of the missing one are no longer fetched", srv.Of("GET", "/repos/mario/returns/traffic/views").Count == 1);
                MainWindow.RemoveResponse = x => DialogResult.OK;
                HandleClick(f, OverviewPage.HitTarget.RemoveAll);
                MainWindow.RemoveResponse = null;
                after = DataStore.Load();
                v("removed from the group", of("mario/returns") == null && Group(f) < 0);

                update(new[] { "mario/real", "mario/mute", "mario/broken", "mario/returns" });
                after = DataStore.Load();
                v("Update: a removed repo that returns on GitHub comes back by itself", of("mario/returns") != null && !of("mario/returns").Missing && of("mario/returns").Stats != null);
                v("Update: it also comes back on the left", Lines(f).Any(r => r.R != null && r.R.Name == "mario/returns") && Group(f) < 0);

                var manyRepos = new List<string> { "mario/real", "mario/mute", "mario/broken", "mario/returns" };
                for (int i = 0; i < 40; i++) { create("mario/lots" + i.ToString("00")); manyRepos.Add("mario/lots" + i.ToString("00")); }
                update(manyRepos.ToArray());
                f.Column.TopIndex = f.Column.Items.Count - 1;
                Application.DoEvents();
                int down = f.Column.TopIndex;
                update(manyRepos.ToArray());
                v("many repos: after Update the column stays where you left it", down > 10 && f.Column.TopIndex == down);

                srv.Force("GET", "/user/repos", 401, "{\"message\":\"Bad credentials\"}", null);
                f.UpdateForTest();
                for (int i = 0; i < 1500 && f.BusyNow; i++) { Application.DoEvents(); Thread.Sleep(10); }
                Application.DoEvents();
                after = DataStore.Load();
                v("Update failed: nothing disappears", after.Repo.Count == 44 && after.Repo.All(r => !r.Missing));
                f.Close();
                GitHub.OnRealGitHub();
                GitHub.Exit();
            }

            var bigRemoval = new AccountAccess.Removal { Texts = 1, HistoryDays = 1 };
            for (int i = 0; i < 11; i++) bigRemoval.Repo.Add("company-with-a-long-name/repo-with-a-long-name-" + i);
            foreach (var c in Language.Codes) {
                Language.Use(c);
                using (var k = new RemoveDialog(bigRemoval)) {
                    Render(k);
                    bool inside = k.RemoveButton.Right <= k.ClientSize.Width && k.CancelChoiceButton.Left >= 0 && k.RemoveButton.Bottom <= k.ClientSize.Height;
                    bool heights = k.Controls.OfType<Label>().All(l => TextRenderer.MeasureText(l.Text, l.Font, new Size(l.Width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height <= l.Height);
                    v(c + ": confirmation: the buttons fit inside and the texts are not cut", inside && heights);
                    k.Close();
                }
                using (var k = new RemoveDialog(new AccountAccess.Removal { Repo = new List<string> { "a/b" }, Texts = 1, HistoryDays = 1 })) {
                    Render(k);
                    v(c + ": confirmation of a single one: the button text fits", k.RemoveButton.Width >= TextRenderer.MeasureText(k.RemoveButton.Text, k.RemoveButton.Font).Width && k.CancelChoiceButton.Left >= Theme.S(24));
                    k.Close();
                }
            }
            Language.Use("en");
        } catch (Exception e) {
            v("exception: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
        }
        MainWindow.RemoveResponse = null;
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }

    public static int OnCopy(string result) {
        var sb = new StringBuilder();
        string data = DataStore.DataOverride;
        if (string.IsNullOrEmpty(data)) { File.WriteAllText(result, "FAIL the copy in PM_DATA_DIR is needed\n"); return 1; }
        Dialog.Silent = true;
        MainWindow.ScreenshotOnly = true;
        var before = DataStore.Load();
        var liveRepos = before.Repo.Where(r => !r.Missing).Select(r => r.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();
        sb.AppendLine("before: repo " + before.Repo.Count + ", missing " + before.Repo.Count(r => r.Missing) + ", live " + liveRepos.Count + ", texts " + before.Items.Count + ", decisions " + before.Decisions.Count);
        var f = new MainWindow();
        Render(f);
        int leftCount = Lines(f).Count(r => r.Group);
        string confirmSays = null;
        MainWindow.RemoveResponse = x => { confirmSays = ((RemoveDialog)x).Story.Replace("\n", " | "); return DialogResult.OK; };
        HandleClick(f, OverviewPage.HitTarget.RemoveAll);
        f.Close();
        MainWindow.RemoveResponse = null;
        var after = DataStore.Load();
        var stayed = after.Repo.Where(r => !r.Missing).Select(r => r.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();
        sb.AppendLine("group on the left: " + leftCount);
        sb.AppendLine("the confirmation says: " + (confirmSays ?? "(not open)"));
        sb.AppendLine("after: repo " + after.Repo.Count + ", missing " + after.Repo.Count(r => r.Missing) + ", live " + stayed.Count + ", texts " + after.Items.Count + ", decisions " + after.Decisions.Count);
        sb.AppendLine("remaining: " + string.Join(", ", after.Repo.Select(r => r.Name)));
        bool good = after.Repo.Count == liveRepos.Count && stayed.SequenceEqual(liveRepos) && after.Repo.All(r => !r.Missing);
        sb.AppendLine(good ? "GREEN: exactly the live ones remain" : "RED");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return good ? 0 : 1;
    }
}
