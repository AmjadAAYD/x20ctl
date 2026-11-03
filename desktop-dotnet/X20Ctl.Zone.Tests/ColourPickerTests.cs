using X20Ctl.Zone.Lighting;

public class ColourPickerTests
{
    [Theory]
    [InlineData(0,1,1,0xff0000)]
    [InlineData(120,1,1,0x00ff00)]
    [InlineData(240,1,1,0x0000ff)]
    [InlineData(360,1,1,0xff0000)]
    [InlineData(200,0,1,0xffffff)]
    [InlineData(200,1,0,0)]
    public void PickerMapsHueAndSquareToRgb(double h,double s,double v,int rgb) =>
        Assert.Equal(rgb, ColourPalette.FromHsv(h,s,v));

    [Fact]
    public void IncludesAll48NamedPresets()
    {
        Assert.Equal(48, ColourPalette.Presets.Count);
        Assert.Equal(48, ColourPalette.Presets.Select(p=>p.Name).Distinct().Count());
        Assert.Contains(ColourPalette.Presets,p=>p.Name=="Orange" && p.Rgb==0xff8000);
        Assert.Contains(ColourPalette.Presets,p=>p.Name=="Silver" && p.Rgb==0xc0c0c0);
    }
}
