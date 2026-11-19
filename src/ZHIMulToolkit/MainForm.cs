using System.Diagnostics;
using System.Reflection;
using ZHIMulToolkit.Core.Maps;
using ZHIMulToolkit.Core.Settings;
using ZHIMulToolkit.Core.Updates;

namespace ZHIMulToolkit;

internal sealed class MainForm : Form
{
    private readonly SettingsStore _settingsStore = new();
    private readonly GitHubReleaseClient _releaseClient = new();
    private readonly UpdateChecker _updateChecker;
    private readonly TextBox _originalDirectory = CreatePathBox();
    private readonly TextBox _modifiedDirectory = CreatePathBox();
    private readonly TextBox _diffDirectory = CreatePathBox();
    private readonly TextBox _outputDirectory = CreatePathBox();
    private readonly ComboBox _mapSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox _terrainCheckBox = new() { Text = "Terrain", Checked = true, AutoSize = true };
    private readonly CheckBox _staticsCheckBox = new() { Text = "Statics", Checked = true, AutoSize = true };
    private readonly RichTextBox _log = new()
    {
        ReadOnly = true,
        DetectUrls = false,
        Dock = DockStyle.Fill,
        BackColor = SystemColors.Window,
        WordWrap = false
    };
    private readonly ToolStripStatusLabel _statusLabel = new() { Text = "Ready" };
    private readonly ProgressBar _progressBar = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
    private readonly string? _githubRepository;
    private ApplicationSettings _settings = new();
    private bool _initializing = true;

    public MainForm()
    {
        _updateChecker = new UpdateChecker(_releaseClient);
        _githubRepository = ReadGitHubRepository();

        Text = "ZHI MUL Toolkit";
        if (System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) is { } applicationIcon)
        {
            Icon = applicationIcon;
        }
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        Size = new Size(980, 700);

        Controls.Add(BuildLayout());
        MainMenuStrip = BuildMenu();
        Controls.Add(MainMenuStrip);
        Load += async (_, _) => await InitializeAsync();
        Shown += async (_, _) => await CheckForUpdatesAtStartupAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _releaseClient.Dispose();
        base.Dispose(disposing);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildDirectoriesPanel(), 0, 0);
        root.Controls.Add(BuildOperationsPanel(), 0, 1);

        var logGroup = new GroupBox { Text = "Processing log", Dock = DockStyle.Fill, Padding = new Padding(8) };
        logGroup.Controls.Add(_log);
        root.Controls.Add(logGroup, 0, 2);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        var statusStrip = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false };
        statusStrip.Items.Add(_statusLabel);
        bottom.Controls.Add(statusStrip, 0, 0);
        bottom.Controls.Add(_progressBar, 1, 0);
        root.Controls.Add(bottom, 0, 3);
        return root;
    }

    private Control BuildDirectoriesPanel()
    {
        var group = new GroupBox { Text = "Map files and output", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        AddPathRow(table, 0, "Original MUL directory", _originalDirectory, "Select the original map files.");
        AddPathRow(table, 1, "Modified MUL directory", _modifiedDirectory, "Select the edited map files.");
        AddPathRow(table, 2, "DIF directory", _diffDirectory, "Select an existing DIF patch directory.");
        AddPathRow(table, 3, "Output directory", _outputDirectory, "Select where future operation results will be written.");

        var mapRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, AutoSize = true };
        mapRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        mapRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        mapRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mapRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mapRow.Controls.Add(new Label { Text = "Discovered map", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _mapSelector.Anchor = AnchorStyles.Left;
        _mapSelector.SelectedIndexChanged += async (_, _) =>
        {
            if (_initializing || _mapSelector.SelectedItem is not MapOption option) return;
            _settings.SelectedMapId = option.MapId;
            await SaveSettingsAsync();
        };
        mapRow.Controls.Add(_mapSelector, 1, 0);
        mapRow.Controls.Add(_terrainCheckBox, 2, 0);
        mapRow.Controls.Add(_staticsCheckBox, 3, 0);
        table.Controls.Add(mapRow, 0, 4);
        table.SetColumnSpan(mapRow, 3);
        group.Controls.Add(table);
        return group;
    }

    private Control BuildOperationsPanel()
    {
        var group = new GroupBox { Text = "Operations", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        foreach (var operation in new[] { "Generate DIF", "Apply DIF", "Verify DIF", "Inspect DIF", "Rebase DIF" })
        {
            var button = new Button { Text = operation, Enabled = false, AutoSize = true, Height = 34, Margin = new Padding(4) };
            new ToolTip().SetToolTip(button, "This operation will be enabled after its format and safety checks are implemented.");
            buttons.Controls.Add(button);
        }

        var note = new Label
        {
            Text = "The current build supports read-only map discovery and terrain block inspection.",
            AutoSize = true,
            Padding = new Padding(4, 10, 4, 2)
        };
        group.Controls.Add(note);
        group.Controls.Add(buttons);
        return group;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var settingsMenu = new ToolStripMenuItem("Settings");
        settingsMenu.Click += async (_, _) => await ShowSettingsAsync();
        var helpMenu = new ToolStripMenuItem("Help");
        var checkUpdates = new ToolStripMenuItem("Check for Updates");
        checkUpdates.Click += async (_, _) => await CheckForUpdatesAsync(showCurrentVersion: true);
        var about = new ToolStripMenuItem("About");
        about.Click += (_, _) => ShowAbout();
        helpMenu.DropDownItems.Add(checkUpdates);
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add(about);
        menu.Items.Add(settingsMenu);
        menu.Items.Add(helpMenu);
        return menu;
    }

    private void AddPathRow(TableLayoutPanel table, int row, string label, TextBox textBox, string description)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textBox.Margin = new Padding(3, 6, 3, 6);
        textBox.AccessibleDescription = description;
        table.Controls.Add(textBox, 1, row);
        var button = new Button { Text = "Browse...", Dock = DockStyle.Fill, Tag = textBox, AccessibleDescription = description };
        button.Click += async (_, _) => await BrowseDirectoryAsync((TextBox)button.Tag!);
        table.Controls.Add(button, 2, row);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _settings = await _settingsStore.LoadAsync();
            _originalDirectory.Text = _settings.OriginalDirectory;
            _modifiedDirectory.Text = _settings.ModifiedDirectory;
            _diffDirectory.Text = _settings.DiffDirectory;
            _outputDirectory.Text = _settings.OutputDirectory;
            RefreshMapList();
            AppendLog("Application ready. Select a directory to discover map files.");
        }
        catch (Exception exception)
        {
            AppendLog($"Settings could not be loaded: {exception.Message}");
        }
        finally
        {
            _initializing = false;
        }
    }

    private async Task BrowseDirectoryAsync(TextBox target)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = target.AccessibleDescription ?? string.Empty,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = target == _outputDirectory,
            SelectedPath = Directory.Exists(target.Text) ? target.Text : string.Empty
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        target.Text = dialog.SelectedPath;
        switch (target)
        {
            case var _ when target == _originalDirectory:
                _settings.OriginalDirectory = target.Text;
                break;
            case var _ when target == _modifiedDirectory:
                _settings.ModifiedDirectory = target.Text;
                break;
            case var _ when target == _diffDirectory:
                _settings.DiffDirectory = target.Text;
                break;
            case var _ when target == _outputDirectory:
                _settings.OutputDirectory = target.Text;
                break;
        }

        await SaveSettingsAsync();
        if (target != _outputDirectory) RefreshMapList(target.Text);
    }

    private void RefreshMapList(string? preferredDirectory = null)
    {
        var directories = new[]
        {
            preferredDirectory,
            _originalDirectory.Text,
            _modifiedDirectory.Text,
            _diffDirectory.Text
        };

        IReadOnlyList<DiscoveredMap> discovered = [];
        foreach (var directory in directories.OfType<string>().Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory)) continue;
            try
            {
                discovered = MapDiscovery.Discover(directory);
            }
            catch (IOException exception)
            {
                AppendLog($"Map discovery failed: {exception.Message}");
            }

            if (discovered.Count > 0) break;
        }

        var previouslySelected = _mapSelector.SelectedItem is MapOption selected ? selected.MapId : _settings.SelectedMapId;
        _mapSelector.BeginUpdate();
        _mapSelector.Items.Clear();
        foreach (var map in discovered)
        {
            _mapSelector.Items.Add(new MapOption(map.MapId, map.Files.Count));
        }
        _mapSelector.EndUpdate();

        var matchingIndex = previouslySelected is null
            ? -1
            : _mapSelector.Items.Cast<MapOption>().ToList().FindIndex(option => option.MapId == previouslySelected);
        if (matchingIndex >= 0) _mapSelector.SelectedIndex = matchingIndex;
        else if (_mapSelector.Items.Count > 0) _mapSelector.SelectedIndex = 0;

        if (discovered.Count == 0)
        {
            _statusLabel.Text = "No map files discovered";
            AppendLog("No supported MUL or DIF filenames were found in the selected directories.");
        }
        else
        {
            _statusLabel.Text = $"Discovered {discovered.Count} map identifier(s)";
            AppendLog($"Discovered map identifiers: {string.Join(", ", discovered.Select(map => map.MapId))}.");
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings);
        }
        catch (Exception exception)
        {
            AppendLog($"Settings could not be saved: {exception.Message}");
        }
    }

    private async Task ShowSettingsAsync()
    {
        using var dialog = new SettingsForm(_settings.CheckForUpdatesAtStartup);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.CheckForUpdatesAtStartup = dialog.CheckForUpdatesAtStartup;
        await SaveSettingsAsync();
    }

    private async Task CheckForUpdatesAtStartupAsync()
    {
        if (!_settings.CheckForUpdatesAtStartup) return;
        await CheckForUpdatesAsync(showCurrentVersion: false);
    }

    private async Task CheckForUpdatesAsync(bool showCurrentVersion)
    {
        if (!GitHubReleaseClient.IsRepositoryName(_githubRepository))
        {
            if (showCurrentVersion)
            {
                MessageBox.Show(this,
                    "Update checks are not configured for this build. Set GitHubRepository=OWNER/REPOSITORY when building the application.",
                    "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }

        SetBusy(true, "Checking for updates...");
        try
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? Application.ProductVersion;
            if (version.Contains('+', StringComparison.Ordinal)) version = version[..version.IndexOf('+')];
            var update = await _updateChecker.CheckAsync(_githubRepository!, version);
            if (update is not null)
            {
                var answer = MessageBox.Show(this,
                    $"Version {update.Version} is available. Open the release page?\n\nThe initial build checks for updates but does not install them.",
                    "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(update.ReleaseUri.AbsoluteUri) { UseShellExecute = true });
                }
            }
            else if (showCurrentVersion)
            {
                MessageBox.Show(this, "You are using the latest stable version.", "Check for Updates",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidDataException or FormatException)
        {
            AppendLog($"Update check failed; the application will continue normally: {exception.Message}");
            if (showCurrentVersion)
            {
                MessageBox.Show(this, $"The update check failed.\n\n{exception.Message}", "Check for Updates",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            SetBusy(false, "Ready");
        }
    }

    private void ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? Application.ProductVersion;
        MessageBox.Show(this, $"ZHI MUL Toolkit\nVersion {version}\n\nCopyright (c) 2026 DemonRokZHI\nLicensed under the MIT License.",
            "About ZHI MUL Toolkit", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AppendLog(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void SetBusy(bool busy, string status)
    {
        _statusLabel.Text = status;
        _progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        _progressBar.Value = 0;
    }

    private static TextBox CreatePathBox() => new() { ReadOnly = true, Dock = DockStyle.Fill };

    private static string? ReadGitHubRepository() => Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "GitHubRepository")?.Value;

    private sealed record MapOption(int MapId, int FileCount)
    {
        public override string ToString() => $"Map {MapId} ({FileCount} files)";
    }
}
