using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using TenXBar.Models;

namespace TenXBar.Services;

public static class ShortcutService
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_FLUSH = 0x1000;

    public static string CreateOrUpdateGroupShortcut(FolderGroup group, string exePath)
    {
        try
        {
            string shortcutsDir = StorageService.ShortcutsDirectory;
            if (!Directory.Exists(shortcutsDir))
                Directory.CreateDirectory(shortcutsDir);

            // Clean invalid file name chars
            string safeName = string.Join("_", group.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "Group_" + group.Id;

            string shortcutPath = Path.Combine(shortcutsDir, $"{safeName}.lnk");

            // Resolve best ICO path for group (prioritizing versioned ICO from IconPath)
            string icoPath = !string.IsNullOrEmpty(group.IconPath) ? Path.ChangeExtension(group.IconPath, ".ico") : string.Empty;
            if (string.IsNullOrEmpty(icoPath) || !File.Exists(icoPath))
            {
                icoPath = Path.Combine(IconExtractorService.CacheDirectory, $"group_{group.Id}.ico");
            }

            if (!File.Exists(icoPath))
            {
                var (_, generatedIco) = IconExtractorService.GenerateGroupFolderIcon(group.Id, group.Name, group.AccentColor);
                icoPath = generatedIco;
            }

            // Create or update shortcut via WScript.Shell
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.Arguments = $"launch \"{group.Id}\"";
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
                shortcut.Description = $"10xbar Folder: {group.Name}";

                if (File.Exists(icoPath))
                {
                    shortcut.IconLocation = $"{icoPath},0";
                }
                else
                {
                    shortcut.IconLocation = $"{exePath},0";
                }

                shortcut.Save();

                // Also update any desktop and pinned taskbar shortcuts matching this group
                UpdateAssociatedShortcuts(group.Id, exePath, icoPath);

                // Notify Windows Shell to refresh taskbar and desktop icons immediately
                try
                {
                    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }

                return shortcutPath;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
        }

        return string.Empty;
    }

    private static void UpdateAssociatedShortcuts(string groupId, string exePath, string icoPath)
    {
        try
        {
            var searchDirs = new List<string>
            {
                StorageService.ShortcutsDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar")
            };

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var lnk in Directory.GetFiles(dir, "*.lnk"))
                {
                    try
                    {
                        dynamic shortcut = shell.CreateShortcut(lnk);
                        string args = (string)shortcut.Arguments;
                        if (!string.IsNullOrEmpty(args) && args.Contains(groupId, StringComparison.OrdinalIgnoreCase))
                        {
                            shortcut.TargetPath = exePath;
                            if (File.Exists(icoPath))
                            {
                                shortcut.IconLocation = $"{icoPath},0";
                            }
                            shortcut.Save();
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public static void RevealInExplorer(string filePath)
    {
        if (File.Exists(filePath))
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        else if (Directory.Exists(filePath))
        {
            Process.Start("explorer.exe", $"\"{filePath}\"");
        }
        else
        {
            Process.Start("explorer.exe", $"\"{StorageService.ShortcutsDirectory}\"");
        }
    }
}
