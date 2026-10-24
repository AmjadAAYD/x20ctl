using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product.Agent;

static class AgentNavigationChecks
{
    public static async Task Run()
    {
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(Application.Current.Dispatcher));
        var buttons = new List<Button>();
        var panel = new AgentPanel("x20_pro", "EasySMX X20 Pro", null, buttons.Add, reducedMotion: true);
        var transcript = (StackPanel)typeof(AgentPanel).GetField("transcript", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        void Click(string tip) => buttons.Last(b => Equals(b.ToolTip, tip)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var leds = (Border[])typeof(AgentPanel).GetField("leds", privateFlags)!.GetValue(panel)!;
        void Colour(byte r, byte g, byte b) => Check(leds.All(l => l.Opacity == 1 && ((SolidColorBrush)l.Background).Color == Color.FromRgb(r, g, b)) &&
            (Color)typeof(AgentPanel).GetField("eyeColor", privateFlags)!.GetValue(panel)! == Color.FromRgb(r, g, b), "LED/eye colours disagree with requested state");
        Colour(61, 214, 140);
        typeof(AgentPanel).GetMethod("Searching", privateFlags)!.Invoke(panel, new object[] { true }); Colour(168, 110, 255);
        typeof(AgentPanel).GetMethod("Lights", privateFlags)!.Invoke(panel, new object[] { X20Ctl.Zone.Agent.AgentMood.Good }); Colour(70, 150, 255);
        typeof(AgentPanel).GetMethod("Lights", privateFlags)!.Invoke(panel, new object[] { X20Ctl.Zone.Agent.AgentMood.Unsure }); Colour(AgentMark.Red.R, AgentMark.Red.G, AgentMark.Red.B);
        typeof(AgentPanel).GetMethod("Steady", privateFlags)!.Invoke(panel, null); Colour(61, 214, 140);
        Check(buttons.First(b => Equals(b.ToolTip, "LB · Undo the last change")).Content is System.Windows.Shapes.Path, "Left shoulder still uses a rectangular outline");
        Check(buttons.First(b => Equals(b.ToolTip, "RB · Put the agent away")).Content is System.Windows.Shapes.Path, "Right shoulder still uses a rectangular outline");
        int initial = transcript.Children.Count;
        foreach (var topic in new[] { "triggers", "buttons", "aim & sticks", "triggers" }) Click("Ideas for " + topic);
        Click("Conversation"); await Task.Delay(1400);
        Check(transcript.Children.Count == initial, "Topic browsing adds unsolicited chat messages");
        Colour(61, 214, 140);
        panel.Search.Text = "sticks"; await Task.Delay(1400);
        Check(transcript.Children.Count == initial, "Search adds unsolicited chat messages");
        Check(!buttons.Any(b => Equals(b.ToolTip, "PC mode")), "PC mode rail shortcut remains");
        Check(!buttons.Any(b => Equals(b.ToolTip, "Agent settings")), "The gear that duplicated the settings button remains");
        int beforeUndo = transcript.Children.Count; Click("LB · Undo the last change");
        Check(transcript.Children.Count >= beforeUndo + 2, "LB does not ask for and answer an undo in the chat");
        bool closed = false; panel.CloseRequested += () => closed = true; Click("RB · Put the agent away");
        Check(closed, "RB does not put the agent away");
        var pages = new List<string>(); var drafts = new List<string>();
        panel.PageRequested += pages.Add;
        Click("Controller check-up"); int before = transcript.Children.Count;
        Check(transcript.Children.Count == before, "Opening the check-up adds chat messages");
        Click("A stick drifts"); Check(pages.Count == 0, "A symptom chip navigates before choosing an action");
        Click("Read scan"); Check(pages.SequenceEqual(new[] { "ReadScan" }), "Check-up symptom does not lead to the Read scan"); pages.Clear(); Click("No, keep chatting");
        var feedback = typeof(AgentPanel).GetEvent("FeedbackRequested");
        Check(feedback != null, "Missing feedback handoff");
        feedback!.AddEventHandler(panel, (Action<string>)drafts.Add);
        Click("Controller check-up"); Click("Describe it yourself");
        await panel.Ask("My right trigger does not work");
        Check(pages.Count == 0 && drafts.Count == 0, "Diagnostic navigates before choosing an action");
        Click("Read scan"); Check(pages.SequenceEqual(new[] { "ReadScan" }), "Read scan route missing");
        panel.Show(); Click("No, keep chatting"); Check(drafts.Count == 0, "Declining feedback creates a draft");
        Click("Controller check-up"); Click("Describe it yourself"); await panel.Ask("Bluetooth settings are missing");
        Click("Write scan"); Check(pages.Last() == "WriteScan", "Write scan route missing");
        Click("Yes, open Feedback"); Check(drafts.Count == 1 && drafts[0].Contains("Bluetooth settings are missing"), "Feedback loses the issue");
        Click("Yes, open Feedback"); Check(drafts.Count == 1, "Feedback confirmation can be submitted twice");
        Click("New conversation"); Click("Controller check-up"); Click("Describe it yourself"); await panel.Ask("My right trigger does not work over Bluetooth");
        panel.ScrollTo(0);
        panel.Body.Width = 1740; panel.Body.Height = 800; panel.Show();
        panel.Body.Measure(new Size(1740, 800)); panel.Body.Arrange(new Rect(0, 0, 1740, 800)); panel.Body.UpdateLayout();
        var output = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts", "agent-navigation-fix"); System.IO.Directory.CreateDirectory(output);
        var bitmap = new RenderTargetBitmap(1740, 800, 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel.Body);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "diagnostic.png"))) png.Save(file);
        foreach (var tip in new[] { "LB · Undo the last change", "RB · Put the agent away" })
            ((System.Windows.Shapes.Path)buttons.First(b => Equals(b.ToolTip, tip)).Content).Stroke = new SolidColorBrush(Color.FromRgb(169, 207, 255));
        typeof(AgentPanel).GetMethod("Steady", privateFlags)!.Invoke(panel, null);
        panel.Body.UpdateLayout();
        bitmap = new RenderTargetBitmap(1740, 800, 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel.Body);
        png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "neutral-shoulder-outlines.png"))) png.Save(file);
        // Re-render the live vector mark at 6x, rather than enlarging the previous bitmap screenshot.
        var detail = new DrawingVisual();
        using (var drawing = detail.RenderOpen())
            drawing.DrawRectangle(new VisualBrush(panel.Body) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(185, 285, 115, 100), Stretch = Stretch.Fill }, null, new Rect(0, 0, 690, 600));
        bitmap = new RenderTargetBitmap(690, 600, 96, 96, PixelFormats.Pbgra32); bitmap.Render(detail);
        png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "logo-detail-6x.png"))) png.Save(file);
        var moving = new AgentPanel("x20_pro", "EasySMX X20 Pro", null, _ => { });
        typeof(AgentPanel).GetMethod("DragEyes", privateFlags)!.Invoke(moving, new object[] { 30.0, -20.0 });
        var wobble = (List<TranslateTransform>)typeof(AgentPanel).GetField("eyeWobbles", privateFlags)!.GetValue(moving)!;
        Check(wobble.Count == 2 && wobble.All(w => w.X == -13 && w.Y == 10), "Drag does not move both eyes opposite to motion within their bounds");
        typeof(AgentPanel).GetMethod("ReleaseEyes", privateFlags)!.Invoke(moving, null);
        Check(wobble.All(w => w.HasAnimatedProperties), "Eyes do not spring back on release"); moving.Hide();
        panel.Hide();
        Console.WriteLine("PASS: silent topic/search navigation, hidden PC shortcut, diagnostic scan routes, declined/confirmed feedback, single handoff, four LED/eye states, bounded drag wobble and spring return, shoulder contours, rendered diagnostic.");
    }
}
