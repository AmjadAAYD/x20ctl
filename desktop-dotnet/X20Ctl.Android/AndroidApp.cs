using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using X20Ctl.Linux;

namespace X20Ctl.Droid;

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<PhoneApp>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // before the first screen: the bundled data unpacked, and the phone's own sound and controller input plugged in
        AppContent.Root = ContentUnpacker.Unpack(this);
        Phone.Active = true;
        IntroSound.Player = PhoneSound.Play;
        GamepadReader.External = PadInput.Pad;
        ExternalLinks.Opener = OpenLink;
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }

    void OpenLink(string url)
    {
        try
        {
            var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView, global::Android.Net.Uri.Parse(url));
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            StartActivity(intent);
        }
        catch (global::Android.Content.ActivityNotFoundException) { }
    }
}

[Activity(Label = "X20CTL", Theme = "@style/X20Theme", Icon = "@drawable/icon", MainLauncher = true, ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Immersive();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) Immersive();
    }

    // full screen like a console app: the status and navigation bars come back with a swipe from the edge
    void Immersive()
    {
        if (Window is not { } window) return;
        var insets = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        insets.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
        insets.SystemBarsBehavior = AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
    }

    // a paired controller's buttons and sticks go to the Studio's Tester, Diagnostics and live previews; they are
    // still passed on, so Android's own handling (and Avalonia's) is unchanged
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is not null) PadInput.Key(e);
        return base.DispatchKeyEvent(e);
    }

    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e is not null && PadInput.Motion(e)) return true;
        return base.DispatchGenericMotionEvent(e);
    }
}
