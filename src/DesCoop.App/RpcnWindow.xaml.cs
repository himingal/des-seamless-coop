using System.Windows;
using DesCoop.Emu;
using DesCoop.Net;

namespace DesCoop.App;

/// <summary>Creates or signs into an RPCN account directly, then writes RPCS3's rpcn.yml.</summary>
public partial class RpcnWindow : Window
{
    readonly Rpcs3Manager _emu;
    string _npid = "", _derived = "";

    public RpcnWindow(Rpcs3Manager emu)
    {
        InitializeComponent();
        Ui.DarkTitleBar(this);
        _emu = emu;
        ApplyLanguage();
        var (npid, _, _) = emu.RpcnAccount();
        if (!string.IsNullOrEmpty(npid)) { TxtUser.Text = npid; RbSignIn.IsChecked = true; }
    }

    void ApplyLanguage()
    {
        Title = Loc.T("lblRpcn");
        LblTitle.Text = Loc.T("rpcnTitle");
        LblBlurb.Text = Loc.T("rpcnBlurb");
        RbCreate.Content = Loc.T("rpcnCreate");
        RbSignIn.Content = Loc.T("rpcnHaveOne");
        LblUser.Text = Loc.T("rpcnUser");
        LblEmail.Text = Loc.T("rpcnEmail");
        LblPass.Text = Loc.T("password");
        LblPass2.Text = Loc.T("rpcnPass2");
        LblTokenSignIn.Text = Loc.T("rpcnTokenSignIn");
        LblTokenTitle.Text = Loc.T("rpcnCreated");
        LblTokenBlurb.Text = Loc.T("rpcnPasteToken");
        BtnResend.Content = Loc.T("rpcnResend");
        BtnConfirm.Content = Loc.T("rpcnConfirm");
        BtnGo.Content = Loc.T(RbCreate.IsChecked == true ? "rpcnCreateAccount" : "rpcnSignInBtn");
    }

    void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (BtnGo == null) return;
        bool create = RbCreate.IsChecked == true;
        EmailRow.Visibility = ConfirmRow.Visibility = create ? Visibility.Visible : Visibility.Collapsed;
        TokenRowSignIn.Visibility = create ? Visibility.Collapsed : Visibility.Visible;
        BtnGo.Content = Loc.T(create ? "rpcnCreateAccount" : "rpcnSignInBtn");
        StepForm.Visibility = Visibility.Visible;
        StepToken.Visibility = Visibility.Collapsed;
        TxtMsg.Text = "";
    }

    void Busy(bool on, string msg = "")
    {
        BtnGo.IsEnabled = BtnConfirm.IsEnabled = BtnResend.IsEnabled = RbCreate.IsEnabled = RbSignIn.IsEnabled = !on;
        TxtMsg.Text = msg;
    }

    async void BtnGo_Click(object sender, RoutedEventArgs e)
    {
        var user = TxtUser.Text.Trim();
        if (!RpcnClient.IsValidUsername(user)) { TxtMsg.Text = Loc.T("rpcnVUser"); return; }
        if (TxtPass.Password.Length < 4) { TxtMsg.Text = Loc.T("rpcnVPass"); return; }
        bool create = RbCreate.IsChecked == true;
        if (create)
        {
            if (!TxtEmail.Text.Contains('@')) { TxtMsg.Text = Loc.T("rpcnVEmail"); return; }
            if (TxtPass.Password != TxtPass2.Password) { TxtMsg.Text = Loc.T("rpcnVMatch"); return; }
        }
        string token = TxtTokenSignIn.Text.Trim();
        if (!create && token.Length > 0 && !RpcnClient.IsValidToken(token)) { TxtMsg.Text = Loc.T("rpcnVToken"); return; }

        Busy(true, Loc.T("rpcnTalking"));
        try
        {
            string pass = TxtPass.Password, email = TxtEmail.Text.Trim();
            _derived = await Task.Run(() => RpcnClient.DerivePassword(pass));
            _npid = user;
            await using var c = await RpcnClient.ConnectAsync();
            if (create)
            {
                var r = await c.CreateAccountAsync(user, _derived, email);
                if (r != RpcnClient.Error.NoError) { Busy(false, RpcnClient.Describe(r)); return; }
                _emu.SaveRpcnAccount(user, _derived, "");
                StepForm.Visibility = Visibility.Collapsed;
                StepToken.Visibility = Visibility.Visible;
                Busy(false, "");
                TxtToken.Focus();
            }
            else
            {
                var r = await c.LoginAsync(user, _derived, token);
                if (r is not (RpcnClient.Error.NoError or RpcnClient.Error.LoginAlreadyLoggedIn)) { Busy(false, RpcnClient.Describe(r)); return; }
                _emu.SaveRpcnAccount(user, _derived, token);
                Done();
            }
        }
        catch (Exception ex) { Busy(false, Loc.T("rpcnUnreachable", ex.Message)); }
    }

    async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var token = TxtToken.Text.Trim();
        if (!RpcnClient.IsValidToken(token)) { TxtMsg.Text = Loc.T("rpcnVToken"); return; }
        Busy(true, Loc.T("rpcnCheckToken"));
        try
        {
            await using var c = await RpcnClient.ConnectAsync();
            var r = await c.LoginAsync(_npid, _derived, token);
            if (r is not (RpcnClient.Error.NoError or RpcnClient.Error.LoginAlreadyLoggedIn)) { Busy(false, RpcnClient.Describe(r)); return; }
            _emu.SaveRpcnAccount(_npid, _derived, token);
            Done();
        }
        catch (Exception ex) { Busy(false, Loc.T("rpcnUnreachable", ex.Message)); }
    }

    async void BtnResend_Click(object sender, RoutedEventArgs e)
    {
        Busy(true, Loc.T("rpcnResending"));
        try
        {
            await using var c = await RpcnClient.ConnectAsync();
            var r = await c.ResendTokenAsync(_npid, _derived);
            Busy(false, r == RpcnClient.Error.NoError ? Loc.T("rpcnResent") : RpcnClient.Describe(r));
        }
        catch (Exception ex) { Busy(false, Loc.T("rpcnUnreachable", ex.Message)); }
    }

    void Done()
    {
        Ui.Info(this, Loc.T("rpcnDone", _npid));
        DialogResult = true;
    }
}
