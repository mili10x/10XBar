using System.Diagnostics;
using System.IO;
using TenXBar.Models;

namespace TenXBar.Services;

public static class ProcessLauncher
{
    public static bool Launch(AppItem item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.TargetPath))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = item.TargetPath,
                Arguments = item.Arguments ?? string.Empty,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && Directory.Exists(item.WorkingDirectory))
            {
                startInfo.WorkingDirectory = item.WorkingDirectory;
            }
            else
            {
                string? dir = Path.GetDirectoryName(item.TargetPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    startInfo.WorkingDirectory = dir;
                }
            }

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not launch '{item.Name}':\n{ex.Message}",
                "10xbar Launch Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
            return false;
        }
    }
}
