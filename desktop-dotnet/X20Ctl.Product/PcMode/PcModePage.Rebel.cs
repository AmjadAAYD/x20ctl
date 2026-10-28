using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>What a theme that lays PC mode out itself (Rebel) reads and does. Every action is this page's own, so the switch, the notice,
/// the profiles, turbo, sharing and reset behave exactly as they do here.</summary>
public sealed partial class PcModePage
{
    internal string StatusText => statusLabel.Text;
    internal string StatusDetail => statusDetail.Text;
    internal string Pipeline => pipeline.Text;
    internal string SavedText => saved.Text;
    internal string SetupNote => setupNote.Text;
    internal bool RouteActive => service.State == PcModeState.Active && service.Routes.Any(r => r.Player == Player);
    internal bool IsOn => enable.IsChecked == true;
    internal bool NoticeShown => notice.Visibility == Visibility.Visible;
    internal bool Picking => picker.Visibility == Visibility.Visible;
    internal bool IsGame => ProfileKey != PcModeStore.DefaultKey;
    internal IReadOnlyList<(string Id, string Label)> ProfileChoices => (profiles.ItemsSource as IEnumerable<Choice>)?.Select(c => (c.Id, c.Label)).ToList() ?? [];
    internal IReadOnlyList<(string Id, string Label)> AppChoices => (apps.ItemsSource as IEnumerable<Choice>)?.Select(c => (c.Id, c.Label)).ToList() ?? [];
    internal IReadOnlyList<(string Id, string Label, string Rate)> TurboChoices => Buttons.Select(b => (b.Id, b.Label, turbo[b.Id].SelectedItem as string ?? "No turbo")).ToList();
    internal int TurboCount => Buttons.Count(b => turbo[b.Id].SelectedIndex > 0);
    /// <summary>Calls back whenever anything this page says changes.</summary>
    internal void Watch(Action changed)
    {
        var text = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        foreach (var t in new[] { statusLabel, statusDetail, pipeline, saved, setupNote }) text.AddValueChanged(t, (_, _) => changed());
        var vis = DependencyPropertyDescriptor.FromProperty(VisibilityProperty, typeof(UIElement));
        foreach (var e in new UIElement[] { notice, picker }) vis.AddValueChanged(e, (_, _) => changed());
        enable.Checked += (_, _) => changed(); enable.Unchecked += (_, _) => changed();
    }
    internal void Toggle() => enable.IsChecked = enable.IsChecked != true;
    internal void PickProfile(string key) => profiles.SelectedValue = key;
    internal void CycleTurbo(string id, int step) { var box = turbo[id]; box.SelectedIndex = (box.SelectedIndex + step + TurboRates.Length) % TurboRates.Length; }
    internal void FindGames() => ShowPicker();
    internal void CancelPicker() => picker.Visibility = Visibility.Collapsed;
    internal void AddGameById(string id) { apps.SelectedValue = id; AddGame(); }
    internal void RemoveThisGame() => RemoveGame();
    internal void SaveFile() => ExportFile();
    internal void OpenFile() => ImportFile();
    internal void CopyAsCode() => CopyCode();
    internal void PasteAsCode() => PasteCode();
    internal void ResetProfile() { loading = true; Fill(new()); loading = false; clearAll = true; Persist(); clearAll = false; }
    internal void Accept() => AcceptNotice();
    internal Task SetUp() => RunSetup();
}
