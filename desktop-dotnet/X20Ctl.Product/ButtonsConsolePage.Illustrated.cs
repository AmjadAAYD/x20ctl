using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product;

public sealed partial class ButtonsConsolePage
{
    private IllustratedDesignPage? matchedButtons;
    private ContextMenu? illustratedOutputs;
    private readonly Dictionary<string,Button> matchedInputRows=new();
    internal bool HasIllustratedInput=>connected;
    private void RefreshIllustratedBindings(){foreach(var pair in matchedInputRows)pair.Value.DataContext=pair.Key==Source;matchedButtons?.Refresh();}
    internal void OpenIllustratedOutputs(FrameworkElement target){if(illustratedOutputs!=null){illustratedOutputs.PlacementTarget=target;illustratedOutputs.IsOpen=true;}}
    private void ComposeIllustrated(Grid visual,Grid editor,Grid actions)
    {
        if(!IllustratedThemeUI.Matched)return;
        matchedButtons=new("buttons",slot=>
        {
            if(slot.Key.StartsWith("output:"))return outputs.GetValueOrDefault(slot.Key[7..]);
            if(slot.Key.StartsWith("input:"))
            {string key=slot.Key[6..];var b=new Button{Tag="illustrated:input:"+key};matchedInputRows[key]=b;b.Click+=(_,_)=>SelectPhysical(key);return b;}
            if(slot.Key=="reset")
            {
                var b=new Button{ToolTip="Restore all local button mappings"};
                b.Click+=(_,_)=>{draft.ResetAll();Changed?.Invoke();RefreshMapping();};return b;
            }
            return illustratedControl?.Invoke(slot.Key);
        });
        matchedButtons.TextValue=(key,text)=>key switch
        {
            "input"=>Source.Length>0?Short(Source):"—",
            "output"=>Source.Length>0&&draft.CanEdit(Source)?Short(draft.Target(Source)):"—",
            "input-name"=>Source.Length>0?Kind(Source):"Select a control",
            "count"=>draft.UnsentChanges==0?"No unsent changes":$"{draft.UnsentChanges} unsent change{(draft.UnsentChanges==1?"":"s")}",
            "clock"=>DateTime.Now.ToString("HH:mm"),
            "player-name"=>$"Player {PadColours.ActivePlayer+1}","player-number"=>$"{PadColours.ActivePlayer+1}",
            "controller-state"=>model.Id.Replace('_',' ').ToUpperInvariant()+" · "+(connected?"Input available":"Input unavailable"),
            _ when key.StartsWith("assignment:")=>draft.CanEdit(key[11..])?Short(draft.Target(key[11..])):Short(key[11..]),
            _ when text.Contains("Controller offline")=>model.Id.Replace('_',' ').ToUpperInvariant()+" · "+(connected?"Input available":"Input unavailable"),
            _=>System.Text.RegularExpressions.Regex.Replace(text.Replace("Player 1",$"Player {PadColours.ActivePlayer+1}"),@"\bX20\b",model.Id.Replace('_',' ').ToUpperInvariant())
        };
        if(matchedButtons.Pads.FirstOrDefault() is {} pad)
        {
            IllustratedComposition.Detach(stage);
            matchedButtons.Place(new Viewbox{Child=stage,Stretch=Stretch.Fill},pad);
        }
        // The reference palette contains twelve outputs. The others remain native keyboard-accessible actions.
        var menu=illustratedOutputs=new ContextMenu();
        foreach(var pair in outputs)
        {
            string target=pair.Key;var item=new MenuItem{Header=TargetName(target)};
            item.Click+=(_,_)=>{pair.Value.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));matchedButtons.Refresh();};menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var assignments=new MenuItem{Header="All assignments"};assignments.SubmenuOpened+=(_,_)=>{assignments.Items.Clear();foreach(var input in layout.Controls.Where(c=>draft.CanEdit(c.Key)))assignments.Items.Add(new MenuItem{Header=$"{label(input.Key)} → {TargetName(draft.Target(input.Key))}",IsEnabled=false});};menu.Items.Add(assignments);
        void Action(string text,Action action){var item=new MenuItem{Header=text};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Action("Front view",()=>ShowView(false));Action("Back view",()=>ShowView(true));Action("Test mapping",()=>toggleTry());
        Children.Clear();ColumnDefinitions.Clear();RowDefinitions.Clear();
        Children.Add(new Viewbox{Child=matchedButtons,Stretch=Stretch.Uniform});
    }
}
