using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace X20Ctl.Product;

/// <summary>Native presentation for the illustrated themes; editor instances and actions stay intact.</summary>
internal static class IllustratedThemeUI
{
    public static IllustratedThemeSpec? Current => IllustratedThemeSpec.Find(ThemeCatalog.Current.Id);
    /// <summary>The theme replaces pages with fixed-canvas layouts (false for native skins on Default's layout).</summary>
    public static bool Matched => Current is {Native:false};
    internal static readonly DependencyProperty PrimaryProperty=DependencyProperty.RegisterAttached("Primary",typeof(bool),typeof(IllustratedThemeUI),new PropertyMetadata(false));
    private static bool installed;
    private static readonly DependencyProperty StyledProperty=DependencyProperty.RegisterAttached("Styled",typeof(bool),typeof(IllustratedThemeUI),new PropertyMetadata(false));
    private static readonly DependencyProperty QueuedProperty=DependencyProperty.RegisterAttached("Queued",typeof(bool),typeof(IllustratedThemeUI),new PropertyMetadata(false));
    private static readonly Dictionary<string,WeakReference<BitmapSource>> images=new();
    private static readonly Dictionary<string,Rect> logoBounds=new();
    internal static void PreserveDesign(Control control)=>control.SetValue(StyledProperty,true);
    internal static Color C(uint rgb)=>Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
    internal static Brush Brush(Color color){var b=new SolidColorBrush(color);b.Freeze();return b;}
    public static IEnumerable<AppTheme> Catalog()=>IllustratedThemeSpec.All.Select(s=>new AppTheme(s.Id,s.Name,
        s.Surface switch {"p4"=>"Black keys · yellow viewfinders","p3"=>"Deep water · white blades","alert"=>"Violet armour · lime focus","slate"=>"Relief map · tablet plates","spider"=>"Red signal · every world",
            "chrome"=>"Lavender marble · sunset chrome","gel"=>"Blue-hour glass · aqua gel","iron"=>"Black iron · crimson tracery","ink"=>"Fine ink · halftone · one red","glass"=>"Rain glass · quiet blue","neon"=>"Neon bevels · arcade highway",_=>"Torn paper · thick comic ink"},
        C(s.Void),C(s.Deep),ThemeCatalog.Mix(C(s.Deep),C(s.Text),.04),C(s.Accent),C(s.Secondary),
        s.Native?(Luma(C(s.Accent))>140?ThemeCatalog.Mix(C(s.Void),Colors.Black,.5):Colors.White):s.Id is "vaporwave" or "frutiger-aero" or "tokyo-night" ? C(s.Void):C(s.Text),
        C(s.Text),ThemeCatalog.Mix(C(s.Text),C(s.Deep),.18),ThemeCatalog.Mix(C(s.Text),C(s.Deep),.38),
        ThemeCatalog.Mix(C(s.Text),C(s.Deep),.73),C(s.Accent),C(s.Secondary),s.Display,"Segoe UI Variable Text, Segoe UI",s.Radius));

    internal static double Luma(Color c)=>.299*c.R+.587*c.G+.114*c.B;
    public static BitmapSource? Image(string relative)
    {
        string path=Path.Combine(AppContext.BaseDirectory,relative.Replace('/',Path.DirectorySeparatorChar));
        if(images.TryGetValue(path,out var weak)&&weak.TryGetTarget(out var cached))return cached;
        if(!File.Exists(path))return null;
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.EndInit();image.Freeze();
        if(images.Count>80)images.Clear();
        images[path]=new(image);return image;
    }
    public static void Install()
    {
        if(Current==null||installed)return;installed=true;
        EventManager.RegisterClassHandler(typeof(Control),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>StyleControl((Control)sender)));
        EventManager.RegisterClassHandler(typeof(Border),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>StyleBorder((Border)sender)));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Image),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>
        {
            if(Current is not {Native:false} s)return;
            var image=(System.Windows.Controls.Image)sender;
            string uri=(image.Source as BitmapImage)?.UriSource?.OriginalString??image.Source?.ToString()??"";
            if(image.Name is "BrandMark" or "StudioBrandMark"||uri.EndsWith("/Assets/icon.png",StringComparison.OrdinalIgnoreCase)||uri.EndsWith("/Assets/brand.png",StringComparison.OrdinalIgnoreCase))image.Source=Image(s.Logo());
        }));
    }
    internal static Rect LogoInk(IllustratedThemeSpec spec)
    {
        if(logoBounds.TryGetValue(spec.Id,out var box))return box;
        if(Image(spec.Logo()) is not {} source)return new Rect(0,0,512,512);
        var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        int width=bitmap.PixelWidth,height=bitmap.PixelHeight;var pixels=new byte[width*height*4];bitmap.CopyPixels(pixels,width*4,0);
        int left=width,top=height,right=0,bottom=0;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[(y*width+x)*4+3]>0){left=Math.Min(left,x);right=Math.Max(right,x+1);top=Math.Min(top,y);bottom=Math.Max(bottom,y+1);}
        box=right>left?new Rect(left,top,right-left,bottom-top):new Rect(0,0,width,height);logoBounds[spec.Id]=box;return box;
    }
    private static void StyleControl(Control control)
    {
        if(Current is not {} s||(bool)control.GetValue(StyledProperty))return;
        // Controller hotspots, graph handles and window glyphs retain their specialised geometry.
        bool button=control.GetType()==typeof(Button),tab=control.GetType()==typeof(RadioButton);
        bool input=control.GetType()==typeof(TextBox),slider=control.GetType()==typeof(Slider);
        if(!(button||tab||input||slider))return;
        if(control.FontFamily.Source.Contains("MDL2",StringComparison.OrdinalIgnoreCase)||control.Style is {} chromeStyle&&ReferenceEquals(chromeStyle,control.TryFindResource("ChromeAction")))return;
        // Native skins: a button whose picture lives in its template (no content) keeps that template.
        if(s.Native&&(button||tab)&&control is ContentControl {Content:null})return;
        control.SetValue(StyledProperty,true);
        bool primary=s.Native&&button&&control.Background is SolidColorBrush {Color:var pc}&&pc.A>200&&Near(pc,ThemeCatalog.Current.Accent);
        if(s.Native&&button&&control.Background is GradientBrush {GradientStops.Count:>0} gb&&gb.GradientStops.All(g=>g.Color.A>200&&Near(g.Color,ThemeCatalog.Current.Accent,90)))primary=true;
        control.SetValue(PrimaryProperty,primary);
        var style=new Style(control.GetType(),control.Style);
        if(s.Native&&control.TryFindResource("DS.InheritText") is Style inherit)style.Resources.Add(typeof(TextBlock),inherit);
        style.Setters.Add(new Setter(Control.FontFamilyProperty,new FontFamily(tab||s.Native&&button?s.Display:ThemeCatalog.Current.Body)));
        style.Setters.Add(new Setter(Control.ForegroundProperty,Brush(primary?ThemeCatalog.Current.OnAccent:ThemeCatalog.Current.Text)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty,Brush(ThemeCatalog.Current.Rule)));
        if(button||tab)
        {
            if(tab){style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(14,5,14,5)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Center));}
            var face=new FrameworkElementFactory(typeof(IllustratedControlFace),"ThemeFace");
            var content=new FrameworkElementFactory(s.Native?typeof(NativePresenter):typeof(ContentPresenter));
            content.SetValue(FrameworkElement.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty,new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);face.AppendChild(content);
            var template=new ControlTemplate(control.GetType()){VisualTree=face};
            void Trigger(DependencyProperty property,object value,DependencyProperty target,object result)
            {var trigger=new Trigger{Property=property,Value=value};trigger.Setters.Add(new Setter(target,result,"ThemeFace"));template.Triggers.Add(trigger);}
            Trigger(UIElement.IsMouseOverProperty,true,IllustratedControlFace.ActiveProperty,true);
            Trigger(UIElement.IsKeyboardFocusWithinProperty,true,IllustratedControlFace.ActiveProperty,true);
            Trigger(ButtonBase.IsPressedProperty,true,IllustratedControlFace.DownProperty,true);
            if(tab)Trigger(ToggleButton.IsCheckedProperty,true,IllustratedControlFace.ChosenProperty,true);
            if(tab&&s.Native)
            {var chosen=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=true};chosen.Setters.Add(new Setter(Control.ForegroundProperty,Brush(ThemeCatalog.Current.OnAccent)));template.Triggers.Add(chosen);}
            Trigger(UIElement.IsEnabledProperty,false,UIElement.OpacityProperty,.42);
            style.Setters.Add(new Setter(Control.TemplateProperty,template));
            control.SetCurrentValue(Control.TemplateProperty,template);
            if(s.Native)
            {   // pages change a key's paint to mark it selected; redraw the face (and its ink) when they do
                void Redraw(object? _,EventArgs __){if(control.Template?.FindName("ThemeFace",control) is UIElement face)face.InvalidateVisual();}
                System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Control.BackgroundProperty,control.GetType())?.AddValueChanged(control,Redraw);
                System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Control.ForegroundProperty,control.GetType())?.AddValueChanged(control,Redraw);
            }
        }
        if(input)
        {
            style.Setters.Add(new Setter(Control.BackgroundProperty,Brush(ThemeCatalog.Alpha(C(s.Deep),240))));
            style.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty,Brush(C(s.Text))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(s.Surface=="paper"?2:1)));
        }
        if(slider)style.Setters.Add(new Setter(Control.ForegroundProperty,Brush(C(s.Accent))));
        control.Style=style;
    }
    private static bool Near(Color a,Color b,int tolerance=40)=>Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)<=tolerance;
    private static void StyleBorder(Border border)
    {
        if(Current is not {} s||(bool)border.GetValue(StyledProperty))return;
        border.SetValue(StyledProperty,true);
        if(s.Native){NativeFaces.Dress(border,s);return;}
        if(s.Surface is "ink" or "paper" or "iron"&&border.Background is GradientBrush)
        {border.Background=Brush(ThemeCatalog.Alpha(C(s.Deep),230));border.CornerRadius=new(s.Radius);border.BorderBrush=Brush(ThemeCatalog.Current.Rule);return;}
        if(border.Background is not SolidColorBrush {Color.A:>100} bg)return;
        var c=bg.Color;
        if(Math.Max(c.R,Math.Max(c.G,c.B))>100)return; // keep semantic colours and artwork
        border.Background=Brush(ThemeCatalog.Alpha(C(s.Deep),border.Name is "OuterFrame"? (byte)255:(byte)218));
        if(border.BorderThickness!=new Thickness(0))
        {
            border.BorderBrush=Brush(C(s.Surface is "ink" or "paper"?s.Text:ThemeCatalog.Current.Rule.RGB()));
            border.CornerRadius=new(s.Radius);
            if(s.Surface=="paper")border.BorderThickness=new(2);
        }
    }
    private static uint RGB(this Color c)=>(uint)(c.R<<16|c.G<<8|c.B);
    public static void Skin(DependencyObject root)
    {
        if(Current is not {} spec)return;
        if(root is Control control)StyleControl(control);
        if(root is Border border)StyleBorder(border);
        if(root is System.Windows.Controls.Image image&&!spec.Native)
        {
            string uri=(image.Source as BitmapImage)?.UriSource?.OriginalString??image.Source?.ToString()??"";
            if(image.Name is "BrandMark" or "StudioBrandMark"||uri.EndsWith("/Assets/icon.png",StringComparison.OrdinalIgnoreCase)||uri.EndsWith("/Assets/brand.png",StringComparison.OrdinalIgnoreCase))image.Source=Image(spec.Logo());
        }
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)Skin(VisualTreeHelper.GetChild(root,i));
    }
    public static void QueueSkin(FrameworkElement root)
    {
        if(Current==null||(bool)root.GetValue(QueuedProperty))return;
        root.SetValue(QueuedProperty,true);
        root.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>{root.SetValue(QueuedProperty,false);Skin(root);}));
    }
    public static void Surface(Grid root,string page)
    {
        if(Current==null)return;
        root.Background=Brush(Colors.Transparent);
        foreach(var facet in root.Children.OfType<FacetBackdrop>())facet.Visibility=Visibility.Collapsed;
        root.Children.Insert(0,new ZoneBackdrop(true){ArtworkPage=page});
        root.Loaded+=(_,_)=>QueueSkin(root);QueueSkin(root);
    }
}

/// <summary>Seven different vector surfaces, drawn around real WPF control content.</summary>
public sealed class IllustratedControlFace:Decorator
{
    public static readonly DependencyProperty ActiveProperty=DependencyProperty.Register("Active",typeof(bool),typeof(IllustratedControlFace),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender,Changed));
    public static readonly DependencyProperty DownProperty=Flag("Down"),ChosenProperty=Flag("Chosen");
    private static readonly DependencyProperty BlendProperty=DependencyProperty.Register("Blend",typeof(double),typeof(IllustratedControlFace),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    private static void Changed(DependencyObject sender,DependencyPropertyChangedEventArgs e)
    {var face=(IllustratedControlFace)sender;double from=(double)face.GetValue(BlendProperty),to=(bool)e.NewValue?1:0;face.BeginAnimation(BlendProperty,null);face.SetValue(BlendProperty,to);if(DS.Motion)face.BeginAnimation(BlendProperty,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(120)){FillBehavior=FillBehavior.Stop});}
    private static DependencyProperty Flag(string name)=>DependencyProperty.Register(name,typeof(bool),typeof(IllustratedControlFace),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>Text colour for the content: dark on a solid accent face, else the control's own.</summary>
    public static readonly DependencyProperty InkProperty=DependencyProperty.Register("Ink",typeof(Brush),typeof(IllustratedControlFace),new PropertyMetadata(null));
    public Brush? Ink{get=>(Brush?)GetValue(InkProperty);set=>SetValue(InkProperty,value);}
    public bool Active{get=>(bool)GetValue(ActiveProperty);set=>SetValue(ActiveProperty,value);}
    public bool Down{get=>(bool)GetValue(DownProperty);set=>SetValue(DownProperty,value);}
    public bool Chosen{get=>(bool)GetValue(ChosenProperty);set=>SetValue(ChosenProperty,value);}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);if(IllustratedThemeUI.Current is not {} s||ActualWidth<2||ActualHeight<2)return;
        var t=ThemeCatalog.Current;double w=ActualWidth,h=ActualHeight;bool hot=Active||Chosen;
        if(s.Native)
        {
            // selected = checked, a primary action, or a page marking its pick by painting the key light or in the accent
            bool painted=TemplatedParent is Control {Background:SolidColorBrush {Color:var paint}} owner&&DependencyPropertyHelper.GetValueSource(owner,Control.BackgroundProperty).BaseValueSource==BaseValueSource.Local&&paint.A>200&&(IllustratedThemeUI.Luma(paint)>170||Math.Abs(paint.R-t.Accent.R)+Math.Abs(paint.G-t.Accent.G)+Math.Abs(paint.B-t.Accent.B)<=40);
            bool on=Chosen||painted||TemplatedParent is DependencyObject p&&(bool)p.GetValue(IllustratedThemeUI.PrimaryProperty);
            var ink=on?t.OnAccent:TemplatedParent is Control c&&c.Foreground is SolidColorBrush f?f.Color:t.Text;
            if(Ink is not SolidColorBrush current||current.Color!=ink)Ink=IllustratedThemeUI.Brush(ink);
            NativeFaces.Control(dc,s,w,h,Active,Down,on,(double)GetValue(BlendProperty));return;
        }
        Color surface=ThemeCatalog.Mix(ThemeCatalog.Alpha(t.Deep,225),t.Accent,Chosen?.3:.13*(double)GetValue(BlendProperty));
        if(TemplatedParent is Control {Background:SolidColorBrush {Color.A:>0} bg}&&bg.Color!=Colors.Transparent)
            surface=ThemeCatalog.Mix(surface,ThemeCatalog.Recolor(bg.Color),.3);
        var edge=IllustratedThemeUI.Brush(hot?t.AccentHi:t.Rule);
        var pen=new Pen(edge,Active?2:s.Surface=="paper"?2.5:1);
        var box=new Rect(1,Down?2:1,Math.Max(0,w-3),Math.Max(0,h-3));
        Brush fill=IllustratedThemeUI.Brush(surface);
        if(s.Surface is "gel" or "chrome")fill=new LinearGradientBrush(ThemeCatalog.Mix(surface,t.Text,s.Surface=="gel" ? .16 : .05),surface,90);
        Geometry Shape(params Point[] points){var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(points[0],true,true);c.PolyLineTo(points.Skip(1).ToArray(),true,false);}g.Freeze();return g;}
        if(s.Surface is "neon" or "iron" or "paper")
        {
            double cut=s.Surface=="iron"?Math.Min(7,h/5):Math.Min(10,h/4);
            var shape=s.Surface=="paper"?Shape(new(2,4),new(w*.26,2),new(w*.29,4),new(w*.75,2),new(w-3,4),new(w-1,h*.6),new(w-4,h-3),new(w*.7,h-1),new(w*.35,h-4),new(2,h-2)):
                Shape(new(cut,1),new(w-2,1),new(w-2,h-cut),new(w-cut,h-2),new(1,h-2),new(1,cut));
            if(s.Surface=="paper"){dc.PushTransform(new TranslateTransform(2,2));dc.DrawGeometry(IllustratedThemeUI.Brush(t.Accent),null,shape);dc.Pop();}
            dc.DrawGeometry(fill,pen,shape);
            if(s.Surface=="neon")dc.DrawLine(new Pen(IllustratedThemeUI.Brush(t.Accent),1),new(10,h-5),new(w-12,h-5));
            if(s.Surface=="iron")dc.DrawEllipse(IllustratedThemeUI.Brush(hot?t.Accent:t.Muted),null,new(w-7,h/2),1.6,1.6);
        }
        else dc.DrawRoundedRectangle(fill,pen,box,s.Radius,s.Radius);
        if(s.Surface=="gel")dc.DrawLine(new Pen(IllustratedThemeUI.Brush(ThemeCatalog.Alpha(t.Text,90)),1),new(16,5),new(Math.Max(16,w-16),5));
        if(s.Surface=="chrome")dc.DrawLine(new Pen(IllustratedThemeUI.Brush(t.AccentHi),1),new(14,h-5),new(Math.Max(14,w-14),h-5));
        if(s.Surface=="ink")
        {dc.DrawLine(new Pen(IllustratedThemeUI.Brush(t.Text),.6),new(4,h-5),new(Math.Max(4,w-9),h-5));dc.DrawRectangle(IllustratedThemeUI.Brush(t.Accent),null,new Rect(Math.Max(2,w-9),3,4,4));}
        if(s.Surface=="glass"&&hot)dc.DrawLine(new Pen(IllustratedThemeUI.Brush(t.Accent),2),new(8,h-3),new(Math.Max(8,w-8),h-3));
    }
}
