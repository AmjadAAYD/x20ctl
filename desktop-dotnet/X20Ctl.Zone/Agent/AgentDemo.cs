using System.Text.Json;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>Fixed teaching examples. No model, retrieval, measurements or device access.</summary>
public static class AgentDemo
{
    public const string Label = "Canned demo · deterministic · preview only · no model";
    public static IReadOnlyList<string> Prompts { get; } = Array.AsReadOnly(new[]
    {
        "demo aim", "demo trigger", "demo bad output", "demo reset"
    });

    public static string Output(string sentence)
    {
        var key = sentence.Trim().ToLowerInvariant();
        object[] actions = key switch
        {
            "demo aim" => [new { action = "stick_curve_points", target = "RightStick", value = (object)"35,20,75,62", reason = "Fixed demo example" }],
            "demo trigger" => [new { action = "trigger_full_at", target = "RightTrigger", value = (object)40, reason = "Fixed demo example" }],
            "demo bad output" => [new { action = "stick_deadzone", target = "RightStick", value = (object)260, reason = "Deliberately outside the range" }],
            "demo reset" => [new { action = "reset", target = "All", value = (object)0, reason = "Reset demo preview" }],
            _ => []
        };
        string speech = key switch
        {
            "demo aim" => "Canned demo: a fixed right-stick aiming curve.",
            "demo trigger" => "Canned demo: right trigger reaches full output at 40%.",
            "demo bad output" => "Canned demo: this 260% dead zone must be rejected.",
            "demo reset" => "Canned demo: reset the preview settings.",
            _ => "Canned demo: choose demo aim, demo trigger, demo bad output or demo reset."
        };
        return JsonSerializer.Serialize(new { speech, actions, recap = "Scripted teaching example; no inference or hardware measurement.", assumptions = Array.Empty<string>(), sources = Array.Empty<int>(), question = "" });
    }

    public static AgentTurn Run(string sentence, string modelId, PcModeProfile current)
    {
        var output = Output(sentence);
        var verdict = ProposalValidator.Check(output, modelId);
        return new(sentence, [new ModelReply(output, 0, 0, 0, 0)], verdict, AgentApplier.Apply(current, verdict));
    }
}
