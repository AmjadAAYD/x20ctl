using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Buttons and Device pages composed from the console kit at the concept art's size (1672x941).
/// Device values here are a labelled review fixture in the shape of the owner's X05 Pro capture.</summary>
internal static class DashboardReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string model in new[] { "x20", "x15", "x20_pro" })
        {
            await Render(Path.Combine(directory, $"buttons-{model}.png"), Shell(model, "Buttons", ButtonsBody(model), false));
            await Render(Path.Combine(directory, $"device-{model}.png"), Shell(model, "Device", DeviceBody(model, true), true));
        }
        await Render(Path.Combine(directory, "device-x20-offline.png"), Shell("x20", "Device", DeviceBody("x20", false), false));
    }

    static string Name(string m) => m switch { "x20" => "EasySMX X20", "x15" => "EasySMX X15", "x20_pro" => "EasySMX X20 Pro", _ => m };

    static Grid Shell(string model, string tab, UIElement body, bool connected)
    {
        var root = new Grid(); root.Children.Add(new GalaxyBackdrop(false));
        var frame = new Border { Margin = new(10), CornerRadius = new(22), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 140, 180, 255)), Background = new SolidColorBrush(Color.FromArgb(60, 4, 8, 20)) };
        root.Children.Add(frame);
        var page = new Grid { Margin = new(26, 4, 26, 18) }; frame.Child = page;
        page.RowDefinitions.Add(new() { Height = GridLength.Auto }); page.RowDefinitions.Add(new() { Height = GridLength.Auto }); page.RowDefinitions.Add(new());
        page.Children.Add(Kit.TitleBar(Name(model), connected ? new StatusChip("Controller detected", ChipKind.Success) : new StatusChip("Controller offline", ChipKind.Warn)));
        var tabs = Kit.Tabs(tab, "Device", "Buttons", "Curves", "Macros", "Vibration", "Setups"); tabs.Margin = new(0, 2, 0, 14); Grid.SetRow(tabs, 1); page.Children.Add(tabs);
        Grid.SetRow(body, 2); page.Children.Add(body);
        return root;
    }

    static Grid TwoPanels(UIElement left, UIElement right, double leftWeight = 1.12)
    {
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new(leftWeight, GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new(16) }); body.ColumnDefinitions.Add(new());
        body.Children.Add(left); Grid.SetColumn(right, 2); body.Children.Add(right); return body;
    }

    static Grid ButtonsBody(string model)
    {
        var art = new PhotoController(model); art.Select("DPAD_UP"); art.Live.Smooth = false; art.Live.SetInput(-.36, .62, .48, -.18, .72, .28);
        var left = Kit.Panel("Button Visualizer", "Select a control on the controller to remap it.", "Front view", new Viewbox { Child = art });

        var r = new StackPanel();
        r.Children.Add(Kit.Section("Control group", 0)); r.Children.Add(Kit.Segmented("D-pad", "Face", "D-pad", "Shoulders", "Sticks", "System"));
        r.Children.Add(Kit.Section("Map D-pad Up to")); r.Children.Add(Kit.Options((1, "Default", "Keep D-pad Up", false), (2, "Map to A", "Face button", true), (3, "Map to B", "Face button", false)));
        r.Children.Add(Kit.Section("Assignment"));
        r.Children.Add(Kit.Field("Physical control", "", "D-pad Up", ""));
        r.Children.Add(Kit.Field("Draft output", "", "A · local draft", "", "DS.AccentHi"));
        r.Children.Add(Kit.Field("Hardware state", "", "Not read · controller offline", "", "DS.Warn"));
        r.Children.Add(Kit.Field("Write support", "", model == "x20" ? "Known protocol · locked until verified" : "Not verified for this model", ""));
        var top = TwoPanels(left, Kit.Panel(null, null, null, Kit.Stack(r, Kit.Actions("", "Try it", "", "All assignments"))));
        // outputs shelf across the full width underneath, so the screen is filled edge to edge
        var page = new Grid(); page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = new(14) }); page.RowDefinitions.Add(new() { Height = GridLength.Auto });
        page.Children.Add(top); var shelf = Kit.Outputs("D-pad Up", "A"); Grid.SetRow(shelf, 2); page.Children.Add(shelf);
        return page;
    }

    static Grid DeviceBody(string model, bool connected)
    {
        var art = new PhotoController(model); art.Live.Smooth = false;
        var leftBody = new DockPanel();
        var hero = connected ? Kit.StatusHero("Controller Status", "Connected", "Gameplay input detected on XInput slot 0.") : Kit.StatusHero("Controller Status", "Not connected", "Connect the controller by USB, receiver or Bluetooth.", false);
        DockPanel.SetDock(hero, Dock.Bottom); leftBody.Children.Add(hero); leftBody.Children.Add(new Viewbox { Child = art });
        var left = Kit.Panel(connected ? "Connected Controller" : "Controller", connected ? "Your controller is connected and ready to test." : "Waiting for a controller.", connected ? "XInput · Slot 0" : null, leftBody);

        var r = new StackPanel();
        r.Children.Add(Kit.Section("Connection Method", 0));
        r.Children.Add(Kit.Choices("Wired USB", ("", "Wired USB"), ("", "2.4G Receiver"), ("", "Bluetooth"), ("", "Not sure")));
        r.Children.Add(Kit.Section("Detected Device"));
        r.Children.Add(Kit.Field("Physical device name", "", connected ? "Controller (Xbox Wireless controller)" : "—"));
        r.Children.Add(Kit.Field("Controller model", "", Name(model) + " · chosen by you", ""));
        r.Children.Add(Kit.Section("OS Device Interfaces"));
        r.Children.Add(Kit.Interfaces(("", "Primary XInput device", "Used by games and most applications", connected ? "Controller (Xbox Wireless controller)" : "Not present"), ("", "HID interface (optional)", "May expose extra reports", connected ? "HID-compliant game controller" : "Not present")));
        r.Children.Add(Kit.Section("Device Identifiers"));
        var ids = new UniformGrid { Rows = 1, Margin = new(0, 0, -10, 0) };
        var usb = Kit.Identifiers("", "USB device (wired)", ("VID", connected ? "0x045E" : "—"), ("PID", connected ? "0x028E" : "—"), ("Version", "Not reported")); usb.Margin = new(0, 0, 10, 0);
        var rx = Kit.Identifiers("", "2.4G receiver (if used)", ("VID", "—"), ("PID", "—"), ("Version", "—")); rx.Margin = new(0, 0, 10, 0);
        ids.Children.Add(usb); ids.Children.Add(rx); r.Children.Add(ids);
        r.Children.Add(Kit.Section("Firmware Information"));
        r.Children.Add(Kit.Field("Controller firmware", "", "Not readable over XInput", ""));
        return TwoPanels(left, Kit.Panel(null, null, null, Kit.Stack(r, Kit.Note("Note:", "EasySMX controllers identify as Xbox controllers in Windows, so the model shown is the one you chose."), Kit.Actions("", "Start Scan", "", "Continue to Buttons"))));
    }

    static async Task Render(string path, Grid root)
    {
        var window = new Window { Width = 1672, Height = 941, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Content = root, ShowInTaskbar = false, Background = Brushes.Black };
        try
        {
            window.Show(); await Task.Delay(600); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(root);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var s = File.Create(path); enc.Save(s);
        }
        finally { window.Close(); }
    }
}
