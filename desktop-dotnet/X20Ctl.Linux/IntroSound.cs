using System.Diagnostics;
namespace X20Ctl.Linux;

/// <summary>The intro's score (Brand/intro.wav), handed to whichever player the system already has: pw-play, paplay, aplay or afplay. No audio library is bundled.
/// Silent when none is found, and always stoppable.</summary>
internal sealed class IntroSound : IDisposable
{
    Process? process;
    public bool Playing => process is { HasExited: false };

    public void Start()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Brand", "intro.wav"); if (!File.Exists(file)) return;
        foreach (string player in new[] { "pw-play", "paplay", "aplay", "afplay" })
        {
            try
            {
                process = Process.Start(new ProcessStartInfo(player, player == "aplay" ? $"-q \"{file}\"" : $"\"{file}\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
                if (process != null) return;
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    public void Stop() { try { if (process is { HasExited: false }) process.Kill(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { } process = null; }
    public void Dispose() => Stop();
}
