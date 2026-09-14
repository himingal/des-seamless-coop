using System.Text.Json;

namespace DesCoop.Server;

/// <summary>Tiny JSON persistence. Private servers hold a handful of players, a database is overkill.</summary>
public sealed class Store
{
    public List<BloodMessage> Messages { get; set; } = [];
    public List<ReplayEntry> Replays { get; set; } = [];
    public Dictionary<string, PlayerStats> Players { get; set; } = [];
    /// <summary>Walkable positions seen per block, used to place party signs when the host position is unknown.</summary>
    public Dictionary<int, List<float[]>> KnownPositions { get; set; } = [];
    public int NextMessageId { get; set; } = 1;
    public int NextReplayId { get; set; } = 1;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false, IncludeFields = true };

    string _path = "";
    readonly object _saveLock = new();
    Timer? _timer;

    public static Store Load(string path)
    {
        Store s;
        try { s = File.Exists(path) ? JsonSerializer.Deserialize<Store>(File.ReadAllText(path), Json) ?? new() : new(); }
        catch { s = new(); }
        s._path = path;
        return s;
    }

    public PlayerStats Stats(string characterId)
    {
        if (!Players.TryGetValue(characterId, out var st)) Players[characterId] = st = new PlayerStats();
        return st;
    }

    public void AddKnownPosition(int block, float x, float y, float z, float ay)
    {
        if (!KnownPositions.TryGetValue(block, out var list)) KnownPositions[block] = list = [];
        list.Add([x, y, z, ay]);
        if (list.Count > 64) list.RemoveAt(0);
    }

    /// <summary>Caller must hold the server lock while calling (serialization reads the collections).</summary>
    public void MarkDirty()
    {
        _timer ??= new Timer(_ => SafeFlush(), null, Timeout.Infinite, Timeout.Infinite);
        _timer.Change(1500, Timeout.Infinite);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Func<object>? LockProvider { get; set; }

    void SafeFlush()
    {
        try { Flush(); } catch { /* disk full / locked file: keep running, next change retries */ }
    }

    public void Flush()
    {
        if (string.IsNullOrEmpty(_path)) return;
        string json;
        lock (LockProvider?.Invoke() ?? _saveLock) json = JsonSerializer.Serialize(this, Json);
        lock (_saveLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, true);
        }
    }
}
