using X20Ctl.Zone.PcMode;

public class ComboProcessorTests
{
    static PadState Held(ushort buttons) => new(buttons, 0, 0, 0, 0, 0, 0);

    static PadProcessor With(params ComboRule[] rules) => new(new PcModeProfile { Combos = rules.ToList() });

    [Fact]
    public void Chord_outputs_the_target_and_hides_the_sources()
    {
        var p = With(new ComboRule { Id = "c", Buttons = ["X", "B"], Output = "Y" });
        p.Process(Held(PadButtons.X), 0);
        var s = p.Process(Held((ushort)(PadButtons.X | PadButtons.B)), 20);
        Assert.Equal(PadButtons.Y, s.Buttons);
        Assert.Equal(0, p.Process(Held(0), 100).Buttons);
    }

    [Fact]
    public void Tap_combo_pulses_the_output_then_releases()
    {
        var p = With(new ComboRule { Id = "t", Buttons = ["A"], Kind = PressKind.Tap, Output = "START" });
        p.Process(Held(PadButtons.A), 0);
        Assert.Equal(PadButtons.Start, p.Process(Held(0), 50).Buttons);
        Assert.Equal(0, p.Process(Held(0), 200).Buttons);
    }

    [Fact]
    public void Output_none_only_swallows_the_buttons()
    {
        var p = With(new ComboRule { Id = "n", Buttons = ["LB", "RB"], Output = "NONE" });
        p.Process(Held(PadButtons.LB), 0);
        Assert.Equal(0, p.Process(Held((ushort)(PadButtons.LB | PadButtons.RB)), 10).Buttons);
    }

    [Fact]
    public void Profiles_without_combos_are_untouched() =>
        Assert.Equal(PadButtons.A, With().Process(Held(PadButtons.A), 0).Buttons);

    [Fact]
    public void Validation_rejects_unknown_buttons_and_duplicate_ids()
    {
        Assert.Throws<InvalidDataException>(() => With(new ComboRule { Id = "x", Buttons = ["NOPE"], Output = "A" }));
        Assert.Throws<InvalidDataException>(() => With(new ComboRule { Id = "x", Buttons = ["A"] }, new ComboRule { Id = "x", Buttons = ["B"] }));
    }

    [Fact]
    public void A_profile_with_combos_is_not_neutral() =>
        Assert.False(new PcModeProfile { Combos = [new ComboRule { Id = "x", Buttons = ["A"] }] }.IsNeutral);
}

public class ComboActionTests
{
    static PadState Held(ushort b) => new(b, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Action_output_is_asked_for_down_then_up_and_hides_the_buttons()
    {
        var p = new PadProcessor(new PcModeProfile { Combos = [new ComboRule { Id = "a", Buttons = ["X", "B"], Output = "ACT:Escape" }] });
        p.Process(Held(PadButtons.X), 0);
        var s = p.Process(Held((ushort)(PadButtons.X | PadButtons.B)), 10);
        Assert.Equal(0, s.Buttons);
        Assert.Equal([("Escape", true)], p.TakeActions());
        p.Process(Held(0), 100);
        Assert.Equal([("Escape", false)], p.TakeActions());
    }

    [Fact]
    public void Tap_action_is_a_down_and_up_in_one_go()
    {
        var p = new PadProcessor(new PcModeProfile { Combos = [new ComboRule { Id = "t", Buttons = ["A"], Kind = PressKind.Tap, Output = "ACT:LeftClick" }] });
        p.Process(Held(PadButtons.A), 0); p.Process(Held(0), 50);
        Assert.Equal([("LeftClick", true), ("LeftClick", false)], p.TakeActions());
    }

    [Theory]
    [InlineData("ACT:None")] [InlineData("ACT:Nope")] [InlineData("ACT:")]
    public void Unknown_actions_are_rejected(string output) =>
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Combos = [new ComboRule { Id = "x", Buttons = ["A"], Output = output }] }));
}
