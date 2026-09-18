using System.Text.Json;
using DesCoop.Game;

namespace DesCoop;

public enum PartyMode { None, Host, Join, Public }

public sealed class AppSettings
{
    public string? Rpcs3Dir { get; set; }
    public string? GamePath { get; set; }
    public PartyMode Mode { get; set; } = PartyMode.None;
    public string? JoinCode { get; set; }
    /// <summary>Address that worked last time we joined (what RPCS3 is pointed at).</summary>
    public string? JoinedAddress { get; set; }
    public string PartyName { get; set; } = Environment.UserName + "'s Party";
    public string PartyPassword { get; set; } = Net.Rendezvous.NewPassword();
    public string? JoinName { get; set; }
    public string? JoinPassword { get; set; }
    public PatchOptions Patch { get; set; } = new();
    public bool Fullscreen { get; set; }
    public bool UseUpnp { get; set; } = true;
    /// <summary>UI language code ("en", "pt", "es"); empty = detect from Windows on first run.</summary>
    public string Language { get; set; } = "";
    /// <summary>Host's world tendency for the party: -200 pure black, 0 normal, +200 pure white.</summary>
    public int WorldTendency { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static string BaseDir => AppContext.BaseDirectory;

    /// <summary>
    /// Always %APPDATA%\DeSSeamlessCoop: the same folder no matter where (or how, elevated or not) the
    /// app was installed, and it survives reinstalls. v1.0/1.1 portable data next to the exe is migrated.
    /// </summary>
    public static string DataDir { get; } = ResolveDataDir();

    static string ResolveDataDir()
    {
        var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeSSeamlessCoop");
        Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>Experimental "Seamless (TEST)" mode (launched with --seamless): a separate settings profile
    /// and a beefier patch preset, kept apart from the stable co-op so testing never touches it.</summary>
    public static bool Seamless { get; set; }

    static string StableFilePath => Path.Combine(DataDir, "descoop-settings.json");
    static string FilePath => Seamless ? Path.Combine(DataDir, "descoop-settings.seamless.json") : StableFilePath;
    static string LegacyFilePath => Path.Combine(BaseDir, "descoop-settings.json");
    static string LegacyRpcs3 => Path.Combine(BaseDir, "rpcs3");

    public static AppSettings Load()
    {
        AppSettings s = new();
        try
        {
            if (File.Exists(FilePath)) s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new();
            // First run of the seamless profile: seed emulator/game/RPCN paths from the stable one so the
            // tester doesn't reconfigure. Party name/mode start fresh (the seamless preset overrides tweaks).
            else if (Seamless && File.Exists(StableFilePath)) s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StableFilePath), Json) ?? new();
            else if (File.Exists(LegacyFilePath)) s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(LegacyFilePath), Json) ?? new();
        }
        catch { }

        // The game tweaks are a fixed, non-selectable set, so any value persisted by an older version is
        // ignored — every launch and every --setup applies the current intended options. Seamless mode uses
        // the beefier experimental preset instead.
        s.Patch = Seamless ? PatchOptions.SeamlessPreset() : new PatchOptions();

        // An RPCS3 that an older version (or the installer) put next to the exe keeps being used,
        // so firmware, saves and the RPCN account are not lost.
        if (string.IsNullOrWhiteSpace(s.Rpcs3Dir) && !File.Exists(Path.Combine(DataDir, "rpcs3", "rpcs3.exe"))
            && File.Exists(Path.Combine(LegacyRpcs3, "rpcs3.exe")))
            s.Rpcs3Dir = LegacyRpcs3;
        return s;
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); } catch { }
    }

    public string EffectiveRpcs3Dir => string.IsNullOrWhiteSpace(Rpcs3Dir) ? Path.Combine(DataDir, "rpcs3") : Rpcs3Dir;
}
