using X20Ctl.Zone.PcMode;

public class ChordEngineTests
{
    const uint X = 1, B = 2, Home = 4;

    static string Run(ChordEngine e, params (long t, uint down)[] steps) =>
        string.Join(" ", steps.SelectMany(s => e.Update(s.t, s.down)).Select(ev => $"{ev.Id}:{ev.Phase}"));

    [Fact]
    public void Press_follows_the_button_down_and_up() =>
        Assert.Equal("a:Down a:Up", Run(new ChordEngine([new("a", X)]), (0, X), (10, X), (20, 0)));

    [Fact]
    public void Chord_fires_when_both_go_down_inside_the_window() =>
        Assert.Equal("c:Down c:Up", Run(new ChordEngine([new("c", X | B)]), (0, X), (20, X | B), (50, 0)));

    [Fact]
    public void Buttons_pressed_far_apart_are_not_a_chord() =>
        Assert.Equal("", Run(new ChordEngine([new("c", X | B)]), (0, X), (200, X | B), (300, 0)));

    [Fact]
    public void Wait_mode_single_is_swallowed_by_a_chord()
    {
        var e = new ChordEngine([new("x", X, Mode: ChordMode.WaitForChord), new("xb", X | B)]);
        Assert.Equal("xb:Down xb:Up", Run(e, (0, X), (10, X | B), (30, X | B), (60, 0)));
    }

    [Fact]
    public void Wait_mode_single_fires_after_the_window_when_alone()
    {
        var e = new ChordEngine([new("x", X, Mode: ChordMode.WaitForChord), new("xb", X | B)]);
        Assert.Equal("x:Down x:Up", Run(e, (0, X), (30, X), (50, X), (100, 0)));
    }

    [Fact]
    public void Instant_single_fires_at_once_even_with_a_chord()
    {
        var e = new ChordEngine([new("x", X), new("xb", X | B)]);
        Assert.StartsWith("x:Down", Run(e, (0, X), (10, X | B)));
    }

    [Fact]
    public void Tap_fires_on_short_release_but_not_after_a_hold() =>
        Assert.Equal("t:Once", Run(new ChordEngine([new("t", X, PressKind.Tap)]), (0, X), (100, 0), (1000, X), (1500, 0)));

    [Fact]
    public void Hold_fires_once_after_the_hold_time() =>
        Assert.Equal("h:Once", Run(new ChordEngine([new("h", X, PressKind.Hold)]), (0, X), (200, X), (400, X), (600, X), (700, 0)));

    [Fact]
    public void Double_tap_needs_two_quick_taps() =>
        Assert.Equal("d:Once", Run(new ChordEngine([new("d", X, PressKind.DoubleTap)]), (0, X), (50, 0), (150, X), (200, 0)));

    [Fact]
    public void Release_binding_fires_on_letting_go() =>
        Assert.Equal("r:Once", Run(new ChordEngine([new("r", Home, PressKind.Release)]), (0, Home), (500, 0)));
}
