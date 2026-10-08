using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TenXBar.Models;
using TenXBar.Services;

namespace TenXBar.Views;

public partial class FlyoutWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private readonly FolderGroup _group;
    private readonly bool _isStandaloneLaunch;

    public FlyoutWindow(FolderGroup group, bool isStandaloneLaunch = true)
    {
        InitializeComponent();
        _group = group;
        _isStandaloneLaunch = isStandaloneLaunch;

        DataContext = _group;
        LoadGroupData();
    }

    private void LoadGroupData()
    {
        GroupTitleText.Text = _group.Name;
        ItemCountText.Text = $" ({_group.Items.Count})";

        if (!string.IsNullOrEmpty(_group.IconPath) && File.Exists(_group.IconPath))
        {
            try
            {
                GroupIconImage.Source = new BitmapImage(new Uri(_group.IconPath));
            }
            catch { }
        }

        AppsItemsControl.ItemsSource = _group.Items;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Configure columns on UniformGrid if present in template
        var grid = FindVisualChild<UniformGrid>(AppsItemsControl);
        if (grid != null)
        {
            grid.Columns = Math.Max(1, Math.Min(8, _group.Columns));
        }

        // Measure and position the window accurately
        UpdateLayout();
        var (left, top) = TaskbarPositionService.CalculateFlyoutPosition(this, ActualWidth, ActualHeight);
        Left = left;
        Top = top;

        Activate();
        Focus();
    }

    private void AppItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is AppItem item)
        {
            ProcessLauncher.Launch(item);
            DismissAndClose();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DismissAndClose();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        DismissAndClose();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissAndClose();
        }
    }

    private void DismissAndClose()
    {
        try
        {
            Close();
            if (_isStandaloneLaunch)
            {
                Application.Current.Shutdown();
            }
        }
        catch { }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
}
