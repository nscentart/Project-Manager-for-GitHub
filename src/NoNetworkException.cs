using System;

public class NoNetworkException : Exception {
    public NoNetworkException(string text) : base(text) { }
}
