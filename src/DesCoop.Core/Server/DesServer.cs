using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DesCoop.Server;

public sealed class DesServerOptions
{
    public string ServerName { get; set; } = "DeS Seamless Co-op";
    public string DataDir { get; set; } = "server-data";
    /// <summary>Show every party member's co-op sign right next to whoever is looking for signs, in any area.</summary>
    public bool PartySigns { get; set; } = true;
    /// <summary>US/EU/JP clients share one pool (RPCN already redirects the NP comm IDs to one).</summary>
    public bool MergeRegions { get; set; } = true;
    /// <summary>Seconds between ghost uploads; lower = fresher host position for party signs.</summary>
    public int GhostInterval { get; set; } = 8;
    /// <summary>Address advertised to clients that came through NAT and never said how they reached us.</summary>
    public string? PublicAddress { get; set; }
    public string FallbackHost { get; set; } = "ds-eu-g.scej-online.jp";
    public int BootstrapPort { get; set; } = 18000;
    public int PortUS { get; set; } = 18666;
    public int PortEU { get; set; } = 18667;
    public int PortJP { get; set; } = 18668;
    public IPAddress Bind { get; set; } = IPAddress.Any;
    /// <summary>
    /// Server world tendency sent to every player for all worlds (-200 pure black .. +200 pure white).
    /// The game pulls its world tendency toward this value whenever it syncs with the server.
    /// </summary>
    public int WorldTendency { get; set; }

    /// <summary>
    /// Same-machine 2-player test: two DeS instances on one PC point at different loopback addresses
    /// (host → 127.0.0.1, helper → 127.0.0.2). The server (bound to Any) then keys and advertises each
    /// client by the loopback address it connected TO, so the two are distinct even though both are
    /// loopback and neither carries the relay header. Off in normal use (all loopback = "127.0.0.1").
    /// </summary>
    public bool LocalTest { get; set; }
}

public sealed record PartyPlayerStatus(string Name, string Area, int BlockId, bool HasSign, bool InSession, int SecondsAgo);
public sealed record ServerStatus(string App, string Version, string Name, PartyPlayerStatus[] Players);

public sealed class DesServer : IDisposable
{
    public const string Version = "2.0.0";
    static readonly int[] MonkBlocks = [40070, 40071, 40072, 40073, 40074, 40170, 40171, 40172, 40270];
    static readonly TimeSpan SignTtl = TimeSpan.FromSeconds(30);
    static readonly TimeSpan GhostTtl = TimeSpan.FromSeconds(45);
    static readonly TimeSpan OnlineTtl = TimeSpan.FromSeconds(120);
    static readonly JsonSerializerOptions JsonOut = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly object _lock = new();
    readonly DesServerOptions _o;
    readonly Store _store;
    readonly List<TcpListener> _listeners = [];
    readonly CancellationTokenSource _cts = new();
    readonly Dictionary<string, string> _ipToChar = [];
    readonly Dictionary<string, PlayerLive> _live = [];
    readonly Dictionary<string, SosSign> _sos = [];
    readonly Dictionary<string, string> _pendingSummon = [];
    readonly Dictionary<string, string> _pendingMonk = [];
    readonly Dictionary<string, Ghost> _ghosts = [];
    readonly Dictionary<string, string> _via = [];
    readonly Random _rng = new();
    StreamWriter? _logFile;
    uint _nextSos = 1;

    public event Action<string>? Log;
    public event Action? Changed;
    public DesServerOptions Options => _o;
    public bool Running { get; private set; }

    public DesServer(DesServerOptions options)
    {
        _o = options;
        Directory.CreateDirectory(_o.DataDir);
        _store = Store.Load(Path.Combine(_o.DataDir, "world.json"));
        _store.LockProvider = () => _lock;
    }

    public void Start()
    {
        try { _logFile = new StreamWriter(Path.Combine(_o.DataDir, "server.log"), true) { AutoFlush = true }; } catch { }
        foreach (int port in new[] { _o.BootstrapPort, _o.PortUS, _o.PortEU, _o.PortJP })
        {
            var l = new TcpListener(_o.Bind, port);
            l.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            l.Start(64);
            _listeners.Add(l);
            _ = AcceptLoop(l, port);
        }
        Running = true;
        Write("Party server running — your friend can join.");
    }

    public void Dispose()
    {
        Running = false;
        _cts.Cancel();
        foreach (var l in _listeners) { try { l.Stop(); } catch { } }
        try { _store.Flush(); } catch { }
        _logFile?.Dispose();
    }

    // important=false lines (per-request chatter) go to the log file only, not to the app's status box.
    void Write(string msg, bool important = true)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        try { _logFile?.WriteLine(line); } catch { }
        if (important) Log?.Invoke(line);
    }

    async Task AcceptLoop(TcpListener l, int port)
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient c;
            try { c = await l.AcceptTcpClientAsync(_cts.Token); }
            catch { if (_cts.IsCancellationRequested) return; continue; }
            _ = Task.Run(() => HandleClientAsync(c, port));
        }
    }

    // ------------------------------------------------------------------ HTTP

    async Task HandleClientAsync(TcpClient client, int port)
    {
        using var _ = client;
        try
        {
            client.ReceiveTimeout = client.SendTimeout = 10000;
            var stream = client.GetStream();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeout.CancelAfter(15000);

            var (requestLine, headers, body) = await ReadRequestAsync(stream, timeout.Token);
            var remote = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
            var local = ((IPEndPoint)client.Client.LocalEndPoint!).Address;
            if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
            if (local.IsIPv4MappedToIPv6) local = local.MapToIPv4();

            var parts = requestLine.Split(' ');
            string path = parts.Length > 1 ? parts[1] : "/";
            byte[] response;

            // Relayed players all arrive from loopback; the friend's forwarder tags them. In same-machine
            // test mode two loopback clients are told apart by the loopback address they connected to.
            string clientKey = IPAddress.IsLoopback(remote) && headers.TryGetValue(Net.RelayForwarder.ClientHeader, out var relayId)
                ? "relay:" + relayId
                : (_o.LocalTest && IPAddress.IsLoopback(remote) ? "local:" + local : remote.ToString());

            if (path.StartsWith("/descoop/", StringComparison.OrdinalIgnoreCase))
                response = HandleCustom(path, remote);
            else if (port == _o.BootstrapPort)
                response = Protocol.BuildBootstrap(BuildInfoSs(AdvertisedAddress(remote, local)));
            else
            {
                string cmdName = path.Split('?')[0].Split('/')[^1];
                var p = Protocol.ParseParams(Protocol.Decrypt(body));
                var result = Dispatch(cmdName, p, clientKey, port);
                if (result == null) return;
                response = Protocol.BuildResponse(result.Value.cmd, result.Value.data);
            }
            await stream.WriteAsync(response, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException) { }
        catch (Exception ex) { Write("Error: " + ex.Message); }
    }

    static async Task<(string, Dictionary<string, string>, byte[])> ReadRequestAsync(NetworkStream s, CancellationToken ct)
    {
        var buf = new List<byte>(4096);
        var chunk = new byte[4096];
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int n = await s.ReadAsync(chunk, ct);
            if (n == 0) throw new IOException("closed");
            buf.AddRange(chunk.AsSpan(0, n));
            headerEnd = IndexOf(buf, "\r\n\r\n"u8);
            if (buf.Count > 1 << 20) throw new IOException("header too large");
        }
        var head = Protocol.Raw.GetString(buf.GetRange(0, headerEnd).ToArray()).Split("\r\n");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in head.Skip(1))
        {
            int c = h.IndexOf(':');
            if (c > 0) headers[h[..c].Trim()] = h[(c + 1)..].Trim();
        }
        int len = headers.TryGetValue("Content-Length", out var cl) && int.TryParse(cl, out var v) ? v : 0;
        if (len < 0 || len > 8 << 20) throw new IOException("bad length");
        var body = new byte[len];
        int have = Math.Min(len, buf.Count - headerEnd - 4);
        buf.CopyTo(headerEnd + 4, body, 0, have);
        while (have < len)
        {
            int n = await s.ReadAsync(body.AsMemory(have), ct);
            if (n == 0) throw new IOException("closed");
            have += n;
        }
        return (head[0], headers, body);
    }

    static int IndexOf(List<byte> b, ReadOnlySpan<byte> pat)
    {
        for (int i = 0; i + pat.Length <= b.Count; i++)
        {
            int j = 0;
            while (j < pat.Length && b[i + j] == pat[j]) j++;
            if (j == pat.Length) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------ bootstrap / addressing

    static bool IsPrivate(IPAddress a)
    {
        var b = a.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168));
    }

    /// <summary>
    /// Which address this client should use for the game servers. The info file carries raw
    /// addresses, so each client gets the address it can actually reach us on.
    /// </summary>
    internal string AdvertisedAddress(IPAddress remote, IPAddress local)
    {
        // Same-machine test: advertise the loopback address the client connected to (127.0.0.1 host,
        // 127.0.0.2 helper), so each keeps talking to its own endpoint and stays distinct.
        if (IPAddress.IsLoopback(remote)) return _o.LocalTest ? local.ToString() : "127.0.0.1";
        lock (_lock) if (_via.TryGetValue(remote.ToString(), out var via)) return via;
        if (remote.Equals(local)) return local.ToString();
        if (IsPrivate(local) && !IsPrivate(remote)) return _o.PublicAddress ?? _o.FallbackHost; // came through a port forward
        return local.ToString();
    }

    internal string BuildInfoSs(string addr)
    {
        string Url(int port) => $"http://{addr}:{port}/cgi-bin/";
        var ports = new (int idx, int port)[]
        {
            (1, _o.PortUS), (2, _o.PortEU), (3, _o.PortJP), (4, _o.PortJP), (5, _o.PortEU), (6, _o.PortEU),
            (7, _o.PortEU), (8, _o.PortEU), (11, _o.PortJP), (12, _o.PortJP),
        };
        var sb = new StringBuilder();
        sb.Append("<ss>0</ss>\n");
        foreach (int i in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 11, 12 }) sb.Append($"<lang{i}></lang{i}>\n");
        foreach (var (idx, port) in ports) sb.Append($"<gameurl{idx}>{Url(port)}</gameurl{idx}>\n");
        foreach (int i in new[] { 1, 2, 3 }) sb.Append($"<browserurl{i}></browserurl{i}>\n");
        foreach (int i in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 11, 12 }) sb.Append($"<interval{i}>120</interval{i}>\n");
        sb.Append($"<getWanderingGhostInterval>20</getWanderingGhostInterval>\n");
        sb.Append($"<setWanderingGhostInterval>{Math.Clamp(_o.GhostInterval, 3, 60)}</setWanderingGhostInterval>\n");
        sb.Append("<getBloodMessageNum>80</getBloodMessageNum>\n");
        sb.Append("<getReplayListNum>80</getReplayListNum>\n");
        sb.Append("<enableWanderingGhost>1</enableWanderingGhost>\n");
        return sb.ToString();
    }

    byte[] HandleCustom(string path, IPAddress remote)
    {
        var split = path.Split('?', 2);
        var q = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (split.Length > 1)
            foreach (var kv in split[1].Split('&'))
            {
                var p = kv.Split('=', 2);
                if (p.Length == 2) q[Uri.UnescapeDataString(p[0])] = Uri.UnescapeDataString(p[1]);
            }

        object result;
        switch (split[0].ToLowerInvariant().TrimEnd('/'))
        {
            case "/descoop/hello":
                if (q.TryGetValue("via", out var via) && (IPAddress.TryParse(via, out _) || Uri.CheckHostName(via) == UriHostNameType.Dns))
                {
                    lock (_lock) _via[remote.ToString()] = via;
                    Write($"Client {remote} will use address {via}" + (q.TryGetValue("name", out var n) ? $" ({n})" : ""));
                }
                result = new { ok = true, app = "descoop", version = Version, name = _o.ServerName, yourIp = remote.ToString() };
                break;
            case "/descoop/status":
                result = GetStatus();
                break;
            case "/descoop/signs":
                result = new { app = "descoop", signs = ActiveSigns() };
                break;
            default:
                result = new { ok = true, app = "descoop", version = Version, name = _o.ServerName };
                break;
        }
        return Protocol.HttpOk(JsonSerializer.SerializeToUtf8Bytes(result, JsonOut), "application/json");
    }

    public ServerStatus GetStatus()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            // "[ip]" entries are connections seen before the game said who it is (login happens first).
            var players = _live.Values
                .Where(p => now - p.LastSeen < OnlineTtl && !p.CharacterId.StartsWith('['))
                .OrderBy(p => p.DisplayName)
                .Select(p => new PartyPlayerStatus(p.DisplayName, BlockNames.Get(p.LastBlock), p.LastBlock,
                    _sos.TryGetValue(p.CharacterId, out var s) && now - s.UpdatedAt < SignTtl, p.InSession,
                    (int)(now - p.LastSeen).TotalSeconds))
                .ToArray();
            return new ServerStatus("descoop", Version, _o.ServerName, players);
        }
    }

    // ------------------------------------------------------------------ game commands

    internal (byte cmd, byte[] data)? Dispatch(string cmd, Dictionary<string, string> p, string ip, int port)
    {
        lock (_lock)
        {
            // initializeCharacter carries the bare NPID; the real id (NPID + save slot) is assigned in the handler.
            if (p.TryGetValue("characterID", out var cid) && cmd is not ("updateOtherPlayerGrade.spd" or "initializeCharacter.spd"))
                _ipToChar[ip] = cid;
            string me = _ipToChar.TryGetValue(ip, out var known) ? known : $"[{ip}]";
            var live = Touch(me, ip, port);
            if (p.TryGetValue("blockID", out var blk) && cmd is "getSosData.spd" or "getBloodMessage.spd" or "getWanderingGhost.spd" or "getReplayList.spd")
                live.LastBlock = Protocol.ToSigned(blk);

            try
            {
                var r = cmd switch
                {
                    "login.spd" => Login(me, port),
                    "initializeCharacter.spd" => InitializeCharacter(p, ip, port),
                    "getQWCData.spd" => GetQwcData(),
                    "addQWCData.spd" => ((byte)0x09, [1]),
                    "getMultiPlayGrade.spd" => GetMultiPlayGrade(p),
                    "getBloodMessageGrade.spd" => GetBloodMessageGrade(p),
                    "getTimeMessage.spd" => ((byte)0x22, [0, 0, 0]),
                    "getAgreement.spd" or "addNewAccount.spd" => ((byte)0x01, new Payload().U8(1).U8(1).CStr("Hello!!\r\n").ToArray()),
                    "getBloodMessage.spd" => GetBloodMessage(p),
                    "addBloodMessage.spd" => AddBloodMessage(p),
                    "updateBloodMessageGrade.spd" => UpdateBloodMessageGrade(p),
                    "deleteBloodMessage.spd" => DeleteBloodMessage(p),
                    "getReplayList.spd" => GetReplayList(p),
                    "getReplayData.spd" => GetReplayData(p),
                    "addReplayData.spd" => AddReplayData(p),
                    "getWanderingGhost.spd" => GetWanderingGhost(p),
                    "setWanderingGhost.spd" => SetWanderingGhost(p, port),
                    "getSosData.spd" => GetSosData(p, me, port),
                    "addSosData.spd" => AddSosData(p, port),
                    "checkSosData.spd" => CheckSosData(p),
                    "outOfBlock.spd" => OutOfBlock(p),
                    "summonOtherCharacter.spd" => SummonOtherCharacter(p, me),
                    "summonBlackGhost.spd" => SummonBlackGhost(p, me),
                    "initializeMultiPlay.spd" => InitializeMultiPlay(p),
                    "finalizeMultiPlay.spd" => FinalizeMultiPlay(p),
                    "updateOtherPlayerGrade.spd" => UpdateOtherPlayerGrade(p, me),
                    _ => ((byte, byte[])?)null,
                };
                if (r == null) Write($"Unknown game command: {cmd}", false);
                return r;
            }
            finally
            {
                Changed?.Invoke();
            }
        }
    }

    PlayerLive Touch(string characterId, string ip, int port)
    {
        if (!_live.TryGetValue(characterId, out var l)) _live[characterId] = l = new PlayerLive { CharacterId = characterId };
        l.LastSeen = DateTime.UtcNow;
        l.Ip = ip;
        l.Port = port;
        return l;
    }

    void UpdatePosition(string characterId, int block, float x, float y, float z, float ax, float ay, float az, bool remember = true)
    {
        if (!_live.TryGetValue(characterId, out var l)) _live[characterId] = l = new PlayerLive { CharacterId = characterId };
        l.LastPos = new WorldPos(block, x, y, z, ax, ay, az, DateTime.UtcNow);
        l.LastBlock = block;
        if (remember && float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z))
        {
            _store.AddKnownPosition(block, x, y, z, ay);
            _store.MarkDirty();
        }
    }

    bool SameRegion(int a, int b) => _o.MergeRegions || a == b;

    (byte, byte[]) Login(string me, int port)
    {
        var st = GetStatus();
        var motd = new StringBuilder();
        motd.Append($"{Ascii(_o.ServerName)}  -  Seamless Co-op\r\n\r\n");
        motd.Append("HOW TO PLAY TOGETHER\r\n");
        motd.Append("1) Pick who leads: that player is the HOST.\r\n");
        motd.Append("2) HELPER: take the Cyanide Pill to become a\r\n");
        motd.Append("   ghost, then use the JOIN SIGIL.\r\n");
        motd.Append("3) HOST: use the HOST SIGIL to be human, then\r\n");
        motd.Append("   touch the sign - it appears right next to\r\n");
        motd.Append("   you, in any area.\r\n");
        motd.Append("4) TREASURE and NPCs work for both of you.\r\n");
        motd.Append("5) BOSSES count for both. Rejoin anytime,\r\n");
        motd.Append("   even with the boss already dead.\r\n");
        var motd2 = new StringBuilder();
        motd2.Append($"Players online: {st.Players.Length}\r\n");
        foreach (var pl in st.Players.Take(8))
            motd2.Append($"{Ascii(pl.Name)} - {pl.Area}{(pl.HasSign ? " [sign]" : "")}\r\n");
        return (0x02, new Payload().U8(1).U8(2).CStr(motd.ToString()).CStr(motd2.ToString()).ToArray());
    }

    static string Ascii(string s) => new(s.Normalize(NormalizationForm.FormD).Where(c => c < 128).ToArray());

    (byte, byte[]) InitializeCharacter(Dictionary<string, string> p, string ip, int port)
    {
        string id = p["characterID"] + (p.TryGetValue("index", out var idx) && idx.Length > 0 ? idx[..1] : "0");
        _ipToChar[ip] = id;
        _live.Remove($"[{ip}]");
        _store.Stats(id);
        _store.MarkDirty();
        Touch(id, ip, port);
        Write($"{id} joined the server");
        return (0x17, new Payload().CStr(id).ToArray());
    }

    /// <summary>7 worlds × (white/black, light/dark) tendency, as the retail server sent them.</summary>
    (byte, byte[]) GetQwcData()
    {
        int wb = Math.Clamp(_o.WorldTendency, -200, 200);
        var w = new Payload();
        for (int i = 0; i < 7; i++) w.I32(wb).I32(0);
        return (0x0e, w.ToArray());
    }

    PlayerStats StatsByNpid(string npid) =>
        _store.Players.TryGetValue(npid, out var s) ? s : _store.Players.TryGetValue(npid + "0", out s) ? s : _store.Stats(npid);

    (byte, byte[]) GetMultiPlayGrade(Dictionary<string, string> p)
    {
        var s = StatsByNpid(p.GetValueOrDefault("NPID", ""));
        var w = new Payload().U8(1);
        foreach (var r in s.Ratings()) w.I32(r);
        w.I32(s.Sessions);
        return (0x28, w.ToArray());
    }

    (byte, byte[]) GetBloodMessageGrade(Dictionary<string, string> p) =>
        (0x29, new Payload().U8(1).I32(StatsByNpid(p.GetValueOrDefault("NPID", "")).MessageRating).ToArray());

    // ---- messages

    (byte, byte[]) GetBloodMessage(Dictionary<string, string> p)
    {
        string me = p.GetValueOrDefault("characterID", "");
        int block = Protocol.ToSigned(p["blockID"]);
        int num = Protocol.I(p.GetValueOrDefault("replayNum", "20"));
        var inBlock = _store.Messages.Where(m => m.BlockId == block).ToList();
        var pick = inBlock.Where(m => m.CharacterId == me).OrderBy(_ => _rng.Next())
            .Concat(inBlock.Where(m => m.CharacterId != me).OrderBy(_ => _rng.Next()))
            .Take(num).ToList();
        var w = new Payload().I32(pick.Count);
        foreach (var m in pick) w.Bytes(m.Serialize());
        return (0x1f, w.ToArray());
    }

    (byte, byte[]) AddBloodMessage(Dictionary<string, string> p)
    {
        var m = new BloodMessage
        {
            CharacterId = p["characterID"],
            BlockId = Protocol.ToSigned(p["blockID"]),
            PosX = Protocol.F(p["posx"]), PosY = Protocol.F(p["posy"]), PosZ = Protocol.F(p["posz"]),
            AngX = Protocol.F(p["angx"]), AngY = Protocol.F(p["angy"]), AngZ = Protocol.F(p["angz"]),
            MessageId = Protocol.I(p["messageID"]), MainMsgId = Protocol.I(p["mainMsgID"]), AddMsgCateId = Protocol.I(p["addMsgCateID"]),
        };
        UpdatePosition(m.CharacterId, m.BlockId, m.PosX, m.PosY, m.PosZ, m.AngX, m.AngY, m.AngZ);
        if (m.MainMsgId != 13002)
        {
            m.BmId = _store.NextMessageId++;
            _store.Messages.Add(m);
            if (_store.Messages.Count > 5000) _store.Messages.RemoveAt(0);
            _store.MarkDirty();
        }
        return (0x1d, [1]);
    }

    (byte, byte[]) UpdateBloodMessageGrade(Dictionary<string, string> p)
    {
        int id = Protocol.I(p["bmID"]);
        var m = _store.Messages.FirstOrDefault(x => x.BmId == id);
        if (m != null)
        {
            m.Rating++;
            _store.Stats(m.CharacterId).MessageRating++;
            _store.MarkDirty();
        }
        return (0x2a, [1]);
    }

    (byte, byte[]) DeleteBloodMessage(Dictionary<string, string> p)
    {
        int id = Protocol.I(p["bmID"]);
        _store.Messages.RemoveAll(x => x.BmId == id);
        _store.MarkDirty();
        return (0x27, [1]);
    }

    // ---- replays (bloodstains)

    (byte, byte[]) GetReplayList(Dictionary<string, string> p)
    {
        int block = Protocol.ToSigned(p["blockID"]);
        int num = Protocol.I(p.GetValueOrDefault("replayNum", "20"));
        var pick = _store.Replays.Where(r => r.BlockId == block).OrderBy(_ => _rng.Next()).Take(num).ToList();
        var w = new Payload().I32(pick.Count);
        foreach (var r in pick) w.Bytes(r.SerializeHeader());
        return (0x1f, w.ToArray());
    }

    (byte, byte[]) GetReplayData(Dictionary<string, string> p)
    {
        int id = Protocol.I(p["ghostID"]);
        var r = _store.Replays.FirstOrDefault(x => x.GhostId == id);
        string bin = r?.ReplayBinary ?? "";
        return (0x1e, new Payload().I32(id).I32(bin.Length).Str(bin).ToArray());
    }

    (byte, byte[]) AddReplayData(Dictionary<string, string> p)
    {
        var raw = Protocol.DecodeBrokenBase64(p["replayBinary"]);
        if (ParseReplay(raw) != null)
        {
            var r = new ReplayEntry
            {
                GhostId = _store.NextReplayId++,
                CharacterId = p["characterID"],
                BlockId = Protocol.ToSigned(p["blockID"]),
                PosX = Protocol.F(p["posx"]), PosY = Protocol.F(p["posy"]), PosZ = Protocol.F(p["posz"]),
                AngX = Protocol.F(p["angx"]), AngY = Protocol.F(p["angy"]), AngZ = Protocol.F(p["angz"]),
                MessageId = Protocol.I(p["messageID"]), MainMsgId = Protocol.I(p["mainMsgID"]), AddMsgCateId = Protocol.I(p["addMsgCateID"]),
                ReplayBinary = Protocol.EncodeGameBase64(raw),
            };
            _store.Replays.Add(r);
            if (_store.Replays.Count > 2000) _store.Replays.RemoveAt(0);
            _store.MarkDirty();
            UpdatePosition(r.CharacterId, r.BlockId, r.PosX, r.PosY, r.PosZ, r.AngX, r.AngY, r.AngZ);
        }
        return (0x1d, [1]);
    }

    /// <summary>Validates a zlib replay and returns its last recorded position (big endian floats).</summary>
    internal static float[]? ParseReplay(byte[] raw)
    {
        try
        {
            using var z = new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            z.CopyTo(ms);
            var d = ms.ToArray();
            uint count = BinaryPrimitives.ReadUInt32BigEndian(d);
            if (count > 100000) return null;
            int expected = 12 + (int)count * 32 + 80 + 34;
            if (d.Length != expected) return null;
            if (count == 0) return [];
            int o = 12 + ((int)count - 1) * 32;
            var f = new float[6];
            for (int i = 0; i < 6; i++) f[i] = BinaryPrimitives.ReadSingleBigEndian(d.AsSpan(o + i * 4));
            return f;
        }
        catch { return null; }
    }

    // ---- wandering ghosts

    void PurgeGhosts()
    {
        var now = DateTime.UtcNow;
        foreach (var k in _ghosts.Where(g => now - g.Value.At > GhostTtl).Select(g => g.Key).ToList()) _ghosts.Remove(k);
    }

    (byte, byte[]) GetWanderingGhost(Dictionary<string, string> p)
    {
        PurgeGhosts();
        string me = p.GetValueOrDefault("characterID", "");
        int block = Protocol.ToSigned(p["blockID"]);
        int max = Protocol.I(p.GetValueOrDefault("maxGhostNum", "10"));
        var cands = _ghosts.Values.Where(g => g.BlockId == block && g.CharacterId != me).OrderBy(_ => _rng.Next()).Take(max).ToList();
        var w = new Payload().I32(0).I32(cands.Count);
        foreach (var g in cands)
        {
            var b64 = Protocol.EncodeGameBase64(g.ReplayData);
            w.I32(b64.Length).Str(b64);
        }
        return (0x11, w.ToArray());
    }

    (byte, byte[]) SetWanderingGhost(Dictionary<string, string> p, int port)
    {
        string me = p["characterID"];
        int block = Protocol.ToSigned(p["ghostBlockID"]);
        var raw = Protocol.DecodeBrokenBase64(p["replayData"]);
        var last = ParseReplay(raw);
        if (last != null)
        {
            if (_ghosts.TryGetValue(me, out var prev) && prev.BlockId != block)
                Write($"{me} moved to {BlockNames.Get(block)}", false);
            _ghosts[me] = new Ghost { CharacterId = me, BlockId = block, ReplayData = raw };
            if (last.Length == 6) UpdatePosition(me, block, last[0], last[1], last[2], last[3], last[4], last[5]);
        }
        return (0x17, [1]);
    }

    // ---- summon signs

    void PurgeSigns()
    {
        var now = DateTime.UtcNow;
        foreach (var k in _sos.Where(s => now - s.Value.UpdatedAt > SignTtl).Select(s => s.Key).ToList())
        {
            _sos.Remove(k);
            Write($"Sign of {k} expired", false);
        }
    }

    /// <summary>Where to put a party member's sign so the player at <paramref name="block"/> sees it.</summary>
    bool TryAnchor(string me, int block, out WorldPos anchor)
    {
        if (_live.TryGetValue(me, out var l) && l.LastPos is { } lp && lp.BlockId == block && DateTime.UtcNow - lp.At < TimeSpan.FromMinutes(15))
        {
            anchor = lp;
            return true;
        }
        if (_store.KnownPositions.TryGetValue(block, out var list) && list.Count > 0)
        {
            var k = list[^1];
            anchor = new WorldPos(block, k[0], k[1], k[2], 0, k[3], 0, DateTime.UtcNow);
            return true;
        }
        anchor = default;
        return false;
    }

    (byte, byte[]) GetSosData(Dictionary<string, string> p, string me, int port)
    {
        PurgeSigns();
        int block = Protocol.ToSigned(p["blockID"]);
        // The reference server (desse) ignores the client's sosNum and returns every matching sign; the game
        // often asks for 0, so honouring it would return nothing (this was why signs never appeared). Cap only
        // to keep the response sane.
        int clientAsked = Protocol.I(p.GetValueOrDefault("sosNum", "0"));
        const int max = 100;
        var knownIds = new HashSet<string>(p.GetValueOrDefault("sosList", "").Split("a0a"));
        var known = new List<uint>();
        var fresh = new List<byte[]>();
        WorldPos anchor = default;
        bool hasAnchor = false;
        int slot = 0;

        int considered = 0, relocated = 0, skipped = 0;
        foreach (var s in _sos.Values.OrderByDescending(s => s.UpdatedAt))
        {
            if (known.Count + fresh.Count >= max) break;
            if (!SameRegion(s.Port, port)) { skipped++; continue; }
            considered++;
            bool sameBlock = s.BlockId == block;
            bool mine = s.CharacterId == me;

            // Your own sign only shows in its own block (the client hides it anyway). Another player's blue
            // co-op sign is moved right next to you whenever we know where you are, so it is always reachable —
            // that is the "seamless" part. Only red/invasion signs are left where they were placed.
            bool place = sameBlock;
            bool doRelocate = false;
            if (!mine && s.IsCoopSign && _o.PartySigns)
            {
                if (hasAnchor || (hasAnchor = TryAnchor(me, block, out anchor))) { place = true; doRelocate = true; }
            }
            if (!place) { skipped++; if (!mine) Write($"getSosData {me} block {block}: {s.CharacterId}'s sign in block {s.BlockId} NOT shown (no anchor to move it here)", false); continue; }

            if (knownIds.Contains(s.SosId.ToString())) { known.Add(s.SosId); continue; }
            if (doRelocate)
            {
                double yaw = anchor.AngY + (slot - 0.5) * 0.6;
                float x = anchor.X + (float)(Math.Sin(yaw) * 1.2);
                float z = anchor.Z + (float)(Math.Cos(yaw) * 1.2);
                fresh.Add(s.Serialize(x, anchor.Y, z, anchor.AngX, anchor.AngY, anchor.AngZ));
                slot++; relocated++;
                if (!mine) Write($"getSosData {me} block {block}: showing {s.CharacterId}'s sign next to you (from block {s.BlockId})", false);
            }
            else fresh.Add(s.Serialize());
        }
        if (_sos.Count > 0)
            Write($"getSosData {me} block {block} port {port} (asked {clientAsked}): {_sos.Count} sign(s), {considered} in region, returned {known.Count + fresh.Count} ({relocated} moved), skipped {skipped}", false);

        var w = new Payload().I32(known.Count);
        foreach (var id in known) w.U32(id);
        w.I32(fresh.Count);
        foreach (var f in fresh) w.Bytes(f);
        return (0x0f, w.ToArray());
    }

    /// <summary>Active signs, for the /descoop/signs diagnostic endpoint.</summary>
    public object[] ActiveSigns()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            return _sos.Values.OrderByDescending(s => s.UpdatedAt).Select(s => (object)new
            {
                who = s.CharacterId,
                area = BlockNames.Get(s.BlockId),
                blockId = s.BlockId,
                color = s.IsBlack == 2 ? "blue" : s.IsBlack == 1 ? "red" : s.IsBlack == 3 ? "invasion" : $"other({s.IsBlack})",
                ageSeconds = (int)(now - s.UpdatedAt).TotalSeconds,
                port = s.Port,
            }).ToArray();
        }
    }

    (byte, byte[]) AddSosData(Dictionary<string, string> p, int port)
    {
        var s = SosSign.FromParams(p, _nextSos++, port);
        var st = _store.Stats(s.CharacterId);
        s.Ratings = st.Ratings();
        s.TotalSessions = st.Sessions;
        _sos[s.CharacterId] = s;
        _pendingSummon.Remove(s.CharacterId);
        UpdatePosition(s.CharacterId, s.BlockId, s.PosX, s.PosY, s.PosZ, s.AngX, s.AngY, s.AngZ);
        Write($"{s.CharacterId} placed a {(s.IsCoopSign ? "blue" : "red")} sign in {BlockNames.Get(s.BlockId)}");
        return (0x0a, [1]);
    }

    (byte, byte[]) CheckSosData(Dictionary<string, string> p)
    {
        string id = p.GetValueOrDefault("characterID", "");
        if (_sos.TryGetValue(id, out var s)) s.UpdatedAt = DateTime.UtcNow;
        if (_pendingMonk.Remove(id, out var monkRoom))
        {
            Write($"{id} summoned as the Old Monk", false);
            return (0x0b, Protocol.Raw.GetBytes(monkRoom));
        }
        if (_pendingSummon.Remove(id, out var room))
        {
            Write($"{id} is being summoned", false);
            return (0x0b, Protocol.Raw.GetBytes(room));
        }
        return (0x0b, [0]);
    }

    (byte, byte[]) OutOfBlock(Dictionary<string, string> p)
    {
        _sos.Remove(p.GetValueOrDefault("characterID", ""));
        return (0x15, [1]);
    }

    (byte, byte[]) SummonOtherCharacter(Dictionary<string, string> p, string me)
    {
        uint ghostId = (uint)Protocol.ToSigned(p["ghostID"]);
        string room = p.GetValueOrDefault("NPRoomID", "");
        var s = _sos.Values.FirstOrDefault(x => x.SosId == ghostId);
        if (s == null)
        {
            Write($"{me} tried to summon a sign that is gone (#{ghostId})");
            return (0x0a, [0]);
        }
        _pendingSummon[s.CharacterId] = room;
        Write($"{me} is summoning {s.CharacterId}", false);
        return (0x0a, [1]);
    }

    (byte, byte[]) SummonBlackGhost(Dictionary<string, string> p, string me)
    {
        var s = _sos.Values.FirstOrDefault(x => MonkBlocks.Contains(x.BlockId) && x.CharacterId != me);
        if (s == null) return (0x23, [0]);
        _pendingMonk[s.CharacterId] = p.GetValueOrDefault("NPRoomID", "");
        return (0x23, [1]);
    }

    (byte, byte[]) InitializeMultiPlay(Dictionary<string, string> p)
    {
        string id = p.GetValueOrDefault("characterID", "");
        _store.Stats(id).Sessions++;
        _store.MarkDirty();
        if (_live.TryGetValue(id, out var l)) l.InSession = true;
        Write($"{id} started a co-op session");
        return (0x15, [1]);
    }

    (byte, byte[]) FinalizeMultiPlay(Dictionary<string, string> p)
    {
        string id = p.GetValueOrDefault("characterID", "");
        var st = _store.Stats(id);
        if (p.GetValueOrDefault("gradeS") == "1") st.GradeS++;
        else if (p.GetValueOrDefault("gradeA") == "1") st.GradeA++;
        else if (p.GetValueOrDefault("gradeB") == "1") st.GradeB++;
        else if (p.GetValueOrDefault("gradeC") == "1") st.GradeC++;
        else if (p.GetValueOrDefault("gradeD") == "1") st.GradeD++;
        _store.MarkDirty();
        if (_live.TryGetValue(id, out var l)) l.InSession = false;
        Write($"{id} finished a co-op session");
        return (0x21, [1]);
    }

    (byte, byte[]) UpdateOtherPlayerGrade(Dictionary<string, string> p, string me)
    {
        string other = p.GetValueOrDefault("characterID", "") + "0";
        var st = _store.Stats(other);
        switch (Protocol.I(p.GetValueOrDefault("grade", "2")))
        {
            case 0: st.GradeS++; break;
            case 1: st.GradeA++; break;
            case 2: st.GradeB++; break;
            case 3: st.GradeC++; break;
            default: st.GradeD++; break;
        }
        _store.MarkDirty();
        return (0x2b, [1]);
    }
}
