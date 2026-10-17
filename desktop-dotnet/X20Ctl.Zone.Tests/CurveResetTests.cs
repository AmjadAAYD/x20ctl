using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;
public sealed class CurveResetTests
{
    [Fact] public void Reset_clears_curve_and_all_tuning_without_touching_other_channels_or_buttons()
    {
        var saved=new PcModeProfile();
        saved.Sticks["RightStick"]=new StickTuning { Inner=12, MaxOutput=80, AntiDeadzone=5, AxisRatio=120, SquareGate=true };
        saved.Sticks["LeftStick"]=new StickTuning { Inner=7 };
        saved.Curves["RightStick"]=new("custom","default",0,100,85,40,170,145);
        saved.Remap["A"]="B"; saved.Turbo["X"]=12;
        var next=PcModeDrafts.ResetCurves(saved,[CurveChannel.RightStick]);
        Assert.False(next.Sticks.ContainsKey("RightStick")); Assert.False(next.Curves.ContainsKey("RightStick"));
        Assert.Equal(7,next.Sticks["LeftStick"].Inner); Assert.Equal("B",next.Remap["A"]); Assert.Equal(12,next.Turbo["X"]);
        Assert.Equal(12,saved.Sticks["RightStick"].Inner);
        Assert.Equal(.5,PadProcessor.Response(null,null,.5),6);
    }
    [Fact] public async Task Agent_reset_curves_uses_response_targets_instead_of_resetting_everything()
    {
        var saved=new PcModeProfile(); saved.Sticks["RightStick"]=new StickTuning { Inner=12 }; saved.Remap["A"]="B";
        var turn=await new Agent.AgentSession(new NoModel(),"x20_pro","X20 Pro").Ask("Reset curves",saved);
        Assert.True(turn.Verdict.Valid); Assert.Empty(turn.Outcome.Next!.Sticks); Assert.Equal("B",turn.Outcome.Next.Remap["A"]);
    }
    sealed class NoModel : Agent.IAgentModel
    {
        public string Name=>"unused";
        public Task<Agent.ModelReply> Chat(IReadOnlyList<Agent.ChatMessage> messages,string schema,CancellationToken cancel)=>throw new Exception("No generation needed for literal reset.");
    }
    [Fact] public void Reset_all_clears_tuning_even_when_no_curve_points_were_changed()
    {
        var saved=new PcModeProfile(); saved.Sticks["LeftStick"]=new StickTuning { Inner=8 }; saved.Sticks["RightStick"]=new StickTuning { MaxOutput=80 };
        var next=PcModeDrafts.ResetCurves(saved,Enum.GetValues<CurveChannel>());
        Assert.Empty(next.Curves); Assert.Empty(next.Sticks); Assert.True(next.IsNeutral);
    }
}
