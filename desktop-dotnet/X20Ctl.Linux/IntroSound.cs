using System.Diagnostics;
namespace X20Ctl.Linux;

/// <summary>The opening film's sound (Brand/intro-film.wav, the Windows film's own track), handed to whichever player the
/// system already has: pw-play, paplay, aplay or afplay. No audio library is bundled. Silent when none is found, and always
/// stoppable. Starting part-way (unmuting mid-film) plays a copy cut at that moment, since none of them can seek.</summary>
internal sealed class IntroSound : IDisposable
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Brand", "intro-film.wav");
    Process? process; string? cut;

    public void Start(double from = 0)
    {
        Stop();
        string file = FilePath; if (!File.Exists(file)) return;
        if (from > .05) { cut = Cut(file, from); if (cut == null) return; file = cut; }
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

    /// <summary>A copy of the WAV from <paramref name="from"/> seconds on, in the temp folder; null if it can't be read or is over by then.</summary>
    static string? Cut(string file, double from)
    {
        try
        {
            byte[] wav = File.ReadAllBytes(file);
            string Id(int at) => System.Text.Encoding.ASCII.GetString(wav, at, 4);
            if (wav.Length < 12 || Id(0) != "RIFF" || Id(8) != "WAVE") return null;
            int fmt = -1, fmtLength = 0, data = -1, dataLength = 0;
            for (int at = 12; at + 8 <= wav.Length;)
            {
                int length = BitConverter.ToInt32(wav, at + 4); if (length < 0) return null;
                if (Id(at) == "fmt ") { fmt = at + 8; fmtLength = length; }
                else if (Id(at) == "data") { data = at + 8; dataLength = Math.Min(length, wav.Length - data); break; }
                at += 8 + length + (length & 1);
            }
            if (fmt < 0 || data < 0 || fmtLength < 16 || fmt + fmtLength > wav.Length) return null;
            int byteRate = BitConverter.ToInt32(wav, fmt + 8), align = BitConverter.ToInt16(wav, fmt + 12); if (byteRate <= 0 || align <= 0) return null;
            int skip = (int)Math.Min(dataLength, (long)(from * byteRate) / align * align), keep = dataLength - skip; if (keep <= 0) return null;
            string path = Path.Combine(Path.GetTempPath(), $"x20ctl-intro-{Environment.ProcessId}.wav");
            using var output = new BinaryWriter(File.Create(path));
            output.Write("RIFF"u8); output.Write(4 + 8 + fmtLength + (fmtLength & 1) + 8 + keep); output.Write("WAVE"u8);
            output.Write("fmt "u8); output.Write(fmtLength); output.Write(wav, fmt, fmtLength); if ((fmtLength & 1) == 1) output.Write((byte)0);
            output.Write("data"u8); output.Write(keep); output.Write(wav, data + skip, keep);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Stop()
    {
        try { if (process is { HasExited: false }) process.Kill(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        process = null;
        if (cut != null) { try { File.Delete(cut); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } cut = null; }
    }
    public void Dispose() => Stop();
}
