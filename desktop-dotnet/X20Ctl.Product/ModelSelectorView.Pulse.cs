using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildPulseSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var models = catalog.Models.ToDictionary(model => model.Id);
        string[] order = ["x20_pro", "x05_pro", "x10", "x20", "d10", "x15", "dune"];
        string selectedModelId = "x20";

        var board = new Canvas { Name = "PulseChoose", Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(PulseKit.VoidBackdrop());

        // Header bar
        var header = PulseParts.TopHeader("Choose Controller", $"Player {Player + 1}", "Cancel", () => Cancelled?.Invoke(), out _, out _, out _);
        Canvas.SetLeft(header, 124);
        Canvas.SetTop(header, 22);
        board.Children.Add(header);

        // Turntable stage: center at (ox=800, oy=500), rx=560, ry=95
        double ox = 800, oy = 500, rx = 560, ry = 95;
        board.Children.Add(PulseKit.Aura(ox, oy + 40, 520, 180, 0.22));
        board.Children.Add(PulseKit.Ripples(ox, oy + 40, n: 6, r0: 160, step: 60, op: 0.08));

        var rail = new Ellipse
        {
            Width = rx * 2,
            Height = ry * 2,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.35)),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection([4, 6])
        };
        Canvas.SetLeft(rail, ox - rx);
        Canvas.SetTop(rail, oy - ry);
        board.Children.Add(rail);

        // Host canvas for turntable cards
        var turntableCanvas = new Canvas { Width = 1600, Height = 900 };
        board.Children.Add(turntableCanvas);

        // Bottom Rising Pearl Card for Selected Details (x=360, y=690, w=880, h=175)
        var detailHost = new Border();
        Canvas.SetLeft(detailHost, 360);
        Canvas.SetTop(detailHost, 690);
        board.Children.Add(detailHost);

        void RenderTurntable(string activeId)
        {
            selectedModelId = activeId;
            turntableCanvas.Children.Clear();

            int n = order.Length;
            int activeIndex = Array.IndexOf(order, activeId);
            if (activeIndex < 0) activeIndex = 0;

            for (int i = 0; i < n; i++)
            {
                string id = order[i];
                if (!models.TryGetValue(id, out var model)) continue;

                // Relative angle so selected model is in front (ang = 90)
                int relIndex = (i - activeIndex + n) % n;
                double ang = 90 + relIndex * (360.0 / n);
                double rad = ang * Math.PI / 180.0;
                double depth = (Math.Sin(rad) + 1.0) / 2.0;

                bool isSelected = id == activeId;
                double w = isSelected ? 420 : 160 + depth * 120;
                double h = w / 1.5;

                double x = ox + rx * Math.Cos(rad) - w / 2;
                double y = oy + ry * Math.Sin(rad) - h / 2 - (isSelected ? 24 : 0);

                var itemStack = new StackPanel
                {
                    Width = w,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Cursor = Cursors.Hand
                };

                // Reflection / shadow under the floating controller
                var shadow = new Ellipse
                {
                    Width = w * 0.72,
                    Height = isSelected ? 28 : 18,
                    Fill = new RadialGradientBrush(Color.FromArgb((byte)(isSelected ? 160 : 80), 0, 0, 0), Colors.Transparent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new(0, 0, 0, -18)
                };

                var padArt = PulseParts.ControllerPedestal(id, rear: false, width: w, height: h, showRipples: isSelected, finish: null);
                padArt.HorizontalAlignment = HorizontalAlignment.Center;
                if (!isSelected) padArt.Opacity = 0.40 + 0.45 * depth;

                itemStack.Children.Add(padArt);
                itemStack.Children.Add(shadow);

                var nameBlock = PulseParts.Disp(model.Name.Replace("EasySMX ", ""), isSelected ? 17 : 12.5, isSelected ? PulseParts.TextPrimary : PulseParts.TextSoft, isSelected ? FontWeights.Bold : FontWeights.Normal);
                nameBlock.HorizontalAlignment = HorizontalAlignment.Center;
                nameBlock.Margin = new(0, isSelected ? 12 : 6, 0, 0);
                itemStack.Children.Add(nameBlock);

                string pickId = id;
                itemStack.MouseLeftButtonDown += (_, e) =>
                {
                    RenderTurntable(pickId);
                    e.Handled = true;
                };

                Canvas.SetLeft(itemStack, x);
                Canvas.SetTop(itemStack, y);
                Panel.SetZIndex(itemStack, isSelected ? 100 : (int)(depth * 50));
                turntableCanvas.Children.Add(itemStack);
            }

            // Update bottom pearl card
            var activeModel = models[activeId];
            var f = facts.TryGetValue(activeId, out var fact) ? fact : null;

            var pearlStack = new DockPanel { LastChildFill = true };

            var chooseBtn = PulseParts.PillButton("Select Controller", primary: true, onPearl: true, onClick: () =>
            {
                if (choices.TryGetValue(activeId, out var realBtn))
                    realBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            });
            chooseBtn.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(chooseBtn, Dock.Right);
            pearlStack.Children.Add(chooseBtn);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 24, 0) };
            infoStack.Children.Add(PulseParts.Kicker("CONTROLLER SPECIFICATION", onPearl: true));
            infoStack.Children.Add(PulseParts.Disp(activeModel.Name, 24, PulseParts.PearlInk, FontWeights.Bold).Also(t => t.Margin = new(0, 2, 0, 4)));

            string specText = f != null ? $"{f.Description} · {f.SupportLine}" : "Verified EasySMX controller model with full local configuration support.";
            infoStack.Children.Add(PulseParts.Body(specText, 13, PulseParts.PearlMid, wrap: true));
            pearlStack.Children.Add(infoStack);

            var pearl = PulseParts.RisingPearlCard(pearlStack, new Thickness(36, 20, 36, 20));
            pearl.Width = 880;
            pearl.Height = 155;
            detailHost.Child = pearl;
        }

        RenderTurntable("x20");

        // Footer hints
        var hintsHost = new StackPanel { Orientation = Orientation.Horizontal };
        PulseParts.PopulateFooterHints(hintsHost, new[] { ("A", "Choose"), ("B", "Back"), ("◄►", "Rotate Turntable") });
        Canvas.SetLeft(hintsHost, 1150);
        Canvas.SetTop(hintsHost, 850);
        board.Children.Add(hintsHost);

        Children.Clear();
        Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform });
    }
}
