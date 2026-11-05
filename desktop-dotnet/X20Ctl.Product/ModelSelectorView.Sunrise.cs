using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildSunriseSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var models = catalog.Models.ToDictionary(model => model.Id);

        var chooseItems = new (string Key, string Name, string Specs, string Status)[]
        {
            ("x20", "EasySMX X20", "Hall sticks · Hall triggers · RGB · 4 macro paddles", "Full configuration"),
            ("x20_pro", "EasySMX X20 Pro", "TMR sticks · Hall / microswitch triggers · RGB · screen · trigger haptics · 6 macro paddles", "Full configuration"),
            ("x05_pro", "EasySMX X05 Pro", "Hall sticks · Hall / microswitch triggers · RGB · trigger haptics · 2 macro paddles", "Not available yet"),
            ("x10", "EasySMX X10", "Hall sticks · Hall triggers · 2 macro paddles", "Input testing · experimental"),
            ("d10", "EasySMX D10", "TMR sticks · Hall / microswitch triggers · RGB · 2 macro paddles", "Input testing · experimental")
        };

        var frame = new SunriseToolFrame(
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
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:model:{item.Key}"
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoSp = new StackPanel();
            infoSp.Children.Add(new TextBlock { Text = item.Name.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 14, Foreground = SunriseKit.InkBrush });
            infoSp.Children.Add(new TextBlock { Text = item.Specs, FontFamily = SunriseKit.Body, FontSize = 11.5, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 4, 0, 0) });
            Grid.SetColumn(infoSp, 0);

            var statusBadge = new Border
            {
                Background = SunriseKit.InkBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadge.Child = new TextBlock { Text = item.Status.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = SunriseKit.YellowBrush };
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

        var framed = SunriseKit.CreateStickerCard(listSp, 1020, 520, cornerRadius: 22);
        Canvas.SetLeft(framed, 290);
        Canvas.SetTop(framed, 40);
        canvas.Children.Add(framed);

        Children.Add(frame);
    }
}
