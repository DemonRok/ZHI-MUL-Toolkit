namespace ZHIMulToolkit.Core.Settings;

public sealed class ApplicationSettings
{
    public string OriginalDirectory { get; set; } = string.Empty;

    public string ModifiedDirectory { get; set; } = string.Empty;

    public string DiffDirectory { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public int? SelectedMapId { get; set; }

    public bool CheckForUpdatesAtStartup { get; set; } = true;
}
