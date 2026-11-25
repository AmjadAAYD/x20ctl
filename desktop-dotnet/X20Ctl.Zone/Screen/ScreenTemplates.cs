namespace X20Ctl.Zone.Screen;

public enum ScreenTemplateKind
{
    GamertagBadge,
    RetroGrid,
    AcousticWave,
    MinimalEmblem,
}

/// <summary>
/// Pure cross-platform 160x80 RGBA animated template generator for White flash mode upload.
/// Uses an embedded 5x7 crisp bitmap font so zero Windows GDI/WPF calls are needed.
/// </summary>
public static class ScreenTemplates
{
    public const int Width = 160;
    public const int Height = 80;
    public const int Stride = Width * 4;

    public static byte[][] Generate(ScreenTemplateKind kind, string? label, uint accentRgb, int frameCount = 16)
    {
        int count = Math.Clamp(frameCount, 1, 32);
        string cleanLabel = SanitizeLabel(label);
        byte ar = (byte)((accentRgb >> 16) & 0xFF);
        byte ag = (byte)((accentRgb >> 8) & 0xFF);
        byte ab = (byte)(accentRgb & 0xFF);

        var frames = new byte[count][];
        for (int f = 0; f < count; f++)
        {
            double phase = (double)f / count * Math.PI * 2.0;
            var px = new byte[Width * Height * 4];
            RenderBackground(px, ar, ag, ab);
            switch (kind)
            {
                case ScreenTemplateKind.GamertagBadge:
                    RenderGamertagBadge(px, cleanLabel, ar, ag, ab, phase);
                    break;
                case ScreenTemplateKind.RetroGrid:
                    RenderRetroGrid(px, cleanLabel, ar, ag, ab, phase);
                    break;
                case ScreenTemplateKind.AcousticWave:
                    RenderAcousticWave(px, cleanLabel, ar, ag, ab, phase);
                    break;
                case ScreenTemplateKind.MinimalEmblem:
                    RenderMinimalEmblem(px, cleanLabel, ar, ag, ab, phase);
                    break;
            }
            frames[f] = px;
        }
        return frames;
    }

    static string SanitizeLabel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "X20 PRO";
        var clean = new string(text.Trim().ToUpperInvariant().Where(c => Font5x7.ContainsKey(c)).Take(14).ToArray());
        return clean.Length == 0 ? "X20 PRO" : clean;
    }

    static void RenderBackground(byte[] px, byte ar, byte ag, byte ab)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = (y * Width + x) * 4;
                double v = (double)y / Height;
                px[i] = (byte)Math.Clamp((int)(6 + ar * 0.06 * (1 - v)), 0, 255);
                px[i + 1] = (byte)Math.Clamp((int)(8 + ag * 0.06 * (1 - v)), 0, 255);
                px[i + 2] = (byte)Math.Clamp((int)(14 + ab * 0.08 * (1 - v)), 0, 255);
                px[i + 3] = 255;
            }
        }
    }

    static void RenderGamertagBadge(byte[] px, string label, byte ar, byte ag, byte ab, double phase)
    {
        int scan = (int)Math.Round((Math.Sin(phase) * 0.5 + 0.5) * (Width - 20));
        // Outer accent border
        DrawRectOutline(px, 4, 4, Width - 8, Height - 8, ar, ag, ab);
        // Sliding highlight bar top & bottom
        FillRect(px, 8 + scan, 4, 16, 2, 245, 250, 255);
        FillRect(px, Width - 24 - scan, Height - 6, 16, 2, 245, 250, 255);
        DrawCenteredText(px, label, 28, 2, 248, 250, 255);
        DrawCenteredText(px, "READY", 54, 1, ar, ag, ab);
    }

    static void RenderRetroGrid(byte[] px, string label, byte ar, byte ag, byte ab, double phase)
    {
        int offset = (int)(phase / (Math.PI * 2) * 16) % 16;
        for (int y = 40; y < Height; y++)
        {
            double depth = (y - 39) / 40.0;
            bool hLine = ((y + offset) % 8) == 0;
            for (int x = 0; x < Width; x++)
            {
                double worldX = (x - 80) / Math.Max(0.15, depth);
                bool vLine = Math.Abs(((int)Math.Round(worldX) % 24)) < 2;
                if (hLine || vLine)
                    BlendPixel(px, x, y, ar, ag, ab, 0.72);
            }
        }
        // Horizon glow & centered tag
        FillRect(px, 0, 39, Width, 2, ar, ag, ab);
        DrawCenteredText(px, label, 14, 2, 248, 252, 255);
    }

    static void RenderAcousticWave(byte[] px, string label, byte ar, byte ag, byte ab, double phase)
    {
        const int bars = 20;
        int barW = 5, gap = 2, startX = 11;
        for (int b = 0; b < bars; b++)
        {
            double wave = 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(phase + b * 0.55));
            int h = Math.Clamp((int)Math.Round(wave * 38), 4, 38);
            int x = startX + b * (barW + gap);
            FillRect(px, x, 68 - h, barW, h, ar, ag, ab);
            FillRect(px, x, 68 - h, barW, 2, 245, 250, 255);
        }
        DrawCenteredText(px, label, 10, 2, 248, 252, 255);
    }

    static void RenderMinimalEmblem(byte[] px, string label, byte ar, byte ag, byte ab, double phase)
    {
        double pulseR = 22.0 + 3.0 * Math.Sin(phase);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                double dx = x - 34, dy = y - 40;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (Math.Abs(d - pulseR) < 1.8)
                    BlendPixel(px, x, y, ar, ag, ab, 0.95);
                else if (d < 8)
                    BlendPixel(px, x, y, 245, 250, 255, 0.9);
            }
        }
        DrawText(px, label, 68, 30, 2, 248, 252, 255);
        DrawText(px, "X20CTL", 68, 50, 1, ar, ag, ab);
    }

    static void DrawCenteredText(byte[] px, string text, int y, int scale, byte r, byte g, byte b)
    {
        int totalW = text.Length * (6 * scale) - scale;
        int startX = Math.Max(4, (Width - totalW) / 2);
        DrawText(px, text, startX, y, scale, r, g, b);
    }

    static void DrawText(byte[] px, string text, int x, int y, int scale, byte r, byte g, byte b)
    {
        int cx = x;
        foreach (char ch in text)
        {
            if (Font5x7.TryGetValue(ch, out var rows))
            {
                for (int row = 0; row < 7; row++)
                {
                    byte mask = rows[row];
                    for (int col = 0; col < 5; col++)
                    {
                        if ((mask & (1 << (4 - col))) != 0)
                            FillRect(px, cx + col * scale, y + row * scale, scale, scale, r, g, b);
                    }
                }
            }
            cx += 6 * scale;
        }
    }

    static void DrawRectOutline(byte[] px, int x, int y, int w, int h, byte r, byte g, byte b)
    {
        FillRect(px, x, y, w, 1, r, g, b);
        FillRect(px, x, y + h - 1, w, 1, r, g, b);
        FillRect(px, x, y, 1, h, r, g, b);
        FillRect(px, x + w - 1, y, 1, h, r, g, b);
    }

    static void FillRect(byte[] px, int x, int y, int w, int h, byte r, byte g, byte b)
    {
        for (int yy = Math.Max(0, y); yy < Math.Min(Height, y + h); yy++)
        {
            for (int xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
            {
                int i = (yy * Width + xx) * 4;
                px[i] = r; px[i + 1] = g; px[i + 2] = b; px[i + 3] = 255;
            }
        }
    }

    static void BlendPixel(byte[] px, int x, int y, byte r, byte g, byte b, double alpha)
    {
        if (x is < 0 or >= Width || y is < 0 or >= Height) return;
        int i = (y * Width + x) * 4;
        px[i] = (byte)Math.Clamp((int)(px[i] * (1 - alpha) + r * alpha), 0, 255);
        px[i + 1] = (byte)Math.Clamp((int)(px[i + 1] * (1 - alpha) + g * alpha), 0, 255);
        px[i + 2] = (byte)Math.Clamp((int)(px[i + 2] * (1 - alpha) + b * alpha), 0, 255);
        px[i + 3] = 255;
    }

    static readonly Dictionary<char, byte[]> Font5x7 = new()
    {
        ['A'] = [0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['B'] = [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E],
        ['C'] = [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
        ['D'] = [0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C],
        ['E'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F],
        ['F'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
        ['G'] = [0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F],
        ['H'] = [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['I'] = [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['J'] = [0x07, 0x02, 0x02, 0x02, 0x12, 0x12, 0x0C],
        ['K'] = [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11],
        ['L'] = [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
        ['M'] = [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11],
        ['N'] = [0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11],
        ['O'] = [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['P'] = [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
        ['Q'] = [0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D],
        ['R'] = [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
        ['S'] = [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E],
        ['T'] = [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
        ['U'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['V'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04],
        ['W'] = [0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11],
        ['X'] = [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
        ['Y'] = [0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04],
        ['Z'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
        ['0'] = [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
        ['1'] = [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['2'] = [0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F],
        ['3'] = [0x1F, 0x02, 0x04, 0x06, 0x01, 0x11, 0x0E],
        ['4'] = [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
        ['5'] = [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
        ['6'] = [0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E],
        ['7'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08],
        ['8'] = [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
        ['9'] = [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C],
        [' '] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
        ['-'] = [0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00],
        ['.'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x06],
        [':'] = [0x00, 0x06, 0x06, 0x00, 0x06, 0x06, 0x00],
    };
}
