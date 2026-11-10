using X20Ctl.Zone.Agent;

namespace X20Ctl.Zone.Tests;

/// <summary>The easter egg: "I love you" on its own is answered with love; a request that only mentions love is not.</summary>
public sealed class AgentLoveTests
{
    [Theory]
    [InlineData("I love you")]
    [InlineData("i love u")]
    [InlineData("love you!")]
    [InlineData("I LOVE YOU SO MUCH")]
    [InlineData("i love you, controller ❤")]
    [InlineData("luv ya xd")]
    [InlineData("ily")]
    [InlineData("<3")]
    public void Declarations_are_love(string said) => Assert.True(AgentCharacter.SaysLove(said));

    [Theory]
    [InlineData("I love you but my sticks drift")]
    [InlineData("I love the triggers")]
    [InlineData("do you love me?")]
    [InlineData("make the sticks feel like you love aiming")]
    [InlineData("lower the deadzone")]
    public void Requests_are_not(string said) => Assert.False(AgentCharacter.SaysLove(said));

    [Fact]
    public void It_says_it_back() => Assert.Contains("I love you too", AgentCharacter.LoveBack());

    [Fact]
    public void Dizzy_lines_change_with_how_dizzy()
    {
        var first = new[] { AgentCharacter.Dizzy(1), AgentCharacter.Dizzy(1), AgentCharacter.Dizzy(1) };
        var more = AgentCharacter.Dizzy(2);
        Assert.All(first, line => Assert.False(string.IsNullOrWhiteSpace(line)));
        Assert.DoesNotContain(more, first);
        Assert.False(string.IsNullOrWhiteSpace(AgentCharacter.Recovered()));
    }
}

public sealed class AgentPadColourTests
{
    [Theory]
    [InlineData("Change my controller to green", "green")]
    [InlineData("make my pad red", "red")]
    [InlineData("paint the controller dark purple", "dark purple")]
    [InlineData("change my controller colour to blue please", "blue")]
    public void A_colour_for_the_controller_is_read_whole(string said, string colour) => Assert.Equal(colour, AgentColors.Asked(said));

    [Theory]
    [InlineData("change my controller")]
    [InlineData("change my controller to the x05")]
    [InlineData("make my controller faster")]
    public void Not_a_colour(string said) => Assert.Null(AgentColors.Asked(said));
}
