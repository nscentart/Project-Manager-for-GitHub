using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class RetryTests {
    const string FakeKey = "fake-key-for-retries";

    static string Error(Action a) {
        try { a(); return null; } catch (WriteException e) { return e.Message; }
    }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL a test data folder is needed\n"); return 1; }
        Language.Use("en");
        Dialog.Silent = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        int attempts = Writer.PublishAttempts, wait = Writer.PublishWaitMs;
        v("default: 6 attempts at 20 seconds", attempts == 6 && wait == 20000);
        Writer.PublishAttempts = 3;
        Writer.PublishWaitMs = 1;
        try {
            using (var s = new FakeServer()) {
                s.Who[FakeKey] = "mario"; s.Permissions[FakeKey] = "repo, read:org";
                var c = Client.Fake(s.Gateway, FakeKey, true);
                int prog = 0;
                Func<FakeServer.FakeRepo> repo = () => {
                    var r = s.New("mario/r" + (++prog), "User", "admin", false);
                    r.Release.Add(new FakeServer.FakeRelease { Id = 5, Tag = "v1.1", Title = "x", Draft = true });
                    return r;
                };
                Func<FakeServer.FakeRepo, string> via = r => "/repos/" + r.Name + "/releases/5";
                Func<FakeServer.FakeRepo, int> patch = r => s.Of("PATCH", via(r)).Count;
                var steps = new List<string>();
                Action<string> step = t => steps.Add(t);

                var r0 = repo();
                var f0 = Writer.Done(c, r0.Name);
                string e0 = Error(() => Writer.PublishWithRetry(c, f0, r0.Name, 5, true, step));
                v("right away: published, one attempt, no step", e0 == null && !r0.Release[0].Draft && patch(r0) == 1 && steps.Count == 0);

                var r1 = repo();
                var f1 = Writer.Done(c, r1.Name);
                s.Force("PATCH", via(r1), 404, "{\"message\":\"Not Found\"}", null);
                string e1 = Error(() => Writer.PublishWithRetry(c, f1, r1.Name, 5, true, step));
                v("404 then fine: it succeeds by itself", e1 == null && !r1.Release[0].Draft && patch(r1) == 2);
                v("404 then fine: it says where it is", steps.Count == 1 && steps[0].Contains("attempt 2 of 3") && steps[0].Contains("GitHub can't find the repo yet"));

                steps.Clear();
                var r2 = repo();
                var f2 = Writer.Done(c, r2.Name);
                s.ForceTimes("PATCH", via(r2), 404, "{\"message\":\"Not Found\"}", null, 99);
                string e2 = Error(() => Writer.PublishWithRetry(c, f2, r2.Name, 5, true, step));
                v("404 forever: three attempts", patch(r2) == 3);
                v("404 forever: does not say 'isn't on GitHub'", e2 == Language.T("writes.release_still_draft_github_isnt") && !e2.Contains("isn't on GitHub"));
                v("404 forever: stays a draft", r2.Release[0].Draft);
                v("404 forever: the steps with both attempts", steps.Any(x => x.Contains("attempt 2 of 3")) && steps.Any(x => x.Contains("attempt 3 of 3")));
                s.Force("PATCH", via(r2), 200, null, null);
                r2.Release[0].Draft = true;

                var r3 = repo();
                var f3 = Writer.Done(c, r3.Name);
                s.Force("PATCH", via(r3), 404, "{\"message\":\"Not Found\"}", null);
                string e3 = Error(() => Writer.PublishWithRetry(c, f3, r3.Name, 5, true, t => r3.Release[0].Draft = false));
                v("already published in the meantime: it is a success", e3 == null);
                v("already published in the meantime: it does not republish", patch(r3) == 1);

                var r4 = repo();
                var f4 = Writer.Done(c, r4.Name);
                s.ForceTimes("PATCH", via(r4), 404, "{\"message\":\"Not Found\"}", null, 99);
                string e4 = Error(() => Writer.PublishWithRetry(c, f4, r4.Name, 5, true, t => r4.Release.Clear()));
                v("really gone: it says so", e4 == Language.T("writes.release_no_longer_github_nothing"));

                var r5 = repo();
                var f5 = Writer.Done(c, r5.Name);
                s.ForceTimes("PATCH", via(r5), 404, "{\"message\":\"Not Found\"}", null, 99);
                s.ForceTimes("GET", via(r5), 404, "{\"message\":\"Not Found\"}", null, 99);
                s.ForceTimes("GET", "/repos/" + r5.Name + "/releases", 404, "{\"message\":\"Not Found\"}", null, 99);
                string e5 = Error(() => Writer.PublishWithRetry(c, f5, r5.Name, 5, true, step));
                v("GitHub can't find the repo: it says so, and that nothing was published", e5 == Language.T("writes.github_still_cant_find_repo") && e5.Contains("was not published"));

                foreach (var code in new[] { 403, 422, 500 }) {
                    steps.Clear();
                    var r6 = repo();
                    var f6 = Writer.Done(c, r6.Name);
                    s.ForceTimes("PATCH", via(r6), code, "{\"message\":\"no\"}", null, 99);
                    string e6 = Error(() => Writer.PublishWithRetry(c, f6, r6.Name, 5, true, step));
                    v(code + ": no retry, and nothing else is told", e6 != null && patch(r6) == 1 && steps.Count == 0);
                }

                var r7 = repo();
                var f7 = Writer.Done(c, r7.Name);
                string e7 = Error(() => Writer.PublishWithRetry(c, f7, "wrong", 5, true, step));
                v("wrong name: does not publish", e7 != null && patch(r7) == 0 && r7.Release[0].Draft);

                const string NF = "{\"message\":\"Not Found\"}";
                Writer.PublishAttempts = 6;
                steps.Clear();
                var r9 = repo();
                var f9 = Writer.Done(c, r9.Name);
                r9.Release.Clear();
                string e9 = Error(() => Writer.PublishWithRetry(c, f9, r9.Name, 5, true, step));
                v("deleted by others: it says so", e9 == Language.T("writes.release_no_longer_github_nothing"));
                v("deleted by others: only one wait, not all", steps.Count == 1 && patch(r9) == 0);
                v("deleted by others: while waiting it talks about the release, not the repo", steps.Count == 1 && steps[0] == Language.T("writes.github_cant_find_release_may", "sec", 1));
                var r10 = repo();
                var f10 = Writer.Done(c, r10.Name);
                var via10 = r10.Release[0];
                r10.Release.Clear();
                string e10 = Error(() => Writer.PublishWithRetry(c, f10, r10.Name, 5, true, t => { if (r10.Release.Count == 0) r10.Release.Add(via10); }));
                v("missing for one read only: it publishes", e10 == null && !via10.Draft && patch(r10) == 1);
                Writer.PublishAttempts = 3;

                var r11 = repo();
                var f11 = Writer.Done(c, r11.Name);
                s.Force("PATCH", via(r11), 404, NF, null);
                string e11 = Error(() => Writer.PublishWithRetry(c, f11, r11.Name, 5, true, t => { s.ForceTimes("GET", via(r11), 404, NF, null, 99); s.ForceTimes("GET", "/repos/" + r11.Name + "/releases", 404, NF, null, 99); }));
                v("after one attempt, unable to read again: it says it does not know", e11 == Language.T("writes.github_wont_let_read_release") && patch(r11) == 1);

                Action<string> stop = t => { throw new CheckStoppedException(Language.T("management.check_stopped_nothing_been_done")); };
                var r12 = repo();
                var f12 = Writer.Done(c, r12.Name);
                s.ForceTimes("PATCH", via(r12), 404, NF, null, 99);
                string e12 = Error(() => Writer.PublishWithRetry(c, f12, r12.Name, 5, true, stop));
                v("stopped after one attempt, still a draft: it says so, without 'nothing has been done'", e12 == Language.T("writes.stopped_read_again_release_still") && !e12.Contains("nothing has been done") && patch(r12) == 1);
                var r13 = repo();
                var f13 = Writer.Done(c, r13.Name);
                s.ForceTimes("PATCH", via(r13), 404, NF, null, 99);
                string e13 = Error(() => Writer.PublishWithRetry(c, f13, r13.Name, 5, true, t => { r13.Release[0].Draft = false; stop(t); }));
                v("stopped, but the 404 had taken effect: it is published, and does not say the opposite", e13 == null);
                var r14 = repo();
                var f14 = Writer.Done(c, r14.Name);
                s.ForceTimes("PATCH", via(r14), 404, NF, null, 99);
                string e14 = Error(() => Writer.PublishWithRetry(c, f14, r14.Name, 5, true, t => { s.ForceTimes("GET", via(r14), 404, NF, null, 99); stop(t); }));
                v("stopped, unable to read again: it says it does not know", e14 == Language.T("writes.github_wont_let_read_release"));
                var r15 = repo();
                var f15 = Writer.Done(c, r15.Name);
                string e15 = Error(() => Writer.PublishWithRetry(c, f15, r15.Name, 5, true, step, () => stop(null)));
                v("stopped before any attempt: the PATCH does not start, and 'nothing has been done' is true", e15 == Language.T("management.check_stopped_nothing_been_done") && patch(r15) == 0 && r15.Release[0].Draft);

                var P = WriteTests.ForScreenshot(s);
                P.Login = "mario";
                Writer.PublishWaitMs = 3000;
                Writer.PublishAttempts = 6;
                var r8 = repo();
                s.ForceTimes("PATCH", via(r8), 404, "{\"message\":\"Not Found\"}", null, 99);
                ConfirmDialog k = null;
                k = new ConfirmDialog(P, r8.Name, "Publish", "Publish", true, false, (cx, fx) => "", (cx, fx, st) => Writer.PublishWithRetry(cx, fx, st, 5, true, t => k.Step(t))) { Retry = true };
                k.ShowInTaskbar = false; k.StartPosition = FormStartPosition.Manual; k.Location = new Point(-20000, -20000);
                k.Show();
                Func<Func<bool>, bool> until = cond => { for (int i = 0; i < 1000; i++) { if (cond()) return true; Application.DoEvents(); Thread.Sleep(10); } return cond(); };
                v("window: it reads the repo again", until(() => k.Seen != null));
                k.NameBox.Text = r8.Name;
                Application.DoEvents();
                k.YesButton.PerformClick();
                bool writes = until(() => k.Story.Text.Contains("attempt 2 of 6"));
                v("window: while waiting it says where it is", writes);
                v("window: while waiting it is not blocked and can be cancelled", k.NoButton.Enabled);
                int before = patch(r8);
                k.NoButton.PerformClick();
                v("window: Cancel stops", k.Stopped);
                until(() => !k.Working);
                Application.DoEvents();
                Thread.Sleep(50);
                v("window: once stopped it no longer publishes", patch(r8) == before && r8.Release[0].Draft);
                k.Close();
                k.Dispose();

                Writer.PublishWaitMs = 200;
                Writer.PublishAttempts = 3;
                const string Read = "what is being confirmed";
                Func<FakeServer.FakeRepo, Action<ConfirmDialog, string>, Action<ConfirmDialog>, ConfirmDialog> open = (r, pa, co) => {
                    ConfirmDialog w = null;
                    w = new ConfirmDialog(P, r.Name, "Publish", "Publish", true, false, (cx, fx) => Read, (cx, fx, st) => Writer.PublishWithRetry(cx, fx, st, 5, true, t => pa(w, t), () => co(w))) { Retry = true };
                    w.ShowInTaskbar = false; w.StartPosition = FormStartPosition.Manual; w.Location = new Point(-20000, -20000);
                    w.Show();
                    until(() => w.Seen != null);
                    w.NameBox.Text = r.Name;
                    Application.DoEvents();
                    return w;
                };
                Action<ConfirmDialog> cancel = w => w.Invoke((Action)(() => w.NoButton.PerformClick()));
                Action<ConfirmDialog> waitFor = w => { until(() => !w.Working); Application.DoEvents(); };
                Action<ConfirmDialog> close = w => { w.Close(); w.Dispose(); };

                var r20 = repo();
                s.ForceTimes("PATCH", via(r20), 404, NF, null, 99);
                int checks = 0;
                var w1 = open(r20, (w, t) => w.Step(t), w => { if (++checks == 2) cancel(w); w.Check(); });
                w1.YesButton.PerformClick();
                waitFor(w1);
                v("Cancel right before the write: the PATCH does not start", checks == 2 && patch(r20) == 1 && r20.Release[0].Draft);
                v("Cancel right before the write: it says it is still a draft", w1.Notice.Text == Language.T("writes.stopped_read_again_release_still") && !w1.Done);
                v("after Cancel: 'Stopping...' does not remain", w1.Story.Text.Contains(Read) && !w1.Story.Text.Contains(Language.T("management.stopping")));

                s.ForceTimes("PATCH", via(r20), 404, NF, null, 1);
                int beforeNew = patch(r20);
                w1.YesButton.PerformClick();
                v("Publish again: the earlier stop no longer holds", !w1.Stopped);
                waitFor(w1);
                v("Publish again: it retries by itself and publishes", w1.Done && !r20.Release[0].Draft && patch(r20) == beforeNew + 2);
                close(w1);

                var r21 = repo();
                s.ForceTimes("PATCH", via(r21), 404, NF, null, 99);
                var w2 = open(r21, (w, t) => { w.Step(t); cancel(w); }, w => w.Check());
                w2.YesButton.PerformClick();
                waitFor(w2);
                v("Cancel in the last second of the wait: the PATCH does not start", patch(r21) == 1 && r21.Release[0].Draft && w2.Notice.Text == Language.T("writes.stopped_read_again_release_still"));
                close(w2);

                var r22 = repo();
                s.ForceTimes("PATCH", via(r22), 404, NF, null, 99);
                var w3 = open(r22, (w, t) => { w.Step(t); r22.Release[0].Draft = false; cancel(w); w.Step(t); }, w => w.Check());
                w3.YesButton.PerformClick();
                waitFor(w3);
                v("Cancel, but the attempt had arrived: it is a success", w3.Done && w3.Stopped && patch(r22) == 1);
                close(w3);

                var r23 = repo();
                s.ForceTimes("PATCH", via(r23), 404, NF, null, 99);
                var w4 = open(r23, (w, t) => w.Step(t), w => w.Check());
                w4.YesButton.PerformClick();
                waitFor(w4);
                v("after the last attempt: 'attempt 3 of 3' does not remain", !w4.Story.Text.Contains("attempt") && w4.Story.Text.Contains(Read));
                v("after the last attempt: below it says how it went", w4.Notice.Text == Language.T("writes.release_still_draft_github_isnt") && !w4.Done);
                close(w4);

                var r24 = repo();
                var w5 = open(r24, (w, t) => w.Step(t), w => w.Check());
                r24.Release.Clear();
                w5.YesButton.PerformClick();
                waitFor(w5);
                v("deleted by others, in the window: it says so and the wait does not remain", w5.Notice.Text == Language.T("writes.release_no_longer_github_nothing") && w5.Story.Text.Contains(Read));
                close(w5);
            }
        } catch (Exception e) {
            v("exception: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
        } finally {
            Writer.PublishAttempts = attempts;
            Writer.PublishWaitMs = wait;
        }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }
}
