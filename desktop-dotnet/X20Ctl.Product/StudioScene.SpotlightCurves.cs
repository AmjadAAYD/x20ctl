using System;
using System.Linq;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using X20Ctl.Zone;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private static readonly ControlTemplate SpotlightFaderTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="{x:Type Slider}">
          <Grid Width="42">
            <Border Width="6" Background="#050302" BorderBrush="#6D552D" BorderThickness="1" CornerRadius="3"/>
            <Track x:Name="PART_Track" Orientation="Vertical" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" IsDirectionReversed="False">
              <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
              <Track.Thumb><Thumb Width="42" Height="14"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border BorderBrush="#F6E2B6" BorderThickness="1" CornerRadius="1"><Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="0,1"><GradientStop Color="#F6E2B6" Offset="0"/><GradientStop Color="#B99658" Offset="1"/></LinearGradientBrush></Border.Background><Border Margin="5,6" Height="1" Background="#6D552D"/></Border></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
              <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
        """);

    private bool spotlightCurvesWired;
    private Action? spotlightCurveRefresh;
    private void SpotlightShowCurves()
    {
        spotlightPageHost!.Children.Clear();
        if (!spotlightCurvesWired) { spotlightCurvesWired = true; Curves.Changed += () => spotlightCurveRefresh?.Invoke(); Curves.ChannelSelected += _ => SpotlightLater(); }
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Tag: Local draft
        var draftTag = SpotlightKit.Tag("Local draft");
        Canvas.SetLeft(draftTag, 302);
        Canvas.SetTop(draftTag, 144);
        canvas.Children.Add(draftTag);

        var noteText = new TextBlock
        {
            Text = "Not written to controller memory.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e")
        };
        Canvas.SetLeft(noteText, 410);
        Canvas.SetTop(noteText, 146);
        canvas.Children.Add(noteText);

        // Tabs: Left stick · Right stick · Triggers
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var channel in Enum.GetValues<CurveChannel>())
        {
            var face = new Border { Padding = new Thickness(10,6,10,6), Margin = new Thickness(0,0,14,0), BorderBrush = SpotlightKit.GoldBrush, BorderThickness = channel == Curves.Channel ? new Thickness(0,0,0,1) : new Thickness(0), Child = new TextBlock { Text = CurveWorkbench.Name(channel), FontFamily = SpotlightKit.Body, FontSize = 17, Foreground = channel == Curves.Channel ? SpotlightKit.GoldBrush : SpotlightKit.MuteBrush } };
            tabs.Children.Add(ThemeAction(face,"theme:curve:channel:"+channel,()=>Curves.SelectChannel(channel)));
        }
        Canvas.SetLeft(tabs,302); Canvas.SetTop(tabs,196); canvas.Children.Add(tabs);

        // Lighting Desk at (292, 258, w = 620, h = 470)
        var deskBg = new Border
        {
            Width = 620, Height = 470,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#1b120e"), 0.0), new(SpotlightKit.C("#120b09"), 1.0) }
            },
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.25)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 50, ShadowDepth = 30, Opacity = 0.6 }
        };
        Canvas.SetLeft(deskBg, 292);
        Canvas.SetTop(deskBg, 258);
        canvas.Children.Add(deskBg);

        // Desk Header bar
        var deskHeader = new Border
        {
            Width = 620, Height = 44,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#2a1c15"), 0.0), new(SpotlightKit.C("#1b120e"), 1.0) }
            },
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.30)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 0, 0, 0)
        };
        deskHeader.Child = new TextBlock
        {
            Text = CurveWorkbench.Name(Curves.Channel).ToUpperInvariant(),
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(deskHeader, 292);
        Canvas.SetTop(deskHeader, 258);
        canvas.Children.Add(deskHeader);

        int fieldIndex = 0;
        foreach (var (name, source) in RebelCurveRows)
        {
            double x = 360 + fieldIndex++ * 148;
            var slider = ThemeSlider(source,"theme:curve:"+name,42,Curves.Draft.CanEdit);
            slider.Style = null; slider.Orientation = Orientation.Vertical; slider.Height = 290;
            slider.Foreground = SpotlightKit.GoldBrush; slider.Background = SpotlightKit.B("#050302");
            slider.Template = SpotlightFaderTemplate;
            Canvas.SetLeft(slider,x); Canvas.SetTop(slider,340); canvas.Children.Add(slider);
            var label = new TextBlock { Text = name, Width = 142, FontSize = 13, FontFamily = SpotlightKit.Body, Foreground = SpotlightKit.GoldBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap }; Canvas.SetLeft(label,x-50); Canvas.SetTop(label,640); canvas.Children.Add(label);
            var value = new TextBlock { Width = 142, Foreground = SpotlightKit.GoldBrush, TextAlignment = TextAlignment.Center };
            value.SetBinding(TextBlock.TextProperty,new Binding("Value") { Source = source,StringFormat="{0:0}%" }); Canvas.SetLeft(value,x-50); Canvas.SetTop(value,682); canvas.Children.Add(value);
        }

        // Cue card at (960, 258, w = 600, label = "Response")
        var plotCanvas = new Canvas { Width = 520, Height = 220, ClipToBounds = false };

        void RefreshGraph()
        {
            plotCanvas.Children.Clear();
        // Grid lines
        var axisPath = new Path
        {
            Data = Geometry.Parse("M0 0V220H520"),
            Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.35)),
            StrokeThickness = 1
        };
        plotCanvas.Children.Add(axisPath);

        for (int i = 1; i < 4; i++)
        {
            var gridLine = new Line
            {
                X1 = 520 * i / 4.0, Y1 = 0,
                X2 = 520 * i / 4.0, Y2 = 220,
                Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.08)),
                StrokeThickness = 1
            };
            plotCanvas.Children.Add(gridLine);
        }

        // Response curve path
        plotCanvas.Children.Add(new Polyline { Points = new PointCollection(Enumerable.Range(0,201).Select(i=>new Point(i/200.0*520,220*(1-Math.Clamp(Curves.Output(i/200.0),0,1))))), Stroke=SpotlightKit.GoldBrush, StrokeThickness=2.6 });

        // Current point indicator
        var curPoint = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = SpotlightKit.B("#fff6e6"),
            Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(curPoint, 286 - 6);
        Canvas.SetTop(curPoint, 220 * (1 - Math.Clamp(Curves.Output(.55),0,1)) - 6);
        plotCanvas.Children.Add(curPoint);

        // Labels
        var inText = new TextBlock { Text = "Input %", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush };
        Canvas.SetLeft(inText, 0); Canvas.SetTop(inText, 224); plotCanvas.Children.Add(inText);

        var outText = new TextBlock { Text = "Output % ↑", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush };
        Canvas.SetRight(outText, 0); Canvas.SetTop(outText, 224); plotCanvas.Children.Add(outText);

        }
        spotlightCurveRefresh = RefreshGraph; RefreshGraph();

        var plotInner = new StackPanel();
        plotInner.Children.Add(plotCanvas);

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 44, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var kbBtn = SpotlightKit.TheatricalButton("Keyboard test", false, onClick: StartTry, height: 42, fontSize: 12);
        actionsRow.Children.Add(kbBtn);

        var testNote = new TextBlock
        {
            Text = "Connect a controller or use Keyboard test",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        actionsRow.Children.Add(testNote);
        plotInner.Children.Add(actionsRow);
        var more = new WrapPanel { Margin = new Thickness(0,14,0,0) };
        foreach(var (label,tag,action) in new (string,string,Action)[] { ("Reset","reset",()=>Curves.Reset()), ("Preset","preset",()=>Curves.SetPreset(RebelPresets[(Array.IndexOf(RebelPresets,Curves.Draft[Curves.Channel].Preset)+1)%RebelPresets.Length])) })
        {
            var face = SpotlightKit.TheatricalButton(label,false,onClick:action,height:36,fontSize:12);
            face.Tag = "theme:curve:" + tag; face.IsEnabled = Curves.Draft.CanEdit; more.Children.Add(face);
        }
        plotInner.Children.Add(more);

        var cueCard = SpotlightKit.CueCard(plotInner, 600, label: "Response", padding: new Thickness(40, 40, 40, 34));
        Canvas.SetLeft(cueCard, 960);
        Canvas.SetTop(cueCard, 258);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "curves",
            title: "Curves",
            lead: "",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Adjust"), ("X", "Reset"), ("B", "Back") });

        spotlightPageHost.Children.Add(frame);
    }

    public void SpotlightShowAdvanced() => OpenStickTools();
}
