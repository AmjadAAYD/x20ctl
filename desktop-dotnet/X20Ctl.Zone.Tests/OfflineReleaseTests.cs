using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;
public sealed class OfflineReleaseTests
{
    [Theory]
    [InlineData("Does the agent need internet?", "No internet")]
    [InlineData("My controller is not recognised", "USB")]
    [InlineData("Why do I get double input?", "HidHide")]
    [InlineData("What is circularity error?", "circle")]
    [InlineData("Can a dead zone repair a worn stick?", "repair")]
    [InlineData("Tell me about input lag", "polling")]
    [InlineData("How do I make a macro?", "cannot create")]
    public async Task Practical_questions_are_explained_without_mutation(string text,string expected)
    {
        var turn=await new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro").Ask(text,new());
        Assert.Empty(turn.Outcome.Changes); Assert.Contains(expected,turn.Verdict.Proposal!.Recap,StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("Could you enable mouse mode?",true)]
    [InlineData("I don't want mouse mode",false)]
    public async Task Politeness_and_negative_preference_keep_the_right_switch_direction(string sentence,bool on)
    {
        var turn=await new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro").Ask(sentence,new());
        Assert.True(turn.Verdict.Valid);Assert.Equal(on,turn.Outcome.Mouse);
    }
    [Theory]
    [InlineData("Don't enable mouse mode")]
    [InlineData("Could you not enable mouse mode?")]
    [InlineData("Please do not disable mouse mode")]
    [InlineData("Do not reset my sticks")]
    [InlineData("Right stick deadzone -8")]
    public async Task Negative_or_prohibited_requests_cannot_apply_a_positive_setting(string text)
    {
        var turn=await new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro").Ask(text,new());
        Assert.Empty(turn.Outcome.Changes);Assert.Null(turn.Outcome.Mouse);
    }
    [Fact] public async Task More_remembers_the_previous_curve_and_adjusts_its_current_points()
    {
        var session=new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro");
        var first=await session.Ask("Make my right stick precise for sniping",new());
        Assert.True(first.Outcome.Changes.Count>0);
        var more=await session.Ask("a little more",first.Outcome.Next!);
        Assert.True(more.Verdict.Valid);Assert.True(more.Outcome.Changes.Count>0);
        Assert.True(more.Outcome.Next!.Curves["RightStick"].Y1 < first.Outcome.Next!.Curves["RightStick"].Y1);
        Assert.False(more.Outcome.Next.Sticks.ContainsKey("LeftStick"));
    }
    [Fact] public async Task Compound_values_stay_with_the_named_controls()
    {
        var turn=await new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro").Ask("Set left stick dead zone to 6 and set right stick dead zone to 9",new());
        Assert.True(turn.Verdict.Valid);Assert.Equal(6,turn.Outcome.Next!.Sticks["LeftStick"].Inner);Assert.Equal(9,turn.Outcome.Next.Sticks["RightStick"].Inner);
    }
    [Fact] public async Task Ambiguous_pair_of_values_asks_instead_of_setting_both_to_the_first_number()
    {
        var turn=await new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro").Ask("Turbo on X at 12 and Y at 8",new());
        Assert.Empty(turn.Outcome.Changes);Assert.NotEmpty(turn.Verdict.Proposal!.Question);
    }
    [Fact] public async Task Replace_A_with_B_is_a_remap_not_hardware_repair_advice()
    {
        var turn=await new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro").Ask("Replace A with B",new());
        Assert.Equal("B",turn.Outcome.Next!.Remap["A"]);
    }
    [Fact] public async Task Vague_better_request_asks_for_a_goal_without_guessing()
    {
        var session=new AgentSession(new OfflineAgent(),"x20_pro","X20 Pro");
        var turn=await session.Ask("Make my controller better",new());
        Assert.Empty(turn.Outcome.Changes); Assert.True(session.HasPendingClarification);
        var answer=await session.Ask("steadier aim",new());
        Assert.True(answer.Outcome.Changes.Count>0);
    }
}
