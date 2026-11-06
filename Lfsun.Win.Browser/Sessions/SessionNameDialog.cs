using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Sessions;

public sealed class SessionNameDialog : Form
{
    private readonly TextBox _nameTextBox = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private SessionNameDialog(string defaultName)
    {
        Text = "命名会话";
        Width = 520;
        Height = 200;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);

        BuildLayout(defaultName);
        ApplyTheme();
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public string SessionName => _nameTextBox.Text.Trim();

    public static bool ShowDialog(IWin32Window owner, string defaultName, out string sessionName)
    {
        using var dialog = new SessionNameDialog(defaultName);
        var result = dialog.ShowDialog(owner) == DialogResult.OK;
        sessionName = dialog.SessionName;
        return result;
    }

    private void BuildLayout(string defaultName)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "会话名称",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 0);

        _nameTextBox.Text = defaultName;
        panel.Controls.Add(_nameTextBox, 1, 0);

        panel.Controls.Add(new Label
        {
            Text = "示例：工作标签 / 购物对比 / 多账号登录",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 10, 0, 0)
        }, 1, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12)
        };

        _okButton.Text = "保存";
        _okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("会话名称不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

