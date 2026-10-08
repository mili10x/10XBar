using System.IO;
using TenXBar.Models;
using TenXBar.Services;
using Xunit;

namespace TenXBar.Tests;

public class TenXBarTests
{
    [Fact]
    public void StorageService_LoadsAndSavesGroupsSuccessfully()
    {
        var storage = new StorageService();
        var groups = storage.LoadGroups();

        Assert.NotNull(groups);
        Assert.NotEmpty(groups);

        // Verify structure
        var first = groups[0];
        Assert.False(string.IsNullOrWhiteSpace(first.Name));
        Assert.True(first.Columns >= 2 && first.Columns <= 8);

        // Add a test group and save
        string testGroupName = "Test Group " + Guid.NewGuid().ToString("N");
        var testGroup = new FolderGroup(testGroupName, "#8B5CF6", 3);
        groups.Add(testGroup);

        storage.SaveGroups(groups);

        // Reload and verify presence
        var reloaded = storage.LoadGroups();
        Assert.Contains(reloaded, g => g.Name == testGroupName);

        // Cleanup
        reloaded.RemoveAll(g => g.Name == testGroupName);
        storage.SaveGroups(reloaded);
    }

    [Fact]
    public void IconExtractorService_GeneratesFolderIconPngAndIco()
    {
        string id = Guid.NewGuid().ToString("N");
        string name = "DevTest";
        string hexColor = "#3B82F6";

        var (pngPath, icoPath) = IconExtractorService.GenerateGroupFolderIcon(id, name, hexColor);

        Assert.False(string.IsNullOrEmpty(pngPath));
        Assert.True(File.Exists(pngPath), $"PNG icon file should exist at {pngPath}");
        Assert.True(new FileInfo(pngPath).Length > 0, "PNG file should not be empty");

        Assert.False(string.IsNullOrEmpty(icoPath));
        Assert.True(File.Exists(icoPath), $"ICO icon file should exist at {icoPath}");
        Assert.True(new FileInfo(icoPath).Length > 0, "ICO file should not be empty");
    }

    [Fact]
    public void IconExtractorService_ExtractsSystemIcon()
    {
        string sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string cmdPath = Path.Combine(sysRoot, "cmd.exe");

        if (File.Exists(cmdPath))
        {
            string id = "cmd_test_" + Guid.NewGuid().ToString("N");
            string iconPath = IconExtractorService.ExtractAndCacheIcon(cmdPath, id);

            Assert.False(string.IsNullOrEmpty(iconPath));
            Assert.True(File.Exists(iconPath));
            Assert.True(new FileInfo(iconPath).Length > 0);
        }
    }

    [Fact]
    public void ShortcutService_GeneratesTaskbarShortcut()
    {
        var group = new FolderGroup("TestShortcutGroup", "#10B981", 4);
        string dummyExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");

        string shortcutPath = ShortcutService.CreateOrUpdateGroupShortcut(group, dummyExe);

        Assert.False(string.IsNullOrEmpty(shortcutPath));
        Assert.True(File.Exists(shortcutPath), $"Shortcut file should exist at {shortcutPath}");
        Assert.Equal(".lnk", Path.GetExtension(shortcutPath).ToLowerInvariant());
    }

    [Fact]
    public void ShortcutService_GeneratesShortcutsForAllLoadedGroups()
    {
        var storage = new StorageService();
        var groups = storage.LoadGroups();
        string dummyExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TenXBar.exe");

        foreach (var group in groups)
        {
            string path = ShortcutService.CreateOrUpdateGroupShortcut(group, dummyExe);
            Assert.True(File.Exists(path), $"Shortcut should exist for {group.Name}");
        }
    }
}