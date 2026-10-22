namespace X20Ctl.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder().UseMauiApp<App>().Build();
}

public sealed class App : Application
{
    public App() { UserAppTheme = AppTheme.Dark; }

    protected override Window CreateWindow(IActivationState? activationState) => new(Ui.Styled(new Shell
    {
        FlyoutBehavior = FlyoutBehavior.Disabled,
        BackgroundColor = Ui.Background,
        Items =
        {
            new TabBar
            {
                Items =
                {
                    Ui.Tab("Home", new HomePage()),
                    Ui.Tab("Input", new InputTesterPage()),
                    Ui.Tab("Bluetooth", new BluetoothPage()),
                    Ui.Tab("Write scan", new WriteScanPage()),
                },
            },
        },
    }));
}
