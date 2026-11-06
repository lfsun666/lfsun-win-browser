using System.Drawing.Imaging;
using Lfsun.Win.Browser.Browser;

namespace Lfsun.Win.Browser.UI.SidePanel;

/// <summary>
/// 中文注释：侧边栏实用工具箱视图。
/// 整合网页整页快照截图、手机跨端扫码浏览、阅读与护眼滤镜、缩放快捷控制及链接格式化复制等高频生产力工具。
/// </summary>
public sealed class ToolboxView : UserControl
{
    private readonly Func<BrowserTab?> _getActiveTab;
    private readonly Action<string> _toastCallback;
    private readonly Action? _toggleSplitCallback;

    private readonly PictureBox _qrBox = new();
    private readonly Label _qrUrlLabel = new();
    private readonly Label _zoomDisplayLabel = new();
    private Bitmap? _cachedQrBitmap;

    public ToolboxView(
        Func<BrowserTab?> getActiveTab,
        Action<string> toastCallback,
        Action? toggleSplitCallback = null)
    {
        _getActiveTab = getActiveTab;
        _toastCallback = toastCallback;
        _toggleSplitCallback = toggleSplitCallback;

        Dock = DockStyle.Fill;
        AutoScroll = true;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        ApplyTheme();
        RefreshContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cachedQrBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var mainLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 8, 10, 16),
            BackColor = AppTheme.Surface
        };

        // 1. 网页截图卡片
        mainLayout.Controls.Add(CreateSectionHeader("📸 网页截图与快照"));
        var capturePanel = new FlowLayoutPanel { Width = 310, AutoSize = true, Margin = new Padding(0, 4, 0, 10) };
        var copyShotBtn = CreateButton("📷 截图到剪贴板", 140, async () =>
        {
            var tab = _getActiveTab();
            if (tab is null) return;
            var bmp = await tab.CaptureScreenshotAsync();
            if (bmp is not null)
            {
                Clipboard.SetImage(bmp);
                _toastCallback("网页截图已复制到剪贴板！");
            }
            else
            {
                _toastCallback("截图失败，请确保页面已加载完成");
            }
        });
        var saveShotBtn = CreateButton("💾 截图另存为...", 140, async () =>
        {
            var tab = _getActiveTab();
            if (tab is null) return;
            var bmp = await tab.CaptureScreenshotAsync();
            if (bmp is null)
            {
                _toastCallback("截图失败，请确保页面已加载完成");
                return;
            }

            using var saveDialog = new SaveFileDialog
            {
                Filter = "PNG 图像 (*.png)|*.png",
                FileName = $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png"
            };
            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                bmp.Save(saveDialog.FileName, ImageFormat.Png);
                _toastCallback("截图已保存成功！");
            }
        });
        capturePanel.Controls.Add(copyShotBtn);
        capturePanel.Controls.Add(saveShotBtn);
        mainLayout.Controls.Add(capturePanel);

        // 2. 手机跨端扫码 (二维码)
        mainLayout.Controls.Add(CreateSectionHeader("📱 手机扫码继续浏览"));
        var qrCard = new Panel
        {
            Width = 310,
            Height = 210,
            BackColor = AppTheme.SurfaceAlt,
            Margin = new Padding(0, 4, 0, 10),
            Padding = new Padding(8)
        };
        _qrBox.Size = new Size(130, 130);
        _qrBox.Location = new Point(90, 8);
        _qrBox.SizeMode = PictureBoxSizeMode.CenterImage;
        _qrBox.BackColor = AppTheme.IsDark ? Color.FromArgb(32, 38, 52) : Color.White;
        qrCard.Controls.Add(_qrBox);

        _qrUrlLabel.Size = new Size(294, 20);
        _qrUrlLabel.Location = new Point(8, 144);
        _qrUrlLabel.TextAlign = ContentAlignment.MiddleCenter;
        _qrUrlLabel.Font = AppTheme.CreateFont(8.0F);
        _qrUrlLabel.ForeColor = AppTheme.TextSecondary;
        qrCard.Controls.Add(_qrUrlLabel);

        var qrActions = new FlowLayoutPanel { Location = new Point(45, 170), Size = new Size(220, 32) };
        var copyQrUrlBtn = CreateButton("复制网址", 100, () =>
        {
            var tab = _getActiveTab();
            if (tab?.CurrentUri is not null)
            {
                Clipboard.SetText(tab.CurrentUri.AbsoluteUri);
                _toastCallback("已复制网址");
            }
        });
        var saveQrImgBtn = CreateButton("保存二维码", 100, () =>
        {
            if (_cachedQrBitmap is null) return;
            using var saveDialog = new SaveFileDialog { Filter = "PNG 图像 (*.png)|*.png", FileName = "qrcode.png" };
            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                _cachedQrBitmap.Save(saveDialog.FileName, ImageFormat.Png);
                _toastCallback("二维码已保存");
            }
        });
        qrActions.Controls.Add(copyQrUrlBtn);
        qrActions.Controls.Add(saveQrImgBtn);
        qrCard.Controls.Add(qrActions);
        mainLayout.Controls.Add(qrCard);

                // 2.5 沉浸阅读、视频画中画与分屏
        mainLayout.Controls.Add(CreateSectionHeader("📖 沉浸阅读与实用扩展"));
        var readerPipPanel = new FlowLayoutPanel { Width = 310, AutoSize = true, Margin = new Padding(0, 4, 0, 10) };
        readerPipPanel.Controls.Add(CreateButton("📖 阅读模式", 90, async () =>
        {
            var tab = _getActiveTab();
            if (tab is not null)
            {
                var active = await tab.ToggleReaderModeAsync();
                _toastCallback(active ? "已开启沉浸式阅读模式 (Esc 退出)" : "已退出沉浸式阅读模式");
            }
        }));
        readerPipPanel.Controls.Add(CreateButton("📺 视频画中画", 95, async () =>
        {
            var tab = _getActiveTab();
            if (tab is not null)
            {
                var res = await tab.TogglePictureInPictureAsync();
                _toastCallback(res.message);
            }
        }));
        readerPipPanel.Controls.Add(CreateButton("◫ 双屏分屏", 95, () =>
        {
            if (_toggleSplitCallback != null)
            {
                _toggleSplitCallback();
            }
            else
            {
                _toastCallback("可通过快捷键 Alt+D 触发分屏");
            }
        }));
        mainLayout.Controls.Add(readerPipPanel);

        // 3. 阅读护眼滤镜
        mainLayout.Controls.Add(CreateSectionHeader("🌓 页面阅读与护眼滤镜"));
        var filterPanel = new FlowLayoutPanel { Width = 310, AutoSize = true, Margin = new Padding(0, 4, 0, 10) };
        filterPanel.Controls.Add(CreateButton("原样默认", 70, async () => await ApplyFilterAsync(string.Empty)));
        filterPanel.Controls.Add(CreateButton("柔和暖色", 70, async () => await ApplyFilterAsync("sepia(0.35) contrast(0.95) brightness(0.95)")));
        filterPanel.Controls.Add(CreateButton("夜间反色", 70, async () => await ApplyFilterAsync("invert(0.9) hue-rotate(180deg)")));
        filterPanel.Controls.Add(CreateButton("高对比度", 70, async () => await ApplyFilterAsync("contrast(1.3)")));
        mainLayout.Controls.Add(filterPanel);

        // 4. 页面缩放快捷控制
        mainLayout.Controls.Add(CreateSectionHeader("🔍 页面缩放快捷控制"));
        var zoomPanel = new FlowLayoutPanel { Width = 310, AutoSize = true, Margin = new Padding(0, 4, 0, 10) };
        var zoomOutBtn = CreateButton("— 缩小 10%", 90, () => ChangeZoom(-0.1));
        var zoomResetBtn = CreateButton("100% 重置", 90, () => SetZoom(1.0));
        var zoomInBtn = CreateButton("+ 放大 10%", 90, () => ChangeZoom(0.1));
        zoomPanel.Controls.Add(zoomOutBtn);
        zoomPanel.Controls.Add(zoomResetBtn);
        zoomPanel.Controls.Add(zoomInBtn);
        mainLayout.Controls.Add(zoomPanel);

        // 5. 格式化链接复制
        mainLayout.Controls.Add(CreateSectionHeader("📋 快捷格式化链接复制"));
        var linkPanel = new FlowLayoutPanel { Width = 310, AutoSize = true, Margin = new Padding(0, 4, 0, 10) };
        linkPanel.Controls.Add(CreateButton("仅复制网址", 90, () =>
        {
            var tab = _getActiveTab();
            if (tab?.CurrentUri is not null)
            {
                Clipboard.SetText(tab.CurrentUri.AbsoluteUri);
                _toastCallback("已复制纯网址");
            }
        }));
        linkPanel.Controls.Add(CreateButton("复制网页标题", 90, () =>
        {
            var tab = _getActiveTab();
            if (!string.IsNullOrWhiteSpace(tab?.Title))
            {
                Clipboard.SetText(tab.Title);
                _toastCallback("已复制网页标题");
            }
        }));
        linkPanel.Controls.Add(CreateButton("Markdown 链接", 100, () =>
        {
            var tab = _getActiveTab();
            if (tab?.CurrentUri is not null)
            {
                Clipboard.SetText($"[{tab.DisplayTitle}]({tab.CurrentUri.AbsoluteUri})");
                _toastCallback("已复制 Markdown 格式链接");
            }
        }));
        mainLayout.Controls.Add(linkPanel);

        Controls.Add(mainLayout);
    }

    private async Task ApplyFilterAsync(string filterCss)
    {
        var tab = _getActiveTab();
        if (tab is not null)
        {
            await tab.ApplyPageFilterAsync(filterCss);
            _toastCallback(string.IsNullOrEmpty(filterCss) ? "已恢复默认显示" : "已应用页面视觉滤镜");
        }
    }

    private void ChangeZoom(double delta)
    {
        var tab = _getActiveTab();
        if (tab is not null)
        {
            var newZoom = Math.Clamp(tab.ZoomFactor + delta, 0.25, 5.0);
            tab.ZoomFactor = newZoom;
            _toastCallback($"缩放: {(int)Math.Round(newZoom * 100)}%");
        }
    }

    private void SetZoom(double zoom)
    {
        var tab = _getActiveTab();
        if (tab is not null)
        {
            tab.ZoomFactor = zoom;
            _toastCallback("缩放已恢复 100%");
        }
    }

    public void RefreshContent()
    {
        var tab = _getActiveTab();
        var url = tab?.CurrentUri?.AbsoluteUri ?? "about:blank";

        _qrUrlLabel.Text = url.Length > 36 ? $"{url[..34]}..." : url;

        _cachedQrBitmap?.Dispose();
        _cachedQrBitmap = QrCodeGenerator.Generate(
            url,
            pixelSize: 4,
            margin: 2,
            fgColor: AppTheme.IsDark ? Color.FromArgb(235, 240, 255) : Color.FromArgb(20, 24, 35),
            bgColor: AppTheme.IsDark ? Color.FromArgb(32, 38, 52) : Color.White);

        _qrBox.Image = _cachedQrBitmap;
    }

    private Label CreateSectionHeader(string title)
    {
        return new Label
        {
            Text = title,
            Font = AppTheme.CreateFont(9.2F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Size = new Size(310, 22),
            Margin = new Padding(0, 6, 0, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Button CreateButton(string text, int width, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(width, 30),
            Font = AppTheme.CreateFont(8.5F),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(3),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
    }
}
