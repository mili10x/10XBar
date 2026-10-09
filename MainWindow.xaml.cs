using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TenXBar.Models;
using TenXBar.Services;
using TenXBar.Views;

namespace TenXBar;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly StorageService _storage = new();
    private readonly ObservableCollection<FolderGroup> _groups = new();
    private FolderGroup? _selectedGroup;
    private bool _isUpdatingUi = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Dark titlebar
        var hwnd = new WindowInteropHelper(this).Handle;
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        LoadGroups();
        AutoStartCheckBox.IsChecked = StartupService.IsStartupEnabled();
        PresetIconsList.ItemsSource = IconExtractorService.GetPresetIcons();
    }

    private void LoadGroups()
    {
        _groups.Clear();
        var loaded = _storage.LoadGroups();
        bool hasChanges = false;
        foreach (var g in loaded)
        {
            foreach (var item in g.Items)
            {
                string cleaned = SanitizeAppName(item.Name);
                if (cleaned != item.Name)
                {
                    item.Name = cleaned;
                    hasChanges = true;
                }
            }
            _groups.Add(g);
        }

        if (hasChanges)
        {
            _storage.SaveGroups(_groups.ToList());
        }

        GroupsListBox.ItemsSource = _groups;

        if (_groups.Count > 0)
        {
            GroupsListBox.SelectedIndex = 0;
        }
        else
        {
            DetailsContainer.Visibility = Visibility.Collapsed;
        }
    }

    private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedGroup = GroupsListBox.SelectedItem as FolderGroup;
        if (_selectedGroup == null)
        {
            DetailsContainer.Visibility = Visibility.Collapsed;
            return;
        }

        DetailsContainer.Visibility = Visibility.Visible;
        _isUpdatingUi = true;

        GroupNameTextBox.Text = _selectedGroup.Name;
        ColumnsComboBox.SelectedIndex = Math.Clamp(_selectedGroup.Columns - 2, 0, 4);

        // Map accent color
        AccentColorComboBox.SelectedIndex = _selectedGroup.AccentColor switch
        {
            "#10B981" => 1,
            "#8B5CF6" => 2,
            "#F59E0B" => 3,
            "#EC4899" => 4,
            "#06B6D4" => 5,
            _ => 0
        };

        UpdateFolderIconPreview();

        AppsListBox.ItemsSource = _selectedGroup.Items;
        UpdateItemsCount();

        _isUpdatingUi = false;
    }

    private void UpdateFolderIconPreview()
    {
        if (_selectedGroup == null) return;

        if (!string.IsNullOrEmpty(_selectedGroup.IconPath) && File.Exists(_selectedGroup.IconPath))
        {
            try
            {
                using var stream = new FileStream(_selectedGroup.IconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                SelectedFolderIconPreview.Source = bmp;
            }
            catch
            {
                SelectedFolderIconPreview.Source = null;
            }
        }
        else
        {
            SelectedFolderIconPreview.Source = null;
        }

        IconStatusText.Text = _selectedGroup.HasCustomIcon
            ? "10X custom/preset icon active"
            : "Default generated folder icon";
    }

    private void UpdateItemsCount()
    {
        if (_selectedGroup == null) return;
        ItemsCountLabel.Text = $" ({_selectedGroup.Items.Count})";
        EmptyStatePanel.Visibility = _selectedGroup.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedGroup == null) return;
        _selectedGroup.Name = GroupNameTextBox.Text;

        if (!_selectedGroup.HasCustomIcon)
        {
            var (png, _) = IconExtractorService.GenerateGroupFolderIcon(_selectedGroup.Id, _selectedGroup.Name, _selectedGroup.AccentColor);
            _selectedGroup.IconPath = png;
            UpdateFolderIconPreview();
        }

        _storage.SaveGroups(_groups.ToList());
    }

    private void ColumnsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedGroup == null) return;
        _selectedGroup.Columns = ColumnsComboBox.SelectedIndex + 2;
        _storage.SaveGroups(_groups.ToList());
    }

    private void AccentColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedGroup == null) return;

        string hex = AccentColorComboBox.SelectedIndex switch
        {
            1 => "#10B981",
            2 => "#8B5CF6",
            3 => "#F59E0B",
            4 => "#EC4899",
            5 => "#06B6D4",
            _ => "#3B82F6"
        };

        _selectedGroup.AccentColor = hex;

        if (!_selectedGroup.HasCustomIcon)
        {
            var (png, _) = IconExtractorService.GenerateGroupFolderIcon(_selectedGroup.Id, _selectedGroup.Name, hex);
            _selectedGroup.IconPath = png;
            UpdateFolderIconPreview();
        }

        _storage.SaveGroups(_groups.ToList());
    }

    private void ChangeFolderIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup == null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Select Folder Icon from System",
            Filter = "Icons & Images (*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll)|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            var (pngPath, icoPath) = IconExtractorService.SetCustomGroupIcon(_selectedGroup.Id, dialog.FileName);
            if (!string.IsNullOrEmpty(pngPath) && File.Exists(pngPath))
            {
                _selectedGroup.IconPath = pngPath;
                _selectedGroup.HasCustomIcon = true;
                _storage.SaveGroups(_groups.ToList());

                UpdateFolderIconPreview();

                try
                {
                    int sel = GroupsListBox.SelectedIndex;
                    GroupsListBox.Items.Refresh();
                    GroupsListBox.SelectedIndex = sel;
                }
                catch { }

                // Update shortcut if present
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ??
                                 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");
                ShortcutService.CreateOrUpdateGroupShortcut(_selectedGroup, exePath);
            }
        }
    }

    private void ResetFolderIcon_Click(object sender, RoutedEventArgs e)
    {
        _selectedGroup ??= GroupsListBox.SelectedItem as FolderGroup;
        if (_selectedGroup == null && _groups.Count > 0)
        {
            _selectedGroup = _groups[0];
            GroupsListBox.SelectedItem = _selectedGroup;
        }
        if (_selectedGroup == null) return;

        _selectedGroup.HasCustomIcon = false;
        var (png, _) = IconExtractorService.GenerateGroupFolderIcon(_selectedGroup.Id, _selectedGroup.Name, _selectedGroup.AccentColor);
        _selectedGroup.IconPath = png;
        _storage.SaveGroups(_groups.ToList());

        UpdateFolderIconPreview();

        try
        {
            int sel = GroupsListBox.SelectedIndex;
            GroupsListBox.Items.Refresh();
            GroupsListBox.SelectedIndex = sel;
        }
        catch { }

        // Update shortcut if present
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ??
                         Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");
        ShortcutService.CreateOrUpdateGroupShortcut(_selectedGroup, exePath);
    }

    private void PresetIconButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedGroup ??= GroupsListBox.SelectedItem as FolderGroup;
        if (_selectedGroup == null && _groups.Count > 0)
        {
            _selectedGroup = _groups[0];
            GroupsListBox.SelectedItem = _selectedGroup;
        }
        if (_selectedGroup == null) return;

        PresetIconItem? preset = null;
        if (sender is System.Windows.Controls.Button btn)
        {
            preset = (btn.Tag as PresetIconItem) ?? (btn.DataContext as PresetIconItem);
        }

        if (preset == null) return;

        var (pngPath, icoPath) = IconExtractorService.SetPresetGroupIcon(_selectedGroup.Id, preset.PngPath, preset.IcoPath);
        if (!string.IsNullOrEmpty(pngPath) && File.Exists(pngPath))
        {
            _selectedGroup.IconPath = pngPath;
            _selectedGroup.HasCustomIcon = true;
            _storage.SaveGroups(_groups.ToList());

            UpdateFolderIconPreview();

            try
            {
                int sel = GroupsListBox.SelectedIndex;
                GroupsListBox.Items.Refresh();
                GroupsListBox.SelectedIndex = sel;
            }
            catch { }

            // Update shortcut if present
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ??
                             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");
            ShortcutService.CreateOrUpdateGroupShortcut(_selectedGroup, exePath);
        }
    }

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        var newGroup = new FolderGroup($"Folder {_groups.Count + 1}", "#3B82F6", 4);
        var (png, _) = IconExtractorService.GenerateGroupFolderIcon(newGroup.Id, newGroup.Name, newGroup.AccentColor);
        newGroup.IconPath = png;

        _groups.Add(newGroup);
        _storage.SaveGroups(_groups.ToList());

        GroupsListBox.SelectedItem = newGroup;
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the folder '{_selectedGroup.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            int prevIndex = GroupsListBox.SelectedIndex;
            _groups.Remove(_selectedGroup);
            _storage.SaveGroups(_groups.ToList());

            if (_groups.Count > 0)
            {
                GroupsListBox.SelectedIndex = Math.Clamp(prevIndex, 0, _groups.Count - 1);
            }
            else
            {
                DetailsContainer.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup == null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Select Application or Shortcut",
            Filter = "Applications & Shortcuts (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                AddFileToSelectedGroup(file);
            }
            _storage.SaveGroups(_groups.ToList());
            UpdateItemsCount();
        }
    }

    private void QuickAddCommon_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup == null) return;

        var cm = new ContextMenu();
        string sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var presets = new List<(string Name, string Path)>
        {
            ("Windows Terminal", Path.Combine(localApp, "Microsoft", "WindowsApps", "wt.exe")),
            ("Command Prompt", Path.Combine(sysRoot, "cmd.exe")),
            ("PowerShell", Path.Combine(sysRoot, "WindowsPowerShell", "v1.0", "powershell.exe")),
            ("File Explorer", Path.Combine(winDir, "explorer.exe")),
            ("Task Manager", Path.Combine(sysRoot, "Taskmgr.exe")),
            ("Notepad", Path.Combine(winDir, "notepad.exe")),
            ("Calculator", Path.Combine(sysRoot, "calc.exe")),
            ("Visual Studio Code", Path.Combine(localApp, "Programs", "Microsoft VS Code", "Code.exe")),
            ("Google Chrome", Path.Combine(progFiles, "Google", "Chrome", "Application", "chrome.exe")),
            ("Microsoft Edge", Path.Combine(progFiles, "Microsoft", "Edge", "Application", "msedge.exe")),
        };

        foreach (var (name, path) in presets)
        {
            var mi = new MenuItem { Header = name };
            string target = path;
            mi.Click += (s, ev) =>
            {
                AddFileToSelectedGroup(target, name);
                _storage.SaveGroups(_groups.ToList());
                UpdateItemsCount();
            };
            cm.Items.Add(mi);
        }

        cm.IsOpen = true;
    }

    public static string SanitizeAppName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "App";

        string name = rawName.Trim();

        // Strip trailing extensions (.lnk, .url, .exe, .bat, etc.)
        while (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            name = Path.GetFileNameWithoutExtension(name).Trim();
        }

        // Regex pattern to strip shortcut suffixes in various languages:
        // " - Shortcut", " - میانبر", " - شورتکات", " - shortcut", " - Verknüpfung", " - Raccourci", " - Acceso directo", etc.
        // Handles hyphens, en-dashes, em-dashes, and trailing duplicate numbers like " - Shortcut (2)" or " - میانبر (1)"
        string pattern = @"\s*[-–—]\s*(Shortcut|میانبر|شورتکات|shortcut|Verknüpfung|Raccourci|Acceso directo|Atalho|Collegamento|Ярлык)(\s*\(\d+\))?$";
        name = System.Text.RegularExpressions.Regex.Replace(name, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        // Also handle cases without dash if formatted like "Telegram میانبر" or "App Shortcut"
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+(میانبر|شورتکات|Shortcut)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        return string.IsNullOrWhiteSpace(name) ? rawName : name;
    }

    private void AddFileToSelectedGroup(string filePath, string? customName = null)
    {
        if (_selectedGroup == null) return;

        string rawName = customName ?? Path.GetFileNameWithoutExtension(filePath);
        string name = SanitizeAppName(rawName);
        string id = Guid.NewGuid().ToString("N");
        string iconPath = IconExtractorService.ExtractAndCacheIcon(filePath, id);

        var item = new AppItem(name, filePath, iconPath);
        _selectedGroup.Items.Add(item);
    }

    private void ItemsList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ItemsList_Drop(object sender, DragEventArgs e)
    {
        if (_selectedGroup == null) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var f in files)
            {
                AddFileToSelectedGroup(f);
            }
            _storage.SaveGroups(_groups.ToList());
            UpdateItemsCount();
        }
    }

    private void MoveUpItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is AppItem item && _selectedGroup != null)
        {
            int idx = _selectedGroup.Items.IndexOf(item);
            if (idx > 0)
            {
                _selectedGroup.Items.Move(idx, idx - 1);
                _storage.SaveGroups(_groups.ToList());
            }
        }
    }

    private void MoveDownItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is AppItem item && _selectedGroup != null)
        {
            int idx = _selectedGroup.Items.IndexOf(item);
            if (idx >= 0 && idx < _selectedGroup.Items.Count - 1)
            {
                _selectedGroup.Items.Move(idx, idx + 1);
                _storage.SaveGroups(_groups.ToList());
            }
        }
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is AppItem item && _selectedGroup != null)
        {
            _selectedGroup.Items.Remove(item);
            _storage.SaveGroups(_groups.ToList());
            UpdateItemsCount();
        }
    }

    private void TestFlyout_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup == null) return;

        // Preview flyout without closing main app
        var flyout = new FlyoutWindow(_selectedGroup, isStandaloneLaunch: false);
        flyout.Show();
    }

    private void PinToTaskbar_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup == null) return;

        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");
        }

        string shortcutPath = ShortcutService.CreateOrUpdateGroupShortcut(_selectedGroup, exePath);

        if (!string.IsNullOrEmpty(shortcutPath) && File.Exists(shortcutPath))
        {
            ShortcutService.RevealInExplorer(shortcutPath);

            MessageBox.Show(
                $"✅ شورت‌کات رسمی پوشه «{_selectedGroup.Name}» با آیکون اختصاصی ۱۰X ساخته شد!\n\n" +
                $"پنجره شورت‌کات‌ها باز شده و فایل «{Path.GetFileName(shortcutPath)}» در حالت انتخاب است.\n\n" +
                $"کافیست روی فایل انتخاب‌شده راست‌کلیک کرده و گزینه زیر را بزنید:\n" +
                $"👉 «Pin to taskbar» (پین کردن به نوار وظیفه)\n\n" +
                $"پس از آن، آیکون پوشه مستقیماً روی تسک‌بار شما قرار می‌گیرد و با کلیک روی آن، درست مثل «Test Flyout» پنجره برنامه‌ها باز می‌شود.",
                "10XBar - آماده پین به تسک‌بار",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            MessageBox.Show("Failed to create shortcut file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenShortcutsFolder_Click(object sender, RoutedEventArgs e)
    {
        ShortcutService.RevealInExplorer(StorageService.ShortcutsDirectory);
    }

    private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        StartupService.SetStartup(true);
    }

    private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        StartupService.SetStartup(false);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Minimize to system tray instead of terminating
        e.Cancel = true;
        Hide();
        App.Tray.ShowNotification("10xbar Active", "10xbar is running in the background. Right-click the system tray icon to open or exit.");
    }
}