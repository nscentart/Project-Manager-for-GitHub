using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

public static class Legacy {
    public const string ArchiveFile = "archivio.json", HistoryFile = "storico.json", LicenseFile = "licenza.txt", LanguageFile = "lingua.txt",
        NoInstallFile = "senza-installare.txt", TransitionDeclinedFile = "passaggio-no.txt", PackagesFolder = "pacchetti";
    public const string BackupSuffix = ".prima", TempSuffix = ".nuovo", BrokenSuffix = ".rotto", OldLogSuffix = ".vecchio";
    public const string DataVariable = "GPM_DATI", ReposVariable = "GPM_REPO";
    public const string UninstallArgument = "--disinstalla", PortableArgument = "--portatile", LanguageArgument = "--lingua";
    public const string HandoversFolder = "consegne", AcceptedFolder = "accettate", ReleaseKey = "rilascio";

    static readonly Dictionary<Type, Dictionary<string, Dictionary<string, string>>> ValueNames = new Dictionary<Type, Dictionary<string, Dictionary<string, string>>> {
        { typeof(WorkItem), new Dictionary<string, Dictionary<string, string>> { { "Type", Map("commento", "comment") } } },
        { typeof(Decision), new Dictionary<string, Dictionary<string, string>> {
            { "State", Map("approvato", "approved", "rifiutato", "rejected", "fatto", "done", "quarantena", "quarantine") } } },
        { typeof(GitHubAccount), new Dictionary<string, Dictionary<string, string>> { { "Origin", Map("vecchio", "legacy") } } },
        { typeof(Archive), new Dictionary<string, Dictionary<string, string>> { { "Filter", Map("personali", "personal", "altri", "others") } } }
    };

    static readonly Dictionary<string, string> KeyPrefixes = Map("commento:", "comment:");

    public static string Variable(string name, string legacyName) {
        return Environment.GetEnvironmentVariable(name) ?? Environment.GetEnvironmentVariable(legacyName);
    }

    static bool Present(string path) { return File.Exists(path) || Directory.Exists(path); }

    public static string Existing(string path, string legacyPath) { return Present(path) || !Present(legacyPath) ? path : legacyPath; }

    static readonly Dictionary<Type, Dictionary<string, string>> FieldNames = new Dictionary<Type, Dictionary<string, string>> {
        { typeof(WorkItem), Map(
            "Chiave", "Key", "Numero", "Number", "Tipo", "Type", "Titolo", "Title", "Autore", "Author", "Testo", "Text",
            "Etichette", "Labels", "Creato", "Created", "Aggiornato", "Updated", "Aperta", "State") },
        { typeof(Decision), Map(
            "Chiave", "Key", "Impronta", "Fingerprint", "Stato", "State", "Quando", "When", "Riassunto", "Summary",
            "RiassuntoImpronta", "SummaryFingerprint", "RiassuntoModello", "SummaryModel") },
        { typeof(RepoInfo), Map(
            "Nome", "Name", "Descrizione", "Description", "Proprietario", "Owner", "Privata", "Private",
            "Seguita", "Followed", "Mia", "Mine", "Organizzazione", "Organization", "Dal", "Since", "Sparita", "Missing",
            "Ramo", "Branch", "Pannello", "Overview", "Conti", "Accounts", "Numeri", "Stats") },
        { typeof(Collaborator), Map("Nome", "Name", "Ruolo", "Role") },
        { typeof(RepoOverview), Map(
            "LettoIl", "ReadAt", "ReleaseLetta", "ReleaseRead", "TagLetto", "TagRead", "CommitLetto", "CommitRead",
            "LicenzaLetta", "LicenseRead", "ReadmeLetto", "ReadmeRead", "ReleaseData", "ReleaseDate",
            "ReleaseErrore", "ReleaseError", "UltimoTag", "LatestTag", "TagErrore", "TagError",
            "CommitDopo", "CommitsSinceRelease", "CommitDopoErrore", "CommitsSinceReleaseError",
            "UltimoCommit", "LatestCommit", "CommitErrore", "CommitError", "Collaboratori", "Collaborators",
            "CollaboratoriErrore", "CollaboratorsError", "Licenza", "License", "LicenzaErrore", "LicenseError",
            "LicenzaAltra", "OtherLicense", "ReadmeData", "ReadmeDate", "ReadmeErrore", "ReadmeError",
            "ReadmeDataErrore", "ReadmeDateError") },
        { typeof(TrafficDay), Map("Giorno", "Day", "Conta", "Count", "Unici", "Uniques") },
        { typeof(TrafficEntry), Map("Nome", "Name", "Titolo", "Title", "Conta", "Count", "Unici", "Uniques") },
        { typeof(DownloadedAsset), Map("Nome", "Name", "Download", "Downloads") },
        { typeof(DownloadedRelease), Map("Download", "Downloads", "Allegati", "Assets") },
        { typeof(RepoStats), Map(
            "LettoIl", "ReadAt", "VisiteLette", "ViewsRead", "CloniLetti", "ClonesRead", "FontiLette", "ReferrersRead",
            "PagineLette", "PagesRead", "DownloadLetti", "DownloadsRead", "RepoLetta", "RepoRead",
            "VisiteErrore", "ViewsError", "CloniErrore", "ClonesError", "FontiErrore", "ReferrersError",
            "PagineErrore", "PagesError", "DownloadErrore", "DownloadsError", "RepoErrore", "RepoError", "Visite", "Views",
            "VisiteUnici", "UniqueViews", "Cloni", "Clones", "CloniUnici", "UniqueClones", "VisiteGiorni", "ViewDays",
            "CloniGiorni", "CloneDays", "Fonti", "Referrers", "Pagine", "Pages", "Download", "Downloads",
            "Versioni", "Releases", "Stelle", "Stars", "Copie", "Forks", "Osservatori", "Watchers",
            "StellePrima", "StarsBefore", "CopiePrima", "ForksBefore", "StelleCambiate", "StarsChanged",
            "CopieCambiate", "ForksChanged") },
        { typeof(HistoryDay), Map("Giorno", "Day", "Visite", "Views", "Unici", "Uniques", "Cloni", "Clones", "Download", "Downloads") },
        { typeof(HistoryArchive), Map() },
        { typeof(GitHubAccount), Map("Origine", "Origin") },
        { typeof(Archive), Map(
            "UltimoAggiornamento", "LastUpdate", "Modello", "Model", "Filtro", "Filter", "CartellaRepo", "ReposFolder",
            "Conti", "Accounts", "Elementi", "Items", "Decisioni", "Decisions", "DominiFidati", "TrustedDomains",
            "AvvisoSpento", "UpdateNoticeOff", "VersioneSaltata", "SkippedVersion") }
    };

    static Dictionary<string, string> Map(params string[] pairs) {
        var map = new Dictionary<string, string>();
        for (int i = 0; i < pairs.Length; i += 2) map[pairs[i]] = pairs[i + 1];
        return map;
    }

    public static Archive ArchiveFromJson(string json) { return FromJson<Archive>(json); }

    public static HistoryArchive HistoryFromJson(string json) { return FromJson<HistoryArchive>(json); }

    static T FromJson<T>(string json) {
        var serializer = DataStore.Json();
        return serializer.ConvertToType<T>(Rename(serializer.DeserializeObject(json), typeof(T)));
    }

    static object Rename(object node, Type type) {
        var dictionary = node as Dictionary<string, object>;
        if (dictionary != null) {
            Dictionary<string, string> names;
            if (FieldNames.TryGetValue(type, out names)) {
                var renamed = new Dictionary<string, object>();
                foreach (var pair in dictionary) {
                    string name;
                    if (!names.TryGetValue(pair.Key, out name)) name = pair.Key;
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    renamed[name] = Upgrade(type, name, field == null ? pair.Value : Rename(pair.Value, field.FieldType));
                }
                return renamed;
            }
            var valueType = ItemType(type);
            if (valueType == null) return node;
            var values = new Dictionary<string, object>();
            foreach (var pair in dictionary) values[pair.Key] = Rename(pair.Value, valueType);
            return values;
        }
        var list = node as IList;
        if (list != null) {
            var itemType = ItemType(type);
            if (itemType == null) return node;
            var items = new ArrayList();
            foreach (var item in list) items.Add(Rename(item, itemType));
            return items;
        }
        return node;
    }

    static object Upgrade(Type type, string field, object value) {
        string text = value as string;
        if (text == null) return value;
        Dictionary<string, Dictionary<string, string>> fields;
        Dictionary<string, string> values;
        string replacement;
        if (ValueNames.TryGetValue(type, out fields) && fields.TryGetValue(field, out values) && values.TryGetValue(text, out replacement)) return replacement;
        if (field == "Key" && (type == typeof(WorkItem) || type == typeof(Decision)))
            foreach (var prefix in KeyPrefixes)
                if (text.StartsWith(prefix.Key, StringComparison.Ordinal)) return prefix.Value + text.Substring(prefix.Key.Length);
        return value;
    }

    static Type ItemType(Type type) {
        if (!type.IsGenericType) return null;
        var definition = type.GetGenericTypeDefinition();
        var arguments = type.GetGenericArguments();
        if (definition == typeof(List<>)) return arguments[0];
        if (definition == typeof(Dictionary<,>)) return arguments[1];
        return null;
    }
}
