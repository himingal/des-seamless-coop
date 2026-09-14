using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using DesCoop.Emu;
using DesCoop.Game;

namespace DesCoop.App;

public sealed class SetupStep : INotifyPropertyChanged
{
    public required string Name { get; init; }
    string _state = "waiting";
    Brush _color = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x44, 0x38));
    public string State { get => _state; set { _state = value; PropertyChanged?.Invoke(this, new(nameof(State))); } }
    public Brush Color { get => _color; set { _color = value; PropertyChanged?.Invoke(this, new(nameof(Color))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Runs right after the installer: RPCS3, Sony firmware, game registration, co-op patch and RPCS3
/// settings, so the only thing left is the RPCN account and pressing PLAY.
/// </summary>
public partial class SetupWindow : Window
{
    readonly string? _gamePath;
    readonly ObservableCollection<SetupStep> _steps = [];
    bool _failed;

    public SetupWindow(string? gamePath)
    {
        InitializeComponent();
        Ui.DarkTitleBar(this);
        _gamePath = gamePath;
        foreach (var n in new[] { "RPCS3 emulator", "PS3 firmware (from Sony)", "Demon's Souls", "Co-op patch", "RPCS3 settings" })
            _steps.Add(new SetupStep { Name = n });
        Steps.ItemsSource = _steps;
        Loaded += async (_, _) => await RunAsync();
    }

    Brush Res(string key) => (Brush)FindResource(key);

    async Task Step(int i, Func<IProgress<DownloadProgress>, Task<string>> work)
    {
        var s = _steps[i];
        s.State = "working…";
        s.Color = Res("Gold");
        var progress = new Progress<DownloadProgress>(p => { TxtStatus.Text = p.Stage; Bar.Value = p.Fraction; });
        try
        {
            s.State = await work(progress);
            s.Color = Res("Ok");
        }
        catch (Exception ex)
        {
            _failed = true;
            s.State = "failed";
            s.Color = Res("Bad");
            TxtStatus.Text = ex.Message;
            await Task.Delay(1500);
        }
    }

    async Task RunAsync()
    {
        var settings = AppSettings.Load();
        var emu = new Rpcs3Manager(settings.EffectiveRpcs3Dir);
        GameInfo? game = GameLocator.Resolve(_gamePath) ?? GameLocator.Resolve(settings.GamePath);

        await Step(0, async p =>
        {
            if (emu.IsInstalled) return "already installed";
            await emu.InstallLatestAsync(p);
            return "installed";
        });
        await Step(1, async p =>
        {
            if (!emu.IsInstalled) return "skipped";
            if (emu.FirmwareInstalled) return "already installed";
            await emu.InstallFirmwareAsync(p);
            return "installed";
        });
        await Step(2, _ =>
        {
            if (game == null) return Task.FromResult(_gamePath == null ? "pick it later in the app" : "not found, pick it in the app");
            settings.GamePath = game.Root;
            settings.Save();
            if (emu.IsInstalled) emu.RegisterGame(game);
            return Task.FromResult($"{game.Serial} v{game.Version}");
        });
        await Step(3, async _ =>
        {
            if (game == null) return "skipped";
            TxtStatus.Text = "Patching the game files (backup kept)…";
            var r = await Task.Run(() => GamePatcher.Apply(game, settings.Patch));
            if (r.Changed && emu.IsInstalled) emu.ClearGameCache();
            return r.Lines.Any(l => l.StartsWith("Warning") || l.StartsWith("Error")) ? "partially applied" : "applied";
        });
        await Step(4, async _ =>
        {
            if (!emu.IsInstalled) return "skipped";
            emu.PrepareGuiSettings();
            await emu.EnableQualityPatchesAsync();
            return "done";
        });

        Bar.Value = 1;
        if (_failed)
        {
            TxtTitle.Text = "ALMOST THERE";
            TxtStatus.Text = "Some steps failed (no internet?). The app has buttons to retry each one.";
            BtnClose.Visibility = Visibility.Visible;
        }
        else
        {
            TxtTitle.Text = "THE NEXUS AWAITS";
            TxtStatus.Text = "All set.";
            await Task.Delay(1400);
            Close();
        }
    }

    void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
