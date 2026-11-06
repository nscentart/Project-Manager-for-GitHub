using System;
using System.Collections.Generic;

public class WriteException : Exception {
    public int Code;
    public List<Finding> Findings;
    public WriteException(string text) : base(text) { }
    public WriteException(int code, string text) : base(text) { Code = code; }
    public WriteException(string text, List<Finding> findings) : base(text) { Findings = findings; }
}
