using X20Ctl.Zone.Lighting;

public class LightingTests
{
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void OneApplyCombinesColourAndEffectPreservingOtherBytes(byte mode)
    {
        var original=Convert.FromHexString(Red);
        var actual=LightingRecord.WithSelection(original,0,128,255,mode);
        Assert.Equal(new byte[]{0,128,255},actual[..3]);
        Assert.Equal(mode,actual[6]);
        Assert.Equal(original[3..6],actual[3..6]);
        Assert.Equal(original[7..],actual[7..]);
    }
    const string Red = "ff00000000500201001080400000ff408080808080a080ff";

    [Fact]
    public void CustomColourPreservesEveryOtherSetting()
    {
        var original = Convert.FromHexString(Red);
        var changed = LightingRecord.WithColour(original, 255, 255, 255);
        Assert.Equal("FFFFFF", Convert.ToHexString(changed[..3]));
        Assert.Equal(original[3..], changed[3..]);
        Assert.Equal(Red.ToUpperInvariant(), Convert.ToHexString(original));
    }

    [Fact]
    public void ColourCannotSilentlyChangeAnActiveEffect()
    {
        var record = Convert.FromHexString(Red);
        record[4] = 3;
        Assert.Throws<InvalidOperationException>(() => LightingRecord.WithColour(record, 1, 2, 3));
        record[4] = 0; record[6] = 1;
        Assert.Throws<InvalidOperationException>(() => LightingRecord.WithColour(record, 1, 2, 3));
    }

    [Theory]
    [InlineData(0)] [InlineData(23)] [InlineData(25)]
    public void RejectsUnknownRecordShapes(int length) =>
        Assert.Throws<ArgumentException>(() => LightingRecord.WithColour(new byte[length], 1, 2, 3));

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void NativeModePreservesColourAndOtherParameters(byte mode)
    {
        var original = Convert.FromHexString(Red);
        var changed = LightingRecord.WithMode(original, mode);
        Assert.Equal(mode, changed[6]);
        changed[6] = original[6];
        Assert.Equal(original, changed);
    }

    [Theory]
    [InlineData(1)] [InlineData(5)] [InlineData(255)]
    public void RejectsUnmappedModes(byte mode) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LightingRecord.WithMode(Convert.FromHexString(Red), mode));
}
