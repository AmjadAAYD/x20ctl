using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>Sends one piece of feedback to the X20CTLADMIN inbox (POST /api/feedback, JSON), the same private site that
/// receives Controller Check reports. Only what the player typed plus the app and Windows versions are sent, and only when
/// they press Send.</summary>
public static class FeedbackClient
{
    public const string Endpoint = "https://x20-admin.vercel.app/api/feedback";
    public static readonly (string Id, string Label)[] Categories = [("bug", "Something's broken"), ("feature", "Idea or request"), ("compatibility", "My controller"), ("other", "Something else")];

    /// <summary>Posts the feedback and returns its FB-… receipt. Throws with a plain reason when the inbox refuses it.</summary>
    public static async Task<string> Send(string category, string message, string? controller, string? connection)
    {
        if (ReviewSandbox.Active) return "FB-SANDBOX";
        var body = JsonSerializer.Serialize(new
        {
            clientSubmissionId = Guid.NewGuid().ToString(), category, message = message.Trim(), appVersion = Scanner.ScanFlow.AppVersion,
            clientTimestamp = DateTime.UtcNow.ToString("o"), controllerName = string.IsNullOrWhiteSpace(controller) ? null : controller,
            osVersion = Environment.OSVersion.VersionString, connectionType = string.IsNullOrWhiteSpace(connection) ? null : connection, website = ""
        });
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await http.PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "application/json"));
        string text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text.Length == 0 ? "{}" : text);
        if ((int)response.StatusCode is not (200 or 201))
            throw new HttpRequestException(doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : $"The inbox answered HTTP {(int)response.StatusCode}.");
        string id = doc.RootElement.TryGetProperty("submissionId", out var s) ? s.GetString() ?? "" : "";
        if (!Regex.IsMatch(id, "^FB-[0-9A-F]{24}$")) throw new InvalidOperationException("The inbox didn't return a receipt.");
        return id;
    }
}

/// <summary>Tools → Send feedback: pick what it's about, write it, send it to X20CTL's private inbox.</summary>
public sealed class FeedbackPage : Border
{
    public Button SendButton { get; }
    public TextBox Message { get; }
    private readonly TextBlock status, counter;
    private readonly ComboBox controller = new() { MinHeight = 42, FontSize = 15, Width = 300 }, connection = new() { MinHeight = 42, FontSize = 15, Width = 220 };
    private const string NotChosen = "Not chosen";
    private string category = "bug";
    private bool sending;

    public FeedbackPage(Button back, IEnumerable<string> controllers)
    {
        CornerRadius = new(22); BorderThickness = new(1); Padding = new(48, 36, 48, 36); MaxWidth = 1100; HorizontalAlignment = HorizontalAlignment.Left; VerticalAlignment = VerticalAlignment.Top;
        Background = new LinearGradientBrush(Color.FromArgb(215, 14, 24, 52), Color.FromArgb(200, 8, 14, 34), 90);
        BorderBrush = new LinearGradientBrush(Color.FromArgb(110, 120, 170, 255), Color.FromArgb(30, 120, 170, 255), 90);
        var stack = new StackPanel(); Child = stack;
        stack.Children.Add(Kit.Text("Send feedback", 36, "DS.Text", FontWeights.Bold));
        stack.Children.Add(Kit.Text("Tell X20CTL what's broken, what you'd like, or how your controller behaves. It goes straight to the X20CTL inbox.", 16.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 8, 0, 0); }));

        stack.Children.Add(Label("WHAT IS IT ABOUT?", 26));
        var kinds = new UniformGrid { Rows = 1, HorizontalAlignment = HorizontalAlignment.Left }; string group = "fb" + Guid.NewGuid().ToString("N");
        foreach (var (id, label) in FeedbackClient.Categories)
        {
            var r = new RadioButton { Content = label, Style = DS.Style("DS.Segment"), GroupName = group, IsChecked = id == category, MinWidth = 190 };
            r.Checked += (_, _) => category = id; kinds.Children.Add(r);
        }
        stack.Children.Add(new Border { CornerRadius = new(12), Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderThickness = new(1), Padding = new(1), HorizontalAlignment = HorizontalAlignment.Left, Child = kinds });

        controller.Items.Add(NotChosen); foreach (var c in controllers) controller.Items.Add(c); controller.SelectedIndex = 0;
        foreach (var c in new[] { NotChosen, "USB cable", "2.4G receiver", "Bluetooth" }) connection.Items.Add(c); connection.SelectedIndex = 0;
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 22, 0, 0), Children = {
            new StackPanel { Children = { Label("CONTROLLER (OPTIONAL)", 0), controller } },
            new StackPanel { Margin = new(18, 0, 0, 0), Children = { Label("CONNECTED BY (OPTIONAL)", 0), connection } } } });

        stack.Children.Add(Label("YOUR MESSAGE", 22));
        Message = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 170, MaxHeight = 260, MaxLength = 4000, FontSize = 15.5, VerticalContentAlignment = VerticalAlignment.Top, Padding = new(14, 12, 14, 12), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Foreground = DS.Brush("DS.Text"), CaretBrush = DS.Brush("DS.Text"), Background = new SolidColorBrush(Color.FromArgb(120, 4, 8, 20)), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 140, 180, 255)), BorderThickness = new(1) };
        stack.Children.Add(Message);
        counter = Kit.Text("0 / 4000", 12.5, "DS.TextMuted"); counter.HorizontalAlignment = HorizontalAlignment.Right; counter.Margin = new(0, 6, 0, 0); stack.Children.Add(counter);

        SendButton = new Button { Style = DS.Style("DS.ActionPrimary"), Height = 46, MinHeight = 0, Padding = new(24, 0, 26, 0), IsEnabled = false,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon("", 15, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text("Send to X20CTL", 15.5, "DS.Text", FontWeights.SemiBold) } } };
        SendButton.Click += async (_, _) => await Submit();
        Message.TextChanged += (_, _) => { counter.Text = $"{Message.Text.Length} / 4000"; SendButton.IsEnabled = !sending && Message.Text.Trim().Length >= 3; };
        back.Margin = new(0, 0, 12, 0);
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 18, 0, 0), Children = { back, SendButton } });
        status = Kit.Text("", 15, "DS.TextSoft"); status.TextWrapping = TextWrapping.Wrap; status.Margin = new(0, 16, 0, 0); stack.Children.Add(status);
        stack.Children.Add(Kit.Text("What's sent: your message, what it's about, the controller and connection if you pick them, the X20CTL version and the Windows version. No account, files or personal details.", 13, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 18, 0, 0); }));
    }

    private static TextBlock Label(string text, double top) => Kit.Text(text, 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, top, 0, 8));

    public async Task Submit()
    {
        if (sending || Message.Text.Trim().Length < 3) return;
        sending = true; SendButton.IsEnabled = false; status.Foreground = DS.Brush("DS.TextSoft"); status.Text = "Sending…";
        try
        {
            string receipt = await FeedbackClient.Send(category, Message.Text, controller.SelectedItem as string is NotChosen or null ? null : (string)controller.SelectedItem, connection.SelectedItem as string is NotChosen or null ? null : (string)connection.SelectedItem);
            status.Foreground = DS.Brush("DS.Success"); status.Text = $"Sent. Thank you! Your receipt is {receipt}.";
            Message.Text = "";
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            status.Foreground = DS.Brush("DS.Warn"); status.Text = "Not sent: " + error.Message + " Your message is still here; try again in a moment.";
        }
        finally { sending = false; SendButton.IsEnabled = Message.Text.Trim().Length >= 3; }
    }
}
