using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Bookmarks;

public sealed class BookmarkEditDialog : Form
{
    private readonly TextBox _nameTextBox = new();
    private readonly TextBox _urlTextBox = new();
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
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Text = "名称", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _nameTextBox.Text = defaultName;
        panel.Controls.Add(_nameTextBox, 1, 0);

        if (!_isFolder)
        {
            panel.Controls.Add(new Label { Text = "地址", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _urlTextBox.Text = defaultUrl;
            panel.Controls.Add(_urlTextBox, 1, 1);
        }

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

        Controls.Add(panel);
        Controls.Add(footer);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        AppTheme.ApplyTextInput(_nameTextBox);
        AppTheme.ApplyTextInput(_urlTextBox);
        AppTheme.ApplyPrimaryButton(_okButton, emphasize: true);
        AppTheme.ApplyPrimaryButton(_cancelButton);
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

