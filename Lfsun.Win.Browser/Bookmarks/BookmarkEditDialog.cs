using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Bookmarks;

public sealed class BookmarkEditDialog : Form
{
    private readonly TextBox _nameTextBox = new();
    private readonly TextBox _urlTextBox = new();
    private readonly Panel _cardPanel = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();
    private readonly bool _isFolder;

    private BookmarkEditDialog(string title, bool isFolder, string defaultName, string defaultUrl)
    {
        _isFolder = isFolder;
        Text = title;
        Width = 520;
        Height = isFolder ? 210 : 260;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);

        BuildLayout(defaultName, defaultUrl);
        ApplyTheme();
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public string BookmarkName => _nameTextBox.Text.Trim();
    public string BookmarkUrl => _urlTextBox.Text.Trim();

    public static bool ShowDialogForBookmark(
        IWin32Window owner,
        string title,
        string defaultName,
        string defaultUrl,
        out string name,
        out string url)
    {
        using var dialog = new BookmarkEditDialog(title, isFolder: false, defaultName, defaultUrl);
        var result = dialog.ShowDialog(owner) == DialogResult.OK;
        name = dialog.BookmarkName;
        url = dialog.BookmarkUrl;
        return result;
    }

    public static bool ShowDialogForFolder(
        IWin32Window owner,
        string title,
        string defaultName,
        out string name)
    {
        using var dialog = new BookmarkEditDialog(title, isFolder: true, defaultName, string.Empty);
        var result = dialog.ShowDialog(owner) == DialogResult.OK;
        name = dialog.BookmarkName;
        return result;
    }

    private void BuildLayout(string defaultName, string defaultUrl)
    {
        _cardPanel.Dock = DockStyle.Fill;
        _cardPanel.BackColor = AppTheme.SurfaceAlt;
        _cardPanel.Padding = new Padding(12);
        _cardPanel.Margin = new Padding(12, 12, 12, 4);
        _cardPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        AppTheme.ApplyControlRegion(_cardPanel, 8);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = _isFolder ? 1 : 2,
            BackColor = Color.Transparent
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Text = "名称", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _nameTextBox.Dock = DockStyle.Fill;
        _nameTextBox.Text = defaultName;
        panel.Controls.Add(_nameTextBox, 1, 0);

        if (!_isFolder)
        {
            panel.Controls.Add(new Label { Text = "地址", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _urlTextBox.Dock = DockStyle.Fill;
            _urlTextBox.Text = defaultUrl;
            panel.Controls.Add(_urlTextBox, 1, 1);
        }

        _cardPanel.Controls.Add(panel);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12)
        };

        _okButton.Text = "确定";
        _okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("名称不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_isFolder && string.IsNullOrWhiteSpace(_urlTextBox.Text))
            {
                MessageBox.Show("地址不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        _cancelButton.Text = "取消";
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        footer.Controls.Add(_okButton);
        footer.Controls.Add(_cancelButton);

        var root = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 0)
        };
        root.Controls.Add(_cardPanel);

        Controls.Add(root);
        Controls.Add(footer);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        _cardPanel.BackColor = AppTheme.SurfaceAlt;
        AppTheme.ApplyControlRegion(_cardPanel, 8);
        _cardPanel.Invalidate();
        AppTheme.ApplyTextInput(_nameTextBox);
        AppTheme.ApplyTextInput(_urlTextBox);
        AppTheme.ApplyPrimaryButton(_okButton, emphasize: true);
        AppTheme.ApplyPrimaryButton(_cancelButton);
        AppTheme.ApplyControlRegion(_okButton, 6);
        AppTheme.ApplyControlRegion(_cancelButton, 6);
        StyleControlTree(this);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.10F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.05F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.14F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.04F);

        using var gradient = new LinearGradientBrush(ClientRectangle, startColor, endColor, 90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);
    }

    private static void StyleControlTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Label label)
            {
                label.ForeColor = AppTheme.TextSecondary;
            }

            StyleControlTree(control);
        }
    }
}

