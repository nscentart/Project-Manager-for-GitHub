// ProvaRiprova.cs (Project Manager 1.1, 03/11/2026): subito dopo «Rendi pubblica» GitHub può dare 404 sul pubblicare una release. Il programma
// riprova da solo, si può fermare, e alla fine dice quello che è vero dopo aver riletto, non l'ultimo errore.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class ProvaRiprova {
    const string CHIAVE = "chiave-finta-riprova";

    static string Errore(Action a) {
        try { a(); return null; } catch (ErroreScrittura e) { return e.Message; }
    }

    public static int Prova(string esito) {
        var sb = new StringBuilder(); int tot = 0, male = 0;
        Action<string, bool> v = (nome, ok) => { tot++; if (!ok) male++; sb.AppendLine((ok ? "OK      " : "SBAGLIA ") + nome); };
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPM_DATI"))) { File.WriteAllText(esito, "SBAGLIA serve una cartella dati di prova\n"); return 1; }
        Lingua.Usa("it");
        Dialogo.Silenzioso = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        int tentativi = Scrittura.TentativiPubblica, attesa = Scrittura.AttesaPubblicaMs;
        v("di partenza: 6 tentativi a 20 secondi", tentativi == 6 && attesa == 20000);
        Scrittura.TentativiPubblica = 3;
        Scrittura.AttesaPubblicaMs = 1;
        try {
            using (var s = new ServerFinto()) {
                s.Chi[CHIAVE] = "mario"; s.Permessi[CHIAVE] = "repo, read:org";
                var c = Cliente.Finto(s.Porta, CHIAVE, true);
                int prog = 0;
                Func<ServerFinto.RepoF> repo = () => {
                    var r = s.Nuova("mario/r" + (++prog), "User", "admin", false);
                    r.Release.Add(new ServerFinto.ReleaseF { Id = 5, Tag = "v1.1", Titolo = "x", Bozza = true });
                    return r;
                };
                Func<ServerFinto.RepoF, string> via = r => "/repos/" + r.Nome + "/releases/5";
                Func<ServerFinto.RepoF, int> patch = r => s.Di("PATCH", via(r)).Count;
                var passi = new List<string>();
                Action<string> passo = t => passi.Add(t);

                // subito bene
                var r0 = repo();
                var f0 = Scrittura.Fatti(c, r0.Nome);
                string e0 = Errore(() => Scrittura.PubblicaConRiprova(c, f0, r0.Nome, 5, true, passo));
                v("subito bene: pubblicata, un tentativo, nessun passo", e0 == null && !r0.Release[0].Bozza && patch(r0) == 1 && passi.Count == 0);

                // 404 e poi bene
                var r1 = repo();
                var f1 = Scrittura.Fatti(c, r1.Nome);
                s.Forza("PATCH", via(r1), 404, "{\"message\":\"Not Found\"}", null);
                string e1 = Errore(() => Scrittura.PubblicaConRiprova(c, f1, r1.Nome, 5, true, passo));
                v("404 e poi bene: riesce da solo", e1 == null && !r1.Release[0].Bozza && patch(r1) == 2);
                v("404 e poi bene: dice a che punto è", passi.Count == 1 && passi[0].Contains("tentativo 2 di 3") && passi[0].Contains("GitHub non trova ancora la repo"));

                // 404 per sempre: è ancora una bozza, e lo dice
                passi.Clear();
                var r2 = repo();
                var f2 = Scrittura.Fatti(c, r2.Nome);
                s.ForzaVolte("PATCH", via(r2), 404, "{\"message\":\"Not Found\"}", null, 99);
                string e2 = Errore(() => Scrittura.PubblicaConRiprova(c, f2, r2.Nome, 5, true, passo));
                v("404 per sempre: tre tentativi", patch(r2) == 3);
                v("404 per sempre: non dice «non c'è»", e2 == Lingua.T("scrittura.pubblica_ancora_bozza") && !e2.Contains("non c'è"));
                v("404 per sempre: resta bozza", r2.Release[0].Bozza);
                v("404 per sempre: i passi con entrambi i tentativi", passi.Any(x => x.Contains("tentativo 2 di 3")) && passi.Any(x => x.Contains("tentativo 3 di 3")));
                s.Forza("PATCH", via(r2), 200, null, null);
                r2.Release[0].Bozza = true;

                // il tentativo di prima era arrivato: è un successo, senza ripubblicare
                var r3 = repo();
                var f3 = Scrittura.Fatti(c, r3.Nome);
                s.Forza("PATCH", via(r3), 404, "{\"message\":\"Not Found\"}", null);
                string e3 = Errore(() => Scrittura.PubblicaConRiprova(c, f3, r3.Nome, 5, true, t => r3.Release[0].Bozza = false));
                v("già pubblicata nel frattempo: è un successo", e3 == null);
                v("già pubblicata nel frattempo: non ripubblica", patch(r3) == 1);

                // la release è sparita davvero
                var r4 = repo();
                var f4 = Scrittura.Fatti(c, r4.Nome);
                s.ForzaVolte("PATCH", via(r4), 404, "{\"message\":\"Not Found\"}", null, 99);
                string e4 = Errore(() => Scrittura.PubblicaConRiprova(c, f4, r4.Nome, 5, true, t => r4.Release.Clear()));
                v("sparita davvero: lo dice", e4 == Lingua.T("scrittura.pubblica_non_c_piu"));

                // GitHub non trova nemmeno l'elenco
                var r5 = repo();
                var f5 = Scrittura.Fatti(c, r5.Nome);
                s.ForzaVolte("PATCH", via(r5), 404, "{\"message\":\"Not Found\"}", null, 99);
                s.ForzaVolte("GET", via(r5), 404, "{\"message\":\"Not Found\"}", null, 99);
                s.ForzaVolte("GET", "/repos/" + r5.Nome + "/releases", 404, "{\"message\":\"Not Found\"}", null, 99);
                string e5 = Errore(() => Scrittura.PubblicaConRiprova(c, f5, r5.Nome, 5, true, passo));
                v("GitHub non trova la repo: lo dice, e che non è stato pubblicato niente", e5 == Lingua.T("scrittura.pubblica_github_non_trova") && e5.Contains("non è stata pubblicata"));

                // un errore che non è 404 non si ritenta
                foreach (var codice in new[] { 403, 422, 500 }) {
                    passi.Clear();
                    var r6 = repo();
                    var f6 = Scrittura.Fatti(c, r6.Nome);
                    s.ForzaVolte("PATCH", via(r6), codice, "{\"message\":\"no\"}", null, 99);
                    string e6 = Errore(() => Scrittura.PubblicaConRiprova(c, f6, r6.Nome, 5, true, passo));
                    v(codice + ": non si ritenta, e non si racconta altro", e6 != null && patch(r6) == 1 && passi.Count == 0);
                }

                // il nome scritto male si ferma subito, come prima
                var r7 = repo();
                var f7 = Scrittura.Fatti(c, r7.Nome);
                string e7 = Errore(() => Scrittura.PubblicaConRiprova(c, f7, "sbagliato", 5, true, passo));
                v("nome sbagliato: non pubblica", e7 != null && patch(r7) == 0 && r7.Release[0].Bozza);

                const string NF = "{\"message\":\"Not Found\"}";
                // cancellata da altri: non si aspettano tutti i tentativi, e mentre si aspetta si parla della release
                Scrittura.TentativiPubblica = 6;
                passi.Clear();
                var r9 = repo();
                var f9 = Scrittura.Fatti(c, r9.Nome);
                r9.Release.Clear();
                string e9 = Errore(() => Scrittura.PubblicaConRiprova(c, f9, r9.Nome, 5, true, passo));
                v("cancellata da altri: lo dice", e9 == Lingua.T("scrittura.pubblica_non_c_piu"));
                v("cancellata da altri: una sola attesa, non tutte", passi.Count == 1 && patch(r9) == 0);
                v("cancellata da altri: mentre aspetta parla della release, non della repo", passi.Count == 1 && passi[0] == Lingua.T("scrittura.pubblica_riprovo_release", "sec", 1));
                var r10 = repo();
                var f10 = Scrittura.Fatti(c, r10.Nome);
                var via10 = r10.Release[0];
                r10.Release.Clear();
                string e10 = Errore(() => Scrittura.PubblicaConRiprova(c, f10, r10.Nome, 5, true, t => { if (r10.Release.Count == 0) r10.Release.Add(via10); }));
                v("assente per una lettura sola: si pubblica", e10 == null && !via10.Bozza && patch(r10) == 1);
                Scrittura.TentativiPubblica = 3;

                // dopo un tentativo non si dice «non è stata pubblicata» senza aver riletto
                var r11 = repo();
                var f11 = Scrittura.Fatti(c, r11.Nome);
                s.Forza("PATCH", via(r11), 404, NF, null);
                string e11 = Errore(() => Scrittura.PubblicaConRiprova(c, f11, r11.Nome, 5, true, t => { s.ForzaVolte("GET", via(r11), 404, NF, null, 99); s.ForzaVolte("GET", "/repos/" + r11.Nome + "/releases", 404, NF, null, 99); }));
                v("dopo un tentativo, senza poter rileggere: dice che non lo sa", e11 == Lingua.T("scrittura.pubblica_non_so") && patch(r11) == 1);

                // fermato: quello che dice è vero solo dopo aver riletto
                Action<string> ferma = t => { throw new ControlloFermato(Lingua.T("gestione.controllo_fermato_non_ho_fatto")); };
                var r12 = repo();
                var f12 = Scrittura.Fatti(c, r12.Nome);
                s.ForzaVolte("PATCH", via(r12), 404, NF, null, 99);
                string e12 = Errore(() => Scrittura.PubblicaConRiprova(c, f12, r12.Nome, 5, true, ferma));
                v("fermato dopo un tentativo, ancora bozza: lo dice, senza «non ho fatto niente»", e12 == Lingua.T("scrittura.pubblica_fermata_bozza") && !e12.Contains("non ho fatto niente") && patch(r12) == 1);
                var r13 = repo();
                var f13 = Scrittura.Fatti(c, r13.Nome);
                s.ForzaVolte("PATCH", via(r13), 404, NF, null, 99);
                string e13 = Errore(() => Scrittura.PubblicaConRiprova(c, f13, r13.Nome, 5, true, t => { r13.Release[0].Bozza = false; ferma(t); }));
                v("fermato, ma il 404 aveva avuto effetto: è pubblicata, e non dice il contrario", e13 == null);
                var r14 = repo();
                var f14 = Scrittura.Fatti(c, r14.Nome);
                s.ForzaVolte("PATCH", via(r14), 404, NF, null, 99);
                string e14 = Errore(() => Scrittura.PubblicaConRiprova(c, f14, r14.Nome, 5, true, t => { s.ForzaVolte("GET", via(r14), 404, NF, null, 99); ferma(t); }));
                v("fermato, senza poter rileggere: dice che non lo sa", e14 == Lingua.T("scrittura.pubblica_non_so"));
                var r15 = repo();
                var f15 = Scrittura.Fatti(c, r15.Nome);
                string e15 = Errore(() => Scrittura.PubblicaConRiprova(c, f15, r15.Nome, 5, true, passo, () => ferma(null)));
                v("fermato prima di ogni tentativo: il PATCH non parte, e «non ho fatto niente» è vero", e15 == Lingua.T("gestione.controllo_fermato_non_ho_fatto") && patch(r15) == 0 && r15.Release[0].Bozza);

                // ---------- la finestra vera: riprova senza bloccarsi, e si può fermare ----------
                var P = ProvaScrittura.PerFoto(s);
                P.Login = "mario";
                Scrittura.AttesaPubblicaMs = 3000;
                Scrittura.TentativiPubblica = 6;
                var r8 = repo();
                s.ForzaVolte("PATCH", via(r8), 404, "{\"message\":\"Not Found\"}", null, 99);
                Conferma k = null;
                k = new Conferma(P, r8.Nome, "Pubblica", "Pubblica", true, false, (cx, fx) => "", (cx, fx, st) => Scrittura.PubblicaConRiprova(cx, fx, st, 5, true, t => k.Passo(t))) { Riprova = true };
                k.ShowInTaskbar = false; k.StartPosition = FormStartPosition.Manual; k.Location = new Point(-20000, -20000);
                k.Show();
                Func<Func<bool>, bool> finche = cond => { for (int i = 0; i < 1000; i++) { if (cond()) return true; Application.DoEvents(); Thread.Sleep(10); } return cond(); };
                v("finestra: rilegge la repo", finche(() => k.Visti != null));
                k.Nome.Text = r8.Nome;
                Application.DoEvents();
                k.BSi.PerformClick();
                bool scrive = finche(() => k.Racconto.Text.Contains("tentativo 2 di 6"));
                v("finestra: mentre aspetta dice a che punto è", scrive);
                v("finestra: mentre aspetta non è bloccata e si può annullare", k.BNo.Enabled);
                int prima = patch(r8);
                k.BNo.PerformClick();
                v("finestra: Annulla ferma", k.Fermato);
                finche(() => !k.Lavorando);
                Application.DoEvents();
                Thread.Sleep(50);
                v("finestra: fermata non pubblica più", patch(r8) == prima && r8.Release[0].Bozza);
                k.Close();
                k.Dispose();

                // ---------- Annulla nell'istante sbagliato: il clic arriva proprio lì ----------
                Scrittura.AttesaPubblicaMs = 200;
                Scrittura.TentativiPubblica = 3;
                const string LETTO = "quello che si conferma";
                Func<ServerFinto.RepoF, Action<Conferma, string>, Action<Conferma>, Conferma> apri = (r, pa, co) => {
                    Conferma w = null;
                    w = new Conferma(P, r.Nome, "Pubblica", "Pubblica", true, false, (cx, fx) => LETTO, (cx, fx, st) => Scrittura.PubblicaConRiprova(cx, fx, st, 5, true, t => pa(w, t), () => co(w))) { Riprova = true };
                    w.ShowInTaskbar = false; w.StartPosition = FormStartPosition.Manual; w.Location = new Point(-20000, -20000);
                    w.Show();
                    finche(() => w.Visti != null);
                    w.Nome.Text = r.Nome;
                    Application.DoEvents();
                    return w;
                };
                Action<Conferma> annulla = w => w.Invoke((Action)(() => w.BNo.PerformClick()));
                Action<Conferma> aspetta = w => { finche(() => !w.Lavorando); Application.DoEvents(); };
                Action<Conferma> chiudi = w => { w.Close(); w.Dispose(); };

                // subito prima della scrittura, sul secondo tentativo
                var r20 = repo();
                s.ForzaVolte("PATCH", via(r20), 404, NF, null, 99);
                int controlli = 0;
                var w1 = apri(r20, (w, t) => w.Passo(t), w => { if (++controlli == 2) annulla(w); w.Controlla(); });
                w1.BSi.PerformClick();
                aspetta(w1);
                v("Annulla subito prima della scrittura: il PATCH non parte", controlli == 2 && patch(r20) == 1 && r20.Release[0].Bozza);
                v("Annulla subito prima della scrittura: dice che è ancora una bozza", w1.Avviso.Text == Lingua.T("scrittura.pubblica_fermata_bozza") && !w1.Fatto);
                v("dopo Annulla: non resta «Mi fermo...»", w1.Racconto.Text.Contains(LETTO) && !w1.Racconto.Text.Contains(Lingua.T("gestione.mi_fermo")));

                // il fermo di prima non vale per il giro nuovo: riprova da solo, e al secondo 404 non dice «Controllo fermato»
                s.ForzaVolte("PATCH", via(r20), 404, NF, null, 1);
                int primaDelNuovo = patch(r20);
                w1.BSi.PerformClick();
                v("di nuovo Pubblica: il fermo di prima non vale più", !w1.Fermato);
                aspetta(w1);
                v("di nuovo Pubblica: riprova da solo e pubblica", w1.Fatto && !r20.Release[0].Bozza && patch(r20) == primaDelNuovo + 2);
                chiudi(w1);

                // nell'ultimo secondo dell'attesa
                var r21 = repo();
                s.ForzaVolte("PATCH", via(r21), 404, NF, null, 99);
                var w2 = apri(r21, (w, t) => { w.Passo(t); annulla(w); }, w => w.Controlla());
                w2.BSi.PerformClick();
                aspetta(w2);
                v("Annulla nell'ultimo secondo dell'attesa: il PATCH non parte", patch(r21) == 1 && r21.Release[0].Bozza && w2.Avviso.Text == Lingua.T("scrittura.pubblica_fermata_bozza"));
                chiudi(w2);

                // il 404 aveva avuto effetto: «Pubblicata» è vero, e l'ha riletto
                var r22 = repo();
                s.ForzaVolte("PATCH", via(r22), 404, NF, null, 99);
                var w3 = apri(r22, (w, t) => { w.Passo(t); r22.Release[0].Bozza = false; annulla(w); w.Passo(t); }, w => w.Controlla());
                w3.BSi.PerformClick();
                aspetta(w3);
                v("Annulla, ma il tentativo era arrivato: è un successo", w3.Fatto && w3.Fermato && patch(r22) == 1);
                chiudi(w3);

                // dopo l'ultimo tentativo
                var r23 = repo();
                s.ForzaVolte("PATCH", via(r23), 404, NF, null, 99);
                var w4 = apri(r23, (w, t) => w.Passo(t), w => w.Controlla());
                w4.BSi.PerformClick();
                aspetta(w4);
                v("dopo l'ultimo tentativo: non resta «tentativo 3 di 3»", !w4.Racconto.Text.Contains("tentativo") && w4.Racconto.Text.Contains(LETTO));
                v("dopo l'ultimo tentativo: sotto c'è come è andata", w4.Avviso.Text == Lingua.T("scrittura.pubblica_ancora_bozza") && !w4.Fatto);
                chiudi(w4);

                // cancellata da altri, nella finestra
                var r24 = repo();
                var w5 = apri(r24, (w, t) => w.Passo(t), w => w.Controlla());
                r24.Release.Clear();
                w5.BSi.PerformClick();
                aspetta(w5);
                v("cancellata da altri, nella finestra: lo dice e non resta l'attesa", w5.Avviso.Text == Lingua.T("scrittura.pubblica_non_c_piu") && w5.Racconto.Text.Contains(LETTO));
                chiudi(w5);
            }
        } catch (Exception e) {
            v("eccezione: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace, false);
        } finally {
            Scrittura.TentativiPubblica = tentativi;
            Scrittura.AttesaPubblicaMs = attesa;
        }
        sb.AppendLine(tot - male + " su " + tot + " giuste");
        File.WriteAllText(esito, sb.ToString(), new UTF8Encoding(false));
        return male == 0 ? 0 : 1;
    }
}
