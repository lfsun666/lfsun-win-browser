using System.Text;

namespace Lfsun.Win.Browser.UI.SidePanel;

/// <summary>
/// 中文注释：侧边栏随手便签与临时记事本。
/// 支持自动持久化到本地文件、一键插入当前网页标题与链接、字数统计、快捷复制。
/// </summary>
public sealed class ScratchpadView : UserControl
{
    private readonly Func<string?> _getCurrentPageLinkCallback;
    private readonly Action<string> _toastCallback;
    private readonly string _notesFilePath;

    private readonly Panel _textBoxCardPanel = new();
    private readonly TextBox _textBox = new();
    private readonly Button _insertLinkButton = new();
    private readonly Button _copyAllButton = new();
    private readonly Button _clearButton = new();
    private readonly Label _statsLabel = new();
    private readonly System.Windows.Forms.Timer _autoSaveTimer = new();

    public ScratchpadView(
        Func<string?> getCurrentPageLinkCallback,
        Action<string> toastCallback)
    {
        _getCurrentPageLinkCallback = getCurrentPageLinkCallback;
        _toastCallback = toastCallback;

        var notesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LfsunBrowser",
            "notes");
        Directory.CreateDirectory(notesDir);
        _notesFilePath = Path.Combine(notesDir, "scratchpad.txt");

        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        ApplyTheme();
        LoadNoteContent();

        _autoSaveTimer.Interval = 1000;
        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            SaveNoteContent();
        };
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 6, 8, 8),
            BackColor = AppTheme.Surface
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Top toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Notes text box
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Bottom stats

        var toolBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        _insertLinkButton.Text = "➕ 插入当前页";
        _insertLinkButton.Size = new Size(100, 28);
        _insertLinkButton.Font = AppTheme.CreateFont(8.5F);
        _insertLinkButton.FlatStyle = FlatStyle.Flat;
        _insertLinkButton.FlatAppearance.BorderSize = 0;
        _insertLinkButton.BackColor = AppTheme.Accent;
        _insertLinkButton.ForeColor = Color.White;
        _insertLinkButton.Cursor = Cursors.Hand;
        AppTheme.ApplyControlRegion(_insertLinkButton, 6);
        _insertLinkButton.Click += (_, _) => InsertCurrentPageLink();

        _copyAllButton.Text = "📋 复制";
        _copyAllButton.Size = new Size(68, 28);
        _copyAllButton.Font = AppTheme.CreateFont(8.5F);
        _copyAllButton.FlatStyle = FlatStyle.Flat;
        _copyAllButton.BackColor = AppTheme.SurfaceAlt;
        _copyAllButton.ForeColor = AppTheme.TextPrimary;
        _copyAllButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _copyAllButton.Cursor = Cursors.Hand;
        AppTheme.ApplyControlRegion(_copyAllButton, 6);
        _copyAllButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_textBox.Text))
            {
                Clipboard.SetText(_textBox.Text);
                _toastCallback("便签内容已复制到剪贴板");
            }
        };

        _clearButton.Text = "🗑️ 清空";
        _clearButton.Size = new Size(68, 28);
        _clearButton.Font = AppTheme.CreateFont(8.5F);
        _clearButton.FlatStyle = FlatStyle.Flat;
        _clearButton.BackColor = AppTheme.SurfaceAlt;
        _clearButton.ForeColor = AppTheme.TextSecondary;
        _clearButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _clearButton.Cursor = Cursors.Hand;
        AppTheme.ApplyControlRegion(_clearButton, 6);
        _clearButton.Click += (_, _) =>
        {
            if (_textBox.TextLength > 0 &&
                MessageBox.Show("确定清空便签内容吗？", "清空便签", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _textBox.Clear();
                SaveNoteContent();
                UpdateStats();
            }
        };

        toolBar.Controls.Add(_insertLinkButton);
        toolBar.Controls.Add(_copyAllButton);
        toolBar.Controls.Add(_clearButton);
        mainLayout.Controls.Add(toolBar, 0, 0);

        _textBoxCardPanel.Dock = DockStyle.Fill;
        _textBoxCardPanel.BackColor = AppTheme.SurfaceAlt;
        _textBoxCardPanel.Padding = new Padding(8);
        _textBoxCardPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _textBoxCardPanel.Width - 1, _textBoxCardPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        AppTheme.ApplyControlRegion(_textBoxCardPanel, 8);

        _textBox.Dock = DockStyle.Fill;
        _textBox.Multiline = true;
        _textBox.ScrollBars = ScrollBars.Vertical;
        _textBox.BorderStyle = BorderStyle.None;
        _textBox.BackColor = AppTheme.SurfaceAlt;
        _textBox.Font = AppTheme.CreateFont(9.5F);
        _textBox.PlaceholderText = "在此记录灵感、待读清单、摘录或网页笔记...\n(自动即时保存到本地)";
        _textBox.TextChanged += (_, _) =>
        {
            UpdateStats();
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        };
        _textBoxCardPanel.Controls.Add(_textBox);
        mainLayout.Controls.Add(_textBoxCardPanel, 0, 1);

        _statsLabel.Dock = DockStyle.Fill;
        _statsLabel.TextAlign = ContentAlignment.MiddleRight;
        _statsLabel.Font = AppTheme.CreateFont(8F);
        _statsLabel.ForeColor = AppTheme.TextSecondary;
        _statsLabel.Text = "0 字 | 已自动保存";
        mainLayout.Controls.Add(_statsLabel, 0, 2);

        Controls.Add(mainLayout);
    }

    private void InsertCurrentPageLink()
    {
        var link = _getCurrentPageLinkCallback();
        if (string.IsNullOrWhiteSpace(link))
        {
            _toastCallback("当前标签无可用链接");
            return;
        }

        var insertText = $"{link}\r\n";
        _textBox.Paste(insertText);
        SaveNoteContent();
        _toastCallback("已插入当前网页链接");
    }

    private void UpdateStats()
    {
        var count = _textBox.TextLength;
        _statsLabel.Text = $"{count} 字 | 自动保存";
    }

    private void LoadNoteContent()
    {
        try
        {
            if (File.Exists(_notesFilePath))
            {
                _textBox.Text = File.ReadAllText(_notesFilePath, Encoding.UTF8);
                UpdateStats();
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private void SaveNoteContent()
    {
        try
        {
            File.WriteAllText(_notesFilePath, _textBox.Text, Encoding.UTF8);
            _statsLabel.Text = $"{_textBox.TextLength} 字 | 已保存 √";
        }
        catch
        {
            // Ignore save errors
        }
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _textBoxCardPanel.BackColor = AppTheme.SurfaceAlt;
        _textBox.BackColor = AppTheme.SurfaceAlt;
        _textBox.ForeColor = AppTheme.TextPrimary;

        _insertLinkButton.BackColor = AppTheme.Accent;
        _insertLinkButton.ForeColor = Color.White;
        _insertLinkButton.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
        _insertLinkButton.FlatAppearance.MouseDownBackColor = AppTheme.AccentPressed;
        AppTheme.ApplyControlRegion(_insertLinkButton, 6);

        _copyAllButton.BackColor = AppTheme.SurfaceAlt;
        _copyAllButton.ForeColor = AppTheme.TextPrimary;
        _copyAllButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _copyAllButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.AccentHover, AppTheme.SurfaceAlt, 0.35F);
        _copyAllButton.FlatAppearance.MouseDownBackColor = AppTheme.Mix(AppTheme.AccentPressed, AppTheme.SurfaceAlt, 0.45F);
        AppTheme.ApplyControlRegion(_copyAllButton, 6);

        _clearButton.BackColor = AppTheme.SurfaceAlt;
        _clearButton.ForeColor = AppTheme.TextSecondary;
        _clearButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _clearButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        _clearButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(199, 15, 29);
        AppTheme.ApplyControlRegion(_clearButton, 6);

        AppTheme.ApplyControlRegion(_textBoxCardPanel, 8);
        _textBoxCardPanel.Invalidate();

        _statsLabel.ForeColor = AppTheme.TextSecondary;
    }
}
