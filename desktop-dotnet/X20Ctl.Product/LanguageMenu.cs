using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace X20Ctl.Product;

/// <summary>
/// The language button on the Controller Zone (owner request 31 Oct 2026): a round translate mark that opens the nine
/// languages. It sits in the host above the Zone, so every theme has the same one; the choice is saved and starts with
/// the next launch, like the theme.
/// </summary>
public sealed class LanguageMenu : Grid
{
    private readonly X20Ctl.Zone.ProductPreferences? preferences;
    private readonly Button button = new();
    private readonly Popup popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Top, PopupAnimation = PopupAnimation.Fade };
    private readonly StackPanel list = new();
    private readonly TextBlock note = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new(6, 8, 6, 2), Foreground = new SolidColorBrush(Color.FromRgb(160, 176, 205)), MaxWidth = 210 };
    private readonly Button restart = new() { Content = "Restart now", Visibility = Visibility.Collapsed, Margin = new(4, 6, 4, 2), Padding = new(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Action restartApp;

    public LanguageMenu(X20Ctl.Zone.ProductPreferences? preferences, UIElement showWith, Action? restartApp = null)
    {
        this.preferences = preferences;
        this.restartApp = restartApp ?? ThemeCatalog.Restart;
        HorizontalAlignment = HorizontalAlignment.Right; VerticalAlignment = VerticalAlignment.Bottom; Margin = new(0, 0, 22, 72);
        // it shows whenever the Zone does, and fades in with it
        SetBinding(VisibilityProperty, new Binding("Visibility") { Source = showWith });
        SetBinding(OpacityProperty, new Binding("Opacity") { Source = showWith });

        button.Width = button.Height = 46; button.Cursor = Cursors.Hand; button.Focusable = true; button.ToolTip = "Language";
        System.Windows.Automation.AutomationProperties.SetName(button, "Language");
        button.Template = ButtonTemplate();
        button.Content = Mark();
        button.Click += (_, _) => Open();
        Children.Add(button);

        popup.PlacementTarget = button; popup.VerticalOffset = -10; popup.HorizontalOffset = 0; popup.Placement = PlacementMode.Top;
        var panel = new Border
        {
            CornerRadius = new(14), Padding = new(8), BorderThickness = new(1), MinWidth = 220,
            Background = new SolidColorBrush(Color.FromArgb(244, 11, 17, 34)), BorderBrush = new SolidColorBrush(Color.FromArgb(120, 130, 165, 235)),
            Child = new StackPanel { Children = { list, note, restart } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, Opacity = .5, ShadowDepth = 4 },
        };
        popup.Child = panel;
        restart.Click += (_, _) => { popup.IsOpen = false; if (!ReviewSandbox.Active) this.restartApp(); };
        restart.Style = TryFindResource("DS.Secondary") as Style;
    }

    private string Chosen => preferences?.Language is { } code ? Loc.Find(code).Code : Loc.Current.Code;

    private void Open()
    {
        list.Children.Clear();
        foreach (var language in Loc.All)
        {
            bool picked = language.Code == Chosen;
            var row = new Button
            {
                Content = (picked ? "✓  " : "     ") + language.Name, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(12, 8, 12, 8), Margin = new(0, 1, 0, 1), FontSize = 15,
                Foreground = Brushes.White, Background = picked ? new SolidColorBrush(Color.FromArgb(70, 80, 140, 255)) : Brushes.Transparent, BorderThickness = new(0), Cursor = Cursors.Hand,
                Template = RowTemplate(),
            };
            var chosen = language;
            row.Click += (_, _) => Choose(chosen);
            list.Children.Add(row);
        }
        ShowNote();
        popup.IsOpen = true;
    }

    private void Choose(AppLanguage language)
    {
        try { preferences?.SetLanguage(language.Code); }
        catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException) { note.Text = "Couldn't save this setting: " + error.Message; return; }
        Open();
    }

    private void ShowNote()
    {
        bool pending = Chosen != Loc.Current.Code;
        note.Text = pending ? "Starts the next time X20CTL opens." : "";
        note.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        restart.Visibility = pending && !ReviewSandbox.Active ? Visibility.Visible : Visibility.Collapsed;
    }

    private static ControlTemplate ButtonTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "face"; factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(23)); factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(215, 14, 22, 44)));
        factory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(140, 150, 180, 240)));
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = factory };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(240, 36, 56, 104)), "face"));
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(150, 190, 255)), "face"));
        template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(150, 190, 255)), "face"));
        template.Triggers.Add(focus);
        return template;
    }

    private static ControlTemplate RowTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Border)); factory.Name = "row"; factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left); content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = factory };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(90, 110, 160, 255)), "row"));
        template.Triggers.Add(hover);
        return template;
    }

    /// <summary>The translate mark: a character, an A, and two arrows turning round each other.</summary>
    private static UIElement Mark()
    {
        var ink = new SolidColorBrush(Color.FromRgb(236, 242, 255));
        var canvas = new Canvas { Width = 28, Height = 28 };
        canvas.Children.Add(new TextBlock { Text = "文", FontSize = 12.5, Foreground = ink, FontFamily = new FontFamily("Yu Gothic UI, Microsoft YaHei UI, Segoe UI") }.At(1, 10.5));
        canvas.Children.Add(new TextBlock { Text = "A", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = ink, FontFamily = new FontFamily("Segoe UI") }.At(15, 0));
        Path Arc(string data) => new() { Data = Geometry.Parse(data), Stroke = ink, StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        canvas.Children.Add(Arc("M4,9 C6,3 11,1.5 15,2.5 M12.5,0.8 L15.3,2.6 L12.8,5"));
        canvas.Children.Add(Arc("M24.5,19 C22.5,25 17.5,26.5 13.5,25.5 M16,27.2 L13.2,25.4 L15.7,23"));
        return canvas;
    }
}

internal static class LanguageMenuExtensions
{
    public static T At<T>(this T element, double left, double top) where T : UIElement { Canvas.SetLeft(element, left); Canvas.SetTop(element, top); return element; }
}
