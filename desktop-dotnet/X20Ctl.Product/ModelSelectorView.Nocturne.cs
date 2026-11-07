using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildNocturneSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var models = catalog.Models.ToDictionary(model => model.Id);

        var chooseItems = new (string Key, string Name, string Specs, string Status)[]
        {
            ("x20", "EasySMX X20", "Hall sticks · Hall triggers · RGB · 4 macro paddles", "Full PC Mode Configuration"),
            ("x20_pro", "EasySMX X20 Pro", "TMR sticks · Hall / microswitch triggers · RGB · screen · trigger haptics · 6 macro paddles", "Full PC Mode Configuration"),
            ("x05_pro", "EasySMX X05 Pro", "Hall sticks · Hall / microswitch triggers · RGB · trigger haptics · 2 macro paddles", "Full PC Mode Configuration"),
            ("x10", "EasySMX X10", "Hall sticks · Hall triggers · 2 macro paddles", "Full PC Mode Configuration"),
            ("d10", "EasySMX D10", "TMR sticks · Hall / microswitch triggers · RGB · 2 macro paddles", "Full PC Mode Configuration")
        };

        var frame = new NocturneToolFrame(
            "CHOOSE CONTROLLER MODEL",
            "Select which EasySMX gamepad profile to configure",
            onBack: () => Cancelled?.Invoke());

        var canvas = frame.ContentArea;

        var listSp = new StackPanel { Width = 960 };
        for (int i = 0; i < chooseItems.Length; i++)
        {
            var item = chooseItems[i];
            models.TryGetValue(item.Key, out var model);

            var itemCard = new Border
            {
                Background = NocturneKit.NightBrush,
                BorderBrush = NocturneKit.RuleBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:model:{item.Key}",
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoSp = new StackPanel();
            infoSp.Children.Add(new TextBlock { Text = item.Name.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = NocturneKit.WhiteBrush });
            infoSp.Children.Add(new TextBlock { Text = item.Specs, FontFamily = NocturneKit.Body, FontSize = 11.5, Foreground = NocturneKit.SoftBrush, Margin = new Thickness(0, 4, 0, 0) });
            Grid.SetColumn(infoSp, 0);

            var statusBadge = new Border
            {
                Background = NocturneKit.AccentBrush,
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadge.Child = new TextBlock { Text = item.Status.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, FontSize = 10, Foreground = NocturneKit.WhiteBrush };
            Grid.SetColumn(statusBadge, 1);

            grid.Children.Add(infoSp);
            grid.Children.Add(statusBadge);
            itemCard.Child = grid;

            itemCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (model != null) Chosen?.Invoke(model);
            };

            listSp.Children.Add(itemCard);
        }

        var framed = NocturneKit.NocturneWhiteCard(listSp, 1020, 520);
        Canvas.SetLeft(framed, 290);
        Canvas.SetTop(framed, 40);
        canvas.Children.Add(framed);

        Children.Add(frame);
    }
}
