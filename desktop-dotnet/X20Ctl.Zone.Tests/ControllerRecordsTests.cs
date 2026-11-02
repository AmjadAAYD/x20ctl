using X20Ctl.Zone;

public class ControllerRecordsTests
{
    const string Sticks = "0E03035555AAAA0003035555AAAA00", Triggers = "0E06105555AAAA0006105555AAAA00";
    static ControllerSnapshot Baseline() => new([0], Convert.FromHexString(Sticks), Convert.FromHexString(Triggers), [.. new[] { "0100008800200800", "0100008800100800", "0100008800200800", "0100008800100800", "0100008810000800", "0100008820000800" }.Select(Convert.FromHexString)]);

    [Fact]
    public void AToBMatchesTheRecordThePadAcceptedAndReadBack()
    {
        Assert.Equal("1002000000000000000000000000000000", Convert.ToHexString(ControllerRecords.BuildRemap(new Dictionary<string, string> { ["A"] = "B" })));
        Assert.Equal("B", ControllerRecords.ParseRemap(Convert.FromHexString("1002000000000000000000000000000000"))["A"]);
    }

    [Theory]
    [InlineData(0x52, "03DFA902")] // the X20 on 1.23 that the reset was proved on
    [InlineData(0x56, "03DFA902")] // the X20 Pro on 1.44 from a PC
    [InlineData(0x57, "03DFA901")] // generation 2 in mode 7 counts as 1
    [InlineData(0x20, "03DFA901")]
    public void FactoryResetFollowsThePadsGeneration(byte flags, string expected) => Assert.Equal(expected, Convert.ToHexString(ControllerRecords.FactoryResetPayload(flags)!));

    [Fact]
    public void FactoryResetRefusesAnUnknownGeneration() => Assert.Null(ControllerRecords.FactoryResetPayload(0x62));

    [Fact]
    public void ChangesCountsDifferentRecords()
    {
        var before = Baseline();
        Assert.Equal(0, ControllerRecords.Changes(before, before with { Remap = new byte[17] { 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } }));
        var macros = before.Macros.ToArray(); macros[2] = [1, 0, 0, 1, 0, 0, 8, 0];
        Assert.Equal(2, ControllerRecords.Changes(before, before with { Sticks = Convert.FromHexString(Triggers), Macros = macros }));
    }

    [Fact]
    public void BothNoRemapFormsMeanTheSame()=>Assert.True(ControllerRecords.SameRemap([0], Convert.FromHexString("1000000000000000000000000000000000")));

    [Fact]
    public void ReadsTheDefaultCurveRecords()
    {
        var stick = ControllerRecords.ParseChannel(Convert.FromHexString(Sticks), 0, false);
        Assert.Equal((3, 97, "default"), (stick.Inner, stick.Outer, stick.Preset));
        var trigger = ControllerRecords.ParseChannel(Convert.FromHexString(Triggers), 1, true);
        Assert.Equal((3, 92), (trigger.Inner, trigger.Outer));
    }

    [Fact]
    public void ACurveChangeTouchesOnlyItsChannel()
    {
        var current = Convert.FromHexString(Sticks);
        var left = ControllerRecords.ParseChannel(current, 0, false);
        var built = ControllerRecords.BuildCurves(current, left, left with { Inner = 5, Point1 = (64, 102), Point2 = (166, 217) }, false);
        Assert.Equal("0E03035555AAAA000503" + "4066A6D9" + "00", Convert.ToHexString(built));
        Assert.Equal(Sticks, Convert.ToHexString(ControllerRecords.BuildCurves(current, left, left, false)));
    }

    [Fact]
    public void AnOddTriggerValueSurvivesWhenLeftAlone()
    {
        var current = Convert.FromHexString("0E07115555AAAA0006105555AAAA00");
        Assert.Equal(current, ControllerRecords.BuildCurves(current, ControllerRecords.ParseChannel(current, 0, true), ControllerRecords.ParseChannel(current, 1, true), true));
    }

    [Theory]
    [InlineData("0100008810000000")] [InlineData("0100008800200800")] [InlineData("0200008810008C00880000 1A00")] [InlineData("01000082000 01400")]
    public void MacrosRebuildToTheBytesTheyCameFrom(string hex)
    {
        var raw = Convert.FromHexString(hex.Replace(" ", ""));
        Assert.Equal(raw, ControllerRecords.BuildMacro(MacroRecord.Parse(raw), raw));
    }

    [Fact]
    public void MacroLoopBytesAreKept()=>Assert.Equal("010501881000" + "1400",Convert.ToHexString(ControllerRecords.BuildMacro(MacroRecord.Parse(Convert.FromHexString("0100008810001400")), Convert.FromHexString("0105018800000000"))));

    [Fact]
    public void AMacroOverThePadsLimitIsRefused()
    {
        var events = Enumerable.Range(0, 43).Select(_ => new MacroEvent(Guid.NewGuid(), ["A"], StickHeading.Neutral, StickHeading.Neutral, 40, 0)).ToList();
        Assert.Throws<InvalidOperationException>(() => ControllerRecords.BuildMacro(events, [0]));
    }

    [Fact]
    public void LoadingThenComposingChangesNothing()
    {
        var read = Baseline();
        var (buttons, curves, macros) = (new ButtonDraft("x20_pro"), new CurveDraft("x20_pro"), new MacroDraft("x20_pro"));
        ControllerRecords.Load(read, buttons, curves, macros);
        var target = ControllerRecords.Compose(read, buttons, curves, macros);
        Assert.Equal(read.Remap, target.Remap); Assert.Equal(read.Sticks, target.Sticks); Assert.Equal(read.Triggers, target.Triggers);
        for (int i = 0; i < 6; i++) Assert.Equal(read.Macros[i], target.Macros[i]);
    }

    [Fact]
    public void ComposingOneMappingChangesOnlyTheRemapRecord()
    {
        var read = Baseline();
        var (buttons, curves, macros) = (new ButtonDraft("x20_pro"), new CurveDraft("x20_pro"), new MacroDraft("x20_pro"));
        ControllerRecords.Load(read, buttons, curves, macros);
        buttons.Set("A", "B");
        var target = ControllerRecords.Compose(read, buttons, curves, macros);
        Assert.Equal("1002000000000000000000000000000000", Convert.ToHexString(target.Remap));
        Assert.Equal(read.Sticks, target.Sticks); Assert.Equal(read.Macros[4], target.Macros[4]);
    }
}
