using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public partial class OverviewPage : Panel {
    public class Counts { public int Quarantine, Work, Errors, Handovers = -1, NewReports, NewComments; }
    public class Cell { public string Text; public Color Color; }
    public enum HitTarget { None, Group, RemoveAll, RemoveThis }

    class Card {
        public string Title, Large, Sentence, Notice;
        public Color LargeWith = Theme.Ink;
        public List<string[]> Lines = new List<string[]>();
        public int[] Stats; public string[] Captions;
    }

    RepoInfo repo;
    List<RepoInfo> allRepoList = new List<RepoInfo>();
    Func<string, Counts> accounts = n => new Counts();
    string login = "";
    bool stale;
    int top;
    readonly List<KeyValuePair<Rectangle, RepoInfo>> lines = new List<KeyValuePair<Rectangle, RepoInfo>>();
    RepoInfo above;
    bool missingOpen;
    Func<string, List<HistoryDay>> historyOf = n => new List<HistoryDay>();
    List<HistoryDay> days = new List<HistoryDay>();
    readonly List<KeyValuePair<Rectangle, HitTarget>> buttons = new List<KeyValuePair<Rectangle, HitTarget>>();
    readonly List<KeyValuePair<Rectangle, string>> bars = new List<KeyValuePair<Rectangle, string>>();
    readonly ToolTip hint = new ToolTip();
    HitTarget buttonAbove;
    string barAbove;
    public event Action<RepoInfo> ChosenRepo;
    public event Action GroupClicked, RemoveAll, RemoveThis;

    static int S(int v) { return Theme.S(v); }

    public OverviewPage() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        AutoScroll = true; BackColor = Theme.Backdrop;
    }

    public void Configure(RepoInfo r, List<RepoInfo> allRepos, string who, Func<string, Counts> howMany, bool notUpdated, bool groupOpen, Func<string, List<HistoryDay>> history) {
        bool isOther = r != repo;
        repo = r; allRepoList = allRepos; login = who ?? ""; accounts = howMany; stale = notUpdated; missingOpen = groupOpen; historyOf = history;
        days = r == null ? new List<HistoryDay>() : history(r.Name);
        if (isOther) AutoScrollPosition = Point.Empty;
        Measure();
        Invalidate();
    }

    void Measure() {
        using (var g = CreateGraphics()) { top = 0; int h = Scene(g, false); AutoScrollMinSize = new Size(0, h); }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); if (IsHandleCreated) Measure(); Invalidate(); }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (Form.ActiveForm == FindForm()) Focus(); }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        top = AutoScrollPosition.Y;
        lines.Clear(); buttons.Clear(); bars.Clear();
        Scene(e.Graphics, true);
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        RepoInfo s = null;
        var t = BelowButton(e.Location);
        if (t == HitTarget.None) foreach (var kv in lines) if (kv.Key.Contains(e.Location)) s = kv.Value;
        if (s != above || t != buttonAbove) { above = s; buttonAbove = t; Cursor = s != null || t != HitTarget.None ? Cursors.Hand : Cursors.Default; Invalidate(); }
        string b = null;
        foreach (var kv in bars) if (kv.Key.Contains(e.Location)) b = kv.Value;
        if (b != barAbove) { barAbove = b; if (b == null) hint.Hide(this); else hint.Show(b, this, e.X + S(12), e.Y + S(12), 4000); }
    }
    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        if (barAbove != null) { barAbove = null; hint.Hide(this); }
        if (above != null || buttonAbove != HitTarget.None) { above = null; buttonAbove = HitTarget.None; Invalidate(); }
    }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); HandleClick(e.Location); }

    HitTarget BelowButton(Point p) {
        var t = HitTarget.None;
        foreach (var kv in buttons) if (kv.Key.Contains(p)) t = kv.Value;
        return t;
    }

    public void HandleClick(Point p) {
        var t = BelowButton(p);
        if (t == HitTarget.Group) { if (GroupClicked != null) GroupClicked(); return; }
        if (t == HitTarget.RemoveAll) { if (RemoveAll != null) RemoveAll(); return; }
        if (t == HitTarget.RemoveThis) { if (RemoveThis != null) RemoveThis(); return; }
        foreach (var kv in lines) if (kv.Key.Contains(p) && ChosenRepo != null) { ChosenRepo(kv.Value); return; }
    }

    public Rectangle ZoneOf(HitTarget t) {
        foreach (var kv in buttons) if (kv.Value == t) return kv.Key;
        return Rectangle.Empty;
    }
    public List<string> NamesInTable() { return lines.Select(x => x.Value.Name).ToList(); }

    public List<string> CardsForTest() { return Cards().Select(x => x.Title + " | " + x.Large + " | " + x.Sentence + " | " + string.Join(" | ", x.Lines.Select(r => string.Join(": ", r)))).ToList(); }

    public List<string> Drawn() {
        var n = repo != null ? repo.Stats : null;
        var l = new List<string> { Language.T("stats.how_going"), Language.T("stats.github_says_how_many_people"), Language.T("stats.where_people_come"), Language.T("stats.most_viewed_pages"), Language.T("stats.worth_look"), Language.T("stats.views_day_day"), Language.T("stats.downloads_day_day"), Language.T("stats.downloads_version"),
            Language.T("missing.remove_all"), Language.T("missing.remove_list"), Language.T("missing.github_no_longer_lists_remove"), Language.T("stats.not_read_yet_arrive_next"), Language.T("stats.nothing_yet"), Language.T("stats.github_only_gives_total_daily"), Language.T("stats.no_published_versions"), Language.T("stats.arent_following_repo_new_reports"), Language.T("stats.nothing_new_last_14_days"),
            Language.T("stats.views"), Language.T("stats.people"), Language.T("stats.clones"), Language.T("stats.downloads_2"), Language.T("stats.stars_2"), Language.T("stats.forks"), Language.T("stats.watchers"), Language.T("stats.main_page") };
        l.AddRange(Columns);
        l.Add(StatsSentence(n, 3));
        l.AddRange(StatsNotices(n));
        l.AddRange(News(n, new Counts { NewReports = 2, NewComments = 1 }, DateTime.UtcNow.ToString("yyyy-MM-dd")));
        return l;
    }

    public static string Data(string iso) {
        DateTime t;
        return DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out t) ? t.ToLocalTime().ToString("dd/MM/yyyy") : "";
    }
    static string V(string s) { return Screener.MakeVisible((s ?? "").Replace("\r", " ").Replace("\n", " ")); }
    public static string ShortOf(RepoInfo r, string who) {
        return V(Organization.Short(r, who));
    }

    public static string OwnerOf(RepoInfo r, string who) {
        string p = r.Owner;
        if (string.IsNullOrEmpty(p) && r.Name != null && r.Name.IndexOf('/') > 0) p = r.Name.Substring(0, r.Name.IndexOf('/'));
        string type = Organization.Equal(p, who) ? Language.T("overview.personal") : (r.Organization || r.Mine) ? Language.T("common.organization") : Language.T("overview.others");
        return type + " · " + V(p);
    }

    static string RoleOf(string role) {
        switch ((role ?? "").ToLowerInvariant()) {
            case "read": return Language.T("overview.reads");
            case "triage": return Language.T("overview.reads_sorts_reports");
            case "write": return Language.T("overview.writes");
            case "maintain": return Language.T("overview.writes_manages");
            case "admin": return Language.T("overview.administers");
            default: return Language.T("overview.unknown_permission", "role", V(role));
        }
    }

    static List<string> WhatIsMissing(RepoOverview p, Counts c) {
        var l = new List<string>();
        if (p != null) {
            if (p.ReleaseRead && string.IsNullOrEmpty(p.ReleaseTag)) l.Add(Language.T("overview.no_release"));
            else if (p.CommitsSinceRelease > 0) l.Add(Language.N("overview.commits_after_latest_release", p.CommitsSinceRelease));
            if (p.LicenseRead && string.IsNullOrEmpty(p.License)) l.Add(Language.T("overview.no_license"));
            if (p.ReadmeRead && !p.Readme) l.Add(Language.T("overview.no_main_page"));
        }
        if (c.Handovers > 0) l.Add(Language.N("overview.handoffs_waiting_release", c.Handovers));
        if (c.Quarantine > 0) l.Add(Language.N("overview.texts_quarantine", c.Quarantine));
        if (c.Errors > 0) l.Add(Language.N("overview.recurring_errors", c.Errors));
        return l;
    }

    public static bool AllRead(RepoOverview p) { return p != null && p.ReleaseRead && p.LicenseRead && p.ReadmeRead; }

    public static List<string> ToRead(IEnumerable<RepoInfo> liveRepos) {
        var l = new List<string>();
        int never = liveRepos.Count(r => r.Overview == null), meta = liveRepos.Count(r => r.Overview != null && !AllRead(r.Overview));
        if (never > 0) l.Add(Language.N("overview.repos_not_read_yet", never));
        if (meta > 0) l.Add(Language.N("overview.repos_partly_read", meta));
        return l;
    }

    public static string AllSentence(int liveRepos, int chip, List<string> toRead) {
        if (chip > 0) return null;
        if (liveRepos == 0) return Language.T("overview.no_repos_check");
        return toRead.Count > 0 ? Language.T("overview.until_read_dont_know_anything") : Language.T("overview.nothing_missing");
    }

    public static string RepoTitle(RepoOverview p, int chip) {
        return chip > 0 ? Language.T("overview.whats_missing") : AllRead(p) ? Language.T("overview.nothing_missing_2") : Language.T("overview.incomplete_reading");
    }

    static List<string> NotRead(RepoOverview p) {
        var l = new List<string>();
        if (p == null) return l;
        if (!p.ReleaseRead && p.ReleaseError != null) l.Add(Language.T("overview.the_release"));
        if (p.Collaborators == null && p.CollaboratorsError != null) l.Add(Language.T("overview.the_collaborators"));
        if (!p.LicenseRead && p.LicenseError != null) l.Add(Language.T("overview.the_license"));
        if (!p.ReadmeRead && p.ReadmeError != null) l.Add(Language.T("overview.main_page"));
        return l;
    }

    static string Errors(RepoOverview p, bool hasData, params string[] errors) {
        var e = errors.Where(x => x != null).Distinct().ToList();
        if (e.Count == 0) return null;
        return (hasData ? Language.T("overview.not_read_again_earlier_data") : Language.T("overview.could_not_read_2")) + string.Join("; ", e) + ".";
    }

    List<Card> Cards() {
        var p = repo.Overview;
        var c = accounts(repo.Name);
        var l = new List<Card>();
        string Not = Language.T("overview.not_read_4");

        var v = new Card { Title = Language.T("overview.version") };
        if (p == null || !p.ReleaseRead) { v.Large = Language.T("overview.not_read"); v.LargeWith = Theme.Gray; v.Sentence = Language.T("overview.dont_know_yet_what_latest"); }
        else if (string.IsNullOrEmpty(p.ReleaseTag)) { v.Large = Language.T("overview.no_release_2"); v.LargeWith = Theme.AmberDark; v.Sentence = Language.T("overview.release_version_ready_download_none"); }
        else {
            v.Large = V(p.ReleaseTag);
            v.Sentence = Language.T("overview.latest_version_ready_download", "part", (Data(p.ReleaseDate).Length > 0 ? Language.T("overview.published_on", "releasedata", Data(p.ReleaseDate)) : ""));
        }
        bool tag = p != null && p.TagRead, com = p != null && p.CommitRead;
        v.Lines.Add(new[] { Language.T("overview.latest_tag_version_bookmark"), !tag ? Not : string.IsNullOrEmpty(p.LatestTag) ? Language.T("common.none_3") : V(p.LatestTag) });
        v.Lines.Add(new[] { Language.T("overview.commits_after_latest_release_2"), p == null || !p.ReleaseRead ? Not : string.IsNullOrEmpty(p.ReleaseTag) ? Language.T("overview.no_release") : p.CommitsSinceRelease < 0 ? Not : p.CommitsSinceRelease.ToString() });
        v.Lines.Add(new[] { Language.T("overview.last_commit_saved_change"), !com ? Not : string.IsNullOrEmpty(p.LatestCommit) ? Language.T("common.none_3") : Data(p.LatestCommit) });
        if (p != null) v.Notice = Errors(p, p.ReleaseRead || tag || com, p.ReleaseError, p.TagError, p.CommitsSinceReleaseError, p.CommitError);
        l.Add(v);

        var w = new Card { Title = Language.T("common.who_sees"), Large = repo.Private ? Language.T("overview.private") : Language.T("common.public_2"), LargeWith = repo.Private ? Theme.Ink : Theme.GreenDark,
            Sentence = repo.Private ? Language.T("overview.only_people_gave_permission_see") : Language.T("overview.anyone_internet_see_even_without") };
        bool org = repo.Organization || (repo.Mine && !Organization.Equal(repo.Owner, login));
        w.Lines.Add(new[] { org ? Language.T("overview.owner_organization") : Language.T("overview.owner"), V(repo.Owner) });
        if (org && repo.Private) w.Sentence = Language.T("overview.only_organization_members_people_gave");
        l.Add(w);

        var k = new Card { Title = Language.T("common.collaborators") };
        if (p == null || p.Collaborators == null) { k.Large = Language.T("overview.not_read_3"); k.LargeWith = Theme.Gray; k.Sentence = Language.T("overview.dont_know_yet_who_get"); }
        else {
            var otherPeople = Organization.Other(p.Collaborators, repo, login);
            k.Large = otherPeople.Count == 0 ? (p.Collaborators.Count == 0 ? Language.T("overview.nobody") : Language.T("overview.only_you")) : Language.N("overview.people", p.Collaborators.Count);
            k.Sentence = otherPeople.Count == 0 ? Language.T("overview.nobody_else_get_repo") : Language.T("overview.who_get_what");
            foreach (var x in p.Collaborators.OrderByDescending(x => x.Name == repo.Owner).ThenBy(x => x.Name))
                k.Lines.Add(new[] { V(x.Name) + (Organization.Equal(x.Name, login) ? Language.T("overview.you") : ""), RoleOf(x.Role) });
        }
        if (p != null) k.Notice = Errors(p, p.Collaborators != null, p.CollaboratorsError);
        l.Add(k);

        var q = new Card { Title = Language.T("common.license") };
        if (p == null || !p.LicenseRead) { q.Large = Language.T("overview.not_read"); q.LargeWith = Theme.Gray; q.Sentence = Language.T("overview.dont_know_yet_license"); }
        else if (string.IsNullOrEmpty(p.License)) { q.Large = Language.T("common.none_2"); q.LargeWith = Theme.AmberDark; q.Sentence = Language.T("overview.without_license_nobody_permission_reuse"); }
        else { q.Large = V(p.License); q.Sentence = p.OtherLicense ? Language.T("overview.tells_others_what_code_github") : Language.T("overview.tells_others_what_code"); }
        q.Lines.Add(new[] { Language.T("overview.copyright_notice_readme"), p == null || !p.ReadmeRead ? Not : !p.Readme ? Language.T("overview.no_readme") : p.ReadmeCopyright ? Language.T("overview.yes") : Language.T("overview.no") });
        if (p != null) q.Notice = Errors(p, p.LicenseRead, p.LicenseError);
        l.Add(q);

        var m = new Card { Title = Language.T("overview.main_page_readme") };
        if (p == null || !p.ReadmeRead) { m.Large = Language.T("overview.not_read"); m.LargeWith = Theme.Gray; m.Sentence = Language.T("overview.dont_know_yet_one"); }
        else if (!p.Readme) { m.Large = Language.T("overview.missing"); m.LargeWith = Theme.AmberDark; m.Sentence = Language.T("overview.page_see_when_open_repo"); }
        else { m.Large = Language.T("overview.present"); m.Sentence = Language.T("overview.page_see_when_open_repo_2"); }
        m.Lines.Add(new[] { Language.T("overview.last_change"), p == null || !p.ReadmeRead ? Not : !p.Readme ? Language.T("common.none_3") : Data(p.ReadmeDate).Length > 0 ? Data(p.ReadmeDate) : Not });
        if (c.Handovers >= 0) m.Lines.Add(new[] { Language.T("overview.accepted_handoffs_waiting_release"), c.Handovers.ToString() });
        if (p != null) m.Notice = Errors(p, p.ReadmeRead, p.ReadmeError, p.ReadmeDateError);
        l.Add(m);

        l.Add(new Card { Title = Language.T("common.reports"), Stats = new[] { c.Quarantine, c.Work, c.Errors }, Captions = new[] { Language.T("overview.in_quarantine"), Language.T("overview.to_work_on"), Language.T("overview.recurring_errors_2") },
            Sentence = repo.Missing ? Language.T("overview.texts_already_downloaded_stay_archive") : repo.Followed ? Language.T("overview.issues_comments_log_reports_came") : Language.T("overview.arent_following_repo_new_texts") });
        return l;
    }

    int Scene(Graphics g, bool dis) {
        int m = S(20), w = ClientSize.Width - m * 2, y = m + top;
        if (w < S(200)) return 0;
        y = repo == null ? AllScenes(g, dis, m, y, w) : RepoScenes(g, dis, m, y, w);
        return y - top;
    }

    int RepoScenes(Graphics g, bool dis, int m, int y, int w) {
        var p = repo.Overview;
        var c = accounts(repo.Name);
        List<string> chip; string title, below = null; Color point = Theme.Amber;
        if (repo.Missing) {
            title = Language.T("overview.repo_no_longer_github");
            below = Language.T("overview.deleted_renamed_no_longer_downloaded", "part", (p != null && p.ReadAt != null ? Language.T("overview.what_see_last_reading", "read_on", p.ReadAt) : Language.T("overview.didnt_read_before_disappeared_only")));
            chip = new List<string>();
        } else if (p == null) {
            title = Language.T("overview.havent_read_yet");
            below = stale ? Language.T("overview.needs_network_fills_own_soon") : Language.T("overview.reading_now_press_update_github");
            chip = new List<string>();
        } else {
            chip = WhatIsMissing(p, c);
            title = RepoTitle(p, chip.Count);
            if (chip.Count == 0 && !AllRead(p)) below = Language.T("overview.until_read_all_dont_know");
        }
        var notRead = NotRead(p).Select(x => Language.T("overview.could_not_read", "text", x)).ToList();
        y = Strip(g, dis, m, y, w, title, below, chip, notRead, chip.Count == 0 && below == null ? Theme.Lime : Theme.Text, point) + S(14);
        if (repo.Missing) y = RemoveLine(g, dis, m, y, w) + S(14);
        if (repo.Stats != null || !repo.Missing) y = Box(g, dis, m, y, w) + S(14);

        var cards = Cards();
        if (repo.Missing && p == null) cards = cards.GetRange(cards.Count - 1, 1);
        int gap = S(14), col = w >= S(700) ? 2 : 1, lc = (w - gap * (col - 1)) / col;
        for (int i = 0; i < cards.Count; i += col) {
            int h = 0;
            for (int j = i; j < i + col && j < cards.Count; j++) h = Math.Max(h, CardHeight(g, cards[j], lc));
            if (dis) for (int j = i; j < i + col && j < cards.Count; j++) DrawCard(g, cards[j], m + (j - i) * (lc + gap), y, lc, h);
            y += h + gap;
        }
        return y - gap + m;
    }

    int Strip(Graphics g, bool dis, int x, int y, int w, string title, string below, List<string> chip, List<string> gray, Color titleWith, Color point) {
        int pad = S(16), start = y, aChip = S(26), step = S(32);
        int yy = y + S(12);
        var pieces = new List<KeyValuePair<Rectangle, KeyValuePair<string, Color>>>();
        int hT = S(24);
        yy += hT;
        int hS = 0;
        if (below != null) { hS = Theme.MeasureHeight(g, below, Theme.Normal, w - pad * 2); yy += S(2) + hS; }
        int yTexts = yy - hS;
        var allEntries = chip.Select(s => new KeyValuePair<string, Color>(s, point)).Concat(gray.Select(s => new KeyValuePair<string, Color>(s, Theme.Muted))).ToList();
        if (allEntries.Count > 0) {
            yy += S(8);
            int cx = x + pad;
            foreach (var t in allEntries) {
                int lw = Theme.MeasureWidth(g, t.Key, Theme.Normal) + S(32);
                if (cx + lw > x + w - pad && cx > x + pad) { cx = x + pad; yy += step; }
                pieces.Add(new KeyValuePair<Rectangle, KeyValuePair<string, Color>>(new Rectangle(cx, yy, Math.Min(lw, w - pad * 2), aChip), t));
                cx += lw + S(8);
            }
            yy += aChip;
        }
        yy += S(14);
        if (dis) {
            Theme.Full(g, new Rectangle(x, start, w, yy - start), S(12), Theme.Background);
            Theme.T(g, title, Theme.Medium, x + pad, start + S(12), w - pad * 2, hT, titleWith);
            if (below != null) Theme.DrawTextWrapped(g, below, Theme.Normal, x + pad, yTexts, w - pad * 2, hS + S(2), Theme.Muted);
            foreach (var pz in pieces) {
                var r = pz.Key;
                Theme.Full(g, r, r.Height / 2, Theme.Capsule);
                Theme.Full(g, new Rectangle(r.X + S(11), r.Y + (r.Height - S(7)) / 2, S(7), S(7)), S(4), pz.Value.Value);
                Theme.T(g, pz.Value.Key, Theme.Normal, r.X + S(23), r.Y, r.Width - S(30), r.Height, Theme.Text);
            }
        }
        return yy;
    }

    int CardHeight(Graphics g, Card c, int w) {
        int pad = S(16), lt = w - pad * 2;
        int h = S(14) + S(20);
        if (c.Stats != null) h += S(8) + S(36) + S(20);
        else h += S(4) + S(30);
        if (c.Sentence != null) h += S(4) + Theme.MeasureHeight(g, c.Sentence, Theme.Normal, lt);
        if (c.Lines.Count > 0) h += S(8) + c.Lines.Count * S(26);
        if (c.Notice != null) h += S(8) + Theme.MeasureHeight(g, c.Notice, Theme.Small, lt);
        return h + S(14);
    }

    void DrawCard(Graphics g, Card c, int x, int y, int w, int h) {
        int pad = S(16), lt = w - pad * 2, xt = x + pad;
        Theme.Card(g, new Rectangle(x, y, w, h), S(10), Color.White, Theme.Border);
        int yy = y + S(14);
        Theme.T(g, c.Title, Theme.Strong, xt, yy, lt, S(20), Theme.Azure);
        yy += S(20);
        if (c.Stats != null) {
            yy += S(8);
            int lc = lt / c.Stats.Length;
            for (int i = 0; i < c.Stats.Length; i++) {
                bool alarm = i != 1 && c.Stats[i] > 0;
                Theme.T(g, c.Stats[i].ToString(), Theme.Title, xt + i * lc, yy, lc - S(6), S(36), c.Stats[i] == 0 ? Theme.Gray : alarm ? Theme.AmberDark : Theme.Azure);
                Theme.T(g, c.Captions[i], Theme.Small, xt + i * lc, yy + S(36), lc - S(6), S(20), Theme.Gray);
            }
            yy += S(36) + S(20);
        } else {
            yy += S(4);
            Theme.T(g, c.Large, Theme.Large, xt, yy, lt, S(30), c.LargeWith);
            yy += S(30);
        }
        if (c.Sentence != null) {
            int hf = Theme.MeasureHeight(g, c.Sentence, Theme.Normal, lt);
            yy += S(4);
            Theme.DrawTextWrapped(g, c.Sentence, Theme.Normal, xt, yy, lt, hf + S(2), Theme.Gray);
            yy += hf;
        }
        if (c.Lines.Count > 0) {
            yy += S(8);
            using (var n = new Pen(Color.FromArgb(235, 237, 241)))
                foreach (var r in c.Lines) {
                    g.DrawLine(n, xt, yy, xt + lt, yy);
                    int wv = Math.Min(Theme.MeasureWidth(g, r[1], Theme.Strong), lt * 3 / 5);
                    Theme.T(g, r[0], Theme.Normal, xt, yy, lt - wv - S(10), S(26), Theme.Gray);
                    Theme.T(g, r[1], Theme.Strong, xt + lt - wv, yy, wv + 1, S(26), r[1] == Language.T("overview.not_read_4") ? Theme.Gray : Theme.Ink);
                    yy += S(26);
                }
        }
        if (c.Notice != null) {
            int has = Theme.MeasureHeight(g, c.Notice, Theme.Small, lt);
            yy += S(8);
            Theme.DrawTextWrapped(g, c.Notice, Theme.Small, xt, yy, lt, has + S(2), Theme.AmberDark);
        }
    }

    public static string[] Columns { get { return new[] { Language.T("common.repo"), Language.T("common.who_sees"), Language.T("overview.version"), Language.T("stats.views_14_days"), Language.T("stats.downloads"), Language.T("stats.stars"), Language.T("stats.reports_read"), Language.T("overview.errors") }; } }
    public static readonly int[] Weights = { 25, 9, 9, 13, 11, 9, 13, 8 };

    int AllScenes(Graphics g, bool dis, int m, int y, int w) {
        var liveRepos = allRepoList.Where(r => !r.Missing).ToList();
        var outside = allRepoList.Where(r => r.Missing).ToList();
        int missingCount = outside.Count;
        var chip = Problems(liveRepos, allRepoList, accounts);
        string title = Language.N("overview.repos_github", liveRepos.Count) + (missingCount > 0 ? ", " + Language.N("common.gone", missingCount) : "");
        var toRead = ToRead(liveRepos);
        var gray = toRead.Concat(MissingStats(liveRepos)).ToList();
        y = Strip(g, dis, m, y, w, title, AllSentence(liveRepos.Count, chip.Count, toRead), chip, gray, Theme.Text, Theme.Amber) + S(14);

        int headerHeight = S(44), rowHeight = S(66), groupHeight = S(48), hTab = headerHeight + rowHeight * (liveRepos.Count + (missingOpen ? missingCount : 0)) + (missingCount > 0 ? groupHeight : 0);
        if (dis) {
            Theme.Card(g, new Rectangle(m, y, w, hTab), S(10), Color.White, Theme.Border);
            int pad = S(14), lu = w - pad * 2, sp = Weights.Sum();
            var xs = new int[Columns.Length + 1];
            int acc = 0;
            for (int i = 0; i < Columns.Length; i++) { xs[i] = m + pad + acc * lu / sp; acc += Weights[i]; }
            xs[Columns.Length] = m + pad + lu;
            for (int i = 0; i < Columns.Length; i++)
                TextRenderer.DrawText(g, Columns[i], Theme.SmallF, new Rectangle(xs[i], y, xs[i + 1] - xs[i] - S(8), headerHeight), Theme.Gray,
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.TextBoxControl);
            int yy = y + headerHeight;
            using (var n = new Pen(Color.FromArgb(235, 237, 241))) {
                Action<RepoInfo> line = r => {
                    var zone = new Rectangle(m + 1, yy, w - 2, rowHeight);
                    lines.Add(new KeyValuePair<Rectangle, RepoInfo>(zone, r));
                    if (r == above) using (var b = new SolidBrush(Theme.Backdrop)) g.FillRectangle(b, zone);
                    g.DrawLine(n, m + 1, yy, m + w - 2, yy);
                    TableLine(g, r, xs, yy, rowHeight);
                    yy += rowHeight;
                };
                foreach (var r in liveRepos) line(r);
                if (missingCount > 0) {
                    var zone = new Rectangle(m + 1, yy, w - 2, groupHeight);
                    buttons.Add(new KeyValuePair<Rectangle, HitTarget>(zone, HitTarget.Group));
                    if (buttonAbove == HitTarget.Group) using (var b = new SolidBrush(Theme.Backdrop)) g.FillRectangle(b, zone);
                    g.DrawLine(n, m + 1, yy, m + w - 2, yy);
                    Arrow(g, xs[0], yy + groupHeight / 2, missingOpen, Theme.Gray);
                    Theme.T(g, Language.T("missing.gone", "n", Language.Number(missingCount)), Theme.Strong, xs[0] + S(18), yy, S(240), groupHeight, Theme.Gray);
                    DrawnButton(g, Language.T("missing.remove_all"), m + w - pad, yy + (groupHeight - S(30)) / 2, S(30), HitTarget.RemoveAll, true);
                    yy += groupHeight;
                    if (missingOpen) foreach (var r in outside) line(r);
                }
            }
        }
        return y + hTab + S(20);
    }

    public static List<string> Problems(List<RepoInfo> liveRepos, List<RepoInfo> allRepos, Func<string, Counts> howMany) {
        Func<RepoInfo, bool> withoutRelease = r => r.Overview != null && r.Overview.ReleaseRead && string.IsNullOrEmpty(r.Overview.ReleaseTag);
        Func<RepoInfo, bool> withAfter = r => r.Overview != null && r.Overview.ReleaseRead && !string.IsNullOrEmpty(r.Overview.ReleaseTag) && r.Overview.CommitsSinceRelease > 0;
        Func<RepoInfo, bool> withoutLicense = r => r.Overview != null && r.Overview.LicenseRead && string.IsNullOrEmpty(r.Overview.License);
        Func<RepoInfo, bool> withoutReadme = r => r.Overview != null && r.Overview.ReadmeRead && !r.Overview.Readme;
        var chip = new List<string>();
        Action<int, string> count = (n, key) => { if (n > 0) chip.Add(Language.N(key, n)); };
        count(liveRepos.Count(withoutRelease), "overview.repos_without_release");
        count(liveRepos.Count(withAfter), "overview.repos_commits_after_release");
        count(liveRepos.Count(withoutLicense), "overview.repos_without_license");
        count(liveRepos.Count(withoutReadme), "overview.repos_without_main_page");
        count(allRepos.Sum(r => Math.Max(0, howMany(r.Name).Handovers)), "overview.handoffs_waiting_release");
        count(allRepos.Sum(r => howMany(r.Name).Quarantine), "overview.texts_quarantine");
        count(allRepos.Sum(r => howMany(r.Name).Errors), "overview.recurring_errors");
        return chip;
    }

    public static List<string> MissingStats(IEnumerable<RepoInfo> liveRepos) {
        int n = liveRepos.Count(r => r.Stats != null && (!r.Stats.ViewsRead || !r.Stats.DownloadsRead));
        return n > 0 ? new List<string> { Language.N("stats.numbers_not_available_repos", n) } : new List<string>();
    }

    public static Cell NumberCell(RepoStats n, bool wasRead, string error, long val) {
        if (n == null) return new Cell { Text = Language.T("overview.not_read_4"), Color = Theme.Gray };
        if (!wasRead) return new Cell { Text = error != null ? Language.T("stats.not_available") : Language.T("overview.not_read_4"), Color = Theme.Gray };
        return new Cell { Text = Language.Number(val), Color = error != null ? Theme.AmberDark : Theme.Ink };
    }

    void TableLine(Graphics g, RepoInfo r, int[] xs, int y, int h) {
        var p = r.Overview; var c = accounts(r.Name); var d = r.Stats;
        Action<int, string, Color> cell = (i, s, col) => Theme.T(g, s, Theme.Normal, xs[i], y, xs[i + 1] - xs[i] - S(8), h, col);
        Color ink = Theme.Ink, gr = Theme.Gray, am = Theme.AmberDark;
        int wn = xs[1] - xs[0] - S(8);
        Theme.T(g, ShortOf(r, login), Theme.Strong, xs[0], y + S(7), wn, S(20), r.Missing ? gr : ink);
        Theme.T(g, OwnerOf(r, login), Theme.Small, xs[0], y + S(27), wn, S(17), Theme.Azure);
        string below = r.Missing ? Language.T("common.no_longer_github") : !r.Followed ? Language.T("common.not_followed") : p == null || !p.CommitRead ? "" : string.IsNullOrEmpty(p.LatestCommit) ? Language.T("overview.empty_no_commits") : Language.T("overview.last_commit", "last_commit", Data(p.LatestCommit));
        Theme.T(g, below, Theme.Small, xs[0], y + S(45), wn, S(17), r.Missing ? am : gr);
        Theme.DrawPill(g, r.Private ? Language.T("common.private") : Language.T("common.public"), Theme.SmallF, xs[1], y + (h - S(20)) / 2, S(20), r.Private ? Theme.Backdrop : Theme.GreenTint, r.Private ? gr : Theme.GreenDark);
        if (r.Missing && p == null) cell(2, "-", gr);
        else {
            bool release = p != null && p.ReleaseRead;
            cell(2, !release ? Language.T("overview.not_read_2") : string.IsNullOrEmpty(p.ReleaseTag) ? Language.T("common.none") : V(p.ReleaseTag), !release ? gr : string.IsNullOrEmpty(p.ReleaseTag) ? am : ink);
        }
        if (r.Missing && d == null) { cell(3, "-", gr); cell(4, "-", gr); cell(5, "-", gr); }
        else {
            Action<int, Cell> num = (i, x) => cell(i, x.Text, x.Color);
            num(3, d == null ? NumberCell(null, false, null, 0) : NumberCell(d, d.ViewsRead, d.ViewsError, d.Views));
            num(4, d == null ? NumberCell(null, false, null, 0) : NumberCell(d, d.DownloadsRead, d.DownloadsError, d.Downloads));
            num(5, d == null ? NumberCell(null, false, null, 0) : NumberCell(d, d.RepoRead, d.RepoError, d.Stars));
        }
        int toReadCount = c.Quarantine + c.Work;
        cell(6, Language.Number(toReadCount), c.Quarantine > 0 ? am : toReadCount > 0 ? Theme.Azure : gr);
        cell(7, Language.Number(c.Errors), c.Errors > 0 ? am : gr);
    }

    static void Arrow(Graphics g, int x, int cy, bool isOpen, Color c) {
        var points = isOpen ? new[] { new Point(x, cy - S(3)), new Point(x + S(10), cy - S(3)), new Point(x + S(5), cy + S(3)) }
                           : new[] { new Point(x + S(2), cy - S(5)), new Point(x + S(8), cy), new Point(x + S(2), cy + S(5)) };
        var v = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(c)) g.FillPolygon(b, points);
        g.SmoothingMode = v;
    }

    Rectangle DrawnButton(Graphics g, string text, int x, int y, int alt, HitTarget t, bool alignRight) {
        int w = Theme.MeasureWidth(g, text, Theme.Strong) + S(26);
        var r = new Rectangle(alignRight ? x - w : x, y, w, alt);
        Theme.Card(g, r, S(7), buttonAbove == t ? Theme.Backdrop : Color.White, Theme.Border);
        TextRenderer.DrawText(g, text, Theme.Strong, r, Theme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        buttons.Add(new KeyValuePair<Rectangle, HitTarget>(r, t));
        return r;
    }

    int RemoveLine(Graphics g, bool dis, int m, int y, int w) {
        string text = Language.T("missing.remove_list"), sentence = Language.T("missing.github_no_longer_lists_remove");
        int alt = S(34), lt = Theme.MeasureWidth(g, text, Theme.Strong) + S(26), lf = w - lt - S(14);
        int hf = Theme.MeasureHeight(g, sentence, Theme.Normal, lf), h = Math.Max(alt, hf);
        if (dis) {
            DrawnButton(g, text, m, y + (h - alt) / 2, alt, HitTarget.RemoveThis, false);
            Theme.DrawTextWrapped(g, sentence, Theme.Normal, m + lt + S(14), y + (h - hf) / 2, lf, hf + S(2), Theme.Gray);
        }
        return y + h;
    }

    public static string StatsSentence(RepoStats n, int toRead) {
        string visits = n != null && n.ViewsRead ? Language.N("stats.views_3", n.Views) + " " + Language.N("stats.from_people", n.UniqueViews) : Language.T("stats.views_not_available");
        string download = n != null && n.DownloadsRead ? Language.N("stats.downloads_3", n.Downloads) : Language.T("stats.downloads_not_available");
        return Language.T("stats.last_14_days_all_time", "visits", visits, "download", download, "issues", Language.N("stats.reports_read_2", toRead));
    }

    public static List<string> StatsNotices(RepoStats n) {
        var output = new List<string>();
        if (n == null) return output;
        var parts = new[] {
            new { What = Language.T("stats.views_2"), Read = n.ViewsRead, Error = n.ViewsError },
            new { What = Language.T("stats.clones_2"), Read = n.ClonesRead, Error = n.ClonesError },
            new { What = Language.T("stats.sources"), Read = n.ReferrersRead, Error = n.ReferrersError },
            new { What = Language.T("stats.viewed_pages"), Read = n.PagesRead, Error = n.PagesError },
            new { What = Language.T("stats.downloads_4"), Read = n.DownloadsRead, Error = n.DownloadsError },
            new { What = Language.T("stats.stars_forks"), Read = n.RepoRead, Error = n.RepoError } };
        foreach (var groupRow in parts.Where(p => p.Error != null).GroupBy(p => new { p.Error, p.Read })) {
            string what = string.Join(", ", groupRow.Select(p => p.What));
            what = char.ToUpper(what[0], CultureInfo.CurrentCulture) + what.Substring(1);
            output.Add(Language.T(groupRow.Key.Read ? "stats.not_read_again_earlier_figures" : "stats.not_available_2", "what", what, "reason", groupRow.Key.Error.TrimEnd('.', ' ')));
        }
        return output;
    }

    public static List<string> News(RepoStats n, Counts c, string today) {
        var l = new List<string>();
        if (c.NewReports > 0) l.Add(Language.N("stats.new_reports_last_14_days", c.NewReports));
        if (c.NewComments > 0) l.Add(Language.N("stats.new_comments_last_14_days", c.NewComments));
        if (n == null) return l;
        Func<string, bool> recent = g => {
            DateTime d, o;
            return g != null && DateTime.TryParseExact(g, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                && DateTime.TryParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out o) && (o - d).TotalDays <= 14;
        };
        if (n.StarsBefore >= 0 && recent(n.StarsChanged)) l.Add(Language.T("stats.stars_from_to_on", "from", Language.Number(n.StarsBefore), "to", Language.Number(n.Stars), "date", ShortDay(n.StarsChanged)));
        if (n.ForksBefore >= 0 && recent(n.ForksChanged)) l.Add(Language.T("stats.forks_from_to_on", "from", Language.Number(n.ForksBefore), "to", Language.Number(n.Forks), "date", ShortDay(n.ForksChanged)));
        return l;
    }

    static string ShortDay(string day) {
        DateTime d;
        return DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("dd/MM") : "";
    }

    int Box(Graphics g, bool dis, int x, int y, int w) {
        int end = Content(g, false, x, y, w);
        if (!dis) return end;
        Theme.Card(g, new Rectangle(x, y, w, end - y), S(10), Color.White, Theme.Border);
        return Content(g, true, x, y, w);
    }

    int Content(Graphics g, bool dis, int x, int y, int w) {
        var n = repo.Stats;
        var c = accounts(repo.Name);
        int pad = S(16), lt = w - pad * 2, xt = x + pad, yy = y + S(14);
        string when = n != null && n.ReadAt != null ? Language.T("stats.read_on", "date", n.ReadAt) : "";
        if (dis) {
            Theme.T(g, Language.T("stats.how_going"), Theme.Strong, xt, yy, lt - Theme.MeasureWidth(g, when, Theme.Small) - S(10), S(20), Theme.Azure);
            if (when.Length > 0) Theme.DrawTextRight(g, when, Theme.Small, xt + lt, yy, S(20), Theme.Gray);
        }
        yy += S(24);
        if (n == null) {
            string again = Language.T("stats.not_read_yet_arrive_next");
            int has = Theme.MeasureHeight(g, again, Theme.Normal, lt);
            if (dis) Theme.DrawTextWrapped(g, again, Theme.Normal, xt, yy, lt, has + S(2), Theme.Gray);
            return yy + has + S(14);
        }
        string sentence = StatsSentence(n, c.Quarantine + c.Work);
        int hf = Theme.MeasureHeight(g, sentence, Theme.Medium, lt);
        if (dis) Theme.DrawTextWrapped(g, sentence, Theme.Medium, xt, yy, lt, hf + S(2), Theme.Ink);
        yy += hf + S(12);

        var conte = new[] {
            new { What = Language.T("stats.views"), Read = n.ViewsRead, Error = n.ViewsError, HowMany = n.Views },
            new { What = Language.T("stats.people"), Read = n.ViewsRead, Error = n.ViewsError, HowMany = n.UniqueViews },
            new { What = Language.T("stats.clones"), Read = n.ClonesRead, Error = n.ClonesError, HowMany = n.Clones },
            new { What = Language.T("stats.downloads_2"), Read = n.DownloadsRead, Error = n.DownloadsError, HowMany = n.Downloads },
            new { What = Language.T("stats.stars_2"), Read = n.RepoRead, Error = n.RepoError, HowMany = n.Stars },
            new { What = Language.T("stats.forks"), Read = n.RepoRead, Error = n.RepoError, HowMany = n.Forks },
            new { What = Language.T("stats.watchers"), Read = n.RepoRead, Error = n.RepoError, HowMany = n.Watchers } };
        int lc = lt / conte.Length;
        if (dis) for (int i = 0; i < conte.Length; i++) {
            var k = conte[i];
            Theme.T(g, k.Read ? Language.Number(k.HowMany) : "-", Theme.Large, xt + i * lc, yy, lc - S(6), S(30), !k.Read ? Theme.Gray : k.Error != null ? Theme.AmberDark : Theme.Ink);
            Theme.T(g, k.What, Theme.Small, xt + i * lc, yy + S(30), lc - S(6), S(18), Theme.Gray);
        }
        yy += S(48) + S(10);

        foreach (var av in StatsNotices(n)) {
            int has = Theme.MeasureHeight(g, av, Theme.Small, lt);
            if (dis) Theme.DrawTextWrapped(g, av, Theme.Small, xt, yy, lt, has + S(2), Theme.AmberDark);
            yy += has + S(6);
        }
        yy += S(6);

        yy = SideBySide(xt, yy, lt, (px, py, pw) => DaysBlock(g, dis, px, py, pw, n), (px, py, pw) => VersionsBlock(g, dis, px, py, pw, n)) + S(14);
        yy = SideBySide(xt, yy, lt,
            (px, py, pw) => EntriesBlock(g, dis, px, py, pw, Language.T("stats.where_people_come"), n.ReferrersRead, n.Referrers, null),
            (px, py, pw) => EntriesBlock(g, dis, px, py, pw, Language.T("stats.most_viewed_pages"), n.PagesRead, n.Pages, repo.Name)) + S(14);

        yy = Section(g, dis, xt, yy, lt, Language.T("stats.worth_look"));
        var newOnes = News(n, c, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        if (!repo.Followed) newOnes.Add(Language.T("stats.arent_following_repo_new_reports"));
        if (newOnes.Count == 0) newOnes.Add(Language.T("stats.nothing_new_last_14_days"));
        foreach (var r in newOnes) {
            int has = Theme.MeasureHeight(g, r, Theme.Normal, lt);
            if (dis) Theme.DrawTextWrapped(g, r, Theme.Normal, xt, yy, lt, has + S(2), Theme.Ink);
            yy += has + S(4);
        }
        yy += S(10);

        string limits = Language.T("stats.github_says_how_many_people");
        int hl = Theme.MeasureHeight(g, limits, Theme.Small, lt);
        if (dis) Theme.DrawTextWrapped(g, limits, Theme.Small, xt, yy, lt, hl + S(2), Theme.Gray);
        return yy + hl + S(14);
    }

    int SideBySide(int x, int y, int w, Func<int, int, int, int> a, Func<int, int, int, int> b) {
        int gap = S(24), col = w >= S(640) ? 2 : 1, lc = (w - gap * (col - 1)) / col;
        int f1 = a(x, y, lc);
        int f2 = col == 2 ? b(x + lc + gap, y, lc) : b(x, f1 + S(12), lc);
        return Math.Max(f1, f2);
    }

    int Section(Graphics g, bool dis, int x, int y, int w, string title) {
        if (dis) Theme.T(g, title, Theme.SmallF, x, y, w, S(18), Theme.Gray);
        return y + S(22);
    }

    int EntryLine(Graphics g, bool dis, int x, int y, int w, string name, string val) {
        if (dis) {
            using (var n = new Pen(Color.FromArgb(235, 237, 241))) g.DrawLine(n, x, y, x + w, y);
            int wv = Math.Min(Theme.MeasureWidth(g, val, Theme.Strong), w * 3 / 5);
            Theme.T(g, name, Theme.Normal, x, y, w - wv - S(10), S(24), Theme.Ink);
            Theme.T(g, val, Theme.Strong, x + w - wv, y, wv + 1, S(24), Theme.Ink);
        }
        return y + S(24);
    }

    int ShortSentence(Graphics g, bool dis, int x, int y, int w, string text) {
        int h = Theme.MeasureHeight(g, text, Theme.Normal, w);
        if (dis) Theme.DrawTextWrapped(g, text, Theme.Normal, x, y, w, h + S(2), Theme.Gray);
        return y + h;
    }

    int DaysBlock(Graphics g, bool dis, int x, int y, int w, RepoStats n) {
        var visits = days.Where(d => d.Views >= 0).Select(d => new TrafficDay { Day = d.Day, Count = d.Views }).ToList();
        if (visits.Count == 0 && n.ViewsRead && n.ViewDays != null) visits = n.ViewDays;
        y = Section(g, dis, x, y, w, Language.T("stats.views_day_day"));
        if (visits.Count == 0) y = ShortSentence(g, dis, x, y, w, n.ViewsRead ? Language.T("stats.nothing_yet") : Language.T("stats.not_available"));
        else y = Bars(g, dis, x, y, w, visits, v => Language.N("stats.views_3", v));
        y += S(12);
        y = Section(g, dis, x, y, w, Language.T("stats.downloads_day_day"));
        var downloaded = History.DownloadForDay(days);
        if (downloaded.Count >= 2) return Bars(g, dis, x, y, w, downloaded, v => Language.N("stats.downloads_3", v));
        return ShortSentence(g, dis, x, y, w, Language.T("stats.github_only_gives_total_daily"));
    }

    int Bars(Graphics g, bool dis, int x, int y, int w, List<TrafficDay> data, Func<long, string> says) {
        int alt = S(58);
        if (!dis) return y + alt + S(20);
        var forDay = new Dictionary<string, long>();
        foreach (var d in data) forDay[d.Day] = d.Count;
        var date = data.Select(d => DateTime.ParseExact(d.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture)).OrderBy(d => d).ToList();
        DateTime lastDay = date[date.Count - 1], first = date[0];
        if ((lastDay - first).TotalDays > 29) first = lastDay.AddDays(-29);
        int howMany = (int)(lastDay - first).TotalDays + 1;
        long max = Math.Max(1, forDay.Where(kv => string.CompareOrdinal(kv.Key, first.ToString("yyyy-MM-dd")) >= 0).Select(kv => kv.Value).DefaultIfEmpty(0).Max());
        int step = Math.Max(S(4), Math.Min(S(26), w / howMany)), space = S(2), wide = Math.Max(S(2), step - space), baseY = y + alt;
        for (int i = 0; i < howMany; i++) {
            string day = first.AddDays(i).ToString("yyyy-MM-dd");
            long v;
            if (!forDay.TryGetValue(day, out v)) continue;
            int bx = x + i * step;
            bars.Add(new KeyValuePair<Rectangle, string>(new Rectangle(bx, y, step, alt), first.AddDays(i).ToString("dd/MM") + ": " + says(v)));
            if (v == 0) continue;
            int h = Math.Max(S(3), (int)(v * (alt - S(2)) / max));
            Theme.Full(g, new Rectangle(bx, baseY - h, wide, h), Math.Min(S(3), wide / 2), Theme.Azure);
            using (var b = new SolidBrush(Theme.Azure)) g.FillRectangle(b, bx, baseY - Math.Min(h, S(3)), wide, Math.Min(h, S(3)));
        }
        int plotEnd = x + howMany * step;
        using (var n = new Pen(Theme.Border)) g.DrawLine(n, x, baseY, plotEnd, baseY);
        Theme.T(g, first.ToString("dd/MM"), Theme.Small, x, baseY + S(3), S(50), S(16), Theme.Gray);
        Theme.DrawTextRight(g, lastDay.ToString("dd/MM"), Theme.Small, plotEnd, baseY + S(3), S(16), Theme.Gray);
        string mx = Language.T("stats.peak", "n", Language.Number(max));
        int lm = Theme.MeasureWidth(g, mx, Theme.Small);
        Theme.T(g, mx, Theme.Small, x + (plotEnd - x - lm) / 2, baseY + S(3), lm + 1, S(16), Theme.Gray);
        return baseY + S(20);
    }

    int VersionsBlock(Graphics g, bool dis, int x, int y, int w, RepoStats n) {
        y = Section(g, dis, x, y, w, Language.T("stats.downloads_version"));
        if (!n.DownloadsRead) return ShortSentence(g, dis, x, y, w, Language.T("stats.not_available"));
        if (n.Releases == null || n.Releases.Count == 0) return ShortSentence(g, dis, x, y, w, Language.T("stats.no_published_versions"));
        foreach (var v in n.Releases.Take(6)) {
            string name = V(v.Tag) + (v.Assets.Count == 1 ? " · " + V(v.Assets[0].Name) : v.Assets.Count > 1 ? " · " + Language.N("publish.files", v.Assets.Count) : "");
            y = EntryLine(g, dis, x, y, w, name, Language.Number(v.Downloads));
        }
        if (n.Releases.Count > 6) y = ShortSentence(g, dis, x, y + S(4), w, Language.N("stats.more_versions", n.Releases.Count - 6));
        return y;
    }

    int EntriesBlock(Graphics g, bool dis, int x, int y, int w, string title, bool readFlag, List<TrafficEntry> entries, string removeButton) {
        y = Section(g, dis, x, y, w, title);
        if (!readFlag) return ShortSentence(g, dis, x, y, w, Language.T("stats.not_available"));
        if (entries == null || entries.Count == 0) return ShortSentence(g, dis, x, y, w, Language.T("stats.nothing_yet"));
        foreach (var v in entries.Take(5)) {
            string name = v.Name ?? "";
            if (removeButton != null && name.StartsWith("/" + removeButton, StringComparison.OrdinalIgnoreCase)) name = name.Substring(removeButton.Length + 1);
            if (removeButton != null && name.Trim('/').Length == 0) name = Language.T("stats.main_page");
            y = EntryLine(g, dis, x, y, w, V(name), Language.N("stats.views_3", v.Count) + ", " + Language.N("stats.people_2", v.Uniques));
        }
        return y;
    }
}
