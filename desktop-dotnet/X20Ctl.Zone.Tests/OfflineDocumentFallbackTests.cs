using X20Ctl.Zone.Agent;
namespace X20Ctl.Zone.Tests;
public sealed class OfflineDocumentFallbackTests
{
    [Fact]
    public async Task Unrecognised_request_does_not_dump_a_retrieved_readme()
    {
        var reply = await new OfflineAgent().Chat([
            new ChatMessage("system", "From the X20CTL docs\n[1] README.md Introduction: PROMOTIONAL_SENTINEL Download the latest release."),
            new ChatMessage("user", "banana telescope")
        ], AgentSession.Schema, default);
        Assert.DoesNotContain("PROMOTIONAL_SENTINEL", reply.Content);
        Assert.DoesNotContain("Here's what the docs say", reply.Content);
    }
}

