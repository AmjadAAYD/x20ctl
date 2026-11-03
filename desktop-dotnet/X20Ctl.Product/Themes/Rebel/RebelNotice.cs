using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace X20Ctl.Product.Rebel;

/// <summary>The approved Rebel calling card, with native buttons and a contained focus cycle.</summary>
internal sealed class RebelNotice : Grid
{
    internal Button No { get; }
    internal Button Yes { get; }
    private readonly Action<bool> answer;
    private bool resolved;
    internal RebelNotice(string title, string summary, IReadOnlyList<string> lines, string question, string yes, Action<bool> answer)
    {
        this.answer = answer; Name = "RebelNotice"; Background = Ink.B(Ink.A(Ink.Black, .84));
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle); KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.Cycle);
        var board = new Canvas { Width = 1600, Height = 900, Background = Brushes.Transparent };
        Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform });
        var card = new Canvas { Width = 940, Height = 500, Background = Ink.B(Ink.Red), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-1.5), Effect = Ink.Shadow(14, 14, 0, Ink.Black, 1) };
        card.Children.Add(new System.Windows.Shapes.Polygon { Points = new PointCollection([new(724, 0), new(752, 0), new(566, 500), new(538, 500)]), Fill = Ink.B(Ink.Black), IsHitTestVisible = false });
        card.Children.Add(new Border { Width = 940, Height = 500, BorderBrush = Ink.B(Ink.White), BorderThickness = new(6), IsHitTestVisible = false });
        RebelToolFrame.At(board, card, 330, 90); RebelToolFrame.At(card, Pieces.Logo(120), 44, 34);
        var left = new StackPanel { Width = 410 }; left.Children.Add(Ink.Text(title, 28));
        var lead = Ink.Text(summary, 19); lead.TextWrapping = TextWrapping.Wrap; lead.Margin = new(0, 14, 0, 0); left.Children.Add(lead);
        foreach (string line in lines) { var body = Ink.Text("✦  " + line, 16, null, false); body.TextWrapping = TextWrapping.Wrap; body.Margin = new(0, 12, 0, 0); left.Children.Add(body); }
        RebelToolFrame.At(card, left, 46, 132);
        var right = new StackPanel { Width = 400 }; var bodyText = Ink.Text(question, 16, null, false); bodyText.TextWrapping = TextWrapping.Wrap; right.Children.Add(bodyText);
        var call = new Viewbox { Child = Ransom.Make(yes, 40), MaxWidth = 400, Height = 110, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 22, 0, 0) }; right.Children.Add(call);
        var actions = new WrapPanel { Margin = new(0, 26, 0, 0) };
        No = new SlabButton("B  Not now", 170, 48, 950, 0, 18, Ink.White); Yes = new SlabButton("A  " + yes, 216, 48, 951, 0, 18, Ink.Black, true);
        No.Click += (_, _) => Resolve(false); Yes.Click += (_, _) => Resolve(true); actions.Children.Add(No); actions.Children.Add(Yes); right.Children.Add(actions); RebelToolFrame.At(card, right, 500, 48);
        PreviewKeyDown += (_, e) => { if (e.Key is Key.Escape or Key.B) { Resolve(false); e.Handled = true; } };
        Loaded += (_, _) => No.Focus(); Unloaded += (_, _) => Resolve(false);
    }
    internal void Resolve(bool accepted) { if (resolved) return; resolved = true; answer(accepted); }
}
