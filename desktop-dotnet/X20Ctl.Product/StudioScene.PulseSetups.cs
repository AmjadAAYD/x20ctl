using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private SetupsWorkbench? pulseSetupsBench;

    private void BuildPulseSetups(Canvas host)
    {
        var w = Setups;
        if (w == null) return;

        if (!ReferenceEquals(w, pulseSetupsBench))
        {
            pulseSetupsBench = w;
            w.Watch(() =>
            {
                if (CurrentPage == "Setups" && ReferenceEquals(pulseSetupsBench, Setups))
                    PulseLater();
            });
        }

        var cards = w.Cards;
        string selId = w.SelectedId ?? (cards.Count > 0 ? cards[0].Id : "");

        // Frame Header per t_pulse.py:770
        var title = PulseParts.Disp("Setups", 44, PulseParts.TextPrimary, FontWeights.ExtraLight);
        Canvas.SetLeft(title, 272);
        Canvas.SetTop(title, 30);
        host.Children.Add(title);

        // 1. Top action buttons at (1040, 40) per t_pulse.py:771
        var topActions = new StackPanel { Orientation = Orientation.Horizontal };
        topActions.Children.Add(PulseParts.PillButton("+ New setup", primary: false, onPearl: false, small: true, onClick: () => { w.Click("new"); PulseLater(); }));
        topActions.Children.Add(PulseParts.PillButton("Import", primary: false, onPearl: false, small: true, onClick: () => { w.Click("import"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0)));
        if (w.HasLegacy)
            topActions.Children.Add(PulseParts.PillButton("Copy legacy", primary: false, onPearl: false, small: true, onClick: () => { w.Click("legacy"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0)));

        Canvas.SetLeft(topActions, 1040);
        Canvas.SetTop(topActions, 40);
        host.Children.Add(topActions);

        // 2. Selected Large Setup Card at (x=330, y=150, w=300, h=360) per t_pulse.py:774
        double cardW = 300, cardH = 360;
        double selX = 330, selY = 150;

        // Resonance arcs around selected card
        var rres = PulseParts.ResonanceMark(cardW / 2 + 26);
        Canvas.SetLeft(rres, selX + cardW / 2 - (cardW / 2 + 26));
        Canvas.SetTop(rres, selY + cardH / 2 - (cardW / 2 + 26));
        host.Children.Add(rres);

        var selCard = new Border
        {
            Width = cardW,
            Height = cardH,
            CornerRadius = new(26),
            Background = new LinearGradientBrush(PulseInk.C("#0d1b34"), PulseInk.C("#070e1c"), 90),
            BorderBrush = PulseInk.B(PulseParts.PearlColor),
            BorderThickness = new(2),
            Effect = PulseInk.Glow(PulseParts.CyanBright, 34, 0.55),
            ClipToBounds = true,
            Cursor = Cursors.Hand
        };

        var selContent = new Canvas { Width = cardW, Height = cardH };
        double padW = cardW * 0.88;
        double padH = padW / 1.5;
        var padArt = PulseParts.ControllerPedestal(Model.Id, rear: false, width: padW, height: padH, showRipples: false, finish: PadColours.For(Player, Model.Id));
        Canvas.SetLeft(padArt, (cardW - padW) / 2);
        Canvas.SetTop(padArt, 24);
        selContent.Children.Add(padArt);

        var selInfo = new StackPanel { Margin = new(24, 0, 24, 22), VerticalAlignment = VerticalAlignment.Bottom };
        selInfo.Children.Add(PulseParts.Kicker(w.DetailState is { Length: > 0 } st ? st : "LOCAL PROFILE"));
        selInfo.Children.Add(PulseParts.Disp(w.DetailName is { Length: > 0 } dn ? dn : "Default Setup", 28, PulseParts.TextPrimary, FontWeights.SemiBold).Also(t => t.Margin = new(0, 4, 0, 0)));
        Canvas.SetLeft(selInfo, 0);
        Canvas.SetTop(selInfo, cardH - 85);
        selContent.Children.Add(selInfo);
        selCard.Child = selContent;

        Canvas.SetLeft(selCard, selX);
        Canvas.SetTop(selCard, selY);
        host.Children.Add(selCard);

        // 3. Arc of Remaining Setups along circle arc (C0=(1150, 1800), R0=1520) per t_pulse.py:780
        double c0x = 1150, c0y = 1800, r0 = 1520;
        int cardIdx = 0;
        foreach (var c in cards)
        {
            if (c.Id == selId) continue;
            double acx = 770 + cardIdx * 160;
            double dy = Math.Sqrt(Math.Max(0, r0 * r0 - (acx - c0x) * (acx - c0x)));
            double acy = c0y - dy + 30;
            double cw = 132, chh = 168;

            var otherCard = new Border
            {
                Width = cw,
                Height = chh,
                CornerRadius = new(18),
                Background = new LinearGradientBrush(PulseInk.A(PulseInk.C("#0d1b34"), 0.7), PulseInk.A(PulseInk.C("#070e1c"), 0.8), 90),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, 0.25)),
                BorderThickness = new(1),
                Effect = PulseInk.Drop(0, 14, 26, Colors.Black, 0.55),
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            var otherStack = new StackPanel { Margin = new(12) };
            otherStack.Children.Add(PulseParts.Kicker("SETUP", overrideColor: PulseParts.TextDim));
            otherStack.Children.Add(PulseParts.Body(c.Name, 13.5, PulseParts.TextPrimary, FontWeights.SemiBold).Also(t => t.Margin = new(0, 4, 0, 0)));
            otherStack.Children.Add(PulseParts.Body(c.State, 11, PulseParts.TextSoft).Also(t => t.Margin = new(0, 4, 0, 0)));
            otherCard.Child = otherStack;

            string pickId = c.Id;
            otherCard.MouseLeftButtonDown += (_, e) =>
            {
                w.Select(pickId);
                PulseLater();
                e.Handled = true;
            };

            Canvas.SetLeft(otherCard, acx - cw / 2);
            Canvas.SetTop(otherCard, acy - chh / 2);
            host.Children.Add(otherCard);

            cardIdx++;
            if (cardIdx >= 5) break;
        }

        // Empty card slots if fewer setups exist
        while (cardIdx < 5)
        {
            double acx = 770 + cardIdx * 160;
            double dy = Math.Sqrt(Math.Max(0, r0 * r0 - (acx - c0x) * (acx - c0x)));
            double acy = c0y - dy + 30;
            double cw = 132, chh = 168;

            var emptySlot = new Border
            {
                Width = cw,
                Height = chh,
                CornerRadius = new(18),
                Background = PulseInk.B(PulseInk.A(Colors.White, 0.015)),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, 0.14)),
                BorderThickness = new(1.4),
                Child = PulseParts.Body("Empty", 12, PulseParts.TextDim).Also(t =>
                {
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    t.VerticalAlignment = VerticalAlignment.Center;
                })
            };
            Canvas.SetLeft(emptySlot, acx - cw / 2);
            Canvas.SetTop(emptySlot, acy - chh / 2);
            host.Children.Add(emptySlot);
            cardIdx++;
        }

        // 4. Rising Pearl Card at (262, 596, w=1300, h=230) per t_pulse.py:795
        var detailStack = new StackPanel();

        // Top tag row
        var tagRow = new StackPanel { Orientation = Orientation.Horizontal };
        tagRow.Children.Add(PulseParts.Kicker("SELECTED SETUP"));
        tagRow.Children.Add(PulseParts.Chip(w.DetailState is { Length: > 0 } ds ? ds : "ACTIVE", "on", onPearl: true).Also(c => c.Margin = new(12, 0, 0, 0)));
        tagRow.Children.Add(PulseParts.Body("Matches assigned controller model", 13, PulseParts.PearlSoft).Also(t => t.Margin = new(12, 2, 0, 0)));
        detailStack.Children.Add(tagRow);

        // Name + Stats row
        var midRow = new DockPanel { LastChildFill = true, Margin = new(0, 14, 0, 18) };
        var nameText = PulseParts.Disp(w.DetailName is { Length: > 0 } nameVal ? nameVal : "My Setup", 28, PulseParts.PearlInk, FontWeights.SemiBold);
        nameText.Width = 300;
        DockPanel.SetDock(nameText, Dock.Left);
        midRow.Children.Add(nameText);

        var statsGrid = new UniformGrid { Columns = 4 };
        foreach (var (mLabel, mVal) in w.Metrics)
        {
            var mCol = new StackPanel();
            mCol.Children.Add(PulseParts.Kicker(mLabel, onPearl: true, overrideColor: PulseParts.PearlSoft));
            mCol.Children.Add(PulseParts.Disp(mVal, 22, PulseParts.PearlInk, FontWeights.Light).Also(t => t.Margin = new(0, 4, 0, 0)));
            statsGrid.Children.Add(mCol);
        }
        midRow.Children.Add(statsGrid);
        detailStack.Children.Add(midRow);

        // Actions row
        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal };
        actionsRow.Children.Add(PulseParts.PillButton("Apply to controller", primary: true, onPearl: true, onClick: () => { w.Click("open"); PulseLater(); }));
        actionsRow.Children.Add(PulseParts.PillButton("Duplicate", primary: false, onPearl: true, onClick: () => { w.Click("duplicate"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0)));
        actionsRow.Children.Add(PulseParts.PillButton("Rename", primary: false, onPearl: true, onClick: () => { w.Click("rename"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0)));
        actionsRow.Children.Add(PulseParts.PillButton("Export file", primary: false, onPearl: true, onClick: () => { w.Click("export"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0)));
        actionsRow.Children.Add(PulseParts.PillButton("Delete setup", primary: false, onPearl: true, danger: true, onClick: () => { w.Click("delete"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0)));
        detailStack.Children.Add(actionsRow);

        var pearl = PulseParts.RisingPearlCard(detailStack, new Thickness(30, 24, 30, 24));
        pearl.Width = 1300;
        pearl.Height = 230;
        Canvas.SetLeft(pearl, 262);
        Canvas.SetTop(pearl, 596);
        host.Children.Add(pearl);

        // Bottom note at (272, 852)
        var bottomNote = PulseParts.Body("Setups store all remappings, stick curves, macros, and vibration profiles.", 13.5, PulseParts.TextSoft);
        Canvas.SetLeft(bottomNote, 272);
        Canvas.SetTop(bottomNote, 852);
        host.Children.Add(bottomNote);

        // Modal Delete Confirmation (if pending)
        if (w.DeletePending)
        {
            var modalSheet = PulseParts.RisingPearlCard(new StackPanel
            {
                Children =
                {
                    PulseParts.Kicker("CONFIRM DELETION", onPearl: true, overrideColor: PulseParts.RoseColor),
                    PulseParts.Disp("Delete this setup?", 22, PulseParts.PearlInk, FontWeights.Bold).Also(t => t.Margin = new(0, 6, 0, 10)),
                    PulseParts.Body(w.DeleteQuestion, 14, PulseParts.PearlMid, wrap: true),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new(0, 20, 0, 0),
                        Children =
                        {
                            PulseParts.PillButton("Delete Permanently", primary: false, onPearl: true, danger: true, onClick: () => { w.Click("remove"); PulseLater(); }),
                            PulseParts.PillButton("Keep Setup", primary: true, onPearl: true, onClick: () => { w.Click("keep"); PulseLater(); }).Also(b => b.Margin = new(12, 0, 0, 0))
                        }
                    }
                }
            }, new Thickness(32));
            modalSheet.Width = 520;
            Canvas.SetLeft(modalSheet, 540);
            Canvas.SetTop(modalSheet, 320);
            Panel.SetZIndex(modalSheet, 50);
            host.Children.Add(modalSheet);
        }
    }
}
