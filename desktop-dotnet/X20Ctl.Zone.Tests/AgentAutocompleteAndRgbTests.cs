using X20Ctl.Zone.Agent;
using X20Ctl.Zone.Lighting;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class AgentAutocompleteAndRgbTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("make my led", "s to ambient")]
    [InlineData("make my led ", "to ambient")]
    [InlineData("make my led c", "yan")]
    [InlineData("Change my RGB to ", "ambient")]
    [InlineData("Change my RGB ", "to ambient")]
    [InlineData("Change my RGB to amb", "ient")]
    [InlineData("set lights to moo", "nlight pulse")]
    [InlineData("set lights to batt", "ery mode")]
    [InlineData("Enable a", "mbient mode")]
    [InlineData("My right stick ", "drifts")]
    [InlineData("make my aim ", "more precise")]
    [InlineData("remap LB ", "to B")]
    [InlineData("Open ", "diagnostics")]
    [InlineData("open c", "urves")]
    [InlineData("how do i ", "use ambient mode?")]
    public void AutocompletePredictsExpectedSuffix(string typed, string expectedSuffix)
    {
        Assert.Equal(expectedSuffix, AgentAutocomplete.Suggest(typed));
    }

    [Theory]
    [InlineData("ambient", LightingEffectKind.ScreenAmbient)]
    [InlineData("ambient mode", LightingEffectKind.ScreenAmbient)]
    [InlineData("screen sync", LightingEffectKind.ScreenAmbient)]
    [InlineData("ambilight", LightingEffectKind.ScreenAmbient)]
    [InlineData("battery", LightingEffectKind.BatteryLevel)]
    [InlineData("battery mode", LightingEffectKind.BatteryLevel)]
    [InlineData("battery lights", LightingEffectKind.BatteryLevel)]
    [InlineData("palette jump", LightingEffectKind.PaletteStep)]
    [InlineData("multi-color jump", LightingEffectKind.PaletteStep)]
    [InlineData("game auto", LightingEffectKind.GameAutoProfile)]
    [InlineData("audio reactive", LightingEffectKind.AudioReactive)]
    [InlineData("music sync", LightingEffectKind.AudioReactive)]
    [InlineData("sound reactive", LightingEffectKind.AudioReactive)]
    [InlineData("beat sync", LightingEffectKind.AudioReactive)]
    [InlineData("audio mode", LightingEffectKind.AudioReactive)]
    [InlineData("beat shift", LightingEffectKind.AudioReactive)]
    [InlineData("bass fire", LightingEffectKind.AudioReactive)]
    [InlineData("drop strobe", LightingEffectKind.AudioReactive)]
    [InlineData("music aurora", LightingEffectKind.AudioReactive)]
    public void AgentLightingEffectsParsesAllStudioAndSyncModes(string text, LightingEffectKind expected)
    {
        Assert.True(AgentLightingEffects.TryParse(text, out var spec, out _));
        Assert.Equal(expected, spec.Kind);
    }

    [Theory]
    [InlineData("enable ambient mode")]
    [InlineData("change my rgb to ambient")]
    [InlineData("switch lights to battery mode")]
    [InlineData("set rgb to palette jump")]
    [InlineData("turn on screen sync")]
    [InlineData("enable audio reactive")]
    [InlineData("set lights to music sync")]
    [InlineData("turn on beat sync")]
    public void AskedDetectsNewRgbAndSyncRequests(string sentence)
    {
        Assert.NotNull(AgentLightingEffects.Asked(sentence));
    }

    [Fact]
    public void EffectLabelsIncludeAmbientAndBattery()
    {
        Assert.Equal("Ambient Screen Sync", LightingEffectSpec.EffectLabel(LightingEffectKind.ScreenAmbient));
        Assert.Equal("Audio Reactive RGB", LightingEffectSpec.EffectLabel(LightingEffectKind.AudioReactive));
        Assert.Equal("Battery Level Indicator", LightingEffectSpec.EffectLabel(LightingEffectKind.BatteryLevel));
    }

    [Fact]
    public void OfflineAgentHandlesLightingTyposDongleBleAndRejectsGibberishOrBanana()
    {
        var agent = new OfflineAgent();

        var ambent = agent.Answer("turn on ambent");
        Assert.Single(ambent.Actions);
        Assert.Equal(AgentVocabulary.LedEffect, ambent.Actions[0].Action);
        Assert.True(AgentLightingEffects.TryParse((string)ambent.Actions[0].Value, out var ambSpec, out _));
        Assert.Equal(LightingEffectKind.ScreenAmbient, ambSpec.Kind);

        var rainbow = agent.Answer("turn on rainbow");
        Assert.Single(rainbow.Actions);
        Assert.Equal(AgentVocabulary.LedEffect, rainbow.Actions[0].Action);
        Assert.True(AgentLightingEffects.TryParse((string)rainbow.Actions[0].Value, out var rbSpec, out _));
        Assert.Equal(LightingEffectKind.NativeRainbow, rbSpec.Kind);

        var banana = agent.Answer("turn on banana");
        Assert.Empty(banana.Actions);

        var gibberish = agent.Answer("zxcvbnm qwertyy 123");
        Assert.Empty(gibberish.Actions);

        Assert.Equal(AgentCommand.Access, AgentCommands.Parse("can u see my controller?"));

        var dongle = agent.Answer("how does the dongle work with bluetooth?");
        Assert.Empty(dongle.Actions);
        Assert.Contains("2.4 GHz", dongle.Speech, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("d7f010e0", dongle.Speech, StringComparison.OrdinalIgnoreCase);
    }
}
