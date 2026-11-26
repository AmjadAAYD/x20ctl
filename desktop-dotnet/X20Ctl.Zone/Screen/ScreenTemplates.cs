namespace X20Ctl.Zone.Screen;

public enum ScreenTemplateKind
{
    GamertagBadge,
    RetroGrid,
    AcousticWave,
    MinimalEmblem,
    StarfieldWarp,
    PlasmaRing,
}

/// <summary>
/// Pure cross-platform 160x80 RGBA animated template generator for White flash mode upload.
/// Uses pure #000000 AMOLED backgrounds, anti-aliased pixel blending, and crisp bitmap glyphs.
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
            double progress = (double)f / count;
            var px = new byte[Width * Height * 4];
            RenderPureBlack(px);
            switch (kind)
            {
                case ScreenTemplateKind.GamertagBadge:
                    RenderObsidianTag(px, cleanLabel, ar, ag, ab, phase, progress);
                    break;
                case ScreenTemplateKind.RetroGrid:
                    RenderCyberHorizon(px, cleanLabel, ar, ag, ab, progress);
                    break;
                case ScreenTemplateKind.AcousticWave:
                    RenderPulseWave(px, cleanLabel, ar, ag, ab, phase);
                    break;
                case ScreenTemplateKind.MinimalEmblem:
                    RenderMinimalTelemetry(px, cleanLabel, ar, ag, ab, phase, progress);
                    break;
                case ScreenTemplateKind.StarfieldWarp:
                    RenderStarfieldWarp(px, cleanLabel, ar, ag, ab, progress);
                    break;
                case ScreenTemplateKind.PlasmaRing:
                    RenderPlasmaRing(px, cleanLabel, ar, ag, ab, phase);
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

    static void RenderPureBlack(byte[] px)
    {
        for (int i = 3; i < px.Length; i += 4)
            px[i] = 255;
    }

    static void RenderObsidianTag(byte[] px, string label, byte ar, byte ag, byte ab, double phase, double progress)
    {
        // Corner tech brackets in accent color
        int b = 8, t = 2;
        FillRect(px, 6, 6, b, t, ar, ag, ab);
        FillRect(px, 6, 6, t, b, ar, ag, ab);
        FillRect(px, Width - 6 - b, 6, b, t, ar, ag, ab);
        FillRect(px, Width - 6 - t, 6, t, b, ar, ag, ab);
        FillRect(px, 6, Height - 8, b, t, ar, ag, ab);
        FillRect(px, 6, Height - 6 - b, t, b, ar, ag, ab);
        FillRect(px, Width - 6 - b, Height - 8, b, t, ar, ag, ab);
        FillRect(px, Width - 6 - t, Height - 6 - b, t, b, ar, ag, ab);

        // Smooth ambient glow behind text center
        for (int y = 18; y < 56; y++)
        {
            for (int x = 16; x < Width - 16; x++)
            {
                double dx = (x - 80) / 58.0;
                double dy = (y - 36) / 18.0;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1.0)
                    BlendPixel(px, x, y, ar, ag, ab, (1.0 - d) * 0.22 * (0.85 + 0.15 * Math.Sin(phase)));
            }
        }

        // Seamless wrapping underline beam
        int barW = 92;
        int bx = (Width - barW) / 2;
        FillRect(px, bx, 56, barW, 1, (byte)(ar / 4), (byte)(ag / 4), (byte)(ab / 4));
        int pulsePos = (int)Math.Round(progress * barW);
        for (int i = 0; i < 24; i++)
        {
            int pxX = bx + (pulsePos + i) % barW;
            double a = Math.Sin(i / 23.0 * Math.PI);
            BlendPixel(px, pxX, 55, ar, ag, ab, a * 0.9);
            BlendPixel(px, pxX, 56, 250, 252, 255, a);
        }

        int scale = label.Length <= 8 ? 2 : 1;
        int yText = scale == 2 ? 27 : 31;
        DrawCenteredText(px, label, yText + 1, scale, (byte)(ar / 3), (byte)(ag / 3), (byte)(ab / 3));
        DrawCenteredText(px, label, yText, scale, 250, 252, 255);
    }

    static void RenderCyberHorizon(byte[] px, string label, byte ar, byte ag, byte ab, double progress)
    {
        const int horizonY = 44;
        // Soft atmospheric radial glow above horizon
        for (int y = 0; y <= horizonY; y++)
        {
            double h = 1.0 - Math.Abs(horizonY - y) / 36.0;
            if (h <= 0) continue;
            for (int x = 12; x < Width - 12; x++)
            {
                double edge = 1.0 - Math.Abs(x - 80) / 70.0;
                if (edge > 0)
                    BlendPixel(px, x, y, ar, ag, ab, h * h * edge * 0.32);
            }
        }

        // Smooth perspective ground lines
        for (int row = 1; row <= 6; row++)
        {
            double z = (row - progress) / 6.0;
            if (z <= 0.04) z += 1.0;
            int y = horizonY + (int)Math.Round(Math.Pow(z, 1.75) * (Height - horizonY - 2));
            if (y > horizonY && y < Height)
            {
                double alpha = 0.25 + 0.65 * z;
                for (int x = 0; x < Width; x++)
                    BlendPixel(px, x, y, ar, ag, ab, alpha * 0.78);
            }
        }
        for (int ray = -4; ray <= 4; ray++)
        {
            double slope = ray * 0.62;
            for (int y = horizonY + 1; y < Height; y++)
            {
                double t = (double)(y - horizonY) / (Height - horizonY);
                int x = 80 + (int)Math.Round(slope * (y - horizonY) * 1.45);
                BlendPixel(px, x, y, ar, ag, ab, 0.22 + 0.55 * t);
                BlendPixel(px, x + 1, y, ar, ag, ab, 0.12 + 0.28 * t);
            }
        }

        // Crisp luminous horizon beam
        for (int x = 0; x < Width; x++)
        {
            double fade = 1.0 - Math.Pow(Math.Abs(x - 80) / 82.0, 1.6);
            if (fade > 0)
            {
                BlendPixel(px, x, horizonY, 245, 250, 255, fade);
                BlendPixel(px, x, horizonY - 1, ar, ag, ab, fade * 0.75);
                BlendPixel(px, x, horizonY + 1, ar, ag, ab, fade * 0.75);
            }
        }

        int scale = label.Length <= 9 ? 2 : 1;
        DrawCenteredText(px, label, scale == 2 ? 14 : 18, scale, 250, 252, 255);
    }

    static void RenderPulseWave(byte[] px, string label, byte ar, byte ag, byte ab, double phase)
    {
        for (int x = 6; x < Width - 6; x++)
        {
            double nx = (x - 6) / (double)(Width - 12);
            double env = Math.Sin(nx * Math.PI);
            double y1 = 52.0 - env * (14.0 * Math.Sin(nx * Math.PI * 3.0 - phase) + 5.0 * Math.Cos(nx * Math.PI * 6.0 + phase));
            double y2 = 52.0 + env * (9.0 * Math.Sin(nx * Math.PI * 2.5 + phase));

            DrawAntiAliasedDot(px, x, y2, (byte)(ar / 2), (byte)(ag / 2), (byte)(ab / 2), 0.7);
            DrawAntiAliasedDot(px, x, y1, ar, ag, ab, 1.0);
            DrawAntiAliasedDot(px, x, y1 - 0.6, 250, 252, 255, 0.75);
        }
        int scale = label.Length <= 10 ? 2 : 1;
        DrawCenteredText(px, label, 10, scale, 250, 252, 255);
    }

    static void RenderMinimalTelemetry(byte[] px, string label, byte ar, byte ag, byte ab, double phase, double progress)
    {
        const double cx = 31.0, cy = 40.0, r = 18.0;
        for (int y = 16; y <= 64; y++)
        {
            for (int x = 8; x <= 56; x++)
            {
                double dx = x - cx, dy = y - cy;
                double d = Math.Sqrt(dx * dx + dy * dy);
                double ringDist = Math.Abs(d - r);
                if (ringDist < 1.6)
                {
                    double ang = Math.Atan2(dy, dx);
                    double rel = ((ang - phase) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2);
                    double arc = rel < 2.4 ? 1.0 - rel / 2.4 : 0.18;
                    BlendPixel(px, x, y, ar, ag, ab, (1.6 - ringDist) / 1.6 * arc);
                }
                else if (d < 5.5)
                {
                    double core = 0.7 + 0.3 * Math.Sin(phase);
                    BlendPixel(px, x, y, 250, 252, 255, (5.5 - d) / 5.5 * core);
                }
            }
        }

        int scale = label.Length <= 8 ? 2 : 1;
        DrawText(px, label, 64, scale == 2 ? 22 : 26, scale, 250, 252, 255);
        // Clean micro telemetry bars below label
        for (int b = 0; b < 6; b++)
        {
            int bx = 64 + b * 14;
            int bh = 4 + (int)Math.Round(6.0 * (0.5 + 0.5 * Math.Sin(phase + b * 0.85)));
            FillRect(px, bx, 56 - bh, 9, bh, ar, ag, ab);
            FillRect(px, bx, 56 - bh, 9, 2, 245, 250, 255);
        }
        _ = progress;
    }

    static void RenderStarfieldWarp(byte[] px, string label, byte ar, byte ag, byte ab, double progress)
    {
        const int starCount = 34;
        for (int s = 0; s < starCount; s++)
        {
            double angle = (s * 2.3999632) % (Math.PI * 2);
            double speed = 0.7 + (s % 3) * 0.35;
            double z = ((s / (double)starCount) + progress * speed) % 1.0;
            double dist = 4.0 + Math.Pow(z, 1.8) * 76.0;
            double tail = dist * 0.16 * (s % 2 == 0 ? 1.0 : 0.65);

            int steps = 5;
            for (int st = 0; st < steps; st++)
            {
                double rr = Math.Max(2.0, dist - st * (tail / steps));
                double sx = 80.0 + Math.Cos(angle) * rr * 1.15;
                double sy = 40.0 + Math.Sin(angle) * rr * 0.62;
                double alpha = Math.Clamp(z * (1.0 - st / (double)steps), 0.08, 1.0);
                if (st == 0) DrawAntiAliasedDot(px, sx, sy, 250, 252, 255, alpha);
                else DrawAntiAliasedDot(px, sx, sy, ar, ag, ab, alpha * 0.85);
            }
        }

        // Subtle dark vignette behind crisp label for readability
        for (int y = 26; y < 52; y++)
            for (int x = 22; x < Width - 22; x++)
                BlendPixel(px, x, y, 0, 0, 0, 0.55);

        int scale = label.Length <= 8 ? 2 : 1;
        DrawCenteredText(px, label, scale == 2 ? 32 : 36, scale, 250, 252, 255);
    }

    static void RenderPlasmaRing(byte[] px, string label, byte ar, byte ag, byte ab, double phase)
    {
        const double cx = 80.0, cy = 40.0;
        for (int y = 6; y < Height - 6; y++)
        {
            for (int x = 32; x < Width - 32; x++)
            {
                double dx = (x - cx) * 0.85, dy = y - cy;
                double r = Math.Sqrt(dx * dx + dy * dy);
                double ang = Math.Atan2(dy, dx);
                double waveR = 27.0 + 2.2 * Math.Sin(ang * 4.0 - phase);
                double d = Math.Abs(r - waveR);
                if (d < 2.2)
                {
                    double glow = (2.2 - d) / 2.2;
                    double seg = 0.5 + 0.5 * Math.Sin(ang * 2.0 + phase * 2.0);
                    BlendPixel(px, x, y, ar, ag, ab, glow * (0.45 + 0.55 * seg));
                    if (d < 0.85)
                        BlendPixel(px, x, y, 250, 252, 255, glow * seg * 0.75);
                }
            }
        }

        for (int k = 0; k < 2; k++)
        {
            double a = phase + k * Math.PI;
            DrawAntiAliasedDot(px, cx + Math.Cos(a) * 32.0, cy + Math.Sin(a) * 27.0, 255, 255, 255, 1.0);
        }

        int scale = label.Length <= 8 ? 2 : 1;
        DrawCenteredText(px, label, scale == 2 ? 33 : 36, scale, 250, 252, 255);
    }

    static void DrawAntiAliasedDot(byte[] px, double x, double y, byte r, byte g, byte b, double alpha)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        BlendPixel(px, x0, y0, r, g, b, alpha * (1 - fx) * (1 - fy));
        BlendPixel(px, x0 + 1, y0, r, g, b, alpha * fx * (1 - fy));
        BlendPixel(px, x0, y0 + 1, r, g, b, alpha * (1 - fx) * fy);
        BlendPixel(px, x0 + 1, y0 + 1, r, g, b, alpha * fx * fy);
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
        if (x is < 0 or >= Width || y is < 0 or >= Height || alpha <= 0) return;
        double a = Math.Clamp(alpha, 0, 1);
        int i = (y * Width + x) * 4;
        px[i] = (byte)Math.Clamp((int)(px[i] * (1 - a) + r * a), 0, 255);
        px[i + 1] = (byte)Math.Clamp((int)(px[i + 1] * (1 - a) + g * a), 0, 255);
        px[i + 2] = (byte)Math.Clamp((int)(px[i + 2] * (1 - a) + b * a), 0, 255);
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
