using X20Ctl.Linux;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class FfWireTests
{
    [Theory]
    [InlineData(8, 48, 104, 24)]
    [InlineData(4, 44, 96, 16)]
    public void Sizes_match_the_kernel_structs(int pointer, int effect, int upload, int evt)
    {
        Assert.Equal(effect, FfWire.EffectSize(pointer)); Assert.Equal(upload, FfWire.UploadSize(pointer)); Assert.Equal(evt, FfWire.EventSize(pointer));
    }

    [Fact]
    public void Rumble_effect_puts_each_field_where_ff_effect_has_it()
    {
        var e = FfWire.RumbleEffect(8, -1, 0x1234, 0x00FF, 0xFFFF);
        Assert.Equal(48, e.Length);
        Assert.Equal(0x50, BitConverter.ToUInt16(e, 0)); Assert.Equal(-1, BitConverter.ToInt16(e, 2)); Assert.Equal(0xFFFF, BitConverter.ToUInt16(e, 10));
        Assert.Equal(0x1234, BitConverter.ToUInt16(e, 16)); Assert.Equal(0x00FF, BitConverter.ToUInt16(e, 18));
    }

    [Fact]
    public void An_upload_record_is_read_back_from_the_new_effect()
    {
        var up = new byte[FfWire.UploadSize(8)];
        FfWire.RumbleEffect(8, 3, 40000, 12000, 100).CopyTo(up, 8);
        var fx = FfWire.NewEffect(up);
        Assert.Equal((FfWire.FfRumble, (short)3, (ushort)40000, (ushort)12000), fx);
    }

    [Fact]
    public void Input_events_are_split_by_record()
    {
        var buf = new byte[48];
        void Put(int i, ushort t, ushort c, int v) { BitConverter.TryWriteBytes(buf.AsSpan(i * 24 + 16, 2), t); BitConverter.TryWriteBytes(buf.AsSpan(i * 24 + 18, 2), c); BitConverter.TryWriteBytes(buf.AsSpan(i * 24 + 20, 4), v); }
        Put(0, FfWire.EvUinput, 1, 77); Put(1, FfWire.EvFf, 2, 1);
        Assert.Equal([(FfWire.EvUinput, (ushort)1, 77), (FfWire.EvFf, (ushort)2, 1)], FfWire.Events(buf, 48, 8));
        Assert.Single(FfWire.Events(buf, 47, 8)); // a partial trailing record is ignored
    }

    [Fact]
    public void Mix_takes_the_strongest_of_each_motor_among_playing_effects()
    {
        var fx = new Dictionary<int, (ushort, ushort)> { [0] = (100, 900), [1] = (500, 10), [2] = (60000, 60000) };
        Assert.Equal(((ushort)500, (ushort)900), FfWire.Mix(fx, [0, 1]));
        Assert.Equal(((ushort)0, (ushort)0), FfWire.Mix(fx, []));
        Assert.Equal(((ushort)0, (ushort)0), FfWire.Mix(fx, [9]));
    }
}
