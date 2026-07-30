namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：页内查找浮动条（输入框 + 匹配计数 + 上一个/下一个 + 区分大小写 + 关闭）。
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
        Size = new Size(380, 40);
        Padding = new Padding(8, 6, 8, 6);
        BorderStyle = BorderStyle.FixedSingle;
        Visible = false;

        _searchTextBox.BorderStyle = BorderStyle.FixedSingle;
        _searchTextBox.PlaceholderText = "在页面中查找";
        _searchTextBox.Location = new Point(10, 9);
        _searchTextBox.Width = 150;
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
        _matchCountLabel.Size = new Size(56, 22);
        _matchCountLabel.Location = new Point(164, 9);
        _matchCountLabel.TextAlign = ContentAlignment.MiddleCenter;
        _matchCountLabel.Text = string.Empty;

        ConfigureIconButton(_previousButton, "\uE70E", "↑", "上一个匹配 (Shift+Enter)", new Point(222, 7));
        _previousButton.Click += (_, _) => PreviousRequested?.Invoke();
        ConfigureIconButton(_nextButton, "\uE70D", "↓", "下一个匹配 (Enter)", new Point(252, 7));
        _nextButton.Click += (_, _) => NextRequested?.Invoke();

        _matchCaseCheckBox.Appearance = Appearance.Button;
        _matchCaseCheckBox.Size = new Size(30, 26);
        _matchCaseCheckBox.Location = new Point(282, 7);
        _matchCaseCheckBox.Text = "Aa";
        _matchCaseCheckBox.TextAlign = ContentAlignment.MiddleCenter;
        _matchCaseCheckBox.FlatStyle = FlatStyle.Flat;
        _matchCaseCheckBox.FlatAppearance.BorderSize = 0;
        new ToolTip().SetToolTip(_matchCaseCheckBox, "区分大小写");
        _matchCaseCheckBox.CheckedChanged += (_, _) => RaiseSearch();

        ConfigureIconButton(_closeButton, "\uE711", "×", "关闭查找 (Esc)", new Point(316, 7));
        _closeButton.Click += (_, _) => CloseRequested?.Invoke();

        Controls.Add(_searchTextBox);
        Controls.Add(_matchCountLabel);
        Controls.Add(_previousButton);
        Controls.Add(_nextButton);
        Controls.Add(_matchCaseCheckBox);
        Controls.Add(_closeButton);
        ApplyTheme();
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
        _matchCountLabel.ForeColor = count > 0 ? AppTheme.TextSecondary : Color.IndianRed;
    }

    /// <summary>中文注释：清空查询与计数（关闭时调用）。</summary>
    public void ResetSearch()
    {
        _searchTextBox.Clear();
        UpdateMatches(0, 0);
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;
        _searchTextBox.BackColor = AppTheme.InputBackground;
        _searchTextBox.ForeColor = AppTheme.TextPrimary;
        _matchCountLabel.ForeColor = AppTheme.TextSecondary;
        foreach (var button in new[] { _previousButton, _nextButton, _closeButton })
        {
            button.BackColor = AppTheme.Surface;
            button.ForeColor = AppTheme.TextPrimary;
            button.FlatAppearance.MouseOverBackColor = AppTheme.SurfaceAlt;
        }

        _matchCaseCheckBox.BackColor = _matchCaseCheckBox.Checked ? AppTheme.Accent : AppTheme.Surface;
        _matchCaseCheckBox.ForeColor = _matchCaseCheckBox.Checked ? Color.White : AppTheme.TextPrimary;
    }

    private void ConfigureIconButton(Button button, string glyph, string fallbackText, string toolTip, Point location)
    {
        button.Size = new Size(28, 26);
        button.Location = location;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        var iconFont = AppTheme.CreateIconFont(9F);
        if (iconFont is not null)
        {
            button.Font = iconFont;
            button.Text = glyph;
        }
        else
        {
            button.Text = fallbackText;
        }

        button.TabStop = false;
        new ToolTip().SetToolTip(button, toolTip);
    }

    private void RaiseSearch()
    {
        _matchCaseCheckBox.BackColor = _matchCaseCheckBox.Checked ? AppTheme.Accent : AppTheme.Surface;
        _matchCaseCheckBox.ForeColor = _matchCaseCheckBox.Checked ? Color.White : AppTheme.TextPrimary;
        SearchRequested?.Invoke(_searchTextBox.Text, _matchCaseCheckBox.Checked);
    }
}
