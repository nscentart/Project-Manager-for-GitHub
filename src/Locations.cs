using System.IO;

public class Locations {
    public string Folder, MenuStart, Key, ExeName = "ProjectManager.exe";
    public string Exe { get { return Path.Combine(Folder, ExeName); } }
}
