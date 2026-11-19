using System.Diagnostics;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Per-user bundle updates. Host permissions and system deployments can fail safely to the release page.</summary>
internal static class FlatpakUpdater
{
    public const string AppId = "io.github.AmjadAAYD.x20ctl";
    static ProcessStartInfo Host(params string[] args)
    {
        var command = new ProcessStartInfo("flatpak-spawn") { UseShellExecute = false };
        command.ArgumentList.Add("--host"); command.ArgumentList.Add("flatpak");
        foreach (var arg in args) command.ArgumentList.Add(arg);
        return command;
    }
    public static ProcessStartInfo InstallCommand(string bundle)
    {
        if (!bundle.StartsWith('/') || !bundle.EndsWith(".flatpak", StringComparison.Ordinal))
            throw new ArgumentException("An absolute host-visible bundle path is required.", nameof(bundle));
        return Host("install", "--user", "--noninteractive", "--or-update", "--bundle", bundle);
    }
    public static ProcessStartInfo RestartCommand() => Host("run", "--user", "--arch=x86_64", "--branch=stable", AppId);
    public static bool SameDeployment(string info, string userLocation)
    {
        bool instance = false;
        foreach (var raw in info.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('[')) instance = line == "[Instance]";
            if (instance && line.StartsWith("app-path=", StringComparison.Ordinal))
                return userLocation.Trim().StartsWith('/') && (line[9..].TrimEnd('/') == userLocation.Trim().TrimEnd('/') || line[9..].TrimEnd('/') == userLocation.Trim().TrimEnd('/') + "/files");
        }
        return false;
    }
    static async Task<int> Execute(ProcessStartInfo command)
    {
        // Host transactions are not killed by the download cancel button.
        using var process = Process.Start(command) ?? throw new IOException("Host command did not start.");
        await process.WaitForExitAsync(); return process.ExitCode;
    }
    public static async Task<(bool Done, string Message)> InstallVerified(string bundle, Func<ProcessStartInfo, Task<int>>? execute = null)
    {
        try
        {
            int exit = await (execute ?? Execute)(InstallCommand(bundle));
            return exit == 0 ? (true, "Update installed. Restart to use it.") : (false, $"Flatpak install failed ({exit}). Open the release page.");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        { return (false, "Host installation unavailable. Open the release page. " + e.Message); }
    }
    public static async Task<(bool Done, string Message)> Install(UpdatePackage? package, IProgress<double>? progress, CancellationToken cancel, Action? installing)
    {
        if (package == null) return (false, "No verified Flatpak bundle is available. Open the release page.");
        string? download = null;
        try
        {
            var location = Host("info", "--user", "--show-location", "--arch=x86_64", AppId + "//stable");
            location.RedirectStandardOutput = true;
            using var check = Process.Start(location) ?? throw new IOException("Host command did not start.");
            string path = await check.StandardOutput.ReadToEndAsync(cancel);
            await check.WaitForExitAsync(cancel);
            if (check.ExitCode != 0 || !SameDeployment(await File.ReadAllTextAsync("/.flatpak-info", cancel), path))
                return (false, "This deployment is not the per-user installation. Open the release page.");
            // Flatpak maps XDG_CACHE_HOME to ~/.var/app/APP_ID/cache, visible at the same path on the host.
            // /tmp is private to the sandbox and cannot be passed to a host process.
            string? cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (cache == null || !cache.StartsWith('/')) return (false, "No host-visible app cache. Open the release page.");
            Directory.CreateDirectory(cache);
            download = System.IO.Path.Combine(cache, "update-" + Guid.NewGuid().ToString("N") + ".flatpak");
            var fetched = await UpdateKit.Fetch(package, download, progress, cancel);
            if (fetched.Outcome != UpdateKit.Outcome.Verified) return (false, fetched.Message);
            cancel.ThrowIfCancellationRequested();
            installing?.Invoke();
            return await InstallVerified(download);
        }
        catch (OperationCanceledException) { return (false, "Update cancelled."); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        { return (false, "Flatpak update unavailable. Open the release page. " + e.Message); }
        finally { if (download != null) { try { File.Delete(download); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
    }
    public static async Task<bool> Restart()
    {
        try
        {
            var process = Process.Start(RestartCommand());
            if (process == null) return false;
            using (process)
            {
                // Catch immediate permission/launch errors before closing the current copy.
                var exited = process.WaitForExitAsync();
                return await Task.WhenAny(exited, Task.Delay(1000)) != exited || process.ExitCode == 0;
            }
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }
}
