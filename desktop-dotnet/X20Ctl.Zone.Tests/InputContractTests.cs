using X20Ctl.Zone;

namespace X20Ctl.Zone.Tests;

/// <summary>The automatable rows of audit/input-binding-research-2026-11-02/SCENARIO-MATRIX.csv (ids in the names).</summary>
public class InputContractTests
{
    private static readonly string[] ProTabs = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "PcMode", "RGB", "Screen", "Device", "Mouse", "Setups"];
    private static readonly string[] PlainTabs = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "PcMode", "Device", "Mouse", "Setups"];

    [Theory]
    [InlineData("PcMode", 1, "RGB")]      // S015 S028 S041
    [InlineData("RGB", 1, "Screen")]      // S016 S029 S042
    [InlineData("Screen", -1, "RGB")]     // S017 S030 S043
    [InlineData("Screen", 1, "Device")]
    [InlineData("Dashboard", -1, "Setups")] // S085 wraps
    [InlineData("Setups", 1, "Dashboard")]  // S086 wraps
    [InlineData("Diagnostics", 1, "Mouse")] // not a tab: steps from Device
    [InlineData("Tester", -1, "Screen")]
    public void Bumpers_follow_the_visible_tabs(string from, int step, string expected) => Assert.Equal(expected, StudioTabs.Step(ProTabs, from, step));

    [Fact]
    public void Hidden_tabs_are_skipped_the_same_way_by_every_route() // S089
    {
        Assert.Equal("Device", StudioTabs.Step(PlainTabs, "PcMode", 1));
        Assert.DoesNotContain(PlainTabs, p => p is "RGB" or "Screen");
        // a page that is not in the list (a theme-only page) starts the walk from an end, never throws
        Assert.Equal("Dashboard", StudioTabs.Step(PlainTabs, "Agent", 1));
        Assert.Equal("Setups", StudioTabs.Step(PlainTabs, "Agent", -1));
        Assert.Null(StudioTabs.Step([], "Buttons", 1));
    }

    private static RemapCapture Armed(RemapCapture.Side side = RemapCapture.Side.Source) { var c = new RemapCapture(); c.Begin(side, []); return c; }

    [Fact]
    public void One_snapshot_with_two_buttons_chooses_nothing() // S025 S038 S051 S057
    {
        var c = Armed();
        Assert.Null(c.Observe(["A", "B"]));
        Assert.True(c.Ambiguous); Assert.Equal(RemapCapture.Side.Source, c.Listening);
        Assert.Null(c.Observe(["A"])); // still held: no fresh gesture
        Assert.Null(c.Observe([]));
        Assert.Equal("B", c.Observe(["B"]));
        Assert.False(c.Ambiguous);
    }

    [Fact]
    public void Target_needs_a_release_after_the_source() // S022 S035 S048 S056
    {
        var c = Armed();
        Assert.Equal("A", c.Observe(["A"]));
        c.Continue(RemapCapture.Side.Target);
        Assert.Null(c.Observe(["A", "B"])); // B pressed while the source is still held
        Assert.Null(c.Observe(["B"]));
        Assert.Null(c.Observe([]));
        Assert.Equal("LB", c.Observe(["LB"]));
    }

    [Fact]
    public void Two_discrete_presses_without_release_fill_only_the_source() // S022 direct-call form
    {
        var c = Armed();
        Assert.Equal("A", c.Observe(["A"]));
        c.Continue(RemapCapture.Side.Target);
        Assert.Null(c.Observe(["B"])); // no release was ever reported
    }

    [Fact]
    public void Inputs_held_when_capture_opens_never_count() // S054 S055
    {
        var c = new RemapCapture(); c.Begin(RemapCapture.Side.Source, ["A", "LB"]);
        Assert.True(c.WaitingForRelease);
        Assert.Null(c.Observe(["LB"]));
        Assert.Null(c.Observe(["LB", "X"])); // fresh X, but not from neutral
        Assert.Null(c.Observe([]));
        Assert.Equal("LB", c.Observe(["LB"]));
    }

    [Fact]
    public void Repeats_and_duplicate_downs_are_one_gesture() // S058
    {
        var c = Armed();
        Assert.Equal("RB", c.Observe(["RB"]));
        c.Continue(RemapCapture.Side.Target);
        Assert.Null(c.Observe(["RB"])); Assert.Null(c.Observe(["RB"]));
        Assert.True(c.WaitingForRelease);
    }

    [Theory]
    [InlineData("B")] [InlineData("LB")] [InlineData("RB")] [InlineData("START")] [InlineData("SELECT")] [InlineData("L3")] [InlineData("R3")]
    public void Every_button_is_capture_data(string button) // S061 S062 S063 S064
    {
        var c = Armed();
        Assert.Equal(button, c.Observe([button]));
        c.Continue(RemapCapture.Side.Target);
        c.Observe([]);
        Assert.Equal(button, c.Observe([button]));
    }

    [Fact]
    public void Cancel_stops_listening_without_choosing() // S059 S060
    {
        var c = Armed();
        c.Cancel();
        Assert.Equal(RemapCapture.Side.None, c.Listening);
        Assert.Null(c.Observe(["A"]));
    }

    [Fact]
    public void Not_listening_accepts_nothing_but_keeps_tracking_held_buttons()
    {
        var c = new RemapCapture();
        Assert.Null(c.Observe(["A"]));
        c.Begin(RemapCapture.Side.Source, ["A"]);
        Assert.Null(c.Observe(["A"]));
    }

    private static string Key(params string[] buttons) => string.Join(",", buttons);

    [Fact]
    public void A_tap_between_renders_reaches_the_ui() // S024 S037 S050 S140
    {
        var q = new EdgeQueue<int>();
        q.Offer(1, Key()); q.Offer(2, Key("LB")); q.Offer(3, Key());
        Assert.Equal([1, 2, 3], q.Drain());
        Assert.Equal(0, q.Coalesced);
    }

    [Fact]
    public void Analog_only_samples_merge_into_the_latest()
    {
        var q = new EdgeQueue<string>();
        q.Offer("stick 0.1", Key("A")); q.Offer("stick 0.2", Key("A")); q.Offer("stick 0.3", Key("A"));
        Assert.Equal(["stick 0.3"], q.Drain());
        Assert.Equal(2, q.Coalesced);
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void A_slow_ui_has_a_bounded_queue_and_counts_what_it_lost() // S141
    {
        var q = new EdgeQueue<int>(4);
        for (int i = 0; i < 10; i++) q.Offer(i, i % 2 == 0 ? Key() : Key("A"));
        Assert.Equal([6, 7, 8, 9], q.Drain());
        Assert.Equal(6, q.Dropped);
    }

    private static InputOwnerRules.State State(bool active = true, bool connected = true, bool modal = false, bool recording = false, bool remap = false, bool tester = false, bool text = false) => new(active, connected, modal, recording, remap, tester, text);

    [Fact]
    public void Background_input_has_no_owner() // S023 S036 S049
    {
        Assert.Equal(InputOwnerRules.Owner.Nobody, InputOwnerRules.Resolve(State(active: false, remap: true)));
        Assert.Equal(InputOwnerRules.Owner.Nobody, InputOwnerRules.Resolve(State(connected: false, recording: true)));
    }

    [Fact]
    public void Recording_owns_the_pad_ahead_of_capture_and_navigation() // S026 S039 S052 S112
    {
        Assert.Equal(InputOwnerRules.Owner.MacroRecording, InputOwnerRules.Resolve(State(recording: true, remap: true, text: true)));
    }

    [Fact]
    public void A_modal_wins_over_capture_and_a_field() // S071
    {
        Assert.Equal(InputOwnerRules.Owner.Modal, InputOwnerRules.Resolve(State(modal: true, remap: true, recording: true)));
        Assert.Equal(InputOwnerRules.Owner.TextField, InputOwnerRules.Resolve(State(text: true)));
        Assert.Equal(InputOwnerRules.Owner.RemapCapture, InputOwnerRules.Resolve(State(remap: true, text: true)));
        Assert.Equal(InputOwnerRules.Owner.Navigation, InputOwnerRules.Resolve(State()));
    }

    [Fact]
    public void Every_state_has_exactly_one_owner()
    {
        var owners = new HashSet<InputOwnerRules.Owner>();
        for (int bits = 0; bits < 128; bits++)
        {
            bool B(int i) => (bits & (1 << i)) != 0;
            owners.Add(InputOwnerRules.Resolve(new(B(0), B(1), B(2), B(3), B(4), B(5), B(6))));
        }
        Assert.Equal(Enum.GetValues<InputOwnerRules.Owner>().Length, owners.Count);
    }
}
