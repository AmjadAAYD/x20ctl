using X20Ctl.Zone.PcMode;

public class PcModeEntryTests
{
    [Theory]
    [InlineData("x20", false)]
    [InlineData("x20_pro", false)]
    [InlineData("dune", true)]
    [InlineData("x05_pro", true)]
    public void Only_the_x20_has_no_entry(string model, bool available) =>
        Assert.Equal(available, PcModeEntry.IsAvailable(model));

    [Fact]
    public void Offers_only_types_that_exist_and_names_them() =>
        Assert.All(PcModeEntry.Offered, t => Assert.False(string.IsNullOrWhiteSpace(PcModeEntry.DisplayName(t))));

    [Fact]
    public void Warning_mentions_ban_risk_and_own_risk() =>
        Assert.Contains("ban", PcModeEntry.Warning) ;
}
