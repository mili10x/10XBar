using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using TenXBar.Models;
using TenXBar.Views;

namespace TenXBar.Services;

public class TrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly StorageService _storage = new();
    private MainWindow? _mainWindow;

    public void Initialize(MainWindow? mainWindow)
    {
        _mainWindow = mainWindow;

        try
        {
            _notifyIcon = new NotifyIcon();

            // Find icon file
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(baseDir, "Resources", "app.ico");

            if (File.Exists(icoPath))
            {
                _notifyIcon.Icon = new Icon(icoPath);
            }
            else
            {
                // Fallback to executable icon
                string? exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    _notifyIcon.Icon = Icon.ExtractAssociatedIcon(exe);
                }
            }

            _notifyIcon.Text = "10xbar - Windows Taskbar Folders";
            _notifyIcon.Visible = true;

            UpdateContextMenu();

            _notifyIcon.DoubleClick += (s, e) => ShowDashboard();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize TrayService: {ex.Message}");
        }
    }

    public void UpdateContextMenu()
    {
        if (_notifyIcon == null) return;

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("⚡ Open 10xbar Dashboard", null, (s, e) => ShowDashboard());
        openItem.Font = new Font(openItem.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        // Dynamic folders list
        var groups = _storage.LoadGroups();
        if (groups.Count > 0)
        {
            var foldersHeader = new ToolStripMenuItem("📁 Your Folders") { Enabled = false };
            menu.Items.Add(foldersHeader);

            foreach (var g in groups)
            {
                var groupItem = new ToolStripMenuItem($"  {g.Name} ({g.Items.Count} apps)");
                groupItem.Click += (s, e) => OpenGroupFlyout(g);
                menu.Items.Add(groupItem);
            }

            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(new ToolStripMenuItem("📂 Shortcuts Folder", null, (s, e) =>
        {
            ShortcutService.RevealInExplorer(StorageService.ShortcutsDirectory);
        }));

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("❌ Exit 10xbar", null, (s, e) =>
        {
            Dispose();
            System.Windows.Application.Current.Shutdown();
        }));

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void ShowDashboard()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_mainWindow == null || !_mainWindow.IsLoaded)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    private void OpenGroupFlyout(FolderGroup group)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var flyout = new FlyoutWindow(group, isStandaloneLaunch: false);
            flyout.Show();
        });
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
