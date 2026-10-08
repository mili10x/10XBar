using System.Diagnostics;
using System.IO;
using TenXBar.Models;

namespace TenXBar.Services;

public static class ShortcutService
{
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

            // Ensure ICO exists for group
            string icoPath = Path.Combine(IconExtractorService.CacheDirectory, $"group_{group.Id}.ico");
            if (!File.Exists(icoPath))
            {
                var (_, generatedIco) = IconExtractorService.GenerateGroupFolderIcon(group.Id, group.Name, group.AccentColor);
                icoPath = generatedIco;
            }

            // Create shortcut via WScript.Shell
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
                return shortcutPath;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
        }

        return string.Empty;
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
