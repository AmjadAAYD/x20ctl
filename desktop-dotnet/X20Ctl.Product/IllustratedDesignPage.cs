using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace X20Ctl.Product;

// Native primitives compiled from each separately authored design. There is no browser at runtime,
// screenshot UI, or common layout substituted for the seven designs. Real controls occupy measured slots.
internal sealed class IllustratedDesignPage:Canvas
{
    public static readonly DependencyProperty WatchedTextProperty=DependencyProperty.Register(nameof(WatchedText),typeof(string),typeof(IllustratedDesignPage),new PropertyMetadata("",(d,_)=>((IllustratedDesignPage)d).Refresh()));
    public string WatchedText{get=>(string)GetValue(WatchedTextProperty);set=>SetValue(WatchedTextProperty,value);}
    internal sealed class Part
    {
        public string Kind{get;set;}="";public string Key{get;set;}="";public string Text{get;set;}="";
        public string Fill{get;set;}="";public string Gradient{get;set;}="";public string Border{get;set;}="";
        public string Color{get;set;}="";public string Font{get;set;}="Segoe UI";public string Clip{get;set;}="none";
        public string Align{get;set;}="left";
        public double X{get;set;}public double Y{get;set;}public double W{get;set;}public double H{get;set;}
        public double Size{get;set;}=14;public int Weight{get;set;}=400;public double Line{get;set;}
        public double Opacity{get;set;}=1;public double[] Radii{get;set;}=[];
        public double Angle{get;set;}
        public List<Part> Active{get;set;}=[];
        public List<Part> Primitives{get;set;}=[];public bool Empty{get;set;}
    }
    private sealed class Design
    {
        public string Veil{get;set;}="";public List<Part> Primitives{get;set;}=[];
        public List<Part> Widgets{get;set;}=[];public List<Part> Pads{get;set;}=[];public Part? Graph{get;set;}
    }
    private readonly Design design;
    private readonly List<DesignDrawing> drawings=[];
    private Border? notice;
    internal void ShowNotice(string message)
    {
        if(notice==null)
        {
            bool indexed=IllustratedThemeUI.Current!.Id is "gothic" or "manga" or "tokyo-night" or "crimson-rebellion";
            notice=new Border{Background=IllustratedThemeUI.Brush(ThemeCatalog.Current.Deep),Padding=new(12,6,12,6),Child=new TextBlock{Foreground=IllustratedThemeUI.Brush(ThemeCatalog.Current.Text),FontSize=12,TextWrapping=TextWrapping.Wrap}};
            Place(notice,new(){X=indexed?250:48,Y=838,W=indexed?1080:1280,H=46},5);
        }
        ((TextBlock)notice.Child).Text=message;
    }
    internal Func<string,string,string>? TextValue{get;set;}
    internal IReadOnlyList<Part> Pads=>design.Pads;
    internal Part? Graph=>design.Graph;
    internal Rect? MarkTarget(Visual to)
    {
        var logo=design.Primitives.FirstOrDefault(p=>p.Kind=="logo");if(logo==null||!IsLoaded)return null;
        var ink=IllustratedThemeUI.LogoInk(IllustratedThemeUI.Current!);
        return TransformToVisual(to).TransformBounds(new Rect(logo.X+ink.X/512*logo.W,logo.Y+ink.Y/512*logo.H,ink.Width/512*logo.W,ink.Height/512*logo.H));
    }
    private readonly string page;
    internal string? ArtworkPageOverride{get;set;}
    internal IllustratedDesignPage(string page,Func<Part,FrameworkElement?> resolve)
    {
        this.page=page;Width=1600;Height=900;ClipToBounds=true;Background=Brushes.Transparent;
        Tag="illustrated:matched:"+page+":"+IllustratedThemeUI.Current!.Id;
        var path=Path.Combine(AppContext.BaseDirectory,"Brand","illustrated",IllustratedThemeUI.Current.Id,"layouts",page+".json");
        design=JsonSerializer.Deserialize<Design>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException(path);
        var boxes=new DesignDrawing(this,design.Primitives.Where(p=>p.Kind!="text").ToArray()){Width=1600,Height=900,IsHitTestVisible=false};drawings.Add(boxes);Children.Add(boxes);
        var text=new DesignDrawing(this,design.Primitives.Where(p=>p.Kind=="text").ToArray()){Width=1600,Height=900,IsHitTestVisible=false};drawings.Add(text);Children.Add(text);SetZIndex(text,2);
        foreach(var slot in design.Widgets)
        {
            var control=resolve(slot);if(control==null)continue;
            if(control is ButtonBase button)Dress(button,slot);
            if(control is Slider slider&&slider.Template!=null)
            {
                string accent=ThemeCatalog.Current.Accent.ToString();
                string template=System.Windows.Markup.XamlWriter.Save(slider.Template).Replace("#FFACD7FA",accent).Replace("#ACD7FA",accent).Replace("#FF448DDC",accent).Replace("#448DDC",accent);
                slider.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(template);
            }
            Place(control,slot,3);
        }
        if(page is "buttons" or "curves" or "shell"&&resolve(new(){Key="agent"}) is {} agent)
            Place(agent,new(){X=830,Y=20,W=110,H=28},4);
        if(page is "buttons" or "curves" or "shell"&&resolve(new(){Key="more-pages"}) is {} more)
            Place(more,new(){X=950,Y=20,W=110,H=28},4);
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};
        timer.Tick+=(_,_)=>Refresh();Loaded+=(_,_)=>timer.Start();Unloaded+=(_,_)=>timer.Stop();
    }
    internal void Place(FrameworkElement element,Part slot,int z=1)
    {
        IllustratedComposition.Detach(element);element.Margin=new(0);element.Width=slot.W;element.Height=slot.H;
        if(element.Tag==null)element.Tag="illustrated:slot:"+slot.Key;
        element.MinWidth=element.MinHeight=0;SetLeft(element,slot.X);SetTop(element,slot.Y);SetZIndex(element,z);Children.Add(element);
        if(Math.Abs(slot.Angle)>.01){element.RenderTransformOrigin=new(.5,.5);element.RenderTransform=new RotateTransform(slot.Angle);}
    }
    internal void Refresh(){foreach(var drawing in drawings)drawing.InvalidateVisual();InvalidateVisual();}
    internal string? DisplayedText(string key)
    {
        var part=design.Primitives.Concat(design.Widgets.SelectMany(p=>p.Primitives)).FirstOrDefault(p=>p.Kind=="text"&&p.Key==key);
        return part==null?null:TextValue?.Invoke(key,part.Text)??part.Text;
    }
    internal static bool IsDesignControl(ContentControl control)=>control.Content is DesignDrawing;
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var spec=IllustratedThemeUI.Current!;
        string name=ArtworkPageOverride??(page switch{"buttons"=>"Buttons","curves"=>"Curves","tools"=>"Tools",_=>"Controller Zone"});
        if(IllustratedThemeUI.Image(spec.Wallpaper(name)) is {} image)dc.DrawImage(image,new(0,0,1600,900));
        dc.DrawRectangle(Paint("transparent",design.Veil),null,new(0,0,1600,900));
    }
    private void Dress(ButtonBase control,Part slot)
    {
        IllustratedThemeUI.PreserveDesign(control);
        control.Style=new Style(control.GetType());
        var target=new FrameworkElementFactory(typeof(Border));target.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        target.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        control.Template=new ControlTemplate(control.GetType()){VisualTree=target};
        control.Padding=new(0);control.HorizontalContentAlignment=HorizontalAlignment.Stretch;control.VerticalContentAlignment=VerticalAlignment.Stretch;
        control.FocusVisualStyle=null;control.RenderTransform=Transform.Identity;control.Background=Brushes.Transparent;
        if(System.Windows.Automation.AutomationProperties.GetName(control).Length==0)
            System.Windows.Automation.AutomationProperties.SetName(control,string.Join(" ",slot.Primitives.Where(p=>p.Kind=="text").Select(p=>p.Text.Trim())));
        var face=new DesignDrawing(this,slot.Primitives.ToArray(),control,slot.Active.ToArray()){Width=slot.W,Height=slot.H};
        control.Content=face;drawings.Add(face);
        void Update(){face.InvalidateVisual();}
        control.MouseEnter+=(_,_)=>Update();control.MouseLeave+=(_,_)=>Update();control.GotKeyboardFocus+=(_,_)=>Update();control.LostKeyboardFocus+=(_,_)=>Update();
        if(control is ToggleButton toggle){toggle.Checked+=(_,_)=>Update();toggle.Unchecked+=(_,_)=>Update();}
        control.IsEnabledChanged+=(_,_)=>{control.Opacity=control.IsEnabled?1:.4;Update();};control.Opacity=control.IsEnabled?1:.4;
    }
    private sealed class DesignDrawing(IllustratedDesignPage owner,Part[] parts,ButtonBase? button=null,Part[]? active=null):FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            bool chosen=button is ToggleButton{IsChecked:true} or {DataContext:true};
            foreach(var part in chosen&&active is {Length:>0}?active:parts)
            {
                double opacity=part.Opacity;
                // Disabled preview tabs become real capability-controlled tabs in the application.
                if(button!=null&&opacity<.45)opacity=Math.Min(1,opacity/.38);
                dc.PushOpacity(opacity);
                if(part.Kind=="text")
                {
                    string value=owner.TextValue?.Invoke(part.Key,part.Text)??part.Text;
                    if(value.Length>0)
                    {
                        var type=new Typeface(new FontFamily(part.Font.Replace("\"","").Replace("'","")),FontStyles.Normal,FontWeight.FromOpenTypeWeight(Math.Clamp(part.Weight,1,999)),FontStretches.Normal);
                        var formatted=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,type,Math.Max(1,part.Size),Paint(part.Color),VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        double drawn=formatted.WidthIncludingTrailingWhitespace;double scale=part.Key.Length==0&&drawn>0?part.W/drawn:1;
                        double x=part.X;if(part.Key.Length>0&&part.Align=="center")x+=(part.W-drawn)/2;
                        dc.PushTransform(new TranslateTransform(x,part.Y+(part.H-formatted.Height)/2));
                        dc.PushTransform(new ScaleTransform(Math.Clamp(scale,.5,2),1));dc.DrawText(formatted,new());dc.Pop();dc.Pop();
                    }
                }
                else if(part.Kind=="logo")
                {if(IllustratedThemeUI.Image(IllustratedThemeUI.Current!.Logo()) is {} image)dc.DrawImage(image,new(part.X,part.Y,part.W,part.H));}
                else
                {
                    var geometry=Outline(part);var fill=Paint(part.Fill,part.Gradient);
                    dc.DrawGeometry(fill,part.Line>0?new Pen(Paint(part.Border),part.Line):null,geometry);
                }
                dc.Pop();
            }
            if(button is {IsMouseOver:true} or {IsKeyboardFocusWithin:true}||chosen&&active is not {Length:>0})
            {
                var accent=IllustratedThemeUI.Brush(ThemeCatalog.Current.Accent);
                var shape=parts.FirstOrDefault(p=>p.Kind=="box");
                if(shape!=null)dc.DrawGeometry(null,new(accent,button?.IsKeyboardFocusWithin==true?2:1),Outline(new Part{Kind=shape.Kind,Clip=shape.Clip,Radii=shape.Radii,X=shape.X-6,Y=shape.Y-6,W=shape.W+12,H=shape.H+12}));
                if(button is ToggleButton{IsChecked:true})dc.DrawLine(new(accent,2),new(8,ActualHeight-3),new(Math.Max(8,ActualWidth-8),ActualHeight-3));
            }
        }
    }
    private static Geometry Outline(Part p)
    {
        if(p.Clip.StartsWith("polygon("))
        {
            var points=p.Clip[8..^1].Split(',').Select(pair=>pair.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries)).ToArray();
            double At(string text,double size)=>double.TryParse(text.TrimEnd('%','p','x'),NumberStyles.Float,CultureInfo.InvariantCulture,out double value)?text.EndsWith('%')?value/100*size:value:0;
            var polygon=new StreamGeometry();using(var c=polygon.Open())
            {var first=points[0];c.BeginFigure(new(p.X+At(first[0],p.W),p.Y+At(first[1],p.H)),true,true);foreach(var point in points.Skip(1))c.LineTo(new(p.X+At(point[0],p.W),p.Y+At(point[1],p.H)),true,false);}
            polygon.Freeze();return polygon;
        }
        double Radius(int i)=>p.Radii.Length>i?Math.Min(i%2==0?p.W/2:p.H/2,p.Radii[i]):0;
        double a=Radius(0),ay=Radius(1),b=Radius(2),by=Radius(3),c1=Radius(4),cy=Radius(5),d=Radius(6),dy=Radius(7),x=p.X,y=p.Y,w=p.W,h=p.H;
        var shape=new StreamGeometry();using(var c=shape.Open())
        {
            c.BeginFigure(new(x+a,y),true,true);c.LineTo(new(x+w-b,y),true,false);if(b>0)c.ArcTo(new(x+w,y+by),new(b,by),0,false,SweepDirection.Clockwise,true,false);
            c.LineTo(new(x+w,y+h-cy),true,false);if(c1>0)c.ArcTo(new(x+w-c1,y+h),new(c1,cy),0,false,SweepDirection.Clockwise,true,false);
            c.LineTo(new(x+d,y+h),true,false);if(d>0)c.ArcTo(new(x,y+h-dy),new(d,dy),0,false,SweepDirection.Clockwise,true,false);
            c.LineTo(new(x,y+ay),true,false);if(a>0)c.ArcTo(new(x+a,y),new(a,ay),0,false,SweepDirection.Clockwise,true,false);
        }shape.Freeze();return shape;
    }
    private static readonly Dictionary<string,Brush> paints=new();
    private static Brush Paint(string color,string gradient="")
    {
        string key=color+"|"+gradient;if(paints.TryGetValue(key,out var cached))return cached;
        Color Parse(string text)
        {
            if(text.StartsWith("rgb"))
            {var numbers=Regex.Matches(text,@"[\d.]+").Select(x=>double.Parse(x.Value,CultureInfo.InvariantCulture)).ToArray();return Color.FromArgb(numbers.Length>3?(byte)Math.Clamp(numbers[3]*255,0,255):(byte)255,(byte)numbers[0],(byte)numbers[1],(byte)numbers[2]);}
            try{return (Color)ColorConverter.ConvertFromString(text);}catch{return Colors.Transparent;}
        }
        Brush brush=new SolidColorBrush(Parse(color));
        if(gradient.StartsWith("linear-gradient"))
        {
            var colors=Regex.Matches(gradient,@"rgba?\([^)]*\)|#[a-fA-F0-9]{3,8}").Select(x=>Parse(x.Value)).ToArray();
            if(colors.Length>1)
            {
                var degrees=Regex.Match(gradient,@"([-\d.]+)deg");double angle=degrees.Success?double.Parse(degrees.Groups[1].Value,CultureInfo.InvariantCulture):180;
                double dx=Math.Sin(angle*Math.PI/180)/2,dy=-Math.Cos(angle*Math.PI/180)/2;
                var linear=new LinearGradientBrush{StartPoint=new(.5-dx,.5-dy),EndPoint=new(.5+dx,.5+dy)};
                for(int i=0;i<colors.Length;i++)linear.GradientStops.Add(new(colors[i],i/(double)(colors.Length-1)));brush=linear;
            }
        }
        brush.Freeze();paints[key]=brush;return brush;
    }
}
