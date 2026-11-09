using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>
/// Plain English for people who don't write it well (Preview 6): "i want that you make the stikk more fast", "put the
/// deadzone in ten", "how i can reset" are rewritten into the sentence the agent understands, before anything is
/// matched. Only wording changes, never meaning: a sentence that is already clear comes back as it was. The agent shows
/// the owner the rewrite ("I read that as…") so a wrong reading is seen, not hidden.
/// </summary>
public static class AgentGrammar
{
    const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    static readonly (string From, string To)[] Casual =
    [
        (@"\bu\b", "you"), (@"\bur\b", "your"), (@"\bpl[sz]+\b", "please"), (@"\bplease+\b", "please"), (@"\bthx\b", "thanks"),
        (@"\bwanna\b", "want to"), (@"\bgonna\b", "going to"), (@"\bgotta\b", "have to"),
        (@"\bdont\b", "don't"), (@"\bcant\b", "can't"), (@"\bwont\b", "won't"), (@"\bdoesnt\b", "doesn't"), (@"\bisnt\b", "isn't"),
        (@"\barent\b", "aren't"), (@"\bdidnt\b", "didn't"), (@"\bim\b", "I'm"), (@"\bwhats\b", "what's"), (@"\bthats\b", "that's"),
        (@"\bhows\b", "how's"), (@"\bcontroler\b", "controller"), (@"\bcontroler's\b", "controller's"),
        (@"\b(?:stik|stic|sitck|stcik|stikk|stiick)(s?)\b", "stick$1"), (@"\b(?:triger|trigr|trigerr)(s?)\b", "trigger$1"),
        (@"\bsensible\b", "sensitive"), (@"\bsensibility\b", "sensitivity"), (@"\bsensetive\b", "sensitive"),
    ];

    /// <summary>The slips people really make typing to a controller app (owner, 9 Nov 2026: "the most possible scenarios,
    /// including mistakes, spelling issues"): the setting words, misspelt, cut short, run together or swapped for slang.
    /// Short words are listed here because the general spell check never touches a word under five letters.</summary>
    static readonly (string From, string To)[] Typos =
    [
        // sides
        (@"\b(?:rigth|rihgt|rght|righ|rite|riht|rigt|rgiht|rigjt)\b", "right"), (@"\b(?:lft|lef|leftt|lfet|letf|lefft|leeft)\b", "left"),
        // the controls
        (@"\b(?:stik|stic|sitck|stcik|stikk|stiick|stck|sitkc|stickk|stiq)(s?)\b", "stick$1"),
        (@"\b(?:triger|trigr|trigerr|trigegr|tigger|triggr|triiger|trgger|tirgger|triggger|trigeer|trriger)(s?)\b", "trigger$1"),
        (@"\b(?:trig|trigs)\b(?=\s+(?:dead|deadzone|curve|sensitiv|full|to|at|faster|slower|smoother|on|off|and)\b|\s*$)", "triggers"),
        (@"\b(?:joystik|joystic|joystck|joysitck)(s?)\b", "joystick$1"), (@"\b(?:anolog|analgo|anlog|analouge)(s?)\b", "analog$1"),
        (@"\b(?:butten|buton|buttn|butto|buttom|botton|bttn)(s?)\b", "button$1"), (@"\b(?:bumber|bumpr|bummper)(s?)\b", "bumper$1"),
        (@"\b(?:paddel|padle|paddl|padel|paddels|padles|paddels)\b", "paddle"), (@"\b(?:paddel|padle|padel)s\b", "paddles"),
        (@"\b(?:contorller|controler|contoller|controlelr|controlller|contrller|contrlr|cntroller|controllr|controlr)(s?)\b", "controller$1"),
        (@"\b(?:dpad|d pad)\b", "d-pad"),
        // the settings
        (@"\b(?:deadzon|deadzoen|dedzone|deadzome|deazone|deadzne|deadzones|ded zone|dead zon|dead-zone|deadsone|dead zoen)\b", "dead zone"),
        (@"\b(?:dz|dzs)\b", "dead zone"), (@"\bsens\b", "sensitivity"),
        (@"\b(?:sensitiv|sensitve|senstive|sensative|sensistive|sensitivee|sensitve)\b", "sensitive"),
        (@"\b(?:sensitivty|sensitivy|sensitivit|sensativity|senstivity|sensetivity)\b", "sensitivity"),
        (@"\b(?:curv|cruve|curev|crve)(s?)\b", "curve$1"), (@"\b(?:respose|responce)\b", "response"),
        (@"\b(?:responsiv|responisve|resposive|responive)\b", "responsive"),
        (@"\b(?:turob|trubo|turbp|tubro|trbo|turbo+o|turbbo|tubo)\b", "turbo"),
        (@"\b(?:rapidfire|rapid-fire|rapdi fire|rapid fier|rapid fir)\b", "rapid fire"), (@"\b(?:autofire|auto-fire|auto fier)\b", "auto fire"),
        (@"\bspam(?:ming)?\b(?!.*\b(?:turbo|rapid|auto)\b)", "turbo on"),
        (@"\b(?:hairtrigger|hairtriggers|hair-trigger|hiar trigger|hare trigger)(s?)\b", "hair trigger$1"),
        (@"\b(?:drfting|drifitng|driftin|drifiting|drifring|drifiing|driftng|dirfting|drfiting)\b", "drifting"),
        (@"\b(?:drfit|drif|dirft|driftt|drifft)(s?)\b", "drift$1"),
        (@"\b(?:smoth|smooht|smoothe|smootj|smoooth|smoot|smoth)\b", "smooth"), (@"\b(?:smother|smoothr|smoothier|smoother+)\b", "smoother"),
        (@"\b(?:fastr|fastter|faser)\b", "faster"), (@"\b(?:slowr|slowwer|sloer)\b", "slower"),
        (@"\b(?:snappyer|snapier)\b", "snappier"), (@"\b(?:presice|percise|precize|preccise)\b", "precise"),
        // the actions
        (@"\b(?:swich|swtich|swithc|swicth|swtch|switc)\b", "switch"), (@"\b(?:swpa|sawp|swao|swpa|swapp|svap)\b", "swap"),
        (@"\b(?:remapp|reamp|rempa|remp|remmap|re-map)\b", "remap"), (@"\b(?:rset|reest|resett|rest|resset|reser|rseet)\b(?=\s+(?:all|everything|my|the|both|left|right|settings|to default|back to|[abxy]\b|lb|rb|lt|rt)\b)", "reset"),
        (@"\b(?:enabel|enalbe|enble|enabl|eneable)\b", "enable"), (@"\b(?:disabel|dissable|disble|disalbe|desable)\b", "disable"),
        (@"\b(?:trun|tunr|trn)\b", "turn"), (@"\bturn of\b", "turn off"), (@"\bswitch of\b", "switch off"),
        (@"\b(?:mkae|amke|mak|maek)\b", "make"), (@"\b(?:chnage|chagne|chang|cahnge)\b", "change"),
        (@"\b(?:defualt|deafult|defult|defalt|dafault|default+)\b", "default"),
        (@"\b(?:evrything|everyting|evrythin|everthing|everythign|evreything|everyhting|everythin|evrythng)\b", "everything"),
        (@"\b(?:setings|settigns|settins|setttings|stetings|seetings)\b", "settings"), (@"\b(?:seting|settign)\b", "setting"),
        // the games
        (@"\b(?:snipping|snieping|sniepr|snipper)\b", "sniping"), (@"\b(?:crosshiar|crosshar|crossheir|crosshare|cross hair)\b", "crosshair"),
        (@"\b(?:charachter|charcter|caracter|charecter|chracter|charakter)\b", "character"), (@"\b(?:camra|camrea|cmaera)\b", "camera"),
        // pc mode
        (@"\bp\.?\s*c\.?\s*(?=mod)", "pc "), (@"\bpc mod\b", "pc mode"), (@"\bpc-mode\b", "pc mode"),
        // everyday slips
        (@"\bteh\b", "the"), (@"\badn\b", "and"), (@"\bnad\b", "and"), (@"\bwiht\b", "with"), (@"\bmy\s+my\b", "my"),
        (@"\bso+o\b", "so"), (@"\bto+o+\b", "too"), (@"\bon\s*/\s*off\b", "on off"), (@"\bya\b", "you"), (@"\bcuz\b", "because"),
        (@"\bthru\b", "through"), (@"\brn\b", "right now"), (@"\bidk\b", "i don't know"), (@"\bsth\b", "something"),
        // "to slow", "to sensitive": too, when an adjective follows and nothing it could be a destination of
        // round two (owner, 9 Nov 2026: "add more")
        (@"\b(?:every thing|evry thing|everythig|everyting|evrythig|eveything|everythng|evertyhing)\b", "everything"),
        (@"\bn\b(?=\s+[a-z])", "and"), (@"\b&\b|\s&\s", " and "), (@"\bw/\s*", "with "), (@"\bw/o\b", "without"),
        (@"\brs\b(?!\s+(?:click|press|button))", "right stick"), (@"\bls\b(?!\s+(?:click|press|button))", "left stick"),
        (@"\b(swap|switch|flip|change) around\b", "$1"), (@"\b(swap|switch|flip) over\b", "$1"),
        (@"\bmake (\w+) do what (\w+) does\b", "make $1 act like $2"), (@"\bmake (\w+) do (?:the same as|what) (\w+)\b", "make $1 act like $2"),
        (@"\b(?:abit|alil|a lil|a tad)\b", "a bit"), (@"\b(?:alot|allot)\b", "a lot"), (@"\b(?:abt|bout)\b", "about"),
        (@"\b(?:becuase|becasue|coz|cuz)\b", "because"), (@"\b(?:pleas|pleae|plase|pleasee|plsss+|plss)\b", "please"),
        (@"\b(?:thnx|thanx|tnx|ty)\b", "thanks"), (@"\b(?:wat|wut|wht)\b", "what"), (@"\b(?:hw|hwo)\b", "how"),
        (@"\b(?:dosent|doesnt|dosnt|doesn't)\b", "doesn't"), (@"\b(?:dont|dnt|don't)\b", "don't"), (@"\b(?:wont|wnt)\b(?= work)", "won't"),
        (@"\b(?:aint)\b", "isn't"), (@"\b(?:gimme)\b", "give me"), (@"\b(?:lemme)\b", "let me"), (@"\b(?:kinda|sorta)\b", ""),
        (@"\b(?:aiming|aimin)\b", "aiming"), (@"\b(?:shoting|shootin|shotting)\b", "shooting"), (@"\b(?:walkin|wlaking|waking)\b(?= by| on| alone)", "walking"),
        (@"\b(?:movin|moveing|mvoing)\b", "moving"), (@"\b(?:turnin|turing|trning)\b", "turning"), (@"\b(?:breaking)\b(?= in| for| when| feels| is)", "braking"),
        (@"\b(?:accelaration|acceleraton|acelleration|accelleration)\b", "acceleration"), (@"\b(?:throtle|throttel|trottle)\b", "throttle"),
        (@"\b(?:vibraton|vibraiton|vibartion)\b", "vibration"), (@"\b(?:rumbel|rumle)\b", "rumble"),
        (@"\b(?:mous mode|mousemod|mouse mod)\b", "mouse mode"),
        (@"\b(?:bumpers?)\b(?=\s+(?:swap|switch))", "bumpers"),
        (@"\b(?:screen|view|pov)\b(?= (?:keeps|is|moves|moving|spins|spinning|drifts|drifting|turns|turning|slides|creeps))", "camera"),
        (@"\bmy (?:player|guy|dude|man|soldier|car|operator|toon)\b(?= (?:keeps|is|moves|walks|walking|runs|running|goes|drifts|creeps|slides))", "my character"),
        (@"\b(?:walks|moves|goes|runs) (?:alone|on (?:his|her|its|their) own|by (?:him|her|it|them)sel(?:f|ves))\b", "walks by itself"),
        (@"\bto (?=(?:slow|fast|quick|high|low|much|little|sensitive|jumpy|shaky|twitchy|floaty|sluggish|stiff|loose|heavy|light|big|small|strong|weak|hard|soft|sharp|jittery|laggy|snappy|touchy)\b)", "too "),
    ];

    // two words run together, or spelt apart in a way that hides the setting
    static readonly (string From, string To)[] Glued =
    [
        (@"\b(left|right)(stick|trigger)(s?)\b", "$1 $2$3"), (@"\bpcmode\b", "pc mode"), (@"\bmousemode\b", "mouse mode"),
        (@"\b(left|right)\s+(?:analog|analogue|thumb\s*stick|joy\s*stick)\b", "$1 stick"),
        (@"\bhair\s*trigger\b", "hair trigger"), (@"\bdead\s*zone(s?)\b", "dead zone$1"),
    ];

    static readonly Dictionary<string, string> Comparative = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fast"] = "faster", ["slow"] = "slower", ["smooth"] = "smoother", ["high"] = "higher", ["low"] = "lower", ["big"] = "bigger",
        ["small"] = "smaller", ["quick"] = "quicker", ["strong"] = "stronger", ["weak"] = "weaker", ["tight"] = "tighter",
        ["loose"] = "looser", ["sharp"] = "sharper", ["soft"] = "softer", ["hard"] = "harder", ["easy"] = "easier",
        ["light"] = "lighter", ["heavy"] = "heavier", ["short"] = "shorter", ["long"] = "longer", ["steady"] = "steadier",
        ["snappy"] = "snappier", ["stiff"] = "stiffer", ["wide"] = "wider", ["narrow"] = "narrower", ["gentle"] = "gentler",
    };

    static readonly string Verbs = "change|set|make|turn|switch|enable|disable|swap|remap|reset|increase|decrease|reduce|raise|lower|open|show|measure|fix|adjust|put";
    static readonly string Adjectives = "faster|slower|smoother|snappier|bigger|smaller|higher|lower|stronger|weaker|tighter|looser|sharper|softer|harder|quicker|steadier|stiffer|wider|narrower|gentler|more|less|sensitive|precise|accurate|responsive";

    static readonly Dictionary<string, int> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16,
        ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
        ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90,
    };
    static readonly string NumberWord = string.Join("|", Units.Keys);
    static readonly Regex NumberRun = new($@"\b(?:(?:a|one)\s+)?hundred\b|\b(?:{NumberWord})(?:[\s-]+(?:one|two|three|four|five|six|seven|eight|nine))?\b", I);

    /// <summary>The sentence in plain English: wording tidied, meaning untouched.</summary>
    public static string Plain(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return sentence;
        string s = sentence.Trim().Replace('’', '\'');
        s = Regex.Replace(s, @"\s+", " ");
        s = Regex.Replace(s, @"([!?.,])\1+", "$1");
        s = Regex.Replace(s, @"\b(\w+)(?:\s+\1\b)+", "$1", I);                 // "the the stick"
        foreach (var (from, to) in Typos) s = Regex.Replace(s, from, to, I);
        foreach (var (from, to) in Casual) s = Regex.Replace(s, from, to, I);
        foreach (var (from, to) in Glued) s = Regex.Replace(s, from, to, I);
        // "put the dead zone in ten" -> "set the dead zone to ten", while the number is still a word
        if (Regex.IsMatch(s, @"\b(?:set|put|change|adjust|make|use)\b", I))
            s = Regex.Replace(s, @"(?:\b(?:in|on|at|with|as|into|equals?)|=|:)\s*(?=\d|(?:" + NumberWord + @")\b)", "to ", I);
        s = Numbers(s);
        s = Regex.Replace(s, @"(\d)\s*(?:per\s*cent|percents?|pct)\b", "$1%", I);
        s = Regex.Replace(s, @"(\d)\s+%", "$1%");
        // "more fast" -> "faster", "more smooth" -> "smoother"
        s = Regex.Replace(s, @"\b(?:more|most)\s+(" + string.Join("|", Comparative.Keys) + @")\b", m => Comparative[m.Groups[1].Value], I);
        // "how i can reset" -> "how can i reset"; "is possible to" -> "is it possible to"
        s = Regex.Replace(s, @"\b(how|what|why|where|when) (i|we|you) (can|could|should|must)\b", "$1 $3 $2", I);
        s = Regex.Replace(s, @"\b(is|was) possible to\b", "$1 it possible to", I);
        // asking for a thing: "i want that you make", "can you to make", "i need you to", "please can you"
        s = Regex.Replace(s, @"\b(?:i|we) (?:want|need|would like|wish|like) (?:that )?you (?:to |can |could )?", "", I);
        s = Regex.Replace(s, @"\b(?:please |pls )?(?:can|could|would|will) you (?:please )?(?:to )?", "", I);
        s = Regex.Replace(s, @"^(?:please |kindly )?(?:you )?(?:can|should|must) (?=(?:" + Verbs + @")\b)", "", I);
        // "i want to change the dead zone" -> "change the dead zone"
        s = Regex.Replace(s, @"^(?:i |we )?(?:want|need|would like|wish|like|am trying|try)(?: to| that)?\s+(?=(?:" + Verbs + @")\b)", "", I);
        // "i want the right stick faster" -> "make the right stick faster"
        s = Regex.Replace(s, @"^(?:i |we )?(?:want|need|would like|wish|like)(?: that)?\s+(?=\S.*\b(?:stick|sticks|trigger|triggers|aim|camera|movement|buttons?|turn(?:ing)?)\b.*\b(?:" + Adjectives + @")\b)(?!make\b)", "make ", I);
        s = Regex.Replace(s, @"\b(make|let|keep)((?: (?:the|my|it|them|both|left|right|[a-z]+)){1,5}?) (?:to be|be|is|are|to) (?=(?:" + Adjectives + @")\b)", "$1$2 ", I);
        // "i no want", "i not want", "i dont want" -> "i don't want"
        s = Regex.Replace(s, @"\b(i|we) (?:no|not|never) (want|need|like)s?\b", "$1 don't $2", I);
        s = Regex.Replace(s, @"\bput\b(?=.*(?:\d|dead zone|stick|trigger))", "set", I);
        // the pad that won't pair or isn't found: the words the connection help recognises
        s = Regex.Replace(s, @"\b(?:do(?:es)?n't|do(?:es)? not|can't|cannot|won't|will not|never|isn't|is not|not able to|unable to|no|not)\s+(?:to\s+)?(?:connect(?:ing|ed|s)?|pair(?:ing|ed)?)\b", "won't connect", I);
        s = Regex.Replace(s, @"\b(?:do(?:es)?n't|do(?:es)? not|can't|cannot|never|isn't|is not|not able to|unable to|no|not)\s+(?:to\s+)?(?:detect(?:ed|ing)?|recogni[sz]e[ds]?|show(?:ing)? up|found|appear(?:ing)?)\b", m => Regex.IsMatch(m.Value, @"^(?:is )?not detected$", I) ? m.Value : "not detected", I);
        // "swap the buttons a and b", "button x": a lone letter that names a face button is written as one
        s = Regex.Replace(s, @"\b(buttons?)\s+([abxy])\b(?:\s+(and|with|to|for)\s+([abxy])\b)?", m => m.Groups[1].Value + " " + m.Groups[2].Value.ToUpperInvariant()
            + (m.Groups[3].Success ? " " + m.Groups[3].Value + " " + m.Groups[4].Value.ToUpperInvariant() : ""), I);
        if (Regex.IsMatch(s, @"\b(?:swap|switch|exchange|remap|replace|change|trade)\b", I))
            s = Regex.Replace(s, @"\b([abxy])\s+(and|with|to|for)\s+([abxy])\b", m => m.Groups[1].Value.ToUpperInvariant() + " " + m.Groups[2].Value + " " + m.Groups[3].Value.ToUpperInvariant(), I);
        // "swap the buttons A and B" -> "swap A and B"
        s = Regex.Replace(s, @"\b(swap|switch|exchange)\s+(?:the\s+|my\s+)?buttons?\s+(?=[ABXY]\b)", "$1 ", I);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>How the agent read it, when that is more than tidying: null when the sentence only differs in case, politeness
    /// or punctuation.</summary>
    public static string? Reading(string said, string plain)
    {
        static string Skeleton(string t) => Regex.Replace(Regex.Replace(t.ToLowerInvariant().Replace("'", ""), @"\b(please|pls|plz|thanks|thx|kindly|hey|hi|hello)\b", " "), @"[^a-z0-9%]+", " ").Trim();
        if (Skeleton(said) == Skeleton(plain)) return null;
        string text = plain.TrimEnd('.', '!', '?', ' ');
        return text.Length == 0 ? null : char.ToUpperInvariant(text[0]) + text[1..];
    }

    static string Numbers(string s) => NumberRun.Replace(s, m =>
    {
        string run = m.Value.ToLowerInvariant();
        int value;
        if (run.EndsWith("hundred")) value = 100;
        else
        {
            var parts = run.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
            value = parts.Sum(p => Units[p]);
        }
        bool big = value >= 10;
        string before = Regex.Match(s[..m.Index], @"(\S+)\s*$").Groups[1].Value.ToLowerInvariant();
        string after = s[(m.Index + m.Length)..];
        bool cue = before is "to" or "at" or "by" or "than" or "around" or "about" or "equals" or "=" or ":"
                or "zone" or "output" or "ratio" or "limit" or "cap" or "rate" or "sensitivity" or "speed"
            || Regex.IsMatch(after, @"^\s*(?:%|percent|per\s*cent|points?\b|hz\b|times\b)", I);
        return big || cue ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : m.Value;
    });
}
