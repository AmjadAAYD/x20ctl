using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private TesterWorkbench? hybridTesterSource;
    private TextBlock? hybridTesterStatus, hybridTesterPressed, hybridTesterValues;
    private Button? hybridTesterKeyboard;
    private void HybridTesterPage()
    {
        var tester = Tester; if (hybridTester == null || tester == null) return;
        if (ReferenceEquals(hybridTesterSource, tester) && hybridTester.Children.Count > 0) { HybridTesterRefresh(); return; }
        hybridTesterSource = tester; tester.Updated += HybridTesterRefresh; hybridTester.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridTester.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0); HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0); HAt(board, new Rectangle { Width = 3, Height = 480, Fill = HBrush("#52dcff") }, 846, 108);
        HAt(board, HText("Tester", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14); HAt(board, HText("Gameplay tester · UI navigation is paused while input is captured.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "device", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        var stage = tester.TakeStage(); var art = new Viewbox { Width = 560, Height = 380, Stretch = Stretch.Uniform, Child = stage, IsHitTestVisible = false }; HAt(board, art, 230, 108);
        var front = HybridButton("Front", "hybrid:tester:front", false, 100, 40); front.Click += (_, _) => tester.ShowRear(false); HAt(board, front, 365, 508);
        var back = HybridButton("Back", "hybrid:tester:back", false, 100, 40); back.Click += (_, _) => tester.ShowRear(true); HAt(board, back, 475, 508);
        var panel = new Border { Width = 492, Height = 430, Background = HBrush("#29242f"), CornerRadius = new(24) }; HAt(board, panel, 892, 108);
        HAt(board, HText("GAMEPLAY SOURCE", 13, "#bdb4c8", FontWeights.Bold), 920, 130);
        hybridTesterStatus = HText("", 26, "#f5f1fa", FontWeights.Bold); hybridTesterStatus.Width = 430; hybridTesterStatus.TextWrapping = TextWrapping.Wrap; HAt(board, hybridTesterStatus, 920, 160);
        HAt(board, HText(tester.OwnershipText, 15, "#c8bdd1", FontWeights.Normal), 920, 228);
        hybridTesterPressed = HText("", 15, "#c8bdd1", FontWeights.Normal); hybridTesterPressed.Width = 430; hybridTesterPressed.TextWrapping = TextWrapping.Wrap; HAt(board, hybridTesterPressed, 920, 266);
        var exit = HybridButton("Exit Tester", "hybrid:tester:exit", true, 160, 44); exit.Click += (_, _) => tester.ClickExit(); HAt(board, exit, 920, 350);
        hybridTesterKeyboard = HybridButton("Keyboard Test", "hybrid:tester:keyboard", false, 210, 44); hybridTesterKeyboard.Click += (_, _) => tester.ClickKeyboard(); HAt(board, hybridTesterKeyboard, 1092, 350);
        HAt(board, HText("WASD: left stick · arrows: right stick · Q/E: triggers", 13, "#bdb4c8", FontWeights.Normal), 920, 416);
        hybridTesterValues = HText("", 17, "#f5f1fa", FontWeights.SemiBold); hybridTesterValues.Width = 1168; hybridTesterValues.TextWrapping = TextWrapping.Wrap; HAt(board, hybridTesterValues, 216, 624);
        HybridTesterRefresh();
    }
    private void HybridTesterRefresh()
    {
        if (Tester is not { } t || hybridTesterStatus == null || hybridTesterPressed == null || hybridTesterValues == null) return;
        hybridTesterStatus.Text = t.SourceText; hybridTesterPressed.Text = t.PressedText;
        static string F(double? n) => n.HasValue ? n.Value.ToString("0.00") : "—";
        hybridTesterValues.Text = $"Left stick  {F(t.LeftX)}, {F(t.LeftY)}       Right stick  {F(t.RightX)}, {F(t.RightY)}       LT  {F(t.LeftTrigger)}       RT  {F(t.RightTrigger)}";
        if (hybridTesterKeyboard != null) hybridTesterKeyboard.Content = t.KeyboardText;
    }
}
