using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

/// <summary>The Studio's tabs as PC mode's editor: drafts unlock for any model, and settings round-trip without
/// losing what the tabs can't show.</summary>
public sealed class PcModeDraftsTests
{
    static (ButtonDraft, CurveDraft, MacroDraft) Drafts(string model = "dune", bool pc = true) =>
        (new ButtonDraft(model) { PcMode = pc }, new CurveDraft(model) { PcMode = pc }, new MacroDraft(model) { PcMode = pc });

    [Fact]
    public void A_model_that_cant_be_written_is_editable_only_while_PC_mode_is_on()
    {
        var (b, c, m) = Drafts(pc: false);
        Assert.False(b.CanEdit("A")); Assert.False(c.CanEdit); Assert.False(m.CanEdit);
        Assert.Throws<InvalidOperationException>(() => b.Set("A", "B"));
        (b, c, m) = Drafts();
        Assert.True(b.CanEdit("A")); Assert.True(c.CanEdit); Assert.True(m.CanEdit);
        b.Set("A", "B"); Assert.Equal("B", b.Target("A"));
    }

    [Fact]
    public void Tabs_settings_become_a_profile_and_come_back()
    {
        var (b, c, m) = Drafts();
        b.Set("A", "B"); b.Set("LB", "LT");
        c.SetPreset(CurveChannel.LeftStick, "quick");
        m.Add("M1", 30); m.Update("M1", 0, m["M1"][0] with { Buttons = ["X", "Y"], HoldMs = 55 }); m.SetTrigger("M1", "L3");
        m.Add("M2"); // no button picked: PC mode can't play it, so it isn't saved

        var p = PcModeDrafts.Merge(new PcModeProfile(), b, c, m);
        p.Validate();
        Assert.Equal("B", p.Remap["A"]); Assert.Equal("LT", p.Remap["LB"]); Assert.Equal(2, p.Remap.Count);
        Assert.Equal("quick", p.Curves["LeftStick"].Preset); Assert.Single(p.Curves);
        Assert.Equal(["L3"], p.Macros.Keys);
        Assert.Equal(["X", "Y"], p.Macros["L3"][0].Buttons); Assert.Equal(55, p.Macros["L3"][0].HoldMs);

        var (b2, c2, m2) = Drafts("x05");
        PcModeDrafts.Load(p, b2, c2, m2);
        Assert.Equal("B", b2.Target("A")); Assert.Equal("quick", c2[CurveChannel.LeftStick].Preset);
        Assert.Equal("L3", m2.Trigger("M1")); Assert.Single(m2["M1"]); Assert.Empty(m2["M2"]);
    }

    [Fact]
    public void What_the_tabs_cant_show_is_kept()
    {
        var saved = new PcModeProfile { Name = "Elden Ring" };
        saved.Remap["START"] = "SELECT"; saved.Remap["A"] = "HOME"; saved.Turbo["X"] = 10; saved.Sticks["LeftStick"] = new StickTuning { Inner = 4.5 };
        var (b, c, m) = Drafts();
        PcModeDrafts.Load(saved, b, c, m);
        Assert.Equal("A", b.Target("A")); // Home isn't a Buttons tab output

        var p = PcModeDrafts.Merge(saved, b, c, m);
        Assert.Equal("SELECT", p.Remap["START"]); Assert.Equal("HOME", p.Remap["A"]);
        Assert.Equal(10, p.Turbo["X"]); Assert.Equal(4.5, p.Sticks["LeftStick"].Inner); Assert.Equal("Elden Ring", p.Name);

        b.Set("A", "Y"); // changing it in the tab replaces it
        Assert.Equal("Y", PcModeDrafts.Merge(saved, b, c, m).Remap["A"]);
    }

    [Fact]
    public void A_button_plays_one_slot_and_timings_fit_the_macro_editor()
    {
        var (_, _, m) = Drafts();
        m.SetTrigger("M1", "RB"); m.SetTrigger("M2", "RB");
        Assert.Null(m.Trigger("M1")); Assert.Equal("RB", m.Trigger("M2"));
        Assert.Throws<ArgumentException>(() => m.SetTrigger("M1", "NUKE"));

        var p = new PcModeProfile();
        p.Macros["Y"] = [new(Guid.NewGuid(), ["A", "HOME"], StickHeading.Up, StickHeading.Neutral, 47, 13)];
        var (b, c, m2) = Drafts();
        PcModeDrafts.Load(p, b, c, m2);
        var step = m2["M1"][0];
        Assert.Equal(["A"], step.Buttons); Assert.Equal(StickHeading.Up, step.Left); Assert.Equal(45, step.HoldMs); Assert.Equal(15, step.PauseMs);
    }
}

public sealed class PcModePlayerKeysTests
{
    [Fact]
    public void Each_player_has_its_own_keys_and_player_one_keeps_the_old_ones()
    {
        Assert.Equal("default", PcModeStore.For(0, "default"));
        Assert.Equal("p2:default", PcModeStore.For(1, "default"));
        Assert.Equal("p4:game:eldenring.exe", PcModeStore.For(3, PcModeStore.GameKey(@"C:\Games\EldenRing.exe")));
        Assert.Equal((2, "game:x.exe"), PcModeStore.Split("p3:game:x.exe"));
        Assert.Equal((0, "game:p2:odd.exe"), PcModeStore.Split("game:p2:odd.exe"));
        Assert.Equal((0, "p9:default"), PcModeStore.Split("p9:default"));
        Assert.True(PcModeStore.IsGame("p2:game:x.exe")); Assert.False(PcModeStore.IsGame("p2:default"));
    }

    [Fact]
    public void Players_settings_are_kept_apart_and_a_players_empty_game_is_kept()
    {
        string path = Path.Combine(Path.GetTempPath(), "x20ctl-players-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new PcModeStore(path); store.Load();
            var one = new PcModeProfile(); one.Remap["A"] = "B";
            var two = new PcModeProfile(); two.Remap["X"] = "Y";
            store.Save(PcModeStore.For(0, "default"), one); store.Save(PcModeStore.For(1, "default"), two);
            store.Save(PcModeStore.For(1, "game:x.exe"), new PcModeProfile());
            var again = new PcModeStore(path); again.Load();
            Assert.Equal("B", again.Get("default").Remap["A"]); Assert.False(again.Get("default").Remap.ContainsKey("X"));
            Assert.Equal("Y", again.Get("p2:default").Remap["X"]);
            Assert.True(again.Has("p2:game:x.exe")); Assert.False(again.Has("game:x.exe"));
            store.Save(PcModeStore.For(1, "default"), new PcModeProfile()); // neutral every-game settings go away
            Assert.False(store.Has("p2:default"));
        }
        finally { foreach (var f in new[] { path, path + ".bak" }) if (File.Exists(f)) File.Delete(f); }
    }
}
