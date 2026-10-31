using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product;

// Hybrid's player cards keep the existing controller artwork and actions; only their presentation changes.
public partial class DockView
{
    private bool hybrid, hybridOn;
    private Canvas? hybridFace, hybridActions;
    private static Brush HybridBrush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock HybridText(string text, double size, string color, FontWeight weight) => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = HybridBrush(color), FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
    private static void HybridAt(Canvas canvas, UIElement child, double x, double y) { Canvas.SetLeft(child, x); Canvas.SetTop(child, y); canvas.Children.Add(child); }
    private void InitHybrid()
    {
        if (ThemeCatalog.Current.Id != "hybrid") return;
        hybrid = true; var root = (Grid)Content; root.ClipToBounds = false; Card.Visibility = Visibility.Collapsed;
        hybridFace = new Canvas { IsHitTestVisible = false }; root.Children.Insert(0, hybridFace);
        hybridActions = new Canvas { Visibility = Visibility.Collapsed }; root.Children.Add(hybridActions);
    }
    internal void SetHybridOn(bool on) { if (hybridOn == on) return; hybridOn = on; if (hybrid && !double.IsNaN(Width)) PresentHybrid(isHero); }
    internal bool HybridActionFocused => hybridActions?.IsKeyboardFocusWithin == true;
    internal void FocusHybridAction() => hybridActions?.Children.OfType<Button>().FirstOrDefault()?.Focus();
    private void PresentHybrid(bool hero)
    {
        if (hybridFace == null || hybridActions == null) return;
        bool empty = Player.Model == null; double w = Width, h = Height;
        foreach (UIElement e in new UIElement[] { PlayerPill, Details, Halo, ArtShadow, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge }) e.Visibility = Visibility.Collapsed;
        hybridFace.Children.Clear(); hybridActions.Children.Clear(); hybridActions.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        FocusButton.IsHitTestVisible = !hero;
        if (!hero)
        {
            var face = new Border { Width = w, Height = h, CornerRadius = new(26), Background = HybridBrush(empty ? "#211e27" : "#f7f1ff"), BorderBrush = HybridBrush(hybridOn ? "#52dcff" : empty ? "#716b7b" : "#e6dcef"), BorderThickness = new(hybridOn ? 5 : 2) };
            HybridAt(hybridFace, face, 0, 0);
            var player = HybridText($"●  Player {Player.Number}", 15, empty ? "#e6e1ee" : "#1b1622", FontWeights.Bold); HybridAt(hybridFace, player, 18, 18);
            HybridAt(hybridFace, HybridText(empty ? "Empty" : (Player.Model ?? "").Replace("EasySMX ", ""), 19, empty ? "#c9c2d4" : "#1b1622", FontWeights.Bold), 18, h - 62);
            if (!empty) HybridAt(hybridFace, HybridText(FinishName.Length == 0 ? "EasySMX" : FinishName, 14, "#6d5786", FontWeights.SemiBold), 18, h - 37);
            GlideArt(16, 84, Math.Max(20, w - 32));
        }
        else
        {
            HybridAt(hybridFace, HybridText($"‹   Controller Zone / Player {Player.Number}", 17, "#5c4075", FontWeights.SemiBold), 64, 44);
            HybridAt(hybridFace, HybridText($"PLAYER {Player.Number}", 14, "#5c4075", FontWeights.Bold), 64, 128);
            HybridAt(hybridFace, HybridText(empty ? "Empty" : (Player.Model ?? "").Replace("EasySMX ", ""), 96, "#1b1622", FontWeights.Black), 64, 160);
            HybridAt(hybridFace, HybridText(empty ? "Choose a controller for this player" : Player.Model ?? "", 23, "#1b1622", FontWeights.Bold), 64, 300);
            HybridAt(hybridFace, HybridText(empty ? "" : (Player.IsMockConnected ? "Connected · mock" : "Not detected") + " · Battery not reported", 16, "#5c4075", FontWeights.Normal), 64, 341);
            GlideArt(560, 190, 940);
            void Action(string label, double y, Action action, bool primary = false)
            {
                var b = new Button { Content = label, Tag = "hybrid:action:" + label, Width = 430, Height = 41, MinHeight = 0, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(20, 0, 0, 0), Background = HybridBrush(primary ? "#1b1622" : "#d1bce8"), Foreground = HybridBrush(primary ? "#f6edff" : "#1b1622"), BorderThickness = new(0), FontSize = 17, FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand };
                b.Click += (_, _) => action(); HybridAt(hybridActions, b, 64, y);
            }
            Action(empty ? "Choose controller" : "Open Studio", 494, () => ActionRequested?.Invoke(Index, false), true);
            if (!empty)
            {
                Action("Controller settings", 540, () => PageRequested?.Invoke("Device"));
                Action("Test controller", 586, () => PageRequested?.Invoke("Tester"));
                Action("Manage setups", 632, () => PageRequested?.Invoke("Setups"));
                Action("Change controller", 678, () => ActionRequested?.Invoke(Index, true));
                if (Player.ModelId is string model && PadColours.Catalog.For(model).Count > 1) Action("Change color", 724, async () => { await ChangeColour(); PresentHybrid(true); });
                Action("Remove from slot", 770, () => RemoveRequested?.Invoke(Index));
            }
            Action("Back", 816, () => BackRequested?.Invoke());
        }
    }
}
