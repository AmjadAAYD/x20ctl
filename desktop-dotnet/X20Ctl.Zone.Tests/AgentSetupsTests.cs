using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

/// <summary>The agent's saved setups, the changes it can go back through, and the live stick on the curve cards.</summary>
public sealed class AgentSetupsTests
{
    static string Folder() { string d = Path.Combine(Path.GetTempPath(), "x20ctl-agent-setups-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static PcModeProfile Profile(int deadzone)
    {
        var p = new PcModeProfile(); p.Sticks["LeftStick"] = new StickTuning { Inner = deadzone, Outer = 100 }; p.Turbo["A"] = 10; return p;
    }

    [Fact]
    public void A_setup_is_saved_found_by_any_case_and_kept_across_loads()
    {
        string path = Path.Combine(Folder(), "agent-setups.json");
        var store = new AgentSetupStore(path); store.Load();
        var (setup, replaced) = store.Save("Rocket League", "x20", Profile(6));
        Assert.False(replaced); Assert.Equal("Rocket League", setup.Name);
        var again = new AgentSetupStore(path); again.Load();
        var found = again.Find("  rocket league ")!;
        Assert.Equal(6, found.Profile.Sticks["LeftStick"].Inner); Assert.Equal(10, found.Profile.Turbo["A"]); Assert.Equal("x20", found.Model);
    }

    [Fact]
    public void Saving_under_a_name_that_exists_replaces_it_and_an_empty_name_is_numbered()
    {
        var store = new AgentSetupStore(Path.Combine(Folder(), "s.json")); store.Load();
        store.Save("Apex", "x20", Profile(4));
        Assert.True(store.Save("apex", "x20", Profile(9)).Replaced);
        Assert.Single(store.Items); Assert.Equal(9, store.Find("Apex")!.Profile.Sticks["LeftStick"].Inner);
        Assert.Equal("Setup 1", store.Save("", "x20", Profile(1)).Setup.Name);
        Assert.Equal("Setup 2", store.Save("  ", "x20", Profile(2)).Setup.Name);
        Assert.True(store.Delete("SETUP 1")); Assert.False(store.Delete("Setup 1"));
        Assert.Equal(["Apex", "Setup 2"], store.Names);
    }

    [Fact]
    public void A_saved_setup_is_a_copy_not_the_live_settings()
    {
        var store = new AgentSetupStore(Path.Combine(Folder(), "s.json")); store.Load();
        var live = Profile(5); store.Save("Copy", "x20", live);
        live.Sticks["LeftStick"].Inner = 30;
        Assert.Equal(5, store.Find("Copy")!.Profile.Sticks["LeftStick"].Inner);
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_never_overwritten()
    {
        string path = Path.Combine(Folder(), "s.json"); File.WriteAllText(path, "{\"version\":9,\"setups\":[]}");
        var store = new AgentSetupStore(path); store.Load();
        Assert.False(store.Healthy);
        Assert.Throws<IOException>(() => store.Save("New", "x20", Profile(3)));
        Assert.Contains("\"version\":9", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("Save this as Rocket League", SetupAsk.Save, "Rocket League")]
    [InlineData("save these settings as Apex", SetupAsk.Save, "Apex")]
    [InlineData("save this as my rocket league setup", SetupAsk.Save, "rocket league")]
    [InlineData("save this as a setup called Apex", SetupAsk.Save, "Apex")]
    [InlineData("Save this as The Last of Us", SetupAsk.Save, "The Last of Us")]
    [InlineData("save my setup", SetupAsk.Save, "")]
    [InlineData("save this", SetupAsk.Save, "")]
    [InlineData("save setup Fortnite", SetupAsk.Save, "Fortnite")]
    [InlineData("remember these settings as Racing", SetupAsk.Save, "Racing")]
    [InlineData("save my battery", SetupAsk.None, "")]
    [InlineData("load Rocket League", SetupAsk.Load, "Rocket League")]
    [InlineData("use my apex setup", SetupAsk.Load, "apex")]
    [InlineData("switch to setup Racing", SetupAsk.Load, "Racing")]
    [InlineData("load the Halo setup", SetupAsk.Load, "Halo")]
    [InlineData("use a quick curve on my right stick", SetupAsk.None, "")]
    [InlineData("apply a smooth curve", SetupAsk.None, "")]
    [InlineData("show my setups", SetupAsk.List, "")]
    [InlineData("what setups do I have?", SetupAsk.List, "")]
    [InlineData("setups", SetupAsk.List, "")]
    [InlineData("delete the Apex setup", SetupAsk.Delete, "Apex")]
    [InlineData("forget rocket league", SetupAsk.Delete, "rocket league")]
    [InlineData("remove turbo from A", SetupAsk.None, "")]
    public void Setups_are_asked_for_in_plain_words(string sentence, SetupAsk ask, string name)
    {
        var (got, gotName) = AgentSetupWords.Parse(sentence, ["Rocket League", "Apex"]);
        Assert.Equal(ask, got);
        Assert.Equal(name, gotName);
    }

    // ---- going back ----
    static IReadOnlyList<AgentChange> Changed(int from, int to) => [new("Left stick dead zone", from + "%", to + "%")];

    [Fact]
    public void Going_back_to_before_a_change_undoes_it_and_every_later_one()
    {
        var h = new AgentHistory();
        var one = h.Add("my aim drifts", Changed(0, 4), new PcModeProfile(), Profile(4));
        var two = h.Add("more", Changed(4, 8), Profile(4), Profile(8));
        h.Add("more still", Changed(8, 12), Profile(8), Profile(12));
        var back = h.BackTo(two.Id, Profile(12))!;
        Assert.Equal(4, back.Profile.Sticks["LeftStick"].Inner);
        Assert.Equal(["more still", "more"], back.Undone.Select(s => s.Asked));
        Assert.False(back.ChangedElsewhere);
        Assert.Equal(one, h.Latest);
        Assert.Null(h.BackTo(two.Id, Profile(4)));   // already undone
    }

    [Fact]
    public void Settings_changed_by_hand_since_are_named_when_going_back()
    {
        var h = new AgentHistory();
        var one = h.Add("drift", Changed(0, 4), new PcModeProfile(), Profile(4));
        var back = h.BackTo(one.Id, Profile(20))!;
        Assert.True(back.ChangedElsewhere);
        Assert.Empty(h.Steps);
    }

    [Fact]
    public void A_step_reads_as_its_first_change_and_how_many_more()
    {
        var h = new AgentHistory();
        var step = h.Add("shooter", [new("Right trigger full at", "100%", "40%"), new("Right trigger dead zone", "0%", "2%")], new(), new());
        Assert.Equal("Right trigger full at 100% → 40% and 1 more", step.Summary);
    }

    [Theory]
    [InlineData("undo", UndoAsk.Last)]
    [InlineData("Undo that", UndoAsk.Last)]
    [InlineData("just undo this", UndoAsk.Last)]
    [InlineData("undo the last change", UndoAsk.Last)]
    [InlineData("take that back", UndoAsk.Last)]
    [InlineData("undo everything", UndoAsk.All)]
    [InlineData("Undo everything I changed", UndoAsk.All)]
    [InlineData("undo all my changes", UndoAsk.All)]
    [InlineData("put everything back", UndoAsk.All)]
    [InlineData("go back to how it was", UndoAsk.All)]
    [InlineData("what did you change?", UndoAsk.Show)]
    [InlineData("show my changes", UndoAsk.Show)]
    [InlineData("undo everything, back to factory defaults", UndoAsk.None)]
    [InlineData("reset everything", UndoAsk.None)]
    [InlineData("undo turbo on A", UndoAsk.None)]
    public void Undo_is_asked_for_in_plain_words(string sentence, UndoAsk ask) => Assert.Equal(ask, AgentHistory.Parse(sentence));

    // ---- the live stick ----
    [Fact]
    public void A_live_stick_lands_on_the_drawn_curve()
    {
        var before = new PcModeProfile(); var after = Profile(20);
        var curve = AgentResponsePreview.Curve(before, after, "LeftStick", 4);   // inputs 0, 25, 50, 75, 100
        var half = AgentResponsePreview.Live(before, after, "LeftStick", 16384, 0);
        Assert.Equal(50, half.Input, 2);
        Assert.Equal(curve[2].Before, half.Before, 3); Assert.Equal(curve[2].After, half.After, 3);
        // the push is how far from the centre in any direction: straight down by half is the same push
        var down = AgentResponsePreview.Live(before, after, "LeftStick", 0, -16384);
        Assert.Equal(half, down);
        var rest = AgentResponsePreview.Live(before, after, "LeftStick", 0, 0);
        Assert.Equal((0.0, 0.0, 0.0), rest);
    }
}
