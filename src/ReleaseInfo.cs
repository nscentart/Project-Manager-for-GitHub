using System.Collections.Generic;

public class ReleaseInfo {
    public long Id;
    public string Tag, Title, Note, Branch, Data;
    public bool Draft, Preview;
    public List<AssetInfo> Assets = new List<AssetInfo>();
}
