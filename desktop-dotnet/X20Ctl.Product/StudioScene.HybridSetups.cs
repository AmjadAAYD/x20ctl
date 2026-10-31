using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private SetupsWorkbench? hybridSetupsSource;
    private void HybridSetupsPage()
    {
        var w = Setups; if (hybridSetups == null || w == null) return;
        if (!ReferenceEquals(hybridSetupsSource, w)) { hybridSetupsSource = w; w.Watch(() => { if (ManagementPage == "Setups") HybridLater(); }); }
        string? focused = Keyboard.FocusedElement is FrameworkElement { Tag: string tag } element && hybridSetups.IsAncestorOf(element) ? tag : null;
        TextBox? rename = null; if (w.RenameActive) { rename = w.TakeNameField(); HDetach(rename); }
        hybridSetups.Children.Clear(); var board = new Canvas { Width = 1600, Height = 848, ClipToBounds = true, Background = HBrush("#211d28") }; hybridSetups.Children.Add(board);
        HAt(board, new Rectangle { Width = 800, Height = 848, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#e7dbf7"), (Color)ColorConverter.ConvertFromString("#b89fd1"), 90) }, 0, 0);
        HAt(board, new Rectangle { Width = 800, Height = 848, Fill = HBrush("#211e27") }, 800, 0); HAt(board, new Rectangle { Width = 5, Height = 848, Fill = HBrush("#52dcff") }, 798, 0);
        HAt(board, HText("My setups", 40, "#1b1622", FontWeights.ExtraBold), 160, 52);
        var left = new Border { Width = 664, Height = 620, CornerRadius = new(44, 10, 10, 44), Background = HBrush("#bba0d9"), BorderBrush = HBrush("#d8c6ec"), BorderThickness = new(8) }; HAt(board, left, 120, 124);
        var right = new Border { Width = 648, Height = 620, CornerRadius = new(10, 44, 44, 10), Background = HBrush("#302b36"), BorderBrush = HBrush("#4b4453"), BorderThickness = new(8) }; HAt(board, right, 832, 124);
        var cards = w.Cards; var selected = cards.FirstOrDefault(c => c.Id == w.SelectedId); bool has = selected.Id != null;
        var face = new Border { Width = 270, Height = 340, CornerRadius = new(18), Background = HBrush("#fbf8ff"), BorderBrush = HBrush("#52dcff"), BorderThickness = new(4) }; HAt(board, face, 160, 168);
        HAt(board, Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), 230), 180, 210);
        HAt(board, HText(has ? selected.Name : "No setup selected", 18, "#1b1622", FontWeights.Bold), 178, 438);
        HAt(board, HText(has ? selected.State : w.LibraryText, 13, "#5c4075", FontWeights.Normal), 178, 472);
        HAt(board, HText("SELECTED SETUP", 13, "#5c4075", FontWeights.Bold), 458, 172);
        HAt(board, HText(has ? w.DetailName : "Make it yours", 26, "#1b1622", FontWeights.ExtraBold), 458, 195);
        var detail = HText(has ? w.DetailModel : "Create a setup or import a saved copy.", 14, "#5c4075", FontWeights.SemiBold); detail.Width = 285; detail.TextWrapping = TextWrapping.Wrap; HAt(board, detail, 458, 235);
        int j = 0; foreach (var (label, value) in w.Metrics) { HAt(board, HText(label + "   " + value, 15, "#1b1622", FontWeights.SemiBold), 458, 300 + j++ * 32); }
        if (rename != null) { rename.Width = 260; rename.Height = 36; rename.Tag = "hybrid:setup:name"; HAt(board, rename, 458, 450); var saveName = HybridButton("Save name", "hybrid:setup:saveName", false, 130, 36); saveName.IsEnabled = w.Can("saveName"); saveName.Click += (_, _) => w.Click("saveName"); HAt(board, saveName, 458, 490); }
        var actions = new WrapPanel { Width = 610 };
        foreach (var (label, key) in new[] { ("Open", "open"), ("Save", "save"), ("Duplicate", "duplicate"), ("Rename", "rename"), ("Export", "export"), ("Delete", "delete") }) { var button = HybridButton(label, "hybrid:setup:" + key, key == "open", 130, 40); button.Margin = new(0, 0, 8, 8); button.IsEnabled = w.Can(key); button.Click += (_, _) => w.Click(key); actions.Children.Add(button); } HAt(board, actions, 160, 548);
        var grid = new WrapPanel { Width = 580, ItemWidth = 190, ItemHeight = 205 };
        foreach (var card in cards) { var button = HybridButton(card.Name + "\n" + card.State, "hybrid:setup:card:" + card.Id, card.Id == w.SelectedId, 176, 184); button.Click += (_, _) => w.Select(card.Id); grid.Children.Add(button); }
        var cardScroll = new ScrollViewer { Content = grid, Width = 580, Height = 428, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; HAt(board, cardScroll, 872, 164);
        var utilities = new WrapPanel { Width = 580 };
        foreach (var (label, key) in new[] { ("New setup", "new"), ("Save current draft", "save"), ("Import setup", "import"), ("Copy legacy setups", "legacy") }) { if (key == "legacy" && !w.HasLegacy) continue; var button = HybridButton(label, "hybrid:setup:util:" + key, false, 175, 40); button.Margin = new(0, 0, 8, 8); button.IsEnabled = w.Can(key); button.Click += (_, _) => w.Click(key); utilities.Children.Add(button); } HAt(board, utilities, 872, 668);
        var notice = HText(w.NoticeText, 13, "#4b3d5e", FontWeights.Normal); notice.MaxWidth = 650; notice.TextTrimming = TextTrimming.CharacterEllipsis; HAt(board, notice, 120, 780);
        if (w.DeletePending)
        {
            HAt(board, new Rectangle { Width = 1600, Height = 848, Fill = new SolidColorBrush(Color.FromArgb(185, 0, 0, 0)) }, 0, 0);
            HAt(board, new Border { Width = 560, Height = 220, CornerRadius = new(24), Background = HBrush("#2d2933"), BorderBrush = HBrush("#52dcff"), BorderThickness = new(2) }, 520, 260);
            HAt(board, HText(w.DeleteQuestion, 24, "#f5f1fa", FontWeights.Bold), 555, 288);
            var keep = HybridButton("Keep setup", "hybrid:setup:keep", true, 210, 44); keep.Click += (_, _) => w.Click("keep"); HAt(board, keep, 555, 370);
            var remove = HybridButton("Remove setup", "hybrid:setup:remove", false, 210, 44); remove.Click += (_, _) => w.Click("remove"); HAt(board, remove, 790, 370); focused = "hybrid:setup:keep";
        }
        string? want = focused ?? (rename != null ? "hybrid:setup:name" : has ? "hybrid:setup:card:" + selected.Id : "hybrid:setup:util:new");
        if (want != null) Dispatcher.BeginInvoke(() => FindTagged(board, want)?.Focus(), DispatcherPriority.Input);
    }
}
