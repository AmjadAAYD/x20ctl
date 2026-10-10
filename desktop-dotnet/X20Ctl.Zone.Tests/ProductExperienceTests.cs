namespace X20Ctl.Zone.Tests;
public class ProductExperienceTests
{
    [Fact]public void HeldKeysRemainHeldUntilReleaseAndDiagonalsKeepFullRadialIntensity()
    {
        var keys=new KeyboardSimulation("x20");keys.Enable();keys.Press(SimulationKey.W);Assert.Equal(1,keys.Frame().LeftY);keys.Press(SimulationKey.W);Assert.Equal(1,keys.Frame().LeftY);
        keys.Press(SimulationKey.D);var diagonal=keys.Frame();Assert.Equal(1,Math.Sqrt(diagonal.LeftX*diagonal.LeftX+diagonal.LeftY*diagonal.LeftY),8);
        keys.Release(SimulationKey.W);Assert.Equal(1,keys.Frame().LeftX);Assert.Equal(0,keys.Frame().LeftY);keys.Clear();Assert.Equal(0,keys.Frame().LeftX);
    }
    [Fact]public void BothTriggersAndRightStickAreIndependentAndResetNeverBecomesHardwareEvidence()
    {
        var keys=new KeyboardSimulation("x20");keys.Enable();keys.Press(SimulationKey.Q);keys.Press(SimulationKey.E);keys.Press(SimulationKey.Up);Assert.Equal(1,keys.Frame().LT);Assert.Equal(1,keys.Frame().RT);Assert.Equal(1,keys.Frame().RightY);Assert.True(keys.Frame().IsSimulation);
        keys.Release(SimulationKey.Q);Assert.Equal(0,keys.Frame().LT);Assert.Equal(1,keys.Frame().RT);keys.Disable();Assert.Empty(keys.Held);Assert.Equal(0,keys.Frame().RT);Assert.False(keys.Press(SimulationKey.W));
    }
    [Fact]public void IntroSeenPersistsAndUnknownOrCorruptPreferencesCannotBeOverwritten()
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"experience-fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string path=Path.Combine(directory,"preferences.json");var store=new ProductPreferences(path);store.Load();Assert.False(store.IntroSeen);store.MarkIntroSeen();var second=new ProductPreferences(path);second.Load();Assert.True(second.IntroSeen);
        File.WriteAllText(path,"{\"version\":99,\"keep\":true}");var future=new ProductPreferences(path);Assert.Throws<InvalidDataException>(future.Load);Assert.Throws<InvalidOperationException>(future.MarkIntroSeen);Assert.Contains("keep",File.ReadAllText(path));
    }
}
