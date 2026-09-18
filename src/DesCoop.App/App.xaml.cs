using System.Runtime.InteropServices;
using System.Windows;
using DesCoop.Party;
using DesCoop.Server;

namespace DesCoop.App;

public partial class App : Application
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--server", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (!AttachConsole(-1)) AllocConsole();
            RunHeadless(e.Args).ContinueWith(_ => Dispatcher.Invoke(Shutdown));
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, ev) =>
        {
            ev.Handled = true;
            WriteCrash(ev.Exception);
            MessageBox.Show(ev.Exception.Message, "DeS Seamless Co-op", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) => WriteCrash(ev.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ev) => { WriteCrash(ev.Exception); ev.SetObserved(); };

        // Installer hook: DesCoop.exe --setup [--game "<folder>"] prepares everything, then exits.
        if (e.Args.Contains("--setup", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = new SetupWindow(Arg(e.Args, "--game"));
            MainWindow.Show();
            return;
        }
        // Experimental sandbox: DesCoop.exe --seamless uses a separate settings profile and the beefier
        // "Seamless (TEST)" preset, so it never disturbs the stable co-op the user plays with.
        AppSettings.Seamless = e.Args.Contains("--seamless", StringComparer.OrdinalIgnoreCase);

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    static string? Arg(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        var v = i >= 0 && i + 1 < args.Length ? args[i + 1].Trim().Trim('"') : null;
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    static void WriteCrash(Exception? ex)
    {
        try { File.AppendAllText(Path.Combine(AppSettings.DataDir, "crash.log"), $"[{DateTime.Now:s}] {ex}\n\n"); } catch { }
    }

    /// <summary>Dedicated server mode (for a VPS or an always-on PC): DesCoop.exe --server [--name "X"] [--no-upnp]</summary>
    static async Task RunHeadless(string[] args)
    {
        string name = Arg(args, "--name") ?? "DeS Seamless Co-op";
        bool upnp = !args.Contains("--no-upnp", StringComparer.OrdinalIgnoreCase);

        var opts = new DesServerOptions { ServerName = name, DataDir = Path.Combine(AppSettings.DataDir, "server-data") };
        string password = Arg(args, "--password") ?? Net.Rendezvous.NewPassword();
        await using var host = new PartyHost(opts, password);
        host.Log += Console.WriteLine;
        await host.StartAsync(upnp);
        Console.WriteLine();
        Console.WriteLine($"Party: {host.Name}   Password: {host.Password}");
        Console.WriteLine("Direct code: " + host.Code);
        Console.WriteLine("Press Ctrl+C to stop.");
        var done = new TaskCompletionSource();
        Console.CancelKeyPress += (_, ev) => { ev.Cancel = true; done.TrySetResult(); };
        await done.Task;
    }
}
