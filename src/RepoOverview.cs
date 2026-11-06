using System.Collections.Generic;

public class RepoOverview {
    public string ReadAt;
    public bool ReleaseRead, TagRead, CommitRead, LicenseRead, ReadmeRead;
    public string ReleaseTag, ReleaseDate, ReleaseError;
    public string LatestTag, TagError;
    public int CommitsSinceRelease = -1;
    public string CommitsSinceReleaseError;
    public string LatestCommit, CommitError;
    public List<Collaborator> Collaborators;
    public string CollaboratorsError;
    public string License, LicenseError;
    public bool OtherLicense;
    public bool Readme, ReadmeCopyright;
    public string ReadmeDate, ReadmeError, ReadmeDateError;

    public RepoOverview Copy() { return (RepoOverview)MemberwiseClone(); }
}
