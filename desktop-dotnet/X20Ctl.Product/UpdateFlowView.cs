using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>The in-app update screens over the Zone: the question, the download, the reason when it fails, and (after the restart) What's new.
/// Built from the same dim backdrop and card as the controller scan overlay. A/Enter press the focused button, B/Esc go back, arrows move between buttons.</summary>
public sealed class UpdateFlowView : Grid
{
    readonly UpdateFlow? flow;
    readonly Border card = new() { MaxWidth = 560, MinWidth = 420, Padding = new(32, 28, 32, 28), CornerRadius = new(14), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BorderThickness = new(1) };
    readonly StackPanel content = new();
    readonly bool whatsNew;
    string? whatsNewVersion, whatsNewText;
    Border? fill; TextBlock? percent;

    public event Action? Yes, No, Cancel, Retry, Close, Ok, NeverRemind;

    public UpdateFlowView(UpdateFlow flow) : this(flow, false) { Render(); }
    UpdateFlowView(UpdateFlow? flow, bool whatsNew)
    {
        this.flow = flow; this.whatsNew = whatsNew;
        Background = new SolidColorBrush(Color.FromArgb(170, 2, 6, 18));
        card.Background = new LinearGradientBrush(Color.FromRgb(24, 31, 50), Color.FromRgb(14, 19, 33), 90);
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 140, 175, 230));
        card.Child = content; Children.Add(card);
        Focusable = true;
    }

    /// <summary>The screen shown once after a self-update: the version's own changelog entry (null = no entry), with OK and Never remind me again.</summary>
    public static UpdateFlowView WhatsNew(string versionName, string? entry)
    {
        var view = new UpdateFlowView(null, true) { whatsNewVersion = versionName, whatsNewText = entry };
        view.card.MaxWidth = 680; view.card.Width = 640;
        view.Render(); return view;
    }

    /// <summary>Redraw for the flow's current step (the progress bar updates in place).</summary>
    public void Render()
    {
        if (flow is { Step: UpdateStep.Downloading } && fill != null && percent != null) { SetProgress(flow.Progress); return; }
        content.Children.Clear(); fill = null; percent = null;
        if (whatsNew) { RenderWhatsNew(); return; }
        var f = flow!;
        content.Children.Add(Kit.Text(f.Title, 22, "DS.Text", FontWeights.SemiBold));
        if (f.Step == UpdateStep.Downloading)
        {
            content.Children.Add(Wrap("X20CTL is downloading from GitHub. It is checked before anything is replaced.", 14.5, "DS.TextMuted", new(0, 10, 0, 0)));
            var track = new Border { Height = 8, CornerRadius = new(4), Margin = new(0, 22, 0, 0), Background = new SolidColorBrush(Color.FromArgb(70, 160, 190, 235)) };
            fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new(4), Background = new SolidColorBrush(Color.FromRgb(86, 190, 255)) };
            track.Child = fill; content.Children.Add(track);
            percent = Kit.Text("", 14, "DS.TextMuted"); percent.Margin = new(0, 8, 0, 0); content.Children.Add(percent);
            track.SizeChanged += (_, _) => SetProgress(f.Progress);
            Buttons(("Cancel", "ActionButton", () => Cancel?.Invoke()));
            SetProgress(f.Progress);
        }
        else
        {
            content.Children.Add(Wrap(f.Body, 15, "DS.TextMuted", new(0, 12, 0, 0)));
            switch (f.Step)
            {
                case UpdateStep.Confirm: Buttons(("Yes", "PrimaryButton", () => Yes?.Invoke()), ("No", "ActionButton", () => No?.Invoke())); break;
                case UpdateStep.Failed when f.CanRetry: Buttons(("Try again", "PrimaryButton", () => Retry?.Invoke()), ("Close", "ActionButton", () => Close?.Invoke())); break;
                case UpdateStep.Failed: Buttons(("Close", "PrimaryButton", () => Close?.Invoke())); break;
            }
        }
    }

    void SetProgress(double p)
    {
        if (fill == null || percent == null) return;
        percent.Text = $"{(int)Math.Round(p * 100)}%";
        if (fill.Parent is Border track && track.ActualWidth > 0) fill.Width = track.ActualWidth * Math.Clamp(p, 0, 1);
    }

    void RenderWhatsNew()
    {
        content.Children.Add(Kit.Text("What's new", 13, "DS.TextMuted", FontWeights.SemiBold));
        content.Children.Add(Kit.Text($"X20CTL {whatsNewVersion}", 24, "DS.Text", FontWeights.SemiBold));
        var list = new StackPanel { Margin = new(0, 14, 0, 0) };
        if (string.IsNullOrWhiteSpace(whatsNewText)) list.Children.Add(Wrap($"You are now on {whatsNewVersion}.", 15, "DS.TextMuted", new(0)));
        else foreach (var raw in whatsNewText.Split('\n'))
        {
            string line = raw.TrimEnd(); if (line.Length == 0) continue;
            if (line.StartsWith("### ")) { var h = Kit.Text(line[4..].Trim(), 15, "DS.Text", FontWeights.SemiBold); h.Margin = new(0, 12, 0, 4); list.Children.Add(h); }
            else if (line.StartsWith("- ")) list.Children.Add(Wrap("•  " + Plain(line[2..]), 14.5, "DS.TextMuted", new(0, 4, 0, 0)));
            else list.Children.Add(Wrap(Plain(line), 14.5, "DS.TextMuted", new(0, 4, 0, 0)));
        }
        content.Children.Add(new ScrollViewer { MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });
        Buttons(("OK", "PrimaryButton", () => Ok?.Invoke()), ("Never remind me again", "ActionButton", () => NeverRemind?.Invoke()));
    }

    /// <summary>Markdown emphasis, code ticks and links reduced to their text.</summary>
    public static string Plain(string s) => Regex.Replace(Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1"), @"[*_`]{1,2}", "");

    static TextBlock Wrap(string text, double size, string brush, Thickness margin)
    { var t = Kit.Text(text, size, brush); t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = margin; return t; }

    readonly List<Button> buttons = [];
    void Buttons(params (string Text, string Style, Action Click)[] items)
    {
        buttons.Clear();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 24, 0, 0) };
        foreach (var (text, style, click) in items)
        {
            var b = new Button { Content = text, Style = DS.Style(style), MinHeight = 40, Padding = new(22, 0, 22, 0), Margin = new(0, 0, 10, 0) };
            b.Click += (_, _) => click(); buttons.Add(b); row.Children.Add(b);
        }
        content.Children.Add(row);
    }

    /// <summary>Focus the first button so A / Enter answers straight away.</summary>
    public void FocusFirst() { if (buttons.Count > 0) { buttons[0].Focus(); Keyboard.Focus(buttons[0]); } }

    /// <summary>Keyboard and controller (A, B arrive as keys). The overlay owns the keyboard: nothing behind it moves.</summary>
    public bool HandleKey(Key key)
    {
        if (key is Key.Escape or Key.B) { Back(); return true; }
        if (key is Key.A or Key.Enter) { (Keyboard.FocusedElement as Button ?? buttons.FirstOrDefault())?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return true; }
        if (key is Key.Left or Key.Right or Key.Up or Key.Down && buttons.Count > 1)
        {
            int at = buttons.FindIndex(b => b.IsKeyboardFocused), next = key is Key.Left or Key.Up ? Math.Max(0, at - 1) : Math.Min(buttons.Count - 1, at + 1);
            buttons[Math.Max(0, next)].Focus();
        }
        return true;
    }

    /// <summary>B / Esc: No, Cancel or Close for the flow; OK for What's new (never the "never remind" choice).</summary>
    public void Back()
    {
        if (whatsNew) { Ok?.Invoke(); return; }
        switch (flow!.Step) { case UpdateStep.Confirm: No?.Invoke(); break; case UpdateStep.Downloading: Cancel?.Invoke(); break; case UpdateStep.Failed: Close?.Invoke(); break; }
    }
}
