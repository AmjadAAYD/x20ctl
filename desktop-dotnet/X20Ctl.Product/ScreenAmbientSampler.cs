using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Full-screen high-quality Ambient Screen Sync sampler.
/// Downsamples the entire primary monitor into a 32x18 buffer via GDI StretchBlt (HALFTONE)
/// in ~2 ms and extracts a saturation-weighted dominant colour with fast smoothing.
/// </summary>
internal static class ScreenAmbientSampler
{
    const int SampleWidth = 32;
    const int SampleHeight = 18;
    const int Halftone = 4;
    const int SrcCopy = 0x00CC0020;

    static (double R, double G, double B) smoothRgb = (30, 136, 229);

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    static extern int SetStretchBltMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr pt);

    [DllImport("gdi32.dll")]
    static extern bool StretchBlt(
        IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, int rop);

    public static LightingFrame Sample(LightingEffectSpec spec)
    {
        try
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return Fallback(spec);

            using var small = new System.Drawing.Bitmap(SampleWidth, SampleHeight, PixelFormat.Format32bppArgb);
            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return Fallback(spec);
            try
            {
                using var g = System.Drawing.Graphics.FromImage(small);
                IntPtr destDc = g.GetHdc();
                try
                {
                    SetStretchBltMode(destDc, Halftone);
                    SetBrushOrgEx(destDc, 0, 0, IntPtr.Zero);
                    StretchBlt(
                        destDc, 0, 0, SampleWidth, SampleHeight,
                        screenDc, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                        SrcCopy);
                }
                finally
                {
                    g.ReleaseHdc(destDc);
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }

            var data = small.LockBits(
                new System.Drawing.Rectangle(0, 0, SampleWidth, SampleHeight),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            double sumR = 0, sumG = 0, sumB = 0, totalWeight = 0;
            try
            {
                int byteCount = Math.Abs(data.Stride) * SampleHeight;
                byte[] pixels = new byte[byteCount];
                Marshal.Copy(data.Scan0, pixels, 0, byteCount);

                for (int y = 0; y < SampleHeight; y++)
                {
                    int row = y * data.Stride;
                    for (int x = 0; x < SampleWidth; x++)
                    {
                        int i = row + x * 4;
                        byte b = pixels[i];
                        byte gCh = pixels[i + 1];
                        byte r = pixels[i + 2];

                        byte max = Math.Max(r, Math.Max(gCh, b));
                        byte min = Math.Min(r, Math.Min(gCh, b));
                        double sat = max > 0 ? (max - min) / (double)max : 0.0;

                        // Weight vibrant colours higher and down-weight black letterbox/dark chrome pixels.
                        double lumaWeight = max < 14 ? 0.05 : 0.35 + 0.65 * (max / 255.0);
                        double weight = lumaWeight * (0.25 + sat * sat * 4.5);

                        sumR += r * weight;
                        sumG += gCh * weight;
                        sumB += b * weight;
                        totalWeight += weight;
                    }
                }
            }
            finally
            {
                small.UnlockBits(data);
            }

            if (totalWeight <= 0.001) return Fallback(spec);

            double targetR = sumR / totalWeight;
            double targetG = sumG / totalWeight;
            double targetB = sumB / totalWeight;

            // Fast exponential smoothing (75% new frame, 25% previous) for instant-feeling transitions without harsh jitter.
            smoothRgb = (
                smoothRgb.R * 0.25 + targetR * 0.75,
                smoothRgb.G * 0.25 + targetG * 0.75,
                smoothRgb.B * 0.25 + targetB * 0.75);

            return new LightingFrame(
                (byte)Math.Clamp(Math.Round(smoothRgb.R), 0, 255),
                (byte)Math.Clamp(Math.Round(smoothRgb.G), 0, 255),
                (byte)Math.Clamp(Math.Round(smoothRgb.B), 0, 255),
                spec.MaxBrightness);
        }
        catch
        {
            return Fallback(spec);
        }
    }

    static LightingFrame Fallback(LightingEffectSpec spec)
    {
        var (r, g, b) = LightingEffectSpec.ParseRgb(spec.Palette[0]);
        return new LightingFrame(r, g, b, spec.MaxBrightness);
    }
}
