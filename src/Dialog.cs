using System.Windows.Forms;

public class Dialog : Form {
    public static bool Silent;
    protected override bool ShowWithoutActivation { get { return Silent; } }
}
