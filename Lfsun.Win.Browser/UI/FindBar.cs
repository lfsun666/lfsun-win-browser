using System.Drawing.Drawing2D;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：页内查找浮动条（输入框 + 匹配计数 + 上一个/下一个 + 区分大小写 + 关闭）。
/// 采用 Windows 11 Fluent 浮动圆角卡片质感与平滑微动交互。
/// </summary>
public sealed class FindBar : Panel
{
    private readonly TextBox _searchTextBox = new();
    private readonly Label _matchCountLabel = new();
    private readonly CheckBox _matchCaseCheckBox = new();
    private readonly Button _previousButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _closeButton = new();

    /// <summary>中文注释：查找请求（查询词、是否区分大小写）。</summary>
    public event Action<string, bool>? SearchRequested;

    /// <summary>中文注释：跳到下一个匹配。</summary>
    public event Action? NextRequested;

    /// <summary>中文注释：跳到上一个匹配。</summary>
    public event Action? PreviousRequested;

    /// <summary>中文注释：关闭查找条（Esc 或点关闭按钮）。</summary>
    public event Action? CloseRequested;

    public FindBar()
    {
        Size = new Size(410, 42);
        Padding = new Padding(10, 6, 10, 6);
        BorderStyle = BorderStyle.None;
        DoubleBuffered = true;
        Visible = false;

        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.PlaceholderText = "在页面中查找 (Ctrl+F)";
        _searchTextBox.Location = new Point(12, 11);
        _searchTextBox.Width = 160;
        _searchTextBox.Font = AppTheme.CreateFont(9.5F);
        _searchTextBox.TextChanged += (_, _) => RaiseSearch();
        _searchTextBox.KeyDown += (_, eventArgs) =>
        {
            switch (eventArgs.KeyData)
            {
                case Keys.Enter:
                    NextRequested?.Invoke();
                    break;
                case Keys.Shift | Keys.Enter:
                    PreviousRequested?.Invoke();
                    break;
                case Keys.Escape:
                    CloseRequested?.Invoke();
                    break;
                default:
                    return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
        };

        _matchCountLabel.AutoSize = false;
        _matchCountLabel.Size = new Size(62, 24);
        _matchCountLabel.Location = new Point(178, 9);
        _matchCountLabel.TextAlign = ContentAlignment.MiddleCenter;
        _matchCountLabel.Font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
        _matchCountLabel.Text = string.Empty;

        ConfigureIconButton(_previousButton, "\uE70E", "↑", "上一个匹配 (Shift+Enter)", new Point(246, 7));
        _previousButton.Click += (_, _) => PreviousRequested?.Invoke();
        ConfigureIconButton(_nextButton, "\uE70D", "↓", "下一个匹配 (Enter)", new Point(278, 7));
        _nextButton.Click += (_, _) => NextRequested?.Invoke();

        _matchCaseCheckBox.Appearance = Appearance.Button;
        _matchCaseCheckBox.Size = new Size(34, 26);
        _matchCaseCheckBox.Location = new Point(312, 7);
        _matchCaseCheckBox.Text = "Aa";
        _matchCaseCheckBox.TextAlign = ContentAlignment.MiddleCenter;
        _matchCaseCheckBox.FlatStyle = FlatStyle.Flat;
        _matchCaseCheckBox.FlatAppearance.BorderSize = 0;
        _matchCaseCheckBox.Font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
        new ToolTip().SetToolTip(_matchCaseCheckBox, "区分大小写");
        _matchCaseCheckBox.CheckedChanged += (_, _) => RaiseSearch();

        ConfigureIconButton(_closeButton, "\uE711", "×", "关闭查找 (Esc)", new Point(352, 7));
        _closeButton.Click += (_, _) => CloseRequested?.Invoke();

        Controls.Add(_searchTextBox);
        Controls.Add(_matchCountLabel);
        Controls.Add(_previousButton);
        Controls.Add(_nextButton);
        Controls.Add(_matchCaseCheckBox);
        Controls.Add(_closeButton);
        ApplyTheme();
        UpdateCardRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCardRegion();
    }

    private void UpdateCardRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        var oldRegion = Region;
        using var path = BuildRoundedRectangle(new Rectangle(0, 0, Width, Height), 10);
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    public string SearchTerm => _searchTextBox.Text;

    public bool MatchCase => _matchCaseCheckBox.Checked;

    /// <summary>中文注释：显示并聚焦输入框（已有内容则全选，便于直接替换查询）。</summary>
    public void ShowAndFocus()
    {
        Visible = true;
        BringToFront();
        _searchTextBox.Focus();
        _searchTextBox.SelectAll();
    }

    /// <summary>中文注释：刷新匹配计数显示；count 为 0 且有查询词时标红提示。</summary>
    public void UpdateMatches(int activeIndex, int count)
    {
        if (string.IsNullOrEmpty(_searchTextBox.Text))
        {
            _matchCountLabel.Text = string.Empty;
            _matchCountLabel.ForeColor = AppTheme.TextSecondary;
            return;
        }

        _matchCountLabel.Text = count > 0 ? $"{Math.Max(activeIndex, 1)}/{count}" : "0/0";
        _matchCountLabel.ForeColor = count > 0 ? AppTheme.TextSecondary : AppTheme.Danger;
    }

    /// <summary>中文注释：清空查询与计数（关闭时调用）。</summary>
    public void ResetSearch()
    {
        _searchTextBox.Clear();
        UpdateMatches(0, 0);
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.SurfaceAlt;
        ForeColor = AppTheme.TextPrimary;
        _searchTextBox.BackColor = AppTheme.InputBackground;
        _searchTextBox.ForeColor = AppTheme.TextPrimary;
        _matchCountLabel.ForeColor = AppTheme.TextSecondary;

        foreach (var button in new[] { _previousButton, _nextButton })
        {
            button.BackColor = Color.Transparent;
            button.ForeColor = AppTheme.TextPrimary;
            button.FlatAppearance.MouseOverBackColor = AppTheme.IsDark
                ? Color.FromArgb(40, 255, 255, 255)
                : Color.FromArgb(20, 0, 0, 0);
        }

        _closeButton.BackColor = Color.Transparent;
        _closeButton.ForeColor = AppTheme.TextPrimary;
        _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        _closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(199, 15, 29);

        UpdateMatchCaseButtonVisual();
        Invalidate();
    }

    private void UpdateMatchCaseButtonVisual()
    {
        if (_matchCaseCheckBox.Checked)
        {
            _matchCaseCheckBox.BackColor = AppTheme.Accent;
            _matchCaseCheckBox.ForeColor = Color.White;
        }
        else
        {
            _matchCaseCheckBox.BackColor = Color.Transparent;
            _matchCaseCheckBox.ForeColor = AppTheme.TextSecondary;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var cardPath = BuildRoundedRectangle(bounds, 10);
        using var bgBrush = new SolidBrush(AppTheme.SurfaceAlt);
        using var borderPen = new Pen(AppTheme.PanelBorder, 1F);
        e.Graphics.FillPath(bgBrush, cardPath);
        e.Graphics.DrawPath(borderPen, cardPath);

        // 输入框背后的内嵌圆角胶囊
        var inputRect = new Rectangle(8, 7, 168, 28);
        using var inputPath = BuildRoundedRectangle(inputRect, 6);
        using var inputBrush = new SolidBrush(AppTheme.InputBackground);
        using var inputBorder = new Pen(AppTheme.PanelBorder, 1F);
        e.Graphics.FillPath(inputBrush, inputPath);
        e.Graphics.DrawPath(inputBorder, inputPath);
    }

    private static GraphicsPath BuildRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ConfigureIconButton(Button button, string glyph, string fallbackText, string toolTip, Point location)
    {
        button.Size = new Size(28, 28);
        button.Location = location;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        var iconFont = AppTheme.CreateIconFont(9.5F);
        if (iconFont is not null)
        {
            button.Font = iconFont;
            button.Text = glyph;
        }
        else
        {
            button.Font = AppTheme.CreateFont(9.5F, FontStyle.Bold);
            button.Text = fallbackText;
        }

        button.TabStop = false;
        new ToolTip().SetToolTip(button, toolTip);
    }

    private void RaiseSearch()
    {
        UpdateMatchCaseButtonVisual();
        SearchRequested?.Invoke(_searchTextBox.Text, _matchCaseCheckBox.Checked);
    }
}
