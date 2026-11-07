using X20Ctl.Zone;

public class StudioMemoryTests
{
    [Fact]
    public void AStudioComesBackTheSameAfterARestart()
    {
        string file = Path.Combine(Path.GetTempPath(), "studio-memory-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var buttons = new ButtonDraft("x10") { PcMode = true }; var curves = new CurveDraft("x10") { PcMode = true };
            var macros = new MacroDraft("x10") { PcMode = true }; var vibration = new VibrationDraft("x10");
            buttons.Set("A", "B"); curves.SetPreset(CurveChannel.LeftStick, "quick");
            macros.Add("M1", 30); macros.SetTrigger("M1", "LB");
            new StudioMemory(file).Put(1, "x10", StudioMemory.Capture(buttons, curves, macros, vibration, "Curves", "{}"));

            var again = new StudioMemory(file).Get(1, "x10")!;   // a new launch reads the file
            Assert.Equal("Curves", again.Page); Assert.Equal("{}", again.PcBase);
            var b = new ButtonDraft("x10") { PcMode = true }; var c = new CurveDraft("x10") { PcMode = true }; var m = new MacroDraft("x10") { PcMode = true };
            StudioMemory.Restore(again, b, c, m, new VibrationDraft("x10"));
            Assert.Equal("B", b.Target("A")); Assert.Equal("quick", c[CurveChannel.LeftStick].Preset);
            Assert.Single(m["M1"]); Assert.Equal(30, m["M1"][0].PauseMs); Assert.Equal("LB", m.Trigger("M1"));
            Assert.Null(new StudioMemory(file).Get(0, "x10")); // each player keeps its own
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ALockedEditorKeepsWhatItHasAndTheRestStillComesBack()
    {
        var saved = new StudioMemory.Entry { Mappings = { ["A"] = "B" }, Vibration = 40 };
        var x20 = new VibrationDraft("x20"); var buttons = new ButtonDraft("x10"); // not unlocked: PC mode off
        StudioMemory.Restore(saved, buttons, new CurveDraft("x10"), new MacroDraft("x10"), x20);
        Assert.Equal("A", buttons.Target("A")); Assert.Equal(40, x20.Strength);
    }
}

public class PcModeVibrationTests
{
    [Fact]
    public void TheVibrationTabSetsHowMuchOfAGamesRumbleReachesThePad()
    {
        var dune = new X20Ctl.Zone.VibrationDraft("dune");
        Assert.False(dune.CanEdit);                      // PC mode off: a Dune's strength can't be set
        dune.PcMode = true; dune.Set(40);
        var b = new X20Ctl.Zone.ButtonDraft("dune") { PcMode = true }; var c = new X20Ctl.Zone.CurveDraft("dune") { PcMode = true }; var m = new X20Ctl.Zone.MacroDraft("dune") { PcMode = true };
        var p = X20Ctl.Zone.PcMode.PcModeDrafts.Merge(new X20Ctl.Zone.PcMode.PcModeProfile(), b, c, m, dune);
        Assert.Equal(40, p.Vibration); Assert.False(p.IsNeutral); p.Validate();
        var back = new X20Ctl.Zone.VibrationDraft("dune") { PcMode = true };
        X20Ctl.Zone.PcMode.PcModeDrafts.Load(p, b, c, m, back);
        Assert.Equal(40, back.Strength);
        Assert.True(new X20Ctl.Zone.PcMode.PcModeProfile().IsNeutral); // 100% is the default and changes nothing
    }
}

public class PcModeSetupTests
{
    [Fact]
    public void EveryPcModePadCanSaveAndOpenASetup()
    {
        foreach (var id in new[] { "dune", "x10", "d10", "x15", "x05_pro" })
        {
            var b = new ButtonDraft(id) { PcMode = true }; b.Set("A", "B");
            var m = new MacroDraft(id) { PcMode = true }; var slot = m.SlotNames[0]; m.Add(slot, 30); m.SetTrigger(slot, "LB");
            var s = SetupSnapshot.Capture(id, b, new CurveDraft(id) { PcMode = true }, m, new VibrationDraft(id) { PcMode = true });
            s.Validate();
            var back = new ButtonDraft(id) { PcMode = true }; var mb = new MacroDraft(id) { PcMode = true };
            s.LoadInto(back, new CurveDraft(id) { PcMode = true }, mb, new VibrationDraft(id) { PcMode = true });
            Assert.Equal("B", back.Target("A")); Assert.Single(mb[slot]); Assert.Equal("LB", mb.Trigger(slot));
        }
    }
}
