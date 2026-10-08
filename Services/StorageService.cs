using System.IO;
using System.Text.Json;
using TenXBar.Models;

namespace TenXBar.Services;

public class StorageService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "10xbar"
    );

    private static readonly string DataFile = Path.Combine(AppDataFolder, "groups.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ShortcutsDirectory => Path.Combine(AppDataFolder, "shortcuts");

    public StorageService()
    {
        if (!Directory.Exists(AppDataFolder))
            Directory.CreateDirectory(AppDataFolder);

        if (!Directory.Exists(ShortcutsDirectory))
            Directory.CreateDirectory(ShortcutsDirectory);
    }

    public List<FolderGroup> LoadGroups()
    {
        try
        {
            if (File.Exists(DataFile))
            {
                string json = File.ReadAllText(DataFile);
                var groups = JsonSerializer.Deserialize<List<FolderGroup>>(json, JsonOptions);
                if (groups != null && groups.Count > 0)
                {
                    // Ensure each item has a cached icon
                    foreach (var g in groups)
                    {
                        EnsureGroupIcons(g);
                    }
                    return groups;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading groups: {ex.Message}");
        }

        // Return seeded default groups on first run
        var defaults = CreateDefaultGroups();
        SaveGroups(defaults);
        return defaults;
    }

    public void SaveGroups(List<FolderGroup> groups)
    {
        try
        {
            string json = JsonSerializer.Serialize(groups, JsonOptions);
            File.WriteAllText(DataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving groups: {ex.Message}");
        }
    }

    private void EnsureGroupIcons(FolderGroup group)
    {
        if (string.IsNullOrEmpty(group.IconPath) || !File.Exists(group.IconPath))
        {
            var (png, _) = IconExtractorService.GenerateGroupFolderIcon(group.Id, group.Name, group.AccentColor);
            group.IconPath = png;
        }

        foreach (var item in group.Items)
        {
            if (string.IsNullOrEmpty(item.IconPath) || !File.Exists(item.IconPath))
            {
                item.IconPath = IconExtractorService.ExtractAndCacheIcon(item.TargetPath, item.Id);
            }
        }
    }

    private List<FolderGroup> CreateDefaultGroups()
    {
        var list = new List<FolderGroup>();

        // 1. Dev Tools Group
        var dev = new FolderGroup("Dev Tools", "#3B82F6", 4);
        var sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string cmdPath = Path.Combine(sysRoot, "cmd.exe");
        string psPath = Path.Combine(sysRoot, "WindowsPowerShell", "v1.0", "powershell.exe");
        string notepadPath = Path.Combine(winDir, "notepad.exe");

        // Try detecting VS Code
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string vsCodePath = Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe");

        if (File.Exists(cmdPath))
        {
            var item = new AppItem("Command Prompt", cmdPath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(cmdPath, item.Id);
            dev.Items.Add(item);
        }
        if (File.Exists(psPath))
        {
            var item = new AppItem("PowerShell", psPath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(psPath, item.Id);
            dev.Items.Add(item);
        }
        if (File.Exists(vsCodePath))
        {
            var item = new AppItem("VS Code", vsCodePath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(vsCodePath, item.Id);
            dev.Items.Add(item);
        }
        if (File.Exists(notepadPath))
        {
            var item = new AppItem("Notepad", notepadPath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(notepadPath, item.Id);
            dev.Items.Add(item);
        }

        EnsureGroupIcons(dev);
        list.Add(dev);

        // 2. Productivity / System Group
        var sys = new FolderGroup("Productivity", "#10B981", 4);
        string explorerPath = Path.Combine(winDir, "explorer.exe");
        string calcPath = Path.Combine(sysRoot, "calc.exe");
        string taskmgrPath = Path.Combine(sysRoot, "Taskmgr.exe");

        if (File.Exists(explorerPath))
        {
            var item = new AppItem("File Explorer", explorerPath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(explorerPath, item.Id);
            sys.Items.Add(item);
        }
        if (File.Exists(taskmgrPath))
        {
            var item = new AppItem("Task Manager", taskmgrPath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(taskmgrPath, item.Id);
            sys.Items.Add(item);
        }
        if (File.Exists(calcPath))
        {
            var item = new AppItem("Calculator", calcPath);
            item.IconPath = IconExtractorService.ExtractAndCacheIcon(calcPath, item.Id);
            sys.Items.Add(item);
        }

        EnsureGroupIcons(sys);
        list.Add(sys);

        return list;
    }
}
