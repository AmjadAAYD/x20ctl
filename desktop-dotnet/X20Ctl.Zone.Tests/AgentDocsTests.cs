using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
using Xunit.Abstractions;
namespace X20Ctl.Zone.Tests;

/// <summary>M5: the docs are cut by heading, searched, offered to the model, and a citation it wasn't given is refused.</summary>
public sealed class AgentDocsTests(ITestOutputHelper output)
{
    static string Repo() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d != null && !File.Exists(Path.Combine(d.FullName, "README.md"))) d = d.Parent; return d!.FullName; }

    [Fact]
    public void Passages_keep_their_file_and_heading_path()
    {
        var chunks = AgentDocs.Chunk("G.md", "# Guide\n\nIntro words that are long enough to count as a passage here.\n\n## Tools\n\n### PC mode\n\nPC mode hands your settings to games through a virtual Xbox controller, see [the docs](x.md).\n\n```\ncode that is not prose at all and must be skipped entirely\n```\n");
        Assert.Equal(["Introduction", "Tools › PC mode"], chunks.Select(c => c.Section));
        Assert.Contains("see the docs.", chunks[1].Text);
        Assert.DoesNotContain("code that", chunks[1].Text);
    }

    [Fact]
    public void A_citation_the_model_was_not_given_is_refused()
    {
        const string reply = """{"speech":"s","actions":[],"recap":"r","assumptions":[],"sources":[4]}""";
        Assert.Contains("only [1] to [3]", ProposalValidator.Check(reply, "x20_pro", offered: 3).StructureError);
        Assert.Contains("no doc passages", ProposalValidator.Check(reply, "x20_pro").StructureError);
        Assert.True(ProposalValidator.Check(reply.Replace("[4]", "[2]"), "x20_pro", offered: 3).Valid);
        Assert.True(ProposalValidator.Check("""{"speech":"s","actions":[],"recap":"r","assumptions":[]}""", "x20_pro").Valid); // sources may be left out
    }

    [Fact]
    public async Task Keyword_search_finds_the_answering_section()
    {
        var search = new KeywordSearch(AgentDocs.Load(Repo()));
        int found = 0, total = 0;
        foreach (var c in AgentDocsBench.Cases.Where(c => !c.OutOfDocs))
        {
            var hits = await search.Search(c.Question, AgentSession.DocCount, default);
            total++; if (c.Found(hits)) found++; else output.WriteLine($"MISS {c.Question} -> {string.Join(" | ", hits.Select(h => h.Chunk.Where))}");
        }
        output.WriteLine($"keywords: {found}/{total} in the top {AgentSession.DocCount}");
        Assert.True(found >= total * 0.6, $"{found}/{total}");
    }

    [Fact]
    public async Task Offline_mode_answers_an_app_question_from_the_docs_and_cites_it()
    {
        var session = new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro") { Docs = new KeywordSearch(AgentDocs.Load(Repo())) };
        var turn = await session.Ask("I closed the app but it's still running", new PcModeProfile());
        Assert.True(turn.Verdict.Valid);
        Assert.Contains(turn.Cited, h => h.Chunk.Section.Contains("still running"));
    }
}
