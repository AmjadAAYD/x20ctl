using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace X20Ctl.Product;
/// <summary>Cycles only the Support rim. Unloading stops the clock; reduced motion keeps a static rim.</summary>
public sealed class CyclingSupportOutline:Border
{
    readonly RotateTransform rotation=new(0,.5,.5);
    public bool IsCycling=>rotation.HasAnimatedProperties;
    public CyclingSupportOutline()
    {
        CornerRadius=new(21);Padding=new(3);BorderThickness=new(1.5);Background=new SolidColorBrush(Color.FromRgb(20,33,59));
        BorderBrush=new LinearGradientBrush(new GradientStopCollection{new(Color.FromRgb(98,186,255),0),new(Color.FromRgb(255,110,163),.35),new(Color.FromRgb(255,186,104),.65),new(Color.FromRgb(98,186,255),1)},new Point(0,0),new Point(1,1)){RelativeTransform=rotation};
        Loaded+=(_,_)=>{bool allowed=DS.Motion;for(DependencyObject? owner=this;owner!=null;owner=VisualTreeHelper.GetParent(owner)){if(owner is ZoneScene zone&&zone.ReducedMotion)allowed=false;if(owner is StudioScene studio&&!studio.AnimationsEnabled)allowed=false;}if(allowed)rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromSeconds(5)){RepeatBehavior=RepeatBehavior.Forever});};
        Unloaded+=(_,_)=>rotation.BeginAnimation(RotateTransform.AngleProperty,null);
    }
}
