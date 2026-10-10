using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Markup;
using Microsoft.Win32;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed class SetupsWorkbench
{
    public Grid Body {get;}=new();
    public Grid Shelf {get;}=new();
    public SetupStore Store {get;}
    public string? SelectedId {get;private set;}
    public string? ActiveId {get;private set;}
    public bool DeletePending=>pendingDelete!=null;
    public string Notice=>notice.Text;
    public Button NewButton {get;}
    public Button SaveButton {get;}
    public Button OpenButton {get;}
    public Button RenameButton {get;}
    public Button SaveNameButton {get;}
    public Button MoreButton {get;}
    public TextBox NameField=>name;
    public MenuItem DeleteAction=>deleteAction;
    public Border DetailWorkspace {get;}=new();
    public IReadOnlyList<Button> SetupCards=>setupCards.Values.Select(c=>c.Button).ToArray();
    public ScrollViewer LibraryViewport {get;}=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    public ScrollViewer WorkspaceViewport {get;}=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    public IReadOnlyList<string> LegacySources {get;}
    public event Action? Changed;
    private readonly FrameworkElement resources;
    private readonly Action<Button> register;
    private readonly ControllerCatalog catalog;
    private readonly string model;
    private readonly Func<SetupSnapshot> capture;
    private readonly Action<SetupSnapshot> load;
    private readonly WrapPanel cards=new();
    private readonly Dictionary<string,Card> setupCards=new();
    private readonly Grid header=new();
    private readonly StackPanel headerActions=new(){Orientation=Orientation.Horizontal};
    private readonly TextBlock heading=ManagementVisuals.Copy("My setups",30,true),libraryState=ManagementVisuals.Copy("",15),notice=ManagementVisuals.Copy("Stored on this computer",15);
    private readonly TextBlock detailName=ManagementVisuals.Copy("",30,true),detailModel=ManagementVisuals.Copy("",16),detailState=ManagementVisuals.Copy("",14,true);
    private readonly TextBlock[] metricValues=new TextBlock[4];
    private readonly TextBox name=new(){FontSize=20,Height=40,MaxLength=100,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(17,31,50)),BorderBrush=Brushes.SlateGray,Padding=new Thickness(8,4,8,4),Width=380};
    private readonly WrapPanel renameEditor=new(){Visibility=Visibility.Collapsed,Margin=new(0,12,0,0)};
    private readonly Grid confirmation=new(){Background=new SolidColorBrush(Color.FromArgb(246,7,14,28)),Visibility=Visibility.Collapsed};
    private readonly TextBlock deleteText=ManagementVisuals.Copy("",24,true);
    private readonly Button keepSetup,confirmRemoval,duplicate,export;
    private readonly MenuItem deleteAction;
    private readonly StackPanel empty=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private string? pendingDelete;
    private readonly bool healthy;
    private static readonly ControlTemplate cardTemplate=(ControlTemplate)XamlReader.Parse("""
    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
      <Border x:Name="Surface" CornerRadius="18" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
        <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center" />
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="#AAD1EE" /></Trigger>
        <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Opacity" Value=".8" /></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate>
    """);
    private sealed record Card(Button Button,Viewbox? Artwork,TextBlock Title,TextBlock Summary,TextBlock State);

    public SetupsWorkbench(SetupStore store,ControllerCatalog catalog,string modelId,FrameworkElement owner,Action<Button> focus,Func<SetupSnapshot> current,Action<SetupSnapshot> open,IReadOnlyList<string> legacySources,string? activeId)
    {
        Store=store;this.catalog=catalog;model=modelId;resources=owner;register=focus;capture=current;load=open;LegacySources=legacySources;ActiveId=activeId;
        try{Store.Load();healthy=true;}catch(Exception e) when(e is IOException or JsonException or ArgumentException or InvalidOperationException){healthy=false;notice.Text="Library unavailable. Original file preserved: "+e.Message;}
        Body.RowDefinitions.Add(new(){Height=new GridLength(72)});Body.RowDefinitions.Add(new());
        header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.RowDefinitions.Add(new(){Height=GridLength.Auto});header.RowDefinitions.Add(new(){Height=GridLength.Auto});Body.Children.Add(header);
        var titles=new StackPanel();titles.Children.Add(heading);titles.Children.Add(libraryState);header.Children.Add(titles);Grid.SetColumn(headerActions,1);header.Children.Add(headerActions);
        NewButton=Action("New setup",()=>Create(false));SaveButton=Action("Save current draft",()=>Create(true));headerActions.Children.Add(NewButton);headerActions.Children.Add(SaveButton);headerActions.Children.Add(Action("Import Setup",ImportDialog));
        if(legacySources.Count>0)headerActions.Children.Add(Action("Copy legacy setups",CopyKnownLegacy));
        foreach(Button action in headerActions.Children){action.Height=42;action.VerticalAlignment=VerticalAlignment.Center;}
        var workspace=new StackPanel {Margin=new(0,2,10,16)};LibraryViewport.Content=cards;LibraryViewport.PreviewMouseWheel+=(_,e)=>{LibraryViewport.ScrollToVerticalOffset(Math.Round(LibraryViewport.VerticalOffset/rowStride)*rowStride-Math.Sign(e.Delta)*rowStride);e.Handled=true;};workspace.Children.Add(LibraryViewport);WorkspaceViewport.Content=workspace;Grid.SetRow(WorkspaceViewport,1);Body.Children.Add(WorkspaceViewport);
        DetailWorkspace.BorderBrush=new SolidColorBrush(Color.FromArgb(80,125,172,222));DetailWorkspace.BorderThickness=new(0,1,0,0);DetailWorkspace.Padding=new(16,18,16,18);DetailWorkspace.Margin=new(4,20,6,0);DetailWorkspace.Background=new SolidColorBrush(Color.FromArgb(65,12,24,43));workspace.Children.Add(DetailWorkspace);
        var detail=new StackPanel();DetailWorkspace.Child=detail;
        var detailTop=new DockPanel();detailState.HorizontalAlignment=HorizontalAlignment.Right;detailState.Margin=new(24,12,0,0);DockPanel.SetDock(detailState,Dock.Right);detailTop.Children.Add(detailState);var identity=new StackPanel();identity.Children.Add(ManagementVisuals.Copy("SELECTED SETUP",13));identity.Children.Add(detailName);identity.Children.Add(detailModel);detailTop.Children.Add(identity);detail.Children.Add(detailTop);
        var metrics=new System.Windows.Controls.Primitives.UniformGrid {Columns=4,Margin=new(0,20,0,14)};detail.Children.Add(metrics);int index=0;
        foreach(string label in new[]{"Buttons","Curves","Macros","Vibration"}){var group=new StackPanel();group.Children.Add(ManagementVisuals.Copy(label,14));metricValues[index]=ManagementVisuals.Copy("—",24,true);group.Children.Add(metricValues[index++]);metrics.Children.Add(group);}
        var actions=new WrapPanel();detail.Children.Add(actions);OpenButton=Action("Open",OpenSelected);OpenButton.Background=new SolidColorBrush(Color.FromRgb(35,75,123));OpenButton.FontWeight=FontWeights.SemiBold;OpenButton.MinWidth=110;
        duplicate=Action("Duplicate",DuplicateSelected);RenameButton=Action("Rename",()=>{renameEditor.Visibility=Visibility.Visible;name.Text=Store.Find(SelectedId!).Name;name.Focus();name.SelectAll();name.BringIntoView();});export=Action("Export Setup",ExportDialog);MoreButton=Action("More ···",()=>{MoreButton!.ContextMenu!.PlacementTarget=MoreButton;MoreButton.ContextMenu.IsOpen=true;});MoreButton.ToolTip="More actions: delete this setup";
        foreach(var action in new[]{OpenButton,duplicate,RenameButton,export,MoreButton}){action.Height=42;actions.Children.Add(action);}
        deleteAction=new MenuItem {Header="Delete setup…",Foreground=(Brush)resources.FindResource("DS.Danger")};deleteAction.Click+=(_,_)=>Guard(()=>{MoreButton.ContextMenu!.IsOpen=false;if(SelectedId!=null)RequestDelete(SelectedId);});MoreButton.ContextMenu=new ContextMenu();MoreButton.ContextMenu.Items.Add(deleteAction);
        AutomationProperties.SetName(MoreButton,"More setup actions");AutomationProperties.SetName(name,"Setup name");
        renameEditor.Children.Add(name);SaveNameButton=Action("Save name",()=>{if(SelectedId!=null){Store.Rename(SelectedId,name.Text);renameEditor.Visibility=Visibility.Collapsed;Refresh();}});renameEditor.Children.Add(SaveNameButton);renameEditor.Children.Add(Action("Cancel",()=>{renameEditor.Visibility=Visibility.Collapsed;RenameButton.Focus();}));detail.Children.Add(renameEditor);notice.Margin=new(0,14,0,0);detail.Children.Add(notice);
        var known=catalog.Find(modelId);if(known!=null){var image=ManagementVisuals.Artwork(known,resources);image.Height=190;image.Width=360;empty.Children.Add(image);}var emptyTitle=ManagementVisuals.Copy("Make it yours",32,true);emptyTitle.HorizontalAlignment=HorizontalAlignment.Center;empty.Children.Add(emptyTitle);var emptyCopy=ManagementVisuals.Copy("Keep your controller preferences together.\nCreate a setup, save your draft or import a preserved copy.",18);emptyCopy.TextAlignment=TextAlignment.Center;emptyCopy.Margin=new(0,16,0,0);empty.Children.Add(emptyCopy);
        var confirmPanel=new StackPanel {MaxWidth=650,Margin=new(32),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};confirmPanel.Children.Add(deleteText);confirmPanel.Children.Add(ManagementVisuals.Copy("Only the local library entry is removed. Backups and original legacy files remain.",16));var choices=new StackPanel {Orientation=Orientation.Horizontal,Margin=new(0,24,0,0)};keepSetup=Action("Keep setup",CancelDelete);confirmRemoval=Action("Delete from library",ConfirmDelete);confirmRemoval.Foreground=Brushes.Salmon;choices.Children.Add(keepSetup);choices.Children.Add(confirmRemoval);confirmPanel.Children.Add(choices);confirmation.Children.Add(confirmPanel);Grid.SetRowSpan(confirmation,2);Body.Children.Add(confirmation);
        NewButton.IsEnabled=SaveButton.IsEnabled=healthy && modelId=="x20";Body.SizeChanged+=(_,_)=>Resize();if(healthy)Refresh();else {DetailWorkspace.Visibility=Visibility.Collapsed;libraryState.Text="Library unavailable · Original file preserved";}Resize();
    }
    private Button Action(string title,Action operation)=>ManagementVisuals.Action(resources,title,()=>Guard(operation),register);
    private void Guard(Action action){try{action();Changed?.Invoke();}catch(Exception e) when(e is IOException or JsonException or ArgumentException or InvalidOperationException or UnauthorizedAccessException){notice.Text=e.Message+" Original data preserved.";}}
    public void Create(bool current){if(!healthy || model!="x20")return;var snapshot=current?capture():SetupSnapshot.Capture("x20",new("x20"),new("x20"),new("x20"),new("x20"));var item=Store.Create(current?"Saved draft "+(Store.Items.Count+1):"New setup "+(Store.Items.Count+1),snapshot);SelectedId=item.Id;Refresh();}
    public void Select(string id){Store.Find(id);SelectedId=id;renameEditor.Visibility=Visibility.Collapsed;RefreshDetails();UpdateCardStates();SnapSelected();AnimateDetails();}
    public void RefreshCurrent(){RefreshDetails();UpdateCardStates();}
    public void DuplicateSelected(){if(SelectedId==null)return;SelectedId=Store.Duplicate(SelectedId).Id;Refresh();}
    public void OpenSelected(){if(SelectedId==null)return;var item=Store.Find(SelectedId);if(item.ModelId!=model)throw new InvalidOperationException("This setup belongs to a different model.");load(Store.NativeSnapshot(SelectedId));ActiveId=SelectedId;RefreshCurrent();Changed?.Invoke();}
    public void RequestDelete(string id){Store.Find(id);pendingDelete=id;deleteText.Text="Delete “"+Store.Find(id).Name+"”?";confirmation.Visibility=Visibility.Visible;header.IsEnabled=WorkspaceViewport.IsEnabled=false;keepSetup.Focus();}
    public void CancelDelete(){pendingDelete=null;confirmation.Visibility=Visibility.Collapsed;header.IsEnabled=WorkspaceViewport.IsEnabled=true;MoreButton.Focus();}
    public void ConfirmDelete(){if(pendingDelete==null)return;string id=pendingDelete;CancelDelete();Store.Delete(id);if(ActiveId==id)ActiveId=null;if(SelectedId==id)SelectedId=null;Refresh();Changed?.Invoke();}
    public bool HandleKey(KeyEventArgs e)
    {
        if(DeletePending){if(e.Key is Key.Escape or Key.B)CancelDelete();else if(e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down)(Keyboard.FocusedElement==keepSetup?confirmRemoval:keepSetup).Focus();else if(!e.IsRepeat && e.Key is Key.Enter or Key.A){if(Keyboard.FocusedElement==confirmRemoval)confirmRemoval.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else keepSetup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}e.Handled=true;return true;}
        if(renameEditor.Visibility==Visibility.Visible && e.Key==Key.Escape){renameEditor.Visibility=Visibility.Collapsed;RenameButton.Focus();e.Handled=true;return true;}
        if(Keyboard.FocusedElement==name){if(e.Key==Key.Enter){SaveNameButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}return e.Key is not (Key.Escape or Key.F6 or Key.PageUp or Key.PageDown);}
        return false;
    }
    private void CopyKnownLegacy(){foreach(string source in LegacySources)Store.CopyLegacy(source);Refresh();notice.Text="Legacy copies saved with exact-byte backups. Originals are unchanged; editor conversion is pending.";}
    public void ImportPath(string file){string text=File.ReadAllText(file);using var doc=JsonDocument.Parse(text);if(doc.RootElement.ValueKind==JsonValueKind.Object && doc.RootElement.TryGetProperty("format",out var format) && format.GetString()?.StartsWith("x20ctl-native")==true)SelectedId=Store.ImportNative(text).Id;else SelectedId=Store.CopyLegacy(file).FirstOrDefault()?.Id;Refresh();}
    private void ImportDialog(){if(ReviewSandbox.Active)return;var dialog=new OpenFileDialog {Title="Import Setup",Filter="Setup files (*.json)|*.json",CheckFileExists=true};if(dialog.ShowDialog(Window.GetWindow(Body))==true)ImportPath(dialog.FileName);}
    private void ExportDialog(){if(SelectedId==null||ReviewSandbox.Active)return;var dialog=new SaveFileDialog {Title="Export Setup",Filter="Setup files (*.json)|*.json",FileName="x20ctl-setup.json",OverwritePrompt=true};if(dialog.ShowDialog(Window.GetWindow(Body))==true)ExportPath(dialog.FileName);}
    public void ExportPath(string target)
    {
        if(SelectedId==null)return;string absolute=Path.GetFullPath(target);if(string.Equals(absolute,Store.PathName,StringComparison.OrdinalIgnoreCase)||LegacySources.Any(p=>string.Equals(absolute,Path.GetFullPath(p),StringComparison.OrdinalIgnoreCase))||Store.Items.Any(s=>s.SourcePath!=null && string.Equals(absolute,s.SourcePath,StringComparison.OrdinalIgnoreCase)))throw new IOException("Export to a new path; preserved stores cannot be replaced.");
        string text=Store.Export(SelectedId);using var validation=JsonDocument.Parse(text);string temp=absolute+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=System.Text.Encoding.UTF8.GetBytes(text);stream.Write(bytes);stream.Flush(true);}File.Move(temp,absolute,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal void Refresh()
    {
        cards.Children.Clear();setupCards.Clear();libraryState.Text=Store.Items.Count+" saved "+(Store.Items.Count==1?"setup":"setups")+" · Local library";
        if(SelectedId==null || !Store.Items.Any(s=>s.Id==SelectedId))SelectedId=Store.Items.FirstOrDefault()?.Id;
        if(Store.Items.Count==0){LibraryViewport.Content=empty;DetailWorkspace.Visibility=Visibility.Collapsed;}
        else {LibraryViewport.Content=cards;DetailWorkspace.Visibility=Visibility.Visible;}
        foreach(var item in Store.Items)
        {
            var content=new StackPanel {HorizontalAlignment=HorizontalAlignment.Left};var known=catalog.Find(item.ModelId);Viewbox? photo=null;
            if(known!=null){photo=ManagementVisuals.Artwork(known,resources);photo.Height=148;photo.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(photo);}
            var title=ManagementVisuals.Copy(item.Name,25,true);title.TextWrapping=TextWrapping.NoWrap;title.MaxHeight=62;title.TextTrimming=TextTrimming.CharacterEllipsis;content.Children.Add(title);content.Children.Add(ManagementVisuals.Copy(known?.Name??item.ModelId,15));var summary=ManagementVisuals.Copy(Summary(item),16);summary.Margin=new(0,10,0,0);content.Children.Add(summary);var state=ManagementVisuals.Copy("",13,true);state.Margin=new(0,12,0,0);content.Children.Add(state);
            var button=Action("",()=>Select(item.Id));button.Content=content;button.Template=cardTemplate;button.Tag="SetupLibraryCard";button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new(22,18,22,18);button.Margin=new(10,12,18,12);button.Width=380;button.Height=320;button.ToolTip=item.Name;AutomationProperties.SetName(button,item.Name+", "+(known?.Name??item.ModelId));button.GotKeyboardFocus+=(_,_)=>SnapTo(item.Id);cards.Children.Add(button);setupCards[item.Id]=new(button,photo,title,summary,state);
        }
        RefreshDetails();UpdateCardStates();Resize();
    }
    private static (int Remaps,int Curves,int Macros,int Vibration) Counts(SetupSnapshot snapshot)
    {
        var baseline=new SetupCurve("default","default",0,100,85,85,170,170);
        return(snapshot.Mappings.Count(p=>p.Key!=p.Value),snapshot.Curves.Values.Count(c=>c!=baseline),snapshot.Macros.Count(p=>p.Value.Length>0),snapshot.Vibration);
    }
    private string Summary(SavedSetup item)
    {
        if(!item.Snapshot.HasValue)return "Original fields preserved\nEditor conversion pending";
        var counts=Counts(Store.NativeSnapshot(item.Id));return $"{counts.Remaps} remap{(counts.Remaps==1?"":"s")} · {counts.Curves} curve{(counts.Curves==1?"":"s")} changed\n{counts.Macros} macro{(counts.Macros==1?"":"s")} · vibration {counts.Vibration}%";
    }
    private bool EditorModified(SavedSetup item)
    {
        if(item.Id!=ActiveId || !item.Snapshot.HasValue || item.ModelId!=model)return false;
        var current=capture();var baseline=Store.NativeSnapshot(item.Id);
        return current.Vibration!=baseline.Vibration || current.Mappings.Any(p=>baseline.Mappings[p.Key]!=p.Value) || current.Curves.Any(p=>baseline.Curves[p.Key]!=p.Value) || current.Macros.Any(p=>JsonSerializer.Serialize(p.Value,SetupStore.JsonOptions)!=JsonSerializer.Serialize(baseline.Macros[p.Key],SetupStore.JsonOptions));
    }
    private void RefreshDetails()
    {
        bool selected=SelectedId!=null && Store.Items.Any(s=>s.Id==SelectedId);
        foreach(var action in new[]{OpenButton,duplicate,RenameButton,export,MoreButton})action.IsEnabled=selected&&healthy;
        deleteAction.IsEnabled=selected&&healthy;
        if(!selected){name.Text="";return;}
        var item=Store.Find(SelectedId!);detailName.Text=item.Name;detailModel.Text=(catalog.Find(item.ModelId)?.Name??item.ModelId)+" · "+(item.ModelId==model?"Matches assigned model":"Different model · Cannot open here");name.Text=item.Name;
        if(item.Snapshot.HasValue){var values=Counts(Store.NativeSnapshot(item.Id));metricValues[0].Text=values.Remaps+" remap"+(values.Remaps==1?"":"s");metricValues[1].Text=values.Curves+" changed";metricValues[2].Text=values.Macros+" sequence"+(values.Macros==1?"":"s");metricValues[3].Text=values.Vibration+"%";}
        else foreach(var metric in metricValues)metric.Text="Preserved";
        OpenButton.IsEnabled=healthy && item.Snapshot.HasValue && item.ModelId==model;
        bool modified=EditorModified(item);detailState.Text=item.Id==ActiveId?(modified?"CURRENT · LOCAL CHANGES":"CURRENT · LOCAL EDITOR"):item.LegacyPayload.HasValue?"PRESERVED LEGACY COPY":"SAVED LOCALLY";
        detailState.Foreground=modified?Brushes.SandyBrown:Brushes.LightSkyBlue;
        notice.Text=item.LegacyPayload.HasValue?"Original fields retained · Editor conversion pending · Original export available":modified?"Current setup has local changes · Saved copy retained · Not applied to hardware":"Saved locally · Not applied to hardware";
    }
    private void UpdateCardStates()
    {
        foreach(var (id,card) in setupCards)
        {
            var item=Store.Find(id);bool selected=id==SelectedId,current=id==ActiveId,modified=EditorModified(item);
            card.Button.Background=new SolidColorBrush(selected?Color.FromRgb(24,53,91):Color.FromArgb(155,16,31,53));card.Button.BorderBrush=selected?Brushes.LightSkyBlue:new SolidColorBrush(Color.FromArgb(38,125,165,218));card.Button.BorderThickness=new(selected?1.5:1);
            card.State.Text=current?(modified?"CURRENT · LOCAL CHANGES":"CURRENT · LOCAL EDITOR"):item.ModelId!=model?"OTHER MODEL":item.LegacyPayload.HasValue?"PRESERVED COPY":"LOCAL";
            card.State.Foreground=modified?Brushes.SandyBrown:current?Brushes.LightSkyBlue:Brushes.LightSteelBlue;
        }
    }
    private int columnCount=1;
    private double rowStride=334;
    private void SnapSelected(){if(SelectedId!=null)SnapTo(SelectedId);}
    private void SnapTo(string id){int index=setupCards.Keys.ToList().IndexOf(id);if(index>=0)LibraryViewport.ScrollToVerticalOffset(index/columnCount*rowStride);}
    private void Resize()
    {
        double width=Math.Max(380,Body.ActualWidth-42);bool compact=Body.ActualHeight<620;
        int columns=Math.Clamp((int)Math.Floor(width/368),1,4);if(columns==4 && setupCards.Count<=2)columns=3;columnCount=columns;double cardWidth=Math.Floor((width-28*columns)/columns);
        double cardHeight=compact?280:setupCards.Count<=2?350:320;rowStride=cardHeight+24;
        foreach(var card in setupCards.Values){card.Button.Width=cardWidth;card.Button.Height=cardHeight;card.Title.Width=cardWidth-48;card.Summary.Width=cardWidth-48;card.Title.FontSize=compact?22:25;if(card.Artwork!=null){card.Artwork.Height=compact?112:setupCards.Count<=2?178:148;card.Artwork.Width=cardWidth-48;}}
        LibraryViewport.Height=setupCards.Count==0?Math.Max(350,Body.ActualHeight-100):cardHeight+24;
        bool stacked=Body.ActualWidth<1220;Body.RowDefinitions[0].Height=new(stacked?112:72);Grid.SetColumn(headerActions,stacked?0:1);Grid.SetRow(headerActions,stacked?1:0);headerActions.Margin=new(0,stacked?10:4,0,0);name.Width=Math.Clamp(width*.4,280,480);Body.Dispatcher.BeginInvoke(new Action(SnapSelected),DispatcherPriority.Loaded);
    }
    private void AnimateDetails()
    {
        if(resources is not StudioScene {AnimationsEnabled:true})return;
        DetailWorkspace.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(160)));
    }
}
