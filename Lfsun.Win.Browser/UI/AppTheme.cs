using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Lfsun.Win.Browser.UI;

public static class AppTheme
{
    private static bool _isDark;
    private static bool _isCompact;
    private static string _themePreference = ThemeOptionNames.ThemeLight;
    private static string _accentPreset = ThemeOptionNames.AccentBlue;
    private static string _fontPreset = ThemeOptionNames.FontYaHei;
    private static string _accentStrength = ThemeOptionNames.AccentStrengthStandard;
    private static readonly HashSet<string> InstalledFontFamilies = FontFamily.Families
        .Select(family => family.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly string? IconFontFamilyName = ResolveIconFontFamilyName();
    private static readonly Dictionary<(string Glyph, int Size, int ColorArgb), Bitmap> IconImageCache = new();

    public static bool HasIconFont => IconFontFamilyName is not null;

    public static bool IsDark => _isDark;
    public static bool IsCompact => _isCompact;
    // 中文注释：用户选择的主题偏好（Light/Dark/System）；CurrentTheme 仍返回生效后的深浅。
    public static string CurrentThemePreference => _themePreference;
    public static bool IsFollowingSystem => string.Equals(_themePreference, ThemeOptionNames.ThemeSystem, StringComparison.OrdinalIgnoreCase);
    public static string CurrentAccent => _accentPreset;
    public static string CurrentFontPreset => _fontPreset;
    public static string CurrentAccentStrength => _accentStrength;
    public static string CurrentTheme => _isDark ? ThemeOptionNames.ThemeDark : ThemeOptionNames.ThemeLight;
    public static string CurrentDensity => _isCompact ? ThemeOptionNames.DensityCompact : ThemeOptionNames.DensityNormal;

    // 中文注释：Win11 Fluent 风中性色板：背景/表面层次靠明度区分，不再混入蓝灰调。
    public static Color WindowBackground => _isDark ? Color.FromArgb(25, 27, 31) : Color.FromArgb(238, 240, 244);
    public static Color Surface => _isDark ? Color.FromArgb(32, 35, 41) : Color.FromArgb(249, 250, 252);
    public static Color SurfaceAlt => _isDark ? Color.FromArgb(41, 45, 52) : Color.FromArgb(240, 242, 246);
    public static Color PanelBorder => _isDark ? Color.FromArgb(58, 64, 74) : Color.FromArgb(218, 223, 230);
    public static Color Accent => ResolveAccentBase();
    public static Color AccentMuted => _isDark
        ? Blend(Accent, Surface, ResolveMutedRatio(isDark: true))
        : Blend(Accent, Color.White, ResolveMutedRatio(isDark: false));
    public static Color AccentHover => _isDark
        ? Blend(Accent, Surface, ResolveHoverRatio(isDark: true))
        : Blend(Accent, Color.White, ResolveHoverRatio(isDark: false));
    public static Color AccentPressed => _isDark
        ? Blend(Accent, Color.Black, 0.16F)
        : Blend(Accent, Color.Black, 0.16F);
    public static Color TextPrimary => _isDark ? Color.FromArgb(237, 240, 245) : Color.FromArgb(26, 31, 40);
    public static Color TextSecondary => _isDark ? Color.FromArgb(158, 167, 180) : Color.FromArgb(96, 106, 122);
    public static Color InputBackground => _isDark ? Color.FromArgb(36, 40, 47) : Color.White;
    public static Color Success => _isDark ? Color.FromArgb(82, 199, 152) : Color.FromArgb(21, 128, 92);
    public static Color Warning => _isDark ? Color.FromArgb(242, 178, 90) : Color.FromArgb(178, 108, 14);
    public static Color Danger => _isDark ? Color.FromArgb(235, 113, 130) : Color.FromArgb(180, 45, 62);

    public static void Configure(string? theme, string? density, string? accent = null, string? font = null, string? accentStrength = null)
    {
        _themePreference = string.Equals(theme, ThemeOptionNames.ThemeSystem, StringComparison.OrdinalIgnoreCase)
            ? ThemeOptionNames.ThemeSystem
            : string.Equals(theme, ThemeOptionNames.ThemeDark, StringComparison.OrdinalIgnoreCase)
                ? ThemeOptionNames.ThemeDark
                : ThemeOptionNames.ThemeLight;
        _isDark = IsFollowingSystem
            ? IsSystemDarkTheme()
            : string.Equals(_themePreference, ThemeOptionNames.ThemeDark, StringComparison.OrdinalIgnoreCase);
        _isCompact = string.Equals(density, ThemeOptionNames.DensityCompact, StringComparison.OrdinalIgnoreCase);
        _accentPreset = NormalizeAccentPreset(accent);
        _fontPreset = NormalizeFontPreset(font);
        _accentStrength = NormalizeAccentStrength(accentStrength);
    }

    /// <summary>
    /// 中文注释：读注册表 AppsUseLightTheme 判断系统应用主题是否为深色；读取失败时回退浅色。
    /// </summary>
    public static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int lightFlag && lightFlag == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeAccentPreset(string? accent)
    {
        if (string.Equals(accent, ThemeOptionNames.AccentGreen, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentGreen;
        }

        if (string.Equals(accent, ThemeOptionNames.AccentOrange, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentOrange;
        }

        if (string.Equals(accent, ThemeOptionNames.AccentRose, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentRose;
        }

        return ThemeOptionNames.AccentBlue;
    }

    private static string NormalizeFontPreset(string? font)
    {
        if (string.Equals(font, ThemeOptionNames.FontDengXian, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.FontDengXian;
        }

        if (string.Equals(font, ThemeOptionNames.FontSong, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.FontSong;
        }

        return ThemeOptionNames.FontYaHei;
    }

    private static string NormalizeAccentStrength(string? accentStrength)
    {
        if (string.Equals(accentStrength, ThemeOptionNames.AccentStrengthSoft, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentStrengthSoft;
        }

        if (string.Equals(accentStrength, ThemeOptionNames.AccentStrengthVivid, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentStrengthVivid;
        }

        return ThemeOptionNames.AccentStrengthStandard;
    }

    public static Font CreateFont(float baseSize, FontStyle style = FontStyle.Regular)
    {
        var fontSize = AdjustFontSize(baseSize);
        var familyName = ResolveFontFamilyName();
        return new Font(familyName, fontSize, style, GraphicsUnit.Point);
    }

    /// <summary>
    /// 中文注释：创建开发者向等宽字体（地址栏/状态栏数值芯片用），优先 Cascadia，缺失时回退 Consolas。
    /// </summary>
    public static Font CreateMonoFont(float baseSize, FontStyle style = FontStyle.Regular)
    {
        string[] preferred = ["Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Consolas"];
        var familyName = preferred.FirstOrDefault(InstalledFontFamilies.Contains) ?? "Consolas";
        return new Font(familyName, AdjustFontSize(baseSize), style, GraphicsUnit.Point);
    }

    /// <summary>
    /// 中文注释：创建矢量图标字体（Segoe Fluent/MDL2）；系统缺失图标字体时返回 null，调用方需自行兜底。
    /// </summary>
    public static Font? CreateIconFont(float baseSize)
    {
        return IconFontFamilyName is null
            ? null
            : new Font(IconFontFamilyName, AdjustFontSize(baseSize), FontStyle.Regular, GraphicsUnit.Point);
    }

    private static string? ResolveIconFontFamilyName()
    {
        // 中文注释：优先 Win11 自带的 Segoe Fluent Icons，Win10 回退 Segoe MDL2 Assets，都缺失时返回 null 走字符图标兜底。
        if (InstalledFontFamilies.Contains("Segoe Fluent Icons"))
        {
            return "Segoe Fluent Icons";
        }

        if (InstalledFontFamilies.Contains("Segoe MDL2 Assets"))
        {
            return "Segoe MDL2 Assets";
        }

        return null;
    }

    public static string? GetIconGlyph(string name)
    {
        return name switch
        {
            AppIconNames.Back => "\uE72B",
            AppIconNames.Forward => "\uE72A",
            AppIconNames.Refresh => "\uE72C",
            AppIconNames.NewTab => "\uE710",
            AppIconNames.CloseTab => "\uE711",
            AppIconNames.Downloads => "\uE896",
            AppIconNames.History => "\uE81C",
            AppIconNames.Bookmark => "\uE734",
            AppIconNames.BookmarksBarCollapse => "\uE70D",
            AppIconNames.BookmarksBarExpand => "\uE76C",
            AppIconNames.Settings => "\uE713",
            AppIconNames.PluginReload => "\uE895",
            AppIconNames.DevTools => "\uEC7A",
            AppIconNames.CommandPalette => "\uE756",
            AppIconNames.RecentTabs => "\uE823",
            AppIconNames.Sessions => "\uE700",
            AppIconNames.Workspace => "\uE8A9",
            AppIconNames.WindowMinimize => "\uE921",
            AppIconNames.WindowMaximize => "\uE922",
            AppIconNames.WindowRestore => "\uE923",
            AppIconNames.WindowClose => "\uE8BB",
            _ => null
        };
    }

    public static Image? GetIcon(string name, int size, Color color)
    {
        if (IconFontFamilyName is null || size <= 0)
        {
            return null;
        }

        var glyph = GetIconGlyph(name);
        if (glyph is null)
        {
            return null;
        }

        var cacheKey = (glyph, size, color.ToArgb());
        if (IconImageCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // 中文注释：字体字形渲染成带透明通道的位图并缓存，ToolStripItem 用 Image 即可与中文文字混排。
        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var iconFont = new Font(IconFontFamilyName, Math.Max(6F, size - 2F), FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        })
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            graphics.DrawString(glyph, iconFont, brush, new RectangleF(-1F, 0F, size + 2F, size), format);
        }

        IconImageCache[cacheKey] = bitmap;
        return bitmap;
    }

    private static Color ResolveAccentBase()
    {
        // 中文注释：强调色改用更现代的饱和度：浅色模式压暗保对比，深色模式提亮保清晰。
        return _accentPreset switch
        {
            ThemeOptionNames.AccentGreen => _isDark ? Color.FromArgb(74, 210, 149) : Color.FromArgb(17, 132, 92),
            ThemeOptionNames.AccentOrange => _isDark ? Color.FromArgb(245, 168, 82) : Color.FromArgb(196, 106, 12),
            ThemeOptionNames.AccentRose => _isDark ? Color.FromArgb(240, 132, 180) : Color.FromArgb(186, 62, 122),
            _ => _isDark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(0, 95, 184)
        };
    }

    private static string ResolveFontFamilyName()
    {
        string[] preferred = _fontPreset switch
        {
            ThemeOptionNames.FontDengXian => ["DengXian", "Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI"],
            ThemeOptionNames.FontSong => ["SimSun", "NSimSun", "Microsoft YaHei UI", "Segoe UI"],
            _ => ["Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI Variable Text", "Segoe UI"]
        };

        foreach (var familyName in preferred)
        {
            if (InstalledFontFamilies.Contains(familyName))
            {
                return familyName;
            }
        }

        return "Segoe UI";
    }

    public static Color Mix(Color source, Color target, float ratio)
    {
        return Blend(source, target, ratio);
    }

    private static float ResolveMutedRatio(bool isDark)
    {
        return _accentStrength switch
        {
            "Soft" => isDark ? 0.68F : 0.84F,
            "Vivid" => isDark ? 0.46F : 0.68F,
            _ => isDark ? 0.58F : 0.78F
        };
    }

    private static float ResolveHoverRatio(bool isDark)
    {
        return _accentStrength switch
        {
            "Soft" => isDark ? 0.56F : 0.76F,
            "Vivid" => isDark ? 0.32F : 0.58F,
            _ => isDark ? 0.46F : 0.68F
        };
    }

    private static Color Blend(Color source, Color target, float ratio)
    {
        ratio = Math.Clamp(ratio, 0F, 1F);
        var inverse = 1F - ratio;
        var red = (int)Math.Round((source.R * inverse) + (target.R * ratio));
        var green = (int)Math.Round((source.G * inverse) + (target.G * ratio));
        var blue = (int)Math.Round((source.B * inverse) + (target.B * ratio));
        return Color.FromArgb(red, green, blue);
    }

    public static float AdjustFontSize(float baseSize)
    {
        if (!_isCompact)
        {
            return baseSize;
        }

        return Math.Max(8F, baseSize - 0.4F);
    }

    public static void ApplyPrimaryButton(Button button, bool emphasize = false)
    {
        // 中文注释：改为 AutoSize 并按当前字体计算最小高度，避免高 DPI / 大字体下文字被截断。
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Font = CreateFont(9F);
        var padding = _isCompact ? new Padding(10, 5, 10, 5) : new Padding(12, 6, 12, 6);
        button.Padding = padding;
        var textHeight = TextRenderer.MeasureText("中Ay", button.Font, Size.Empty, TextFormatFlags.SingleLine).Height;
        var minHeight = textHeight + padding.Vertical + (_isCompact ? 6 : 8);
        button.MinimumSize = new Size(_isCompact ? 76 : 84, minHeight);
        button.FlatStyle = FlatStyle.Flat;
        // 中文注释：Fluent 风扁平按钮：强调按钮纯色无描边，普通按钮中性底 + 细边。
        button.FlatAppearance.BorderSize = emphasize ? 0 : 1;
        button.FlatAppearance.BorderColor = PanelBorder;
        button.FlatAppearance.MouseOverBackColor = emphasize
            ? (_isDark ? Blend(Accent, Color.White, 0.10F) : Blend(Accent, Color.Black, 0.08F))
            : (_isDark ? Blend(SurfaceAlt, Color.White, 0.06F) : Blend(SurfaceAlt, Color.Black, 0.03F));
        button.FlatAppearance.MouseDownBackColor = emphasize
            ? (_isDark ? Blend(Accent, Color.Black, 0.16F) : Blend(Accent, Color.Black, 0.16F))
            : (_isDark ? Blend(SurfaceAlt, Color.White, 0.10F) : Blend(SurfaceAlt, Color.Black, 0.06F));
        button.BackColor = emphasize ? Accent : SurfaceAlt;
        button.ForeColor = emphasize ? Color.White : TextPrimary;
        button.Margin = new Padding(5, 4, 5, 4);
    }

    public static void ApplyTextInput(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = InputBackground;
        textBox.ForeColor = TextPrimary;
        textBox.Font = CreateFont(9.3F);
        textBox.Margin = new Padding(0, 4, 0, 4);
    }

    public static void ApplyNumericInput(NumericUpDown numeric)
    {
        numeric.BorderStyle = BorderStyle.FixedSingle;
        numeric.BackColor = InputBackground;
        numeric.ForeColor = TextPrimary;
        numeric.Font = CreateFont(9.3F);
        numeric.Margin = new Padding(0, 4, 0, 4);
    }

    public static void ApplyComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = InputBackground;
        comboBox.ForeColor = TextPrimary;
        comboBox.Font = CreateFont(9.3F);
        comboBox.Margin = new Padding(0, 4, 0, 4);
    }

    public static void ApplyListBox(ListBox listBox)
    {
        listBox.BackColor = InputBackground;
        listBox.ForeColor = TextPrimary;
        listBox.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void ApplyTreeView(TreeView treeView)
    {
        treeView.BackColor = InputBackground;
        treeView.ForeColor = TextPrimary;
        treeView.LineColor = PanelBorder;
        treeView.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void ApplyDataGrid(DataGridView grid)
    {
        // 中文注释：表格去蓝灰调：行/表头/网格线全部走中性色板。
        var rowBackColor = InputBackground;
        var alternateRowBackColor = _isDark
            ? Blend(rowBackColor, Color.White, 0.03F)
            : Blend(rowBackColor, SurfaceAlt, 0.55F);
        var headerBackColor = SurfaceAlt;
        var selectionBackColor = _isDark ? Blend(Accent, Surface, 0.62F) : Blend(Accent, Color.White, 0.84F);
        var selectionForeColor = TextPrimary;

        grid.BackgroundColor = rowBackColor;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.GridColor = PanelBorder;
        grid.RowTemplate.Height = _isCompact ? 26 : 30;
        grid.ColumnHeadersHeight = _isCompact ? 32 : 36;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.BackColor = rowBackColor;
        grid.AlternatingRowsDefaultCellStyle.BackColor = alternateRowBackColor;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = selectionBackColor;
        grid.DefaultCellStyle.SelectionForeColor = selectionForeColor;
        grid.DefaultCellStyle.Padding = _isCompact ? new Padding(4, 1, 4, 1) : new Padding(6, 2, 6, 2);
        grid.ColumnHeadersDefaultCellStyle.BackColor = headerBackColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = selectionBackColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = selectionForeColor;
        grid.ColumnHeadersDefaultCellStyle.Padding = _isCompact ? new Padding(4, 3, 4, 3) : new Padding(6, 4, 6, 4);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.EnableHeadersVisualStyles = false;
        EnableDoubleBuffer(grid);
    }

    public static void ApplyContextMenu(ContextMenuStrip menu)
    {
        menu.BackColor = _isDark ? Color.FromArgb(38, 42, 49) : Color.FromArgb(250, 251, 253);
        menu.ForeColor = TextPrimary;
        menu.Font = CreateFont(9F);
        menu.ShowImageMargin = false;
        menu.Padding = new Padding(4);
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new AccentMenuRenderer();
        StyleContextMenuItems(menu.Items);
    }

    private static void StyleContextMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = TextPrimary;
            item.Font = CreateFont(9F, item.Font.Bold ? FontStyle.Bold : FontStyle.Regular);
            if (item is ToolStripSeparator separator)
            {
                separator.Margin = new Padding(6, 5, 6, 5);
                continue;
            }

            item.Padding = _isCompact ? new Padding(9, 6, 9, 6) : new Padding(11, 7, 11, 7);
            item.Margin = new Padding(2);
            if (item is ToolStripMenuItem menuItem && menuItem.DropDownItems.Count > 0)
            {
                StyleContextMenuItems(menuItem.DropDownItems);
            }
        }
    }

    private static void EnableDoubleBuffer(DataGridView grid)
    {
        try
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(grid, true, null);
        }
        catch
        {
            // Ignore reflection failure; style still applies without double buffering.
        }
    }

    private sealed class AccentMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => AccentHover;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color ToolStripDropDownBackground => _isDark
            ? Color.FromArgb(38, 42, 49)
            : Color.FromArgb(250, 251, 253);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    }

    private sealed class AccentMenuRenderer : ToolStripProfessionalRenderer
    {
        public AccentMenuRenderer() : base(new AccentMenuColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // 中文注释：菜单外框改中性细边，不再用强调色描边。
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var path = BuildRoundedRect(rect, 8);
            using var borderPen = new Pen(PanelBorder, 1F);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(borderPen, path);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            var left = 10;
            var right = e.Item.Width - 10;
            if (right > left)
            {
                using var pen = new Pen(PanelBorder, 1F);
                e.Graphics.DrawLine(pen, left, y, right, y);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var bounds = e.ImageRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            bounds.Inflate(2, 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fillBrush = new SolidBrush(Accent);
            using var path = BuildRoundedRect(bounds, 4);
            e.Graphics.FillPath(fillBrush, path);
            using var checkPen = new Pen(Color.White, 1.8F);
            var cx = bounds.Left + bounds.Width / 2;
            var cy = bounds.Top + bounds.Height / 2;
            e.Graphics.DrawLine(checkPen, cx - 4, cy, cx - 1, cy + 3);
            e.Graphics.DrawLine(checkPen, cx - 1, cy + 3, cx + 4, cy - 3);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = TextSecondary;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            base.OnRenderItemImage(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is not ToolStripMenuItem menuItem)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            bounds.Inflate(-1, -1);
            if (bounds.Width <= 4 || bounds.Height <= 4)
            {
                return;
            }

            var isActive = menuItem.Selected || menuItem.Pressed;
            // 中文注释：菜单项高亮改淡强调色底无描边，更轻盈。
            var backColor = isActive
                ? (_isDark
                    ? Mix(Accent, SurfaceAlt, 0.70F)
                    : Mix(Accent, Color.White, 0.86F))
                : Color.Transparent;
            using var path = BuildRoundedRect(bounds, 6);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (isActive)
            {
                using var brush = new SolidBrush(backColor);
                e.Graphics.FillPath(brush, path);
            }
        }
    }

    public static GraphicsPath BuildRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(rect.Left, rect.Top, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

