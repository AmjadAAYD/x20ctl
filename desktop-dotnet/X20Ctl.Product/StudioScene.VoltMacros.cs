using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using X20Ctl.Product.Volt;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool voltMacroWired;
    private Button VoltMacroAction(string text, string tag, Action action, bool enabled = true)
        => ThemeAction(new Border { Padding = new(10,7,10,7), Margin = new(0,0,6,6), Background = VoltKit.B("#262626"), CornerRadius = new(8), Child = new TextBlock { Text = text, Foreground = VoltKit.WhiteBrush, FontFamily = VoltKit.Display, FontSize = 15 } }, "theme:macro:" + tag, action, enabled);

    private UIElement BuildVoltMacros()
    {
        if (Macros is not { } macro) return new Canvas();
        string? focused = (Keyboard.FocusedElement as FrameworkElement)?.Tag as string;
        if (!voltMacroWired) { voltMacroWired = true; macro.Changed += VoltLater; }
        var content = new Canvas { Width = 970, Height = 560 };
        var slots = new WrapPanel();
        foreach (string slot in macro.Draft.SlotNames)
        {
            var action = VoltMacroAction(slot + (macro.Draft[slot].Count == 0 ? " · Empty" : " · " + macro.Draft[slot].Count), "slot:" + slot, () => macro.SelectSlot(slot));
            if (slot == macro.Slot && action.Content is Border face) { face.Background = VoltKit.AmberHotBrush; ((TextBlock)face.Child).Foreground = VoltKit.B("#111111"); }
            slots.Children.Add(action);
        }
        content.Children.Add(slots);
        var sequence = new StackPanel { Width = 460 };
        sequence.Children.Add(new TextBlock { Text = macro.Slot + " · SEQUENCE · " + macro.CapacityDescription, Foreground = VoltKit.MuteBrush, FontSize = 13, Margin = new(0,0,0,12) });
        var events = macro.Draft[macro.Slot];
        for (int i=0;i<events.Count;i++)
        {
            int index = i; var entry = events[i];
            var row = VoltMacroAction($"{i+1:00}  {RebelPresses(entry)}     {entry.HoldMs} ms hold / {entry.PauseMs} ms pause", "event:" + i, () => macro.Select(index));
            if (i == macro.SelectedIndex && row.Content is Border face) { face.BorderBrush = VoltKit.AmberHotBrush; face.BorderThickness = new(1); }
            sequence.Children.Add(row);
        }
        sequence.Children.Add(VoltMacroAction("+ Add event", "add", macro.Add, macro.Draft.CanEdit && macro.Draft.EntryCount(macro.Slot) < 47));
        var actions = new WrapPanel { Margin = new(0,12,0,0) };
        var record = VoltMacroAction("Record", "record", () => { macro.ToggleRecording(); VoltLater(); });
        record.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = macro.Record });
        if (macro.Record.Content is StackPanel { Children.Count: 2 } native && native.Children[1] is TextBlock label && record.Content is Border { Child: TextBlock caption }) caption.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = label });
        actions.Children.Add(record); actions.Children.Add(VoltMacroAction("Preview timeline", "preview", macro.Preview));
        actions.Children.Add(VoltMacroAction("Clear", "clear", macro.Clear, macro.Draft.CanEdit)); sequence.Children.Add(actions);
        if (macro.Draft.PcMode)
        {
            sequence.Children.Add(new TextBlock { Text = "In PC mode, played by", Foreground = VoltKit.WhiteBrush, Margin = new(0,12,0,8) });
            var trigger = new ComboBox { ItemsSource = new[] { "Not used" }.Concat(MacroDraft.TriggerInputs), SelectedItem = macro.Draft.Trigger(macro.Slot) ?? "Not used", IsEnabled = macro.Draft.CanEdit, Tag = "theme:macro:trigger", Height = 32 };
            trigger.SelectionChanged += (_,_) => { if (trigger.SelectedItem is string choice) macro.SetTrigger(choice == "Not used" ? null : choice); }; sequence.Children.Add(trigger);
        }
        var status = new TextBlock { Foreground = VoltKit.MuteBrush, TextWrapping = TextWrapping.Wrap, Margin = new(0,12,0,0) }; status.SetBinding(TextBlock.TextProperty,new Binding("Text"){Source=macro.StatusText}); sequence.Children.Add(status);
        var sequenceScroll = new ScrollViewer { Width=460, Height=480, Content=sequence, VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; Canvas.SetTop(sequenceScroll,60); content.Children.Add(sequenceScroll);
        var editor = new StackPanel();
        if (macro.SelectedIndex >= 0 && macro.SelectedIndex < events.Count)
        {
            var entry = events[macro.SelectedIndex];
            editor.Children.Add(new TextBlock { Text=$"Event {macro.SelectedIndex+1:00} · what it presses", FontFamily=VoltKit.Display, FontSize=22, Foreground=VoltKit.WhiteBrush, Margin=new(0,0,0,12) });
            var inputs = new WrapPanel();
            foreach(string key in MacroDraft.Inputs) inputs.Children.Add(VoltMacroAction((entry.Buttons.Contains(key)?"● ":"")+key,"input:"+key,()=>macro.Update(entry with {Buttons=entry.Buttons.Contains(key)?entry.Buttons.Where(b=>b!=key).ToArray():entry.Buttons.Append(key).ToArray()}),macro.Draft.CanEdit));
            editor.Children.Add(inputs);
            foreach(var (name,left) in new[]{("Left stick",true),("Right stick",false)})
            {
                editor.Children.Add(new TextBlock{Text=name,Foreground=VoltKit.MuteBrush,Margin=new(0,8,0,4)});
                var heading=new ComboBox{ItemsSource=Enum.GetValues<StickHeading>(),SelectedItem=left?entry.Left:entry.Right,IsEnabled=macro.Draft.CanEdit,Tag="theme:macro:stick:"+(left?"left":"right"),Height=32};
                heading.SelectionChanged+=(_,_)=>{if(heading.SelectedItem is StickHeading value) macro.Update(left?entry with {Left=value}:entry with {Right=value});}; editor.Children.Add(heading);
            }
            foreach(var (name,pause) in new[]{("Hold",false),("Pause",true)})
            {
                int value=pause?entry.PauseMs:entry.HoldMs; var row=new WrapPanel{Margin=new(0,8,0,0)}; row.Children.Add(new TextBlock{Text=name+" "+value+" ms",Width=150,Foreground=VoltKit.WhiteBrush,VerticalAlignment=VerticalAlignment.Center});
                foreach(int delta in new[]{-5,5}) row.Children.Add(VoltMacroAction(delta<0?"−5":"+5","time:"+name+":"+delta,()=>macro.Update(pause?entry with {PauseMs=Math.Clamp(value+delta,0,327675)}:entry with {HoldMs=Math.Clamp(value+delta,0,327675)}),macro.Draft.CanEdit)); editor.Children.Add(row);
            }
            var edits=new WrapPanel{Margin=new(0,10,0,0)};
            foreach(var (text,tag,action) in new (string,string,Action)[]{("Earlier","earlier",()=>macro.Move(-1)),("Later","later",()=>macro.Move(1)),("Duplicate","duplicate",macro.Duplicate),("Remove","remove",macro.Remove)}) edits.Children.Add(VoltMacroAction(text,tag,action,macro.Draft.CanEdit&&(tag!="duplicate"||macro.Draft.EntryCount(macro.Slot)<47)));
            editor.Children.Add(edits);
        }
        else editor.Children.Add(new TextBlock{Text="Add an event to begin.",Foreground=VoltKit.MuteBrush});
        var editCard = new Border { Width=480,Height=480,Padding=new(20),Background=VoltKit.B("#1e1e1e"),CornerRadius=new(12),Child=new ScrollViewer{Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto} }; Canvas.SetLeft(editCard,490); Canvas.SetTop(editCard,60); content.Children.Add(editCard);
        var shell=VoltKit.TwoPaneShell("macros","Macros","Local sequence drafts. Preview emits no controller input.",content,VoltSelectPage);
        if(focused?.StartsWith("theme:macro:")==true) Dispatcher.BeginInvoke(()=>FindTagged(shell,focused)?.Focus(),System.Windows.Threading.DispatcherPriority.Input);
        return shell;
    }
}
