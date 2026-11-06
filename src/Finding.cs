public class Finding {
    public string File, What;
    public int Line;
    public override string ToString() { return File + (Line > 0 ? Language.T("screener.line", "line", Line) : "") + ": " + What; }
}
