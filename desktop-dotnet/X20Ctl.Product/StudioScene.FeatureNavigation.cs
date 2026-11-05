using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product;

public partial class StudioScene
{
    // Shared feature tabs remain reachable in themes that replace the standard navigation.
    void BuildFeatureNavigation()
    {
        var tabs=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,58,110,0)};
        Panel.SetZIndex(tabs,201);
        void Tab(string name,Action action)
        {
            var tab=new Button {Content=name,Style=(Style)FindResource("ConsoleAction"),Padding=new(16,7,16,7),Margin=new(4,0,0,0)};
            tab.Click+=(_,_)=>action();tabs.Children.Add(tab);RegisterFocus(tab);
        }
        if(Model.Id=="x20_pro")Tab("RGB",()=>ShowManagement("RGB"));
        if(ModelProfiles.For(Model.Id).HasScreen)Tab("Screen",()=>ShowManagement("Screen"));
        StudioShell.Children.Add(tabs);
        Visibility? previousVisibility=null;
        Brush? previousBackground=null;
        LayoutUpdated+=(_,_)=>
        {
            bool themed=ThemeCatalog.Current.Id is "nexus" or "dusk" or "hollow" or "nocturne" or "sunrise";
            bool feature=ManagementPage is "RGB" or "Screen";
            var target=themed&&!feature&&tabs.Children.Count>0?Visibility.Visible:Visibility.Collapsed;
            if(tabs.Visibility!=target)tabs.Visibility=target;
            if(themed&&feature)
            {
                if(previousVisibility==null){previousVisibility=RootLayout.Visibility;previousBackground=RootLayout.Background;}
                if(Panel.GetZIndex(RootLayout)!=100)Panel.SetZIndex(RootLayout,100);
                if(RootLayout.Visibility!=Visibility.Visible)RootLayout.Visibility=Visibility.Visible;
                if(RootLayout.Background!=BackgroundFeature)RootLayout.Background=BackgroundFeature;
            }
            else if(previousVisibility.HasValue)
            {
                Panel.SetZIndex(RootLayout,0);RootLayout.Visibility=previousVisibility.Value;RootLayout.Background=previousBackground;previousVisibility=null;
            }
        };
    }
    static readonly Brush BackgroundFeature=new SolidColorBrush(Color.FromRgb(16,24,36));
}
