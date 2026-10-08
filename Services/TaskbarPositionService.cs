using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace TenXBar.Services;

public static class TaskbarPositionService
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint ABM_GETTASKBARPOS = 0x00000005;
    private const uint ABE_LEFT = 0;
    private const uint ABE_TOP = 1;
    private const uint ABE_RIGHT = 2;
    private const uint ABE_BOTTOM = 3;

    public static (double Left, double Top) CalculateFlyoutPosition(Window window, double targetWidthDip, double targetHeightDip)
    {
        // Get cursor position in physical screen coordinates
        GetCursorPos(out POINT cursor);
        var currentScreen = Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));

        // Get DPI scale of target window
        var dpi = VisualTreeHelper.GetDpi(window);
        double dpiX = dpi.DpiScaleX;
        double dpiY = dpi.DpiScaleY;

        double targetWidthPx = targetWidthDip * dpiX;
        double targetHeightPx = targetHeightDip * dpiY;

        var workArea = currentScreen.WorkingArea;
        var bounds = currentScreen.Bounds;

        int marginPx = (int)(12 * dpiX);

        double flyoutX;
        double flyoutY;

        // Detect taskbar position based on WorkArea vs Bounds
        if (workArea.Bottom < bounds.Bottom)
        {
            // Bottom Taskbar (standard)
            flyoutX = cursor.X - (targetWidthPx / 2.0);
            flyoutY = workArea.Bottom - targetHeightPx - marginPx;
        }
        else if (workArea.Top > bounds.Top)
        {
            // Top Taskbar
            flyoutX = cursor.X - (targetWidthPx / 2.0);
            flyoutY = workArea.Top + marginPx;
        }
        else if (workArea.Left > bounds.Left)
        {
            // Left Taskbar
            flyoutX = workArea.Left + marginPx;
            flyoutY = cursor.Y - (targetHeightPx / 2.0);
        }
        else if (workArea.Right < bounds.Right)
        {
            // Right Taskbar
            flyoutX = workArea.Right - targetWidthPx - marginPx;
            flyoutY = cursor.Y - (targetHeightPx / 2.0);
        }
        else
        {
            // Fallback (auto-hidden or matching)
            flyoutX = cursor.X - (targetWidthPx / 2.0);
            flyoutY = workArea.Bottom - targetHeightPx - marginPx;
        }

        // Clamp to screen boundaries
        if (flyoutX < workArea.Left + marginPx)
            flyoutX = workArea.Left + marginPx;
        if (flyoutX + targetWidthPx > workArea.Right - marginPx)
            flyoutX = workArea.Right - targetWidthPx - marginPx;

        if (flyoutY < workArea.Top + marginPx)
            flyoutY = workArea.Top + marginPx;
        if (flyoutY + targetHeightPx > workArea.Bottom - marginPx)
            flyoutY = workArea.Bottom - targetHeightPx - marginPx;

        // Convert physical coordinates back to DIPs for WPF Window
        return (flyoutX / dpiX, flyoutY / dpiY);
    }
}
