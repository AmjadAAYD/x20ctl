using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

/// <summary>People who don't write English well still get what they asked for, and see how they were read.</summary>
public sealed class AgentGrammarTests
{
    [Theory]
    [InlineData("i want that you make the stikk more fast", "make the stick faster")]
    [InlineData("can you to make the left stick more smooth", "make the left stick smoother")]
    [InlineData("put the deadzone in ten", "set the dead zone to 10")]
    [InlineData("put left stick dead zone at fifteen percent", "set left stick dead zone to 15%")]
    [InlineData("set right stick dead zone to twenty five", "set right stick dead zone to 25")]
    [InlineData("i want to change the right stick deadzone", "change the right stick dead zone")]
    [InlineData("make the right stick to be more fast", "make the right stick faster")]
    [InlineData("i want the right stick more sensible", "make the right stick more sensitive")]
    [InlineData("how i can reset my sticks", "how can I reset my sticks")]
    [InlineData("pls make mouse mode on", "please make mouse mode on")]
    [InlineData("my controller dont connect", "my controller won't connect")]
    [InlineData("the controller is not detected", "the controller is not detected")]
    [InlineData("the the left stick is drifting", "the left stick is drifting")]
    [InlineData("set the leftstick dead zone = 8", "set the left stick dead zone to 8")]
    [InlineData("ten", "10")]
    [InlineData("one", "one")]
    [InlineData("swap the buttons a and b", "swap A and B")]
    [InlineData("please make the right stick dead zone five", "please make the right stick dead zone 5")]
    [InlineData("turn on turbo on button x", "turn on turbo on button X")]
    [InlineData("take a look at my sticks", "take a look at my sticks")]
    public void A_broken_sentence_becomes_plain_english(string said, string plain) => Assert.Equal(plain, AgentGrammar.Plain(said), ignoreCase: true);

    [Theory]
    [InlineData("Make the right stick less sensitive")]
    [InlineData("Set left stick dead zone to 8")]
    [InlineData("Turn mouse mode on")]
    [InlineData("What does a hair trigger do?")]
    [InlineData("Swap A and B")]
    [InlineData("Reset my sticks")]
    public void A_clear_sentence_comes_back_as_it_was(string clear)
    {
        Assert.Equal(clear, AgentGrammar.Plain(clear));
        Assert.Null(AgentGrammar.Reading(clear, AgentGrammar.Plain(clear)));
    }

    [Theory]
    [InlineData("i want that you set the left stik dead zone in eight", "Set left stick dead zone to 8")]
    [InlineData("can you to put the right stick deadzone at ten percent", "Set right stick dead zone to 10")]
    [InlineData("pls i want mouse mode on", "Turn mouse mode on")]
    [InlineData("i no want mouse mode", "Turn mouse mode off")]
    [InlineData("i want to reset my sticks", "Reset my sticks")]
    [InlineData("swap the buttons a and b", "Swap A and B")]
    [InlineData("please make the right stick dead zone five", "Set right stick dead zone to 5")]
    public async Task A_broken_request_does_what_the_clear_one_does(string broken, string clear)
    {
        var a = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro").Ask(broken, new());
        var b = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro").Ask(clear, new());
        Assert.True(b.Verdict.Valid, "the clear sentence must work: " + clear);
        Assert.True(a.Verdict.Valid, "the broken sentence must work: " + broken);
        Assert.Equal(b.Outcome.Changes.Select(c => c.What + c.After), a.Outcome.Changes.Select(c => c.What + c.After));
        Assert.Equal(b.Outcome.Mouse, a.Outcome.Mouse);
    }

    [Fact]
    public void The_reading_is_shown_only_when_the_words_really_changed()
    {
        Assert.Null(AgentGrammar.Reading("Make the stick faster!", "make the stick faster"));
        Assert.Null(AgentGrammar.Reading("pls make the stick faster", "please make the stick faster"));
        Assert.Equal("Set the dead zone to 10", AgentGrammar.Reading("put the deadzone in ten", "set the dead zone to 10"));
    }

    [Theory]
    [InlineData("make the camera slower", "RightStick", true)]
    [InlineData("make the camera faster", "RightStick", false)]
    [InlineData("reduce stick speed", "LeftStick,RightStick", true)]
    [InlineData("increase stick speed", "LeftStick,RightStick", false)]
    [InlineData("how to make the aim slower", "RightStick", true)]
    [InlineData("my camera turns too fast", "RightStick", true)]
    [InlineData("raise the camera sensitivity", "RightStick", false)]
    [InlineData("lower right stick sensitivity", "RightStick", true)]
    [InlineData("make my movement slower", "LeftStick", true)]
    [InlineData("slow down my aim", "RightStick", true)]
    [InlineData("the camera is going very fast, make it slow", "RightStick", true)]
    [InlineData("i want that the camera is more slow", "RightStick", true)]
    [InlineData("make my sticks slower", "LeftStick,RightStick", true)]
    public async Task Stick_speed_is_understood_in_any_words(string said, string sticks, bool slower)
    {
        var turn = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro").Ask(said, new());
        Assert.True(turn.Verdict.Valid, said);
        var targets = turn.Verdict.Proposal!.Actions.Where(a => a.Action == AgentVocabulary.StickCurvePoints).ToList();
        Assert.Equal(sticks.Split(','), targets.Select(a => a.Target).OrderBy(t => t));
        Assert.All(targets, a => Assert.Equal(slower ? "30,23,72,65" : "25,33,70,79", a.Value.ToString()));
    }

    [Theory]
    [InlineData("my camera keeps slowly turning by itself")]
    [InlineData("make my right trigger slower")]
    [InlineData("set right stick speed to 80")]
    [InlineData("make the mouse slower")]
    public void Speed_leaves_other_requests_alone(string said) => Assert.Null(OfflineAgent.Speed(said.ToLowerInvariant()));

    [Fact]
    public async Task A_bare_number_word_answers_the_agents_question()
    {
        var session = new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro");
        var asked = await session.Ask("set the right stick dead zone", new());
        Assert.NotEmpty(asked.Verdict.Proposal!.Question);
        var answered = await session.Ask("ten", new());
        Assert.Contains(answered.Outcome.Changes, c => c.After.Contains("10"));
    }
}
