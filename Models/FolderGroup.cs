using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TenXBar.Models;

public class FolderGroup : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New Group";
    private string _iconPath = string.Empty;
    private string _accentColor = "#3B82F6";
    private int _columns = 4;
    private bool _hasCustomIcon = false;
    private ObservableCollection<AppItem> _items = new();
    private DateTime _createdAt = DateTime.UtcNow;

    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath != value)
            {
                _iconPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string AccentColor
    {
        get => _accentColor;
        set { if (_accentColor != value) { _accentColor = value; OnPropertyChanged(); } }
    }

    public int Columns
    {
        get => _columns;
        set { if (_columns != value) { _columns = value; OnPropertyChanged(); } }
    }

    public bool HasCustomIcon
    {
        get => _hasCustomIcon;
        set { if (_hasCustomIcon != value) { _hasCustomIcon = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<AppItem> Items
    {
        get => _items;
        set
        {
            if (_items != null)
                _items.CollectionChanged -= Items_CollectionChanged;

            _items = value;

            if (_items != null)
                _items.CollectionChanged += Items_CollectionChanged;

            OnPropertyChanged();
            OnPropertyChanged(nameof(ItemCountText));
        }
    }

    public string ItemCountText => $"{Items.Count} apps";

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FolderGroup()
    {
        _items.CollectionChanged += Items_CollectionChanged;
    }

    public FolderGroup(string name, string accentColor = "#3B82F6", int columns = 4) : this()
    {
        Name = name;
        AccentColor = accentColor;
        Columns = columns;
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ItemCountText));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
