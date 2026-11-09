using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using X20Ctl.Zone.Mouse;
namespace X20Ctl.Product;

/// <summary>Workbench accessors for custom-themed Controller as mouse pages.</summary>
public sealed partial class MouseWorkbench
{
    internal string StatusText => status.Text;
    internal string SavedText => saved.Text;
    internal string MappedText => mapped.Text;
    internal string ScrollSourceText => scrollSource.Text;
    internal string PrecisionSourceText => precisionSource.Text;
    internal string ReadoutText => previewReadout.Text;
    internal bool IsOn => Switch.IsChecked == true;
    internal bool Testing => testing;
    internal string SelectedKey => selection;
    internal void PickKey(string key) => Select(key);
    internal IReadOnlyList<string> Inputs => mappings.Keys.ToList();
    internal string LabelFor(string key) => InputName(key);
    internal string ActionFor(string key) => profile.Combo && profile.ToggleButtons.Contains(key) ? "Shortcut reserved" : Friendly(profile.Buttons.GetValueOrDefault(key));
    internal MouseAction CurrentAction => profile.Buttons.GetValueOrDefault(selection);
    internal static string[] PresetNames => ["Desktop", "Web browsing", "Media", "Minimal"];
    internal ComboBox SourceBox => source; internal ComboBox PointerBox => pointer; internal ComboBox Chord1Box => chord1; internal ComboBox Chord2Box => chord2; internal ComboBox HoldBox => hold;
    internal Slider SpeedSlider => speed; internal Slider CurveSlider => curve; internal Slider DeadzoneSlider => deadzone; internal Slider PrecisionSlider => precision; internal Slider ScrollSlider => scroll;
    internal CheckBox ScrollsBox => scrolls; internal CheckBox ComboToggle => combo; internal CheckBox HorizontalBox => horizontal; internal CheckBox InvertVBox => invertV; internal CheckBox InvertHBox => invertH;
    internal void FlipSwitch() => Switch.IsChecked = Switch.IsChecked != true;
    internal void Assign(MouseAction action) { var next = profile.Clone(); next.Buttons[selection] = action; Save(next); Select(selection); }
    internal void ApplyPreset(string name) => Preset(name);
    internal void ResetMode() { Save(new MouseProfile { SourceSlot = profile.SourceSlot }); Fill(); Select(selection); }
    internal void TestPointer() => ToggleTest();
    internal void Watch(Action changed)
    {
        var text = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        foreach (var t in new[] { status, saved, mapped, selected, scrollSource, precisionSource }) text.AddValueChanged(t, (_, _) => changed());
        Switch.Checked += (_, _) => changed(); Switch.Unchecked += (_, _) => changed();
        foreach (var s in new[] { speed, curve, deadzone, precision, scroll }) s.ValueChanged += (_, _) => changed();
        foreach (var b in new[] { source, pointer, chord1, chord2, hold }) b.SelectionChanged += (_, _) => changed();
        foreach (var c in new[] { scrolls, combo, horizontal, invertV, invertH }) { c.Checked += (_, _) => changed(); c.Unchecked += (_, _) => changed(); }
    }
    internal void WatchReadout(Action<string> changed)
    {
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(previewReadout, (_, _) => changed(previewReadout.Text));
    }
}
