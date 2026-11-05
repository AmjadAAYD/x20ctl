using System.Windows;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    /// <summary>Centre of a control on the front artwork as fractions of its width and height, from the real control regions.</summary>
    internal bool TryControlCenter(string key, out double fx, out double fy)
    {
        string alias = key switch { "Up" => "DPAD_UP", "Down" => "DPAD_DOWN", "Left" => "DPAD_LEFT", "Right" => "DPAD_RIGHT", "View" => "SELECT", "Menu" => "START", _ => key };
        var region = layout.Controls.FirstOrDefault(c => c.View == "front" && (c.Key == key || c.Key == alias));
        if (region == null) { fx = fy = 0; return false; }
        var a = region.LabelAnchor ?? new(region.Bounds.X + region.Bounds.Width / 2, region.Bounds.Y + region.Bounds.Height / 2);
        fx = a.X; fy = a.Y; return true;
    }

    /// <summary>Like TryControlCenter, for the tight-cropped upright art (Assets/art): fractions of that image, whose height is 739/1100 of its width.</summary>
    internal bool TryCroppedControlCenter(string key, out double fx, out double fy)
    {
        if (!TryControlCenter(key, out var nx, out var ny)) { fx = fy = 0; return false; }
        fx = (nx * 1536 - 78) / 1374.0; fy = (ny * 1024 - 42) / 923.0; return true;
    }

    private string[] ThemeMappingKeys()
    {
        var groups = AvailableGroups();
        return groups.FirstOrDefault(group => group.keys.Contains(SelectedControl)).keys ?? groups.FirstOrDefault().keys ?? Array.Empty<string>();
    }
    internal static Button ThemeAction(FrameworkElement face, string tag, Action action, bool enabled = true)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border)); border.Name = "FocusFace"; border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); border.AppendChild(presenter); template.VisualTree = border;
        var trigger = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BorderBrushProperty, DS.Brush("DS.AccentHi"), "FocusFace")); trigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "FocusFace")); template.Triggers.Add(trigger); template.Seal();
        var button = new Button { Content = face, Template = template, Tag = tag, IsEnabled = enabled, FocusVisualStyle = null, Padding = new(0), MinHeight = 0, Width = face.Width, Height = face.Height, Margin = face.Margin, HorizontalAlignment = face.HorizontalAlignment, VerticalAlignment = face.VerticalAlignment, Cursor = Cursors.Hand };
        face.Margin = new(0); face.HorizontalAlignment = HorizontalAlignment.Stretch; face.VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumn(button, Grid.GetColumn(face)); Grid.SetRow(button, Grid.GetRow(face)); Grid.SetColumnSpan(button, Grid.GetColumnSpan(face)); Grid.SetRowSpan(button, Grid.GetRowSpan(face));
        Canvas.SetLeft(button, Canvas.GetLeft(face)); Canvas.SetTop(button, Canvas.GetTop(face)); Canvas.SetRight(button, Canvas.GetRight(face)); Canvas.SetBottom(button, Canvas.GetBottom(face));
        AutomationProperties.SetName(button, tag.Replace(':', ' ')); button.Click += (_, _) => action(); return button;
    }
    private static Slider ThemeSlider(RangeBase source, string tag, double width, bool editable, Brush? trackBrush = null)
    {
        var slider = new Slider { Minimum = source.Minimum, Maximum = source.Maximum, TickFrequency = 1, IsSnapToTickEnabled = true, Width = width, Style = DS.Style("DS.Slider"), Tag = tag, IsEnabled = editable, VerticalAlignment = VerticalAlignment.Center };
        slider.SetBinding(RangeBase.ValueProperty, new Binding("Value") { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        if (trackBrush != null) slider.Loaded += (_, _) => { slider.ApplyTemplate(); if (slider.Template.FindName("TrackBg", slider) is Border bg) bg.Background = trackBrush; };
        return slider;
    }
    private void ThemeSelectInput(string key)
    {
        if (!OpenMacroSlot(key)) SelectControl(key);
    }
    private void ThemeOutputMenu(string key)
    {
        ThemeSelectInput(key);
        if (!Draft.CanEdit(key)) return;
        // Selection queues a themed page refresh. Attach the popup to the replacement
        // row after that refresh; its previous face is no longer in the visual tree.
        Dispatcher.BeginInvoke(() =>
        {
            if (!OnButtonsPage || SelectedControl != key || FindTagged(this, "theme:input:" + key) is not {} anchor) return;
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
            var targets = Draft.PcMode ? ButtonDraft.Targets.Append("NONE") : ButtonDraft.Targets;
            foreach (string target in targets)
            {
                var item = new MenuItem { Header = Label(target), IsCheckable = true, IsChecked = Draft.Target(key) == target, Tag = "theme:map:" + key + ":" + target };
                item.Click += (_, _) => { if (SelectedControl != key) SelectControl(key); SetDraftTarget(target); }; menu.Items.Add(item);
            }
            anchor.ContextMenu = menu; if (!ReviewSandbox.Active) menu.IsOpen = true;
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }
    private Button ThemeMappingAction(FrameworkElement face, string key)
    {
        var button = ThemeAction(face, "theme:input:" + key, () => ThemeOutputMenu(key));
        AutomationProperties.SetName(button, Label(key) + ", " + (Draft.CanEdit(key) ? "sends " + Label(Draft.Target(key)) : "read only"));
        return button;
    }
    private void ThemeInputMenu(FrameworkElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        foreach (var (name, keys) in AvailableGroups())
        {
            var group = new MenuItem { Header = name };
            foreach (string key in keys) { var item = new MenuItem { Header = Label(key), Tag = "theme:choose:" + key }; item.Click += (_, _) => ThemeSelectInput(key); group.Items.Add(item); }
            menu.Items.Add(group);
        }
        anchor.ContextMenu = menu; if (!ReviewSandbox.Active) menu.IsOpen = true;
    }
}
