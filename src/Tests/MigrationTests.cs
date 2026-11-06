using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class MigrationTests {
    const string OldArchive = "{'Login':'mario','UltimoAggiornamento':'2026-11-01T10:00:00Z','Modello':'model-a','Filtro':'personali','CartellaRepo':'REPOS',"
        + "'Conti':[{'Login':'mario','Origine':'vecchio'},{'Login':'luisa','Origine':'git'},{'Login':'anna','Origine':'browser'}],"
        + "'Repo':[{'Nome':'mario/a','Proprietario':'mario','Seguita':true,'Privata':true,'Mia':true,'Dal':'2026-01-01','Ramo':'main','Conti':['mario'],"
        + "'Pannello':{'LettoIl':'2026-10-30T10:00:00Z','ReleaseLetta':true,'UltimoTag':'v1.0','Collaboratori':[{'Nome':'luisa','Ruolo':'write'}]},"
        + "'Numeri':{'LettoIl':'2026-10-30T10:00:00Z','VisiteLette':true,'Visite':12,'VisiteUnici':7,'VisiteGiorni':[{'Giorno':'2026-10-29','Conta':4,'Unici':2}],'Download':3}},"
        + "{'Nome':'luisa/b','Proprietario':'luisa','Seguita':false,'Sparita':true}],"
        + "'Elementi':[{'Chiave':'issue:1','Repo':'mario/a','Numero':1,'Tipo':'issue','Titolo':'First','Autore':'luisa','Testo':'body one','Etichette':'bug','Url':'https://github.com/mario/a/issues/1','Creato':'2026-10-01T10:00:00Z','Aggiornato':'2026-10-02T10:00:00Z','Aperta':'open'},"
        + "{'Chiave':'commento:9','Repo':'mario/a','Numero':1,'Tipo':'commento','Titolo':'','Autore':'anna','Testo':'a comment','Etichette':'','Url':'https://github.com/mario/a/issues/1#issuecomment-9','Creato':'2026-10-03T10:00:00Z','Aggiornato':'2026-10-03T10:00:00Z','Aperta':'open'},"
        + "{'Chiave':'pull:2','Repo':'mario/a','Numero':2,'Tipo':'pull','Titolo':'Second','Autore':'luisa','Testo':'body two','Etichette':'','Url':'https://github.com/mario/a/pull/2','Creato':'2026-10-04T10:00:00Z','Aggiornato':'2026-10-04T10:00:00Z','Aperta':'closed'}],"
        + "'Decisioni':[{'Chiave':'issue:1','Impronta':'aaaa','Stato':'approvato','Quando':'2026-10-05','Riassunto':'sum','RiassuntoImpronta':'aaaa','RiassuntoModello':'m1'},"
        + "{'Chiave':'commento:9','Impronta':'bbbb','Stato':'quarantena','Quando':'2026-10-05'},"
        + "{'Chiave':'pull:2','Impronta':'cccc','Stato':'rifiutato','Quando':'2026-10-05'},"
        + "{'Chiave':'issue:3','Impronta':'dddd','Stato':'fatto','Quando':'2026-10-05'},"
        + "{'Chiave':'issue:4','Impronta':'eeee','Stato':'','Quando':'2026-10-05'}],"
        + "'DominiFidati':['github.com','example.org'],'AvvisoSpento':true,'VersioneSaltata':'1.9'}";

    const string OldHistory = "{'Repo':{'mario/a':[{'Giorno':'2026-10-28','Visite':10,'Unici':5,'Cloni':2,'Download':-1},{'Giorno':'2026-10-29','Visite':12,'Unici':7,'Cloni':3,'Download':40}],"
        + "'luisa/b':[{'Giorno':'2026-10-29','Visite':1,'Unici':1,'Cloni':0,'Download':-1}]}}";

    static string Json(string quoted) { return quoted.Replace('\'', '"'); }

    static void Empty(string folder) {
        if (!Directory.Exists(folder)) return;
        foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(folder)) Directory.Delete(d, true);
    }

    static bool Same(byte[] a, byte[] b) { return a != null && b != null && a.SequenceEqual(b); }

    static int Count(Dictionary<string, object> raw, string key) {
        object value;
        var list = raw.TryGetValue(key, out value) ? value as System.Collections.IList : null;
        return list == null ? 0 : list.Count;
    }

    static int CountWhere(Dictionary<string, object> raw, string list, string field, Func<string, bool> test) {
        object value;
        var items = raw.TryGetValue(list, out value) ? value as System.Collections.IList : null;
        if (items == null) return 0;
        int n = 0;
        foreach (var item in items) {
            var map = item as Dictionary<string, object>;
            object found;
            if (map != null && map.TryGetValue(field, out found) && found is string && test((string)found)) n++;
        }
        return n;
    }

    public static int OnCopy(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL the copy in PM_DATA_DIR is needed\n"); return 1; }
        string data = DataStore.Folder, oldArchive = Path.Combine(data, "archivio.json"), oldHistory = Path.Combine(data, "storico.json"), newArchive = Path.Combine(data, "archive.json"), newHistory = Path.Combine(data, "history.json");
        try {
            if (!File.Exists(oldArchive)) { v("the copy holds archivio.json", false); throw new InvalidOperationException("no archive in the copy"); }
            if (File.Exists(newArchive) || File.Exists(newHistory)) { v("the copy holds only the old files", false); throw new InvalidOperationException("new files already there"); }
            byte[] oldArchiveBytes = File.ReadAllBytes(oldArchive), oldHistoryBytes = File.Exists(oldHistory) ? File.ReadAllBytes(oldHistory) : null;
            var raw = DataStore.Json().DeserializeObject(File.ReadAllText(oldArchive, Encoding.UTF8)) as Dictionary<string, object>;
            object login;
            bool hasLogin = raw.TryGetValue("Login", out login) && !string.IsNullOrEmpty(login as string);
            int accounts = Count(raw, "Conti"), repos = Count(raw, "Repo"), items = Count(raw, "Elementi"), decisions = Count(raw, "Decisioni"), domains = Count(raw, "DominiFidati");
            int comments = CountWhere(raw, "Elementi", "Tipo", x => x == "commento"), commentKeys = CountWhere(raw, "Elementi", "Chiave", x => x.StartsWith("commento:", StringComparison.Ordinal));
            int decisionCommentKeys = CountWhere(raw, "Decisioni", "Chiave", x => x.StartsWith("commento:", StringComparison.Ordinal));
            var states = new[] { "approvato", "rifiutato", "fatto", "quarantena" };
            var statesNew = new[] { "approved", "rejected", "done", "quarantine" };
            var stateCounts = states.Select(s => CountWhere(raw, "Decisioni", "Stato", x => x == s)).ToArray();
            int oldOrigins = CountWhere(raw, "Conti", "Origine", x => x == "vecchio");
            sb.AppendLine("INFO    in the old file: accounts " + accounts + ", repos " + repos + ", items " + items + " (comments " + comments + "), decisions " + decisions + ", trusted domains " + domains);

            var a = DataStore.Load();
            int addedAccount = accounts == 0 && hasLogin ? 1 : 0;
            v("copy: accounts", a.Accounts.Count == accounts + addedAccount);
            v("copy: repos", a.Repo.Count == repos);
            v("copy: items", a.Items.Count == items);
            v("copy: decisions", a.Decisions.Count == decisions);
            v("copy: trusted domains", a.TrustedDomains.Count == (domains == 0 ? DataStore.DefaultTrustedDomains.Length : domains));
            v("copy: comments keep their meaning", a.Items.Count(x => x.Type == "comment") == comments && a.Items.Count(x => x.Key != null && x.Key.StartsWith("comment:", StringComparison.Ordinal)) == commentKeys && a.Decisions.Count(x => x.Key != null && x.Key.StartsWith("comment:", StringComparison.Ordinal)) == decisionCommentKeys);
            v("copy: no old value is left", a.Items.All(x => x.Type != "commento" && (x.Key == null || !x.Key.StartsWith("commento:", StringComparison.Ordinal))) && a.Decisions.All(x => x.State != "approvato" && x.State != "rifiutato" && x.State != "fatto" && x.State != "quarantena" && (x.Key == null || !x.Key.StartsWith("commento:", StringComparison.Ordinal))) && a.Accounts.All(x => x.Origin != "vecchio") && a.Filter != "personali" && a.Filter != "altri");
            v("copy: decision states carry over one by one", Enumerable.Range(0, states.Length).All(i => a.Decisions.Count(x => x.State == statesNew[i]) == stateCounts[i]));
            v("copy: old account origins", a.Accounts.Count(x => x.Origin == "legacy") == oldOrigins + addedAccount);
            sb.AppendLine("INFO    decisions by state: approved " + stateCounts[0] + ", rejected " + stateCounts[1] + ", done " + stateCounts[2] + ", quarantine " + stateCounts[3] + "; old account origins " + oldOrigins);

            var h = History.Load();
            int historyDays = 0;
            if (oldHistoryBytes != null) {
                var rawHistory = DataStore.Json().DeserializeObject(File.ReadAllText(oldHistory, Encoding.UTF8)) as Dictionary<string, object>;
                var perRepo = rawHistory["Repo"] as Dictionary<string, object>;
                bool eachRepo = true;
                int rawDays = 0;
                foreach (var pair in perRepo) {
                    int n = ((System.Collections.IList)pair.Value).Count;
                    rawDays += n;
                    if (History.DaysHowMany(h, pair.Key) != n) eachRepo = false;
                }
                historyDays = h.Repo.Sum(x => x.Value.Count);
                v("copy: history repos", h.Repo.Count == perRepo.Count);
                v("copy: history days, repo by repo", eachRepo && historyDays == rawDays);
                sb.AppendLine("INFO    history: repos " + perRepo.Count + ", days " + rawDays);
            }

            DataStore.Save(a);
            if (oldHistoryBytes != null) History.Save(h);
            v("copy: the old files are byte for byte as they were", Same(File.ReadAllBytes(oldArchive), oldArchiveBytes) && (oldHistoryBytes == null || Same(File.ReadAllBytes(oldHistory), oldHistoryBytes)));
            v("copy: the new files exist", File.Exists(newArchive) && (oldHistoryBytes == null || File.Exists(newHistory)));
            string written = File.ReadAllText(newArchive, Encoding.UTF8);
            v("copy: the new archive has English field names only", new[] { "\"Elementi\":", "\"Decisioni\":", "\"Conti\":", "\"Chiave\":", "\"Stato\":", "\"Tipo\":", "\"DominiFidati\":", "\"Nome\":", "\"Numeri\":", "\"Pannello\":" }.All(x => !written.Contains(x)));
            var b = DataStore.Load();
            v("copy: read back from the new file, the same counts", b.Accounts.Count == a.Accounts.Count && b.Repo.Count == a.Repo.Count && b.Items.Count == a.Items.Count && b.Decisions.Count == a.Decisions.Count && b.TrustedDomains.Count == a.TrustedDomains.Count);
            v("copy: read back from the new file, the same values", Enumerable.Range(0, a.Items.Count).All(i => a.Items[i].Key == b.Items[i].Key && a.Items[i].Type == b.Items[i].Type && a.Items[i].Title == b.Items[i].Title && a.Items[i].Text == b.Items[i].Text)
                && Enumerable.Range(0, a.Decisions.Count).All(i => a.Decisions[i].Key == b.Decisions[i].Key && a.Decisions[i].State == b.Decisions[i].State && a.Decisions[i].Fingerprint == b.Decisions[i].Fingerprint) && a.Filter == b.Filter);
            int withFingerprint = a.Decisions.Count(d => d.Fingerprint != null);
            int matched = a.Decisions.Count(d => d.Fingerprint != null && a.Items.Any(i => i.Key == d.Key && DataStore.Fingerprint(i) == d.Fingerprint));
            sb.AppendLine("INFO    decisions whose fingerprint still matches their item: " + matched + " of " + withFingerprint);
            if (oldHistoryBytes != null) {
                var h2 = History.Load();
                v("copy: history read back from the new file, the same days", h2.Repo.Count == h.Repo.Count && h2.Repo.Sum(x => x.Value.Count) == historyDays);
            }
        } catch (Exception ex) { v("error: " + ex.GetType().Name + " " + ex.Message, false); }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL a test data folder is needed\n"); return 1; }
        Language.Use("en");
        string data = DataStore.Folder, temp = Path.Combine(Path.GetTempPath(), "pm-migration-" + Guid.NewGuid().ToString("N"));
        string reposBefore = DataStore.ReposFolder;
        try {
            Directory.CreateDirectory(temp);
            Directory.CreateDirectory(data);
            var utf8 = new UTF8Encoding(false);

            Empty(data);
            string oldArchive = Path.Combine(data, "archivio.json"), newArchive = Path.Combine(data, "archive.json");
            string reposPath = Path.Combine(temp, "repos").Replace('\\', '/');
            File.WriteAllText(oldArchive, Json(OldArchive).Replace("REPOS", reposPath), utf8);
            byte[] oldArchiveBytes = File.ReadAllBytes(oldArchive);
            v("archive: only the old file exists, the app sees data", DataStore.HasArchive() && !File.Exists(newArchive));
            var a = DataStore.Load();
            v("archive: old file, counts", a.Accounts.Count == 3 && a.Repo.Count == 2 && a.Items.Count == 3 && a.Decisions.Count == 5 && a.TrustedDomains.Count == 2);
            v("archive: old file, top fields", a.Login == "mario" && a.LastUpdate == "2026-11-01T10:00:00Z" && a.Model == "model-a" && a.ReposFolder == reposPath && a.UpdateNoticeOff && a.SkippedVersion == "1.9");
            v("archive: old filter value becomes the new one", a.Filter == AccountAccess.FilterPersonal && a.Filter == "personal");
            v("archive: old account origin value becomes the new one", a.Accounts[0].Origin == AccountAccess.OriginOld && a.Accounts[0].Origin == "legacy" && a.Accounts[1].Origin == AccountAccess.OriginGit && a.Accounts[2].Origin == AccountAccess.OriginBrowser);
            var item1 = a.Items[0]; var comment = a.Items[1]; var pull = a.Items[2];
            v("archive: item fields", item1.Key == "issue:1" && item1.Number == 1 && item1.Type == "issue" && item1.Title == "First" && item1.Author == "luisa" && item1.Text == "body one" && item1.Labels == "bug" && item1.Created == "2026-10-01T10:00:00Z" && item1.Updated == "2026-10-02T10:00:00Z" && item1.State == "open");
            v("archive: a comment keeps its meaning under the new type and key", comment.Type == "comment" && comment.Key == "comment:9" && comment.Text == "a comment" && comment.Author == "anna");
            v("archive: pull request type and key are unchanged", pull.Type == "pull" && pull.Key == "pull:2" && pull.State == "closed");
            v("archive: decision states", a.Decisions[0].State == "approved" && a.Decisions[1].State == "quarantine" && a.Decisions[2].State == "rejected" && a.Decisions[3].State == "done" && a.Decisions[4].State == "");
            v("archive: decision fields and keys", a.Decisions[0].Key == "issue:1" && a.Decisions[0].Fingerprint == "aaaa" && a.Decisions[0].Summary == "sum" && a.Decisions[0].SummaryFingerprint == "aaaa" && a.Decisions[0].SummaryModel == "m1" && a.Decisions[0].When == "2026-10-05" && a.Decisions[1].Key == "comment:9");
            var repoA = a.Repo[0];
            v("archive: repo fields", repoA.Name == "mario/a" && repoA.Owner == "mario" && repoA.Followed && repoA.Private && repoA.Mine && repoA.Since == "2026-01-01" && repoA.Branch == "main" && repoA.Accounts.Count == 1 && a.Repo[1].Missing && !a.Repo[1].Followed);
            v("archive: overview and statistics are read", repoA.Overview != null && repoA.Overview.ReleaseRead && repoA.Overview.LatestTag == "v1.0" && repoA.Overview.Collaborators.Count == 1 && repoA.Overview.Collaborators[0].Name == "luisa" && repoA.Overview.Collaborators[0].Role == "write"
                && repoA.Stats != null && repoA.Stats.ViewsRead && repoA.Stats.Views == 12 && repoA.Stats.UniqueViews == 7 && repoA.Stats.Downloads == 3 && repoA.Stats.ViewDays.Count == 1 && repoA.Stats.ViewDays[0].Day == "2026-10-29" && repoA.Stats.ViewDays[0].Count == 4 && repoA.Stats.ViewDays[0].Uniques == 2);
            v("archive: loading did not touch the old file", Same(File.ReadAllBytes(oldArchive), oldArchiveBytes) && !File.Exists(newArchive));

            DataStore.Save(a);
            v("archive: saving writes the new file", File.Exists(newArchive) && DataStore.HasArchive());
            v("archive: saving leaves the old file byte for byte as it was", Same(File.ReadAllBytes(oldArchive), oldArchiveBytes));
            string written = File.ReadAllText(newArchive, Encoding.UTF8);
            v("archive: the new file uses English names and values", written.Contains("\"Items\"") && written.Contains("\"Decisions\"") && written.Contains("\"comment:9\"") && written.Contains("\"approved\"") && written.Contains("\"personal\"") && written.Contains("\"legacy\"")
                && !written.Contains("Elementi") && !written.Contains("Decisioni") && !written.Contains("commento") && !written.Contains("approvato") && !written.Contains("quarantena") && !written.Contains("personali") && !written.Contains("vecchio"));
            var b = DataStore.Load();
            v("archive: the new file gives the same counts", b.Accounts.Count == a.Accounts.Count && b.Repo.Count == a.Repo.Count && b.Items.Count == a.Items.Count && b.Decisions.Count == a.Decisions.Count && b.TrustedDomains.Count == a.TrustedDomains.Count);
            v("archive: the new file gives the same values", b.Items[1].Type == "comment" && b.Items[1].Key == "comment:9" && b.Decisions[1].State == "quarantine" && b.Decisions[1].Key == "comment:9" && b.Filter == "personal" && b.Accounts[0].Origin == "legacy" && b.Repo[0].Stats.Views == 12);
            v("archive: once the new file exists the old one is not read", Same(File.ReadAllBytes(oldArchive), oldArchiveBytes) && DataStore.Load().Items.Count == 3);
            b.Items.RemoveAt(2);
            DataStore.Save(b);
            v("archive: the second save keeps the previous file as .bak", File.Exists(newArchive + ".bak") && !File.Exists(newArchive + ".new") && !File.Exists(newArchive + ".prima") && !File.Exists(newArchive + ".nuovo") && DataStore.Load().Items.Count == 2);
            v("archive: the old file is still untouched after two saves", Same(File.ReadAllBytes(oldArchive), oldArchiveBytes));

            Empty(data);
            File.WriteAllText(newArchive, "{broken", utf8);
            File.WriteAllText(oldArchive, Json(OldArchive).Replace("REPOS", reposPath), utf8);
            var broken = DataStore.Load();
            v("archive: a broken new file is not replaced by the old one", broken.Items.Count == 0 && broken.Accounts.Count == 0);
            v("archive: a broken new file is set aside as .broken", File.Exists(newArchive + ".broken") && File.ReadAllText(newArchive + ".broken") == "{broken" && !File.Exists(newArchive + ".rotto"));
            Empty(data);
            File.WriteAllText(oldArchive, "{broken", utf8);
            var brokenOld = DataStore.Load();
            v("archive: a broken old file gives an empty archive", brokenOld.Items.Count == 0 && brokenOld.Repo.Count == 0);
            v("archive: a broken old file is copied aside under the new name and left alone", File.ReadAllText(newArchive + ".broken") == "{broken" && File.ReadAllText(oldArchive) == "{broken" && !File.Exists(oldArchive + ".rotto") && !File.Exists(newArchive));

            Empty(data);
            File.WriteAllText(newArchive, "{\"Login\":\"mario\",\"Items\":[{\"Key\":\"comment:5\",\"Type\":\"comment\",\"Number\":3}],\"Decisions\":[{\"Key\":\"comment:5\",\"State\":\"approved\"}],\"Filter\":\"others\",\"Accounts\":[{\"Login\":\"mario\",\"Origin\":\"legacy\"}]}", utf8);
            var current = DataStore.Load();
            v("archive: a file already in the new form is read as it is", current.Items[0].Key == "comment:5" && current.Items[0].Type == "comment" && current.Decisions[0].State == "approved" && current.Filter == "others" && current.Accounts[0].Origin == "legacy");
            Empty(data);
            File.WriteAllText(oldArchive, Json("{'Filtro':'altri','Elementi':[],'Decisioni':[{'Chiave':'issue:1','Stato':'approved'}]}"), utf8);
            var mixed = DataStore.Load();
            v("archive: old filter 'altri' becomes 'others', values already new stay as they are", mixed.Filter == AccountAccess.FilterOthers && mixed.Filter == "others" && mixed.Decisions[0].State == "approved");

            Empty(data);
            string reposNew = Path.Combine(temp, "from-new-variable"), reposOld = Path.Combine(temp, "from-old-variable");
            string keepNew = Environment.GetEnvironmentVariable(DataStore.ReposVariable), keepOld = Environment.GetEnvironmentVariable(Legacy.ReposVariable);
            try {
                File.WriteAllText(newArchive, "{\"Login\":\"mario\"}", utf8);
                Environment.SetEnvironmentVariable(DataStore.ReposVariable, null);
                Environment.SetEnvironmentVariable(Legacy.ReposVariable, reposOld);
                v("variables: only the old repos variable is still accepted", DataStore.Load().ReposFolder == reposOld);
                Environment.SetEnvironmentVariable(DataStore.ReposVariable, reposNew);
                v("variables: the new repos variable wins over the old one", DataStore.Load().ReposFolder == reposNew);
            } finally {
                Environment.SetEnvironmentVariable(DataStore.ReposVariable, keepNew);
                Environment.SetEnvironmentVariable(Legacy.ReposVariable, keepOld);
            }
            string testNew = "PM_MIGRATION_TEST_NEW", testOld = "PM_MIGRATION_TEST_OLD";
            Environment.SetEnvironmentVariable(testNew, null); Environment.SetEnvironmentVariable(testOld, null);
            v("variables: neither is set", Legacy.Variable(testNew, testOld) == null);
            Environment.SetEnvironmentVariable(testOld, "old");
            v("variables: only the old one is set", Legacy.Variable(testNew, testOld) == "old");
            Environment.SetEnvironmentVariable(testNew, "new");
            v("variables: both set, the new one wins", Legacy.Variable(testNew, testOld) == "new");
            Environment.SetEnvironmentVariable(testNew, null); Environment.SetEnvironmentVariable(testOld, null);
            v("variables: the data folder of this run comes from the new variable", DataStore.DataOverride == Environment.GetEnvironmentVariable(DataStore.DataVariable) && DataStore.Folder == DataStore.DataOverride);

            Empty(data);
            string oldHistory = Path.Combine(data, "storico.json"), newHistory = Path.Combine(data, "history.json");
            File.WriteAllText(oldHistory, Json(OldHistory), utf8);
            byte[] oldHistoryBytes = File.ReadAllBytes(oldHistory);
            var h = History.Load();
            v("history: old file, repos and days", h.Repo.Count == 2 && History.DaysHowMany(h, "mario/a") == 2 && History.DaysHowMany(h, "luisa/b") == 1);
            var daysA = History.Copy(h, "mario/a");
            v("history: old file, values", daysA[0].Day == "2026-10-28" && daysA[0].Views == 10 && daysA[0].Uniques == 5 && daysA[0].Clones == 2 && daysA[0].Downloads == -1 && daysA[1].Day == "2026-10-29" && daysA[1].Views == 12 && daysA[1].Uniques == 7 && daysA[1].Clones == 3 && daysA[1].Downloads == 40);
            v("history: loading did not touch the old file", Same(File.ReadAllBytes(oldHistory), oldHistoryBytes) && !File.Exists(newHistory));
            History.Save(h);
            v("history: saving writes the new file and leaves the old one as it was", File.Exists(newHistory) && Same(File.ReadAllBytes(oldHistory), oldHistoryBytes));
            string historyText = File.ReadAllText(newHistory, Encoding.UTF8);
            v("history: the new file uses English names", historyText.Contains("\"Day\"") && historyText.Contains("\"Views\"") && historyText.Contains("\"Downloads\"") && !historyText.Contains("Giorno") && !historyText.Contains("Visite") && !historyText.Contains("Unici"));
            var h2 = History.Load();
            v("history: the new file gives the same days", h2.Repo.Count == 2 && History.DaysHowMany(h2, "mario/a") == 2 && History.Copy(h2, "mario/a")[1].Downloads == 40 && History.Copy(h2, "luisa/b")[0].Views == 1);
            History.Save(h2);
            v("history: the second save keeps the previous file as .bak", File.Exists(newHistory + ".bak") && !File.Exists(newHistory + ".new") && !File.Exists(newHistory + ".prima") && !File.Exists(newHistory + ".nuovo"));
            Empty(data);
            File.WriteAllText(newHistory, "{broken", utf8);
            File.WriteAllText(oldHistory, Json(OldHistory), utf8);
            var brokenHistory = History.Load();
            v("history: a broken new file gives an empty history, set aside as .broken", brokenHistory.Repo.Count == 0 && File.ReadAllText(newHistory + ".broken") == "{broken" && !File.Exists(newHistory + ".rotto"));
            Empty(data);
            File.WriteAllText(oldHistory, "{broken", utf8);
            var brokenOldHistory = History.Load();
            v("history: a broken old file is copied aside under the new name and left alone", brokenOldHistory.Repo.Count == 0 && File.ReadAllText(newHistory + ".broken") == "{broken" && File.ReadAllText(oldHistory) == "{broken" && !File.Exists(oldHistory + ".rotto"));

            Empty(data);
            string oldLanguage = Path.Combine(data, "lingua.txt"), newLanguage = Path.Combine(data, "language.txt");
            File.WriteAllText(oldLanguage, "de", utf8);
            try {
                v("language: the old choice file counts as a choice", !Language.ToChoose(data, false));
                Language.Start(null, data, false);
                v("language: the old choice file is read", Language.Code == "de");
                File.WriteAllText(newLanguage, "fr", utf8);
                Language.Start(null, data, false);
                v("language: the new file wins over the old one", Language.Code == "fr" && Language.ChoiceFile(data) == newLanguage);
                File.Delete(newLanguage);
                Language.Choose("es", data);
                v("language: choosing writes the new file only", File.ReadAllText(newLanguage).Trim() == "es" && File.ReadAllText(oldLanguage) == "de");
                Empty(data);
                File.WriteAllText(oldLanguage, "klingon", utf8);
                Language.Start(null, data, false);
                v("language: an unknown value in the old file means the default", Language.Code == Language.DefaultCode && !Language.ToChoose(data, false));
                Empty(data);
                v("language: no file at all means the choice is still to be made", Language.ToChoose(data, false));
            } finally { Language.Use("en"); }

            Empty(data);
            v("install marker: asks when there is no marker and the program is not installed here", Installer.ToAsk(false, null));
            File.WriteAllText(Path.Combine(data, "senza-installare.txt"), "chosen on 01/11/2026\r\n", utf8);
            v("install marker: the old marker still means 'do not ask'", !Installer.ToAsk(false, null));
            v("install marker: the old marker does not stop the question when a copy is installed", Installer.ToAsk(false, "1.1"));
            v("install marker: installed here, never asks", !Installer.ToAsk(true, "1.2"));
            Empty(data);
            Installer.MarkWithoutInstall();
            v("install marker: choosing to run without installing writes the new name only", File.Exists(Path.Combine(data, "no-install.txt")) && !File.Exists(Path.Combine(data, "senza-installare.txt")) && !Installer.ToAsk(false, null));
            Empty(data);

            string handovers = Path.Combine(temp, "handover-repos");
            DataStore.ReposFolder = handovers;
            Directory.CreateDirectory(handovers);
            Action<string, string> put = (file, text) => { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, text, utf8); };
            string accepted = Path.Combine(handovers, DataStore.WorkFolder, "App-One", "handovers", "accepted");
            put(Path.Combine(accepted, "a.json"), "{\"repo\":\"App-One\",\"release\":null}");
            put(Path.Combine(accepted, "b.json"), "{\"repo\":\"App-One\",\"release\":\"1.2\"}");
            put(Path.Combine(accepted, "c.json"), "{\"repo\":\"App-One\",\"release\":null}");
            put(Path.Combine(accepted, "d.json"), "{broken");
            put(Path.Combine(accepted, "e.txt"), "{\"release\":null}");
            v("handovers: new layout, counts the ones not yet released and skips the unreadable", DataStore.PendingHandovers("nscentart/App-One") == 2);
            v("handovers: the repo name alone also works", DataStore.PendingHandovers("App-One") == 2);
            string legacyAccepted = Path.Combine(handovers, "App-Two", "consegne", "accettate");
            put(Path.Combine(legacyAccepted, "a.json"), "{\"repo\":\"App-Two\",\"rilascio\":null}");
            put(Path.Combine(legacyAccepted, "b.json"), "{\"repo\":\"App-Two\",\"rilascio\":\"1.1\"}");
            put(Path.Combine(legacyAccepted, "c.json"), "{\"repo\":\"App-Two\",\"rilascio\":null}");
            put(Path.Combine(legacyAccepted, "d.json"), "{\"repo\":\"App-Two\",\"rilascio\":null}");
            v("handovers: old layout is read when the new folder does not exist", DataStore.PendingHandovers("nscentart/App-Two") == 3);
            Directory.CreateDirectory(Path.Combine(handovers, DataStore.WorkFolder, "App-Two", "handovers", "accepted"));
            v("handovers: once the new folder exists the old layout is not read", DataStore.PendingHandovers("nscentart/App-Two") == 0);
            put(Path.Combine(handovers, DataStore.WorkFolder, "App-Two", "handovers", "accepted", "x.json"), "{\"release\":null}");
            v("handovers: new layout with one waiting", DataStore.PendingHandovers("nscentart/App-Two") == 1);
            put(Path.Combine(handovers, DataStore.WorkFolder, "App-Three", "handovers", "accepted", "x.json"), "{\"rilascio\":null}");
            v("handovers: the old key is not read in the new layout", DataStore.PendingHandovers("nscentart/App-Three") == 0);
            v("handovers: no folder, no answer", DataStore.PendingHandovers("nscentart/App-Four") == -1);
            v("handovers: names that are not folders, no answer", DataStore.PendingHandovers("nscentart/..") == -1 && DataStore.PendingHandovers("nscentart/") == -1 && DataStore.PendingHandovers("nscentart/a|b") == -1);
            DataStore.ReposFolder = "";
            v("handovers: without a repos folder, no answer", DataStore.PendingHandovers("nscentart/App-One") == -1);
            DataStore.ReposFolder = null;
            v("handovers: with no repos folder set at all, no answer", DataStore.PendingHandovers("nscentart/App-One") == -1);

            v("arguments: --language is found", Program.LanguageOption(new[] { "--language", "de" }) == 0 && Program.LanguageOption(new[] { "--test-x", "--language", "de" }) == 1);
            v("arguments: the old --lingua is still found", Program.LanguageOption(new[] { Legacy.LanguageArgument, "de" }) == 0 && Program.LanguageOption(new[] { "x", "--lingua", "de" }) == 1 && Legacy.LanguageArgument == "--lingua");
            v("arguments: no language option", Program.LanguageOption(new string[0]) < 0 && Program.LanguageOption(new[] { "--portable" }) < 0);
            v("arguments: --uninstall and the old --disinstalla both uninstall", Program.WantsUninstall(new[] { "--uninstall" }) && Program.WantsUninstall(new[] { "--disinstalla" }) && Legacy.UninstallArgument == "--disinstalla");
            v("arguments: nothing else uninstalls", !Program.WantsUninstall(new string[0]) && !Program.WantsUninstall(new[] { "--uninstall", "x" }) && !Program.WantsUninstall(new[] { "--portable" }) && !Program.WantsUninstall(new[] { "uninstall" }));
            v("arguments: --portable and the old --portatile both start without asking", Program.WantsPortable(new[] { "--portable" }) && Program.WantsPortable(new[] { "--portatile" }) && Legacy.PortableArgument == "--portatile");
            v("arguments: nothing else is portable", !Program.WantsPortable(new string[0]) && !Program.WantsPortable(new[] { "--portable", "x" }) && !Program.WantsPortable(new[] { "--uninstall" }));
        } catch (Exception ex) { v("error: " + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace, false); }
        finally {
            DataStore.ReposFolder = reposBefore;
            Language.Use("en");
            try { Empty(data); } catch { }
            try { Directory.Delete(temp, true); } catch { }
        }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 ? 0 : 1;
    }
}
