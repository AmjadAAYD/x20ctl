namespace X20Ctl.Zone.Tests;

public class PadColourTests
{
    private static FinishCatalog Shipped() => new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "finishes.json")));

    [Fact] public void EveryModelStartsOnItsOriginalAndTheX20ProComesInWhite()
    {
        var catalog = Shipped();
        foreach (var model in new[] { "x20", "x20_pro", "x05_pro", "x10", "d10", "x15", "dune" })
            Assert.Equal(FinishCatalog.Original, catalog.For(model)[0].Id);
        Assert.Equal(["original", "white"], catalog.For("x20_pro").Select(f => f.Id));
        // shown until a player picks: the X20 Pro in white (its base colour is Grey), the Dune in black, the D10 in Farout Purple, the X15 in Wukong (owner, 24 Oct 2026)
        Assert.Equal(("white", "Grey"), (catalog.Default("x20_pro"), catalog.For("x20_pro")[0].Name));
        Assert.Equal("black", catalog.Default("dune")); Assert.Equal(("purple", "wukong"), (catalog.Default("d10"), catalog.Default("x15"))); Assert.Equal(FinishCatalog.Original, catalog.Default("x20"));
        Assert.Throws<InvalidDataException>(() => new FinishCatalog("{\"models\":{\"dune\":[{\"id\":\"original\",\"name\":\"Gold\",\"swatch\":\"#e0aa18\"}]},\"default\":{\"dune\":\"black\"}}"));
        Assert.Equal(17, new[] { "x20", "x20_pro", "x05_pro", "x10", "d10", "x15", "dune" }.Sum(m => catalog.For(m).Count));
    }

    [Fact] public void ChangeColourWalksEveryFinishAndWrapsRound()
    {
        var catalog = Shipped(); string? at = FinishCatalog.Original; var seen = new List<string>();
        for (int i = 0; i < catalog.For("d10").Count; i++) { at = catalog.Next("d10", at).Id; seen.Add(at); }
        Assert.Equal(catalog.For("d10").Select(f => f.Id).Skip(1).Append("original"), seen);
        Assert.Equal("original", catalog.Next("x20", "no_such_colour").Id);
    }

    [Fact] public void ColoursAreRememberedPerPlayerAndModel()
    {
        InTemporaryDirectory(path =>
        {
            var store = new PadColourStore(path); store.Load();
            Assert.Equal("original", store.Get(0, "x20_pro"));
            store.Set(0, "x20_pro", "white"); store.Set(1, "x20_pro", "original"); store.Set(0, "x20", "black");
            var again = new PadColourStore(path); again.Load();
            Assert.Equal("white", again.Get(0, "x20_pro")); Assert.Equal("original", again.Get(1, "x20_pro")); Assert.Equal("black", again.Get(0, "x20"));
        });
    }

    [Fact] public void StaleOrBrokenColourFilesAreNeverOverwritten()
    {
        InTemporaryDirectory(path =>
        {
            var first = new PadColourStore(path); first.Load(); var second = new PadColourStore(path); second.Load();
            first.Set(2, "x10", "silver");
            Assert.Throws<IOException>(() => second.Set(2, "x10", "original"));
            File.WriteAllText(path, "{\"version\":1,\"colours\":{\"9:x10\":\"silver\"}}");
            Assert.Throws<InvalidDataException>(() => new PadColourStore(path).Load());
            Assert.Equal("{\"version\":1,\"colours\":{\"9:x10\":\"silver\"}}", File.ReadAllText(path));
        });
    }

    private static void InTemporaryDirectory(Action<string> test)
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-colour-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { test(Path.Combine(dir, "pad-colours.json")); } finally { Directory.Delete(dir, true); }
    }
}
