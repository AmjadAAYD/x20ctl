using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace X20Ctl.Zone.PcMode;

public sealed record ExternalImportResult(PcModeProfile Profile, string SourceFormat, string Summary);

/// <summary>
/// Clean-room GPL-3.0 parser translating DS4Windows (.xml) and Steam Input (.vdf) controller configs
/// into validated <see cref="PcModeProfile"/> instances alongside native X20CTL (.x20pc) profiles.
/// UX workflow inspired by PadForge.
/// </summary>
public static partial class ExternalProfileImporter
{
    public const int MaxImportBytes = 512 * 1024;

    public static ExternalImportResult Import(string fileName, string content)
    {
        if (content == null) throw new InvalidDataException("Profile content is empty.");
        if (Encoding.UTF8.GetByteCount(content) > MaxImportBytes)
            throw new InvalidDataException("That file is too large to import as a controller profile.");

        string ext = Path.GetExtension(fileName ?? string.Empty).Trim().ToLowerInvariant();
        string trimmed = content.TrimStart();

        if (ext == ".xml" || trimmed.StartsWith('<'))
            return ImportDs4WindowsXml(content);
        if (ext == ".vdf" || trimmed.StartsWith("\"controller_mappings\"", StringComparison.OrdinalIgnoreCase))
            return ImportSteamVdf(content);

        var native = PcModeShare.FromJson(content);
        return new(native, "X20CTL", "Loaded native X20CTL PC mode profile.");
    }

    static ExternalImportResult ImportDs4WindowsXml(string xmlText)
    {
        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
            using var reader = XmlReader.Create(new StringReader(xmlText), settings);
            doc = XDocument.Load(reader);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new InvalidDataException("Could not parse DS4Windows XML profile: " + ex.Message);
        }

        var root = doc.Root ?? throw new InvalidDataException("DS4Windows XML root element is missing.");
        var profile = new PcModeProfile();
        int adjusted = 0;

        static double ReadDouble(XElement parent, string name, double fallback)
        {
            var el = parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
            return el != null && double.TryParse(el.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v
                : fallback;
        }

        double NormalizeDs4Deadzone(double val)
        {
            // DS4Windows stores LSDeadZone either in 0..1.0 float or 0..100 percent depending on version.
            double pct = val <= 1.0 && val > 0.0 ? val * 100.0 : val;
            return Math.Round(Math.Clamp(pct, 0.0, StickTuning.MaxInner), 1);
        }

        double lsDz = NormalizeDs4Deadzone(ReadDouble(root, "LSDeadZone", 0));
        double rsDz = NormalizeDs4Deadzone(ReadDouble(root, "RSDeadZone", 0));
        double lsAnti = Math.Round(Math.Clamp(ReadDouble(root, "LSAntiDeadZone", 0) is double a && a <= 1.0 && a > 0 ? a * 100.0 : ReadDouble(root, "LSAntiDeadZone", 0), 0.0, StickTuning.MaxAnti), 1);
        double rsAnti = Math.Round(Math.Clamp(ReadDouble(root, "RSAntiDeadZone", 0) is double b && b <= 1.0 && b > 0 ? b * 100.0 : ReadDouble(root, "RSAntiDeadZone", 0), 0.0, StickTuning.MaxAnti), 1);
        double lsMax = Math.Round(Math.Clamp(ReadDouble(root, "LSMaxZone", 100) is double m1 && m1 <= 2.0 && m1 > 0.5 ? m1 * 100.0 : ReadDouble(root, "LSMaxZone", 100), StickTuning.MinOuter, 100.0), 1);
        double rsMax = Math.Round(Math.Clamp(ReadDouble(root, "RSMaxZone", 100) is double m2 && m2 <= 2.0 && m2 > 0.5 ? m2 * 100.0 : ReadDouble(root, "RSMaxZone", 100), StickTuning.MinOuter, 100.0), 1);

        if (lsDz > 0 || lsAnti > 0 || lsMax < 100)
        {
            profile.Sticks["LeftStick"] = new StickTuning { Inner = lsDz, AntiDeadzone = lsAnti, Outer = Math.Max(lsMax, Math.Max(StickTuning.MinOuter, lsDz + 1)) };
            adjusted++;
        }
        if (rsDz > 0 || rsAnti > 0 || rsMax < 100)
        {
            profile.Sticks["RightStick"] = new StickTuning { Inner = rsDz, AntiDeadzone = rsAnti, Outer = Math.Max(rsMax, Math.Max(StickTuning.MinOuter, rsDz + 1)) };
            adjusted++;
        }

        double l2Dz = NormalizeDs4Deadzone(ReadDouble(root, "L2DeadZone", 0));
        double r2Dz = NormalizeDs4Deadzone(ReadDouble(root, "R2DeadZone", 0));
        if (l2Dz > 0)
        {
            profile.Curves[CurveChannel.LeftTrigger.ToString()] = new SetupCurve("default", "default", (int)Math.Round(l2Dz), 100, 85, 85, 170, 170);
            adjusted++;
        }
        if (r2Dz > 0)
        {
            profile.Curves[CurveChannel.RightTrigger.ToString()] = new SetupCurve("default", "default", (int)Math.Round(r2Dz), 100, 85, 85, 170, 170);
            adjusted++;
        }

        double rumble = ReadDouble(root, "RumbleBoost", 100);
        int vibPct = (int)Math.Clamp(Math.Round(rumble), 0, PcModeProfile.MaxVibration);
        if (vibPct != 100)
        {
            profile.Vibration = vibPct;
            adjusted++;
        }

        profile.Validate();
        string desc = adjusted > 0
            ? $"Imported DS4Windows XML ({adjusted} stick/trigger/rumble settings mapped)."
            : "Imported DS4Windows XML (standard defaults retained).";
        return new(profile, "DS4Windows", desc);
    }

    static ExternalImportResult ImportSteamVdf(string vdfText)
    {
        if (!vdfText.Contains('"'))
            throw new InvalidDataException("Could not parse Steam Input VDF file.");

        var profile = new PcModeProfile();
        int mapped = 0;

        var kvMatches = VdfPairRegex().Matches(vdfText);
        double innerDz = 0;
        double outerDz = 100;

        foreach (Match m in kvMatches)
        {
            string key = m.Groups[1].Value.Trim().ToLowerInvariant();
            string rawVal = m.Groups[2].Value.Trim();

            if (key == "deadzone_inner_radius" && double.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out double inner))
            {
                double pct = inner <= 1.0 ? inner * 100.0 : inner / 327.67;
                innerDz = Math.Round(Math.Clamp(pct, 0.0, StickTuning.MaxInner), 1);
            }
            else if (key == "deadzone_outer_radius" && double.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out double outer))
            {
                double pct = outer <= 1.0 ? outer * 100.0 : outer / 327.67;
                outerDz = Math.Round(Math.Clamp(pct, StickTuning.MinOuter, 100.0), 1);
            }
            else if (TryMapSteamButton(key, out string? srcBtn) && TryExtractSteamPadTarget(rawVal, out string? dstBtn) && srcBtn != dstBtn)
            {
                profile.Remap[srcBtn] = dstBtn;
                mapped++;
            }
        }

        if (innerDz > 0 || outerDz < 100)
        {
            double safeOuter = Math.Max(outerDz, Math.Max(StickTuning.MinOuter, innerDz + 1));
            profile.Sticks["LeftStick"] = new StickTuning { Inner = innerDz, Outer = safeOuter };
            profile.Sticks["RightStick"] = new StickTuning { Inner = innerDz, Outer = safeOuter };
            mapped++;
        }

        profile.Validate();
        string desc = mapped > 0
            ? $"Imported Steam Input VDF ({mapped} bindings/deadzone settings mapped)."
            : "Imported Steam Input VDF (standard defaults retained).";
        return new(profile, "Steam VDF", desc);
    }

    static bool TryMapSteamButton(string key, out string button)
    {
        button = key switch
        {
            "button_a" or "south" => "A",
            "button_b" or "east" => "B",
            "button_x" or "west" => "X",
            "button_y" or "north" => "Y",
            "left_bumper" or "shoulder_left" => "LB",
            "right_bumper" or "shoulder_right" => "RB",
            "button_back" or "button_view" or "select" => "VIEW",
            "button_start" or "button_menu" or "start" => "MENU",
            "button_lstick" or "left_stick_click" => "LS",
            "button_rstick" or "right_stick_click" => "RS",
            _ => string.Empty,
        };
        return button.Length > 0;
    }

    static bool TryExtractSteamPadTarget(string rawVal, out string target)
    {
        string lower = rawVal.ToLowerInvariant();
        target = lower.Contains("xinput_button a") || lower.Contains("gamepad_button a") ? "A"
            : lower.Contains("xinput_button b") || lower.Contains("gamepad_button b") ? "B"
            : lower.Contains("xinput_button x") || lower.Contains("gamepad_button x") ? "X"
            : lower.Contains("xinput_button y") || lower.Contains("gamepad_button y") ? "Y"
            : lower.Contains("shoulder_left") || lower.Contains("left_bumper") ? "LB"
            : lower.Contains("shoulder_right") || lower.Contains("right_bumper") ? "RB"
            : lower.Contains("stick_left_click") ? "LS"
            : lower.Contains("stick_right_click") ? "RS"
            : string.Empty;
        return target.Length > 0;
    }

    [GeneratedRegex("\"([^\"]+)\"\\s+\"([^\"]*)\"")]
    private static partial Regex VdfPairRegex();
}
