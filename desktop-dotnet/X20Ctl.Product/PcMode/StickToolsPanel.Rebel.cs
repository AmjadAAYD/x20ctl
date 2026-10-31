using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    internal Action<string>? RebelNavigate { get; set; }
    private Canvas? rebelAdvancedFields, rebelAdvancedGraph;
    private Button? rebelAdvancedReset;
    private StackPanel? rebelAdvancedDetail;
    private Slider? rebelDetailSlider;
    private ContentControl? rebelDetailValue;
    private readonly HashSet<Slider> rebelAdvancedWired = [];
    private readonly List<(Grid Face, string Name)> rebelAdvancedRows = [];

    private void BuildRebelAdvanced(Action close, Button reset)
    {
        var board = RebelToolFrame.Create("RebelAdvanced", "Advanced stick tools", "PC-side processing for accessible analog sticks", close, key => RebelNavigate?.Invoke(key), 50, "curves");
        Child = new Viewbox { Child = board, Stretch = Stretch.Uniform }; MaxWidth = double.PositiveInfinity; Padding = new(0); Margin = new(0); BorderThickness = new(0); Background = Ink.B(Ink.Black); Width = 1600; Height = 900;
        rebelAdvancedFields = new Canvas { Width = 1600, Height = 900 }; board.Children.Add(rebelAdvancedFields);
        rebelAdvancedGraph = new Canvas { Width = 250, Height = 160, ClipToBounds = true };
        rebelAdvancedReset = reset;
        status.Width = 650; status.FontSize = 13; status.Foreground = Ink.B(Ink.Dim); status.Margin = new(0); RebelToolFrame.At(board, status, 286, 846);
        RefreshRebelAdvanced();
    }

    private void RefreshRebelAdvanced()
    {
        if (rebelAdvancedFields == null) return;
        var board = rebelAdvancedFields; board.Children.Clear(); rebelAdvancedRows.Clear(); var row = rows[selectedStick];
        var paper = new Canvas { Width = 616, Height = 630, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1.2) };
        paper.Children.Add(new TornPaper { Width = 616, Height = 630, Seed = 11 }); RebelToolFrame.At(board, paper, 930, 156);
        for (int i = 0; i < Sticks.Length; i++)
        {
            var (name, channel) = Sticks[i]; var button = RebelPaperChip(name, channel == selectedStick);
            button.Click += (_, _) => SelectStick(channel); RebelToolFrame.At(paper, button, 38 + i * 100, 34);
        }
        RebelToolFrame.At(paper, rebelAdvancedGraph!, 38, 84);
        RebelToolFrame.At(paper, Ink.Text("Input %\nOutput % ↑", 13, Ink.B(Ink.Grey), false), 312, 218);
        rebelAdvancedDetail = new StackPanel { Width = 540 }; RebelToolFrame.At(paper, rebelAdvancedDetail, 38, 276);
        void AddRow(string name, FrameworkElement control, double y, int index)
        {
            RebelToolFrame.Detach(control); control.Margin = new(0);
            System.Windows.Automation.AutomationProperties.SetName(control, name);
            var face = new Grid { Width = 600, Height = 42, Tag = "advanced:" + name };
            var style = new Style(typeof(Grid)); style.Setters.Add(new Setter(Panel.BackgroundProperty, Ink.B(Ink.Panel))); style.Setters.Add(new Setter(TextElement.ForegroundProperty, Ink.B(Ink.White)));
            var focus = new Trigger { Property = IsKeyboardFocusWithinProperty, Value = true }; focus.Setters.Add(new Setter(Panel.BackgroundProperty, Ink.B(Ink.White))); focus.Setters.Add(new Setter(TextElement.ForegroundProperty, Ink.B(Ink.Black))); style.Triggers.Add(focus); face.Style = style;
            face.Clip = Slab.Poly(Slab.Jag(600, 42, 500 + index, 5, 0));
            var label = Ink.Text(name, 17); label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new(20, 0, 0, 0); label.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = face, Path = new PropertyPath(TextElement.ForegroundProperty) }); face.Children.Add(label);
            control.HorizontalAlignment = HorizontalAlignment.Left; control.VerticalAlignment = VerticalAlignment.Center;
            if (control is Slider slider)
            {
                slider.Style = RebelFineSliderStyle; slider.Width = 220; slider.Height = 28; slider.Margin = new(280, 0, 0, 0); slider.SetBinding(Control.ForegroundProperty, new Binding { Source = face, Path = new PropertyPath(TextElement.ForegroundProperty) });
                slider.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = row.Tuning });
                var value = Ink.Text("", 15, null, false); value.HorizontalAlignment = HorizontalAlignment.Right; value.VerticalAlignment = VerticalAlignment.Center; value.Margin = new(0, 0, 12, 0);
                value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = slider, StringFormat = "{0:0.0}%" }); value.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = face, Path = new PropertyPath(TextElement.ForegroundProperty) }); face.Children.Add(value);
                if (rebelAdvancedWired.Add(slider)) { slider.GotKeyboardFocus += (_, _) => ShowRebelAdvancedDetail(name, slider); slider.PreviewMouseDown += (_, _) => ShowRebelAdvancedDetail(name, slider); slider.ValueChanged += (_, _) => { if (rebelDetailSlider == slider) PaintRebelAdvancedValue(); }; }
            }
            else { control.Margin = new(520, 0, 0, 0); if (control is CheckBox toggle) toggle.Style = RebelAdvancedToggleStyle; if (control != row.Raw && control != linkSticks) control.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = row.Tuning }); }
            rebelAdvancedRows.Add((face, name)); face.Children.Add(control); RebelToolFrame.At(board, face, 262 + index * 2, y);
        }
        AddRow("Link stick settings", linkSticks, 168, 0); AddRow("Bypass X20CTL processing", row.Raw, 218, 1);
        RebelToolFrame.At(board, Ink.Label("Input range"), 272, 276);
        AddRow("Inner dead zone", row.Inner, 294, 2); AddRow("Full output at", row.Outer, 346, 3);
        RebelToolFrame.At(board, Ink.Label("Output range"), 276, 404);
        AddRow("Minimum output", row.Anti, 426, 4); AddRow("Maximum output", row.Max, 476, 5);
        AddRow("Gate: ○ Circular / □ Square", row.Square, 526, 6); AddRow("Vertical sensitivity", row.Ratio, 576, 7);
        var linear = new SlabButton("Reset range to 1:1", 600, 42, 520, 0, 17, Ink.White);
        linear.Click += (_, _) => { loading = true; row.Inner.Value = row.Anti.Value = 0; row.Outer.Value = row.Max.Value = 100; loading = false; Changed(); };
        RebelToolFrame.At(board, linear, 278, 626);
        void ActionRow(Button button, string caption, double y) { RebelToolFrame.Detach(button); button.Margin = new(0); button.Width = 600; button.Height = 42; var wrap = new StackPanel(); wrap.Children.Add(button); DressRebelAdvancedCommand(button, caption, 600, 42, false); RebelToolFrame.At(board, wrap, 280, y); }
        ActionRow(measure, "Measure drift", 676); ActionRow(rebelAdvancedReset!, "Reset selected advanced settings", 726);
        var actions = new WrapPanel { Width = 540 }; var copy = new SlabButton(selectedStick == CurveChannel.LeftStick ? "Copy to right stick" : "Copy to left stick", 260, 46, 521, 0, 17, Ink.White, true);
        copy.Click += (_, _) => CopyStick(selectedStick, selectedStick == CurveChannel.LeftStick ? CurveChannel.RightStick : CurveChannel.LeftStick);
        DressRebelAdvancedCommand(copy, copy.Caption, 230, 46, true); actions.Children.Add(copy); RebelToolFrame.Detach(Done); Done.Margin = new(16, 0, 0, 0); DressRebelAdvancedCommand(Done, "Done", 90, 46, false, true); actions.Children.Add(Done); RebelToolFrame.At(paper, actions, 38, 480);
        RebelToolFrame.Detach(saved); saved.Width = 530; saved.Foreground = Ink.B(Ink.Black); saved.Margin = new(0); RebelToolFrame.At(paper, saved, 38, 540);
        var extras = new WrapPanel { Width = 540 }; foreach (var command in new[] { undoCopy, applyDrift }) { RebelToolFrame.Detach(command); extras.Children.Add(command); } RebelChrome.Style(extras); RebelToolFrame.At(paper, extras, 38, 576);
        RebelToolFrame.Detach(notice); notice.Width = 550; notice.Margin = new(0); notice.Background = Ink.B(Ink.Panel); RebelToolFrame.At(board, notice, 958, 420);
        ShowRebelAdvancedDetail("Inner dead zone", row.Inner); RefreshRebelAdvancedGraph(selectedStick);
    }

    private void ShowRebelAdvancedDetail(string name, Slider slider)
    {
        if (rebelAdvancedDetail == null) return;
        foreach (var (face, caption) in rebelAdvancedRows) { face.Background = Ink.B(caption == name ? Ink.White : Ink.Panel); TextElement.SetForeground(face, Ink.B(caption == name ? Ink.Black : Ink.White)); }
        var detail = rebelAdvancedDetail; detail.Children.Clear();
        detail.Children.Add(Ink.Label(name == "Inner dead zone" ? "Dead zone" : "Selected setting", 12, Ink.B(Ink.Cut)));
        var heading = new WrapPanel { Margin = new(0, 8, 0, 0) }; heading.Children.Add(Ink.Text(name, 28, Ink.B(Ink.Black)));
        rebelDetailSlider = slider; rebelDetailValue = new ContentControl { Width = 172, Height = 64, Margin = new(20, 0, 0, 0) }; PaintRebelAdvancedValue(); heading.Children.Add(rebelDetailValue); detail.Children.Add(heading);
        var presets = new WrapPanel { Margin = new(0, 12, 0, 0) };
        double[] values = name switch { "Full output at" => [100, 80, 65], "Minimum output" => [0, 8, 15], "Maximum output" => [100, 80, 65], "Vertical sensitivity" => [100, 75, 125], _ => [0, 3, 5] };
        foreach (double number in values) { var button = RebelPaperChip(number + "%", Math.Abs(slider.Value - number) < .01); button.Click += (_, _) => slider.Value = number; presets.Children.Add(button); }
        detail.Children.Add(presets);
        var body = Ink.Text("Nothing is written to controller memory. Settings save locally as you edit; sliders adjust in 0.1% steps.", 16, Ink.B(Ink.Black), false); body.TextWrapping = TextWrapping.Wrap; body.Margin = new(0, 14, 0, 0); detail.Children.Add(body);
    }

    private void PaintRebelAdvancedValue()
    {
        if (rebelDetailValue != null && rebelDetailSlider != null) rebelDetailValue.Content = new Viewbox { Child = Ransom.Make(rebelDetailSlider.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%", 46, 2), StretchDirection = StretchDirection.DownOnly };
    }
    private static Button RebelPaperChip(string caption, bool selected)
    {
        var button = new Button { Height = 31, MinWidth = 44, Margin = new(0, 0, 8, 0), FocusVisualStyle = null };
        var border = new Border { Background = Ink.B(selected ? Ink.Red : Ink.Paper), BorderBrush = Ink.B(selected ? Ink.Red : Ink.Grey), BorderThickness = new(1.5), Padding = new(12, 3, 12, 3), RenderTransform = new SkewTransform(-12, 0), Child = Ink.Text(caption, 15, Ink.B(selected ? Ink.White : Ink.Black)) };
        button.Content = border; button.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        button.GotKeyboardFocus += (_, _) => border.BorderBrush = Ink.B(Ink.Black); button.LostKeyboardFocus += (_, _) => border.BorderBrush = Ink.B(selected ? Ink.Red : Ink.Grey);
        System.Windows.Automation.AutomationProperties.SetName(button, caption); return button;
    }
    private static void DressRebelAdvancedCommand(Button button, string caption, double width, double height, bool primary, bool paper = false)
    {
        if (button.Tag as string == "rebel:advanced-command") return;
        button.Tag = "rebel:advanced-command";
        button.Width = width; button.Height = height; button.Padding = new(0); button.MinHeight = 0;
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        var slab = new Slab { Seed = 521, Fill = Ink.B(primary ? Ink.Red : paper ? Ink.Paper : Ink.Panel), Line = paper ? Ink.B(Ink.Black) : null, LineWidth = 2 };
        var text = Ink.Text(caption, 17, Ink.B(paper ? Ink.Black : Ink.White)); text.Margin = new(18, 0, 12, 0); text.VerticalAlignment = VerticalAlignment.Center; slab.Children.Add(text); button.Content = slab;
        button.GotKeyboardFocus += (_, _) => { slab.Under = Ink.B(Ink.Red); slab.InvalidateVisual(); }; button.LostKeyboardFocus += (_, _) => { slab.Under = null; slab.InvalidateVisual(); };
    }

    private static readonly Style RebelAdvancedToggleStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="CheckBox"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><Border Width="58" Height="23" BorderBrush="#8f878a" BorderThickness="1" Background="#292528" RenderTransformOrigin=".5,.5"><Border.RenderTransform><SkewTransform AngleX="-14"/></Border.RenderTransform><Rectangle x:Name="Switch" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="17" Height="16" Margin="4,0" HorizontalAlignment="Left" Fill="#6c666a"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Switch" Property="HorizontalAlignment" Value="Right"/><Setter TargetName="Switch" Property="Fill" Value="#e3122d"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        """);

    private void RefreshRebelAdvancedGraph(CurveChannel channel)
    {
        if (rebelAdvancedGraph == null || channel != selectedStick || !charts.TryGetValue(channel, out var original)) return;
        var curve = original.Children.OfType<Polyline>().FirstOrDefault(); if (curve == null) return;
        var graph = rebelAdvancedGraph; graph.Children.Clear();
        for (int i = 1; i < 6; i++) graph.Children.Add(new Line { X1 = i * 250 / 6d, X2 = i * 250 / 6d, Y1 = 0, Y2 = 160, Stroke = Ink.B(Ink.A(Ink.Black, .08)), StrokeThickness = 1 });
        var points = new PointCollection(curve.Points.Select(point => new Point((point.X - 30) / 310 * 250, 160 - (138 - point.Y) / 105 * 160)));
        var fill = new Polygon { Points = new PointCollection(points.Append(new Point(250, 160)).Append(new Point(0, 160))), Fill = Dots.Tile(Ink.A(Ink.Red, .45), 7, 1.2) }; graph.Children.Add(fill);
        graph.ToolTip = "Response preview · sample at 55% input";
        graph.Children.Add(new Polyline { Points = points, Stroke = Ink.B(Ink.Red), StrokeThickness = 6, Effect = Ink.Shadow(4, 4, 0, Ink.Black, 1) });
        if (points.Count > 55) graph.Children.Add(Star.Make(points[55].X, points[55].Y, 14));
        graph.Children.Add(new Polyline { Points = new PointCollection([new(0, 0), new(0, 160), new(250, 160)]), Stroke = Ink.B(Ink.Black), StrokeThickness = 2 });
    }

    private static readonly Style RebelFineSliderStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Slider"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid Background="Transparent" Height="28"><Rectangle Height="3" VerticalAlignment="Center" Fill="{TemplateBinding Foreground}" Opacity=".22"/><Track x:Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}"><Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Path Data="M0,1 L1,0 L1,1 Z" Stretch="Fill" Fill="#E3122D" Height="12"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False" Opacity="0"/></Track.IncreaseRepeatButton><Track.Thumb><Thumb Width="28" Height="28"><Thumb.Template><ControlTemplate TargetType="Thumb"><Path Data="M14,0 L17,10 L28,14 L17,17 L14,28 L11,17 L0,14 L11,10 Z" Fill="#E3122D" Stroke="#F7F3EE" StrokeThickness="1.4"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
        """);
}
