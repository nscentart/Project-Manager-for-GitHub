using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class Cassaforte {
    public const string PREFISSO = "ProjectManager/";
    const int GENERICA = 1, SOLO_QUI = 2, NON_TROVATA = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENZIALE {
        public int Flags, Type;
        public string TargetName, Comment;
        public int Scritta1, Scritta2;   // FILETIME: due int, per non cambiare l'allineamento
        public int BlobSize;
        public IntPtr Blob;
        public int Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)] static extern bool CredWrite(ref CREDENZIALE c, int flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)] static extern bool CredRead(string nome, int tipo, int flags, out IntPtr c);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)] static extern bool CredDelete(string nome, int tipo, int flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredEnumerateW", SetLastError = true)] static extern bool CredEnumerate(string filtro, int flags, out int quante, out IntPtr elenco);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr p);

    public static void Scrivi(string nome, string utente, string valore) {
        var b = Encoding.Unicode.GetBytes(valore);
        IntPtr p = Marshal.AllocHGlobal(b.Length);
        try {
            Marshal.Copy(b, 0, p, b.Length);
            var c = new CREDENZIALE { Type = GENERICA, TargetName = nome, UserName = utente, Blob = p, BlobSize = b.Length, Persist = SOLO_QUI };
            if (!CredWrite(ref c, 0)) throw new Exception(Lingua.T("cassaforte.windows_non_ha_custodito_l", "errore", new Win32Exception(Marshal.GetLastWin32Error()).Message));
        } finally {
            Marshal.Copy(new byte[b.Length], 0, p, b.Length);
            Marshal.FreeHGlobal(p);
        }
    }

    // nullo se non c'è
    public static string Leggi(string nome) {
        IntPtr p;
        if (!CredRead(nome, GENERICA, 0, out p)) return null;
        try {
            var c = (CREDENZIALE)Marshal.PtrToStructure(p, typeof(CREDENZIALE));
            if (c.BlobSize == 0 || c.Blob == IntPtr.Zero) return null;
            var b = new byte[c.BlobSize];
            Marshal.Copy(c.Blob, b, 0, c.BlobSize);
            return Encoding.Unicode.GetString(b);
        } finally { CredFree(p); }
    }

    // porta una chiave a un altro nome, con lo stesso utente, senza mai leggerne il valore a parole: falso se non c'è o non si scrive
    public static bool Copia(string da, string a) {
        IntPtr p;
        if (!CredRead(da, GENERICA, 0, out p)) return false;
        try {
            var c = (CREDENZIALE)Marshal.PtrToStructure(p, typeof(CREDENZIALE));
            var n = new CREDENZIALE { Type = GENERICA, TargetName = a, UserName = c.UserName, Blob = c.Blob, BlobSize = c.BlobSize, Persist = SOLO_QUI };
            return CredWrite(ref n, 0);
        } finally { CredFree(p); }
    }

    public static void Cancella(string nome) { CredDelete(nome, GENERICA, 0); }

    public static List<string> Elenco(string prefisso) {
        var nomi = new List<string>();
        int quante; IntPtr elenco;
        if (!CredEnumerate(prefisso + "*", 0, out quante, out elenco)) {
            int e = Marshal.GetLastWin32Error();
            if (e != NON_TROVATA) Dati.Log("cassaforte: elenco non letto, codice " + e);
            return nomi;
        }
        try {
            for (int i = 0; i < quante; i++) {
                var c = (CREDENZIALE)Marshal.PtrToStructure(Marshal.ReadIntPtr(elenco, i * IntPtr.Size), typeof(CREDENZIALE));
                nomi.Add(c.TargetName);
            }
        } finally { CredFree(elenco); }
        return nomi;
    }

    public static void TogliTutto(string prefisso) { foreach (var n in Elenco(prefisso)) Cancella(n); }

    public static string NomeDi(string login) { return PREFISSO + (login ?? "").ToLowerInvariant(); }
}
