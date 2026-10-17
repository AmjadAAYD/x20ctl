using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace X20Ctl.Product;
public partial class StudioScene
{
    private RadioButton[] navigation=[];
    private readonly TranslateTransform navigationSlide=new();
    private readonly ScaleTransform navigationLensScale=new(1,1);
    private bool arrangingNavigation,navigationQueued;
    private RadioButton? indicatorTab;
    private double lensTargetX=double.NaN,lensTargetWidth=double.NaN;
    private void PreviousTabClick(object sender,RoutedEventArgs e)=>StepTab(-1);
    private void NextTabClick(object sender,RoutedEventArgs e)=>StepTab(1);
    private void StepTab(int step){int current=Array.FindIndex(navigation,t=>t.IsChecked==true);navigation[(current+step+navigation.Length)%navigation.Length].RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));}
    private void QueueNavigation(){if(navigationQueued)return;navigationQueued=true;Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>{navigationQueued=false;UpdateNavigation();}));}
    private void BuildNavigation()
    {
        navigation=[DashboardNav,ButtonsNav,CurvesNav,MacrosNav,VibrationNav,PcModeNav,DeviceNav,MouseNav,SetupsNav];
        string group="studio-tabs-"+Guid.NewGuid().ToString("N");DiagnosticsNav.GroupName=group;DiagnosticsNav.Checked+=(_,_)=>QueueNavigation();foreach(var tab in navigation){tab.GroupName=group;tab.Checked+=(_,_)=>QueueNavigation();}
        NavigationIndicator.RenderTransform=new TransformGroup{Children={navigationLensScale,navigationSlide}};
        NavigationIndicator.RenderTransformOrigin=new(.5,.5);
        NavigationIndicator.Effect=null;
        NavigationMore.ContextMenu=new ContextMenu();NavigationMore.Click+=(_,_)=>NavigationMore.ContextMenu.IsOpen=true;
        NavigationLayer.SizeChanged+=(_,_)=>QueueNavigation();Loaded+=(_,_)=>QueueNavigation();
    }
    private void UpdateNavigation()
    {
        if(arrangingNavigation||navigation.Length==0||NavigationLayer.ActualWidth<=0)return;arrangingNavigation=true;
        try
        {
            // Reserve the bumper positions even in capture pages: tab coordinates never shift under the lens.
            PreviousTab.Visibility=NextTab.Visibility=ManagementPage is "Diagnostics" or "Mouse"?Visibility.Hidden:Visibility.Visible;
            double available=Math.Max(80,NavigationLayer.ActualWidth-152),used=0;
            foreach(var tab in navigation){tab.Visibility=Visibility.Visible;tab.Measure(new Size(double.PositiveInfinity,48));used+=tab.DesiredSize.Width;}
            NavigationMore.ContextMenu!.Items.Clear();
            foreach(var tab in new[]{PcModeNav,MouseNav,VibrationNav,MacrosNav,CurvesNav,DeviceNav,SetupsNav})
            {
                if(used+(NavigationMore.ContextMenu.Items.Count>0?90:0)<=available)break;
                used-=tab.DesiredSize.Width;tab.Visibility=Visibility.Collapsed;
                var item=new MenuItem{Header=tab.Content,IsCheckable=true,IsChecked=tab.IsChecked==true};var destination=tab;
                item.Click+=(_,_)=>destination.RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));NavigationMore.ContextMenu.Items.Add(item);
            }
            NavigationMore.Visibility=NavigationMore.ContextMenu.Items.Count>0?Visibility.Visible:Visibility.Collapsed;
            NavigationTrack.Width=Math.Min(available,used+(NavigationMore.ContextMenu.Items.Count>0?90:0));NavigationTrack.HorizontalAlignment=HorizontalAlignment.Left;
            Grid.SetColumn(NextTab,0);Grid.SetColumnSpan(NextTab,3);NextTab.HorizontalAlignment=HorizontalAlignment.Left;NextTab.Margin=new(76+NavigationTrack.Width+6,0,0,4);
            NavigationTabs.UpdateLayout();var active=navigation.FirstOrDefault(t=>t.IsChecked==true);if(active==null){NavigationIndicator.Visibility=Visibility.Collapsed;return;}
            NavigationIndicator.Visibility=Visibility.Visible;
            FrameworkElement target=active.Visibility==Visibility.Visible?active:NavigationMore;
            double x=target.TranslatePoint(new Point(),NavigationTrack).X+14,width=28;
            bool changed=indicatorTab!=active;
            // Compare destinations, not animated samples. Duplicate notifications must not cancel an in-flight clock.
            if(!changed && Math.Abs(lensTargetX-x)<.5 && Math.Abs(lensTargetWidth-width)<.5)return;
            bool travel=animate && indicatorTab!=null && changed && double.IsFinite(lensTargetWidth);
            int direction=indicatorTab==null?1:Math.Sign(Array.IndexOf(navigation,active)-Array.IndexOf(navigation,indicatorTab));
            double fromX=navigationSlide.X,fromWidth=double.IsFinite(NavigationIndicator.Width)?NavigationIndicator.Width:width;
            navigationSlide.BeginAnimation(TranslateTransform.XProperty,null);NavigationIndicator.BeginAnimation(WidthProperty,null);
            navigationSlide.X=x;NavigationIndicator.Width=width;lensTargetX=x;lensTargetWidth=width;
            if(travel)
            {
                var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};var duration=TimeSpan.FromMilliseconds(210);
                navigationSlide.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(fromX,x,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
                NavigationIndicator.BeginAnimation(WidthProperty,new DoubleAnimation(fromWidth,width,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            }
            else {navigationLensScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);navigationLensScale.ScaleX=1;}
            indicatorTab=active;
        }
        finally{arrangingNavigation=false;}
    }
}
