using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TenXBar.Services;

public static class StartupService
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "10xbar";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartup(bool enable, string? customExePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                string exePath = customExePath ?? 
                                 Process.GetCurrentProcess().MainModule?.FileName ?? 
                                 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");

                string command = $"\"{exePath}\" --silent";
                key.SetValue(AppName, command);
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set startup registry key: {ex.Message}");
        }
    }
}
