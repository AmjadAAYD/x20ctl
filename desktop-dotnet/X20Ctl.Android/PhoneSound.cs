using Android.Media;

namespace X20Ctl.Droid;

/// <summary>Plays the intro film's sound with Android's own player (the desktop hands the WAV to pw-play or aplay).</summary>
internal static class PhoneSound
{
    public static IDisposable? Play(string file)
    {
        var player = new MediaPlayer();
        try
        {
            player.SetDataSource(file);
            player.Prepare();
            player.Start();
            return new Playing(player);
        }
        catch (Exception e) when (e is Java.Lang.IllegalStateException or Java.IO.IOException or Java.Lang.IllegalArgumentException)
        {
            player.Release();
            return null;
        }
    }

    sealed class Playing(MediaPlayer player) : IDisposable
    {
        bool done;
        public void Dispose()
        {
            if (done) return;
            done = true;
            try { if (player.IsPlaying) player.Stop(); } catch (Java.Lang.IllegalStateException) { }
            player.Release();
        }
    }
}
