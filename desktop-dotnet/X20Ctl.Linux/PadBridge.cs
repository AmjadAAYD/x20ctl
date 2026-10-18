using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
namespace X20Ctl.Linux;

/// <summary>Reads the controller over Bluetooth by running the project's own Python client (the `x20` command, or `python3 -m x20ctl`) and showing what it reports.
/// Read-only: only `status` and `macro --read` are ever run, never anything that writes. Needs that client installed (pip install git+https://github.com/AmjadAAYD/x20ctl) and the pad paired in BlueZ.</summary>
internal static class PadBridge
{
    public sealed record Result(bool Ran, bool Ok, string Text);

    static readonly (string File, string Prefix)[] Launchers = [("x20", ""), ("python3", "-m x20ctl "), ("python", "-m x20ctl ")];
    static readonly Regex Ansi = new("\u001b\\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    /// <summary>The controller's report: identity, firmware, battery and what it exposes.</summary>
    public static Task<Result> Status() => Run("status");
    /// <summary>The macros stored on the pad, as the client prints them.</summary>
    public static Task<Result> Macros() => Run("macro --read");

    static async Task<Result> Run(string command)
    {
        foreach (var (file, prefix) in Launchers)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(file, prefix + command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 });
                if (p == null) continue;
                var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch (InvalidOperationException) { } return new(true, false, "The controller didn't answer within a minute. Is it awake and paired?"); }
                string text = Ansi.Replace(await output, "").Trim(), err = Ansi.Replace(await error, "").Trim();
                // python3 exists everywhere; a missing module is "not installed", not a failed read
                if (p.ExitCode != 0 && err.Contains("No module named", StringComparison.Ordinal)) continue;
                return new(true, p.ExitCode == 0, p.ExitCode == 0 ? text : (err.Length > 0 ? err : text));
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
        return new(false, false, "The X20CTL Bluetooth client isn't installed. Install it with: pip install git+https://github.com/AmjadAAYD/x20ctl");
    }
}
