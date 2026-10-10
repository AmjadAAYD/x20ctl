using System.Text.Json;
namespace X20Ctl.Zone.Tests;

public class StudioFlowTests
{
    private static ControllerCatalog Catalog() => new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
    [Fact] public void CatalogRetainsAllSevenModelsWithoutGrantingNonX20Configuration()
    {
        var catalog = Catalog(); Assert.Equal(7, catalog.Models.Count);
        Assert.Equal("SUPPORTED", catalog.Get("x20").Support);
        Assert.All(catalog.Models.Where(m => m.Id != "x20"), model => Assert.NotEqual("SUPPORTED", model.Support));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AssignmentChangesOnlyClickedPlayerAndKeepsItHero(int index)
    {
        var zone = new ZoneState(); zone.Assign(index, Catalog().Get("x20"));
        Assert.Equal("EasySMX X20", zone.Players[index].Model);
        Assert.Equal(index, zone.FocusedPlayer); Assert.False(zone.Players[index].IsMockConnected);
        Assert.All(zone.Players.Where((p, i) => i != index), player => Assert.Null(player.Model));
        zone.Assign(index, Catalog().Get("x15")); Assert.Equal("EasySMX X15", zone.Players[index].Model);
    }
    [Fact] public void AssignmentRoundTripRestoresOverviewWithoutConnectionClaims()
    {
        InTemporaryDirectory(path => { var store = new AssignmentStore(path); store.Load(); store.Save([null, "x15", "x20", null]);
            var ids = new AssignmentStore(path).Load(); Assert.Equal(new string?[] {null, "x15", "x20", null}, ids);
            var zone = new ZoneState(); zone.Restore(ids, Catalog()); Assert.Null(zone.FocusedPlayer);
            Assert.Equal("EasySMX X20", zone.Players[2].Model); Assert.All(zone.Players, p => Assert.False(p.IsMockConnected)); });
    }
    [Fact] public void CorruptAssignmentFileIsNeverOverwritten()
    {
        InTemporaryDirectory(path => { File.WriteAllText(path, "broken"); var store = new AssignmentStore(path);
            Assert.ThrowsAny<Exception>(() => store.Load()); Assert.ThrowsAny<Exception>(() => store.Save(["x20", null, null, null])); Assert.Equal("broken", File.ReadAllText(path)); });
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":1,\"players\":null}")]
    [InlineData("{\"version\":2,\"players\":[null,null,null,null]}")]
    public void MalformedSchemaIsRejectedAsDataErrorAndPreserved(string json)
    {
        InTemporaryDirectory(path => { File.WriteAllText(path, json); var store = new AssignmentStore(path);
            Assert.Throws<InvalidDataException>(() => store.Load());
            Assert.Throws<InvalidOperationException>(() => store.Save([null, null, null, null])); Assert.Equal(json, File.ReadAllText(path)); });
    }
    [Fact] public void UnknownFieldsSurviveSaveAndConcurrentChangesAreRefused()
    {
        InTemporaryDirectory(path => { File.WriteAllText(path, "{\"version\":1,\"players\":[null,null,null,null],\"future\":{\"value\":42}}");
            var store = new AssignmentStore(path); store.Load(); store.Save(["x20", null, null, null]);
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); Assert.Equal(42, doc.RootElement.GetProperty("future").GetProperty("value").GetInt32());
            File.WriteAllText(path, "external change"); Assert.Throws<IOException>(() => store.Save([null, null, null, null])); Assert.Equal("external change", File.ReadAllText(path)); });
    }
    [Fact] public void DraftEditsCountChangesWithoutChangingHardwareTruth()
    {
        var draft = new ButtonDraft("x20"); draft.Set("A", "B"); draft.Set("X", "Y");
        Assert.Equal(2, draft.UnsentChanges); Assert.Equal("B", draft.Target("A"));
        Assert.False(draft.HardwareStateKnown); Assert.False(draft.CanApplyHardware);
        draft.Reset("A"); Assert.Equal(1, draft.UnsentChanges); Assert.Equal("A", draft.Target("A"));
        draft.Set("X", "X"); Assert.Equal(0, draft.UnsentChanges);
    }
    [Theory] [InlineData("x15", "A")] [InlineData("x20", "M1")] [InlineData("x20", "SELECT")]
    public void UnsupportedRemappingIsRefused(string model, string source)
    {
        var draft = new ButtonDraft(model); Assert.False(draft.CanEdit(source));
        Assert.Throws<InvalidOperationException>(() => draft.Set(source, "A")); Assert.Equal(0, draft.UnsentChanges);
    }
    [Fact] public void InvalidTargetCannotCreateAnUnsentChange()
    {
        var draft = new ButtonDraft("x20"); Assert.Throws<ArgumentException>(() => draft.Set("A", "INVENTED")); Assert.Equal(0, draft.UnsentChanges);
    }
    private static void InTemporaryDirectory(Action<string> test)
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-assignment-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { test(Path.Combine(dir, "assignments.json")); } finally { Directory.Delete(dir, true); }
    }
}
