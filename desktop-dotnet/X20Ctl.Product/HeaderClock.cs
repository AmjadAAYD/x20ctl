using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace X20Ctl.Product;
/// <summary>Actual local wall-clock time. No background timer after the header unloads.</summary>
public sealed class HeaderClock:TextBlock
{
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    public HeaderClock(){FontFamily=new FontFamily("Segoe UI");FontSize=14;Foreground=new SolidColorBrush(Color.FromRgb(190,203,225));VerticalAlignment=VerticalAlignment.Center;Margin=new(12,0,14,0);MinWidth=42;TextAlignment=TextAlignment.Center;System.Windows.Automation.AutomationProperties.SetName(this,"Local time");timer.Tick+=(_,_)=>Refresh();Loaded+=(_,_)=>{Refresh();timer.Start();};Unloaded+=(_,_)=>timer.Stop();Refresh();}
    public void Refresh(){var now=DateTime.Now;Text=now.ToString("HH:mm");ToolTip=now.ToString("dddd, dd MMMM yyyy")+" · "+TimeZoneInfo.Local.DisplayName;}
}
