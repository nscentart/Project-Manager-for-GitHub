using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static partial class Organization {
    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        Func<string, string, bool, bool, RepoInfo> repo = (name, prop, org, mine) => new RepoInfo { Name = name, Owner = prop, Organization = org, Mine = mine };
        Func<string[], List<Collaborator>> col = n => n.Select(x => new Collaborator { Name = x, Role = "admin" }).ToList();

        v("followed: owner login", Mine("pippo", "pippo", false));
        v("followed: organization, real admin", Mine("company", "pippo", true));
        v("not followed: organization, false admin", !Mine("company", "pippo", false));
        v("not followed: repo of another user", !Mine("tizio", "pippo", false));
        v("same capitals in the login", Mine("Pippo", "pippo", false));
        v("empty login not followed", !Mine("company", "", false));

        var o = repo("company/app", "company", true, true);
        v("short: your organization", Short(o, "pippo") == "app");
        v("short: repo of another, not yours", Short(repo("tizio/x", "tizio", false, false), "pippo") == "tizio/x");
        v("short: old archive, owner = login", Short(repo("pippo/y", "pippo", false, false), "pippo") == "y");
        v("short: after a login change the old archive stays valid", Short(repo("company/app", "company", false, true), "pippo") == "app");
        v("short: an organization that is not yours stays whole", Short(repo("company/app", "company", true, false), "pippo") == "company/app");

        v("others: only you in the organization", Other(col(new[] { "pippo" }), o, "pippo").Count == 0);
        v("others: the listed organization is not a person", Other(col(new[] { "pippo", "company" }), o, "pippo").Count == 0);
        v("others: a colleague counts", Other(col(new[] { "pippo", "caio" }), o, "pippo").Select(x => x.Name).SequenceEqual(new[] { "caio" }));
        v("others: repo of another, the owner counts", Other(col(new[] { "pippo", "tizio" }), repo("tizio/x", "tizio", false, false), "pippo").Count == 1);
        v("others: login that differs only by capitals", Other(col(new[] { "Pippo" }), o, "pippo").Count == 0);

        sb.AppendLine(tot - badly + " of " + tot + " passed");
        System.IO.File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }
}
