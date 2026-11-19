namespace ZHIMulToolkit;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _checkForUpdates = new()
    {
        Text = "Check for updates at startup",
        AutoSize = true,
        Anchor = AnchorStyles.Top | AnchorStyles.Left,
        Margin = new Padding(4, 14, 4, 4)
    };

    public SettingsForm(bool checkForUpdatesAtStartup)
    {
        Text = "Settings";
        if (System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) is { } applicationIcon)
        {
            Icon = applicationIcon;
        }
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 144);

        _checkForUpdates.Checked = checkForUpdatesAtStartup;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(6, 2, 0, 2) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(6, 2, 0, 2) };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        layout.Controls.Add(_checkForUpdates, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    public bool CheckForUpdatesAtStartup => _checkForUpdates.Checked;
}
