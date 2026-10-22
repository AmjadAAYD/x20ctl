using Android.App;
using Android.Content.PM;
using Android.Views;

namespace X20Ctl.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public class MainActivity : MauiAppCompatActivity
{
    // Gamepad input reaches the activity before any view. While the input tester is open it is measured and kept,
    // so B doesn't also act as Back; elsewhere it passes through and works as normal navigation.
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is not null && Gamepad.Handle(e)) return true;
        return base.DispatchKeyEvent(e);
    }

    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e is not null && Gamepad.Handle(e)) return true;
        return base.DispatchGenericMotionEvent(e);
    }
}
