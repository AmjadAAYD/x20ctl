using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Controller Zone: four player cards, the selected one grown into the hero spot on the left.</summary>
public sealed class MainWindow : Window
{
    static readonly IBrush Back = new SolidColorBrush(Color.Parse("#0E1420"));
    static readonly IBrush Card = new SolidColorBrush(Color.Parse("#17202F"));
    static readonly IBrush Line = new SolidColorBrush(Color.Parse("#26334A"));
    static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#3D8BFF"));
    static readonly IBrush Quiet = new SolidColorBrush(Color.Parse("#8A97AD"));

    readonly ControllerCatalog catalog;
    readonly ControllerModel?[] players = new ControllerModel?[4];
    readonly Grid zone = new();
    int selected;

    public MainWindow()
    {
        Title = "X20CTL"; Width = 1280; Height = 760; MinWidth = 960; MinHeight = 600; Background = Back;
        catalog = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
        players[0] = catalog.Find("x20_pro");
        var root = new DockPanel { Margin = new Thickness(28) };
        var header = new TextBlock { Text = "Controller Zone", FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header); root.Children.Add(zone);
        Content = root; Rebuild();
        KeyDown += (_, e) => { if (e.Key >= Key.D1 && e.Key <= Key.D4) { selected = e.Key - Key.D1; Rebuild(); } };
    }

    void Rebuild()
    {
        zone.Children.Clear(); zone.ColumnDefinitions.Clear(); zone.RowDefinitions.Clear();
        zone.ColumnDefinitions.Add(new(2.2, GridUnitType.Star)); zone.ColumnDefinitions.Add(new(1, GridUnitType.Star));
        for (int r = 0; r < 3; r++) zone.RowDefinitions.Add(new(1, GridUnitType.Star));
        var others = Enumerable.Range(0, 4).Where(i => i != selected).ToArray();
        Place(PlayerCard(selected, true), 0, 0, 3);
        for (int k = 0; k < 3; k++) Place(PlayerCard(others[k], false), 1, k, 1);
    }

    void Place(Control card, int col, int row, int span)
    {
        card.Margin = new Thickness(col == 0 ? 0 : 14, row == 0 ? 0 : 14, 0, 0);
        Grid.SetColumn(card, col); Grid.SetRow(card, row); Grid.SetRowSpan(card, span); zone.Children.Add(card);
    }

    Control PlayerCard(int index, bool hero)
    {
        var model = players[index];
        var body = new Grid();
        var tag = new TextBlock { Text = "PLAYER " + (index + 1), FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Quiet, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        body.Children.Add(tag);
        if (model != null)
        {
            var image = Art(model);
            if (image != null) body.Children.Add(new Image { Source = image, Margin = new Thickness(0, 22, 0, 36), Stretch = Stretch.Uniform });
            body.Children.Add(new TextBlock { Text = model.Name, FontSize = hero ? 20 : 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left });
        }
        else body.Children.Add(new TextBlock { Text = "+", FontSize = 44, Foreground = Quiet, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var border = new Border { Background = Card, CornerRadius = new CornerRadius(14), Padding = new Thickness(18), BorderThickness = new Thickness(hero ? 2 : 1), BorderBrush = hero ? Accent : Line, Child = body, Cursor = new Cursor(StandardCursorType.Hand) };
        border.PointerPressed += (_, _) => { selected = index; Rebuild(); };
        return border;
    }

    static Bitmap? Art(ControllerModel model)
    {
        try { return new Bitmap(AssetLoader.Open(new Uri($"avares://x20ctl-studio/Assets/{model.Id}/front-full.png"))); }
        catch (Exception) { return null; }
    }
}
