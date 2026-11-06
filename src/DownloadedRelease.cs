using System.Collections.Generic;

public class DownloadedRelease {
    public string Tag;
    public long Downloads;
    public List<DownloadedAsset> Assets = new List<DownloadedAsset>();
}
