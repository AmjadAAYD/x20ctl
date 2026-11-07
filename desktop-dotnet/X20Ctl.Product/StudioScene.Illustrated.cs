using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool illustratedShell;
    private IllustratedDesignPage? matchedCurves;
    private Viewbox? matchedCurvesHost;
    private IllustratedDesignPage? matchedStudio;
    private Viewbox? matchedStudioHost;
    private readonly List<RadioButton> matchedTabs=[];
    private string matchedPage="";
    private string IllustratedPageTitle
    {
        get{string name=CurrentPage=="PcMode"?"PC mode":CurrentPage;return IllustratedThemeUI.Current?.Surface is "ink" or "paper" or "neon"?name.ToUpperInvariant():name;}
    }
    private FrameworkElement? IllustratedStudioControl(string key)
    {
        if(key.StartsWith("chrome:"))
        {
            var button=new Button();System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button,true);
            button.Click+=(_,e)=>{if(key=="chrome:close")Close(button,e);else if(key=="chrome:minimize")Minimize(button,e);else Maximize(button,e);};return button;
        }
        if(key=="agent")
        {var button=new Button{Content="Agent XCTL",FontSize=11,Padding=new(8,4,8,4)};button.Click+=(_,_)=>OpenAgent();return button;}
        if(key=="more-pages")
        {
            var button=new Button{Content="More pages…",FontSize=11,Padding=new(8,4,8,4)};var menu=new ContextMenu();
            foreach(var original in navigation.Concat(new[]{DiagnosticsNav,TesterNav}))
            {var tab=original;var item=new MenuItem{Header=tab.Content};item.SetBinding(IsEnabledProperty,new Binding("IsEnabled"){Source=tab});item.Click+=(_,_)=>tab.RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));menu.Items.Add(item);}
            menu.Items.Add(new Separator());
            var outputs=new MenuItem{Header="Button outputs, assignments and views…"};outputs.Click+=(_,_)=>buttonsPage?.OpenIllustratedOutputs(button);menu.Items.Add(outputs);
            if(PcModeEntryAvailable){var pc=new MenuItem{Header="PC Mode…"};pc.Click+=(_,_)=>RequestPcMode();menu.Items.Add(pc);}
            foreach(var original in new[]{TryChoice,StickToolsChoice})
            {var source=original;var item=new MenuItem{Header=source==TryChoice?"Keyboard test":"Advanced stick tools"};item.Click+=(_,_)=>source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));menu.Items.Add(item);}
            var reset=new MenuItem{Header="Reset all curve drafts"};reset.Click+=(_,_)=>ResetAllCurvesAction.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.Items.Add(reset);
            button.ContextMenu=menu;button.Click+=(_,_)=>{menu.PlacementTarget=button;menu.IsOpen=true;};return button;
        }
        if(key.StartsWith("nav:"))
        {
            string destination=key[4..];
            var original=navigation.FirstOrDefault(tab=>tab.Content as string==destination);
            if(original!=null)
            {
                var tab=new RadioButton{Tag="illustrated:nav:"+destination,IsEnabled=original.IsEnabled};
                tab.SetBinding(IsEnabledProperty,new Binding("IsEnabled"){Source=original});
                tab.Click+=(_,_)=>original.RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));matchedTabs.Add(tab);return tab;
            }
            var button=new Button{Tag="illustrated:nav:"+destination};RegisterFocus(button);
            button.Click+=(_,_)=>{if(destination=="Controller Zone")BackRequested?.Invoke();else if(destination=="Tools")ToolsRequested?.Invoke();};return button;
        }
        if(key=="apply")
        {
            var button=new Button{Tag="illustrated:apply"};
            if(ControllerWrites)
            {button.Click+=(_,_)=>_ = ApplyToController(text=>{ChangeCount.Text=text;matchedCurves?.ShowNotice(text);matchedStudio?.ShowNotice(text);buttonsPage?.SayController(text);});}
            else
            {button.SetBinding(IsEnabledProperty,new Binding("IsEnabled"){Source=Apply});button.Click+=(_,_)=>Apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
            RegisterFocus(button);return button;
        }
        return null;
    }
    private void BuildIllustratedStudio()
    {
        if(!IllustratedThemeUI.Matched||buttonsPage==null)return;
        illustratedShell=true;
        IllustratedComposition.Detach(buttonsPage);StudioShell.Children.Add(buttonsPage);Panel.SetZIndex(buttonsPage,2);
        matchedCurves=new("curves",slot=>
        {
            if(slot.Key.StartsWith("preset:"))return Curves.Buttons.FirstOrDefault(b=>b.Tag as string==slot.Key[7..]);
            if(slot.Key.StartsWith("channel:")&&int.TryParse(slot.Key[8..],out int channel))return Curves.Buttons.FirstOrDefault(b=>b.Tag is CurveChannel c&&(int)c==channel);
            if(slot.Key=="slider:deadzone")return Curves.Inner;
            if(slot.Key=="slider:range")return Curves.Outer;
            if(slot.Key=="reset")
            {var button=new Button();button.Click+=(_,_)=>Curves.Reset();return button;}
            return IllustratedStudioControl(slot.Key);
        });
        matchedCurves.TextValue=(key,text)=>key switch
        {
            "clock"=>DateTime.Now.ToString("HH:mm"),"deadzone"=>$"{Curves.Inner.Value:0}%","range"=>$"{Curves.Outer.Value:0}%",
            "readout:deadzone"=>$"{Curves.Inner.Value:00}","readout:range"=>$"{Curves.Outer.Value:00}",
            "player-name"=>$"Player {Player+1}","player-number"=>$"{Player+1}",
            "controller-state"=>Model.Id.Replace('_',' ').ToUpperInvariant()+" · "+(buttonsPage.HasIllustratedInput?"Input available":"Input unavailable"),
            "count"=>Curves.Draft.ModifiedChannels==0?"No unsent changes":$"{Curves.Draft.ModifiedChannels} curve change{(Curves.Draft.ModifiedChannels==1?"":"s")}",
            _ when text.Contains("Controller offline")=>Model.Id.Replace('_',' ').ToUpperInvariant()+" · "+(buttonsPage.HasIllustratedInput?"Input available":"Input unavailable"),
            _=>System.Text.RegularExpressions.Regex.Replace(text.Replace("Player 1",$"Player {Player+1}"),@"\bX20\b",Model.Id.Replace('_',' ').ToUpperInvariant())
        };
        if(matchedCurves.Graph is {} graph)
        {
            var nativeGraph=Curves.TakeThemeGraph();Curves.Inspector.Children.Clear();Curves.Inspector.RowDefinitions.Clear();Curves.Inspector.ColumnDefinitions.Clear();
            Curves.Inspector.RowDefinitions.Add(new());Curves.Inspector.RowDefinitions.Add(new(){Height=new(0)});
            Curves.Inspector.Children.Add(new Viewbox{Child=nativeGraph,Stretch=Stretch.Uniform});matchedCurves.Place(Curves.Inspector,graph);
        }
        Curves.Changed+=()=>matchedCurves.Refresh();Curves.ChannelSelected+=_=>matchedCurves.Refresh();
        matchedCurvesHost=new(){Child=matchedCurves,Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed};StudioShell.Children.Add(matchedCurvesHost);Panel.SetZIndex(matchedCurvesHost,2);
        matchedStudio=new("shell",slot=>
        {
            var control=IllustratedStudioControl(slot.Key);
            if(slot.Key=="apply"&&control!=null)control.SetBinding(VisibilityProperty,new Binding("Visibility"){Source=Apply});
            return control;
        });
        matchedStudio.TextValue=(key,text)=>key switch
        {
            "page"=>IllustratedPageTitle,"page-breadcrumb"=>" / "+IllustratedPageTitle,"clock"=>DateTime.Now.ToString("HH:mm"),
            "player-name"=>$"Player {Player+1}","player-number"=>$"{Player+1}",
            "controller-state"=>Model.Id.Replace('_',' ').ToUpperInvariant()+" · "+(buttonsPage.HasIllustratedInput?"Input available":"Input unavailable"),
            "count"=>ChangeCount.Visibility==Visibility.Visible?ChangeCount.Text:"",
            _ when text.Contains("Controller offline")=>Model.Id.Replace('_',' ').ToUpperInvariant()+" · "+(buttonsPage.HasIllustratedInput?"Input available":"Input unavailable"),
            _=>System.Text.RegularExpressions.Regex.Replace(text.Replace("Player 1",$"Player {Player+1}"),@"\bX20\b",Model.Id.Replace('_',' ').ToUpperInvariant())
        };
        foreach(var design in new[]{matchedStudio,matchedCurves})
        {
            var live=new MultiBinding{StringFormat="{0}|{1}"};live.Bindings.Add(new Binding("Text"){Source=ChangeCount});live.Bindings.Add(new Binding("Text"){Source=GameplayConnection});
            design.SetBinding(IllustratedDesignPage.WatchedTextProperty,live);
        }
        if(matchedStudio.Graph is not {} frame)throw new InvalidOperationException("Illustrated Studio workspace is missing.");
        frame.X+=18;frame.Y+=18;frame.W-=36;frame.H-=36;matchedStudio.Place(RootLayout,frame);
        matchedStudioHost=new(){Child=matchedStudio,Stretch=Stretch.Uniform};StudioShell.Children.Add(matchedStudioHost);Panel.SetZIndex(matchedStudioHost,1);
        SizeChanged+=(_,_)=>EnforceIllustratedShell();
        UpdateIllustratedPage();
    }
    private void EnforceIllustratedShell()
    {
        if(!illustratedShell)return;
        ((FrameworkElement)HeaderGrid.Parent).Visibility=NavigationLayer.Visibility=StudioFooter.Visibility=Visibility.Collapsed;
        RootLayout.RowDefinitions[0].Height=RootLayout.RowDefinitions[1].Height=RootLayout.RowDefinitions[5].Height=new(0);
    }
    private void UpdateIllustratedPage()
    {
        if(!illustratedShell)return;
        bool buttons=OnButtonsPage,curves=IsCurves&&ManagementPage==null;
        EnforceIllustratedShell();
        RootLayout.Visibility=buttons||curves?Visibility.Collapsed:Visibility.Visible;
        if(buttonsPage!=null)buttonsPage.Visibility=buttons?Visibility.Visible:Visibility.Collapsed;
        if(matchedCurvesHost!=null)matchedCurvesHost.Visibility=curves?Visibility.Visible:Visibility.Collapsed;
        if(matchedStudioHost!=null)matchedStudioHost.Visibility=buttons||curves?Visibility.Collapsed:Visibility.Visible;
        if(matchedStudio!=null){matchedStudio.ArtworkPageOverride=AgentOpen?"Agent XCTL":CurrentPage;matchedStudio.Refresh();}
        foreach(var tab in matchedTabs)
        {string name=((string)tab.Tag)[16..];tab.IsChecked=name.Replace(" ","").Equals(CurrentPage.Replace(" ",""),StringComparison.OrdinalIgnoreCase);}
        if(matchedPage!=CurrentPage){matchedPage=CurrentPage;matchedCurves?.Refresh();}
    }
}
