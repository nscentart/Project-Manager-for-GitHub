// Modello.cs (Project Manager 0.4, 02/11/2026): i dati che il programma tiene. Tutto quello che arriva da
// GitHub è un Elemento: un testo scritto da qualcuno, mai un'istruzione. La Decisione è di chi usa il programma, legata
// all'impronta del testo: se il testo cambia dopo, la decisione non vale più e il testo ripassa dalla dogana.
using System;
using System.Collections.Generic;

public class Elemento {
    public string Chiave;       // «issue:<id>» o «commento:<id>»
    public string Repo;         // proprietario/nome
    public int Numero;          // numero della issue (per i commenti: della issue a cui rispondono)
    public string Tipo;         // issue, pull, commento
    public string Titolo;       // titolo della issue (vuoto nei commenti: il titolo della madre si cerca quando serve)
    public string Autore;
    public string Testo;
    public string Etichette;    // separate da virgola
    public string Url;          // costruito da repo e numero, mai preso dal testo
    public string Creato;       // ISO 8601, UTC
    public string Aggiornato;
    public string Aperta;       // open o closed
}

public class Decisione {
    public string Chiave, Impronta, Stato, Quando;   // Stato: approvato, rifiutato, fatto, quarantena (rimesso a mano), vuoto
    public string Riassunto, RiassuntoImpronta, RiassuntoModello;
}

public class RepoInfo {
    public string Nome, Descrizione, Proprietario;
    public bool Privata, Seguita;
    public bool Mia;            // tua: ne sei il proprietario o l'amministratore (repo di un'organizzazione)
    public bool Organizzazione; // il proprietario è un'organizzazione, non una persona
    public string Dal;          // ultimo «since» usato per le issue, ISO 8601
    public bool Sparita;        // non compare più nell'elenco di GitHub (cancellata o rinominata): non si scarica, i testi restano
    public string Ramo;         // il ramo principale (0.3)
    public Panorama Pannello;   // l'ultima lettura per la Panoramica (0.3); negli archivi vecchi non c'è e resta nullo
    public List<string> Conti;  // gli account collegati che la vedono (0.4); nullo = non si sa ancora
}

public class Collaboratore {
    public string Nome, Ruolo;  // Ruolo come lo dice GitHub: read, triage, write, maintain, admin
}

// l'ultima lettura di una repo. Ogni richiesta può fallire da sola: se fallisce, il dato vecchio resta e accanto c'è
// l'errore. «...Letto» dice se quel dato è mai arrivato: vuoto dopo una lettura vuol dire «non c'è», non «non lo so».
public class Panorama {
    public string LettoIl;      // dd/MM/yyyy HH:mm, l'ora di questo computer
    public bool ReleaseLetta, TagLetto, CommitLetto, LicenzaLetta, ReadmeLetto;
    public string ReleaseTag, ReleaseData, ReleaseErrore;
    public string UltimoTag, TagErrore;
    public int CommitDopo = -1;   // commit dopo l'ultima release; -1 = non si sa o non c'è release
    public string CommitDopoErrore;
    public string UltimoCommit, CommitErrore;
    public List<Collaboratore> Collaboratori;   // nullo = mai letti
    public string CollaboratoriErrore;
    public string Licenza, LicenzaErrore;       // vuota = nessuna
    public bool LicenzaAltra;                   // GitHub non la riconosce: il nome viene dal file
    public bool Readme, ReadmeCopyright;
    public string ReadmeData, ReadmeErrore, ReadmeDataErrore;

    public Panorama Copia() { return (Panorama)MemberwiseClone(); }
}

// un account collegato. La chiave d'accesso non sta qui: la custodisce Windows (Cassaforte) oppure Git
public class ContoGitHub {
    public string Login;
    public string Origine;      // browser (in Cassaforte), git (lo dà Git), vecchio (archivio di prima della 0.4: lo dà Git)
}

public class Archivio {
    public string Login, UltimoAggiornamento, Modello;   // Login: l'account attivo
    public string Filtro;       // cosa mostra la colonna: Accessi.TUTTE, PERSONALI, ALTRI o ORG + nome
    public string CartellaRepo; // dove stanno le repo su questo computer; vuota = nessuna, nullo = mai chiesto
    public List<ContoGitHub> Conti = new List<ContoGitHub>();
    public List<RepoInfo> Repo = new List<RepoInfo>();
    public List<Elemento> Elementi = new List<Elemento>();
    public List<Decisione> Decisioni = new List<Decisione>();
    public List<string> DominiFidati = new List<string>();
}

// lo stato che si vede: deriva a ogni lettura da testo, dogana e decisione, non si salva
public class Voce {
    public Elemento E;
    public Decisione D;
    public List<Motivo> Motivi;
    public string Impronta, Categoria, Stato;   // Stato: quarantena, lavorare, fatto, rifiutato
}
