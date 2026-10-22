namespace X20Ctl.Mobile;

/// <summary>The phone app's few shared colours and building blocks. Dark, restrained, like the desktop studio.</summary>
public static class Ui
{
    public static readonly Color Background = Color.FromArgb("#0B0D12");
    public static readonly Color Surface = Color.FromArgb("#151922");
    public static readonly Color Line = Color.FromArgb("#262C38");
    public static readonly Color Text = Color.FromArgb("#E8ECF3");
    public static readonly Color Muted = Color.FromArgb("#8D96A8");
    public static readonly Color Accent = Color.FromArgb("#6FA8FF");
    public static readonly Color Good = Color.FromArgb("#5FD18B");
    public static readonly Color Locked = Color.FromArgb("#E0A84F");

    public static ShellContent Tab(string title, ContentPage page) => new() { Title = title, Content = page, Route = title.Replace(" ", "").ToLowerInvariant() };

    public static Shell Styled(Shell shell)
    {
        Shell.SetBackgroundColor(shell, Background);
        Shell.SetForegroundColor(shell, Text);
        Shell.SetTitleColor(shell, Text);
        Shell.SetTabBarBackgroundColor(shell, Surface);
        Shell.SetTabBarForegroundColor(shell, Accent);
        Shell.SetTabBarTitleColor(shell, Accent);
        Shell.SetTabBarUnselectedColor(shell, Muted);
        return shell;
    }

    public static ContentPage Page(ContentPage page, string title, View content)
    {
        page.Title = title;
        page.BackgroundColor = Background;
        page.Content = new ScrollView { Content = content };
        return page;
    }

    public static VerticalStackLayout Column(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 16, 16, 32) };
        foreach (var child in children) stack.Add(child);
        return stack;
    }

    public static Label Heading(string text) => new() { Text = text, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Text };
    public static Label Body(string text = "") => new() { Text = text, FontSize = 15, TextColor = Text, LineBreakMode = LineBreakMode.WordWrap };
    public static Label Note(string text = "") => new() { Text = text, FontSize = 13, TextColor = Muted, LineBreakMode = LineBreakMode.WordWrap };
    public static Label Mono(string text = "") => new() { Text = text, FontSize = 12.5, FontFamily = "monospace", TextColor = Text, LineBreakMode = LineBreakMode.WordWrap };

    public static Button Action(string text, EventHandler onClick, bool primary = true)
    {
        var button = new Button { Text = text, CornerRadius = 10, FontSize = 15, Padding = new Thickness(16, 10),
            BackgroundColor = primary ? Accent : Surface, TextColor = primary ? Background : Text, BorderColor = Line, BorderWidth = primary ? 0 : 1 };
        button.Clicked += onClick;
        return button;
    }

    public static Border Card(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        foreach (var child in children) stack.Add(child);
        return new Border { Content = stack, BackgroundColor = Surface, Stroke = Line, StrokeThickness = 1, Padding = 14,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
    }

    public static Entry Field(string placeholder) => new() { Placeholder = placeholder, TextColor = Text, PlaceholderColor = Muted, BackgroundColor = Surface, FontSize = 15 };

    /// <summary>Saves text to the app's own folder and offers Android's share sheet. Nothing leaves the phone unless the owner picks a target.</summary>
    public static async Task ShareText(string fileName, string text)
    {
        var path = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
        await File.WriteAllTextAsync(path, text);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = fileName, File = new ShareFile(path, "text/plain") });
    }
}
