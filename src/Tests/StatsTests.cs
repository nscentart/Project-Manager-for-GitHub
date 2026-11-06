using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class StatsTests {
    const string FakeKey = "fake-key-statistics";

    static Dictionary<string, object> Day(string data, int count, int unique) { return FakeServer.D("timestamp", data + "T00:00:00Z", "count", count, "uniques", unique); }

    static void FullTraffic(FakeServer.FakeRepo r) {
        r.Traffic["/traffic/views"] = FakeServer.D("count", 10, "uniques", 4, "views", new object[] { Day("2026-10-20", 6, 3), Day("2026-10-21", 4, 3) });
        r.Traffic["/traffic/clones"] = FakeServer.D("count", 5, "uniques", 2, "clones", new object[] { Day("2026-10-20", 3, 1), Day("2026-10-21", 2, 2) });
        r.Traffic["/traffic/popular/referrers"] = new object[] { FakeServer.D("referrer", "github.com", "count", 7, "uniques", 3) };
        r.Traffic["/traffic/popular/paths"] = new object[] { FakeServer.D("path", "/mario/app", "title", "app", "count", 9, "uniques", 4) };
    }

    static RepoInfo Info(string name) { return new RepoInfo { Name = name, Owner = name.Split('/')[0] }; }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL a test data folder is needed\n"); return 1; }
        Language.Use("en");
        Dialog.Silent = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string only = Language.T("github.github_only_shows_numbers_people"), understand = Language.T("github.github_gave_answer_dont_understand"), limit = Language.T("github.github_limited_requests_try_again");
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        using (var s = new FakeServer())
        using (var other = new FakeServer()) {
            try {
                s.Who[FakeKey] = "mario"; s.Permissions[FakeKey] = "repo, read:org";
                GitHub.Use(FakeKey, "mario");
                GitHub.OnTestServer(s.Gateway);

                var r = s.New("mario/app", "User", "admin", false);
                r.Stars = 5; r.Forks = 2; r.Watchers = 3;
                FullTraffic(r);
                var release = new FakeServer.FakeRelease { Id = 1, Tag = "v1.0", Last = "true" };
                release.Assets.Add(new KeyValuePair<string, long>("App.exe", 100)); release.Downloaded["App.exe"] = 40;
                release.Assets.Add(new KeyValuePair<string, long>("App.zip", 100)); release.Downloaded["App.zip"] = 2;
                r.Release.Add(release);
                var draft = new FakeServer.FakeRelease { Id = 2, Tag = "v2.0", Draft = true };
                draft.Assets.Add(new KeyValuePair<string, long>("App.exe", 100)); draft.Downloaded["App.exe"] = 999;
                r.Release.Add(draft);
                var n = GitHub.ReadStats(Info("mario/app"), null);
                v("views and people: GitHub's totals", n.ViewsRead && n.Views == 10 && n.UniqueViews == 4);
                v("the people are not the sum of the days (3+3)", n.UniqueViews != n.ViewDays.Sum(x => x.Uniques));
                v("days of the views", n.ViewDays.Count == 2 && n.ViewDays[0].Day == "2026-10-20" && n.ViewDays[0].Count == 6);
                v("clones", n.ClonesRead && n.Clones == 5 && n.UniqueClones == 2 && n.CloneDays.Count == 2);
                v("where people come from", n.ReferrersRead && n.Referrers.Count == 1 && n.Referrers[0].Name == "github.com" && n.Referrers[0].Count == 7);
                v("most viewed pages", n.PagesRead && n.Pages.Count == 1 && n.Pages[0].Name == "/mario/app" && n.Pages[0].Uniques == 4);
                v("downloads: sum of the attachments", n.DownloadsRead && n.Downloads == 42);
                v("downloads by version", n.Releases.Count == 1 && n.Releases[0].Tag == "v1.0" && n.Releases[0].Downloads == 42 && n.Releases[0].Assets.Count == 2);
                v("the draft does not count", n.Releases.All(x => x.Tag != "v2.0"));
                v("stars, forks and watchers", n.RepoRead && n.Stars == 5 && n.Forks == 2 && n.Watchers == 3);
                v("no errors", n.ViewsError == null && n.ClonesError == null && n.ReferrersError == null && n.PagesError == null && n.DownloadsError == null && n.RepoError == null);
                v("read: the time is there", !string.IsNullOrEmpty(n.ReadAt));
                v("reading and nothing else", s.Writes == 0);

                var foreign = s.New("luisa/recipes", "User", "read", false);
                foreign.Stars = 8;
                s.Put(foreign, "README.md", Encoding.UTF8.GetBytes("# recipes"));
                var no = GitHub.ReadStats(Info("luisa/recipes"), null);
                v("403: the views are not read", !no.ViewsRead && !no.ClonesRead && !no.ReferrersRead && !no.PagesRead);
                v("403: the exact reason", no.ViewsError == only && no.ClonesError == only && no.ReferrersError == only && no.PagesError == only);
                v("403: the rest is read all the same", no.DownloadsRead && no.RepoRead && no.Stars == 8 && no.DownloadsError == null && no.RepoError == null);
                var cell = OverviewPage.NumberCell(no, no.ViewsRead, no.ViewsError, no.Views);
                v("403: the cell says 'not available'", cell.Text == "not available");
                v("403: the sentence says so and does not write 0 views", OverviewPage.StatsSentence(no, 0).Contains("views not available") && !OverviewPage.StatsSentence(no, 0).Contains("0 views"));
                var notices = OverviewPage.StatsNotices(no);
                v("403: one reason only, once only", notices.Count == 1 && notices[0].Contains(only) && notices[0].StartsWith("Views, clones, sources, viewed pages: not available."));

                s.Force("GET", "/repos/mario/app/traffic/views", 403, "{\"message\":\"API rate limit exceeded\"}", new Dictionary<string, string> { { "X-RateLimit-Remaining", "0" } });
                var lim = GitHub.ReadStats(Info("mario/app"), null);
                v("403 from limit: says the limit, not the permission", lim.ViewsError == limit && !lim.ViewsRead);
                v("403 from limit: the other readings work", lim.ClonesRead && lim.DownloadsRead && lim.RepoRead);

                s.Force("GET", "/repos/mario/app/releases", 404, "{\"message\":\"Not Found\"}", null);
                var release404 = GitHub.ReadStats(Info("mario/app"), null);
                v("404 on releases: not read, never 0", !release404.DownloadsRead && release404.DownloadsError != null);
                v("404 on releases: the views are there", release404.ViewsRead && release404.RepoRead);
                v("404 on releases: the sentence says so", OverviewPage.StatsSentence(release404, 0).Contains("downloads not available"));

                s.Force("GET", "/repos/mario/app/traffic/views", 200, "{broken", null);
                var broken = GitHub.ReadStats(Info("mario/app"), null);
                v("broken JSON: I do not understand", broken.ViewsError == understand && !broken.ViewsRead);

                foreach (var testCase in new[] {
                    new[] { "missing total", "{\"uniques\":1,\"views\":[]}" }, new[] { "number as text", "{\"count\":\"12\",\"uniques\":1,\"views\":[]}" },
                    new[] { "negative number", "{\"count\":-3,\"uniques\":1,\"views\":[]}" }, new[] { "null number", "{\"count\":null,\"uniques\":1,\"views\":[]}" },
                    new[] { "missing list", "{\"count\":1,\"uniques\":1}" }, new[] { "day without date", "{\"count\":1,\"uniques\":1,\"views\":[{\"count\":1,\"uniques\":1}]}" },
                    new[] { "day with the wrong date", "{\"count\":1,\"uniques\":1,\"views\":[{\"timestamp\":\"yesterday\",\"count\":1,\"uniques\":1}]}" },
                    new[] { "list instead of object", "[]" } }) {
                    s.Force("GET", "/repos/mario/app/traffic/views", 200, testCase[1], null);
                    var x = GitHub.ReadStats(Info("mario/app"), null);
                    v(testCase[0] + ": does not become a 0", !x.ViewsRead && x.ViewsError == understand && x.ViewDays == null);
                }
                s.Force("GET", "/repos/mario/app/traffic/popular/referrers", 200, "{\"nothing\":1}", null);
                v("sources with a strange shape", GitHub.ReadStats(Info("mario/app"), null).ReferrersError == understand);
                s.Force("GET", "/repos/mario/app/releases", 200, "[{\"tag_name\":\"v1\",\"assets\":[{\"name\":\"a\",\"download_count\":\"many\"}]}]", null);
                v("non-numeric downloads", !GitHub.ReadStats(Info("mario/app"), null).DownloadsRead);
                s.Force("GET", "/repos/mario/app", 200, "{\"stargazers_count\":1,\"forks_count\":0}", null);
                var withoutOss = GitHub.ReadStats(Info("mario/app"), null);
                v("missing watchers: stars and forks not halfway", !withoutOss.RepoRead && withoutOss.RepoError == understand && withoutOss.Stars == 0);

                var good = GitHub.ReadStats(Info("mario/app"), null);
                s.Force("GET", "/repos/mario/app/traffic/views", 500, "{}", null);
                var after = GitHub.ReadStats(Info("mario/app"), good);
                v("after: the earlier views stay", after.ViewsRead && after.Views == 10 && after.ViewsError != null);
                v("after: the old reading does not change", good.ViewsError == null);
                var amber = OverviewPage.NumberCell(after, after.ViewsRead, after.ViewsError, after.Views);
                v("after: the old number is in amber", amber.Text == "10" && amber.Color == Theme.AmberDark);
                v("after: the page says it is old", OverviewPage.StatsNotices(after).Any(x => x.Contains("not read again, these are the earlier figures")));
                var resume = GitHub.ReadStats(Info("mario/app"), after);
                v("after: the error goes away when it is read again", resume.ViewsError == null);

                GitHub.OnTestServer(1);
                var down = GitHub.ReadStats(Info("mario/app"), null);
                string network = Language.T("github.github_not_responding_check_internet");
                v("network down: no exception, everything not read", !down.ViewsRead && !down.DownloadsRead && !down.RepoRead && down.ReadAt == null);
                v("network down: the reason", down.ViewsError == network && down.DownloadsError == network && down.RepoError == network);
                GitHub.OnTestServer(s.Gateway);

                var longText = s.New("mario/long", "User", "admin", false);
                for (int i = 1; i <= 120; i++) {
                    var q = new FakeServer.FakeRelease { Id = 1000 + i, Tag = "v" + i };
                    q.Assets.Add(new KeyValuePair<string, long>("A.exe", 1)); q.Downloaded["A.exe"] = 2;
                    longText.Release.Add(q);
                }
                int beforeGet = s.Of("GET", "/repos/mario/long/releases").Count;
                var longStats = GitHub.ReadStats(Info("mario/long"), null);
                v("120 versions: all, following the pages", longStats.Releases.Count == 120 && longStats.Downloads == 240);
                v("120 versions: two pages", s.Of("GET", "/repos/mario/long/releases").Count - beforeGet == 2);
                s.FakeLink = "<http://127.0.0.1:" + other.Gateway + "/repos/mario/long/releases?page=2>; rel=\"next\"";
                var outside = GitHub.ReadStats(Info("mario/long"), null);
                s.FakeLink = null;
                v("a later page on another server: the key does not go there", other.RequestLog.Count == 0);
                v("a later page on another server: no total halfway", !outside.DownloadsRead && outside.Releases == null && outside.DownloadsError != null);

                var s1 = GitHub.ReadStats(Info("mario/app"), null);
                v("first reading: no change", s1.StarsBefore == -1 && s1.StarsChanged == null);
                r.Stars = 7; r.Forks = 3;
                var s2 = GitHub.ReadStats(Info("mario/app"), s1);
                v("stars changed", s2.Stars == 7 && s2.StarsBefore == 5 && s2.StarsChanged == today);
                v("forks changed", s2.Forks == 3 && s2.ForksBefore == 2 && s2.ForksChanged == today);
                var news = OverviewPage.News(s2, new OverviewPage.Counts { NewReports = 2, NewComments = 1 }, today);
                v("news: all four", news.Count == 4 && news[0].StartsWith("2 new reports") && news[1].StartsWith("1 new comment") && news[2].StartsWith("Stars: from 5 to 7") && news[3].StartsWith("Forks: from 2 to 3"));
                v("news: after 14 days they are no longer news", OverviewPage.News(s2, new OverviewPage.Counts(), DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd")).Count == 0);
                var s3 = GitHub.ReadStats(Info("mario/app"), s2);
                v("same number: the change stays the same", s3.StarsBefore == 5);

                var panOnly = GitHub.Overview(Info("luisa/recipes"), null);
                v("the missing numbers do not make 'read halfway'", no.ViewsError != null && OverviewPage.AllRead(panOnly));

                string file = Path.Combine(DataStore.Folder, "history.json");
                Action clean = () => { foreach (var x in Directory.GetFiles(DataStore.Folder, "history.json*")) File.Delete(x); };
                clean();
                var st = new HistoryArchive();
                History.Join(st, "mario/app", good, "2026-10-21");
                v("history: one day per date", History.DaysHowMany(st, "mario/app") == 2);
                var c0 = History.Copy(st, "mario/app");
                v("history: views, uniques, clones and total of downloads", c0[1].Day == "2026-10-21" && c0[1].Views == 4 && c0[1].Uniques == 3 && c0[1].Clones == 2 && c0[1].Downloads == 42);
                v("history: the day without downloads stays -1", c0[0].Downloads == -1);
                v("history: the copy is a copy", ReferenceEquals(c0[0], st.Repo["mario/app"][0]) == false);

                var newItems = good.Copy();
                newItems.ViewDays = new List<TrafficDay> { new TrafficDay { Day = "2026-10-21", Count = 9, Uniques = 5 }, new TrafficDay { Day = "2026-10-22", Count = 1, Uniques = 1 } };
                newItems.CloneDays = new List<TrafficDay>();
                newItems.Downloads = 50;
                History.Join(st, "mario/app", newItems, "2026-10-22");
                var c1 = History.Copy(st, "mario/app");
                v("history: same date, GitHub wins", c1.First(x => x.Day == "2026-10-21").Views == 9);
                v("history: the day GitHub no longer gives stays", c1.First(x => x.Day == "2026-10-20").Views == 6);
                v("history: the clones of that day stay", c1.First(x => x.Day == "2026-10-21").Clones == 2);
                v("history: in order", c1.Select(x => x.Day).SequenceEqual(new[] { "2026-10-20", "2026-10-21", "2026-10-22" }));
                var forDay = History.DownloadForDay(c1);
                v("downloads of the day: the difference between two totals", forDay.Count == 1 && forDay[0].Day == "2026-10-22" && forDay[0].Count == 8);
                v("downloads of the day: a falling total does not make a negative", History.DownloadForDay(new[] { new HistoryDay { Day = "2026-01-01", Downloads = 9 }, new HistoryDay { Day = "2026-01-02", Downloads = 3 } }).Single().Count == 0);
                v("downloads of the day: the first day is unknown", History.DownloadForDay(new[] { new HistoryDay { Day = "2026-01-01", Downloads = 9 } }).Count == 0);

                var failed = newItems.Copy();
                failed.ViewsError = "x"; failed.Views = 999; failed.ClonesError = "x"; failed.DownloadsError = "x"; failed.Downloads = 999;
                failed.ViewDays = new List<TrafficDay> { new TrafficDay { Day = "2026-10-21", Count = 999, Uniques = 999 } };
                History.Join(st, "mario/app", failed, "2026-10-23");
                var c2 = History.Copy(st, "mario/app");
                v("history: a failed reading writes nothing", c2.Count == 3 && c2.All(x => x.Views != 999 && x.Downloads != 999));
                var never = new RepoStats { ViewsError = "x", DownloadsError = "x" };
                History.Join(st, "mario/never", never, "2026-10-23");
                v("history: a repo never read creates nothing", !st.Repo.ContainsKey("mario/never"));

                History.Save(st);
                v("history: round trip", History.Copy(History.Load(), "mario/app").Select(x => x.Views).SequenceEqual(c2.Select(x => x.Views)));
                v("history: no .new left around", !File.Exists(file + ".new"));
                History.Save(st);
                v("history: the second time too", File.Exists(file) && !File.Exists(file + ".new"));

                foreach (var testCase in new[] { new[] { "bad JSON", "{broken" }, new[] { "a number instead of the repos", "{\"Repo\":3}" }, new[] { "null", "null" }, new[] { "a list", "[]" }, new[] { "empty", "" } }) {
                    clean();
                    File.WriteAllText(file, testCase[1], new UTF8Encoding(false));
                    bool holds = true; HistoryArchive readHistory = null;
                    try { readHistory = History.Load(); } catch { holds = false; }
                    v("broken history, " + testCase[0] + ": does not block and restarts empty", holds && readHistory != null && readHistory.Repo.Count == 0);
                    v("broken history, " + testCase[0] + ": the file is kept aside", File.Exists(file + ".broken") && File.ReadAllText(file + ".broken") == testCase[1]);
                    bool save = true;
                    try { History.Join(readHistory, "mario/app", good, today); History.Save(readHistory); } catch { save = false; }
                    v("broken history, " + testCase[0] + ": restarts and saves", save && History.DaysHowMany(History.Load(), "mario/app") > 0);
                }
                clean();
                string oldJson = "{\"Repo\":{\"mario/app\":[{\"Giorno\":\"2026-01-05\",\"Visite\":7,\"Unici\":-1,\"Cloni\":-1,\"Download\":3},{\"Giorno\":\"2026-01-06\",\"Visite\":8,\"Unici\":-1,\"Cloni\":-1,\"Download\":-1}],\"altro/x\":[{\"Giorno\":\"2026-01-07\",\"Visite\":9,\"Unici\":-1,\"Cloni\":-1,\"Download\":-1}]}}";
                File.WriteAllText(file, oldJson, new UTF8Encoding(false));
                HistoryArchive blocked = null; bool holds2 = true;
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    try {
                        blocked = History.Load();
                        History.Join(blocked, "mario/app", good, "2026-10-21");
                        History.Save(blocked);
                        History.Save(blocked);
                    } catch { holds2 = false; }
                    v("locked file: stops nothing and starts without the old days", holds2 && blocked != null && History.DaysHowMany(blocked, "mario/app") == 2);
                    v("locked file: not treated as broken", !File.Exists(file + ".broken"));
                }
                v("locked file: two saves did not touch it", File.ReadAllText(file) == oldJson && !File.Exists(file + ".bak"));
                History.Save(blocked);
                var resumed = History.Load();
                v("reopened: the old days are still there", History.Copy(resumed, "mario/app").Any(x => x.Day == "2026-01-05" && x.Views == 7 && x.Downloads == 3) && History.DaysHowMany(resumed, "altro/x") == 1);
                v("reopened: the new ones too", History.DaysHowMany(resumed, "mario/app") == 4 && History.Copy(resumed, "mario/app").Select(x => x.Day).SequenceEqual(History.Copy(resumed, "mario/app").Select(x => x.Day).OrderBy(x => x, StringComparer.Ordinal)));
                v("reopened: the good file stays also as .bak", File.ReadAllText(file + ".bak") == oldJson);
                v("reopened: in memory they are all visible too", History.DaysHowMany(blocked, "mario/app") == 4);
                History.Save(resumed); History.Save(resumed);
                v("reopened: two more saves lose nothing", History.DaysHowMany(History.Load(), "mario/app") == 4);
                clean();
                File.WriteAllText(file, oldJson, new UTF8Encoding(false));
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) blocked = History.Load();
                File.WriteAllText(file, "{broken", new UTF8Encoding(false));
                History.Join(blocked, "mario/app", good, "2026-10-21");
                History.Save(blocked);
                v("reopened but really broken: set aside, and saved", File.ReadAllText(file + ".broken") == "{broken" && History.DaysHowMany(History.Load(), "mario/app") == 2);

                clean();
                File.WriteAllText(file, "{\"Repo\":{\"a/b\":[{\"Giorno\":\"ieri\",\"Visite\":1},{\"Giorno\":\"2026-13-45\",\"Visite\":2},null,{\"Giorno\":\"2026-10-20\",\"Visite\":3},{\"Giorno\":\"2026-10-20\",\"Visite\":4}],\"c/d\":null}}", new UTF8Encoding(false));
                var dirty = History.Load();
                v("history: bad dates are discarded, and only once per date", History.DaysHowMany(dirty, "a/b") == 1 && History.Copy(dirty, "a/b")[0].Views == 4);
                v("history: a repo without days does not stop it", !dirty.Repo.ContainsKey("c/d"));

                History.Rename(st, "mario/app", "mario/new");
                v("history: follows the new name", History.DaysHowMany(st, "mario/app") == 0 && History.DaysHowMany(st, "mario/new") == 3);
                History.Rename(st, "MARIO/NEW", "mario/new");
                v("history: same name, loses nothing", History.DaysHowMany(st, "mario/new") == 3);
                v("history: remove says how many days", History.Remove(st, "Mario/New") == 3 && History.DaysHowMany(st, "mario/new") == 0);
                v("history: remove of one that is not there", History.Remove(st, "x/y") == 0);
                clean();

                var oldArchive = Legacy.ArchiveFromJson("{\"Login\":\"mario\",\"Repo\":[{\"Nome\":\"mario/app\",\"Proprietario\":\"mario\"}]}");
                v("old archive: no numbers, no errors", oldArchive.Repo[0].Stats == null && !oldArchive.UpdateNoticeOff && oldArchive.SkippedVersion == null);
                var never2 = OverviewPage.NumberCell(null, false, null, 0);
                v("old archive: the cell says 'not read'", never2.Text == "not read");
                v("old archive: the sentence does not invent", OverviewPage.StatsSentence(null, 2).Contains("views not available") && !OverviewPage.StatsSentence(null, 2).Contains("0 download"));
                string ida = DataStore.Json().Serialize(new Archive { Repo = new List<RepoInfo> { new RepoInfo { Name = "a/b", Stats = good } } });
                v("the numbers stay in the archive", DataStore.Json().Deserialize<Archive>(ida).Repo[0].Stats.Views == 10);

                var f = new RepoStats { ViewsRead = true, Views = 312, UniqueViews = 87, DownloadsRead = true, Downloads = 1204 };
                v("the sentence, word for word", OverviewPage.StatsSentence(f, 3) == "Last 14 days: 312 views from 87 people. All time: 1,204 downloads. Right now: 3 reports to read.");
                var f1 = new RepoStats { ViewsRead = true, Views = 1, UniqueViews = 1, DownloadsRead = true, Downloads = 1 };
                v("the sentence, in the singular", OverviewPage.StatsSentence(f1, 1) == "Last 14 days: 1 view from 1 person. All time: 1 download. Right now: 1 report to read.");
                var f0 = new RepoStats { ViewsRead = true, DownloadsRead = true };
                v("a really read 0 is 0", OverviewPage.StatsSentence(f0, 0) == "Last 14 days: 0 views from 0 people. All time: 0 downloads. Right now: 0 reports to read.");
                v("a read 0 is shown", OverviewPage.NumberCell(f0, true, null, 0).Text == "0");
                v("never read without error: not read", OverviewPage.NumberCell(new RepoStats(), false, null, 0).Text == "not read");
                v("thousands with a separator", OverviewPage.NumberCell(f, true, null, 1204).Text == "1,204");

                v("table: eight columns", OverviewPage.Columns.Length == 8 && OverviewPage.Weights.Length == 8);
                v("table: every column has its space", OverviewPage.Weights.All(x => x > 0));
                v("table: the requested columns", OverviewPage.Columns.SequenceEqual(new[] { "Repo", "Who sees it", "Version", "Views, 14 days", "Downloads", "Stars", "Reports to read", "Errors" }));
                var readOverview = new RepoOverview { ReleaseRead = true, LicenseRead = true, ReadmeRead = true, ReleaseTag = "v1", License = "MIT", Readme = true, CommitsSinceRelease = 0 };
                var withAfter = new RepoOverview { ReleaseRead = true, LicenseRead = true, ReadmeRead = true, ReleaseTag = "v1", License = "MIT", Readme = true, CommitsSinceRelease = 3 };
                var without = new RepoOverview { ReleaseRead = true, LicenseRead = true, ReadmeRead = true, ReleaseTag = "", License = "", Readme = false, CommitsSinceRelease = -1 };
                var liveRepos = new List<RepoInfo> { new RepoInfo { Name = "a/ok", Overview = readOverview }, new RepoInfo { Name = "a/after", Overview = withAfter }, new RepoInfo { Name = "a/without", Overview = without } };
                Func<string, OverviewPage.Counts> howMany = x => new OverviewPage.Counts { Quarantine = x == "a/ok" ? 2 : 0, Errors = x == "a/without" ? 1 : 0, Handovers = x == "a/ok" ? 1 : -1 };
                var chip = OverviewPage.Problems(liveRepos, liveRepos, howMany);
                v("at the top all the problems remain, license and commits included", chip.SequenceEqual(new[] { "1 repo without a release", "1 repo with commits after the release", "1 repo without a license", "1 repo without a main page", "1 handoff waiting for a release", "2 texts in quarantine", "1 recurring error" }));
                v("at the top: nothing to say, no labels", OverviewPage.Problems(new List<RepoInfo> { liveRepos[0] }, new List<RepoInfo> { liveRepos[0] }, x => new OverviewPage.Counts()).Count == 0);
                v("at the top: the missing numbers are kept apart", OverviewPage.MissingStats(new[] { new RepoInfo { Stats = no }, new RepoInfo { Stats = good }, new RepoInfo() }).SequenceEqual(new[] { "numbers not available on 1 repo" }));
                v("the page cards: license, people and commits after stay", Overview(new RepoInfo { Name = "a/after", Overview = withAfter }));
            } catch (Exception e) {
                v("exception: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
            } finally {
                GitHub.OnRealGitHub();
                GitHub.Exit();
            }
        }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }

    static bool Overview(RepoInfo r) {
        using (var f = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), ClientSize = new Size(1100, 800) })
        using (var p = new OverviewPage { Dock = DockStyle.Fill }) {
            f.Controls.Add(p);
            f.Show();
            p.Configure(r, new List<RepoInfo> { r }, "a", n => new OverviewPage.Counts(), false, false, n => new List<HistoryDay>());
            using (var b = new Bitmap(p.Width, p.Height)) p.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
            Application.DoEvents();
            var cards = p.CardsForTest();
            return cards.Any(c => c.StartsWith(Language.T("common.license") + " | MIT")) && cards.Any(c => c.StartsWith(Language.T("common.collaborators") + " | ")) && cards.Any(c => c.Contains(Language.T("overview.commits_after_latest_release_2") + ": 3"));
        }
    }
}
