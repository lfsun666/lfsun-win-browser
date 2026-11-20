using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Sessions;

public sealed class SessionNameDialog : Form
{
    private readonly Panel _cardPanel = new();
    private readonly TextBox _nameTextBox = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private SessionNameDialog(string defaultName)
    {
        Text = "命名会话";
        Width = 520;
        Height = 210;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        // 中文注释：与设置窗口一致的高 DPI 自适应，高缩放屏上控件与字体等比例放大。
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

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
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14, 14, 14, 10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _cardPanel.Dock = DockStyle.Fill;
        _cardPanel.BackColor = AppTheme.SurfaceAlt;
        _cardPanel.Padding = new Padding(14, 12, 14, 12);
        AppTheme.ApplyControlRegion(_cardPanel, 8);
        _cardPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = "会话名称",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = AppTheme.CreateFont(9.2F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary
        };
        panel.Controls.Add(titleLabel, 0, 0);

        _nameTextBox.Dock = DockStyle.Fill;
        _nameTextBox.Text = defaultName;
        panel.Controls.Add(_nameTextBox, 1, 0);

        var hintLabel = new Label
        {
            Text = "示例：工作标签 / 购物对比 / 多账号登录",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 8, 0, 0)
        };
        panel.Controls.Add(hintLabel, 1, 1);
        _cardPanel.Controls.Add(panel);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
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

        root.Controls.Add(_cardPanel, 0, 0);
        root.Controls.Add(footer, 0, 1);
        Controls.Add(root);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        _cardPanel.BackColor = AppTheme.SurfaceAlt;
        AppTheme.ApplyTextInput(_nameTextBox);
        AppTheme.ApplyPrimaryButton(_okButton, emphasize: true);
        AppTheme.ApplyPrimaryButton(_cancelButton);
        AppTheme.ApplyControlRegion(_okButton, 6);
        AppTheme.ApplyControlRegion(_cancelButton, 6);
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
}

