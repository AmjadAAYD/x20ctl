using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;
namespace X20Ctl.Zone.Tests;

public class UpdateKitTests
{
    static UpdateAsset Asset(string name, long size = 3) => new(name, UpdateKit.TrustedPrefix + "v0.099/" + name, size);
    static readonly byte[] Bytes = [1, 2, 3];
    static string Hash => Convert.ToHexString(SHA256.HashData(Bytes));
    static UpdatePackage Package(long size = 3) => new(Asset("X20CTL-windows-x64.exe", size), Asset("SHA256SUMS.txt"));

    [Theory]
    [InlineData(InstallKind.WindowsExe, "X20CTL-windows-x64.exe")]
    [InlineData(InstallKind.LinuxAppImage, "X20CTL-linux-x86_64.AppImage")]
    [InlineData(InstallKind.WindowsFolder, null)]
    [InlineData(InstallKind.LinuxFlatpak, "X20CTL-linux-x86_64.flatpak")]
    [InlineData(InstallKind.LinuxOther, null)]
    public void Picks_only_the_install_asset(InstallKind kind, string? expected)
    {
        var names = new[] { "X20CTL-setup.exe", "X20CTL-windows-x64.zip", "X20CTL-linux-x86_64.AppImage.zsync", "X20CTL-linux-x64.tar.gz", "X20CTL-windows-x64.exe", "X20CTL-linux-x86_64.AppImage", "X20CTL-linux-x86_64.flatpak", "SHA256SUMS.txt" };
        Assert.Equal(expected, UpdateKit.Pick(kind, names.Select(n => Asset(n)))?.File.Name);
    }
    [Fact]
    public void Requires_checksums_and_trusted_urls()
    {
        var p = Package();
        Assert.Null(UpdateKit.Pick(InstallKind.WindowsExe, [p.File]));
        Assert.Null(UpdateKit.Pick(InstallKind.WindowsExe, [p.File with { Url = "https://example.com/a.exe" }, p.Sums]));
        Assert.Null(UpdateKit.Pick(InstallKind.WindowsExe, [p.File, p.Sums with { Url = "https://example.com/sums" }]));
    }
    [Fact]
    public void Labels_describe_the_state()
    {
        Assert.Equal("Update to version V0.099", UpdateKit.Label("V0.099", false));
        Assert.Equal("Downloading V0.099 · 42%", UpdateKit.Label("V0.099", true, .42));
        Assert.Equal("Update failed · open release page", UpdateKit.Label("V0.099", false, failed: true));
        Assert.Equal("Downloading V0.099 · 100%", UpdateKit.Label("V0.099", true, 2));
    }
    [Theory]
    [InlineData("ok", UpdateKit.Outcome.Verified)]
    [InlineData("hash", UpdateKit.Outcome.Failed)]
    [InlineData("size", UpdateKit.Outcome.Failed)]
    [InlineData("short", UpdateKit.Outcome.Failed)]
    [InlineData("missing", UpdateKit.Outcome.Failed)]
    [InlineData("404", UpdateKit.Outcome.Failed)]
    public async Task Download_validates_before_install_and_cleans_failure(string mode, UpdateKit.Outcome outcome)
    {
        string folder = Path.Combine(Path.GetTempPath(), "x20ctl-update-test-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        string running = Path.Combine(folder, "X20CTL.exe"), download = running + ".download";
        await File.WriteAllTextAsync(running, "running executable");
        try
        {
            using var handler = new Handler(mode);
            var progress = new Capture();
            var result = await UpdateKit.Fetch(Package(mode == "size" ? 4 : 3), download, progress, CancellationToken.None, handler);
            Assert.Equal(outcome, result.Outcome);
            Assert.Equal("running executable", await File.ReadAllTextAsync(running));
            Assert.Equal(outcome == UpdateKit.Outcome.Verified, File.Exists(download));
            if (outcome == UpdateKit.Outcome.Verified) { Assert.Equal(Bytes, await File.ReadAllBytesAsync(download)); Assert.Contains(1d, progress.Values); }
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public async Task Cancel_mid_stream_removes_partial_download()
    {
        string path = Path.Combine(Path.GetTempPath(), "x20ctl-" + Guid.NewGuid() + ".download");
        using var cancel = new CancellationTokenSource(); using var handler = new Handler("cancel");
        try
        {
            var fetch = UpdateKit.Fetch(Package(), path, null, cancel.Token, handler);
            await handler.Stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancel.Cancel();
            Assert.Equal(UpdateKit.Outcome.Cancelled, (await fetch.WaitAsync(TimeSpan.FromSeconds(5))).Outcome);
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void Flatpak_requires_bundle_sums_and_trusted_urls()
    {
        var f = Asset("X20CTL-linux-x86_64.flatpak"); var sums = Asset("SHA256SUMS.txt");
        Assert.NotNull(UpdateKit.Pick(InstallKind.LinuxFlatpak, [f, sums]));
        Assert.Null(UpdateKit.Pick(InstallKind.LinuxFlatpak, [sums]));
        Assert.Null(UpdateKit.Pick(InstallKind.LinuxFlatpak, [f]));
        Assert.Null(UpdateKit.Pick(InstallKind.LinuxFlatpak, [f with { Url = "https://example.com/a.flatpak" }, sums]));
        Assert.Null(UpdateKit.Pick(InstallKind.LinuxFlatpak, [f, sums with { Url = "https://example.com/sums" }]));
        Assert.Null(UpdateKit.Pick(InstallKind.LinuxFlatpak, [Asset("X20CTL-linux-aarch64.flatpak"), sums]));
    }
    sealed class Capture : IProgress<double> { public List<double> Values = []; public void Report(double value) => Values.Add(value); }
    sealed class Handler(string mode) : HttpMessageHandler
    {
        public BlockingStream Stream = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent((mode == "hash" ? new string('0', 64) : Hash) + "  " + (mode == "missing" ? "other.exe" : Package().File.Name)) });
            var content = mode == "cancel" ? (HttpContent)new StreamContent(Stream) : new ByteArrayContent(mode == "short" ? [1, 2] : Bytes);
            if (mode == "short") content.Headers.ContentLength = 3;
            return Task.FromResult(new HttpResponseMessage(mode == "404" ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = content });
        }
    }
    sealed class BlockingStream : MemoryStream
    {
        bool sent;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (!sent) { buffer.Span[0] = 1; sent = true; return 1; }
            Started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return 0;
        }
    }
}
