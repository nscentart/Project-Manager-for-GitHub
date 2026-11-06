public class RepoFacts {
    public long Id, ByteSize;
    public string Name, Owner, Description, Branch, Last, Website;
    public string ShowNote;
    public bool Private, Archived, Organization, Admin, Maintain, Push, Triage, Pull, Wiki;
    public int Stars, Forks, Reports;
    public string ShortName { get { return Name.Substring(Name.IndexOf('/') + 1); } }
}
