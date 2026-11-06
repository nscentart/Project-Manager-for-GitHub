using System;
using System.Threading;
using System.Windows.Forms;

public class WriteGateway {
    public Func<Client> Route;
    public string Login;
    public IGateway Gateway;
    public Func<DeviceCode> AskDelete;
    public Func<DeviceCode, Func<bool>, string> WaitDelete;
    public Func<string, Client> Disposable;
    public Action<int> Sleeps = Thread.Sleep;
    public Func<Form, DialogResult> Open = f => f.ShowDialog();

    public static WriteGateway Real(RealGateway gateway, Func<Client> route) {
        return new WriteGateway {
            Route = route, Gateway = gateway, Disposable = t => Client.Real(t, false),
            AskDelete = () => AccountAccess.Ask(AccountAccess.ClientId, AccountAccess.DeletePermission, AccountAccess.Post),
            WaitDelete = (c, cancelled) => AccountAccess.Wait(c, AccountAccess.ClientId, AccountAccess.Post, s => { for (int i = 0; i < s * 5 && !cancelled(); i++) Thread.Sleep(200); }, cancelled) };
    }
}
