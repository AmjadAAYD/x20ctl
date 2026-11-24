using X20Ctl.Zone.Lighting;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class HapticsMixerTests
{
    [Fact]
    public void Mix_RespectsAllFourBlendModes()
    {
        var smart = new HapticsFrame(0.60, 0.50, 0.85, 0.70);

        var smartBlend = HapticsMixer.Mix(smart, 0.40, 0.0, NativeRumbleBlendMode.SmartBlend);
        Assert.True(smartBlend.LeftGrip >= 0.40 && smartBlend.LeftGrip <= 1.0);
        Assert.Equal(0.50, smartBlend.RightGrip, 3);
        Assert.Equal(0.85, smartBlend.LeftTrigger, 3);
        Assert.Equal(0.70, smartBlend.RightTrigger, 3);

        var trigOnly = HapticsMixer.Mix(smart, 0.30, 0.25, NativeRumbleBlendMode.TriggersOnly);
        Assert.Equal(0.30, trigOnly.LeftGrip, 3);
        Assert.Equal(0.25, trigOnly.RightGrip, 3);
        Assert.Equal(0.85, trigOnly.LeftTrigger, 3);
        Assert.Equal(0.70, trigOnly.RightTrigger, 3);

        var full = HapticsMixer.Mix(smart, 0.90, 0.90, NativeRumbleBlendMode.FullOverride);
        Assert.Equal(smart, full);

        var natPriorityActive = HapticsMixer.Mix(smart, 0.50, 0.10, NativeRumbleBlendMode.NativePriority);
        Assert.Equal(0.50, natPriorityActive.LeftGrip, 3);
        Assert.Equal(0.0, natPriorityActive.RightTrigger, 3);

        var natPriorityIdle = HapticsMixer.Mix(smart, 0.0, 0.0, NativeRumbleBlendMode.NativePriority);
        Assert.Equal(smart, natPriorityIdle);
    }
}
