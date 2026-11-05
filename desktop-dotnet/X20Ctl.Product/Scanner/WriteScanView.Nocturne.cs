using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    private void BuildNocturneWriteWarning(Panel cards, Button go, Button leave)
    {
        var frame = new NocturneToolFrame(
            "WRITE SCAN INSTRUCTIONS",
            "Capture Bluetooth packets from KeyLinker to decode proprietary command structure.",
            onBack: () => leave.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        var listStack = new StackPanel { Width = 720 };
        listStack.Children.Add(new TextBlock
        {
            Text = "PROTOCOL CAPTURE GUIDELINES",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        string[] steps =
        [
            "Pair controller with mobile phone running KeyLinker",
            "Enable Bluetooth HCI snoop logging in Android Developer Options",
            "Change exactly one setting (e.g. A to B) and record packet timestamps",
            "Export btsnoop_hci.log and load into X20CTL to isolate opcode"
        ];

        foreach (var s in steps)
        {
            var rowSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            rowSp.Children.Add(new Rectangle { Width = 6, Height = 6, Fill = NocturneKit.AccentBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            rowSp.Children.Add(new TextBlock { Text = s, FontFamily = NocturneKit.Body, FontSize = 13, Foreground = NocturneKit.SoftBrush });
            listStack.Children.Add(rowSp);
        }

        if (go.Parent is Panel pGo) pGo.Children.Remove(go);
        else if (go.Parent is ContentControl cGo) cGo.Content = null;
        else if (go.Parent is Border bGo) bGo.Child = null;

        if (leave.Parent is Panel pLeave) pLeave.Children.Remove(leave);
        else if (leave.Parent is ContentControl cLeave) cLeave.Content = null;
        else if (leave.Parent is Border bLeave) bLeave.Child = null;

        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        go.Height = 40;
        go.Margin = new Thickness(0, 0, 14, 0);
        actRow.Children.Add(go);
        leave.Height = 40;
        actRow.Children.Add(leave);
        listStack.Children.Add(actRow);

        var card = NocturneKit.NocturneWhiteCard(listStack, 780, 400);
        Canvas.SetLeft(card, 410);
        Canvas.SetTop(card, 80);
        frame.ContentArea.Children.Add(card);

        Children.Add(frame);
    }
}
