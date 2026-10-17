using X20Ctl.Zone.Agent;
using Xunit;
namespace X20Ctl.Zone.Tests;

public class AgentIdeasTests
{
    [Fact]
    public void Nothing_typed_is_the_help_list_first()
    {
        var ideas = AgentIdeas.Search("");
        Assert.Equal(AgentHelp.Situations[0].Say, ideas[0].Say);
        Assert.All(AgentIdeas.All, i => Assert.NotEqual("", i.Intent));
        Assert.Equal(AgentIdeas.All.Count, AgentIdeas.All.Select(i => i.Say).Distinct().Count());
    }

    [Theory]
    [InlineData("sticks", "stick_")]
    [InlineData("Sticks", "stick_")]
    [InlineData("sti", "stick_")]
    [InlineData("triggers", "trigger_")]
    [InlineData("turbo", "turbo_")]
    [InlineData("tur", "turbo_")]
    [InlineData("paddle", "paddle_")]
    [InlineData("pc mode", "pc_mode_")]
    public void Typing_narrows_to_the_topic(string query, string prefix)
    {
        var ideas = AgentIdeas.Search(query);
        Assert.NotEmpty(ideas);
        Assert.StartsWith(prefix, ideas[0].Intent);
    }

    [Fact]
    public void Sticks_lists_only_stick_ideas_at_the_top()
    {
        var ideas = AgentIdeas.Search("sticks");
        Assert.True(ideas.Count >= 5);
        Assert.All(ideas.Take(5), i => Assert.StartsWith("stick_", i.Intent));
    }

    [Fact]
    public void Nonsense_finds_nothing() => Assert.Empty(AgentIdeas.Search("banana"));

    [Theory]
    [InlineData("Sticks", "Sticks")]
    [InlineData("sticks", "sticks")]
    [InlineData("dead zone", "dead zone")]
    [InlineData("my triggers?", "my triggers")]
    [InlineData("turbo", "turbo")]
    public void A_topic_alone_is_a_search(string sentence, string topic) => Assert.Equal(topic, AgentIdeas.Topic(sentence));

    [Theory]
    [InlineData("Turbo on X")]
    [InlineData("Swap A and B")]
    [InlineData("Make my sticks smooth")]
    [InlineData("Reset everything")]
    [InlineData("HELP")]
    [InlineData("hello")]
    public void A_request_is_not_a_search(string sentence) => Assert.Null(AgentIdeas.Topic(sentence));

    [Fact]
    public void The_reply_says_what_was_searched_in_different_words()
    {
        var replies = Enumerable.Range(0, 12).Select(_ => AgentIdeas.TopicReply("stickss")).ToList();
        Assert.All(replies, r => Assert.Contains("“sticks”", r));
        for (int i = 1; i < replies.Count; i++) Assert.NotEqual(replies[i - 1], replies[i]);
        Assert.True(replies.Distinct().Count() >= 3);
    }

    [Theory]
    [InlineData("stickss", "sticks")]
    [InlineData("Stiks", "sticks")]
    [InlineData("trigers", "triggers")]
    [InlineData("turob", "turbo")]
    [InlineData("paddels", "paddles")]
    public void Typos_are_read_as_meant(string typed, string meant) => Assert.Equal(meant, AgentSpelling.Word(typed).ToLowerInvariant());

    [Theory]
    [InlineData("lb")]
    [InlineData("rt")]
    [InlineData("m1")]
    [InlineData("sticks")]
    [InlineData("zzzzzzzz")]
    public void Known_and_short_words_are_left_alone(string word) => Assert.Equal(word, AgentSpelling.Word(word));

    [Fact]
    public void A_typo_search_finds_the_topic()
    {
        Assert.StartsWith("stick_", AgentIdeas.Search("stickss")[0].Intent);
        Assert.Equal("sticks", AgentIdeas.Topic("Stickss"));
        Assert.Equal(AgentCommand.Battery, AgentCommands.Parse("whats my batery"));
    }
}

/// <summary>The character: off-topic questions in character, and the mood the player lights show.</summary>
public class AgentCharacterTests
{
    static Task<AgentTurn> Ask(string sentence, string model = "x20_pro") =>
        new AgentSession(new OfflineAgent(), model, "X20 Pro") { Meter = new BenchMeter() }.Ask(sentence, new X20Ctl.Zone.PcMode.PcModeProfile());

    [Theory]
    [InlineData("What's 12 times 7?", "a calculator")]
    [InlineData("will it rain tomorrow?", "a weather app")]
    [InlineData("give me a pizza recipe", "a chef")]
    [InlineData("What is the capital of France?", "a history book")]
    [InlineData("Who is the best footballer?", "a search engine")]
    public void Off_topic_is_named(string sentence, string thing) => Assert.Equal(thing, AgentCharacter.OffTopic(sentence));

    [Theory]
    [InlineData("Make my sticks smooth")]
    [InlineData("What's good and bad about hair triggers?")]
    [InlineData("How much does the X20 Pro cost?")]
    [InlineData("Does X20CTL work on a PlayStation 5?")]
    [InlineData("How many hours does the X20's battery last?")]
    [InlineData("Turbo on X, 12 presses a second")]
    [InlineData("banana telescope")]
    public void Controller_talk_is_never_off_topic(string sentence) => Assert.Null(AgentCharacter.OffTopic(sentence));

    [Fact]
    public async Task Offline_answers_off_topic_in_character()
    {
        var turn = await Ask("What's 12 times 7?");
        Assert.True(AgentCharacter.IsSilly(turn.Verdict.Proposal!.Speech));
        Assert.Contains("a calculator", turn.Verdict.Proposal.Speech);
        Assert.Empty(turn.Outcome.Changes);
        Assert.Equal(AgentMood.Unsure, AgentCharacter.Mood(turn));
    }

    [Theory]
    [InlineData("banana telescope", AgentMood.Failed)]
    [InlineData("Put all my settings back to default", AgentMood.Unsure)]
    [InlineData("Turbo on X, 12 presses a second", AgentMood.Good)]
    [InlineData("Make my sticks smooth", AgentMood.Good)]
    [InlineData("What's good and bad about hair triggers?", AgentMood.Good)]
    [InlineData("hello", AgentMood.Good)]
    public async Task Mood_follows_the_answer(string sentence, AgentMood mood) => Assert.Equal(mood, AgentCharacter.Mood(await Ask(sentence)));

    [Fact]
    public void It_never_calls_itself_silly()
    {
        Assert.DoesNotContain("silly", AgentCharacter.Greeting("X20 Pro"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never describe yourself as silly", AgentCharacter.Persona);
    }

    [Fact]
    public void The_prompt_carries_the_character() => Assert.Contains(AgentCharacter.SillyCore, AgentSession.DefaultPrompt("X20 Pro", "x20_pro"));
}
