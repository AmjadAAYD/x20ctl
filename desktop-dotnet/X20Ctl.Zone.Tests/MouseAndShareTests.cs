using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

public sealed class MouseAndShareTests
{
    static PadState Pad(ushort buttons = 0, short lx = 0, short ly = 0, short rx = 0, short ry = 0, byte lt = 0, byte rt = 0) => new(buttons, lt, rt, lx, ly, rx, ry);

    [Fact]
    public void A_full_push_moves_at_the_set_speed_and_up_on_the_stick_is_up_on_screen()
    {
        var m = new MouseMapper(new MouseProfile { Speed = 1000, Curve = 1, Deadzone = 0 });
        int x = 0, y = 0;
        for (int i = 0; i < 100; i++) { var f = m.Step(Pad(lx: 32767), .01); x += f.Dx; y += f.Dy; }
        Assert.InRange(x, 995, 1000); Assert.Equal(0, y);
        var up = m.Step(Pad(ly: 32767), .1);
        Assert.InRange(up.Dy, -100, -99);
    }

    [Fact]
    public void A_resting_stick_inside_the_dead_zone_never_moves_the_pointer()
    {
        var m = new MouseMapper(new MouseProfile { Deadzone = 12 });
        for (int i = 0; i < 1000; i++) { var f = m.Step(Pad(lx: 3000, ly: -2500), .01); Assert.Equal((0, 0), (f.Dx, f.Dy)); }
    }

    [Fact]
    public void Small_pushes_still_move_through_carried_fractions_and_the_curve_keeps_them_slow()
    {
        Assert.True(MouseMapper.Push(.3, 10, 2) < MouseMapper.Push(.3, 10, 1));
        Assert.Equal(1, MouseMapper.Push(1, 10, 2.5), 6);
        var m = new MouseMapper(new MouseProfile { Speed = 200, Deadzone = 10, Curve = 2 });
        int x = 0; for (int i = 0; i < 500; i++) x += m.Step(Pad(lx: (short)(32768 * .2)), .002).Dx;
        Assert.InRange(x, 1, 5); // 1.2 px a second for one second
    }

    [Fact]
    public void Buttons_give_one_edge_each_way_and_precision_slows_without_clicking()
    {
        var m = new MouseMapper(new MouseProfile { Curve = 1, Deadzone = 0, Speed = 1000, PrecisionPercent = 25 });
        var down = m.Step(Pad(PadButtons.A), .01);
        Assert.Equal([(MouseAction.LeftClick, true)], down.Edges);
        Assert.Empty(m.Step(Pad(PadButtons.A), .01).Edges);
        Assert.Equal([(MouseAction.LeftClick, false)], m.Step(Pad(), .01).Edges);
        var slow = m.Step(Pad(lx: 32767, lt: 255), .1);
        Assert.Empty(slow.Edges); Assert.InRange(slow.Dx, 24, 25);
    }

    [Fact]
    public void Switching_off_lets_go_of_everything_held()
    {
        var m = new MouseMapper(new MouseProfile()); m.Step(Pad(PadButtons.A | PadButtons.Up), .01);
        var released = m.Release();
        Assert.Contains((MouseAction.LeftClick, false), released); Assert.Contains((MouseAction.Up, false), released);
    }

    [Fact]
    public void The_other_stick_scrolls_and_the_pointer_stick_can_be_swapped()
    {
        var m = new MouseMapper(new MouseProfile { ScrollSpeed = 10, Deadzone = 0 });
        int wheel = 0; for (int i = 0; i < 100; i++) wheel += m.Step(Pad(ry: -32767), .01).Wheel;
        Assert.InRange(wheel, -1200, -1150); // ten notches down in a second
        var right = new MouseMapper(new MouseProfile { PointerStick = "Right", Curve = 1, Deadzone = 0, Speed = 1000 });
        Assert.InRange(right.Step(Pad(rx: 32767), .1).Dx, 99, 100);
    }

    [Fact]
    public void Mouse_settings_are_checked_and_round_trip()
    {
        Assert.Throws<InvalidDataException>(() => new MouseMapper(new MouseProfile { Speed = 1 }));
        Assert.Throws<InvalidDataException>(() => new MouseProfile { Buttons = { ["Q"] = MouseAction.Enter } }.Validate());
        string path = Path.Combine(Path.GetTempPath(), "x20ctl-mouse-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var p = new MouseProfile { Speed = 2222, PointerStick = "Right" }; p.Buttons["B"] = MouseAction.Escape; p.Save(path);
            var back = MouseProfile.Load(path);
            Assert.Equal((2222, "Right", MouseAction.Escape), (back.Speed, back.PointerStick, back.Buttons["B"]));
            File.WriteAllText(path, "{ not json");
            Assert.Equal(1500, MouseProfile.Load(path).Speed);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_hold_combo_fires_once_after_the_hold()
    {
        var c = new HoldCombo(PadButtons.Back | PadButtons.Start, 1000);
        Assert.False(c.Update(PadButtons.Back | PadButtons.Start, 0));
        Assert.False(c.Update(PadButtons.Back | PadButtons.Start, 999));
        Assert.True(c.Update(PadButtons.Back | PadButtons.Start | PadButtons.A, 1000));
        Assert.False(c.Update(PadButtons.Back | PadButtons.Start, 3000));
        Assert.False(c.Update(PadButtons.Back, 3001));
        Assert.False(c.Update(PadButtons.Back | PadButtons.Start, 3002));
    }

    static PcModeProfile Sample() => new()
    {
        Remap = { ["A"] = "B" }, Turbo = { ["X"] = 10 },
        Macros = { ["RB"] = [new SetupMacro(Guid.NewGuid(), ["A", "X"], StickHeading.Neutral, StickHeading.Up, 50, 30)] },
        Sticks = { ["LeftStick"] = new() { Inner = 4.5, AntiDeadzone = 8 } }, Name = "Secret window title",
    };

    [Fact]
    public void A_share_code_and_file_carry_the_settings_but_not_the_name()
    {
        string code = PcModeShare.ToCode(Sample());
        Assert.StartsWith(PcModeShare.CodePrefix, code); Assert.DoesNotContain("Secret", code);
        var back = PcModeShare.FromCode(" " + code + "\n");
        Assert.Equal("B", back.Remap["A"]); Assert.Equal(10, back.Turbo["X"]); Assert.Equal(StickHeading.Up, back.Macros["RB"][0].Right);
        Assert.Equal(8, back.Sticks["LeftStick"].AntiDeadzone); Assert.Null(back.Name);
        string json = PcModeShare.ToJson(Sample());
        Assert.DoesNotContain("Secret", json);
        Assert.Equal(4.5, PcModeShare.FromJson(json).Sticks["LeftStick"].Inner);
    }

    [Fact]
    public void Damaged_or_hostile_codes_and_files_are_refused_with_a_plain_reason()
    {
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromCode("hello"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromCode(PcModeShare.CodePrefix + "!!!"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromCode(PcModeShare.CodePrefix + "AAAA"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromJson("{\"x20ctlPcMode\":1,\"profile\":{\"Remap\":{\"A\":\"NUKE\"}}}"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromJson("{\"x20ctlPcMode\":1,\"profile\":{\"Macros\":{\"A\":null}}}"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromJson("{\"x20ctlPcMode\":1,\"profile\":{\"Curves\":{\"LeftStick\":null}}}"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromJson("[]"));
        Assert.Throws<InvalidDataException>(() => PcModeShare.FromJson(new string(' ', 70000)));
    }

    [Fact]
    public void Codes_made_by_the_Linux_script_import_and_ours_are_written_for_it()
    {
        // made by x20ctl-pcmode.py export for remap A to B (tests/test_pc_mode_linux.py imports one made by ToCode)
        Assert.Equal("B", PcModeShare.FromCode("X20PC1.q1YKSs1NLFCyqlZyVLJSclKq1VFyLi0qSy0GCgHZIaVFSfkQpm9iclE-VDi4JDM5G8yuBQA").Remap["A"]);
        var code = PcModeShare.ToCode(new PcModeProfile { Remap = { ["A"] = "B" } });
        Assert.Equal("B", PcModeShare.FromCode(code).Remap["A"]);
    }

    [Fact]
    public void Game_profiles_are_kept_even_when_empty_and_can_be_removed()
    {
        string path = Path.Combine(Path.GetTempPath(), "x20ctl-pc-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new PcModeStore(path); store.Load();
            string key = PcModeStore.GameKey(@"C:\Games\EldenRing.EXE");
            Assert.Equal("game:eldenring.exe", key);
            store.Save(key, new PcModeProfile { Name = "ELDEN RING" });
            store.Save(PcModeStore.DefaultKey, new PcModeProfile());
            Assert.True(store.Has(key)); Assert.False(store.Has(PcModeStore.DefaultKey)); Assert.False(store.AnySetting);
            store.Save(PcModeStore.DefaultKey, new PcModeProfile { Remap = { ["A"] = "B" } });
            Assert.True(store.AnySetting);
            var again = new PcModeStore(path); again.Load();
            Assert.Equal("ELDEN RING", again.Get(key).Name);
            again.Remove(key); Assert.False(again.Has(key));
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); }
    }
}
