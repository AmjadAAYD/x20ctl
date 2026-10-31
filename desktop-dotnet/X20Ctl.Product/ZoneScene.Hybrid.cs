using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class ZoneScene
{
    internal bool IsHybrid { get; private set; }
    private Grid? hybridBoard;
    private Canvas? hybridBackground;
    private TextBlock? hybridCaption;
    private Button? hybridAdd;
    private int hybridSelected;
    private static Brush HybridColor(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    private static void HybridPlace(FrameworkElement e, double x, double y) { e.HorizontalAlignment = HorizontalAlignment.Left; e.VerticalAlignment = VerticalAlignment.Top; e.Margin = new(x, y, 0, 0); }
    private void BuildHybrid()
    {
        IsHybrid = true; var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed; OuterFrame.Background = HybridColor("#1b1722"); OuterFrame.BorderBrush = Brushes.Transparent;
        hybridBoard = new Grid { Width = 1600, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true };
        hybridBackground = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false }; hybridBoard.Children.Add(hybridBackground);
        Workspace.Children.Remove(DockStage); DockStage.ClearValue(Grid.RowProperty); DockStage.Margin = new(0); DockStage.HorizontalAlignment = HorizontalAlignment.Left; DockStage.VerticalAlignment = VerticalAlignment.Top; hybridBoard.Children.Add(DockStage);
        Workspace.Children.Remove(FeedbackText); hybridBoard.Children.Add(FeedbackText);
        hybridCaption = new TextBlock { Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, Background = HybridColor("#1b1622"), Padding = new(18, 9, 18, 9) }; HybridPlace(hybridCaption, 142, 270); hybridBoard.Children.Add(hybridCaption);
        hybridAdd = new Button { Content = "+\nAdd controller", Width = 236, Height = 330, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = HybridColor("#e6e1ee"), Background = HybridColor("#211e27"), BorderBrush = HybridColor("#77717f"), BorderThickness = new(2) };
        hybridAdd.Click += (_, _) => { int slot = Array.FindIndex(State.Players.ToArray(), p => p.Model == null); if (slot >= 0) ControllerActionRequested?.Invoke(slot, false); };
        HybridPlace(hybridAdd, 1222, 360); hybridBoard.Children.Add(hybridAdd);
        var title = new TextBlock { Text = "Controller Zone", FontSize = 46, FontWeight = FontWeights.ExtraBold, Foreground = HybridColor("#1b1622"), IsHitTestVisible = false, Name = "HybridZoneTitle" }; HybridPlace(title, 150, 120); hybridBoard.Children.Add(title);
        original.Children.Remove(HeaderBar); var tile = BrandTile; ((Panel)tile.Parent).Children.Remove(tile); tile.Width = 90; tile.Height = 64; BrandMark.Width = 90; BrandMark.Height = 64;
        string logo = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png"); if (System.IO.File.Exists(logo)) BrandMark.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(logo));
        HybridPlace(tile, 44, 26); hybridBoard.Children.Add(tile);
        var strip = HeaderActions; ((Panel)strip.Parent).Children.Remove(strip); Grid.SetColumn(strip, 0); strip.HorizontalAlignment = HorizontalAlignment.Right; strip.VerticalAlignment = VerticalAlignment.Top; strip.Margin = new(0, 12, 14, 0); hybridBoard.Children.Add(strip);
        var notice = UpdateNotice; ((Panel)notice.Parent).Children.Remove(notice); Grid.SetColumn(notice, 0); notice.HorizontalAlignment = HorizontalAlignment.Right; notice.VerticalAlignment = VerticalAlignment.Top; notice.Margin = new(0, 66, 30, 0); hybridBoard.Children.Add(notice);
        original.Visibility = Visibility.Collapsed; Surface.Children.Add(hybridBoard);
        foreach (var dock in docks) HookHybridDock(dock);
        HybridSync();
    }
    private void HookHybridDock(DockView dock)
    {
        dock.FocusButton.GotKeyboardFocus += (_, _) => { hybridSelected = dock.Index; HybridSync(); };
        dock.BackRequested += () => _ = FocusPlayerAsync(null);
        dock.PageRequested += page => StudioPageRequested?.Invoke(dock.Index, page);
    }
    private void HybridSync()
    {
        if (!IsHybrid || hybridBoard == null || hybridBackground == null) return;
        bool hero = State.FocusedPlayer.HasValue; int selected = State.FocusedPlayer ?? Math.Clamp(hybridSelected, 0, 3);
        hybridBackground.Children.Clear(); double split = hero ? 700 : 800;
        hybridBackground.Children.Add(new Rectangle { Width = split, Height = 900, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#e7dbf7"), (Color)ColorConverter.ConvertFromString("#b89fd1"), 90) });
        var right = new Rectangle { Width = 1600 - split, Height = 900, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#38333e"), (Color)ColorConverter.ConvertFromString("#19171e"), 90) }; Canvas.SetLeft(right, split); hybridBackground.Children.Add(right);
        var seam = new Rectangle { Width = 5, Height = 900, Fill = HybridColor("#52dcff"), Opacity = .72 }; Canvas.SetLeft(seam, split - 2); hybridBackground.Children.Add(seam);
        if (!hero) { var table = new Rectangle { Width = 1460, Height = 190, Fill = new LinearGradientBrush(Color.FromArgb(88,255,255,255), Colors.Transparent, 90) }; Canvas.SetLeft(table, 70); Canvas.SetTop(table, 676); hybridBackground.Children.Add(table); }
        hybridBoard.Children.OfType<TextBlock>().First(t => t.Name == "HybridZoneTitle").Visibility = hero ? Visibility.Collapsed : Visibility.Visible;
        hybridCaption!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible;
        var player = State.Players[selected]; hybridCaption.Text = player.Model == null ? $"Player {selected + 1} · Empty" : $"Player {selected + 1} · {player.Model} · {docks[selected].FinishName}";
        hybridAdd!.Visibility = !hero && State.Players.Any(p => p.Model == null) ? Visibility.Visible : Visibility.Collapsed;
        HybridPlace(DockStage, hero ? 0 : 142, hero ? 0 : 360); DockStage.Width = hero ? 1600 : 1046; DockStage.Height = hero ? 900 : 330;
        for (int i = 0; i < docks.Length; i++) { docks[i].Visibility = !hero || i == selected ? Visibility.Visible : Visibility.Collapsed; docks[i].SetHybridOn(i == selected); }
        DockStage.UpdateLayout(); if (DockStage.ActualWidth > 48) SnapLayout();
    }
    private DockBounds[] HybridBounds() => Enumerable.Range(0, 4).Select(i => State.FocusedPlayer == i ? new DockBounds(0, 0, 1600, 900) : new DockBounds(i * 270, 0, 236, 330)).ToArray();
    private Task HybridFocus(int? index)
    {
        transition?.Cancel(); if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        FeedbackText.Text = State.Feedback; UpdateOverviewPill(); if (index.HasValue) hybridSelected = index.Value; HybridSync();
        if (index.HasValue) docks[index.Value].FocusHybridAction(); else docks[hybridSelected].FocusButton.Focus(); return Task.CompletedTask;
    }
    private void HybridHandoff(double reveal, double brand, double controller, double pulse)
    {
        double amount = Math.Clamp(reveal, 0, 1);
        foreach (UIElement element in new UIElement?[] { hybridBackground, hybridCaption, hybridAdd, DockStage, HeaderActions, UpdateNotice, hybridBoard?.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "HybridZoneTitle") }.OfType<UIElement>()) element.Opacity = amount;
        BrandTile.Opacity = 1; BrandMark.Opacity = brand; BrandMark.RenderTransformOrigin = new(.5, .5); BrandMark.RenderTransform = brandPulse;
        brandPulse.ScaleX = brandPulse.ScaleY = pulse;
        if (docks.Length > 0) docks[0].Art.Opacity = controller;
    }
}
