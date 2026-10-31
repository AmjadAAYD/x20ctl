using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

internal sealed class HybridNotice : Grid
{
    private readonly Action<bool> finished;
    private bool resolved;
    internal Button No { get; }
    internal HybridNotice(string title, string summary, IReadOnlyList<string> lines, string question, string yes, Action<bool> finished)
    {
        this.finished = finished; Background = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0));
        var board = new Canvas { Width = 1600, Height = 900 }; Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform });
        HybridToolFrame.At(board, new Border { Width = 720, Height = 430, CornerRadius = new(30), Background = HybridToolFrame.Color("#29242f"), BorderBrush = HybridToolFrame.Color("#52dcff"), BorderThickness = new(3) }, 440, 235);
        var body = new StackPanel { Width = 650 }; body.Children.Add(HybridToolFrame.Text(title, 29, "#f5f1fa", FontWeights.ExtraBold));
        var desc = HybridToolFrame.Text(summary, 17, "#d5bff6", FontWeights.SemiBold); desc.Margin = new(0, 10, 0, 0); body.Children.Add(desc);
        foreach (string line in lines) { var text = HybridToolFrame.Text("•  " + line, 15, "#e4dce9", FontWeights.Normal); text.Margin = new(0, 12, 0, 0); body.Children.Add(text); }
        var prompt = HybridToolFrame.Text(question, 16, "#f5f1fa", FontWeights.Bold); prompt.Margin = new(0, 18, 0, 0); body.Children.Add(prompt); HybridToolFrame.At(board, body, 475, 266);
        No = HybridToolFrame.Button("No", "hybrid:notice:no", 250, 48, true); No.Click += (_, _) => Resolve(false); HybridToolFrame.At(board, No, 475, 586);
        var accept = HybridToolFrame.Button(yes, "hybrid:notice:yes", 250, 48); accept.Click += (_, _) => Resolve(true); HybridToolFrame.At(board, accept, 755, 586);
    }
    internal void Resolve(bool accepted) { if (resolved) return; resolved = true; finished(accepted); }
}
