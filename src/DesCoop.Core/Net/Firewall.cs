using System.Diagnostics;

namespace DesCoop.Net;

/// <summary>
/// Adds inbound Windows Firewall rules for the party server and RPCS3 (P2P) with a single UAC prompt.
/// Without them Windows may silently block the friend when the network is set as "Public".
/// </summary>
public static class Firewall
{
    const string ServerRule = "DeS Seamless Co-op (server)";
    const string EmuRule = "DeS Seamless Co-op (RPCS3)";

    public static bool RuleExists(string name)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{name}\"")
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static bool AllConfigured(string rpcs3Exe) => RuleExists(ServerRule) && (!File.Exists(rpcs3Exe) || RuleExists(EmuRule));

    /// <summary>Returns false if the user declined the UAC prompt.</summary>
    public static bool Configure(string appExe, string rpcs3Exe)
    {
        var cmds = new List<string>
        {
            $"netsh advfirewall firewall delete rule name=\"{ServerRule}\"",
            $"netsh advfirewall firewall add rule name=\"{ServerRule}\" dir=in action=allow protocol=TCP localport=18000,18666-18668 program=\"{appExe}\" profile=any",
        };
        if (File.Exists(rpcs3Exe))
        {
            cmds.Add($"netsh advfirewall firewall delete rule name=\"{EmuRule}\"");
            cmds.Add($"netsh advfirewall firewall add rule name=\"{EmuRule}\" dir=in action=allow program=\"{rpcs3Exe}\" profile=any");
        }
        try
        {
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + string.Join(" & ", cmds))
            { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })!;
            p.WaitForExit(20000);
            return true;
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // UAC declined
    }
}
