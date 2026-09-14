using DesCoop.Net;

namespace DesCoop.Party;

/// <summary>
/// Friend side: finds the party by name + password, then connects directly (LAN/VPN/open ports)
/// or, if that fails, through the relay with a local forwarder. <see cref="Address"/> is what RPCS3
/// must point the Demon's Souls hostnames at.
/// </summary>
public sealed class PartyClient : IDisposable
{
    public string PartyName { get; }
    public string Address { get; }
    public bool ViaRelay => _forwarder != null;
    readonly RelayForwarder? _forwarder;

    PartyClient(string name, string address, RelayForwarder? forwarder)
    {
        PartyName = name;
        Address = address;
        _forwarder = forwarder;
    }

    public static async Task<PartyClient> ConnectAsync(string name, string password, string? playerName, Action<string>? log, CancellationToken ct = default)
    {
        name = name.Trim();
        // Old style long code (or a bare IP) still works: direct connection only.
        if (PartyCode.TryDecode(name, out var codeName, out var codeAddrs))
        {
            var d = await NetUtil.FindReachableAsync(codeAddrs, playerName, ct)
                    ?? throw new InvalidOperationException("The host did not answer on any address in that code.");
            return new PartyClient(d.Name, d.Address, null);
        }

        log?.Invoke($"Looking for party \"{name}\"…");
        PartyInvite? invite;
        try { invite = await Rendezvous.FindAsync(name, password, ct); }
        catch (Exception ex) { throw new InvalidOperationException("Could not reach the party directory (ntfy.sh): " + ex.Message); }
        if (invite == null)
            throw new InvalidOperationException($"No party \"{name}\" with that password is online.\n\nCheck the name and password, and make sure the host pressed \"Create Party\" and kept the app open.");

        log?.Invoke("Party found. Trying a direct connection…");
        var direct = await NetUtil.FindReachableAsync(invite.Direct, playerName, ct);
        if (direct != null)
        {
            log?.Invoke($"Direct connection via {direct.Address}.");
            return new PartyClient(invite.Name, direct.Address, null);
        }

        if (invite.Relay == null || invite.RelayPorts == null || invite.RelayPorts.Count == 0)
            throw new InvalidOperationException("The host can't be reached directly and has no relay running.");

        log?.Invoke($"No direct route. Connecting through the relay ({invite.Relay})…");
        var fwd = new RelayForwarder(invite.Relay, invite.RelayPorts, playerName ?? Environment.UserName);
        if (log != null) fwd.Log += log;
        try { fwd.Start(); }
        catch (System.Net.Sockets.SocketException)
        {
            fwd.Dispose();
            throw new InvalidOperationException("Ports 18000/18666-18668 are busy on this PC. Close any other Demon's Souls server or party you are hosting.");
        }
        var hello = await NetUtil.HelloAsync("127.0.0.1", playerName, ct);
        if (hello == null)
        {
            fwd.Dispose();
            throw new InvalidOperationException("The relay did not reach the host. Ask the host to close and create the party again.");
        }
        log?.Invoke("Connected through the relay.");
        return new PartyClient(invite.Name, "127.0.0.1", fwd);
    }

    public void Dispose() => _forwarder?.Dispose();
}
