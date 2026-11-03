using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

/// <summary>A mock update, end to end and windowless: the real Updater.Install, the real flow and view, and real preferences, against a fake GitHub
/// (an in-memory HTTP handler) and a stand-in exe in a temp folder. Nothing is downloaded from the internet, no release is involved, no window is
/// shown and the installed X20CTL is never touched. Returns the number of failed checks (the process exit code).</summary>
internal static class UpdateMockReview
{
    const string ExeName = "X20CTL-0.099-windows-x64.exe";

    sealed class FakeGitHub(byte[] exe, string sums, bool slow = false) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Requests++;
            if (request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sums) });
            if (request.RequestUri.AbsolutePath.EndsWith("missing.exe")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            Stream body = slow ? new StallingStream(exe) : new MemoryStream(exe);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) { Headers = { ContentLength = exe.Length } } });
        }
    }
    sealed class StallingStream(byte[] data) : MemoryStream(data)
    {   // hands out the first chunk, then waits until cancelled
        bool first = true;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            if (first) { first = false; return await base.ReadAsync(buffer.Length > 1000 ? buffer[..1000] : buffer, cancel); }
            await Task.Delay(Timeout.Infinite, cancel); return 0;
        }
    }

    static int failures; static readonly List<string> log = [];
    static void Check(string what, bool ok) { log.Add((ok ? "PASS  " : "FAIL  ") + what); if (!ok) failures++; }

    public static async Task<int> Run(string directory)
    {
        ReviewSandbox.Active = true; failures = 0; log.Clear();
        Directory.CreateDirectory(directory);
        string work = Path.Combine(Path.GetTempPath(), "x20ctl-update-mock-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        try
        {
            var oldBytes = Encoding.ASCII.GetBytes("OLD-X20CTL-V0.098 " + new string('a', 3000));
            var newBytes = new byte[250_000]; new Random(7).NextBytes(newBytes);
            string good = Hex(newBytes);
            Updater.Release Release(string exeUrl = "/x20ctl/releases/download/v0.099/" + ExeName, long size = -1) =>
                new("v0.099", "0.099", "X20CTL V0.099", "https://example.invalid/release", ExeName, UpdateKit.TrustedPrefix + "v0.099/" + ExeName, UpdateKit.TrustedPrefix + "v0.099/SHA256SUMS.txt", false, size < 0 ? newBytes.Length : size);
            string Target(string name) { string t = Path.Combine(work, name); File.WriteAllBytes(t, oldBytes); Updater.TestTarget = t; return t; }

            // 1. the happy path: ask, Yes, download with progress, verify, swap
            {
                string target = Target("a.exe"); Updater.TestHandler = new FakeGitHub(newBytes, $"{good}  {ExeName}\n");
                var flow = new UpdateFlow("V0.099", Updater.CanInstall); flow.Open();
                Check("clicking the notice asks first (Confirm), nothing downloaded yet", flow.Step == UpdateStep.Confirm && File.ReadAllBytes(target).SequenceEqual(oldBytes));
                Render(flow, directory, "1-confirm");
                flow.Yes(); var seen = new List<double>(); var progress = new SyncProgress(p => { seen.Add(p); flow.Report(p); });
                var (done, message) = await Updater.Install(Release(), progress, default);
                Check("install reports success", done);
                Check("progress rose to 100%", seen.Count > 1 && seen[^1] == 1.0 && seen.SequenceEqual(seen.OrderBy(x => x)));
                Render(flow, directory, "2-progress-end");
                Check("the stand-in exe was replaced by the new bytes", File.ReadAllBytes(target).SequenceEqual(newBytes));
                Check("the old exe was kept as .old for clean-up", File.Exists(target + ".old") && File.ReadAllBytes(target + ".old").SequenceEqual(oldBytes));
                Check("no .download file left", !File.Exists(target + ".download"));
                flow.Installed(); Check("flow moves to Restarting", flow.Step == UpdateStep.Restarting);
                Render(flow, directory, "3-restarting");
            }

            // 2. wrong checksum: nothing replaced, reason shown in-app
            {
                string target = Target("b.exe"); Updater.TestHandler = new FakeGitHub(newBytes, $"{new string('0', 64)}  {ExeName}\n");
                var flow = new UpdateFlow("V0.099", true); flow.Open(); flow.Yes();
                var (done, message) = await Updater.Install(Release(), null, default);
                Check("a checksum mismatch fails", !done && message.Contains("checksum"));
                Check("...and the current exe is byte-for-byte untouched, no .old, no .download", File.ReadAllBytes(target).SequenceEqual(oldBytes) && !File.Exists(target + ".old") && !File.Exists(target + ".download"));
                flow.Fail(message); Check("...the flow offers Try again", flow.Step == UpdateStep.Failed && flow.CanRetry && flow.Body.Contains("untouched") && !flow.Body.Contains("GitHub instead"));
                Render(flow, directory, "4-failed-checksum");
            }

            // 3. size differs from the release's listed size
            {
                string target = Target("c.exe"); Updater.TestHandler = new FakeGitHub(newBytes, $"{good}  {ExeName}\n");
                var (done, message) = await Updater.Install(Release(size: newBytes.Length + 123), null, default);
                Check("a size mismatch fails and leaves the exe untouched", !done && message.Contains("size") && File.ReadAllBytes(target).SequenceEqual(oldBytes) && !File.Exists(target + ".download"));
            }

            // 4. file missing from the checksum list
            {
                string target = Target("d.exe"); Updater.TestHandler = new FakeGitHub(newBytes, $"{good}  some-other-file.exe\n");
                var (done, message) = await Updater.Install(Release(), null, default);
                Check("no checksum entry for the exe: refused, exe untouched", !done && File.ReadAllBytes(target).SequenceEqual(oldBytes));
            }

            // 5. server error
            {
                string target = Target("e.exe"); Updater.TestHandler = new FakeGitHub(newBytes, $"{good}  {ExeName}\n");
                var r = Release() with { ExeUrl = UpdateKit.TrustedPrefix + "v0.099/missing.exe" };
                var (done, message) = await Updater.Install(r, null, default);
                Check("an HTTP 404 fails cleanly, exe untouched", !done && File.ReadAllBytes(target).SequenceEqual(oldBytes) && !File.Exists(target + ".download"));
            }

            // 6. cancel mid-download
            {
                string target = Target("f.exe"); Updater.TestHandler = new FakeGitHub(newBytes, $"{good}  {ExeName}\n", slow: true);
                var flow = new UpdateFlow("V0.099", true); flow.Open(); flow.Yes();
                using var cts = new CancellationTokenSource(400);
                var (done, message) = await Updater.Install(Release(), null, cts.Token);
                flow.Cancel();
                Check("cancel mid-download stops it, no success", !done && flow.Step == UpdateStep.Hidden);
                Check("...and the current exe is untouched, nothing left behind", File.ReadAllBytes(target).SequenceEqual(oldBytes) && !File.Exists(target + ".download") && !File.Exists(target + ".old"));
            }

            // 7. keys on the real view: A answers, B/Esc are No/Cancel/Close, nothing else moves
            {
                var flow = new UpdateFlow("V0.099", true); flow.Open();
                var view = new UpdateFlowView(flow); string events = "";
                view.Yes += () => { events += "Y"; flow.Yes(); }; view.No += () => { events += "N"; flow.No(); };
                Host(view);
                Check("Key.B on the question = No (no download started)", view.HandleKey(Key.B) && events == "N" && flow.Step == UpdateStep.Hidden);
                flow = new UpdateFlow("V0.099", true); flow.Open(); view = new UpdateFlowView(flow); events = "";
                view.Yes += () => { events += "Y"; flow.Yes(); }; view.No += () => { events += "N"; flow.No(); }; Host(view);
                Check("Key.Escape on the question = No", view.HandleKey(Key.Escape) && events == "N");
                flow = new UpdateFlow("V0.099", true); flow.Open(); view = new UpdateFlowView(flow); events = "";
                view.Yes += () => { events += "Y"; flow.Yes(); }; Host(view);
                Check("Key.A answers the first button, Yes", view.HandleKey(Key.A) && events == "Y" && flow.Step == UpdateStep.Downloading);
                bool cancelled = false; view.Cancel += () => { cancelled = true; flow.Cancel(); }; view.Render();
                Check("Key.B while downloading = Cancel", view.HandleKey(Key.B) && cancelled && flow.Step == UpdateStep.Hidden);
                Check("any other key is swallowed by the overlay", view.HandleKey(Key.F5));
            }

            // 8. What's new: only after a self-update, once, never again after "Never remind me again"
            {
                string prefs = Path.Combine(work, "prefs.json");
                var p0 = new ProductPreferences(prefs); p0.Load();
                Check("fresh install / normal launch: not shown", !p0.ShouldShowWhatsNew("0.099"));
                p0.SetWhatsNewPending("0.099");   // what the old exe writes before the swap
                var p1 = new ProductPreferences(prefs); p1.Load();
                Check("first start of the new version: shown", p1.ShouldShowWhatsNew("0.099"));
                Check("a different running version: not shown", !p1.ShouldShowWhatsNew("0.098"));
                string changelog = "## [Unreleased]\n\n- x\n\n## [V0.099] - 2026-11-05\n\n### Added\n\n- **Mock feature.**\n\n## [V0.098] - 2026-10-20\n\n- old\n";
                string entry = Changelog.Entry(changelog, "0.099") ?? "";
                var wn = UpdateFlowView.WhatsNew("V0.099", entry); Host(wn); Render(wn, directory, "5-whats-new");
                bool ok = false, never = false; wn.Ok += () => ok = true; wn.NeverRemind += () => never = true;
                Check("Esc/B on What's new = OK, never the 'never remind' choice", wn.HandleKey(Key.B) && ok && !never);
                p1.SetWhatsNewPending(null);
                var p2 = new ProductPreferences(prefs); p2.Load();
                Check("after it was shown once it is not shown again", !p2.ShouldShowWhatsNew("0.099"));
                p2.SetWhatsNewOff(); p2.SetWhatsNewPending("0.100");
                var p3 = new ProductPreferences(prefs); p3.Load();
                Check("Never remind me again holds for a later update", p3.WhatsNewOff && !p3.ShouldShowWhatsNew("0.100"));
            }

            // 9. a folder build can't self-update: plain in-app message, no browser
            {
                Updater.TestTarget = null; Updater.TestHandler = null;
                var flow = new UpdateFlow("V0.099", false); flow.Open();
                Check("a folder build shows an in-app explanation with no retry", flow.Step == UpdateStep.Failed && !flow.CanRetry && flow.Body.Contains("can't update itself"));
                Render(flow, directory, "6-failed-folder");
            }
        }
        finally
        {
            Updater.TestTarget = null; Updater.TestHandler = null;
            try { Directory.Delete(work, true); } catch (IOException) { }
        }
        log.Add(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        log.Add("Mock only: fake in-memory GitHub, stand-in exe in a temp folder (deleted), real Updater.Install, flow, view and preferences. No window shown, no network, no release, installed X20CTL untouched.");
        File.WriteAllLines(Path.Combine(directory, "update-mock-results.txt"), log);
        return failures;
    }

    sealed class SyncProgress(Action<double> sink) : IProgress<double> { public void Report(double v) => sink(v); }
    static string Hex(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static readonly List<Grid> hosts = [];
    static void Host(UpdateFlowView view) { var g = new Grid { Width = 1100, Height = 640, Background = DS.Brush("Ink") }; g.Children.Add(view); g.Measure(new Size(1100, 640)); g.Arrange(new Rect(0, 0, 1100, 640)); g.UpdateLayout(); hosts.Add(g); }
    static void Render(UpdateFlow flow, string directory, string name) { var v = new UpdateFlowView(flow); Host(v); Render(v, directory, name); }
    static void Render(UpdateFlowView view, string directory, string name)
    {
        var g = (Grid)view.Parent ?? new Grid(); g.UpdateLayout(); view.Render(); g.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1100, 640, 96, 96, PixelFormats.Pbgra32); bitmap.Render(g);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name + ".png")); png.Save(stream);
    }
}
