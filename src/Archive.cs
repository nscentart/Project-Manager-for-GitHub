using System.Collections.Generic;

public class Archive {
    public string Login, LastUpdate, Model;
    public string Filter;
    public string ReposFolder;
    public List<GitHubAccount> Accounts = new List<GitHubAccount>();
    public List<RepoInfo> Repo = new List<RepoInfo>();
    public List<WorkItem> Items = new List<WorkItem>();
    public List<Decision> Decisions = new List<Decision>();
    public List<string> TrustedDomains = new List<string>();
    public bool UpdateNoticeOff;
    public string SkippedVersion;
}
