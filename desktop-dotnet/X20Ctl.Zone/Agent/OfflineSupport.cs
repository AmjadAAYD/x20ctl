using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>Authored offline language/knowledge, with no model, network, or assumed hardware access.</summary>
public static class OfflineSupport
{
    public static string Normalize(string text)
    {
        text = text.Trim().Replace('’', '\'');
        text = Regex.Replace(text, @"^i (?:don't|do not) want (pc|mouse) mode[.!]?$", "turn $1 mode off", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^(?:(?:hey|hi)[,!]?\s+)?(?:(?:can|could|would) you\s+|i (?:would like|want) you to\s+|please\s+)", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(dead\s+zones?|deadzones?)\b", "dead zone", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(left|right) (?:analog|analogue|thumbstick|joystick)\b", "$1 stick", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^replace (a|b|x|y|lb|rb|lt|rt) with (a|b|x|y|lb|rb|lt|rt)[.!]?$", "remap $1 to $2", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(rebind|reassign)\b", "remap", RegexOptions.IgnoreCase);
        return Regex.Replace(text,@"\s+"," ").Trim();
    }

    public static OfflinePlan? Explain(string text)
    {
        var t = text.ToLowerInvariant();
        OfflinePlan Info(string speech, string recap) => OfflinePlan.Say(speech,recap);
        if (Regex.IsMatch(t,@"^(don't|do not|never|not) (change|set|enable|disable|turn|reset|remap)\b"))
            return Info("Understood. I changed nothing.","Tell me explicitly when you want a setting changed.");
        if (Regex.IsMatch(t,@"\b(won't connect|will not connect|not detected|not recognized|not recognised|no controller found|keeps disconnecting|connection keeps dropping)\b"))
            return Info("Let's check the connection first.","Try USB or your receiver, confirm the controller is powered on, and run Diagnostics or Tools → Controller Check. A generic Windows gamepad name does not prove which model it is. I cannot pair Bluetooth or change firmware for you.");
        if (Regex.IsMatch(t,@"\b(need|requires?|without|install|download)\b.*\b(internet|network|model)\b|\b(offline mode|work offline|data private|send my data|privacy)\b") && !Regex.IsMatch(t,@"\b(turn|switch)\b"))
            return Info("I run right here on your PC.","No internet, account, API key or download is needed. I understand controller topics and ask for missing details; I am not a general chatbot. App updates and game art are separate network features.");
        if (Regex.IsMatch(t,@"\b(vigembus|hidhide|virtual controller|double input|two controllers in game)\b"))
            return Info("PC mode uses a virtual controller.","ViGEmBus supplies the virtual Xbox pad. HidHide can hide the physical pad so a game does not read both. Open PC mode or Tools to check setup; I cannot claim a driver is installed or working without a real check.");
        if (Regex.IsMatch(t,@"\b(settings|changes)\b.*\b(not saved|disappear|lost|not sticking)\b"))
            return Info("Check which profile you are editing.","PC mode can use per-player and per-game profiles. Check the selected player/game and the save status. PC-mode settings live on this PC; they are not firmware writes and do not travel with the controller to another console.");
        if (Regex.IsMatch(t,@"\b(macro|combo|dodge)\b") && !Regex.IsMatch(t,@"\b(paddle|m[1-6])\b"))
            return Info("The macro workbench can build a sequence.","Open Macros to choose a trigger, buttons, hold times and pauses. Offline chat cannot create arbitrary timed macros yet. Tell me the game and desired sequence and I can explain the steps; I will not pretend a macro was saved.");
        if (Regex.IsMatch(t,@"\b(paddles?|m[1-6])\b") && Regex.IsMatch(t,@"\b(why|save|program|write|macro|combo|not work)\b"))
            return Info("Onboard paddles need proven controller-specific writes.","X20 has M1–M4 and X20 Pro has M1–M6. This build cannot write hardware paddle programming. PC-mode macros and onboard paddle macros are different features; a simulated preview is not a hardware write.");
        if (Regex.IsMatch(t,@"\b(warranty|repair|broken|worn|cleaning)\b"))
            return Info("Software can compensate for some symptoms, not repair a worn part.","Measure drift in Diagnostics first. A small dead zone may hide drift but reduces small-movement response. Persistent hardware faults may need cleaning, repair or replacement; I cannot diagnose physical damage from a chat message.");
        if (Regex.IsMatch(t,@"\b(raw stick|raw input|advanced settings|advanced stick)\b"))
            return Info("Advanced stick tools are in Curves → Advanced.","They include dead zones, anti-dead zone, maximum output, vertical ratio and square gate. Raw mode bypasses stick shaping. Offline chat supports the named settings in HELP; it cannot change every advanced toggle. Reset curves restores the saved response defaults.");
        if (Regex.IsMatch(t,@"\b(latency|input lag|polling|1000hz|1000 hz|8000hz|8000 hz)\b"))
            return Info("Response curves do not change the controller's USB polling rate.","Diagnostics can report observed input activity. State changes per second are not end-to-end latency or proof of a polling specification. Curves and full-at change how movement is mapped, not the time a hardware report takes to arrive.");
        if (Regex.IsMatch(t,@"\b(circularity|roundness|margin of error|error percentage)\b"))
            return Info("Circularity error measures the stick's edge against a circle.","Open Try it or Diagnostics and roll the physical stick around its edge. The score needs enough sampled directions; keyboard previews cannot measure hardware error. It is deviation from a circle, not a statistical confidence margin.");
        if (Regex.IsMatch(t,@"\b(ps5|playstation|switch console|xbox console|other consoles)\b"))
            return Info("These saved PC-mode changes apply on this PC.","They do not reprogram the controller for another console. Console compatibility and onboard configuration require model-specific documentation and a proven hardware path.");
        if (Regex.IsMatch(t,@"\b(make|tune|improve)\b.*\b(controller|sticks?)\b.*\b(better|good)\b") && !Regex.IsMatch(t,@"\b(sniping|sniper|aim|racing|smooth|fast|precise)\b"))
            return new([],"I need one detail to choose a useful change.","",[]) { Question="Do you want steadier aim, faster turning, or help with drift?" };
        return null;
    }

    public static string FollowUp(IReadOnlyList<ChatMessage> messages, string sentence)
    {
        if (!Regex.IsMatch(sentence.Trim(),@"^(?:(?:a |just |even |make it |a little |a bit |slightly )*)(?:more|less|faster|slower|smoother|steadier)(?: please)?[.!]?$",RegexOptions.IgnoreCase)) return sentence;
        for (int i=messages.Count-2;i>=0;i--)
        {
            if (messages[i].Role!="assistant" || !messages[i].Content.Contains("[applied:")) continue;
            string applied=messages[i].Content.Split("[applied:").Last();
            string[] sticks=AgentVocabulary.Sticks.Where(t=>applied.Contains(AgentApplier.Name(t)+" curve",StringComparison.OrdinalIgnoreCase)).ToArray();
            string[] triggers=AgentVocabulary.Triggers.Where(t=>applied.Contains(AgentApplier.Name(t)+" curve",StringComparison.OrdinalIgnoreCase)).ToArray();
            var targets=sticks.Concat(triggers).Select(AgentApplier.Name).ToArray();
            if (targets.Length==0) return sentence;
            if (Regex.IsMatch(sentence,@"\b(faster|slower|smoother|steadier)\b",RegexOptions.IgnoreCase))
                return "Make my "+string.Join(" and ",targets)+" "+Regex.Replace(sentence,@"^(make it|even)\s+","",RegexOptions.IgnoreCase)+" a little";
            string previous=messages.Take(i).LastOrDefault(m=>m.Role=="user")?.Content??"";
            if (Regex.IsMatch(previous,@"\breset\b",RegexOptions.IgnoreCase)) return sentence;
            if (Regex.IsMatch(sentence,@"\bless\b",RegexOptions.IgnoreCase)) return "Make my "+string.Join(" and ",targets)+" "+(Regex.IsMatch(previous,@"snip|precis|slow|twitch|sensitive",RegexOptions.IgnoreCase)?"faster":"slower")+" a little";
            return previous+" a little more";
        }
        return sentence;
    }
}
