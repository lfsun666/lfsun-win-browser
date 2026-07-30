namespace Lfsun.Win.Browser.Core;

public static class ShortcutParser
{
    public static bool TryParse(string raw, out Keys keyData)
    {
        keyData = Keys.None;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var tokens = raw
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .ToArray();
        if (tokens.Length == 0)
        {
            return false;
        }

        Keys modifiers = Keys.None;
        Keys key = Keys.None;
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= Keys.Control;
                    continue;
                case "SHIFT":
                    modifiers |= Keys.Shift;
                    continue;
                case "ALT":
                    modifiers |= Keys.Alt;
                    continue;
            }

            if (!Enum.TryParse<Keys>(token, true, out key) || key == Keys.None)
            {
                return false;
            }
        }

        if (key == Keys.None)
        {
            return false;
        }

        keyData = modifiers | key;
        return true;
    }

    public static string Normalize(string raw)
    {
        return TryParse(raw, out var keyData) ? ToDisplayString(keyData) : raw.Trim();
    }

    public static string ToDisplayString(Keys keyData)
    {
        var parts = new List<string>();
        if (keyData.HasFlag(Keys.Control))
        {
            parts.Add("Ctrl");
        }
        if (keyData.HasFlag(Keys.Shift))
        {
            parts.Add("Shift");
        }
        if (keyData.HasFlag(Keys.Alt))
        {
            parts.Add("Alt");
        }

        var key = keyData & ~Keys.Control & ~Keys.Shift & ~Keys.Alt;
        parts.Add(key.ToString().ToUpperInvariant());
        return string.Join("+", parts);
    }

    private static string NormalizeToken(string token)
    {
        return token.Trim().Replace(" ", string.Empty).ToUpperInvariant();
    }
}

