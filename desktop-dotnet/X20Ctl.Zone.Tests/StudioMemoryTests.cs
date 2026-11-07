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
