using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：手机扫码与页面二维码卡片弹窗。
/// 采用 Windows 11 Fluent 质感卡片布局，支持扫码、一键复制网址、保存二维码为图片。
/// </summary>
public sealed class QrCodeCardDialog : Form
{
    private readonly string _targetUrl;
    private readonly Bitmap _qrBitmap;
    private readonly PictureBox _qrBox = new();
    private readonly TextBox _urlTextBox = new();
    private readonly Button _copyUrlButton = new();
    private readonly Button _saveImageButton = new();
    private readonly Button _closeButton = new();

    public QrCodeCardDialog(string url, string? title = null)
    {
        _targetUrl = string.IsNullOrWhiteSpace(url) ? "about:blank" : url.Trim();
        Text = "手机扫码继续浏览";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 430);
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        _qrBitmap = QrCodeGenerator.Generate(
            _targetUrl,
            pixelSize: 6,
            margin: 3,
            fgColor: AppTheme.IsDark ? Color.FromArgb(235, 240, 255) : Color.FromArgb(20, 24, 35),
            bgColor: AppTheme.IsDark ? Color.FromArgb(32, 38, 52) : Color.White);

        BuildLayout(title);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _qrBitmap.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildLayout(string? title)
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24, 20, 24, 18),
            BackColor = AppTheme.Surface
        };

        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Title
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220)); // QR Picture
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // URL Box
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Buttons
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Padding

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "📱 手机扫码继续浏览",
            Font = AppTheme.CreateFont(11.5F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleCenter
        };
        mainPanel.Controls.Add(titleLabel, 0, 0);

        _qrBox.Dock = DockStyle.Fill;
        _qrBox.SizeMode = PictureBoxSizeMode.CenterImage;
        _qrBox.Image = _qrBitmap;
        _qrBox.BackColor = AppTheme.IsDark ? Color.FromArgb(32, 38, 52) : Color.White;
        _qrBox.Margin = new Padding(12, 6, 12, 10);

        void UpdateQrBoxRegion()
        {
            if (_qrBox.Width > 8 && _qrBox.Height > 8)
            {
                using var path = AppTheme.BuildRoundedRect(_qrBox.ClientRectangle, 10);
                _qrBox.Region = new Region(path);
            }
        }
        _qrBox.SizeChanged += (_, _) => UpdateQrBoxRegion();
        _qrBox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _qrBox.Width - 1, _qrBox.Height - 1), 10);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        mainPanel.Controls.Add(_qrBox, 0, 1);

        _urlTextBox.Dock = DockStyle.Fill;
        _urlTextBox.ReadOnly = true;
        _urlTextBox.Text = _targetUrl;
        _urlTextBox.Font = AppTheme.CreateFont(8.5F);
        _urlTextBox.BackColor = AppTheme.SurfaceAlt;
        _urlTextBox.ForeColor = AppTheme.TextSecondary;
        _urlTextBox.BorderStyle = BorderStyle.FixedSingle;
        mainPanel.Controls.Add(_urlTextBox, 0, 2);

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };

        _copyUrlButton.Text = "复制网址";
        _copyUrlButton.Size = new Size(94, 30);
        _copyUrlButton.Font = AppTheme.CreateFont(8.8F);
        _copyUrlButton.FlatStyle = FlatStyle.Flat;
        _copyUrlButton.BackColor = AppTheme.Accent;
        _copyUrlButton.ForeColor = Color.White;
        _copyUrlButton.FlatAppearance.BorderSize = 0;
        _copyUrlButton.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
        _copyUrlButton.FlatAppearance.MouseDownBackColor = AppTheme.AccentPressed;
        _copyUrlButton.Cursor = Cursors.Hand;
        _copyUrlButton.Click += (_, _) =>
        {
            Clipboard.SetText(_targetUrl);
            _copyUrlButton.Text = "已复制 √";
        };

        _saveImageButton.Text = "保存图片";
        _saveImageButton.Size = new Size(94, 30);
        _saveImageButton.Font = AppTheme.CreateFont(8.8F);
        _saveImageButton.FlatStyle = FlatStyle.Flat;
        _saveImageButton.BackColor = AppTheme.SurfaceAlt;
        _saveImageButton.ForeColor = AppTheme.TextPrimary;
        _saveImageButton.FlatAppearance.BorderSize = 1;
        _saveImageButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _saveImageButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.AccentHover, AppTheme.SurfaceAlt, 0.35F);
        _saveImageButton.FlatAppearance.MouseDownBackColor = AppTheme.Mix(AppTheme.AccentPressed, AppTheme.SurfaceAlt, 0.45F);
        _saveImageButton.Cursor = Cursors.Hand;
        _saveImageButton.Click += (_, _) => SaveQrImage();

        _closeButton.Text = "关闭";
        _closeButton.Size = new Size(94, 30);
        _closeButton.Font = AppTheme.CreateFont(8.8F);
        _closeButton.FlatStyle = FlatStyle.Flat;
        _closeButton.BackColor = AppTheme.SurfaceAlt;
        _closeButton.ForeColor = AppTheme.TextSecondary;
        _closeButton.FlatAppearance.BorderSize = 1;
        _closeButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _closeButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.AccentHover, AppTheme.SurfaceAlt, 0.35F);
        _closeButton.FlatAppearance.MouseDownBackColor = AppTheme.Mix(AppTheme.AccentPressed, AppTheme.SurfaceAlt, 0.45F);
        _closeButton.Cursor = Cursors.Hand;
        _closeButton.Click += (_, _) => Close();

        buttonBar.Controls.Add(_copyUrlButton);
        buttonBar.Controls.Add(_saveImageButton);
        buttonBar.Controls.Add(_closeButton);
        mainPanel.Controls.Add(buttonBar, 0, 3);

        Controls.Add(mainPanel);
    }

    private void SaveQrImage()
    {
        using var saveDialog = new SaveFileDialog
        {
            Filter = "PNG 图像 (*.png)|*.png",
            FileName = "lfsun-qrcode.png",
            Title = "保存网页二维码"
        };

        if (saveDialog.ShowDialog(this) == DialogResult.OK)
        {
            _qrBitmap.Save(saveDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
            _saveImageButton.Text = "已保存 √";
        }
    }
}
