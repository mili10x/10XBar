using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TenXBar.Services;

public static class IconExtractorService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_LINKOVERLAY = 0x000008000;

    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "10xbar",
        "icons"
    );

    static IconExtractorService()
    {
        if (!Directory.Exists(CacheDirectory))
        {
            Directory.CreateDirectory(CacheDirectory);
        }
    }

    /// <summary>
    /// Resolves target path if file is a .lnk shortcut
    /// </summary>
    public static string ResolveShortcutTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return path;

        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(path);
                    string target = (string)shortcut.TargetPath;
                    if (!string.IsNullOrWhiteSpace(target) && (File.Exists(target) || Directory.Exists(target)))
                    {
                        return target;
                    }
                }
            }
            catch
            {
                // Fallback to original path
            }
        }

        return path;
    }

    /// <summary>
    /// Extracts icon from an exe/lnk/file and saves it as a cached PNG file.
    /// Returns the cached PNG file path.
    /// </summary>
    public static string ExtractAndCacheIcon(string filePath, string fileId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return string.Empty;

            string targetFile = ResolveShortcutTarget(filePath);
            if (!File.Exists(targetFile) && !Directory.Exists(targetFile) && !File.Exists(filePath))
                return string.Empty;

            string usePath = File.Exists(targetFile) ? targetFile : filePath;
            string destPng = Path.Combine(CacheDirectory, $"{fileId}.png");

            // If already cached and valid, reuse
            if (File.Exists(destPng) && new FileInfo(destPng).Length > 0)
                return destPng;

            // If it's already an image
            string ext = Path.GetExtension(usePath).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg")
            {
                File.Copy(usePath, destPng, true);
                return destPng;
            }

            // Extract using SHGetFileInfo
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr hImg = SHGetFileInfo(usePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

            if (shinfo.hIcon != IntPtr.Zero)
            {
                using Icon icon = Icon.FromHandle(shinfo.hIcon);
                using Bitmap bitmap = icon.ToBitmap();
                bitmap.Save(destPng, ImageFormat.Png);
                DestroyIcon(shinfo.hIcon);
                return destPng;
            }

            // Fallback: System.Drawing.Icon.ExtractAssociatedIcon
            if (File.Exists(usePath))
            {
                using Icon? assoc = Icon.ExtractAssociatedIcon(usePath);
                if (assoc != null)
                {
                    using Bitmap bmp = assoc.ToBitmap();
                    bmp.Save(destPng, ImageFormat.Png);
                    return destPng;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting icon: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// Generates a modern folder icon with custom accent color and saves as .ico and .png
    /// </summary>
    public static (string PngPath, string IcoPath) GenerateGroupFolderIcon(string groupId, string name, string hexColor)
    {
        string pngPath = Path.Combine(CacheDirectory, $"group_{groupId}.png");
        string icoPath = Path.Combine(CacheDirectory, $"group_{groupId}.ico");

        try
        {
            System.Drawing.Color accent = ColorTranslator.FromHtml(hexColor);

            int size = 128;
            using Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                // Draw modern folder silhouette
                // Folder Tab
                using (var tabBrush = new SolidBrush(System.Drawing.Color.FromArgb(200, accent.R, accent.G, accent.B)))
                {
                    g.FillRoundedRectangle(tabBrush, 16, 20, 48, 24, 8);
                }

                // Folder Back
                using (var backBrush = new SolidBrush(System.Drawing.Color.FromArgb(220, Math.Max(0, accent.R - 20), Math.Max(0, accent.G - 20), Math.Max(0, accent.B - 20))))
                {
                    g.FillRoundedRectangle(backBrush, 16, 32, 96, 76, 12);
                }

                // Folder Front Pocket
                using (var frontBrush = new SolidBrush(accent))
                {
                    g.FillRoundedRectangle(frontBrush, 16, 46, 96, 62, 12);
                }

                // Draw initial letter in the center
                string initial = !string.IsNullOrWhiteSpace(name) ? name.Substring(0, 1).ToUpperInvariant() : "F";
                using (var font = new Font("Segoe UI", 26, FontStyle.Bold))
                using (var textBrush = new SolidBrush(System.Drawing.Color.White))
                {
                    var textSize = g.MeasureString(initial, font);
                    g.DrawString(initial, font, textBrush, (size - textSize.Width) / 2, 54);
                }
            }

            bmp.Save(pngPath, ImageFormat.Png);

            // Save as ICO
            IntPtr hIcon = bmp.GetHicon();
            using (Icon icon = Icon.FromHandle(hIcon))
            {
                using (FileStream fs = new FileStream(icoPath, FileMode.Create))
                {
                    icon.Save(fs);
                }
            }
            DestroyIcon(hIcon);

            return (pngPath, icoPath);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Applies a custom icon selected by the user from the system (.ico, .png, .jpg, .exe, .dll)
    /// </summary>
    public static (string PngPath, string IcoPath) SetCustomGroupIcon(string groupId, string sourcePath)
    {
        string pngPath = Path.Combine(CacheDirectory, $"group_{groupId}.png");
        string icoPath = Path.Combine(CacheDirectory, $"group_{groupId}.ico");

        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return (string.Empty, string.Empty);

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();

            if (ext == ".ico")
            {
                File.Copy(sourcePath, icoPath, true);
                using (var icon = new Icon(sourcePath))
                using (var bmp = icon.ToBitmap())
                {
                    bmp.Save(pngPath, ImageFormat.Png);
                }
                return (pngPath, icoPath);
            }
            else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                using (var original = new Bitmap(sourcePath))
                {
                    int targetSize = 128;
                    using (var resized = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb))
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.Clear(System.Drawing.Color.Transparent);
                        g.DrawImage(original, 0, 0, targetSize, targetSize);
                        resized.Save(pngPath, ImageFormat.Png);

                        IntPtr hIcon = resized.GetHicon();
                        using (var icon = Icon.FromHandle(hIcon))
                        using (var fs = new FileStream(icoPath, FileMode.Create))
                        {
                            icon.Save(fs);
                        }
                        DestroyIcon(hIcon);
                    }
                }
                return (pngPath, icoPath);
            }
            else
            {
                using (Icon? assoc = Icon.ExtractAssociatedIcon(sourcePath))
                {
                    if (assoc != null)
                    {
                        using (var bmp = assoc.ToBitmap())
                        {
                            bmp.Save(pngPath, ImageFormat.Png);
                        }
                        using (var fs = new FileStream(icoPath, FileMode.Create))
                        {
                            assoc.Save(fs);
                        }
                        return (pngPath, icoPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set custom group icon: {ex.Message}");
        }

        return (string.Empty, string.Empty);
    }

    // Helper extension for Graphics rounded rectangle
    private static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
