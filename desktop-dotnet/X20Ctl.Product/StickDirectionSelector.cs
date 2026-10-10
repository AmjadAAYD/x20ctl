using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>Programmed eight-way direction, never a live analog-position indicator.</summary>
public sealed class StickDirectionSelector : Button
{
    public StickHeading Direction { get; private set; }
    public event Action<StickHeading>? DirectionChanged;
    public StickDirectionSelector()
    {
        Width=Height=108;Focusable=true;FocusVisualStyle=null;Cursor=Cursors.Hand;
        Template=new ControlTemplate(typeof(Button)){VisualTree=new FrameworkElementFactory(typeof(Grid))};
        RenderTransformOrigin=new(.5,.5);
    }
    public void SetDirection(StickHeading value){Direction=value;InvalidateVisual();}
    private void Choose(StickHeading value){SetDirection(value);DirectionChanged?.Invoke(value);}
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e){Focus();CaptureMouse();ChooseAt(e.GetPosition(this));e.Handled=true;}
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e){ReleaseMouseCapture();e.Handled=true;}
    protected override void OnPreviewMouseMove(MouseEventArgs e){if(IsMouseCaptured)ChooseAt(e.GetPosition(this));}
    private void ChooseAt(Point point)
    {
        double x=point.X-54,y=point.Y-54;
        if(Math.Sqrt(x*x+y*y)<17){Choose(StickHeading.Neutral);return;}
        int direction=((int)Math.Round((Math.Atan2(y,x)+Math.PI/2)/(Math.PI/4))+8)%8+1;Choose((StickHeading)direction);
    }
    public bool HandleKey(KeyEventArgs e)
    {
        if(e.Key is Key.Space or Key.Home){Choose(StickHeading.Neutral);e.Handled=true;return true;}
        StickHeading? value=e.Key switch{Key.Up=>StickHeading.Up,Key.Right=>StickHeading.Right,Key.Down=>StickHeading.Down,Key.Left=>StickHeading.Left,_=>null};
        if(value==null)return false;
        if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)
        {
            bool right=Direction is StickHeading.Right or StickHeading.UpRight or StickHeading.DownRight;
            bool left=Direction is StickHeading.Left or StickHeading.UpLeft or StickHeading.DownLeft;
            bool up=Direction is StickHeading.Up or StickHeading.UpRight or StickHeading.UpLeft;
            bool down=Direction is StickHeading.Down or StickHeading.DownRight or StickHeading.DownLeft;
            value=value switch{StickHeading.Up when right=>StickHeading.UpRight,StickHeading.Up when left=>StickHeading.UpLeft,StickHeading.Down when right=>StickHeading.DownRight,StickHeading.Down when left=>StickHeading.DownLeft,StickHeading.Right when up=>StickHeading.UpRight,StickHeading.Right when down=>StickHeading.DownRight,StickHeading.Left when up=>StickHeading.UpLeft,StickHeading.Left when down=>StickHeading.DownLeft,_=>value};
        }
        Choose(value.Value);e.Handled=true;return true;
    }
    protected override void OnRender(DrawingContext dc)
    {
        var center=new Point(54,54);dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(14,27,45)),new Pen(new SolidColorBrush(Color.FromRgb(94,136,186)),1.5),center,49,49);
        for(int i=0;i<8;i++){double angle=-Math.PI/2+i*Math.PI/4;dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(130,112,158,218)),2),new(54+Math.Cos(angle)*39,54+Math.Sin(angle)*39),new(54+Math.Cos(angle)*45,54+Math.Sin(angle)*45));}
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(100,158,192,230)),1),center,16,16);
        double selectedAngle=-Math.PI/2+((int)Direction-1)*Math.PI/4;var dot=Direction==StickHeading.Neutral?center:new Point(54+Math.Cos(selectedAngle)*29,54+Math.Sin(selectedAngle)*29);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90,99,187,248)),3),center,dot);dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(123,202,255)),new Pen(Brushes.White,1.5),dot,9,9);
        var caption=new FormattedText(Direction==StickHeading.Neutral?"NEUTRAL":"DIRECTION",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,new SolidColorBrush(Color.FromRgb(170,196,227)),VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(caption,new(54-caption.Width/2,83));
    }
}
