public class Owner {
    public string Name, Because;
    public bool Organization;
    public override string ToString() { return Name + (Organization ? Language.T("writes.organization") : Language.T("writes.personal")); }
}
