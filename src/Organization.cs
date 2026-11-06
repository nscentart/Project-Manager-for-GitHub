using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static partial class Organization {
    public static bool Equal(string a, string b) { return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase); }

    public static bool Mine(string owner, string login, bool admin) {
        return admin || (!string.IsNullOrEmpty(login) && Equal(owner, login));
    }

    public static List<Collaborator> Other(IEnumerable<Collaborator> all, RepoInfo r, string login) {
        bool org = r.Organization || (r.Mine && !Equal(r.Owner, login));
        return all.Where(x => !Equal(x.Name, login) && !(org && Equal(x.Name, r.Owner))).ToList();
    }

    public static string Short(RepoInfo r, string login) {
        string prefix = (r.Owner ?? "") + "/";
        bool isMine = r.Mine || Equal(r.Owner, login);
        return isMine && r.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? r.Name.Substring(prefix.Length) : r.Name;
    }
}
