using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace X20Ctl.Product;

public partial class ProductSettings
{
    internal string? IllustratedModelId{get;set;}
    private void BuildIllustratedTools()
    {
        if(!IllustratedThemeUI.Matched)return;
        Button[] tools=[Check,PcMode,WriteScan,Feedback,Themes,Replay,About,Support,Star];
        var design=new IllustratedDesignPage("tools",slot=>
        {
            if(slot.Key.StartsWith("chrome:"))
            {
                var button=new Button();System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button,true);
                button.Click+=(_,_)=>{if(Window.GetWindow(this) is not {} window)return;if(slot.Key=="chrome:close")window.Close();else if(slot.Key=="chrome:minimize")SystemCommands.MinimizeWindow(window);else window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;};return button;
            }
            if(slot.Key.StartsWith("tool:")&&int.TryParse(slot.Key[5..],out int index))return tools[index];
            if(slot.Key.StartsWith("nav:"))
            {
                string page=slot.Key[4..];var button=new Button{Tag="illustrated:nav:"+page};
                button.Click+=(_,_)=>{if(page=="Controller Zone")Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else if(page!="Tools")ThemeNavigate?.Invoke(page);};return button;
            }
            return null;
        });
        design.TextValue=(key,text)=>key=="clock"?DateTime.Now.ToString("HH:mm"):
            key=="player-name"?"Tools":key=="player-number"?"—":key=="controller-state"?IllustratedModelId?.Replace('_',' ').ToUpperInvariant()??"No controller selected":
            text.Contains("Controller offline")?IllustratedModelId==null?"No controller selected":"Input status unavailable":
            text=="X20"?IllustratedModelId?.Replace('_',' ').ToUpperInvariant()??"None":text;
        var view=new Viewbox{Child=design};view.SetBinding(VisibilityProperty,new Binding("Visibility"){Source=hub});Children.Add(view);
        // Update and intro preferences are kept in their real existing controls, accessible from this page.
        var preferencesButton=new Button{Content="Update / Intro preferences",FontSize=11,Padding=new(8,4,8,4)};
        preferencesButton.Click+=(_,_)=>
        {
            var popup=new System.Windows.Controls.Primitives.Popup{PlacementTarget=preferencesButton,Placement=System.Windows.Controls.Primitives.PlacementMode.Top,StaysOpen=false,AllowsTransparency=true};
            IllustratedComposition.Detach(footer);footer.Margin=new(0);
            popup.Child=IllustratedComposition.Plate(footer,"preferences",20);popup.Closed+=(_,_)=>{IllustratedComposition.Detach(footer);Grid.SetRow(footer,2);pageGrid!.Children.Add(footer);};popup.IsOpen=true;
        };
        design.Place(preferencesButton,new(){X=1240,Y=850,W=290,H=28},4);
    }
}
