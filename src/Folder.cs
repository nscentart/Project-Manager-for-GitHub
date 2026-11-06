using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

public class Folder {
    public string Root;
    public List<FileToUpload> File = new List<FileToUpload>();
    public List<string> Skipped = new List<string>();
    public List<string> Rejected = new List<string>();
    public List<Finding> Findings = new List<Finding>();
    public List<string> ToConfirm = new List<string>();
    public HashSet<string> Confirmed = new HashSet<string>();
    public List<string> NotReadable = new List<string>();
    public List<Finding> Notices = new List<Finding>();
    public long ByteSize;
    public bool Blocked { get { return Rejected.Count > 0 || Findings.Count > 0; } }
}
