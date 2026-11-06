using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class License {
    public const string FilePath = DataStore.LicenseFile, LicensePrefix = "NSCENT-1";
    public const int MaxHolder = 80, MaxText = 1000;
    const int MaxFile = 8192, MaxProgram = 40, Side = 32;

    public static readonly string[] PublicKeys = { "6spl-c33gRNzEfGA1yLICcsI1-A8llhc5u2hYpdECvZge7moC6WzlDVXyWoCjxHhx9WwVBMiMVg7feeqcEk8Sg" };

    public static readonly int[] Revoked = { };

    public static LicenseResult Check(string text, string program) { return Check(text, program, PublicKeys, Revoked); }

    public static LicenseResult Check(string text, string program, string[] keys, int[] revoked) {
        var no = new LicenseResult { State = LicenseState.NotValid };
        try {
            string t = Clean(text);
            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(program) || keys == null) return no;
            var pieces = t.Split('.');
            if (pieces.Length != 3 || pieces[0] != LicensePrefix) return no;
            byte[] signature = FromBase64Url(pieces[2]);
            if (signature == null || signature.Length != Side * 2 || FromBase64Url(pieces[1]) == null) return no;
            if (!GoodSignature(ToSign(pieces[1]), signature, keys)) return no;
            var e = Read(pieces[1]);
            if (e == null) return no;
            if (e.Program != program) e.State = LicenseState.OtherProgram;
            else if (revoked != null && Array.IndexOf(revoked, e.Number) >= 0) e.State = LicenseState.Revoked;
            else e.State = LicenseState.Valid;
            return e;
        } catch { return no; }
    }

    static bool GoodSignature(byte[] data, byte[] signature, string[] keys) {
        foreach (var k in keys) {
            byte[] blob = PublicBlob(k);
            if (blob == null) continue;
            try {
                using (var c = CngKey.Import(blob, CngKeyBlobFormat.EccPublicBlob))
                using (var e = new ECDsaCng(c)) {
                    e.HashAlgorithm = CngAlgorithm.Sha256;
                    if (e.VerifyData(data, signature)) return true;
                }
            } catch { }
        }
        return false;
    }

    static LicenseResult Read(string data) {
        var lines = new UTF8Encoding(false, true).GetString(FromBase64Url(data)).Split('\n');
        int number;
        if (lines.Length != 4 || !GoodProgram(lines[0]) || !GoodNumber(lines[1], out number) || !GoodHolder(lines[2]) || !GoodDate(lines[3])) return null;
        return new LicenseResult { Program = lines[0], Number = number, Holder = lines[2], Data = lines[3] };
    }

    static bool GoodProgram(string s) {
        if (string.IsNullOrEmpty(s) || s.Length > MaxProgram) return false;
        foreach (char c in s) if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9')) return false;
        return true;
    }

    static bool GoodNumber(string s, out int n) {
        n = 0;
        if (string.IsNullOrEmpty(s) || s.Length > 9 || s[0] == '0') return false;
        foreach (char c in s) if (c < '0' || c > '9') return false;
        n = int.Parse(s, CultureInfo.InvariantCulture);
        return true;
    }

    public static bool GoodHolder(string s) {
        if (string.IsNullOrEmpty(s) || s.Length > MaxHolder || s != s.Trim()) return false;
        for (int i = 0; i < s.Length; i++) {
            if (char.IsSurrogatePair(s, i)) { i++; continue; }
            var u = CharUnicodeInfo.GetUnicodeCategory(s[i]);
            if (u == UnicodeCategory.Control || u == UnicodeCategory.Format || u == UnicodeCategory.Surrogate
                || u == UnicodeCategory.LineSeparator || u == UnicodeCategory.ParagraphSeparator) return false;
        }
        return true;
    }

    static bool GoodDate(string s) {
        DateTime d;
        return s != null && s.Length == 10 && DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }

    public static string Clean(string text) {
        if (text == null || text.Length > MaxFile) return null;
        var sb = new StringBuilder();
        foreach (char c in text) if (!char.IsWhiteSpace(c) && c != '\uFEFF') sb.Append(c);
        return sb.Length > MaxText ? null : sb.ToString();
    }

    public static string Compose(string program, int number, string holder, string data) {
        int n;
        string num = number.ToString(CultureInfo.InvariantCulture);
        if (!GoodProgram(program) || !GoodNumber(num, out n) || !GoodHolder(holder) || !GoodDate(data)) return null;
        return Base64Url(new UTF8Encoding(false, true).GetBytes(program + "\n" + num + "\n" + holder + "\n" + data));
    }

    public static byte[] ToSign(string data) { return Encoding.ASCII.GetBytes(data); }

    public static string Join(string data, byte[] signature) { return LicensePrefix + "." + data + "." + Base64Url(signature); }

    public static string Base64Url(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

    public static byte[] FromBase64Url(string s) {
        if (string.IsNullOrEmpty(s) || s.Length % 4 == 1) return null;
        foreach (char c in s) if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return null;
        try {
            var b = Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/') + new string('=', (4 - s.Length % 4) % 4));
            return Base64Url(b) == s ? b : null;
        } catch (FormatException) { return null; }
    }

    public static string TextAsKey(byte[] blob) {
        if (blob == null || blob.Length != 8 + Side * 2 || blob[0] != 0x45 || blob[1] != 0x43 || blob[2] != 0x53 || blob[3] != 0x31) return null;
        var xy = new byte[Side * 2];
        Buffer.BlockCopy(blob, 8, xy, 0, xy.Length);
        return Base64Url(xy);
    }

    static byte[] PublicBlob(string key) {
        byte[] xy = FromBase64Url(key);
        if (xy == null || xy.Length != Side * 2) return null;
        var blob = new byte[8 + xy.Length];
        blob[0] = 0x45; blob[1] = 0x43; blob[2] = 0x53; blob[3] = 0x31;
        blob[4] = Side;
        Buffer.BlockCopy(xy, 0, blob, 8, xy.Length);
        return blob;
    }

    public static LicenseResult Load(string folder, string program) { return Load(folder, program, PublicKeys, Revoked); }

    public static LicenseResult Load(string folder, string program, string[] keys, int[] revoked) {
        string text;
        try {
            string f = Legacy.Existing(Path.Combine(folder, FilePath), Path.Combine(folder, Legacy.LicenseFile));
            if (!File.Exists(f) && !Directory.Exists(f)) return new LicenseResult { State = LicenseState.Missing };
            using (var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                if (s.Length > MaxFile) return new LicenseResult { State = LicenseState.NotValid };
                var b = new byte[(int)s.Length];
                int read = 0, n;
                while (read < b.Length && (n = s.Read(b, read, b.Length - read)) > 0) read += n;
                using (var r = new StreamReader(new MemoryStream(b, 0, read), Encoding.UTF8, true)) text = r.ReadToEnd();
            }
        } catch { return new LicenseResult { State = LicenseState.Unreadable }; }
        return Check(text, program, keys, revoked);
    }

    public static bool Write(string folder, string text) {
        string t = Clean(text), tmp = null;
        if (string.IsNullOrEmpty(t)) return false;
        try {
            Directory.CreateDirectory(folder);
            string f = Path.Combine(folder, FilePath);
            tmp = f + DataStore.TempSuffix;
            File.WriteAllText(tmp, t + "\r\n", new UTF8Encoding(false));
            if (File.Exists(f)) File.Replace(tmp, f, null); else File.Move(tmp, f);
            return true;
        } catch {
            try { if (tmp != null) File.Delete(tmp); } catch { }
            return false;
        }
    }

    public static bool Remove(string folder) {
        try {
            string f = Path.Combine(folder, FilePath), legacy = Path.Combine(folder, Legacy.LicenseFile);
            if (File.Exists(f)) File.Delete(f);
            if (File.Exists(legacy)) File.Delete(legacy);
            return !File.Exists(f) && !Directory.Exists(f) && !File.Exists(legacy) && !Directory.Exists(legacy);
        } catch { return false; }
    }
}
