using System.Collections.Generic;

public class RepoStats {
    public string ReadAt;
    public bool ViewsRead, ClonesRead, ReferrersRead, PagesRead, DownloadsRead, RepoRead;
    public string ViewsError, ClonesError, ReferrersError, PagesError, DownloadsError, RepoError;
    public long Views, UniqueViews, Clones, UniqueClones;
    public List<TrafficDay> ViewDays, CloneDays;
    public List<TrafficEntry> Referrers, Pages;
    public long Downloads;
    public List<DownloadedRelease> Releases;
    public long Stars, Forks, Watchers;
    public long StarsBefore = -1, ForksBefore = -1;
    public string StarsChanged, ForksChanged;

    public RepoStats Copy() { return (RepoStats)MemberwiseClone(); }
}
