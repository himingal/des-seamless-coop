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
    string _state = Loc.T("suWaiting");
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
        // Match the app language before any text is shown (empty = detect from Windows).
        var lang = AppSettings.Load().Language;
        if (string.IsNullOrEmpty(lang))
        {
            var two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            lang = two is "pt" or "es" ? two : "en";
        }
        Loc.Lang = lang;

        InitializeComponent();
        Ui.DarkTitleBar(this);
        _gamePath = gamePath;
        TxtTitle.Text = Loc.T("suTitlePreparing");
        TxtStatus.Text = Loc.T("suStarting");
        BtnClose.Content = Loc.T("suContinue");
        foreach (var n in new[] { "suStepRpcs3", "suStepFw", "suStepGame", "suStepPatch", "suStepSettings" })
            _steps.Add(new SetupStep { Name = Loc.T(n) });
        Steps.ItemsSource = _steps;
        Loaded += async (_, _) => await RunAsync();
    }

    Brush Res(string key) => (Brush)FindResource(key);

    async Task Step(int i, Func<IProgress<DownloadProgress>, Task<string>> work)
    {
        var s = _steps[i];
        s.State = Loc.T("suWorking");
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
            s.State = Loc.T("suFailed");
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
            if (emu.IsInstalled) return Loc.T("suAlready");
            await emu.InstallLatestAsync(p);
            return Loc.T("suInstalled");
        });
        await Step(1, async p =>
        {
            if (!emu.IsInstalled) return Loc.T("suSkipped");
            if (emu.FirmwareInstalled) return Loc.T("suAlready");
            await emu.InstallFirmwareAsync(p);
            return Loc.T("suInstalled");
        });
        await Step(2, _ =>
        {
            if (game == null) return Task.FromResult(_gamePath == null ? Loc.T("suPickLater") : Loc.T("suNotFound"));
            settings.GamePath = game.Root;
            settings.Save();
            if (emu.IsInstalled) emu.RegisterGame(game);
            return Task.FromResult($"{game.Serial} v{game.Version}");
        });
        await Step(3, async _ =>
        {
            if (game == null) return Loc.T("suSkipped");
            TxtStatus.Text = Loc.T("suPatching");
            var r = await Task.Run(() => GamePatcher.Apply(game, settings.Patch));
            if (r.Changed && emu.IsInstalled) emu.ClearGameCache();
            return r.Lines.Any(l => l.StartsWith("Warning") || l.StartsWith("Error")) ? Loc.T("suPartial") : Loc.T("suApplied");
        });
        await Step(4, async _ =>
        {
            if (!emu.IsInstalled) return Loc.T("suSkipped");
            emu.PrepareGuiSettings();
            await emu.EnableQualityPatchesAsync();
            return Loc.T("suDone");
        });

        Bar.Value = 1;
        if (_failed)
        {
            TxtTitle.Text = Loc.T("suTitleAlmost");
            TxtStatus.Text = Loc.T("suSomeFailed");
            BtnClose.Visibility = Visibility.Visible;
        }
        else
        {
            TxtTitle.Text = Loc.T("suTitleDone");
            TxtStatus.Text = Loc.T("suAllSet");
            await Task.Delay(1400);
            Close();
        }
    }

    void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
