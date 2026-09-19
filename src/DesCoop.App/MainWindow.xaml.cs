using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesCoop.Emu;
using DesCoop.Game;
using DesCoop.Net;
using DesCoop.Party;
using DesCoop.Server;
using Microsoft.Win32;

namespace DesCoop.App;

public sealed record PlayerRow(string Name, string Area, string Badge);

public partial class MainWindow : Window
{
    readonly AppSettings _s = AppSettings.Load();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    Rpcs3Manager _emu;
    GameInfo? _game;
    PartyHost? _host;
    string? _joinedAddress;
    PartyClient? _client;
    bool _busy, _loading = true, _closingConfirmed, _refreshing;

    public MainWindow(string? initialGame = null)
    {
        InitializeComponent();
        Ui.DarkTitleBar(this);
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (ver != null) TxtVersion.Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}";
        _emu = new Rpcs3Manager(_s.EffectiveRpcs3Dir);
        _game = GameLocator.Resolve(_s.GamePath);
        // First launch straight from the installer: it passes the game folder the user picked, so it is
        // remembered without a separate setup step.
        if (_game == null && !string.IsNullOrWhiteSpace(initialGame) && GameLocator.Resolve(initialGame) is { } g0)
        {
            _game = g0;
            _s.GamePath = g0.Root;
            _s.Save();
        }
        _joinedAddress = _s.JoinedAddress;

        // Tweaks are a fixed, always-applied set (no UI for them).
        _s.Patch = new PatchOptions();

        // Language: first run detects the Windows language (pt/es, else English); a dropdown changes it.
        if (string.IsNullOrEmpty(_s.Language))
        {
            var two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _s.Language = two is "pt" or "es" ? two : "en";
        }
        Loc.Lang = _s.Language;
        CmbLang.ItemsSource = Loc.Languages;
        CmbLang.SelectedValue = _s.Language;
        ApplyLanguage();

        (_s.WorldTendency switch
        {
            >= 200 => RbWtPureWhite,
            >= 100 => RbWtWhite,
            <= -200 => RbWtPureBlack,
            <= -100 => RbWtBlack,
            _ => RbWtNormal,
        }).IsChecked = true;
        ChkUpnp.IsChecked = _s.UseUpnp;
        // Default party name = your RPCN name, so the friend already knows it.
        var rpcnName = _emu.IsInstalled ? _emu.RpcnUser() : null;
        TxtPartyName.Text = rpcnName != null && _s.PartyName.EndsWith("'s Party") ? rpcnName : _s.PartyName;
        TxtPartyPass.Text = _s.PartyPassword;
        TxtJoinName.Text = _s.JoinName ?? "";
        TxtJoinPass.Text = _s.JoinPassword ?? "";
        switch (_s.Mode)
        {
            case PartyMode.Host: RbHost.IsChecked = true; break;
            case PartyMode.Join: RbJoin.IsChecked = true; break;
            case PartyMode.Public: RbPublic.IsChecked = true; break;
        }

        _loading = false;
        _timer.Tick += async (_, _) =>
        {
            try { UpdateGameIndicator(); await RefreshPartyAsync(); }
            catch (Exception ex) { Log("Warning: " + ex.Message); }
        };
        _timer.Start();
        RefreshAll();

        // First run: offer the RPCN account right away (the only thing the installer can't do for you).
        Loaded += (_, _) => Dispatcher.BeginInvoke(async () =>
        {
            if (_emu.IsInstalled && _emu.RpcnUser() == null) OpenRpcn();
            // Fully automatic: reopen the party (or rejoin it) the way it was last time.
            try
            {
                if (_s.Mode == PartyMode.Host && _emu.IsInstalled && _game != null)
                    await RunBusy(Loc.T("stOpeningParty"), async _ => await StartHostAsync());
                else if (_s.Mode == PartyMode.Join && !string.IsNullOrWhiteSpace(_s.JoinName))
                    await RunBusy(Loc.T("stRejoining"), async _ => await JoinAsync());
            }
            catch { }
        }, DispatcherPriority.ApplicationIdle);
    }

    void CmbLang_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading || CmbLang.SelectedValue is not string code) return;
        Loc.Lang = code;
        _s.Language = code;
        ApplyLanguage();
        RefreshAll();
        UpdateGameIndicator();
        SaveSettings();
    }

    void ApplyLanguage()
    {
        TxtSubtitle.Text = Loc.T("subtitle");
        LblNexus.Text = Loc.T("theNexus");
        LblYourParty.Text = Loc.T("yourParty");
        LblRpcs3Emu.Text = Loc.T("rpcs3Emulator");
        LblFwTitle.Text = Loc.T("ps3Firmware");
        LblRpcn.Text = Loc.T("lblRpcn");
        BtnRpcs3Pick.Content = Loc.T("browse");
        BtnGame.Content = Loc.T("browse");
        BtnFw.Content = Loc.T("install");
        RbHost.Content = Loc.T("host");
        RbJoin.Content = Loc.T("join");
        RbPublic.Content = Loc.T("public");
        RbWtPureWhite.Content = Loc.T("wtPureWhite");
        RbWtWhite.Content = Loc.T("wtWhite");
        RbWtNormal.Content = Loc.T("wtNormal");
        RbWtBlack.Content = Loc.T("wtBlack");
        RbWtPureBlack.Content = Loc.T("wtPureBlack");
        TxtNoneHint.Text = Loc.T("noneHint");
        LblPartyName.Text = Loc.T("partyName");
        LblPartyPass.Text = Loc.T("password");
        LblJoinName.Text = Loc.T("joinName");
        LblJoinPass.Text = Loc.T("password");
        ChkUpnp.Content = Loc.T("upnp");
        LblWorldTendency.Text = Loc.T("worldTendency");
        LblTellFriend.Text = Loc.T("tellFriend");
        BtnCopy.Content = Loc.T("copy");
        BtnHost.Content = Loc.T(_host == null ? "createParty" : "closeParty");
        BtnJoin.Content = Loc.T("joinBtn");
        if (_client == null) TxtJoinInfo.Text = Loc.T("joinInfo");
        TxtPublicInfo.Text = Loc.T("publicInfo");
        LblPhantoms.Text = Loc.T("phantoms");
        TxtNoPlayers.Text = Loc.T("noPlayers");
        LblHowTo.Text = Loc.T("howTo");
        TxtHowTo.Text = Loc.T("howToLines");
        CmbLang.ToolTip = Loc.T("language");
        if (TxtStatus.Text is "Ready." or "Pronto." or "Listo.") TxtStatus.Text = Loc.T("ready");
    }

    // ------------------------------------------------------------------ helpers

    void Log(string line) => Dispatcher.BeginInvoke(() =>
    {
        TxtLog.AppendText((TxtLog.Text.Length > 0 ? "\n" : "") + line);
        if (TxtLog.LineCount > 400) TxtLog.Text = string.Join("\n", TxtLog.Text.Split('\n').TakeLast(300));
        TxtLog.ScrollToEnd();
    });

    void Status(string text, double? progress = null)
    {
        TxtStatus.Text = text;
        Progress.Visibility = progress.HasValue ? Visibility.Visible : Visibility.Hidden;
        if (progress.HasValue) Progress.Value = progress.Value;
    }

    Brush B(string key) => (Brush)FindResource(key);

    void SaveSettings()
    {
        if (_loading) return;
        // Game tweaks are a fixed set (not user-selectable); _s.Patch stays at its defaults.
        _s.UseUpnp = ChkUpnp.IsChecked == true;
        _s.PartyName = string.IsNullOrWhiteSpace(TxtPartyName.Text) ? _s.PartyName : TxtPartyName.Text.Trim();
        if (!string.IsNullOrWhiteSpace(TxtPartyPass.Text)) _s.PartyPassword = TxtPartyPass.Text.Trim();
        _s.JoinName = TxtJoinName.Text.Trim();
        _s.JoinPassword = TxtJoinPass.Text.Trim();
        _s.JoinedAddress = _joinedAddress;
        _s.Save();
    }

    async Task RunBusy(string what, Func<IProgress<DownloadProgress>, Task> work)
    {
        if (_busy) return;
        _busy = true;
        SetButtons(false);
        var progress = new Progress<DownloadProgress>(p => Status(p.Stage, p.Fraction));
        try
        {
            Status(what, 0);
            await work(progress);
            Status(Loc.T("ready"));
        }
        catch (Exception ex)
        {
            Status(Loc.T("stError", ex.Message));
            Log("ERROR: " + ex);
            Ui.Warn(this, ex.Message);
        }
        finally
        {
            _busy = false;
            SetButtons(true);
            RefreshAll();
        }
    }

    void SetButtons(bool on)
    {
        foreach (var b in new[] { BtnRpcs3Install, BtnRpcs3Pick, BtnFw, BtnGame, BtnRpcn, BtnHost, BtnJoin, BtnPlay })
            b.IsEnabled = on;
    }

    // ------------------------------------------------------------------ setup state

    void RefreshAll()
    {
        bool emu = _emu.IsInstalled;
        DotRpcs3.Fill = B(emu ? "Ok" : "Bad");
        TxtRpcs3.Text = emu ? _emu.Root : Loc.T("rpcs3Missing");
        BtnRpcs3Install.Content = Loc.T("getRpcs3");

        bool fw = emu && _emu.FirmwareInstalled;
        DotFw.Fill = B(fw ? "Ok" : "Bad");
        TxtFw.Text = fw ? Loc.T("fwInstalled") : emu ? Loc.T("fwDownload") : Loc.T("fwNeedRpcs3");
        BtnFw.IsEnabled = !_busy && emu && !fw;

        DotGame.Fill = B(_game != null ? (GameLocator.IsDemonsSouls(_game) ? "Ok" : "Warn") : "Bad");
        TxtGame.Text = _game != null
            ? $"{_game.Title}  [{_game.Serial}]  v{_game.Version}\n{_game.Root}" + (GameLocator.IsDemonsSouls(_game) ? "" : Loc.T("gameNotDeS"))
            : Loc.T("gameHint");

        var rpcn = emu ? _emu.RpcnUser() : null;
        DotRpcn.Fill = B(rpcn != null ? "Ok" : "Bad");
        TxtRpcn.Text = rpcn != null ? Loc.T("rpcnSignedIn", rpcn) : Loc.T("rpcnHint");
        BtnRpcn.Content = Loc.T(rpcn != null ? "change" : "rpcnBtn");
        BtnRpcn.IsEnabled = !_busy && emu;
    }

    // ------------------------------------------------------------------ setup actions

    // RPCS3 is no longer downloaded by the app: it opens the official download page so the user grabs the
    // build they want, then points to it with Browse. (The only thing the app downloads is the PS3 firmware.)
    void BtnRpcs3Install_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://rpcs3.net/download") { UseShellExecute = true }); }
        catch { }
    }

    void BtnRpcs3Pick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("dgTitleRpcs3") };
        if (dlg.ShowDialog(this) != true) return;
        if (!Rpcs3Manager.LooksLikeRpcs3(dlg.FolderName)) { Ui.Warn(this, Loc.T("dgNoRpcs3Exe")); return; }
        _s.Rpcs3Dir = dlg.FolderName;
        _emu = new Rpcs3Manager(dlg.FolderName);
        SaveSettings();
        RefreshAll();
    }

    async void BtnFw_Click(object sender, RoutedEventArgs e) =>
        await RunBusy(Loc.T("stInstallFw"), p => _emu.InstallFirmwareAsync(p));

    void BtnGame_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("dgTitleGame") };
        if (dlg.ShowDialog(this) != true) return;
        var g = GameLocator.Resolve(dlg.FolderName);
        if (g == null)
        {
            Ui.Warn(this, Loc.T("dgNoGame"));
            return;
        }
        _game = g;
        _s.GamePath = g.Root;
        SaveSettings();
        if (_emu.IsInstalled) _emu.RegisterGame(g);
        Log($"Game: {g.Title} [{g.Serial}] at {g.Root}");
        RefreshAll();
    }

    void Tendency_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton rb || !int.TryParse(rb.Tag?.ToString(), out int v)) return;
        _s.WorldTendency = v;
        if (_host != null)
        {
            _host.Server.Options.WorldTendency = v;
            Log($"World tendency set to {rb.Content} (applies when the game syncs with the server).");
        }
        SaveSettings();
    }

    bool? _gameShown;

    /// <summary>Header badge + footer line showing whether Demon's Souls is running.</summary>
    void UpdateGameIndicator()
    {
        bool running = _emu.IsInstalled && _emu.IsRunning();
        var brush = B(running ? "Ok" : "Muted");
        DotGameTop.Fill = DotGame2.Fill = brush;
        TxtGameTop.Text = (running ? "▶ " : "● ") + (running ? Loc.T("gameRunning") : Loc.T("gameClosed")).ToUpperInvariant();
        TxtGameTop.Foreground = running ? B("Ok") : B("Muted");
        TxtGame2.Text = running ? Loc.T("gameRunning") : Loc.T("gameClosed");
        TxtGame2.Foreground = TxtGameTop.Foreground;
        _gameShown = running;
    }

    // The game tweaks are applied automatically when you press PLAY (there is no UI for them).
    void ApplyPatch()
    {
        var r = GamePatcher.Apply(_game!, _s.Patch);
        foreach (var l in r.Lines) Log(l);
        if (r.Changed && _emu.IsInstalled) _emu.ClearGameCache();
    }

    void BtnRpcn_Click(object sender, RoutedEventArgs e) => OpenRpcn();

    void OpenRpcn()
    {
        if (!_emu.IsInstalled) return;
        new RpcnWindow(_emu) { Owner = this }.ShowDialog();
        RefreshAll();
    }

    // ------------------------------------------------------------------ party

    void Mode_Changed(object sender, RoutedEventArgs e)
    {
        PanelNone.Visibility = Visibility.Collapsed;
        PanelHost.Visibility = RbHost.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelJoin.Visibility = RbJoin.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelPublic.Visibility = RbPublic.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _s.Mode = RbHost.IsChecked == true ? PartyMode.Host : RbJoin.IsChecked == true ? PartyMode.Join : RbPublic.IsChecked == true ? PartyMode.Public : PartyMode.None;
        SaveSettings();
    }

    async void BtnHost_Click(object sender, RoutedEventArgs e)
    {
        if (_host != null) { await StopHostAsync(); return; }
        SaveSettings();
        await RunBusy(Loc.T("stCreatingParty"), async _ => await StartHostAsync());
    }

    /// <summary>One UAC prompt so Windows does not block the friend (server ports + RPCS3 P2P).</summary>
    void EnsureFirewall()
    {
        if (Firewall.AllConfigured(_emu.Exe)) return;
        Status(Loc.T("stFirewall"));
        if (!Firewall.Configure(Environment.ProcessPath ?? "", _emu.Exe))
            Log("Firewall not configured (prompt declined). If your friend can't connect, allow DesCoop and RPCS3 in Windows Firewall.");
    }

    async Task StartHostAsync()
    {
        EnsureFirewall();
        var opts = new DesServerOptions { ServerName = _s.PartyName, DataDir = Path.Combine(AppSettings.DataDir, "server-data"), WorldTendency = _s.WorldTendency };
        if (string.IsNullOrWhiteSpace(_s.PartyPassword)) _s.PartyPassword = Rendezvous.NewPassword();
        TxtPartyPass.Text = _s.PartyPassword;
        var host = new PartyHost(opts, _s.PartyPassword);
        host.Log += Log;
        host.StateChanged += () => Dispatcher.BeginInvoke(UpdateHostInfo);
        try { await host.StartAsync(_s.UseUpnp); }
        catch (System.Net.Sockets.SocketException)
        {
            await host.DisposeAsync();
            throw new InvalidOperationException("Ports 18000/18666-18668 are already in use. Is another Demon's Souls server (or another copy of this app) running?");
        }
        _host = host;
        TxtInvite.Text = Loc.T("invite", host.Name, host.Password);
        CodeBox.Visibility = Visibility.Visible;
        BtnHost.Content = Loc.T("closeParty");
        UpdateHostInfo();
    }

    void UpdateHostInfo()
    {
        if (_host == null) return;
        var lines = new List<string>
        {
            _host.RelayOk ? Loc.T("relayOn") : Loc.T("relayOff"),
            _host.Published ? Loc.T("listed") : Loc.T("notListed"),
        };
        if (_host.UpnpOk) lines.Add(Loc.T("upnpOpened", _host.PublicIp ?? ""));
        foreach (var a in _host.LocalAddresses.Where(a => a.Kind != "LAN")) lines.Add($"{a.Kind}: {a.Address}");
        lines.Add(Loc.T("keepOpenServer"));
        TxtHostInfo.Text = string.Join("\n", lines);
    }

    async Task StopHostAsync()
    {
        if (_host == null) return;
        var h = _host;
        _host = null;
        await h.DisposeAsync();
        CodeBox.Visibility = Visibility.Collapsed;
        BtnHost.Content = Loc.T("createParty");
        Log("Party closed.");
    }

    void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_host == null) return;
        try
        {
            Clipboard.SetText($"Demon's Souls party: {_host.Name}\nPassword: {_host.Password}\n(DeS Seamless Co-op > Join a Party)");
            Status(Loc.T("stCopied"));
        }
        catch { }
    }

    async void BtnJoin_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        await RunBusy(Loc.T("joinLooking"), async _ => await JoinAsync());
    }

    async Task JoinAsync()
    {
        var name = TxtJoinName.Text.Trim();
        var pass = TxtJoinPass.Text.Trim();
        if (name.Length == 0) throw new InvalidOperationException(Loc.T("joinNeedName"));
        _client?.Dispose();
        _client = null;
        TxtJoinInfo.Text = Loc.T("joinLooking");
        try
        {
            _client = await PartyClient.ConnectAsync(name, pass, _emu.IsInstalled ? _emu.RpcnUser() : null, Log);
        }
        catch
        {
            TxtJoinInfo.Text = Loc.T("joinFailed");
            throw;
        }
        _joinedAddress = _client.Address;
        SaveSettings();
        var via = _client.ViaRelay ? Loc.T("viaRelay") : Loc.T("viaAddr", _client.Address);
        TxtJoinInfo.Text = Loc.T("joinConnected", _client.PartyName, via);
    }

    async Task RefreshPartyAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            ServerStatus? st = null;
            if (_host != null) st = _host.Server.GetStatus();
            else if (RbJoin.IsChecked == true && _client != null) st = await NetUtil.GetStatusAsync(_client.Address);

            var rows = st?.Players.Select(p => new PlayerRow(p.Name, p.Area,
                p.InSession ? "in co-op" : p.HasSign ? "blue sign ready" : "")).ToList() ?? [];
            ListPlayers.ItemsSource = rows;
            TxtNoPlayers.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (!_busy && DateTime.Now.Second % 15 < 3) RefreshAll();
        }
        finally { _refreshing = false; }
    }

    // ------------------------------------------------------------------ play

    async void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        if (!_emu.IsInstalled) { Ui.Info(this, Loc.T("dgNeedRpcs3")); return; }
        if (!_emu.FirmwareInstalled) { Ui.Info(this, Loc.T("dgNeedFw")); return; }
        if (_game == null) { Ui.Info(this, Loc.T("dgNeedGame")); return; }
        if (_s.Mode == PartyMode.None) { Ui.Info(this, Loc.T("dgNeedMode")); return; }
        if (_emu.RpcnUser() == null)
        {
            OpenRpcn();
            if (_emu.RpcnUser() == null && !Ui.Ask(this, Loc.T("dgOfflineAnyway"))) return;
        }
        if (_emu.IsRunning()) { Ui.Info(this, Loc.T("dgRpcs3Running")); return; }

        await RunBusy(Loc.T("stPreparing"), async _ =>
        {
            string target;
            switch (_s.Mode)
            {
                case PartyMode.Host:
                    if (_host == null) await StartHostAsync();
                    target = "127.0.0.1";
                    break;
                case PartyMode.Join:
                    if (_client == null || await NetUtil.HelloAsync(_client.Address, _emu.RpcnUser()) == null) await JoinAsync();
                    target = _client!.Address;
                    break;
                default:
                    target = Rpcs3Manager.ArchstonesIp;
                    break;
            }

            EnsureFirewall();
            Status(Loc.T("stConfiguring"));
            _emu.ConfigureNetwork(target);
            _emu.RegisterGame(_game);
            await _emu.EnableQualityPatchesAsync();
            Status(Loc.T("stPatch"));
            await Task.Run(ApplyPatch);
            Log($"Launching the game. Server: {target}");
            _emu.Launch(_game, _s.Fullscreen);
            Dispatcher.InvokeAsync(async () => { await Task.Delay(4000); UpdateGameIndicator(); });
        });
        Status(Loc.T(_host != null ? "stGameHost" : "stGameSolo"));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveSettings();
        if (_client != null && !_closingConfirmed && _client.ViaRelay && _emu.IsRunning() &&
            !Ui.Ask(this, Loc.T("dgRelayClose")))
        {
            e.Cancel = true;
            return;
        }
        if (_host == null || _closingConfirmed) { _client?.Dispose(); base.OnClosing(e); return; }
        if (!Ui.Ask(this, Loc.T("dgHostClose")))
        {
            e.Cancel = true;
            return;
        }
        // Stop the party first, then close again once this Closing event has fully returned
        // (WPF throws if Close() is called while the window is still closing).
        e.Cancel = true;
        _closingConfirmed = true;
        _ = CloseAfterStoppingAsync();
    }

    async Task CloseAfterStoppingAsync()
    {
        try { await StopHostAsync(); } catch { }
        await Dispatcher.InvokeAsync(Close, DispatcherPriority.Background);
    }
}
