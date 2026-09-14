using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesCoop.App;

static class Ui
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Dark native title bar (Windows 10 20H1+ / 11) so the chrome matches the theme.</summary>
    public static void DarkTitleBar(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0) DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
            int caption = 0x000A0808; // COLORREF (BGR) of #08080A, Windows 11 only
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
        };
    }

    public static void Info(Window owner, string text) =>
        MessageBox.Show(owner, text, "DeS Seamless Co-op", MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Warn(Window owner, string text) =>
        MessageBox.Show(owner, text, "DeS Seamless Co-op", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static bool Ask(Window owner, string text) =>
        MessageBox.Show(owner, text, "DeS Seamless Co-op", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
