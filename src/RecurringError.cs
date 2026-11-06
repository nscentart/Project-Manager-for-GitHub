using System;
using System.Collections.Generic;

public class RecurringError {
    public string Repo, Signature, Example;
    public int Times;
    public HashSet<string> Reports = new HashSet<string>(), Releases = new HashSet<string>();
    public DateTime First = DateTime.MaxValue, Last = DateTime.MinValue;
}
