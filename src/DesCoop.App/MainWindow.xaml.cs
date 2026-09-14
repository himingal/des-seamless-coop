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
    bool _busy, _loading = true, _closingConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        _emu = new Rpcs3Manager(_s.EffectiveRpcs3Dir);
        _game = GameLocator.Resolve(_s.GamePath);
        _joinedAddress = _s.JoinedAddress;

        ChkBlue.IsChecked = _s.Patch.BlueEyeStoneInBodyForm;
        ChkEph.IsChecked = _s.Patch.InfiniteEphemeralEyes;
        ChkUpnp.IsChecked = _s.UseUpnp;
        ChkFullscreen.IsChecked = _s.Fullscreen;
        TxtPartyName.Text = _s.PartyName;
        TxtJoinCode.Text = _s.JoinCode ?? "";
        switch (_s.Mode)
        {
            case PartyMode.Host: RbHost.IsChecked = true; break;
            case PartyMode.Join: RbJoin.IsChecked = true; break;
            case PartyMode.Public: RbPublic.IsChecked = true; break;
        }
        if (_joinedAddress != null) TxtJoinInfo.Text = $"Último servidor usado: {_joinedAddress}";

        _loading = false;
        _timer.Tick += async (_, _) =>
        {
            try { await RefreshPartyAsync(); }
            catch (Exception ex) { Log("Aviso: " + ex.Message); }
        };
        _timer.Start();
        RefreshAll();
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
        _s.UseUpnp = ChkUpnp.IsChecked == true;
        _s.Fullscreen = ChkFullscreen.IsChecked == true;
        _s.PartyName = string.IsNullOrWhiteSpace(TxtPartyName.Text) ? _s.PartyName : TxtPartyName.Text.Trim();
        _s.JoinCode = TxtJoinCode.Text.Trim();
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
            Status("Pronto.");
        }
        catch (Exception ex)
        {
            Status("Erro: " + ex.Message);
            Log("ERRO: " + ex);
            MessageBox.Show(this, ex.Message, "DeS Seamless Co-op", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        TxtRpcs3.Text = emu ? _emu.Root : "Clique em Baixar (versão oficial mais recente) ou aponte para o seu RPCS3.";
        BtnRpcs3Install.Content = emu ? "Atualizar" : "Baixar";

        bool fw = emu && _emu.FirmwareInstalled;
        DotFw.Fill = B(fw ? "Ok" : "Bad");
        TxtFw.Text = fw ? "Instalado." : emu ? "Baixa o firmware oficial direto da Sony e instala." : "Instale o RPCS3 primeiro.";
        BtnFw.IsEnabled = !_busy && emu && !fw;

        DotGame.Fill = B(_game != null ? (GameLocator.IsDemonsSouls(_game) ? "Ok" : "Warn") : "Bad");
        TxtGame.Text = _game != null
            ? $"{_game.Title} [{_game.Serial}] v{_game.Version}\n{_game.Root}" + (GameLocator.IsDemonsSouls(_game) ? "" : "\nIsso não parece ser Demon's Souls.")
            : "Pasta do jogo (a que tem PS3_GAME dentro). Use o seu próprio dump.";

        bool patched = _game != null && GamePatcher.IsPatched(_game);
        DotPatch.Fill = B(_game == null ? "Bad" : patched ? "Ok" : "Warn");
        TxtPatch.Text = _game == null ? "" : patched ? "Aplicado (backup do original guardado)." : "Ainda não aplicado. É aplicado sozinho ao clicar JOGAR.";
        BtnPatch.IsEnabled = BtnUnpatch.IsEnabled = !_busy && _game != null;
        BtnUnpatch.Visibility = patched ? Visibility.Visible : Visibility.Collapsed;

        var rpcn = emu ? _emu.RpcnUser() : null;
        DotRpcn.Fill = B(rpcn != null ? "Ok" : "Bad");
        TxtRpcn.Text = rpcn != null
            ? $"Conectado como {rpcn}."
            : "Abre o RPCS3: menu Configuration › RPCN › Create Account (use um e-mail real, chega um token). Depois feche o RPCS3.";
        BtnRpcn.IsEnabled = !_busy && emu;
    }

    // ------------------------------------------------------------------ setup actions

    async void BtnRpcs3Install_Click(object sender, RoutedEventArgs e)
    {
        if (_emu.IsRunning()) { MessageBox.Show(this, "Feche o RPCS3 antes de atualizar."); return; }
        _emu = new Rpcs3Manager(string.IsNullOrWhiteSpace(_s.Rpcs3Dir) ? Path.Combine(AppSettings.DataDir, "rpcs3") : _s.Rpcs3Dir);
        await RunBusy("Baixando RPCS3…", async p =>
        {
            await _emu.InstallLatestAsync(p);
            Log("RPCS3 instalado em " + _emu.Root);
            if (!_emu.FirmwareInstalled)
            {
                await _emu.InstallFirmwareAsync(p);
                Log("Firmware instalado.");
            }
        });
    }

    void BtnRpcs3Pick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Pasta onde está o rpcs3.exe" };
        if (dlg.ShowDialog(this) != true) return;
        if (!Rpcs3Manager.LooksLikeRpcs3(dlg.FolderName))
        {
            MessageBox.Show(this, "Não achei o rpcs3.exe nessa pasta.");
            return;
        }
        _s.Rpcs3Dir = dlg.FolderName;
        _emu = new Rpcs3Manager(dlg.FolderName);
        SaveSettings();
        RefreshAll();
    }

    async void BtnFw_Click(object sender, RoutedEventArgs e) =>
        await RunBusy("Instalando firmware…", p => _emu.InstallFirmwareAsync(p));

    void BtnGame_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Pasta do Demon's Souls (a que contém PS3_GAME)" };
        if (dlg.ShowDialog(this) != true) return;
        var g = GameLocator.Resolve(dlg.FolderName);
        if (g == null)
        {
            MessageBox.Show(this, "Não encontrei PS3_GAME/USRDIR/EBOOT.BIN nessa pasta.\nEscolha a pasta do dump do jogo (ex.: BLUS30443).");
            return;
        }
        _game = g;
        _s.GamePath = g.Root;
        SaveSettings();
        if (_emu.IsInstalled) _emu.RegisterGame(g);
        Log($"Jogo: {g.Title} [{g.Serial}] em {g.Root}");
        RefreshAll();
    }

    void PatchOption_Changed(object sender, RoutedEventArgs e) => SaveSettings();

    async void BtnPatch_Click(object sender, RoutedEventArgs e)
    {
        if (_game == null) return;
        SaveSettings();
        await RunBusy("Aplicando patch…", async _ => await Task.Run(ApplyPatch));
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
        await RunBusy("Restaurando arquivos originais…", async _ => await Task.Run(() =>
        {
            GamePatcher.Restore(_game);
            if (_emu.IsInstalled) _emu.ClearGameCache();
            Log("Arquivos originais do jogo restaurados.");
        }));
        ChkBlue.IsChecked = ChkEph.IsChecked = false;
    }

    void BtnRpcn_Click(object sender, RoutedEventArgs e)
    {
        _emu.PrepareGuiSettings();
        _emu.OpenGui();
        Log("RPCS3 aberto: Configuration › RPCN › Create Account. Depois de validar o token, feche o RPCS3.");
    }

    // ------------------------------------------------------------------ party

    void Mode_Changed(object sender, RoutedEventArgs e)
    {
        PanelHost.Visibility = RbHost.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelJoin.Visibility = RbJoin.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelPublic.Visibility = RbPublic.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _s.Mode = RbHost.IsChecked == true ? PartyMode.Host : RbJoin.IsChecked == true ? PartyMode.Join : RbPublic.IsChecked == true ? PartyMode.Public : PartyMode.None;
        SaveSettings();
    }

    async void BtnHost_Click(object sender, RoutedEventArgs e)
    {
        if (_host != null)
        {
            await StopHostAsync();
            return;
        }
        SaveSettings();
        await RunBusy("Criando party…", async _ => await StartHostAsync());
    }

    /// <summary>One UAC prompt so Windows does not block the friend (server ports + RPCS3 P2P).</summary>
    void EnsureFirewall()
    {
        var appExe = Environment.ProcessPath ?? "";
        if (Firewall.AllConfigured(_emu.Exe)) return;
        Status("Liberando no Firewall do Windows (aceite o aviso do Windows)…");
        if (!Firewall.Configure(appExe, _emu.Exe))
            Log("Firewall não configurado (aviso recusado). Se o amigo não conectar, permita o DesCoop e o RPCS3 no Firewall.");
    }

    async Task StartHostAsync()
    {
        EnsureFirewall();
        var opts = new DesServerOptions { ServerName = _s.PartyName, DataDir = Path.Combine(AppSettings.DataDir, "server-data") };
        var host = new PartyHost(opts);
        host.Log += Log;
        try { await host.StartAsync(_s.UseUpnp); }
        catch (System.Net.Sockets.SocketException)
        {
            await host.DisposeAsync();
            throw new InvalidOperationException("As portas 18000/18666-18668 já estão em uso. Outro servidor de Demon's Souls (ou outra cópia deste app) está aberto?");
        }
        _host = host;
        TxtCode.Text = host.Code;
        CodeBox.Visibility = Visibility.Visible;
        BtnHost.Content = "Encerrar party";
        var lines = new List<string>();
        foreach (var a in host.LocalAddresses.Where(a => a.Kind != "LAN")) lines.Add($"{a.Kind}: {a.Address}");
        if (host.PublicIp != null) lines.Add($"Internet: {host.PublicIp}" + (host.UpnpOk ? " (portas abertas via UPnP)" : " (sem UPnP: abra TCP 18000, 18666-18668 ou use Radmin VPN)"));
        foreach (var a in host.LocalAddresses.Where(a => a.Kind == "LAN")) lines.Add($"Rede local: {a.Address}");
        TxtHostInfo.Text = string.Join("\n", lines) + "\nDeixe este app aberto enquanto jogam (ele é o servidor).";
        Log("Party criada. Código: " + host.Code);
    }

    async Task StopHostAsync()
    {
        if (_host == null) return;
        var h = _host;
        _host = null;
        await h.DisposeAsync();
        CodeBox.Visibility = Visibility.Collapsed;
        BtnHost.Content = "Criar party";
        Log("Party encerrada.");
    }

    void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(TxtCode.Text); Status("Código copiado. Mande no WhatsApp/Discord para o seu amigo."); } catch { }
    }

    async void BtnJoin_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        await RunBusy("Procurando a party…", async _ => await JoinAsync());
    }

    async Task<bool> JoinAsync()
    {
        if (!PartyCode.TryDecode(TxtJoinCode.Text, out var name, out var addrs))
            throw new InvalidOperationException("Código inválido. Cole o código DES-… inteiro que o host mandou (ou só o IP dele).");
        TxtJoinInfo.Text = "Testando " + string.Join(", ", addrs) + "…";
        var found = await NetUtil.FindReachableAsync(addrs, _emu.IsInstalled ? _emu.RpcnUser() : null);
        if (found == null)
        {
            TxtJoinInfo.Text = "Não consegui falar com o host.";
            throw new InvalidOperationException(
                "O servidor do host não respondeu.\n\n• O host clicou em \"Criar party\" e deixou o app aberto?\n• Se o roteador dele não tem UPnP, instalem o Radmin VPN (grátis), entrem na mesma rede e o host cria a party de novo (o código passa a ter o IP 26.x).\n• Firewall do Windows do host: permitir o DesCoop.");
        }
        _joinedAddress = found.Address;
        SaveSettings();
        TxtJoinInfo.Text = $"Conectado à \"{found.Name}\" via {found.Address}. Agora é só clicar JOGAR.";
        Log($"Party encontrada: {found.Name} ({found.Address})");
        return true;
    }

    bool _refreshing;

    async Task RefreshPartyAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try { await RefreshPartyCoreAsync(); } finally { _refreshing = false; }
    }

    async Task RefreshPartyCoreAsync()
    {
        ServerStatus? st = null;
        if (_host != null) st = _host.Server.GetStatus();
        else if (RbJoin.IsChecked == true && _joinedAddress != null) st = await NetUtil.GetStatusAsync(_joinedAddress);

        var rows = st?.Players.Select(p => new PlayerRow(p.Name, p.Area,
            p.InSession ? "em co-op" : p.HasSign ? "sinal azul ativo" : "")).ToList() ?? [];
        ListPlayers.ItemsSource = rows;
        TxtNoPlayers.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!_busy && DateTime.Now.Second % 15 < 3) RefreshAll();
    }

    // ------------------------------------------------------------------ play

    async void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        if (!_emu.IsInstalled) { MessageBox.Show(this, "Instale o RPCS3 primeiro (botão Baixar)."); return; }
        if (!_emu.FirmwareInstalled) { MessageBox.Show(this, "Instale o firmware do PS3 primeiro."); return; }
        if (_game == null) { MessageBox.Show(this, "Escolha a pasta do Demon's Souls."); return; }
        if (_s.Mode == PartyMode.None) { MessageBox.Show(this, "Escolha: Criar party, Entrar na party ou Servidor público."); return; }
        if (_emu.RpcnUser() == null &&
            MessageBox.Show(this, "Você ainda não tem conta RPCN configurada, então o online (co-op) não vai funcionar.\n\nJogar mesmo assim?",
                "DeS Seamless Co-op", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (_emu.IsRunning()) { MessageBox.Show(this, "O RPCS3 já está aberto. Feche ele para iniciar pelo app (as configurações de rede são aplicadas antes de abrir)."); return; }

        await RunBusy("Preparando…", async _ =>
        {
            string target;
            switch (_s.Mode)
            {
                case PartyMode.Host:
                    if (_host == null) await StartHostAsync();
                    target = "127.0.0.1";
                    break;
                case PartyMode.Join:
                    if (_joinedAddress == null || await NetUtil.HelloAsync(_joinedAddress, _emu.RpcnUser()) == null) await JoinAsync();
                    target = _joinedAddress!;
                    break;
                default:
                    target = Rpcs3Manager.ArchstonesIp;
                    break;
            }

            EnsureFirewall();
            Status("Configurando o RPCS3…");
            _emu.ConfigureNetwork(target);
            _emu.RegisterGame(_game);
            await _emu.EnableQualityPatchesAsync();
            if (_s.Patch.BlueEyeStoneInBodyForm || _s.Patch.InfiniteEphemeralEyes || GamePatcher.IsPatched(_game))
            {
                Status("Conferindo patch do jogo…");
                await Task.Run(ApplyPatch);
            }
            Log($"Abrindo o jogo. Servidor: {target}");
            _emu.Launch(_game, _s.Fullscreen);
        });
        Status(_host != null ? "Jogo aberto. Deixe este app aberto: ele é o servidor da party." : "Jogo aberto. Bom jogo!");
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        SaveSettings();
        if (_host == null || _closingConfirmed) { base.OnClosing(e); return; }
        if (MessageBox.Show(this, "Você é o host. Fechar o app encerra a party para o seu amigo.\n\nFechar mesmo assim?",
                "DeS Seamless Co-op", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        e.Cancel = true;
        _closingConfirmed = true;
        await StopHostAsync();
        Close();
    }
}
