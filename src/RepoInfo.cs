using System.Collections.Generic;

public class RepoInfo {
    public string Name, Description, Owner;
    public bool Private, Followed;
    public bool Mine;
    public bool Organization;
    public string Since;
    public bool Missing;
    public string Branch;
    public RepoOverview Overview;
    public List<string> Accounts;
    public RepoStats Stats;
}
