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
        var (npid, _, _) = emu.RpcnAccount();
        if (!string.IsNullOrEmpty(npid)) { TxtUser.Text = npid; RbSignIn.IsChecked = true; }
    }

    void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (BtnGo == null) return;
        bool create = RbCreate.IsChecked == true;
        EmailRow.Visibility = ConfirmRow.Visibility = create ? Visibility.Visible : Visibility.Collapsed;
        TokenRowSignIn.Visibility = create ? Visibility.Collapsed : Visibility.Visible;
        BtnGo.Content = create ? "Create Account" : "Sign In";
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
        if (!RpcnClient.IsValidUsername(user)) { TxtMsg.Text = "Username must be 3-16 letters, numbers, - or _."; return; }
        if (TxtPass.Password.Length < 4) { TxtMsg.Text = "Pick a longer password."; return; }
        bool create = RbCreate.IsChecked == true;
        if (create)
        {
            if (!TxtEmail.Text.Contains('@')) { TxtMsg.Text = "Enter a real email: RPCN sends the token there."; return; }
            if (TxtPass.Password != TxtPass2.Password) { TxtMsg.Text = "Passwords don't match."; return; }
        }
        string token = TxtTokenSignIn.Text.Trim();
        if (!create && token.Length > 0 && !RpcnClient.IsValidToken(token)) { TxtMsg.Text = "The token is 16 characters (A-Z, 0-9)."; return; }

        Busy(true, "Talking to RPCN…");
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
        catch (Exception ex) { Busy(false, "Could not reach RPCN: " + ex.Message); }
    }

    async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var token = TxtToken.Text.Trim();
        if (!RpcnClient.IsValidToken(token)) { TxtMsg.Text = "The token is 16 characters (A-Z, 0-9)."; return; }
        Busy(true, "Checking the token…");
        try
        {
            await using var c = await RpcnClient.ConnectAsync();
            var r = await c.LoginAsync(_npid, _derived, token);
            if (r is not (RpcnClient.Error.NoError or RpcnClient.Error.LoginAlreadyLoggedIn)) { Busy(false, RpcnClient.Describe(r)); return; }
            _emu.SaveRpcnAccount(_npid, _derived, token);
            Done();
        }
        catch (Exception ex) { Busy(false, "Could not reach RPCN: " + ex.Message); }
    }

    async void BtnResend_Click(object sender, RoutedEventArgs e)
    {
        Busy(true, "Asking RPCN to resend the email…");
        try
        {
            await using var c = await RpcnClient.ConnectAsync();
            var r = await c.ResendTokenAsync(_npid, _derived);
            Busy(false, r == RpcnClient.Error.NoError ? "Sent. Check your inbox and spam folder." : RpcnClient.Describe(r));
        }
        catch (Exception ex) { Busy(false, "Could not reach RPCN: " + ex.Message); }
    }

    void Done()
    {
        Ui.Info(this, $"You're in, {_npid}. RPCS3 will sign in by itself when the game goes online.");
        DialogResult = true;
    }
}
