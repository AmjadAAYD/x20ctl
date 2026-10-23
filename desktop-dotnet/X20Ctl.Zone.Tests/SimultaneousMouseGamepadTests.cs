using X20Ctl.Simulation;
using X20Ctl.Zone;

namespace X20Ctl.Zone.Tests;

public class SimultaneousMouseGamepadTests
{
    [Fact]
    public void PointerMode_IgnoresSmallJitter_AndActivatesOnActualMovement()
    {
        var pointer = new PointerMode();
        pointer.EnterDemo(100, 100);

        // Small jitter (radius <= 6 px, distance squared <= 36)
        Assert.False(pointer.Observe(102, 102));
        Assert.False(pointer.Observe(104, 104));

        // Real deliberate mouse movement (distance squared > 36)
        Assert.True(pointer.Observe(120, 120));

        // Once transitioned, further observations return false until demo/gamepad navigation is re-entered
        Assert.False(pointer.Observe(130, 130));
    }

    [Fact]
    public void ZoneNavigation_CanToggleBetweenHeroAndGrid_WithoutLosingFocus()
    {
        var zone = new ZoneState(ZoneScenario.MultipleControllers);
        Assert.Null(zone.FocusedPlayer);

        // Gamepad A or mouse click focuses player 0
        zone.Activate(0);
        Assert.Equal(0, zone.FocusedPlayer);

        // Gamepad B or Esc collapses back to grid
        zone.Collapse();
        Assert.Null(zone.FocusedPlayer);
        Assert.Equal(0, zone.SelectedPlayer);
    }
}
