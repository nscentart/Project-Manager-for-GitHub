using System;

public class GitHubException : Exception {
    public int Code;
    public bool Limit;
    public GitHubException(int code, string text) : base(text) { Code = code; }
}
