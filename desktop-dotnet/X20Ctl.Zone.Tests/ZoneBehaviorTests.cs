using X20Ctl.Simulation;
namespace X20Ctl.Zone.Tests;

public class ZoneBehaviorTests
{
    [Fact]
    public void StartupIsAnUnassignedOverview()
    {
        var zone = new ZoneState();
        Assert.Equal(4, zone.Players.Count);
        Assert.Null(zone.FocusedPlayer);
        Assert.All(zone.Players, player => { Assert.Null(player.Model); Assert.False(player.IsMockConnected); });
    }

    [Theory]
    [InlineData(-1)] [InlineData(4)]
    public void InvalidPlayerIsRejected(int index) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneState().Activate(index));

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EmptyPlayerActivationRetainsClickedPlayerAndDoesNotAssignHardware(int index)
    {
        var zone = new ZoneState();
        zone.Activate(index);
        Assert.Equal(index, zone.SelectedPlayer);
        Assert.Contains($"Player {index + 1}", zone.Feedback);
        Assert.Null(zone.Players[index].Model);
        Assert.False(zone.Players[index].IsMockConnected);
    }

    [Fact]
    public void SwitchingAndCollapsingFocusPreservesEveryAssignment()
    {
        var zone = new ZoneState(ZoneScenario.MultipleControllers);
        var assignments = zone.Players.ToArray();
        zone.Activate(0);
        Assert.Equal(0, zone.FocusedPlayer);
        zone.Activate(1);
        Assert.Equal(1, zone.FocusedPlayer);
        zone.Collapse();
        Assert.Null(zone.FocusedPlayer);
        Assert.Equal(assignments, zone.Players);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AnyEmptyPlayerCanBecomeHero(int index)
    {
        var zone = new ZoneState(); zone.Activate(index);
        Assert.Equal(index, zone.FocusedPlayer);
        Assert.Null(zone.Players[index].Model);
    }

    [Fact]
    public void PopulatedFixtureDoesNotAutomaticallyFocusPlayerOne()
    {
        var zone = new ZoneState(ZoneScenario.OneController);
        Assert.Equal("EasySMX X20", zone.Players[0].Model);
        Assert.Null(zone.FocusedPlayer);
    }

    [Theory]
    [InlineData(948, 390)] [InlineData(1128, 490)]
    public void OverviewIsEqualAndAllPlayersReceiveTheSameHeroBounds(double width, double height)
    {
        var overview = ZoneLayout.Calculate(width, height, null);
        Assert.All(overview, dock => { Assert.Equal(overview[0].Width, dock.Width); Assert.Equal(overview[0].Height, dock.Height); });
        var hero = ZoneLayout.Calculate(width, height, 0)[0];
        for (int index = 0; index < 4; index++)
        {
            var layout = ZoneLayout.Calculate(width, height, index);
            Assert.Equal(hero, layout[index]);
            for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++) Assert.False(layout[a].Overlaps(layout[b]));
        }
    }

    [Theory]
    [InlineData(InputOwner.TesterCapture)] [InlineData(InputOwner.ScannerCapture)]
    [InlineData(InputOwner.MacroRecording)] [InlineData(InputOwner.RemapCapture)]
    public void EachCaptureOwnerBlocksActivationAndRequiresFreshPressOnReturn(InputOwner owner)
    {
        var nav = new ZoneNavigation();
        nav.Read(Frame(0, Buttons.None), true, InputOwner.UiNavigation);
        Assert.Equal(NavigationAction.None, nav.Read(Frame(10, Buttons.A | Buttons.RB), true, owner));
        Assert.Equal(NavigationAction.None, nav.Read(Frame(20, Buttons.A | Buttons.RB), true, InputOwner.UiNavigation));
        nav.Read(Frame(30, Buttons.None), true, InputOwner.UiNavigation);
        Assert.Equal(NavigationAction.Activate, nav.Read(Frame(40, Buttons.A), true, InputOwner.UiNavigation));
    }

    [Fact]
    public void HeldInputOnForegroundReturnCannotActivate()
    {
        var nav = new ZoneNavigation();
        nav.Read(Frame(0, Buttons.None), true, InputOwner.UiNavigation);
        nav.Read(Frame(10, Buttons.None), false, InputOwner.UiNavigation);
        Assert.Equal(NavigationAction.None, nav.Read(Frame(20, Buttons.A), true, InputOwner.UiNavigation));
        nav.Read(Frame(30, Buttons.None), true, InputOwner.UiNavigation);
        Assert.Equal(NavigationAction.Activate, nav.Read(Frame(40, Buttons.A), true, InputOwner.UiNavigation));
        Assert.Equal(NavigationAction.None, nav.Read(Frame(50, Buttons.A), true, InputOwner.UiNavigation));
    }

    private static InputFrame Frame(double time, Buttons buttons) => new(0, true, "simulation:zone-keys", time, buttons);
}
