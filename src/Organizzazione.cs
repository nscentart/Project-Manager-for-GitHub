// Organizzazione.cs (Project Manager 0.3, 01/11/2026): chi è «tuo» quando il login e il proprietario di una
// repo non coincidono (repo di un'organizzazione di cui il login è amministratore). Niente nomi scritti nel codice:
// tutto deriva da quello che GitHub dice di ogni repo. Funzioni pure, provate con dati finti da --prova-organizzazione.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class Organizzazione {
    public static bool Uguali(string a, string b) { return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase); }

    // una repo è «tua» se ne sei il proprietario o se ne sei amministratore (organizzazione); se sei solo collaboratore no
    public static bool Mia(string proprietario, string login, bool admin) {
        return admin || (!string.IsNullOrEmpty(login) && Uguali(proprietario, login));
    }

    // chi conta come «altra persona» tra i collaboratori: né tu, né l'organizzazione proprietaria
    public static List<Collaboratore> Altri(IEnumerable<Collaboratore> tutti, RepoInfo r, string login) {
        bool org = r.Organizzazione || (r.Mia && !Uguali(r.Proprietario, login));
        return tutti.Where(x => !Uguali(x.Nome, login) && !(org && Uguali(x.Nome, r.Proprietario))).ToList();
    }

    // il nome da mostrare: senza «proprietario/» se la repo è tua, intero se è di un altro
    public static string Corto(RepoInfo r, string login) {
        string prefisso = (r.Proprietario ?? "") + "/";
        bool mia = r.Mia || Uguali(r.Proprietario, login);
        return mia && r.Nome.StartsWith(prefisso, StringComparison.OrdinalIgnoreCase) ? r.Nome.Substring(prefisso.Length) : r.Nome;
    }

    // ---------- la prova, con dati finti ----------
    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        Func<string, string, bool, bool, RepoInfo> repo = (nome, prop, org, mia) => new RepoInfo { Nome = nome, Proprietario = prop, Organizzazione = org, Mia = mia };
        Func<string[], List<Collaboratore>> col = n => n.Select(x => new Collaboratore { Nome = x, Ruolo = "admin" }).ToList();

        v("seguita: login proprietario", Mia("pippo", "pippo", false));
        v("seguita: organizzazione, admin vero", Mia("azienda", "pippo", true));
        v("non seguita: organizzazione, admin falso", !Mia("azienda", "pippo", false));
        v("non seguita: repo di un altro utente", !Mia("tizio", "pippo", false));
        v("maiuscole uguali nel login", Mia("Pippo", "pippo", false));
        v("login vuoto non seguito", !Mia("azienda", "", false));

        var o = repo("azienda/app", "azienda", true, true);
        v("corto: organizzazione tua", Corto(o, "pippo") == "app");
        v("corto: repo di un altro non tuo", Corto(repo("tizio/x", "tizio", false, false), "pippo") == "tizio/x");
        v("corto: vecchio archivio, proprietario = login", Corto(repo("pippo/y", "pippo", false, false), "pippo") == "y");
        v("corto: dopo il cambio di login l'archivio vecchio resta valido", Corto(repo("azienda/app", "azienda", false, true), "pippo") == "app");
        v("corto: organizzazione non tua resta intera", Corto(repo("azienda/app", "azienda", true, false), "pippo") == "azienda/app");

        v("altri: solo tu in organizzazione", Altri(col(new[] { "pippo" }), o, "pippo").Count == 0);
        v("altri: l'organizzazione elencata non è una persona", Altri(col(new[] { "pippo", "azienda" }), o, "pippo").Count == 0);
        v("altri: un collega conta", Altri(col(new[] { "pippo", "caio" }), o, "pippo").Select(x => x.Nome).SequenceEqual(new[] { "caio" }));
        v("altri: repo di un altro, il proprietario conta", Altri(col(new[] { "pippo", "tizio" }), repo("tizio/x", "tizio", false, false), "pippo").Count == 1);
        v("altri: login diverso per maiuscole", Altri(col(new[] { "Pippo" }), o, "pippo").Count == 0);

        sb.AppendLine(tot - male + " su " + tot + " giuste");
        System.IO.File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }
}
