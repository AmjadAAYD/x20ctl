using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// Sends a Write scan to X20CTLADMIN as a research report (schema 2, the same /api/controller-report route as the Read
/// scan). The receiver is asked first whether it takes schema 2; until that's deployed nothing is sent, and the owner is
/// told. Only ever called after the owner reviewed what goes and pressed Send.
/// </summary>
public static class WriteScanUpload
{
    const string Route = "https://x20-admin.vercel.app/api/controller-report";
    public const string ScannerVersion = "write-scan-1";

    public sealed class NotReady(string message) : Exception(message);

    public static async Task<string> Send(byte[] zip, string controllerName)
    {
        if (ReviewSandbox.Active) return "CR-SANDBOX";
        if (zip.Length > X20Ctl.Zone.Capture.WriteScanReport.MaxResearchZipBytes) throw new InvalidDataException("The report is over the receiver's 4 MB limit.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // does the receiver take research reports yet? An older one answers 405 to GET
        using (var probe = await http.GetAsync(Route))
        {
            bool ready = false;
            if (probe.IsSuccessStatusCode)
                try
                {
                    using var doc = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
                    ready = doc.RootElement.TryGetProperty("reportSchemaVersions", out var v) && v.ValueKind == JsonValueKind.Array && v.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Number && x.GetInt32() == 2);
                }
                catch (JsonException) { }
            if (!ready) throw new NotReady("X20CTLADMIN can't take Write scans yet. Nothing was sent; the findings are saved on this PC.");
        }
        var metadata = JsonSerializer.Serialize(new
        {
            controllerName = string.IsNullOrWhiteSpace(controllerName) ? "Unknown controller" : controllerName.Trim()[..Math.Min(120, controllerName.Trim().Length)],
            appVersion = ScanFlow.AppVersion, scannerVersion = ScannerVersion, clientTimestamp = DateTime.UtcNow.ToString("o"), connectionType = "Bluetooth (phone)", reportSchemaVersion = 2,
        });
        using var form = new MultipartFormDataContent("x20ctl-" + Guid.NewGuid().ToString("N"))
        {
            { new StringContent(Guid.NewGuid().ToString()), "clientSubmissionId" },
            { new StringContent(metadata), "metadata" },
        };
        var file = new ByteArrayContent(zip); file.Headers.ContentType = new("application/zip"); form.Add(file, "report", "write-scan.zip");
        using var response = await http.PostAsync(Route, form);
        string text = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode is not (200 or 201))
        {
            string reason = $"HTTP {(int)response.StatusCode}";
            try { using var d = JsonDocument.Parse(text); if (d.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)) reason = m.GetString() ?? reason; } catch (JsonException) { }
            throw new HttpRequestException("X20CTLADMIN refused it: " + reason);
        }
        using var doc2 = JsonDocument.Parse(text);
        string id = doc2.RootElement.TryGetProperty("submissionId", out var s) ? s.GetString() ?? "" : "";
        if (!Regex.IsMatch(id, "^CR-[0-9A-F]{24}$")) throw new InvalidDataException("X20CTLADMIN returned no valid receipt.");
        return id;
    }
}
