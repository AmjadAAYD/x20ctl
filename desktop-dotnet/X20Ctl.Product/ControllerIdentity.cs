namespace X20Ctl.Product;

public enum VerifyOutcome { Match, Mismatch, Unconfirmed, NoController }

/// <summary>Minimal HID/USB identity fields needed to evaluate connected controllers without Windows or WPF dependencies.</summary>
public sealed record DeviceIdentity(string Kind, string Path, string Container, string Product, string Manufacturer, int Vid, int Pid, int UsagePage, int Usage);

/// <summary>What the scan on entering a controller found. ModelId is a model id when Windows named the pad as one.</summary>
public sealed record DetectedController(string Name, string? ModelId, string Detail);

public sealed record VerifyResult(
    VerifyOutcome Outcome,
    string? DetectedId,
    string? DetectedName,
    string Detail,
    IReadOnlyList<DetectedController>? Controllers = null,
    string? Transport = null);

/// <summary>
/// Pure controller-identity matching shared between ControllerVerify, ScanFlow, and unit tests.
/// Shared IDs (045E:028E Xbox XInput, 0079:181C DragonRise/STK-7024X DInput) are never mapped to a single model,
/// and the retracted 1D57:FA60 Xenta wireless mouse/keyboard receiver is never treated as a controller.
/// </summary>
public static class ControllerIdentity
{
    public const int DragonRiseDInputVid = 0x0079;
    public const int DragonRiseDInputPid = 0x181C;
    public const int RetractedXentaVid = 0x1D57;
    public const int RetractedXentaPid = 0xFA60;
    public const int Receiver413DVid = 0x413D;
    public const int Receiver413DPid = 0x2131;

    // Model ids keyed by the label DetectModelFromText returns.
    public static readonly Dictionary<string, string> ByName = new()
    {
        ["Dune / D15"] = "dune",
        ["X20 Pro"] = "x20_pro",
        ["X20"] = "x20",
        ["X15"] = "x15",
        ["X10"] = "x10",
        ["X05 Pro"] = "x05_pro",
        ["D10"] = "d10",
    };

    public static (string Name, string Model)? DetectModelFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains("EasySMX", StringComparison.OrdinalIgnoreCase))
            return null;

        // Normalize separators so "X20PRO", "X20-Pro", and "X20_Pro" match before plain "X20".
        string compact = text.ToUpperInvariant()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);

        string? model =
            compact.Contains("DUNE", StringComparison.Ordinal) || compact.Contains("D15", StringComparison.Ordinal) ? "Dune / D15" :
            compact.Contains("X20PRO", StringComparison.Ordinal) ? "X20 Pro" :
            compact.Contains("X20", StringComparison.Ordinal) ? "X20" :
            compact.Contains("X15", StringComparison.Ordinal) ? "X15" :
            compact.Contains("X10", StringComparison.Ordinal) ? "X10" :
            compact.Contains("X05PRO", StringComparison.Ordinal) ? "X05 Pro" :
            compact.Contains("D10", StringComparison.Ordinal) ? "D10" : null;

        return model != null ? (text.Trim(), model) : null;
    }

    /// <summary>The model a controller names itself as from product/manufacturer strings, or the 413D:2131 receiver fallback.</summary>
    public static (string Name, string Model)? DetectModel(IEnumerable<DeviceIdentity> interfaces)
    {
        var list = interfaces as IReadOnlyList<DeviceIdentity> ?? interfaces.ToList();
        foreach (var i in list)
        {
            if (IsRetractedXenta(i)) continue;
            foreach (var text in new[] { i.Product, i.Manufacturer })
                if (DetectModelFromText(text) is { } match)
                    return match;
        }
        if (list.Any(IsReceiver413D))
            return ("EasySMX receiver (X20 Pro or Dune)", "X20 Pro / Dune");
        return null;
    }

    /// <summary>Bluetooth when enumerated over Bluetooth HID, Receiver when 413D:2131 is present, otherwise null.</summary>
    public static string? DetectTransport(IEnumerable<DeviceIdentity> interfaces)
    {
        var list = interfaces as IReadOnlyList<DeviceIdentity> ?? interfaces.ToList();
        if (list.Any(IsReceiver413D)) return "Receiver";
        if (list.Any(i => i.UsagePage == 1 && i.Usage is 4 or 5 &&
            (i.Path.Contains("00001124-0000-1000-8000-00805f9b34fb", StringComparison.OrdinalIgnoreCase) ||
             i.Path.Contains("BTHLE", StringComparison.OrdinalIgnoreCase) ||
             i.Path.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase))))
            return "Bluetooth";
        return null;
    }

    public static IReadOnlyList<DetectedController> Describe(IEnumerable<DeviceIdentity> interfaces)
    {
        var found = new List<DetectedController>();
        var groups = interfaces
            .Where(i => !IsRetractedXenta(i))
            .GroupBy(x => string.IsNullOrEmpty(x.Container) ? "interface:" + x.Path : "container:" + x.Container);

        foreach (var group in groups)
        {
            var items = group.ToList();
            (string Name, string Model)? named = null;
            foreach (var i in items)
            {
                foreach (var text in new[] { i.Product, i.Manufacturer })
                {
                    named = DetectModelFromText(text);
                    if (named != null) break;
                }
                if (named != null) break;
            }

            var pad = items.FirstOrDefault(i => i.Kind == "HID" && i.UsagePage == 1 && i.Usage is 4 or 5);
            pad ??= items.FirstOrDefault(i => i.Vid == 0x045E &&
                (i.Product.Contains("Xbox", StringComparison.OrdinalIgnoreCase) ||
                 i.Product.Contains("Controller", StringComparison.OrdinalIgnoreCase)));
            pad ??= items.FirstOrDefault(IsDragonRiseDInput);
            bool isReceiver = items.Any(IsReceiver413D);

            if (pad == null && named == null && !isReceiver) continue;

            var device = pad ?? items[0];
            string? id = named is { } n && ByName.TryGetValue(n.Model, out var mapped) ? mapped : null;
            bool isSharedDInput = id == null && items.Any(IsDragonRiseDInput);

            string name = named?.Name
                ?? (isReceiver ? "EasySMX receiver (X20 Pro or Dune)"
                : !string.IsNullOrWhiteSpace(device.Product) ? device.Product
                : isSharedDInput ? "DInput gamepad (0079:181C)"
                : "Windows gamepad");

            string suffix = isReceiver ? " · 2.4 GHz receiver"
                : id != null ? " · reported identity"
                : isSharedDInput ? " · DInput mode (model unconfirmed)"
                : " · model unconfirmed";

            found.Add(new(name, id, $"{device.Kind} · {device.Vid:X4}:{device.Pid:X4}{suffix}"));
        }
        return found;
    }

    public static VerifyResult Evaluate(string modelId, IEnumerable<DeviceIdentity> interfaces)
    {
        var list = interfaces as IReadOnlyList<DeviceIdentity> ?? interfaces.ToList();
        var found = Describe(list);
        string? transport = DetectTransport(list);
        VerifyResult Result(VerifyOutcome outcome, string? id, string? name, string detail) =>
            new(outcome, id, name, detail, found, transport);

        var claims = new List<(string Id, string Name)>();
        foreach (var i in list)
        {
            if (IsRetractedXenta(i)) continue;
            foreach (var text in new[] { i.Product, i.Manufacturer })
            {
                if (DetectModelFromText(text) is { } named && ByName.TryGetValue(named.Model, out var claimedId))
                    claims.Add((claimedId, named.Name));
            }
        }

        // 1. Exact explicit model string match (also handles multi-controller setups).
        foreach (var claim in claims)
            if (claim.Id == modelId)
                return Result(VerifyOutcome.Match, claim.Id, claim.Name, claim.Name);

        // 2. 413D:2131 receiver matches X20 Pro or Dune / D15 before any other claim can shadow it.
        if (list.Any(IsReceiver413D) && modelId is "x20_pro" or "dune")
            return Result(VerifyOutcome.Match, modelId, "EasySMX receiver (X20 Pro or Dune)", "EasySMX receiver (X20 Pro or Dune)");

        // 3. Explicit mismatch only when an EasySMX product string actively names a different model.
        if (claims.Count > 0)
        {
            var other = claims[0];
            return Result(VerifyOutcome.Mismatch, other.Id, other.Name, other.Name);
        }

        // 4. Shared identities (XInput 045E:028E or DInput 0079:181C / STK-7024X) remain Unconfirmed, never Mismatch.
        if (found.Count > 0)
        {
            string detail = list.Any(IsDragonRiseDInput)
                ? "Connected in DInput mode (0079:181C, often shown as STK-7024X in Steam). Model is unconfirmed — switch to XInput mode for full PC support, or enter Studio now."
                : "A gamepad is connected but doesn't name its model.";
            return Result(VerifyOutcome.Unconfirmed, null, null, detail);
        }

        return Result(VerifyOutcome.NoController, null, null, "No gamepad was listed.");
    }

    private static bool IsRetractedXenta(DeviceIdentity i) => i.Vid == RetractedXentaVid && i.Pid == RetractedXentaPid;
    private static bool IsDragonRiseDInput(DeviceIdentity i) => i.Vid == DragonRiseDInputVid && i.Pid == DragonRiseDInputPid;
    private static bool IsReceiver413D(DeviceIdentity i) => i.Vid == Receiver413DVid && i.Pid == Receiver413DPid;
}
