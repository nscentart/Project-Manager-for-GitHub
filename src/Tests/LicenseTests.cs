using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class LicenseTests {
    const string PM = AppLicense.Program, WM = "WeightMonitor";

    public class KeyPair : IDisposable {
        readonly CngKey k = CngKey.Create(CngAlgorithm.ECDsaP256);
        public string Publish { get { return License.TextAsKey(k.Export(CngKeyBlobFormat.EccPublicBlob)); } }
        public byte[] Signature(byte[] data) { using (var e = new ECDsaCng(k)) { e.HashAlgorithm = CngAlgorithm.Sha256; return e.SignData(data); } }
        public string DataSignature(string data) { return License.Join(data, Signature(License.ToSign(data))); }
        public string Text(string program, int number, string holder, string data) { return DataSignature(License.Compose(program, number, holder, data)); }
        public string Lines(string lines) { return DataSignature(License.Base64Url(new UTF8Encoding(false).GetBytes(lines))); }
        public string Byte(byte[] b) { return DataSignature(License.Base64Url(b)); }
        public void Dispose() { k.Dispose(); }
    }

    const string SampleKey = "QL4gH5LfgRlHa-HHi_mYEEzIR05zO3WfsQfXvqXturBg89xrznZq_WFFIjrfEoHwPzk_rQcCxJsPLMgtCVlxVA";
    const string SampleText = "NSCENT-1.UHJvamVjdE1hbmFnZXIKMQpSb3NzaSBTcmwKMjAyNi0xMS0wMw.x4uOx3v4G8F2mRuqfQAxBwiY6tYu0FRobzssDsSxgdF3rhMx40gXkNdl8BPxhUmN2z35tSlRdDBE7YIu76AYjQ";

    public static int RunTests(string result) {
        var sb = new StringBuilder(); int tot = 0, badly = 0;
        Action<string, bool> v = (name, ok) => { tot++; if (!ok) badly++; sb.AppendLine((ok ? "OK      " : "FAIL ") + name); };
        if (string.IsNullOrEmpty(DataStore.DataOverride)) { File.WriteAllText(result, "FAIL needs PM_DATA_DIR: a test folder\r\n", new UTF8Encoding(false)); return 2; }
        string temp = Path.Combine(Path.GetTempPath(), "gpm-test-license-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var realKeys = AppLicense.Keys;
        try {
            using (var one = new KeyPair())
            using (var two = new KeyPair()) {
                var publisherKeys = new[] { one.Publish };
                var noWithdrawal = new int[0];
                Func<string, LicenseResult> c = t => License.Check(t, PM, publisherKeys, noWithdrawal);
                Func<string, bool> notValid = t => { var e = c(t); return e.State == LicenseState.NotValid && !e.Valid && e.Holder == "" && e.Number == 0; };
                string good = one.Text(PM, 12, "Rossi Srl", "2026-11-03");
                var pieces = good.Split('.');

                var b = c(good);
                v("good signature: valid", b.State == LicenseState.Valid && b.Valid);
                v("good signature: the fields are the signed ones", b.Program == PM && b.Number == 12 && b.Holder == "Rossi Srl" && b.Data == "2026-11-03");
                v("shape: starts with NSCENT-1 and has three pieces", pieces.Length == 3 && pieces[0] == "NSCENT-1");
                v("shape: the signature is 64 bytes", License.FromBase64Url(pieces[2]).Length == 64);
                v("shape: the data is four lines", Encoding.UTF8.GetString(License.FromBase64Url(pieces[1])) == PM + "\n12\nRossi Srl\n2026-11-03");
                v("shape: a single line, no spaces", !good.Any(char.IsWhiteSpace));
                v("pasted with spaces and line breaks: valid", c("  \r\n" + good + " \r\n\t").Valid);
                v("split by a mail program: valid", c(good.Substring(0, 40) + "\r\n" + good.Substring(40)).Valid);
                v("name with accents and symbols: valid", c(one.Text(PM, 13, "Caf\u00e9 \u00c5ngstr\u00f6m & Sons Ltd", "2026-11-03")).Holder == "Caf\u00e9 \u00c5ngstr\u00f6m & Sons Ltd");
                v("name of 80 characters: valid", c(one.Text(PM, 14, new string('x', 80), "2026-11-03")).Valid);
                v("name with characters outside the basic plane: valid", c(one.Text(PM, 15, "Rossi \U0001F600 Srl", "2026-11-03")).Valid);
                v("every signature is different but good", c(one.Text(PM, 12, "Rossi Srl", "2026-11-03")).Valid);
                v("WeightMonitor with its own name: valid", License.Check(one.Text(WM, 16, "Rossi Srl", "2026-11-03"), WM, publisherKeys, noWithdrawal).Valid);

                string[] otherLicenseData = {
                    one.Text(PM, 12, "Bianchi Spa", "2026-11-03"), one.Text(PM, 99, "Rossi Srl", "2026-11-03"),
                    one.Text(PM, 12, "Rossi Srl", "2030-01-01"), one.Text(WM, 12, "Rossi Srl", "2026-11-03") };
                foreach (var a in otherLicenseData) v("tampered: data of another license with this signature", notValid(pieces[0] + "." + a.Split('.')[1] + "." + pieces[2]));
                bool allI = true;
                for (int i = 0; i < pieces[1].Length; i++) {
                    var x = pieces[1].ToCharArray(); x[i] = x[i] == 'A' ? 'B' : 'A';
                    if (!notValid(pieces[0] + "." + new string(x) + "." + pieces[2])) allI = false;
                }
                v("tampered: one character changed, in every position of the data", allI);
                allI = true;
                for (int i = 0; i < pieces[2].Length; i++) {
                    var x = pieces[2].ToCharArray(); x[i] = x[i] == 'A' ? 'B' : 'A';
                    if (!notValid(pieces[0] + "." + pieces[1] + "." + new string(x))) allI = false;
                }
                v("tampered: one character changed, in every position of the signature", allI);
                v("tampered: data and signature swapped", notValid(pieces[0] + "." + pieces[2] + "." + pieces[1]));
                v("tampered: one extra piece", notValid(good + ".AAAA"));
                v("tampered: signature missing", notValid(pieces[0] + "." + pieces[1] + "."));
                v("tampered: only two pieces", notValid(pieces[0] + "." + pieces[1]));
                v("tampered: signature of zeros", notValid(pieces[0] + "." + pieces[1] + "." + License.Base64Url(new byte[64])));
                v("tampered: signature of 63 bytes", notValid(pieces[0] + "." + pieces[1] + "." + License.Base64Url(new byte[63])));
                v("tampered: signature with = padding", notValid(good + "=="));
                v("tampered: start of another version", notValid("NSCENT-2." + pieces[1] + "." + pieces[2]));
                v("tampered: start in lowercase", notValid("nscent-1." + pieces[1] + "." + pieces[2]));
                v("tampered: no start", notValid(pieces[1] + "." + pieces[2]));
                v("tampered: normal base64 instead of the url one", pieces[1].IndexOfAny("-_".ToCharArray()) < 0 && pieces[2].IndexOfAny("-_".ToCharArray()) < 0 || notValid(good.Replace('-', '+').Replace('_', '/')));
                string lastPiece = pieces[2];
                char alt = (char)(lastPiece[lastPiece.Length - 1] + 1);
                v("tampered: the same signature written in another way", notValid(pieces[0] + "." + pieces[1] + "." + lastPiece.Substring(0, lastPiece.Length - 1) + alt));
                v("strict: base64url with extra bits", License.FromBase64Url("QQ") != null && License.FromBase64Url("QR") == null);

                v("signature of another key: not valid", notValid(two.Text(PM, 12, "Rossi Srl", "2026-11-03")));
                v("signature of another key: says nothing about the content", c(two.Text(WM, 12, "Rossi Srl", "2026-11-03")).Program == "");
                v("signature of another key: even with a revoked number it does not say revoked", License.Check(two.Text(PM, 7, "x", "2026-11-03"), PM, publisherKeys, new[] { 7 }).State == LicenseState.NotValid);

                var at = c(one.Text(WM, 20, "Rossi Srl", "2026-11-03"));
                v("other program: not valid", !at.Valid && at.State == LicenseState.OtherProgram);
                v("other program: says which one, from the signed data", at.Program == WM && at.Holder == "Rossi Srl");
                v("other program: different letter case is not enough", c(one.Text("projectmanager", 21, "Rossi Srl", "2026-11-03")).State == LicenseState.OtherProgram);
                v("other program: a program never heard of", c(one.Text("Other", 22, "Rossi Srl", "2026-11-03")).State == LicenseState.OtherProgram);
                v("other program: a caller without a name gets nothing", License.Check(good, "", publisherKeys, noWithdrawal).State == LicenseState.NotValid && License.Check(good, null, publisherKeys, noWithdrawal).State == LicenseState.NotValid);

                var rev = License.Check(good, PM, publisherKeys, new[] { 3, 12 });
                v("revoked: not valid", !rev.Valid && rev.State == LicenseState.Revoked);
                v("revoked: says the number", rev.Number == 12);
                v("revoked: the other numbers stay good", License.Check(good, PM, publisherKeys, new[] { 3, 13, 120 }).Valid);
                v("revoked: null list means no withdrawal", License.Check(good, PM, publisherKeys, null).Valid);

                string[] randomTexts = { null, "", "   ", "\r\n", "hello", ".", "..", "...", "....", "NSCENT-1", "NSCENT-1.", "NSCENT-1..", "NSCENT-1...", "NSCENT-1.A.A", "NSCENT-1.AAAA.AAAA", "=====", "\0\0\0",
                    "<script>alert(1)</script>", "../../../windows/system32", "Commercial license: Rossi Srl", "ProjectManager\n1\nRossi Srl\n2026-11-03", "NSCENT-1.é.è", "\uD800", "NSCENT-1.\uD800.\uDC00" };
                bool all = true;
                foreach (var a in randomTexts) if (!notValid(a)) all = false;
                v("random text: " + randomTexts.Length + " texts, none valid and no error", all);
                var die = new Random(20261103);
                all = true;
                for (int i = 0; i < 300; i++) {
                    var by = new byte[die.Next(1, 200)]; die.NextBytes(by);
                    if (!notValid(Encoding.UTF8.GetString(by)) || !notValid("NSCENT-1." + License.Base64Url(by) + "." + License.Base64Url(new byte[64]))) all = false;
                }
                v("random text: 600 dice rolls", all);
                v("empty text: not valid", notValid(""));
                v("very long: a million letters", notValid(new string('A', 1000000)));
                v("very long: ten million dots", notValid(new string('.', 10000000)));
                v("very long: two huge pieces", notValid("NSCENT-1." + new string('A', 1000000) + "." + new string('A', 1000000)));
                v("very long: a good license inside lots of spaces", notValid(new string(' ', 1000000) + good));
                v("very long: huge data with a good signature", notValid(one.Byte(new byte[900])));

                v("empty list: every license is not valid", License.Check(good, PM, new string[0], noWithdrawal).State == LicenseState.NotValid);
                v("null list: not valid, no error", License.Check(good, PM, null, noWithdrawal).State == LicenseState.NotValid);
                v("the program's list: a test key is not accepted", License.Check(good, PM).State == LicenseState.NotValid);
                v("list with two keys: the first one works", License.Check(good, PM, new[] { one.Publish, two.Publish }, noWithdrawal).Valid);
                v("list with two keys: the second one works", License.Check(good, PM, new[] { two.Publish, one.Publish }, noWithdrawal).Valid);
                v("list with broken keys: they do not stop the good one", License.Check(good, PM, new[] { "", null, "broken", License.Base64Url(new byte[64]), License.Base64Url(new byte[10]), one.Publish }, noWithdrawal).Valid);
                v("list of broken keys only: not valid", License.Check(good, PM, new[] { "", null, "broken", License.Base64Url(new byte[64]) }, noWithdrawal).State == LicenseState.NotValid);
                v("key as text: refuses anything but a public P-256", License.TextAsKey(new byte[72]) == null && License.TextAsKey(null) == null);

                string[] wrong = { "", PM, PM + "\n12", PM + "\n12\nRossi Srl", PM + "\n12\nRossi Srl\n2026-11-03\n", PM + "\n12\nRossi Srl\n2026-11-03\nother",
                    "\n12\nRossi Srl\n2026-11-03", PM + "\n\nRossi Srl\n2026-11-03", PM + "\n12\n\n2026-11-03", PM + "\n12\nRossi Srl\n",
                    PM + "\n0\nRossi Srl\n2026-11-03", PM + "\n007\nRossi Srl\n2026-11-03", PM + "\n-1\nRossi Srl\n2026-11-03", PM + "\n+1\nRossi Srl\n2026-11-03", PM + "\nabc\nRossi Srl\n2026-11-03",
                    PM + "\n1234567890\nRossi Srl\n2026-11-03", PM + "\n\u0663\nRossi Srl\n2026-11-03",
                    PM + "\n12\n" + new string('x', 81) + "\n2026-11-03", PM + "\n12\nRossi\tSrl\n2026-11-03", PM + "\n12\nRossi\rSrl\n2026-11-03", PM + "\n12\nRossi\0Srl\n2026-11-03",
                    PM + "\n12\nRossi\u202ESrl\n2026-11-03", PM + "\n12\nRossi\u200BSrl\n2026-11-03", PM + "\n12\nRossi\u2028Srl\n2026-11-03", PM + "\n12\n Rossi Srl\n2026-11-03", PM + "\n12\nRossi Srl \n2026-11-03",
                    PM + "\n12\nRossi Srl\n2026-13-03", PM + "\n12\nRossi Srl\n2026-02-30", PM + "\n12\nRossi Srl\n03/11/2026", PM + "\n12\nRossi Srl\n2026-1-3", PM + "\n12\nRossi Srl\n2026-11-03 ", PM + "\n12\nRossi Srl\n\u0662\u0660\u0662\u0666-11-03",
                    "Project Manager\n12\nRossi Srl\n2026-11-03", new string('P', 41) + "\n12\nRossi Srl\n2026-11-03", "\uFEFF" + PM + "\n12\nRossi Srl\n2026-11-03" };
                foreach (var s in wrong) v("good signature, wrong data: " + Line(s), notValid(one.Lines(s)));
                v("good signature, bytes that are not UTF-8", notValid(one.Byte(new byte[] { 0xC3, 0x28, 0x0A, 0xFF })));
                v("good signature, UTF-8 with half a character", notValid(one.Byte(new byte[] { 0x50, 0x0A, 0x31, 0x0A, 0xED, 0xA0, 0x80, 0x0A, 0x32 })));

                v("compose: refuses the wrong fields", new[] {
                    License.Compose("", 1, "x", "2026-11-03"), License.Compose("Project Manager", 1, "x", "2026-11-03"), License.Compose(PM, 0, "x", "2026-11-03"), License.Compose(PM, -3, "x", "2026-11-03"), License.Compose(PM, 1000000000, "x", "2026-11-03"),
                    License.Compose(PM, 1, "", "2026-11-03"), License.Compose(PM, 1, null, "2026-11-03"), License.Compose(PM, 1, new string('x', 81), "2026-11-03"), License.Compose(PM, 1, "a\nb", "2026-11-03"),
                    License.Compose(PM, 1, " x", "2026-11-03"), License.Compose(PM, 1, "\uD800", "2026-11-03"), License.Compose(PM, 1, "x", "03/11/2026"), License.Compose(PM, 1, "x", null) }.All(x => x == null));

                var sample = License.Check(SampleText, PM, new[] { SampleKey }, noWithdrawal);
                v("sample made by the little program: valid", sample.Valid);
                v("sample: its fields", sample.Program == PM && sample.Number == 1 && sample.Holder == "Rossi Srl" && sample.Data == "2026-11-03");
                v("sample: not valid with the program's keys", License.Check(SampleText, PM).State == LicenseState.NotValid);

                string f = Path.Combine(temp, "data"), license = Path.Combine(f, License.FilePath);
                Func<LicenseResult> load = () => License.Load(f, PM, publisherKeys, noWithdrawal);
                v("file: folder that does not exist, missing", load().State == LicenseState.Missing);
                v("file: removing a license that is not there is not an error", License.Remove(f));
                v("file: an empty text is not written", !License.Write(f, "") && !License.Write(f, "   ") && !License.Write(f, null));
                v("file: a very long text is not written", !License.Write(f, new string('A', 2000)));
                v("file: it gets written", License.Write(f, "  " + good + "\r\n"));
                v("file: inside is the text, on one line", File.ReadAllText(license) == good + "\r\n");
                v("file: no temporary files left around", Directory.GetFiles(f).Length == 1);
                var ri = load();
                v("file: read back as valid", ri.Valid && ri.Holder == "Rossi Srl");
                v("file: it gets replaced", License.Write(f, one.Text(PM, 30, "Bianchi Spa", "2026-11-03")) && load().Holder == "Bianchi Spa");
                v("file: no copy of the old one", Directory.GetFiles(f).Length == 1);
                File.WriteAllText(license, good, new UTF8Encoding(true));
                v("file: with the BOM mark, valid", load().Valid);
                File.WriteAllText(license, good, Encoding.Unicode);
                v("file: saved as UTF-16 by Notepad, valid", load().Valid);
                File.WriteAllText(license, "");
                v("empty file: not valid", load().State == LicenseState.NotValid);
                File.WriteAllText(license, "this is not a license");
                v("broken file: not valid", load().State == LicenseState.NotValid);
                var dirty = new byte[4000]; die.NextBytes(dirty);
                File.WriteAllBytes(license, dirty);
                v("file of random bytes: not valid", load().State == LicenseState.NotValid);
                File.WriteAllText(license, good + "\r\n" + good + "\r\n");
                v("file with two licenses: not valid", load().State == LicenseState.NotValid);
                using (var g = new FileStream(license, FileMode.Create)) g.SetLength(50 * 1024 * 1024);
                v("file of 50 MB: not valid", load().State == LicenseState.NotValid);
                File.WriteAllText(license, good);
                using (new FileStream(license, FileMode.Open, FileAccess.Read, FileShare.None))
                    v("file locked by another program: unreadable", load().State == LicenseState.Unreadable);
                v("file: removing works", License.Remove(f) && load().State == LicenseState.Missing);
                string legacyLicense = Path.Combine(f, "licenza.txt");
                File.WriteAllText(legacyLicense, good);
                v("old file name: a license saved by version 1.1 is read", load().Valid && !File.Exists(license));
                v("old file name: a new license goes to the new file and the old one stays as it was", License.Write(f, one.Text(PM, 30, "Bianchi Spa", "2026-11-03")) && File.Exists(license) && load().Holder == "Bianchi Spa" && File.ReadAllText(legacyLicense) == good);
                v("old file name: removing the license removes both files", License.Remove(f) && !File.Exists(license) && !File.Exists(legacyLicense) && load().State == LicenseState.Missing);
                Directory.CreateDirectory(license);
                v("a folder instead of the file: unreadable", load().State == LicenseState.Unreadable);
                v("a folder instead of the file: cannot be removed and says so", !License.Remove(f));
                v("a folder instead of the file: cannot be written and says so", !License.Write(f, good));
                v("a folder instead of the file: no temporary file is left", !File.Exists(license + ".new"));
                Directory.Delete(license);
                v("null folder: unreadable, no error", License.Load(null, PM, publisherKeys, noWithdrawal).State == LicenseState.Unreadable && !License.Write(null, good) && !License.Remove(null));
                v("folder with forbidden characters: unreadable, no error", License.Load("a<b>|c", PM, publisherKeys, noWithdrawal).State != LicenseState.Valid && !License.Write("a<b>|c", good));

                var all5 = new[] { LicenseState.Missing, LicenseState.NotValid, LicenseState.OtherProgram, LicenseState.Revoked, LicenseState.Unreadable };
                v("caption: 'Personal license' for every invalid state", all5.All(s => AppLicense.LicenseCaption(new LicenseResult { State = s, Holder = "Rossi Srl" }) == "Personal license"));
                v("caption: 'Commercial license: Rossi Srl' with a valid one", AppLicense.LicenseCaption(b) == "Commercial license: Rossi Srl");
                v("phrases: every problem is explained in simple words", all5.Skip(1).All(s => !string.IsNullOrEmpty(AppLicense.Problem(new LicenseResult { State = s }, "This license"))));
                v("phrases: no problem phrase if there is no problem", AppLicense.Problem(b, "x") == null && AppLicense.Problem(new LicenseResult(), "x") == null);
                v("phrases: says which program it is for", AppLicense.Problem(at, "This license").Contains(WM));
                v("phrases: the date as day/month/year", AppLicense.Detail(b).Contains("03/11/2026") && AppLicense.Detail(b).Contains("12"));
                v("phrases: says where to buy, without a long dash", AppLicense.Sale == "The commercial license will be on sale soon at www.nscent.art/en/software; meanwhile, write to info@nscent.art." && !(AppLicense.Sale + AppLicense.Limits).Contains("\u2014"));

                Dialog.Silent = true;
                AppLicense.Keys = publisherKeys;
                string window = Path.Combine(temp, "window"), windowLicense = Path.Combine(window, License.FilePath);
                Directory.CreateDirectory(window);
                var w = new LicenseDialog(window) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                w.Show();
                v("window: starts from 'Personal license'", w.StateText == "Personal license" && !w.RemoveButton.Enabled);
                w.UseButton.PerformClick();
                v("window: empty box, asks to paste", w.ResultText.Contains("Paste") && !File.Exists(windowLicense) && !w.Changed);
                w.Box.Text = "random text"; w.UseButton.PerformClick();
                v("window: random text, says so and does not save", w.ResultText.Contains("is not valid") && !File.Exists(windowLicense) && w.StateText == "Personal license");
                w.Box.Text = new string('A', 100000); w.UseButton.PerformClick();
                v("window: very long text, says so and does not save", w.ResultText.Contains("is not valid") && !File.Exists(windowLicense));
                w.Box.Text = one.Text(WM, 40, "Rossi Srl", "2026-11-03"); w.UseButton.PerformClick();
                v("window: other program, says so and does not save", w.ResultText.Contains(WM) && !File.Exists(windowLicense));
                w.Box.Text = two.Text(PM, 41, "Rossi Srl", "2026-11-03"); w.UseButton.PerformClick();
                v("window: other key, says so and does not save", w.ResultText.Contains("is not valid") && !File.Exists(windowLicense));
                w.Box.Text = "\r\n " + good + "\r\n"; w.UseButton.PerformClick();
                v("window: good license, saved and shown", File.Exists(windowLicense) && w.StateText == "Commercial license: Rossi Srl" && w.Changed && w.RemoveButton.Enabled && w.Box.Text == "");
                w.Box.Text = "random text"; w.UseButton.PerformClick();
                v("window: a wrong text does not throw away the good one", AppLicense.Now(window).Valid && w.ResultText.Contains("is not valid"));
                w.AskRemove = () => false; w.RemoveButton.PerformClick();
                v("window: remove asks, and on no it stays", File.Exists(windowLicense) && w.StateText.StartsWith("Commercial license"));
                w.AskRemove = () => true; w.RemoveButton.PerformClick();
                v("window: on yes it removes it", !File.Exists(windowLicense) && w.StateText == "Personal license" && !w.RemoveButton.Enabled);
                w.Close();
                File.WriteAllText(windowLicense, one.Text(WM, 42, "Rossi Srl", "2026-11-03"));
                w = new LicenseDialog(window) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                w.Show();
                v("window: a saved one from another program is reported, and can be removed", w.StateText == "Personal license" && w.DetailText.Contains(WM) && w.RemoveButton.Enabled);
                w.Close();
                File.WriteAllText(windowLicense, "broken");
                w = new LicenseDialog(window) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                w.Show();
                v("window: a saved broken one is reported, and can be removed", w.StateText == "Personal license" && w.DetailText.Contains("is not valid") && w.RemoveButton.Enabled);
                w.Close();
                File.Delete(windowLicense);

                MainWindow.ScreenshotOnly = true;
                Directory.CreateDirectory(DataStore.Folder);
                string realLicense = Path.Combine(DataStore.Folder, License.FilePath);
                Func<string> part = () => {
                    var p = new MainWindow { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                    p.Show();
                    for (int i = 0; i < 4; i++) { Application.DoEvents(); Thread.Sleep(10); }
                    string t = p.Visible ? p.LicenseText : "not started";
                    p.Close();
                    return t;
                };
                v("the program starts: license missing, 'Personal license'", part() == "Personal license");
                File.WriteAllText(realLicense, "broken");
                v("the program starts: broken license, 'Personal license'", part() == "Personal license");
                File.WriteAllBytes(realLicense, dirty);
                v("the program starts: file of random bytes, 'Personal license'", part() == "Personal license");
                using (var g = new FileStream(realLicense, FileMode.Create)) g.SetLength(50 * 1024 * 1024);
                v("the program starts: huge file, 'Personal license'", part() == "Personal license");
                File.Delete(realLicense); Directory.CreateDirectory(realLicense);
                v("the program starts: a folder instead of the file, 'Personal license'", part() == "Personal license");
                Directory.Delete(realLicense);
                File.WriteAllText(realLicense, good);
                v("the program starts: good license, the name in the header", part() == "Commercial license: Rossi Srl");
                AppLicense.Keys = new string[0];
                v("the program starts: empty key list, 'Personal license'", part() == "Personal license");
                File.Delete(realLicense);
            }
        } catch (Exception ex) {
            badly++; sb.AppendLine("FAIL unexpected error: " + ex.GetType().Name + ": " + ex.Message);
        } finally {
            AppLicense.Keys = realKeys;
            try { Directory.Delete(temp, true); } catch { }
        }
        sb.AppendLine(tot - badly + " of " + tot + " passed");
        File.WriteAllText(result, sb.ToString(), new UTF8Encoding(false));
        return badly == 0 && tot > 0 ? 0 : 1;
    }

    static string Line(string s) { return new string(s.Select(c => c < ' ' || c > '~' ? '?' : c).ToArray()); }
}
