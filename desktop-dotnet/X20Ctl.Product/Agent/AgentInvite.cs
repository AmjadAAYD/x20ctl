using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product.Agent;

/// <summary>A small controller conversation card, attached to the hero rather than a floating generic tooltip.</summary>
public static class AgentInvite
{
    public const string Text = "Need help? I will assist you!";
    public static Button Make(Action open, bool tailRight, double size = 14)
    {
        var edge=new SolidColorBrush(Color.FromArgb(150,135,190,250));
        var fill=new LinearGradientBrush(Color.FromRgb(35,62,100),Color.FromRgb(12,25,48),90);
        var shape=new Path { Data=Geometry.Parse(tailRight
            ? "M 17,0 H 243 Q 260,0 260,17 V 28 L 270,35 L 260,42 V 53 Q 260,70 243,70 H 17 Q 0,70 0,53 V 17 Q 0,0 17,0 Z"
            : "M 17,0 H 243 Q 260,0 260,17 V 53 Q 260,70 243,70 H 43 L 24,82 L 28,70 H 17 Q 0,70 0,53 V 17 Q 0,0 17,0 Z"),Fill=fill,Stroke=edge,StrokeThickness=1,
            Effect=new DropShadowEffect { Color=Colors.Black,BlurRadius=18,ShadowDepth=5,Opacity=.3 },IsHitTestVisible=false };
        var label=new StackPanel { Margin=new(64,10,27,0) };
        label.Children.Add(Kit.Text("AGENT XCTL",10,"DS.TextSoft",FontWeights.SemiBold));
        label.Children.Add(Kit.Text("Need help?",size+1,"DS.Text",FontWeights.SemiBold));
        label.Children.Add(Kit.Text("I will assist you!",11,"DS.TextSoft"));
        var icon=new Border { Width=36,Height=36,CornerRadius=new(11),Background=new SolidColorBrush(Color.FromArgb(85,85,145,225)),BorderBrush=new SolidColorBrush(Color.FromArgb(80,165,210,255)),BorderThickness=new(1),Margin=new(15,17,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Child=AgentMark.Glyph(27) };
        var art=new Grid { Width=tailRight?271:261,Height=tailRight?71:83,Children={shape,icon,label,Kit.Text("↗",15,"DS.AccentHi").Also(t=>{t.HorizontalAlignment=HorizontalAlignment.Right;t.VerticalAlignment=VerticalAlignment.Top;t.Margin=new(0,17,16,0);})} };
        var scale=new ScaleTransform(1,1);
        var button=new Button { Content=art,Cursor=Cursors.Hand,ToolTip=Text,FocusVisualStyle=null,RenderTransform=scale,RenderTransformOrigin=new(.5,.5),Template=new ControlTemplate(typeof(Button)){VisualTree=new FrameworkElementFactory(typeof(ContentPresenter))} };
        System.Windows.Automation.AutomationProperties.SetName(button,"Open Agent XCTL: "+Text);
        button.Click+=(_,_)=>open();
        void Hover(bool on) { shape.Stroke=on?Brushes.White:edge; if(DS.Motion){var a=new DoubleAnimation(on?1.025:1,TimeSpan.FromMilliseconds(160)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};scale.BeginAnimation(ScaleTransform.ScaleXProperty,a);scale.BeginAnimation(ScaleTransform.ScaleYProperty,a);} }
        button.MouseEnter+=(_,_)=>Hover(true);button.MouseLeave+=(_,_)=>Hover(false);button.GotKeyboardFocus+=(_,_)=>Hover(true);button.LostKeyboardFocus+=(_,_)=>Hover(false);
        return button;
    }
}
