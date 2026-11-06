public class LicenseResult {
    public LicenseState State;
    public string Program = "", Holder = "", Data = "";
    public int Number;
    public bool Valid { get { return State == LicenseState.Valid; } }
}
