using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;
public class MacroModelSlotsTests
{
    [Theory]
    [InlineData("x20_pro",6)] [InlineData("x20",4)] [InlineData("dune",4)]
    [InlineData("x05_pro",2)] [InlineData("x10",2)] [InlineData("d10",2)] [InlineData("x15",2)]
    public void Physical_buttons_match_model_and_agent(string model,int count)
    {
        var draft=new MacroDraft(model);
        Assert.Equal(count,draft.PhysicalSlots.Count);
        using var catalog=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json")));
        var entry=catalog.RootElement.EnumerateArray().Single(m=>m.GetProperty("id").GetString()==model);
        var expected=entry.GetProperty("macroSlots").EnumerateArray().Select(s=>s.GetString()!).ToArray();
        Assert.Equal(expected,draft.PhysicalSlots);
        Assert.Equal(draft.PhysicalSlots,AgentVocabulary.Paddles(model));
        if(count>0)Assert.Equal(count,draft.SlotNames.Length);
    }
    [Fact] public void Pro_M5_and_M6_keep_independent_sequences_and_survive_pc_save_load()
    {
        var macros=new MacroDraft("x20_pro"){PcMode=true};
        for(int i=0;i<6;i++){var slot=macros.SlotNames[i];macros.Add(slot,i*5);macros.SetTrigger(slot,MacroDraft.TriggerInputs[i]);}
        var buttons=new ButtonDraft("x20_pro"){PcMode=true};var curves=new CurveDraft("x20_pro"){PcMode=true};
        var saved=PcModeDrafts.Merge(new(),buttons,curves,macros);saved.Validate();Assert.Equal(6,saved.Macros.Count);
        var restored=new MacroDraft("x20_pro"){PcMode=true};PcModeDrafts.Load(saved,buttons,curves,restored);
        for(int i=0;i<6;i++){var slot=restored.SlotNames[i];Assert.Single(restored[slot]);Assert.Equal(i*5,restored[slot][0].PauseMs);Assert.Equal(MacroDraft.TriggerInputs[i],restored.Trigger(slot));}
        restored.Clear("M5");Assert.Single(restored["M6"]);
    }
    [Fact] public void Two_button_pad_does_not_create_M3_or_M4()
    {
        var draft=new MacroDraft("x10"){PcMode=true};Assert.DoesNotContain("M3",draft.SlotNames);Assert.Throws<ArgumentException>(()=>draft.SetTrigger("M3","A"));
    }
}
