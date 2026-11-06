public class Reason {
    public string Code, Explanation, Extracted;
    public override string ToString() { return Explanation + (string.IsNullOrEmpty(Extracted) ? "" : ": «" + Extracted + "»"); }
}
