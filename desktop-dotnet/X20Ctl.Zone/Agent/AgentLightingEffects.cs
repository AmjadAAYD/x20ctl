using System.Text.RegularExpressions;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Zone.Agent;

/// <summary>
/// Parses natural-language lighting effect requests ("rainbow lights", "turn off lights", "moonlight pulse blue",
/// "cyberpunk fade", "lava shimmer", "trigger glow") into deterministic <see cref="LightingEffectSpec"/> values.
/// </summary>
public static class AgentLightingEffects
{
    public static bool TryParse(string? text, out LightingEffectSpec spec, out string summary)
    {
        spec = null!;
        summary = "";
        if (string.IsNullOrWhiteSpace(text)) return false;
        string raw = Regex.Replace(text.Trim().ToLowerInvariant(), @"\b(the|my|leds?|lights?|lighting|rgb|effect|mode|glow|please|pls|now|to|into|with|on|a|an)\b", " ");
        raw = Regex.Replace(raw, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(raw)) return false;

        // Check whether an optional colour word or #hex accompanies the effect name.
        string? stripped = Regex.Replace(raw, @"\b(rainbow|rainbow\s*0x01|hardware\s*rainbow|off|turn\s*off|dark|breathe|breathing|cycle|cycling|palette\s*fade|fade|moonlight|pulse|ultra\s*dim|hue\s*sweep|hue\s*wave|wave|sweep|strobe|police|dual\s*flash|flash|lava|fire|ember|shimmer|trigger|triggers|stick|sticks|heat|button|action|idle|nightlight|sleep|game\s*auto)\b", " ");
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
        AgentColor? parsedColor = AgentColors.TryParse(stripped, out var c) ? c : null;

        if (Regex.IsMatch(raw, @"\b(off|lights?\s*off|dark)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.NativeOff, Palette = ["#000000"], MaxBrightness = 0x00 }.Normalized();
            summary = "LEDs Off (Hardware 0x00)";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(rainbow)\b") && !raw.Contains("sweep") && !raw.Contains("wave"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.NativeRainbow, Palette = [parsedColor?.Hex ?? "#FF0000"], MaxBrightness = 0xB0 }.Normalized();
            summary = "Rainbow (Hardware 0x01)";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(moonlight|pulse|ultra\s*dim)\b"))
        {
            string hex = parsedColor?.Hex ?? "#6EC8FF";
            spec = new LightingEffectSpec
            {
                Kind = raw.Contains("dim") && !raw.Contains("pulse") && !raw.Contains("moonlight") ? LightingEffectKind.Solid : LightingEffectKind.MoonlightPulse,
                Palette = [hex],
                MinBrightness = LightingRecord.MoonlightBrightnessByte,
                MaxBrightness = raw.Contains("dim") && !raw.Contains("pulse") ? LightingRecord.MoonlightBrightnessByte : (byte)0x58,
                CyclePeriodMs = 4600,
                StepIntervalMs = 360,
            }.Normalized();
            summary = spec.Kind == LightingEffectKind.Solid ? $"Ultra-Dim Moonlight ({hex} @ 0x01)" : $"Moonlight Pulse ({hex}, 0x01 floor)";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(breathe|breathing)\b"))
        {
            string hex = parsedColor?.Hex ?? "#00C8FF";
            spec = new LightingEffectSpec { Kind = LightingEffectKind.NativeBreathe, Palette = [hex], MaxBrightness = 0xB0 }.Normalized();
            summary = $"Hardware Breathe ({hex})";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(hue\s*sweep|hue\s*wave|smooth\s*rainbow|color\s*wave|colour\s*wave|sweep)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.SmoothHueWave, MaxBrightness = 0xC8, CyclePeriodMs = 6000, StepIntervalMs = 340 }.Normalized();
            summary = "Smooth Hue Sweep";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(strobe|police|dual\s*flash)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.DualStrobe, Palette = [parsedColor?.Hex ?? "#FF0020", "#0055FF"], MaxBrightness = 0xFF, StepIntervalMs = 360 }.Normalized();
            summary = "Dual Flash / Strobe";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(lava|fire|ember|shimmer)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.EmberShimmer, Palette = [parsedColor?.Hex ?? "#FF2A00", "#FF8C00", "#FFB800"], MinBrightness = 0x35, MaxBrightness = 0xE8, StepIntervalMs = 380 }.Normalized();
            summary = "Fire / Ember Shimmer";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(trigger)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.TriggerGlow, Palette = ["#1B5CFF", parsedColor?.Hex ?? "#FF2E00"], MinBrightness = 0x24, MaxBrightness = 0xFF, StepIntervalMs = 350 }.Normalized();
            summary = "Trigger Pressure Glow";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(stick|heat)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.StickHeat, Palette = ["#00E0FF", "#FFAA00", "#FF1E56"], MinBrightness = 0x28, MaxBrightness = 0xFF, StepIntervalMs = 350 }.Normalized();
            summary = "Stick Deflection Heat";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(button|flash)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.ButtonFlash, Palette = ["#1D3557", parsedColor?.Hex ?? "#00F5D4"], MinBrightness = 0x20, MaxBrightness = 0xFF, StepIntervalMs = 340 }.Normalized();
            summary = "Button Action Flash";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(idle|nightlight|sleep)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.IdleSleepDim, Palette = [parsedColor?.Hex ?? "#4CC9F0"], MinBrightness = LightingRecord.MoonlightBrightnessByte, MaxBrightness = 0xA0, IdleTimeoutSeconds = 30, StepIntervalMs = 500 }.Normalized();
            summary = "Idle Auto-Dim Nightlight";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(ambient|ambilight|screen\s*sync|screen\s*ambient)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.ScreenAmbient, Palette = [parsedColor?.Hex ?? "#1E88E5"], MaxBrightness = 0xE0, StepIntervalMs = 360 }.Normalized();
            summary = "Ambient Screen Sync";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(battery(?:\s+(?:mode|lights?|leds?|level|indicator))?)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.BatteryLevel, Palette = ["#1EB980"], MaxBrightness = 0xC0 }.Normalized();
            summary = "Battery Level Indicator";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(game\s*auto)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.GameAutoProfile, MaxBrightness = 0xC0 }.Normalized();
            summary = "Auto by Foreground Game";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b((?:multi[\s-]*)?(?:color|colour|palette)\s*jump|palette\s*step)\b"))
        {
            spec = new LightingEffectSpec { Kind = LightingEffectKind.PaletteStep, Palette = [parsedColor?.Hex ?? "#00FFFF", "#FF00FF", "#FFAA00"], MaxBrightness = 0xD8, StepIntervalMs = 500 }.Normalized();
            summary = "Multi-Color Jump";
            return true;
        }
        if (Regex.IsMatch(raw, @"\b(cycle|cycling|fade)\b"))
        {
            spec = raw.Contains("fade")
                ? new LightingEffectSpec { Kind = LightingEffectKind.PaletteFade, Palette = [parsedColor?.Hex ?? "#00FFFF", "#FF00FF", "#3A6CFF"], MaxBrightness = 0xD8, StepIntervalMs = 360 }.Normalized()
                : new LightingEffectSpec { Kind = LightingEffectKind.NativeCycle, Palette = [parsedColor?.Hex ?? "#00FFFF"], MaxBrightness = 0xB0 }.Normalized();
            summary = spec.Kind == LightingEffectKind.PaletteFade ? "Multi-Color Fade" : "Hardware Cycle (0x04)";
            return true;
        }
        return false;
    }

    public static string? Asked(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return null;
        string s = AgentSpelling.Fix(sentence).Trim().ToLowerInvariant().Replace('’', '\'');
        if (Regex.IsMatch(s, @"\b(how|why|which|what)\b") && !Regex.IsMatch(s, @"\b(what\s+about|how\s+about|look\s+like)\b")) return null;
        if (Regex.IsMatch(s, @"\b(?:pc\s*mode|mouse\s*mode|dead\s*zone|anti[\s-]?dead|curve|turbo|remap|swap|paddle|recoil|drift|reset|slower|faster|speed|open\s+(?:diagnostics|tools|mouse|curves|buttons|macros|vibration|device)|left\s+(?:stick|trigger|bumper)|right\s+(?:stick|trigger|bumper))\b")
            || Regex.IsMatch(s, @"\band\s+(?:make|set|turn|switch|enable|disable|put|swap|remap|reset|open)\b"))
            return null;
        bool mentionsLights = Regex.IsMatch(s, @"\b(leds?|lights?|lighting|rgb|glow|backlight|rainbow|strobe|moonlight|ember|shimmer|nightlight|ambient|ambilight|screen\s*sync|battery\s*(?:lights?|leds?|mode|level|indicator)|(?:color|colour|palette)\s*(?:jump|fade)|hue\s*(?:sweep|wave)|trigger\s*glow|stick\s*heat|button\s*flash|game\s*auto)\b");
        if (!mentionsLights) return null;
        // Require either a lights-off turn off request or an effect keyword (never swallow colour questions like "change my led colour").
        if (Regex.IsMatch(s, @"\bturn\s+off\b.*\b(leds?|lights?|lighting|rgb)\b") || Regex.IsMatch(s, @"\b(leds?|lights?|rgb)\s+off\b"))
            return TryParse("off", out _, out _) ? "off" : null;
        return TryParse(s, out _, out _) ? s.Trim('.', '!', '?') : null;
    }
}
