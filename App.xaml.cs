using System.Windows;
using TenXBar.Models;
using TenXBar.Services;
using TenXBar.Views;

namespace TenXBar;

public partial class App : Application
{
    public static TrayService Tray { get; } = new();

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, exArgs) =>
        {
            try
            {
                string crashPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "10xbar", "crash.txt");
                System.IO.File.WriteAllText(crashPath, exArgs.Exception.ToString());
            }
            catch { }
        };

        var args = e.Args;

        // 1. Check if launching a specific folder group
        if (args.Length >= 2 && args[0].Equals("launch", StringComparison.OrdinalIgnoreCase))
        {
            string groupIdentifier = args[1].Trim('\"');
            var storage = new StorageService();
            var groups = storage.LoadGroups();

            var targetGroup = groups.FirstOrDefault(g => 
                g.Id.Equals(groupIdentifier, StringComparison.OrdinalIgnoreCase) ||
                g.Name.Equals(groupIdentifier, StringComparison.OrdinalIgnoreCase));

            if (targetGroup != null)
            {
                var flyout = new FlyoutWindow(targetGroup, isStandaloneLaunch: true);
                flyout.Show();
                return;
            }
            else
            {
                MessageBox.Show(
                    $"10xbar: Folder group '{groupIdentifier}' not found.",
                    "10xbar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                Shutdown();
                return;
            }
        }

        // 2. Generate shortcuts CLI command
        if (args.Length >= 1 && (args[0].Equals("generate-shortcuts", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--generate-shortcuts", StringComparison.OrdinalIgnoreCase)))
        {
            var storage = new StorageService();
            var groups = storage.LoadGroups();
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? 
                             System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");

            foreach (var group in groups)
            {
                ShortcutService.CreateOrUpdateGroupShortcut(group, exePath);
            }
            Shutdown();
            return;
        }

        // Keep running in background when main window is closed
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 3. Silent / Background Startup mode (Run at Windows boot)
        bool isSilent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || 
                                     a.Equals("--startup", StringComparison.OrdinalIgnoreCase));

        if (isSilent)
        {
            Tray.Initialize(null);
            return;
        }

        // 4. Normal UI mode
        var mainWindow = new MainWindow();
        Tray.Initialize(mainWindow);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Tray.Dispose();
        base.OnExit(e);
    }
}
