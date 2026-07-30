using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Security;

public sealed class CredentialEditDialog : Form
{
    private readonly TextBox _siteTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _passwordTextBox = new();
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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label { Text = "站点", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _siteTextBox.Dock = DockStyle.Fill;
        _siteTextBox.Text = site;
        _siteTextBox.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_siteTextBox, 1, 0);

        root.Controls.Add(new Label { Text = "用户名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _usernameTextBox.Dock = DockStyle.Fill;
        _usernameTextBox.Text = username;
        _usernameTextBox.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_usernameTextBox, 1, 1);

        root.Controls.Add(new Label { Text = "密码", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _passwordTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.Text = password;
        _passwordTextBox.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_passwordTextBox, 1, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _okButton.Text = "确定";
        _okButton.DialogResult = DialogResult.OK;
        _cancelButton.Text = "取消";
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(_cancelButton);
        root.Controls.Add(buttons, 1, 3);

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
        AppTheme.ApplyTextInput(_siteTextBox);
        AppTheme.ApplyTextInput(_usernameTextBox);
        AppTheme.ApplyTextInput(_passwordTextBox);
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

