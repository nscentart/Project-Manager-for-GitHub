using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public partial class OverviewPage {
    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        Func<RepoOverview> read = () => new RepoOverview { ReleaseRead = true, LicenseRead = true, ReadmeRead = true };
        Func<RepoOverview, RepoInfo> repo = p => new RepoInfo { Name = "pippo/x", Overview = p };
        Func<RepoInfo[], int, string> sentence = (r, chip) => AllSentence(r.Length, chip, ToRead(r));
        const string All = "Nothing is missing.";

        var never = new[] { repo(null), repo(null) };
        v("no repo read: does not say everything is fine", sentence(never, 0) != All && sentence(never, 0) != null);
        v("no repo read: says how many are missing", ToRead(never).SequenceEqual(new[] { "2 repos not read yet" }));
        var one = new[] { repo(read()), repo(null) };
        v("one read and one not: does not say everything is fine", sentence(one, 0) != All);
        v("one read and one not: says which count", ToRead(one).SequenceEqual(new[] { "1 repo not read yet" }));
        var withoutLicense = read(); withoutLicense.LicenseRead = false; withoutLicense.LicenseError = "fake error";
        var meta = new[] { repo(withoutLicense) };
        v("license not read: does not say everything is fine", sentence(meta, 0) != All);
        v("license not read: it is a partly read repo", ToRead(meta).SequenceEqual(new[] { "1 repo partly read" }));
        var withoutRelease = read(); withoutRelease.ReleaseRead = false;
        var withoutReadme = read(); withoutReadme.ReadmeRead = false;
        v("release not read: partly read, even without a written error", !AllRead(withoutRelease) && sentence(new[] { repo(withoutRelease) }, 0) != All);
        v("main page not read: partly read", !AllRead(withoutReadme) && sentence(new[] { repo(withoutReadme) }, 0) != All);
        v("two partly read and one never: counts both entries", ToRead(new[] { repo(withoutRelease), repo(withoutReadme), repo(null) }).SequenceEqual(new[] { "1 repo not read yet", "2 repos partly read" }));
        var allRepos = new[] { repo(read()), repo(read()) };
        v("all read and nothing to fix: nothing is missing", sentence(allRepos, 0) == All && ToRead(allRepos).Count == 0);
        var noWith = read(); noWith.CollaboratorsError = "403";
        v("collaborators denied: does not count, GitHub gives them only to whoever administers", sentence(new[] { repo(noWith) }, 0) == All);
        v("with something to fix: the sentence is not needed", sentence(allRepos, 2) == null && sentence(one, 2) == null);
        v("no repo: does not say everything is fine", sentence(new RepoInfo[0], 0) != All);
        v("the card of a repo: all read, nothing is missing", RepoTitle(read(), 0) == "Nothing is missing");
        v("the card of a repo: partly read, does not say so", RepoTitle(withoutLicense, 0) != "Nothing is missing");
        v("the card of a repo: partly read with something to fix", RepoTitle(withoutLicense, 1) == "What's missing");
        Func<string, string, bool, bool, RepoInfo> of = (name, prop, org, mine) => new RepoInfo { Name = name, Owner = prop, Organization = org, Mine = mine };
        v("table: the personal repo says 'personal' and the owner", OwnerOf(of("pippo/x", "pippo", false, true), "pippo") == "personal · pippo");
        v("table: the repo of an organization says 'organization' and its name", OwnerOf(of("company/app", "company", true, true), "pippo") == "organization · company");
        v("table: an organization you are not an administrator of is always an organization", OwnerOf(of("company/app", "company", true, false), "pippo") == "organization · company");
        v("table: the repo of another person says 'others'", OwnerOf(of("tizio/x", "tizio", false, false), "pippo") == "others' · tizio");
        v("table: same capitals in the login, stays personal", OwnerOf(of("Pippo/x", "Pippo", false, true), "pippo") == "personal · Pippo");
        v("table: old archive, yours but of another owner, is an organization", OwnerOf(of("company/app", "company", false, true), "pippo") == "organization · company");
        v("table: without a written owner it takes it from the name", OwnerOf(of("tizio/x", null, false, false), "pippo") == "others' · tizio");
        v("table: the short name stays as before", ShortOf(of("company/app", "company", true, true), "pippo") == "app");
        v("table: in the owner the invisible characters are shown", OwnerOf(of("a/x", "a​b", false, false), "pippo").Contains("{U+200B}"));
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }
}
