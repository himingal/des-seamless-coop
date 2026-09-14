using System.Diagnostics;
using System.Text.Json;
using DesCoop.Game;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace DesCoop.Emu;

public sealed record DownloadProgress(string Stage, double Fraction);

/// <summary>Installs, configures and launches a portable RPCS3 for Demon's Souls online play.</summary>
public sealed class Rpcs3Manager
{
    public static readonly string[] DesHosts =
    [
        "ds-eu-c.scej-online.jp", "ds-eu-g.scej-online.jp", "c.demons-souls.com",
        "g.demons-souls.com", "cmnap.scej-online.jp", "demons-souls.scej-online.jp",
    ];

    /// <summary>The Archstones community server (public matchmaking, no party features).</summary>
    public const string ArchstonesIp = "206.189.232.242";

    // RPCS3 patch hashes (Demon's Souls 01.00) that get "Skip Intro Videos" enabled.
    static readonly (string hash, string serial)[] SkipIntroPatches =
    [
        ("PPU-83681f6110d33442329073b72b8dc88a2f677172", "BLUS30443"),
        ("PPU-5446a2645880eefa75f7e374abd6b7818511e2ef", "BLES00932"),
    ];

    static readonly HttpClient Http = CreateHttp();

    static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("DesCoop/1.0 (+https://github.com)");
        return h;
    }

    public string Root { get; }
    public string Exe => Path.Combine(Root, "rpcs3.exe");
    public string ConfigDir => Path.Combine(Root, "config");
    public bool IsInstalled => File.Exists(Exe);
    public bool FirmwareInstalled => File.Exists(Path.Combine(Root, "dev_flash", "vsh", "module", "vsh.self"));

    public Rpcs3Manager(string root) => Root = Path.GetFullPath(root);

    /// <summary>A folder is a usable RPCS3 install if it contains rpcs3.exe.</summary>
    public static bool LooksLikeRpcs3(string? dir) => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, "rpcs3.exe"));

    // ------------------------------------------------------------------ install

    public async Task InstallLatestAsync(IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        progress?.Report(new("Looking for the latest RPCS3 build", 0));
        using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://api.github.com/repos/RPCS3/rpcs3-binaries-win/releases/latest", ct));
        var asset = doc.RootElement.GetProperty("assets").EnumerateArray()
            .First(a => a.GetProperty("name").GetString()!.EndsWith("_win64_msvc.7z", StringComparison.OrdinalIgnoreCase)
                     || a.GetProperty("name").GetString()!.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));
        var url = asset.GetProperty("browser_download_url").GetString()!;
        var name = asset.GetProperty("name").GetString()!;

        var tmp = Path.Combine(Path.GetTempPath(), name);
        await DownloadAsync(url, tmp, "Downloading RPCS3", progress, ct);

        progress?.Report(new("Extracting RPCS3", 0));
        Directory.CreateDirectory(Root);
        using (var archive = ArchiveFactory.OpenArchive(tmp))
        {
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            int i = 0;
            foreach (var e in entries)
            {
                ct.ThrowIfCancellationRequested();
                e.WriteToDirectory(Root, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                progress?.Report(new("Extracting RPCS3", ++i / (double)entries.Count));
            }
        }
        try { File.Delete(tmp); } catch { }
        PrepareGuiSettings();
    }

    public async Task InstallFirmwareAsync(IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        progress?.Report(new("Asking Sony for the official PS3 firmware", 0));
        var list = await Http.GetStringAsync("http://fus01.ps3.update.playstation.net/update/ps3/list/us/ps3-updatelist.txt", ct);
        var url = list.Split(';').Select(s => s.Trim())
            .Where(s => s.StartsWith("CDN=", StringComparison.Ordinal) && s.EndsWith("PS3UPDAT.PUP", StringComparison.OrdinalIgnoreCase))
            .Select(s => s[4..]).FirstOrDefault() ?? throw new InvalidOperationException("Could not find the firmware on Sony's update server.");

        var pup = Path.Combine(Path.GetTempPath(), "PS3UPDAT.PUP");
        await DownloadAsync(url, pup, "Downloading PS3 firmware (Sony)", progress, ct);

        progress?.Report(new("Installing firmware into RPCS3", 0));
        // Headless mode installs without the "Install firmware?" dialog and exits when done.
        using var proc = Process.Start(new ProcessStartInfo(Exe, $"{CommonArgs} --headless --installfw \"{pup}\"")
        { WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true })!;

        var flash = Path.Combine(Root, "dev_flash");
        var sw = Stopwatch.StartNew();
        while (!proc.HasExited && sw.Elapsed < TimeSpan.FromMinutes(15))
        {
            await Task.Delay(1000, ct);
            int count = Directory.Exists(flash) ? Directory.EnumerateFiles(flash, "*", SearchOption.AllDirectories).Count() : 0;
            progress?.Report(new($"Installing firmware ({count} files)", Math.Min(0.99, count / 1900.0)));
            if (count == 0 && sw.Elapsed > TimeSpan.FromSeconds(120)) break; // stuck (e.g. an error dialog)
        }
        if (!proc.HasExited) try { proc.Kill(true); } catch { }
        try { File.Delete(pup); } catch { }
        if (!FirmwareInstalled) throw new InvalidOperationException("The firmware was not installed. Try again, or install it from RPCS3 (File > Install Firmware).");
        progress?.Report(new("Firmware instalado", 1));
    }

    static async Task DownloadAsync(string url, string dest, string stage, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);
        var buf = new byte[1 << 16];
        long done = 0;
        int n;
        var last = DateTime.MinValue;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            done += n;
            if (DateTime.UtcNow - last > TimeSpan.FromMilliseconds(150))
            {
                last = DateTime.UtcNow;
                progress?.Report(new($"{stage} ({done / 1048576.0:0.0} MB{(total > 0 ? $" of {total / 1048576.0:0.0} MB" : "")})", total > 0 ? done / (double)total : 0));
            }
        }
    }

    /// <summary>Silences the first-run dialogs so CLI boots and firmware installs are not blocked.</summary>
    public void PrepareGuiSettings()
    {
        var ini = Path.Combine(Root, "GuiConfigs", "CurrentSettings.ini");
        IniFile.Set(ini, "main_window", new Dictionary<string, string>
        {
            ["infoBoxEnabledWelcome"] = "false",
            ["infoBoxEnabledInstallPUP"] = "false",
            ["confirmationBoxBootGame"] = "false",
        });
        IniFile.Set(ini, "Meta", new Dictionary<string, string> { ["checkUpdateStart"] = "false" });
    }

    // ------------------------------------------------------------------ configuration

    public static string BuildSwapList(string address) => string.Join("&&", DesHosts.Select(h => $"{h}={address}"));

    /// <summary>Points the Demon's Souls hostnames at <paramref name="serverAddress"/> and turns RPCN on.</summary>
    public void ConfigureNetwork(string serverAddress)
    {
        var cfg = new YamlFile(Path.Combine(ConfigDir, "config.yml"));
        var net = YamlFile.Map(cfg.Root, "Net");
        YamlFile.Set(net, "Internet enabled", "Connected");
        YamlFile.Set(net, "PSN status", "RPCN");
        YamlFile.Set(net, "UPNP Enabled", "true");
        YamlFile.Set(net, "IP swap list", BuildSwapList(serverAddress));
        cfg.Save();
    }

    public string? CurrentSwapTarget()
    {
        var cfg = new YamlFile(Path.Combine(ConfigDir, "config.yml"));
        var swap = YamlFile.Get(YamlFile.Map(cfg.Root, "Net"), "IP swap list");
        return swap?.Split("&&").Select(p => p.Split('=')).FirstOrDefault(p => p.Length == 2)?[1];
    }

    public string? RpcnUser()
    {
        var cfg = new YamlFile(Path.Combine(ConfigDir, "rpcn.yml"));
        var npid = YamlFile.Get(cfg.Root, "NPID");
        var pass = YamlFile.Get(cfg.Root, "Password");
        return !string.IsNullOrWhiteSpace(npid) && !string.IsNullOrWhiteSpace(pass) ? npid : null;
    }

    public (string? npid, string? password, string? token) RpcnAccount()
    {
        var cfg = new YamlFile(Path.Combine(ConfigDir, "rpcn.yml"));
        return (YamlFile.Get(cfg.Root, "NPID"), YamlFile.Get(cfg.Root, "Password"), YamlFile.Get(cfg.Root, "Token"));
    }

    /// <summary>Writes rpcn.yml exactly like RPCS3's own account dialog (password already derived).</summary>
    public void SaveRpcnAccount(string npid, string derivedPassword, string token)
    {
        var cfg = new YamlFile(Path.Combine(ConfigDir, "rpcn.yml"));
        YamlFile.Set(cfg.Root, "Version", "2");
        if (YamlFile.Get(cfg.Root, "Host") is not { Length: > 0 }) YamlFile.Set(cfg.Root, "Host", Net.RpcnClient.DefaultHost);
        YamlFile.Set(cfg.Root, "NPID", npid);
        YamlFile.Set(cfg.Root, "Password", derivedPassword);
        YamlFile.Set(cfg.Root, "Token", token);
        cfg.Save();
    }

    public void RegisterGame(GameInfo game)
    {
        if (!game.IsDisc) return; // HDD games are found by RPCS3 itself when inside dev_hdd0
        var f = new YamlFile(Path.Combine(ConfigDir, "games.yml"));
        YamlFile.Set(f.Root, game.Serial, game.Root.Replace('\\', '/').TrimEnd('/') + "/");
        f.Save();
    }

    public async Task EnableQualityPatchesAsync(CancellationToken ct = default)
    {
        var patchDir = Path.Combine(Root, "patches");
        var patchYml = Path.Combine(patchDir, "patch.yml");
        if (!File.Exists(patchYml) || File.GetLastWriteTimeUtc(patchYml) < DateTime.UtcNow.AddDays(-14))
        {
            try
            {
                using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://rpcs3.net/compatibility?patch&api=v1&v=1.2", ct));
                if (doc.RootElement.GetProperty("return_code").GetInt32() == 0)
                {
                    Directory.CreateDirectory(patchDir);
                    await File.WriteAllTextAsync(patchYml, doc.RootElement.GetProperty("patch").GetString(), ct);
                }
            }
            catch { /* offline: RPCS3 still runs without patches */ }
        }

        var cfg = new YamlFile(Path.Combine(ConfigDir, "patch_config.yml"));
        foreach (var (hash, serial) in SkipIntroPatches)
        {
            var node = YamlFile.Map(YamlFile.Map(YamlFile.Map(YamlFile.Map(YamlFile.Map(cfg.Root, hash), "Skip Intro Videos"), "Demon's Souls"), serial), "01.00");
            YamlFile.Set(node, "Enabled", "true");
        }
        cfg.Save();
    }

    /// <summary>Demon's Souls copies data to the HDD1 cache; clear it so patched params are re-read.</summary>
    public void ClearGameCache()
    {
        var caches = Path.Combine(Root, "dev_hdd1", "caches");
        if (!Directory.Exists(caches)) return;
        foreach (var d in Directory.EnumerateDirectories(caches)) try { Directory.Delete(d, true); } catch { }
        foreach (var f in Directory.EnumerateFiles(caches)) try { File.Delete(f); } catch { }
    }

    // ------------------------------------------------------------------ run

    /// <summary>RPCS3 refuses to start from some folders (e.g. Temp) unless told otherwise; users pick any folder here.</summary>
    const string CommonArgs = "--allow-any-location";

    public Process Launch(GameInfo game, bool fullscreen)
    {
        PrepareGuiSettings();
        var args = $"{CommonArgs} --no-gui {(fullscreen ? "--fullscreen " : "")}\"{game.Eboot}\"";
        return Process.Start(new ProcessStartInfo(Exe, args) { WorkingDirectory = Root, UseShellExecute = false })!;
    }

    public Process OpenGui() => Process.Start(new ProcessStartInfo(Exe, CommonArgs) { WorkingDirectory = Root, UseShellExecute = false })!;

    public bool IsRunning() => Process.GetProcessesByName("rpcs3").Any(p =>
    {
        try { return string.Equals(p.MainModule?.FileName, Exe, StringComparison.OrdinalIgnoreCase); } catch { return false; }
    });
}
