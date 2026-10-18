using X20Ctl.Linux;
namespace X20Ctl.Zone.Tests;

public sealed class PadLayoutTests
{
    static AbsInfo Stick(int lo = -32768, int hi = 32767) => new(lo, hi, (lo + hi) / 2);
    static AbsInfo Trigger(int hi = 255) => new(0, hi, 0);
    static AbsInfo Hat() => new(-1, 1, 0);
    static readonly int[] Face = [0x130, 0x131, 0x133, 0x134, 0x136, 0x137, 0x138, 0x139, 0x13A, 0x13B, 0x13C, 0x13D, 0x13E];

    static PadCaps Xpad() => new(Face.ToHashSet(), new Dictionary<int, AbsInfo>
    { [0] = Stick(), [1] = Stick(), [3] = Stick(), [4] = Stick(), [2] = Trigger(), [5] = Trigger(), [0x10] = Hat(), [0x11] = Hat() });

    [Fact]
    public void An_xbox_pad_in_the_kernels_layout_is_standard_and_reads_its_triggers_from_Z_and_RZ()
    {
        var l = PadLayouts.Guess(Xpad());
        Assert.Equal("standard", l.Source);
        Assert.Equal(2, l.LT!.Code); Assert.Equal(5, l.RT!.Code);
        Assert.Equal("A", l.Buttons[0x130]); Assert.Equal("X", l.Buttons[0x133]);
        Assert.True(l.LY!.Invert); Assert.False(l.LX!.Invert);
        Assert.Equal(0x10, l.HatX!.Code);
        Assert.DoesNotContain("LT", l.Buttons.Values); // analog triggers replace the digital keys
    }

    [Fact]
    public void A_playstation_style_pad_with_sticks_resting_at_128_is_still_standard()
    {
        var caps = new PadCaps(Face.ToHashSet(), new Dictionary<int, AbsInfo>
        { [0] = new(0, 255, 128), [1] = new(0, 255, 128), [3] = new(0, 255, 128), [4] = new(0, 255, 128), [2] = Trigger(), [5] = Trigger(), [0x10] = Hat(), [0x11] = Hat() });
        var l = PadLayouts.Guess(caps);
        Assert.Equal("standard", l.Source); Assert.Equal(3, l.RX!.Code);
    }

    [Fact]
    public void A_generic_directinput_pad_is_guessed_with_the_right_stick_on_Z_and_RZ()
    {
        var keys = Enumerable.Range(0x120, 12).ToHashSet();
        var caps = new PadCaps(keys, new Dictionary<int, AbsInfo> { [0] = new(0, 255, 128), [1] = new(0, 255, 128), [2] = new(0, 255, 128), [5] = new(0, 255, 128), [0x10] = Hat(), [0x11] = Hat() });
        var l = PadLayouts.Guess(caps);
        Assert.Equal("guessed", l.Source);
        Assert.Equal(2, l.RX!.Code); Assert.Equal(5, l.RY!.Code);
        Assert.Null(l.LT); Assert.Equal("A", l.Buttons[0x120]); Assert.Equal("LT", l.Buttons[0x126]); // no analog triggers, so the buttons carry them
        Assert.True(l.HasDpad);
    }

    [Fact]
    public void Triggers_that_rest_high_are_flipped()
    {
        var axes = new Dictionary<int, AbsInfo> { [0] = Stick(), [1] = Stick(), [3] = Stick(), [4] = Stick(), [2] = new(0, 255, 255), [5] = new(0, 255, 255), [0x10] = Hat(), [0x11] = Hat() };
        var l = PadLayouts.Guess(new PadCaps(Face.ToHashSet(), axes));
        Assert.True(l.LT!.Invert && l.RT!.Invert);
    }

    [Fact]
    public void A_pad_with_digital_triggers_only_keeps_them_as_buttons()
    {
        var axes = new Dictionary<int, AbsInfo> { [0] = Stick(), [1] = Stick(), [3] = Stick(), [4] = Stick(), [0x10] = Hat(), [0x11] = Hat() };
        var l = PadLayouts.Guess(new PadCaps(Face.ToHashSet(), axes));
        Assert.Null(l.LT); Assert.Equal("LT", l.Buttons[0x138]); Assert.Equal("standard", l.Source);
    }

    [Fact]
    public void A_touchpad_a_tablet_and_a_mouse_are_not_gamepads()
    {
        var pad = new Dictionary<int, AbsInfo> { [0] = Stick(), [1] = Stick() };
        Assert.False(PadLayouts.LooksLikeGamepad(new PadCaps(new HashSet<int> { 0x110, 0x14A, 0x145 }, pad)));
        Assert.False(PadLayouts.LooksLikeGamepad(new PadCaps(new HashSet<int> { 0x110, 0x111, 0x112 }, new Dictionary<int, AbsInfo>())));
        Assert.True(PadLayouts.LooksLikeGamepad(Xpad()));
        Assert.True(PadLayouts.LooksLikeGamepad(new PadCaps(Enumerable.Range(0x120, 8).ToHashSet(), pad)));
    }

    [Fact]
    public void Learning_replaces_a_wrong_guess_with_what_the_owner_pressed()
    {
        var caps = new PadCaps(Enumerable.Range(0x120, 12).ToHashSet(), new Dictionary<int, AbsInfo> { [0] = Stick(), [1] = Stick(), [2] = Stick(), [5] = Stick(), [0x10] = Hat(), [0x11] = Hat() });
        var seen = new Dictionary<string, PadLayouts.Seen>
        {
            ["A"] = new(0x121, null, 0), ["B"] = new(0x120, null, 0),
            ["LS_UP"] = new(null, 1, 1), ["LS_RIGHT"] = new(null, 0, -1), ["RS_UP"] = new(null, 5, -1), ["RS_RIGHT"] = new(null, 2, 1),
        };
        var l = PadLayouts.Learn(caps, seen);
        Assert.Equal("learned", l.Source);
        Assert.Equal("A", l.Buttons[0x121]); Assert.Equal("B", l.Buttons[0x120]);
        Assert.False(l.LY!.Invert);           // up already reads positive
        Assert.True(l.LX!.Invert);            // right reads negative, so flip it
        Assert.Equal(5, l.RY!.Code); Assert.True(l.RY.Invert); Assert.Equal(2, l.RX!.Code); Assert.False(l.RX.Invert);
        Assert.Equal("X", l.Buttons[0x122]);  // untouched buttons keep the guess
    }

    [Fact]
    public void Learning_trigger_axes_and_dpad_axes_records_direction()
    {
        var l = PadLayouts.Learn(Xpad(), new Dictionary<string, PadLayouts.Seen>
        { ["LT"] = new(null, 2, -1), ["DPAD_UP"] = new(null, 0x11, 1), ["DPAD_LEFT"] = new(null, 0x10, -1) });
        Assert.True(l.LT!.Invert); Assert.True(l.HatY!.Invert); Assert.False(l.HatX!.Invert);
    }

    [Fact]
    public void Identical_layouts_have_no_differences_and_a_moved_button_is_reported()
    {
        var a = PadLayouts.Guess(Xpad()); var b = PadLayouts.Guess(Xpad());
        Assert.Empty(PadLayouts.Differences(a, b));
        b.Buttons.Remove(0x130); b.Buttons[0x120] = "A";
        Assert.Contains(PadLayouts.Differences(a, b), s => s.StartsWith("A "));
    }

    [Fact]
    public void Layouts_survive_a_round_trip_through_the_saved_file()
    {
        var learned = PadLayouts.Learn(Xpad(), new Dictionary<string, PadLayouts.Seen> { ["A"] = new(0x121, null, 0) });
        var back = PadLayouts.FromJson(PadLayouts.ToJson(new() { ["045e:028e"] = new() { Name = "Pad", Verified = true, Layout = learned } }));
        Assert.Equal("A", back["045e:028e"].Layout!.Buttons[0x121]);
        Assert.Equal(learned.LT!.Code, back["045e:028e"].Layout!.LT!.Code);
        Assert.True(back["045e:028e"].Verified);
        Assert.Empty(PadLayouts.FromJson("not json"));
    }
}
