using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Security;

public sealed class CredentialEditDialog : Form
{
    private readonly TextBox _siteTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly Panel _cardPanel = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private CredentialEditDialog(string title, string site, string username, string password)
    {
        Text = title;
        Width = 480;
        Height = 300;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        // 中文注释：与设置窗口一致的高 DPI 自适应，高缩放屏上控件与字体等比例放大。
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        _cardPanel.Dock = DockStyle.Fill;
        _cardPanel.BackColor = AppTheme.SurfaceAlt;
        _cardPanel.Padding = new Padding(12);
        _cardPanel.Margin = new Padding(0, 0, 0, 6);
        _cardPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        AppTheme.ApplyControlRegion(_cardPanel, 8);

        var formTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        formTable.Controls.Add(new Label { Text = "站点", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _siteTextBox.Dock = DockStyle.Fill;
        _siteTextBox.Text = site;
        _siteTextBox.BorderStyle = BorderStyle.FixedSingle;
        formTable.Controls.Add(_siteTextBox, 1, 0);

        formTable.Controls.Add(new Label { Text = "用户名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _usernameTextBox.Dock = DockStyle.Fill;
        _usernameTextBox.Text = username;
        _usernameTextBox.BorderStyle = BorderStyle.FixedSingle;
        formTable.Controls.Add(_usernameTextBox, 1, 1);

        formTable.Controls.Add(new Label { Text = "密码", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _passwordTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.Text = password;
        _passwordTextBox.BorderStyle = BorderStyle.FixedSingle;
        formTable.Controls.Add(_passwordTextBox, 1, 2);

        _cardPanel.Controls.Add(formTable);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 10, 12, 12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_cardPanel, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        _okButton.Text = "确定";
        _okButton.DialogResult = DialogResult.OK;
        _cancelButton.Text = "取消";
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(_cancelButton);
        root.Controls.Add(buttons, 0, 1);

        _okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_siteTextBox.Text) ||
                string.IsNullOrWhiteSpace(_usernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(_passwordTextBox.Text))
            {
                MessageBox.Show("站点、用户名、密码均不能为空。", "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        Controls.Add(root);
        ApplyTheme();
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        _cardPanel.BackColor = AppTheme.SurfaceAlt;
        AppTheme.ApplyControlRegion(_cardPanel, 8);
        _cardPanel.Invalidate();
        AppTheme.ApplyTextInput(_siteTextBox);
        AppTheme.ApplyTextInput(_usernameTextBox);
        AppTheme.ApplyTextInput(_passwordTextBox);
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

    public static bool ShowDialog(
        IWin32Window owner,
        string title,
        string defaultSite,
        string defaultUsername,
        string defaultPassword,
        out string site,
        out string username,
        out string password)
    {
        using var dialog = new CredentialEditDialog(title, defaultSite, defaultUsername, defaultPassword);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            site = string.Empty;
            username = string.Empty;
            password = string.Empty;
            return false;
        }

        site = dialog._siteTextBox.Text.Trim();
        username = dialog._usernameTextBox.Text.Trim();
        password = dialog._passwordTextBox.Text;
        return true;
    }
}

