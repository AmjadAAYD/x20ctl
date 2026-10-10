namespace X20Ctl.Zone.Tests;
public class WindowPresentationTests
{
    [Theory][InlineData(96)][InlineData(120)][InlineData(144)]
    public void WorkAreaAndDpiAreIndependentOfMonitorOrigin(int dpi)
    {
        var monitor=new PixelBounds(-2560,-200,2560,1440);var work=new PixelBounds(-2520,-160,2520,1400);
        var result=WindowPresentation.Maximize(monitor,work,dpi);Assert.Equal(40,result.X);Assert.Equal(40,result.Y);Assert.Equal(2520,result.Width);Assert.Equal(1400,result.Height);
        Assert.Equal((int)Math.Ceiling(1040*dpi/96d),result.MinWidth);var restore=WindowPresentation.Restore(work,dpi);Assert.True(restore.Left>=work.Left && restore.Top>=work.Top);Assert.True(restore.Left+restore.Width<=work.Left+work.Width);
    }
    [Fact] public void StrengthControlsSpeedDistanceOpacityAndZeroIsStill()
    {
        var off=VibrationVisualProfile.For(0);Assert.Equal(new VibrationVisualProfile(0,0,0),off);
        var levels=new[]{25,50,75,100}.Select(VibrationVisualProfile.For).ToArray();for(int i=1;i<levels.Length;i++){Assert.True(levels[i].CycleSeconds<levels[i-1].CycleSeconds);Assert.True(levels[i].Spread>levels[i-1].Spread);Assert.True(levels[i].Opacity>levels[i-1].Opacity);}
    }
}
