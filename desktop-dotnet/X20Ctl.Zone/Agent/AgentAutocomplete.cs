namespace X20Ctl.Zone.Agent;

/// <summary>
/// Fast, compositional Gemini / AI Studio-style inline ghost autocomplete for Agent XCTL.
/// Understands every RGB / Effects Studio mode, colour, stick/trigger tuning, remap, turbo,
/// paddle, PC/mouse mode, diagnostics page, and question across arbitrary phrasing.
/// </summary>
public static class AgentAutocomplete
{
    static readonly string[] RgbEffectsAndColors =
    [
        "ambient",
        "ambient mode",
        "screen sync",
        "battery mode",
        "moonlight pulse",
        "hue sweep",
        "palette jump",
        "multi-color fade",
        "rainbow",
        "breathe cyan",
        "lava ember",
        "dual strobe",
        "trigger glow",
        "stick heat",
        "button flash",
        "idle nightlight",
        "game auto",
        "cyan",
        "red",
        "blue",
        "green",
        "purple",
        "magenta",
        "pink",
        "orange",
        "yellow",
        "white",
        "warm white",
        "teal",
        "gold",
        "off",
    ];

    static readonly string[] Pages =
    [
        "diagnostics",
        "curves",
        "buttons",
        "macros",
        "vibration",
        "mouse",
        "pc mode",
        "tools",
        "device",
    ];

    static readonly string[] Buttons =
    [
        "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3",
        "START", "SELECT", "HOME", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT",
    ];

    static readonly string[] StickTuningCompletions =
    [
        "more precise",
        "smoother",
        "faster",
        "slower",
        "less twitchy",
        "deadzone to 8%",
        "anti-deadzone to 10%",
        "curve to smooth",
        "max output to 85%",
        "axis ratio to 85%",
        "square gate on",
    ];

    static readonly string[] TriggerCompletions =
    [
        "hair trigger",
        "faster",
        "progressive",
        "deadzone to 2%",
        "full at 60%",
        "curve to quick",
    ];

    static readonly string[] DomainVocabulary =
    [
        "ambient", "ambient mode", "ambilight", "anti-deadzone", "axis ratio",
        "battery", "battery mode", "breathe", "brightness", "button flash", "buttons",
        "calibrate", "controller", "curve", "curves", "cyan", "cycle",
        "deadzone", "default", "device", "diagnostics", "disable", "drift", "drifting", "dual strobe",
        "ember", "enable",
        "fade", "faster", "fine",
        "game auto", "glow",
        "hair trigger", "hue sweep",
        "idle nightlight",
        "lava ember", "left stick", "left trigger", "lighting", "lights",
        "macros", "magenta", "max output", "measure drift", "moonlight pulse", "mouse mode", "multi-color fade",
        "nightlight",
        "orange", "overshooting",
        "paddle", "palette jump", "pc mode", "precise", "purple",
        "quick",
        "rainbow", "recoil", "remap", "reset", "responsive", "right stick", "right trigger",
        "screen sync", "sensitivity", "slow", "smoother", "snappy", "square gate", "stick heat", "strobe",
        "teal", "tools", "trigger glow", "triggers", "turbo", "twitchy",
        "vibration",
        "warm white",
    ];

    public static string Suggest(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed) || typed.Length > 180 || typed.Contains('\n') || typed.Contains('\r'))
            return "";

        string lower = typed.ToLowerInvariant();
        bool endsWithSpace = char.IsWhiteSpace(typed[^1]);
        string trimmed = typed.TrimEnd();
        string trimmedLower = trimmed.ToLowerInvariant();
        string lastWord = ExtractTrailingWord(lower);

        // 1. RGB / LED / Lighting intent anywhere in the prompt
        if (HasAnyWord(lower, "rgb", "led", "leds", "light", "lights", "lighting", "glow", "backlight"))
        {
            string rgb = PredictRgb(lower, trimmedLower, endsWithSpace, lastWord);
            if (rgb.Length > 0) return rgb;
        }

        // 2. Open / Show / Go to Page intent
        if (StartsWithAny(lower, "open ", "show ", "go to ", "switch to page "))
        {
            string page = CompleteFromList(lastWord, endsWithSpace, Pages, "diagnostics");
            if (page.Length > 0) return page;
        }

        // 3. Remap / Swap / Bind buttons
        if (HasAnyWord(lower, "remap", "swap", "bind", "map"))
        {
            string remap = PredictRemap(lower, endsWithSpace, lastWord);
            if (remap.Length > 0) return remap;
        }

        // 4. Turbo
        if (HasAnyWord(lower, "turbo"))
        {
            if (endsWithSpace)
            {
                if (trimmedLower.EndsWith("turbo")) return "on A to 12";
                if (trimmedLower.EndsWith(" on")) return "A to 12";
                if (trimmedLower.EndsWith(" to") || trimmedLower.EndsWith(" at")) return "12";
                if (trimmedLower.EndsWith("off")) return "on A";
            }
            string btn = CompleteFromList(lastWord, false, Buttons, "");
            if (btn.Length > 0) return btn;
        }

        // 5. Paddle (M1-M4)
        if (HasAnyWord(lower, "paddle", "paddles", "m1", "m2", "m3", "m4"))
        {
            if (endsWithSpace)
            {
                if (trimmedLower.EndsWith("to")) return "A";
                if (trimmedLower.EndsWith("paddle") || trimmedLower.EndsWith("paddles")) return "M1 to A";
                return "to A";
            }
        }

        // 6. Trigger tuning
        if (HasAnyWord(lower, "trigger", "triggers", "lt", "rt", "l2", "r2"))
        {
            string trig = PredictTuning(trimmedLower, endsWithSpace, lastWord, TriggerCompletions, "hair trigger");
            if (trig.Length > 0) return trig;
        }

        // 7. Stick / Aim / Drift / Deadzone / Curve / Recoil tuning
        if (HasAnyWord(lower, "stick", "aim", "drift", "drifts", "drifting", "deadzone", "curve", "recoil", "sens", "sensitivity", "twitchy", "overshoot", "diagonal", "square"))
        {
            string stick = PredictStick(lower, trimmedLower, endsWithSpace, lastWord);
            if (stick.Length > 0) return stick;
        }

        // 8. PC Mode / Mouse Mode / Reset / Measure / Diagnostics / Enable / Disable
        string modeOrAction = PredictModeOrAction(lower, trimmedLower, endsWithSpace, lastWord);
        if (modeOrAction.Length > 0) return modeOrAction;

        // 9. Questions
        if (StartsWithAny(lower, "how ", "what ", "why ", "can ", "does ", "is ", "where ", "which "))
        {
            string q = PredictQuestion(lower, endsWithSpace);
            if (q.Length > 0) return q;
        }

        // 10. Universal trailing-word completion from full X20CTL vocabulary (so partial words never stall)
        if (!endsWithSpace && lastWord.Length >= 2)
        {
            string vocab = CompleteFromList(lastWord, false, DomainVocabulary, "");
            if (vocab.Length > 0) return vocab;
        }

        // 11. Universal connector fallback when user ends on a preposition/verb waiting for a target
        if (endsWithSpace)
        {
            if (trimmedLower.EndsWith(" to") || trimmedLower.EndsWith(" into")) return "ambient";
            if (trimmedLower.EndsWith(" on")) return "ambient mode";
            if (trimmedLower.EndsWith(" off")) return "my lights";
            if (trimmedLower.EndsWith(" my")) return "RGB to ambient";
        }

        return "";
    }

    static string PredictRgb(string lower, string trimmedLower, bool endsWithSpace, string lastWord)
    {
        if (!endsWithSpace)
        {
            // Complete "led" -> "s to ambient" or partial effect/color word
            if (trimmedLower is "make my led" or "change my led" or "set my led")
                return "s to ambient";

            if (lastWord.Length >= 1 && !IsLightNoun(lastWord) && !IsConnector(lastWord))
            {
                string match = CompleteFromList(lastWord, false, RgbEffectsAndColors, "");
                if (match.Length > 0) return match;
            }

            if (IsLightNoun(lastWord))
                return " to ambient";

            return "";
        }

        // Ends with space: check what precedes it
        if (trimmedLower.EndsWith(" off")) return "";
        if (trimmedLower.EndsWith(" to") || trimmedLower.EndsWith(" into") || trimmedLower.EndsWith(" like") || trimmedLower.EndsWith(" in"))
            return "ambient";
        if (trimmedLower.EndsWith(" on"))
            return "ambient mode";

        // User wrote e.g. "make my led " / "set my rgb " / "turn lights "
        if (HasAnyWord(lower, "turn off", "shut off", "switch off", "disable"))
            return "";
        if (HasAnyWord(lower, "make", "set", "change", "switch", "put", "turn"))
            return "to ambient";

        return "ambient";
    }

    static string PredictRemap(string lower, bool endsWithSpace, string lastWord)
    {
        if (endsWithSpace)
        {
            if (lower.Contains(" to ")) return "B";
            if (lower.Contains(" and ")) return "B";
            if (HasAnyWord(lower, "a", "b", "x", "y", "lb", "rb", "lt", "rt", "l3", "r3"))
                return lower.Contains("swap") ? "and B" : "to B";
            return lower.Contains("swap") ? "A and B" : "A to B";
        }
        return CompleteFromList(lastWord, false, Buttons, "");
    }

    static string PredictStick(string lower, string trimmedLower, bool endsWithSpace, string lastWord)
    {
        if (!endsWithSpace)
        {
            if (lastWord.Length >= 1 && !IsStickNoun(lastWord) && !IsConnector(lastWord))
            {
                string match = CompleteFromList(lastWord, false, StickTuningCompletions, "");
                if (match.Length > 0) return match;
            }
            if (trimmedLower.EndsWith("drift")) return "s";
            return "";
        }

        if (trimmedLower.EndsWith("fix") || trimmedLower.EndsWith("measure") || trimmedLower.EndsWith("check"))
            return "stick drift";
        if (trimmedLower.EndsWith("stick") || trimmedLower.EndsWith("sticks"))
            return lower.Contains("fix") || lower.Contains("measure") || lower.Contains("check") ? "drift" : "drifts";
        if (trimmedLower.EndsWith("aim"))
            return "more precise";
        if (trimmedLower.EndsWith("recoil"))
            return "by setting axis ratio to 85%";
        if (trimmedLower.EndsWith("deadzone"))
            return "to 8%";
        if (trimmedLower.EndsWith("curve"))
            return "to smooth";

        return PredictTuning(trimmedLower, endsWithSpace, lastWord, StickTuningCompletions, "more precise");
    }

    static string PredictTuning(string trimmedLower, bool endsWithSpace, string lastWord, string[] options, string defaultChoice)
    {
        if (!endsWithSpace)
            return CompleteFromList(lastWord, false, options, "");

        if (trimmedLower.EndsWith(" to"))
        {
            if (trimmedLower.Contains("deadzone")) return "6%";
            if (trimmedLower.Contains("full at")) return "60%";
            if (trimmedLower.Contains("curve")) return "smooth";
            return "85%";
        }
        if (trimmedLower.EndsWith(" more")) return "precise";
        if (trimmedLower.EndsWith(" less")) return "twitchy";
        return defaultChoice;
    }

    static string PredictModeOrAction(string lower, string trimmedLower, bool endsWithSpace, string lastWord)
    {
        string[] quickActions =
        [
            "ambient mode",
            "battery mode",
            "pc mode",
            "mouse mode",
            "moonlight pulse",
            "hue sweep",
            "palette jump",
            "trigger glow",
            "stick heat",
            "button flash",
            "game auto",
            "diagnostics",
            "everything",
            "my lights",
            "stick drift",
            "my controller",
        ];

        if (!endsWithSpace && lastWord.Length >= 1)
        {
            if (StartsWithAny(lower, "enable ", "turn on ", "switch on ", "start "))
                return CompleteFromList(lastWord, false, quickActions, "");
            if (StartsWithAny(lower, "disable ", "turn off ", "stop "))
                return CompleteFromList(lastWord, false, ["my lights", "ambient mode", "pc mode", "mouse mode", "turbo"], "");
            if (StartsWithAny(lower, "reset "))
                return CompleteFromList(lastWord, false, ["everything", "left stick", "right stick", "left trigger", "right trigger", "all curves"], "");
        }

        if (endsWithSpace)
        {
            if (trimmedLower is "enable" or "turn on" or "switch on" or "start") return "ambient mode";
            if (trimmedLower is "disable" or "turn off" or "stop") return "my lights";
            if (trimmedLower is "reset") return "everything";
            if (trimmedLower is "measure" or "check" or "test") return "stick drift";
            if (trimmedLower is "calibrate") return "my sticks";
            if (trimmedLower is "make" or "make my") return "lights ambient";
            if (trimmedLower is "change" or "change my" or "set" or "set my") return "RGB to ambient";
        }

        return "";
    }

    static string PredictQuestion(string lower, bool endsWithSpace)
    {
        string[] questions =
        [
            "how do i use ambient mode?",
            "how do i enable pc mode?",
            "how do i fix stick drift?",
            "what is ambient mode?",
            "what is pc mode?",
            "what is anti-deadzone?",
            "why is my aim twitchy?",
            "can i set different colors per led?",
        ];
        if (!endsWithSpace || lower.Length >= 4)
        {
            foreach (string q in questions)
            {
                if (q.Length > lower.Length && q.StartsWith(lower, StringComparison.Ordinal))
                    return q[lower.Length..];
            }
        }
        return "";
    }

    static string CompleteFromList(string lastWord, bool endsWithSpace, IReadOnlyList<string> candidates, string fallbackWhenEmpty)
    {
        if (endsWithSpace) return fallbackWhenEmpty;
        if (string.IsNullOrEmpty(lastWord)) return "";
        foreach (string item in candidates)
        {
            if (item.Length > lastWord.Length && item.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
                return item[lastWord.Length..];
        }
        return "";
    }

    static string ExtractTrailingWord(string lower)
    {
        if (lower.Length == 0 || char.IsWhiteSpace(lower[^1])) return "";
        int i = lower.Length - 1;
        while (i >= 0 && !char.IsWhiteSpace(lower[i])) i--;
        return lower[(i + 1)..];
    }

    static bool HasAnyWord(string lower, params string[] words)
    {
        foreach (string w in words)
        {
            int idx = 0;
            while ((idx = lower.IndexOf(w, idx, StringComparison.Ordinal)) >= 0)
            {
                bool leftOk = idx == 0 || !char.IsLetterOrDigit(lower[idx - 1]);
                int end = idx + w.Length;
                bool rightOk = end == lower.Length || !char.IsLetterOrDigit(lower[end]);
                if (leftOk && rightOk) return true;
                idx = end;
            }
        }
        return false;
    }

    static bool StartsWithAny(string lower, params string[] prefixes)
    {
        foreach (string p in prefixes)
        {
            if (lower.StartsWith(p, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    static bool IsLightNoun(string word) => word is "led" or "leds" or "light" or "lights" or "lighting" or "rgb" or "glow" or "backlight" or "color" or "colour";
    static bool IsStickNoun(string word) => word is "stick" or "sticks" or "aim" or "deadzone" or "curve";
    static bool IsConnector(string word) => word is "to" or "into" or "on" or "in" or "like" or "at" or "for" or "by" or "with" or "my";
}
