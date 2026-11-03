using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>Workbench accessors for custom-themed My setups pages.</summary>
public sealed partial class SetupsWorkbench
{
    internal event Action? Rebuilt;
    internal bool Healthy => healthy;
    internal string LibraryText => libraryState.Text;
    internal string DetailName => detailName.Text;
    internal string DetailModel => detailModel.Text;
    internal string DetailState => detailState.Text;
    internal bool RenameActive => renameEditor.Visibility == Visibility.Visible;
    internal string DeleteQuestion => deleteText.Text;
    internal IReadOnlyList<(string Id, string Name, string State)> Cards => Store.Items.Where(s => s.ModelId == model).Select(s => (s.Id, s.Name, CardState(s))).ToList();
    internal IReadOnlyList<(string Label, string Value)> Metrics => new[] { "Buttons", "Curves", "Macros", "Vibration" }.Select((l, i) => (l, metricValues[i].Text)).ToList();
    internal bool HasLegacy => LegacySources.Count > 0;
    private string CardState(SavedSetup item)
    {
        bool current = item.Id == ActiveId, modified = EditorModified(item);
        return current ? (modified ? "CURRENT · LOCAL CHANGES" : "CURRENT · LOCAL EDITOR") : item.ModelId != model ? "OTHER MODEL" : item.LegacyPayload.HasValue ? "PRESERVED COPY" : "LOCAL";
    }
    private Button? Named(string which) => which switch
    {
        "new" => NewButton, "save" => SaveButton, "open" => OpenButton, "duplicate" => duplicate, "rename" => RenameButton, "export" => export, "saveName" => SaveNameButton, "more" => MoreButton,
        "import" => headerActions.Children.OfType<Button>().FirstOrDefault(b => b.Content as string == "Import Setup"), "legacy" => headerActions.Children.OfType<Button>().FirstOrDefault(b => b.Content as string == "Copy legacy setups"),
        "keep" => keepSetup, "remove" => confirmRemoval, "cancelRename" => renameEditor.Children.OfType<Button>().FirstOrDefault(b => b.Content as string == "Cancel"), _ => null
    };
    internal bool Can(string which) => which == "delete" ? deleteAction.IsEnabled : Named(which)?.IsEnabled == true;
    internal void Click(string which)
    {
        if (which == "delete") { deleteAction.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); return; }
        Named(which)?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }
    internal TextBox TakeNameField() { if (name.Parent is Panel p) p.Children.Remove(name); return name; }
    internal void Watch(Action changed)
    {
        Rebuilt += changed;
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(renameEditor, (_, _) => changed());
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(confirmation, (_, _) => changed());
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(notice, (_, _) => changed());
    }
    internal string NoticeText => notice.Text;
}
