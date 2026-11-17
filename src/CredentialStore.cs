using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class CredentialStore {
    public const string Prefix = "ProjectManager/";
    const int Generic = 1, HereOnly = 2, NotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NativeCredential {
        public int Flags, Type;
        public string TargetName, Comment;
        public int Text1, Text2;
        public int BlobSize;
        public IntPtr Blob;
        public int Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)] static extern bool CredWrite(ref NativeCredential c, int flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)] static extern bool CredRead(string name, int type, int flags, out IntPtr c);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)] static extern bool CredDelete(string name, int type, int flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredEnumerateW", SetLastError = true)] static extern bool CredEnumerate(string filter, int flags, out int howMany, out IntPtr joinedList);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr p);

    public static void Write(string name, string user, string val) {
        var b = Encoding.Unicode.GetBytes(val);
        IntPtr p = Marshal.AllocHGlobal(b.Length);
        try {
            Marshal.Copy(b, 0, p, b.Length);
            var c = new NativeCredential { Type = Generic, TargetName = name, UserName = user, Blob = p, BlobSize = b.Length, Persist = HereOnly };
            if (!CredWrite(ref c, 0)) throw new Exception(Language.T("credentials.windows_not_store_access", "error", new Win32Exception(Marshal.GetLastWin32Error()).Message));
        } finally {
            Marshal.Copy(new byte[b.Length], 0, p, b.Length);
            Marshal.FreeHGlobal(p);
        }
    }

    public static string Read(string name) {
        IntPtr p;
        if (!CredRead(name, Generic, 0, out p)) return null;
        try {
            var c = (NativeCredential)Marshal.PtrToStructure(p, typeof(NativeCredential));
            if (c.BlobSize == 0 || c.Blob == IntPtr.Zero) return null;
            var b = new byte[c.BlobSize];
            Marshal.Copy(c.Blob, b, 0, c.BlobSize);
            return Encoding.Unicode.GetString(b);
        } finally { CredFree(p); }
    }

    public static bool Copy(string source, string a) {
        IntPtr p;
        if (!CredRead(source, Generic, 0, out p)) return false;
        try {
            var c = (NativeCredential)Marshal.PtrToStructure(p, typeof(NativeCredential));
            var n = new NativeCredential { Type = Generic, TargetName = a, UserName = c.UserName, Blob = c.Blob, BlobSize = c.BlobSize, Persist = HereOnly };
            return CredWrite(ref n, 0);
        } finally { CredFree(p); }
    }

    public static void Delete(string name) { CredDelete(name, Generic, 0); }

    public static List<string> ListNames(string prefix) {
        var names = new List<string>();
        int howMany; IntPtr list;
        if (!CredEnumerate(prefix + "*", 0, out howMany, out list)) {
            int e = Marshal.GetLastWin32Error();
            if (e != NotFound) DataStore.Log("credential store: list not read, code " + e);
            return names;
        }
        try {
            for (int i = 0; i < howMany; i++) {
                var c = (NativeCredential)Marshal.PtrToStructure(Marshal.ReadIntPtr(list, i * IntPtr.Size), typeof(NativeCredential));
                names.Add(c.TargetName);
            }
        } finally { CredFree(list); }
        return names;
    }

    public static void RemoveAll(string prefix) { foreach (var n in ListNames(prefix)) Delete(n); }

    public static string NameOf(string login) { return Prefix + (login ?? "").ToLowerInvariant(); }
}
