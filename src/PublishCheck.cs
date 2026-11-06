using System.Collections.Generic;
using System.Linq;

public class PublishCheck {
    public long RepoId;
    public string Sha, Incomplete;
    public int Scanned, Branches, Commit, HistoryScanned, Issue, Pull, Release, Comments, Assets, PlainEmail;
    public bool Wiki;
    public List<string> NotScanned = new List<string>();
    public List<Finding> Findings = new List<Finding>();
    public List<Finding> Notices = new List<Finding>();
    public bool Clean { get { return Incomplete == null && Findings.Count == 0 && NotScanned.Count == 0; } }
}
