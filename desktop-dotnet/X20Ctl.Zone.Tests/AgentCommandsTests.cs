using X20Ctl.Zone.Agent;
using Xunit;
namespace X20Ctl.Zone.Tests;

public class AgentCommandsTests
{
    [Theory]
    [InlineData("EasySMX X20 Pro", AgentCommand.AboutController)]
    [InlineData("easysmx x20 pro?", AgentCommand.AboutController)]
    [InlineData("What is the X20 Pro?", AgentCommand.AboutController)]
    [InlineData("tell me about my x20", AgentCommand.AboutController)]
    [InlineData("What's my current battery?", AgentCommand.Battery)]
    [InlineData("how much battery do I have left", AgentCommand.Battery)]
    [InlineData("battery", AgentCommand.Battery)]
    [InlineData("Change controller", AgentCommand.ChangeController)]
    [InlineData("I want to switch to another controller", AgentCommand.ChangeController)]
    [InlineData("switch to my X20", AgentCommand.ChangeController)]
    [InlineData("I want to use a different controller", AgentCommand.ChangeController)]
    [InlineData("take me to the controller zone", AgentCommand.ChangeController)]
    [InlineData("Change my controller to green", AgentCommand.None)]
    [InlineData("change the controller lights to blue", AgentCommand.None)]
    [InlineData("switch my controller's A and B", AgentCommand.None)]
    [InlineData("can you read my controller?", AgentCommand.Access)]
    [InlineData("do you have access to my controller settings", AgentCommand.Access)]
    [InlineData("can you write to my controller", AgentCommand.Access)]
    [InlineData("I want to give a star", AgentCommand.Star)]
    [InlineData("star the repo", AgentCommand.Star)]
    [InlineData("I want to donate", AgentCommand.Donate)]
    [InlineData("how can I support the developer?", AgentCommand.Donate)]
    public void Recognised(string sentence, AgentCommand command) => Assert.Equal(command, AgentCommands.Parse(sentence));

    [Theory]
    [InlineData("Make my sticks smooth")]
    [InlineData("Turbo on X, 12 presses a second")]
    [InlineData("Swap A and B")]
    [InlineData("Is the X20 Pro supported?")]
    [InlineData("How many hours does the X20's battery last?")]
    [InlineData("Does X20CTL work on a PlayStation 5?")]
    [InlineData("My right stick drifts a little")]
    [InlineData("Put jump on M1")]
    [InlineData("sticks")]
    public void Settings_and_questions_are_left_to_the_agent(string sentence) => Assert.Equal(AgentCommand.None, AgentCommands.Parse(sentence));

    [Fact]
    public void Battery_answers_in_character()
    {
        Assert.Contains("full", AgentCommands.Battery("full", "X20 Pro"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cable", AgentCommands.Battery("wired", "X20 Pro"));
        Assert.Contains("X20 Pro connected", AgentCommands.Battery(null, "X20 Pro"));
        // the controller's own reading over Bluetooth (owner, 8 Nov 2026: connected by USB and Bluetooth, yet "not connected")
        Assert.Contains("3 of 4", AgentCommands.Battery("3/4", "X20 Pro"));
        Assert.Contains("charging", AgentCommands.Battery("2/4 (Charging)", "X20 Pro"));
        Assert.DoesNotContain("charging", AgentCommands.Battery("4/4", "X20 Pro"));
        Assert.DoesNotContain("Windows says", AgentCommands.Battery("1/4", "X20 Pro"));
    }
}

public class AgentOfflineTypoTests
{
    [Theory]
    [InlineData("make my stickss smooth", "stick_smooth")]
    [InlineData("hair trigers for shooting", "trigger_fast")]
    public async Task Offline_reads_typos(string sentence, string expected)
    {
        var turn = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro").Ask(sentence, new X20Ctl.Zone.PcMode.PcModeProfile());
        Assert.NotEmpty(turn.Outcome.Changes);
        Assert.Equal(expected, new OfflineAgent().Classify(AgentSpelling.Fix(sentence)).Intent?.Id);
    }
}
