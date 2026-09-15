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

    public MainWindow()
    {
        InitializeComponent();
        Ui.DarkTitleBar(this);
        _emu = new Rpcs3Manager(_s.EffectiveRpcs3Dir);
        _game = GameLocator.Resolve(_s.GamePath);
        _joinedAddress = _s.JoinedAddress;

        ChkBlue.IsChecked = _s.Patch.BlueEyeStoneInBodyForm;
        ChkEph.IsChecked = _s.Patch.InfiniteEphemeralEyes;
        ChkSoul.IsChecked = _s.Patch.FullHpSoulForm;
        ChkStaySoul.IsChecked = _s.Patch.StayInSoulForm;
        ChkStartBlue.IsChecked = _s.Patch.StartWithBlueEyeStone;
        ChkClasses.IsChecked = _s.Patch.RevampedClasses;
        ChkShops.IsChecked = _s.Patch.CheaperShops;
        ChkBlade.IsChecked = _s.Patch.EasierPureBladestone;
        ChkLoad.IsChecked = _s.Patch.HeavierLoads;
        TxtClasses.ToolTip = string.Join("\n\n", ClassRevamp.Kits.Select(k =>
            $"{k.Title}  ·  Soul Level {k.SoulLevel}\n{k.Pitch}\nVIT {k.Vit}  INT {k.Int}  END {k.End}  STR {k.Str}  DEX {k.Dex}  MAG {k.Mag}  FAI {k.Fai}  LCK {k.Luc}"));
        (_s.WorldTendency switch
        {
            >= 200 => RbWtPureWhite,
            >= 100 => RbWtWhite,
            <= -200 => RbWtPureBlack,
            <= -100 => RbWtBlack,
            _ => RbWtNormal,
        }).IsChecked = true;
        ChkUpnp.IsChecked = _s.UseUpnp;
        ChkFullscreen.IsChecked = _s.Fullscreen;
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
                    await RunBusy("Opening your party…", async _ => await StartHostAsync());
                else if (_s.Mode == PartyMode.Join && !string.IsNullOrWhiteSpace(_s.JoinName))
                    await RunBusy("Rejoining the party…", async _ => await JoinAsync());
            }
            catch { }
        }, DispatcherPriority.ApplicationIdle);
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
        _s.Patch.BlueEyeStoneInBodyForm = ChkBlue.IsChecked == true;
        _s.Patch.InfiniteEphemeralEyes = ChkEph.IsChecked == true;
        _s.Patch.FullHpSoulForm = ChkSoul.IsChecked == true;
        _s.Patch.StayInSoulForm = ChkStaySoul.IsChecked == true;
        _s.Patch.StartWithBlueEyeStone = ChkStartBlue.IsChecked == true;
        _s.Patch.RevampedClasses = ChkClasses.IsChecked == true;
        _s.Patch.CheaperShops = ChkShops.IsChecked == true;
        _s.Patch.EasierPureBladestone = ChkBlade.IsChecked == true;
        _s.Patch.HeavierLoads = ChkLoad.IsChecked == true;
        _s.UseUpnp = ChkUpnp.IsChecked == true;
        _s.Fullscreen = ChkFullscreen.IsChecked == true;
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
            Status("Ready.");
        }
        catch (Exception ex)
        {
            Status("Error: " + ex.Message);
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
        foreach (var b in new[] { BtnRpcs3Install, BtnRpcs3Pick, BtnFw, BtnGame, BtnPatch, BtnUnpatch, BtnRpcn, BtnHost, BtnJoin, BtnPlay })
            b.IsEnabled = on;
    }

    // ------------------------------------------------------------------ setup state

    void RefreshAll()
    {
        bool emu = _emu.IsInstalled;
        DotRpcs3.Fill = B(emu ? "Ok" : "Bad");
        TxtRpcs3.Text = emu ? _emu.Root : "Download the latest official build, or point to your own RPCS3.";
        BtnRpcs3Install.Content = emu ? "Update" : "Download";

        bool fw = emu && _emu.FirmwareInstalled;
        DotFw.Fill = B(fw ? "Ok" : "Bad");
        TxtFw.Text = fw ? "Installed." : emu ? "Downloads the official firmware straight from Sony." : "Install RPCS3 first.";
        BtnFw.IsEnabled = !_busy && emu && !fw;

        DotGame.Fill = B(_game != null ? (GameLocator.IsDemonsSouls(_game) ? "Ok" : "Warn") : "Bad");
        TxtGame.Text = _game != null
            ? $"{_game.Title}  [{_game.Serial}]  v{_game.Version}\n{_game.Root}" + (GameLocator.IsDemonsSouls(_game) ? "" : "\nThis doesn't look like Demon's Souls.")
            : "Your own game dump: the folder that contains PS3_GAME.";

        bool patched = _game != null && GamePatcher.IsPatched(_game);
        DotPatch.Fill = B(_game == null ? "Bad" : patched ? "Ok" : "Warn");
        TxtPatch.Text = _game == null ? "" : patched ? "Applied (original files backed up)." : "Not applied yet. PLAY applies it automatically.";
        BtnPatch.IsEnabled = BtnUnpatch.IsEnabled = !_busy && _game != null;
        BtnUnpatch.Visibility = patched ? Visibility.Visible : Visibility.Collapsed;

        var rpcn = emu ? _emu.RpcnUser() : null;
        DotRpcn.Fill = B(rpcn != null ? "Ok" : "Bad");
        TxtRpcn.Text = rpcn != null ? $"Signed in as {rpcn}." : "Free account needed for online play (RPCS3's PlayStation Network).";
        BtnRpcn.Content = rpcn != null ? "Change" : "Create / Sign in";
        BtnRpcn.IsEnabled = !_busy && emu;
    }

    // ------------------------------------------------------------------ setup actions

    async void BtnRpcs3Install_Click(object sender, RoutedEventArgs e)
    {
        if (_emu.IsRunning()) { Ui.Info(this, "Close RPCS3 first."); return; }
        _emu = new Rpcs3Manager(_s.EffectiveRpcs3Dir);
        await RunBusy("Downloading RPCS3…", async p =>
        {
            await _emu.InstallLatestAsync(p);
            Log("RPCS3 installed at " + _emu.Root);
            if (!_emu.FirmwareInstalled)
            {
                await _emu.InstallFirmwareAsync(p);
                Log("Firmware installed.");
            }
        });
    }

    void BtnRpcs3Pick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Folder that contains rpcs3.exe" };
        if (dlg.ShowDialog(this) != true) return;
        if (!Rpcs3Manager.LooksLikeRpcs3(dlg.FolderName)) { Ui.Warn(this, "rpcs3.exe was not found in that folder."); return; }
        _s.Rpcs3Dir = dlg.FolderName;
        _emu = new Rpcs3Manager(dlg.FolderName);
        SaveSettings();
        RefreshAll();
    }

    async void BtnFw_Click(object sender, RoutedEventArgs e) =>
        await RunBusy("Installing firmware…", p => _emu.InstallFirmwareAsync(p));

    void BtnGame_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Demon's Souls folder (the one that contains PS3_GAME)" };
        if (dlg.ShowDialog(this) != true) return;
        var g = GameLocator.Resolve(dlg.FolderName);
        if (g == null)
        {
            Ui.Warn(this, "PS3_GAME/USRDIR/EBOOT.BIN was not found there.\nPick your game dump folder (e.g. BLUS30443).");
            return;
        }
        _game = g;
        _s.GamePath = g.Root;
        SaveSettings();
        if (_emu.IsInstalled) _emu.RegisterGame(g);
        Log($"Game: {g.Title} [{g.Serial}] at {g.Root}");
        RefreshAll();
    }

    void PatchOption_Changed(object sender, RoutedEventArgs e) => SaveSettings();

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
        if (_gameShown == running) return;
        _gameShown = running;
        var brush = B(running ? "Ok" : "Muted");
        DotGameTop.Fill = DotGame2.Fill = brush;
        TxtGameTop.Text = running ? "GAME RUNNING" : "GAME CLOSED";
        TxtGameTop.Foreground = running ? B("Ok") : B("Muted");
        TxtGame2.Text = running ? "Demon's Souls is running" : "Demon's Souls is closed";
        TxtGame2.Foreground = TxtGameTop.Foreground;
    }

    async void BtnPatch_Click(object sender, RoutedEventArgs e)
    {
        if (_game == null) return;
        SaveSettings();
        await RunBusy("Applying patch…", async _ => await Task.Run(ApplyPatch));
    }

    void ApplyPatch()
    {
        var r = GamePatcher.Apply(_game!, _s.Patch);
        foreach (var l in r.Lines) Log(l);
        if (r.Changed && _emu.IsInstalled) _emu.ClearGameCache();
    }

    async void BtnUnpatch_Click(object sender, RoutedEventArgs e)
    {
        if (_game == null) return;
        await RunBusy("Restoring original files…", async _ => await Task.Run(() =>
        {
            GamePatcher.Restore(_game);
            if (_emu.IsInstalled) _emu.ClearGameCache();
            Log("Original game files restored.");
        }));
        ChkBlue.IsChecked = ChkEph.IsChecked = ChkSoul.IsChecked = ChkStaySoul.IsChecked = ChkStartBlue.IsChecked = ChkClasses.IsChecked =
            ChkShops.IsChecked = ChkBlade.IsChecked = ChkLoad.IsChecked = false;
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
        await RunBusy("Creating party…", async _ => await StartHostAsync());
    }

    /// <summary>One UAC prompt so Windows does not block the friend (server ports + RPCS3 P2P).</summary>
    void EnsureFirewall()
    {
        if (Firewall.AllConfigured(_emu.Exe)) return;
        Status("Allowing through Windows Firewall (accept the Windows prompt)…");
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
        TxtInvite.Text = $"Party: {host.Name}     Password: {host.Password}";
        CodeBox.Visibility = Visibility.Visible;
        BtnHost.Content = "Close Party";
        UpdateHostInfo();
    }

    void UpdateHostInfo()
    {
        if (_host == null) return;
        var lines = new List<string>
        {
            _host.RelayOk ? "Relay online: your friend can join from anywhere, no VPN needed." : "Relay offline: only LAN/VPN/open-port connections will work.",
            _host.Published ? "Party listed: your friend types the name and password in \"Join a Party\"." : "Party not listed online yet.",
        };
        if (_host.UpnpOk) lines.Add($"Router ports opened via UPnP ({_host.PublicIp}).");
        foreach (var a in _host.LocalAddresses.Where(a => a.Kind != "LAN")) lines.Add($"{a.Kind}: {a.Address}");
        lines.Add("Keep this app open while you play: it is the party server.");
        TxtHostInfo.Text = string.Join("\n", lines);
    }

    async Task StopHostAsync()
    {
        if (_host == null) return;
        var h = _host;
        _host = null;
        await h.DisposeAsync();
        CodeBox.Visibility = Visibility.Collapsed;
        BtnHost.Content = "Create Party";
        Log("Party closed.");
    }

    void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_host == null) return;
        try
        {
            Clipboard.SetText($"Demon's Souls party: {_host.Name}\nPassword: {_host.Password}\n(DeS Seamless Co-op > Join a Party)");
            Status("Copied. Send it to your friend (Discord, WhatsApp…).");
        }
        catch { }
    }

    async void BtnJoin_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        await RunBusy("Looking for the party…", async _ => await JoinAsync());
    }

    async Task JoinAsync()
    {
        var name = TxtJoinName.Text.Trim();
        var pass = TxtJoinPass.Text.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Type the host's party name and password.");
        _client?.Dispose();
        _client = null;
        TxtJoinInfo.Text = "Looking for the party…";
        try
        {
            _client = await PartyClient.ConnectAsync(name, pass, _emu.IsInstalled ? _emu.RpcnUser() : null, Log);
        }
        catch
        {
            TxtJoinInfo.Text = "Could not join.";
            throw;
        }
        _joinedAddress = _client.Address;
        SaveSettings();
        TxtJoinInfo.Text = $"Connected to \"{_client.PartyName}\"" + (_client.ViaRelay ? " through the relay" : $" via {_client.Address}") +
                           ". Now press PLAY and keep this app open.";
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
        if (!_emu.IsInstalled) { Ui.Info(this, "Install RPCS3 first (Download)."); return; }
        if (!_emu.FirmwareInstalled) { Ui.Info(this, "Install the PS3 firmware first."); return; }
        if (_game == null) { Ui.Info(this, "Pick your Demon's Souls folder first."); return; }
        if (_s.Mode == PartyMode.None) { Ui.Info(this, "Choose one: Host a Party, Join a Party or Public Server."); return; }
        if (_emu.RpcnUser() == null)
        {
            OpenRpcn();
            if (_emu.RpcnUser() == null && !Ui.Ask(this, "No RPCN account yet, so online co-op won't work.\n\nPlay offline anyway?")) return;
        }
        if (_emu.IsRunning()) { Ui.Info(this, "RPCS3 is already open. Close it so the app can apply the network settings before launching."); return; }

        await RunBusy("Preparing…", async _ =>
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
            Status("Configuring RPCS3…");
            _emu.ConfigureNetwork(target);
            _emu.RegisterGame(_game);
            await _emu.EnableQualityPatchesAsync();
            var o = _s.Patch;
            if (o.BlueEyeStoneInBodyForm || o.InfiniteEphemeralEyes || o.FullHpSoulForm || o.StayInSoulForm || o.StartWithBlueEyeStone || o.RevampedClasses
                || o.CheaperShops || o.EasierPureBladestone || o.HeavierLoads || GamePatcher.IsPatched(_game))
            {
                Status("Checking the co-op patch…");
                await Task.Run(ApplyPatch);
            }
            Log($"Launching the game. Server: {target}");
            _emu.Launch(_game, _s.Fullscreen);
            Dispatcher.InvokeAsync(async () => { await Task.Delay(4000); UpdateGameIndicator(); });
        });
        Status(_host != null ? "Game running. Keep this app open: it is the party server." : "Game running. Good hunting, Slayer of Demons.");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveSettings();
        if (_client != null && !_closingConfirmed && _client.ViaRelay && _emu.IsRunning() &&
            !Ui.Ask(this, "You are connected through the relay. Closing the app disconnects you from the party.\n\nClose anyway?"))
        {
            e.Cancel = true;
            return;
        }
        if (_host == null || _closingConfirmed) { _client?.Dispose(); base.OnClosing(e); return; }
        if (!Ui.Ask(this, "You are the host. Closing the app ends the party for your friend.\n\nClose anyway?"))
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
