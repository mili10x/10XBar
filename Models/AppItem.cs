namespace TenXBar.Models;

public class AppItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;

    public AppItem() { }

    public AppItem(string name, string targetPath, string iconPath = "", string arguments = "", string workingDir = "")
    {
        Name = name;
        TargetPath = targetPath;
        IconPath = iconPath;
        Arguments = arguments;
        WorkingDirectory = workingDir;
    }
}
