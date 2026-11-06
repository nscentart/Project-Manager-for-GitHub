using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

public class RealGateway : IGateway {
    public bool Browser { get { return AccountAccess.ClientId.Length > 0; } }
    public bool Git { get { return AccountAccess.FindGit() != null; } }
    public DeviceCode Ask() { return AccountAccess.Ask(AccountAccess.ClientId, AccountAccess.Post); }
    public string Wait(DeviceCode c, Func<bool> cancelled) {
        return AccountAccess.Wait(c, AccountAccess.ClientId, AccountAccess.Post, s => { for (int i = 0; i < s * 5 && !cancelled(); i++) Thread.Sleep(200); }, cancelled);
    }
    public string GitFrom() { return GitHub.GitFrom(null); }
    public string LoginOf(string token) { GitHub.Use(token, null); return GitHub.LoginOf(); }
    public List<RepoInfo> RepoOf(string token, string login) { GitHub.Use(token, login); return GitHub.Repo(); }
    public List<string> OrganizationsOf(string token, string login) { GitHub.Use(token, login); return GitHub.Organizations(); }
    public void Store(string login, string token) {
        Writer.CheckKey(Client.Real(token, true));
        CredentialStore.Write(CredentialStore.NameOf(login), login, token);
    }
    public void Copy(string text) { try { Clipboard.SetText(text); } catch (Exception ex) { DataStore.Log("clipboard: " + ex.Message); } }
    public void Open(string page) {
        if (!page.StartsWith("https://github.com/")) return;
        try { Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + page + "\""); } catch (Exception ex) { DataStore.Log("browser: " + ex.Message); }
    }
}
