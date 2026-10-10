namespace X20Ctl.Zone.Tests;

public class ControlRegionTests
{
    [Fact] public void TriggerViewerSidesReverseBetweenOwnedFrontAndRearArtwork()
    {
        var model=new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json"))).Get("x20");
        var owned=ControlRegions.Owned(model,File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/control-shapes.json")),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/x20-rear.json")));
        Assert.True(model.FrontButtons["LT"].X<.5);Assert.True(model.FrontButtons["RT"].X>.5);
        var lt=owned.Controls.Single(c=>c.Key=="LT");var rt=owned.Controls.Single(c=>c.Key=="RT");
        Assert.Equal("shoulder",lt.View);Assert.Equal("shoulder",rt.View);
        Assert.True(rt.Bounds.X+rt.Bounds.Width<.5);Assert.True(lt.Bounds.X>.5);
        Assert.Equal(1-rt.Bounds.X-rt.Bounds.Width,lt.Bounds.X,8);
        Assert.Equal(rt.Bounds.Y,lt.Bounds.Y);Assert.Equal(rt.Bounds.Height,lt.Bounds.Height);
        Assert.Null(lt.Transform);Assert.Null(rt.Transform); // Paths already mirrored; do not mirror twice.
    }
    [Fact] public void TriggerPathsRetainCanonicalOwnedLabelsAndExactBounds()
    {
        string shapes=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/control-shapes.json"));
        var model=new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json"))).Get("x20");
        var owned=ControlRegions.Owned(model,shapes,File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/x20-rear.json")));
        using var json=System.Text.Json.JsonDocument.Parse(shapes);
        foreach(string key in new[]{"LT","RT"})
        {
            var canonical=json.RootElement.GetProperty("models").GetProperty("x20").GetProperty("back").GetProperty(key);
            var region=owned.Controls.Single(c=>c.Key==key);Assert.Equal(canonical.GetProperty("path").GetString(),region.PathData);
            Assert.Equal(canonical.GetProperty("bounds")[0].GetDouble()/1536,region.Bounds.X);
        }
    }
    [Fact] public void OwnedLayoutIncludesEveryKnownFrontInputAndPreservesRearLabelOrder()
    {
        var model = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/catalog.json"))).Get("x20");
        var layout = ControlRegions.Owned(model, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/x20-rear.json")));
        foreach (string key in new[] { "A", "B", "X", "Y", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT", "LB", "RB", "LT", "RT", "L3", "R3", "LSTICK_ANALOG", "RSTICK_ANALOG", "HOME", "CAPTURE", "TURBO", "SELECT", "START", "M1", "M2", "M3", "M4" })
            Assert.Contains(layout.Controls, r => r.Key == key);
        Assert.Contains("1061", layout.Controls.Single(r => r.Key == "A").PathData);
        Assert.Equal("shoulder", layout.Controls.Single(r => r.Key == "LB").View);
        Assert.Equal(ControlShape.Path, layout.Controls.Single(r => r.Key == "LT").Shape);
        Assert.True(layout.Controls.Single(r => r.Key == "M2").Bounds.X > .65);
        Assert.True(layout.Controls.Single(r => r.Key == "M3").Bounds.X < .45);
        Assert.All(layout.Controls, r => Assert.InRange(r.Bounds.X, 0, 1));
    }
    [Fact] public void RearRegionsUseEveryStoredContourVertex()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "x20-rear.json"));
        var regions = ControlRegions.Rear(json);
        Assert.Equal(4, regions.Count); Assert.All(regions, r => Assert.Equal(ControlShape.Polygon, r.Shape));
        var m1 = regions.Single(r => r.Key == "M1"); Assert.Equal(15, m1.Points.Count);
        Assert.Equal(new ControlPoint(.27083333, .6328125), m1.Points[0]);
        Assert.Equal(.26757812, m1.Bounds.X); Assert.True(m1.Bounds.Width < .09 && m1.Bounds.Height > .13);
    }
    [Fact] public void FrontShapesMatchControlFamiliesAndFaceCenters()
    {
        var model = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json"))).Get("x20");
        var regions = ControlRegions.Front(model);
        Assert.Equal(ControlShape.Polygon, regions.Single(r => r.Key == "DPAD_UP").Shape);
        Assert.Equal(ControlShape.RoundedRectangle, regions.Single(r => r.Key == "LB").Shape);
        Assert.Equal(ControlShape.Circle, regions.Single(r => r.Key == "L3").Shape);
        var a = regions.Single(r => r.Key == "A"); Assert.Equal(ControlShape.Circle, a.Shape);
        Assert.Equal(model.FrontButtons["A"].X, a.Bounds.X + a.Bounds.Width / 2, 8);
        Assert.Equal(model.FrontButtons["A"].Y, a.Bounds.Y + a.Bounds.Height / 2, 8);
    }
    [Fact] public void ResetAllClearsLocalChangesAndDoesNotGrantHardwareApply()
    {
        var draft = new ButtonDraft("x20"); draft.Set("A", "B"); draft.Set("X", "Y"); Assert.Equal(2, draft.Changes.Count);
        draft.ResetAll(); Assert.Empty(draft.Changes); Assert.False(draft.CanApplyHardware);
    }
}
