namespace X20Ctl.Zone;
public sealed record PixelBounds(int Left,int Top,int Width,int Height);
public sealed record MaximizeMetrics(int X,int Y,int Width,int Height,int MinWidth,int MinHeight);
/// <summary>Window presentation geometry only; device protocols are unrelated.</summary>
public static class WindowPresentation
{
    public static MaximizeMetrics Maximize(PixelBounds monitor,PixelBounds work,double dpi)
    {
        if(dpi<=0 || work.Width<=0 || work.Height<=0)throw new ArgumentOutOfRangeException(nameof(dpi));
        return new(work.Left-monitor.Left,work.Top-monitor.Top,work.Width,work.Height,(int)Math.Ceiling(1040*dpi/96),(int)Math.Ceiling(700*dpi/96));
    }
    public static PixelBounds Restore(PixelBounds work,double dpi)
    {
        int width=Math.Min(work.Width,(int)Math.Round(1220*dpi/96)),height=Math.Min(work.Height,(int)Math.Round(800*dpi/96));
        return new(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,width,height);
    }
}
public sealed record VibrationVisualProfile(double CycleSeconds,double Spread,double Opacity)
{
    public static VibrationVisualProfile For(int percent)
    {
        if(percent is <0 or >100)throw new ArgumentOutOfRangeException(nameof(percent));
        if(percent==0)return new(0,0,0);double strength=percent/100d;return new(2.3-1.65*strength,6+26*strength,.12+.46*strength);
    }
}
