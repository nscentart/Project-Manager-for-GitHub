using System;
using System.Collections.Generic;

public interface IGateway {
    bool Browser { get; }
    bool Git { get; }
    DeviceCode Ask();
    string Wait(DeviceCode c, Func<bool> cancelled);
    string GitFrom();
    string LoginOf(string token);
    List<RepoInfo> RepoOf(string token, string login);
    List<string> OrganizationsOf(string token, string login);
    void Store(string login, string token);
    void Copy(string text);
    void Open(string page);
}
