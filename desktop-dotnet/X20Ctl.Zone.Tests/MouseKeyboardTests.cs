using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;

public class MouseKeyboardTests
{
    static PadState Pad(ushort buttons = 0) => new(buttons, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void TheDPadMovesThePointerByDefault()
    {
        var mapper = new MouseMapper(new MouseProfile());
        var right = mapper.Step(Pad(PadButtons.Right), .1);
        Assert.True(right.Dx > 0); Assert.Equal(0, right.Dy);
        var up = mapper.Step(Pad(PadButtons.Up), .1);
        Assert.True(up.Dy < 0);
        Assert.DoesNotContain(up.Edges, e => MouseProfile.Moves(e.Action)); // moving is not a click or a key
    }

    [Fact]
    public void TurboClicksOverAndOverWhileHeld()
    {
        var p = new MouseProfile(); p.Turbo["A"] = 10; // A is left click: 10 clicks a second
        var mapper = new MouseMapper(p);
        int downs = 0, ups = 0;
        for (int i = 0; i < 50; i++) // one second in 20 ms ticks
            foreach (var (action, down) in mapper.Step(Pad(PadButtons.A), .02).Edges)
                if (action == MouseAction.LeftClick) { if (down) downs++; else ups++; }
        Assert.InRange(downs, 9, 11); Assert.InRange(ups, 8, 11);
    }

    [Fact]
    public void Mouse6And7AreOffered()
    {
        Assert.True(Enum.IsDefined(MouseAction.TiltLeft)); Assert.True(Enum.IsDefined(MouseAction.TiltRight));
        Assert.Equal(7, KeyCatalog.Mouse.Length);
        Assert.Equal("M6 Tilt left", KeyCatalog.Label("M:TiltLeft"));
    }

    [Fact]
    public void TurboOnAnUnknownButtonIsRefused()
    {
        var p = new MouseProfile(); p.Turbo["PADDLE"] = 10;
        Assert.Throws<InvalidDataException>(p.Validate);
        p.Turbo.Clear(); p.Turbo["A"] = 99;
        Assert.Throws<InvalidDataException>(p.Validate);
    }

    [Fact]
    public void AKeyMappedButtonHoldsTheKeyAndTheGameNeverSeesIt()
    {
        var p = new PcModeProfile(); p.Keys["X"] = "K:87"; // X sends W
        var pad = new PadProcessor(p);
        var held = pad.Process(Pad(PadButtons.X), 0);
        Assert.Equal(0, held.Buttons & PadButtons.X);
        Assert.Equal(new[] { ("K:87", true) }, pad.TakeActions());
        pad.Process(Pad(PadButtons.X), 16); Assert.Empty(pad.TakeActions()); // held, nothing new
        pad.Process(Pad(), 32);
        Assert.Equal(new[] { ("K:87", false) }, pad.TakeActions());
        Assert.False(p.IsNeutral);
    }

    [Fact]
    public void ACustomMappingPlaysItsStepsOncePerPress()
    {
        var p = new PcModeProfile();
        p.Custom["A"] = [new KeyStep { Output = "K:81", HoldMs = 50, PauseMs = 30 }, new KeyStep { Output = "M:LeftClick", HoldMs = 20, PauseMs = 0 }];
        var pad = new PadProcessor(p);
        var events = new List<(long, string, bool)>();
        for (long t = 0; t <= 300; t += 10)
        {
            var out1 = pad.Process(Pad(PadButtons.A), t); // held the whole time: still plays once
            Assert.Equal(0, out1.Buttons & PadButtons.A);
            foreach (var (a, d) in pad.TakeActions()) events.Add((t, a, d));
        }
        Assert.Equal(new[] { (0L, "K:81", true), (50L, "K:81", false), (80L, "M:LeftClick", true), (100L, "M:LeftClick", false) }, events);
    }

    [Fact]
    public void StoppingLetsGoOfHeldKeys()
    {
        var p = new PcModeProfile(); p.Keys["LB"] = "K:160"; p.Custom["B"] = [new KeyStep { Output = "K:69", HoldMs = 500 }];
        var pad = new PadProcessor(p);
        pad.Process(Pad(PadButtons.LB | PadButtons.B), 0); pad.TakeActions();
        var ups = pad.ReleaseAll();
        Assert.Contains(("K:160", false), ups); Assert.Contains(("K:69", false), ups);
    }

    [Theory]
    [InlineData("K:999")] [InlineData("M:Nope")] [InlineData("A")]
    public void UnknownKeysAreRefused(string output)
    {
        var p = new PcModeProfile(); p.Keys["A"] = output;
        Assert.Throws<InvalidDataException>(p.Validate);
    }

    [Fact]
    public void TheKeyboardHasEveryKey()
    {
        var ids = KeyCatalog.Keys.Select(k => k.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") Assert.Contains($"K:{(int)c}", ids);
        for (int f = 0; f < 12; f++) Assert.Contains($"K:{0x70 + f}", ids);
        foreach (int arrow in new[] { 0x25, 0x26, 0x27, 0x28 }) Assert.Contains($"K:{arrow}e", ids);
        Assert.Equal("Num 7", KeyCatalog.Label("K:103"));
    }
}
