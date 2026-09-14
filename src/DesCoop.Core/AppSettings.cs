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
    public string PartyName { get; set; } = "Party do " + Environment.UserName;
    public PatchOptions Patch { get; set; } = new();
    public bool Fullscreen { get; set; }
    public bool UseUpnp { get; set; } = true;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static string BaseDir => AppContext.BaseDirectory;

    /// <summary>Portable next to the exe when writable (default install is per-user), else %APPDATA%.</summary>
    public static string DataDir { get; } = ResolveDataDir();

    static string ResolveDataDir()
    {
        try
        {
            var probe = Path.Combine(BaseDir, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return BaseDir;
        }
        catch
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeSSeamlessCoop");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    static string FilePath => Path.Combine(DataDir, "descoop-settings.json");

    public static AppSettings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new(); }
        catch { }
        return new();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); } catch { }
    }

    public string EffectiveRpcs3Dir => string.IsNullOrWhiteSpace(Rpcs3Dir) ? Path.Combine(DataDir, "rpcs3") : Rpcs3Dir;
}
