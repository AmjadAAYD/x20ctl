namespace X20Ctl.Product;

/// <summary>Presentation-only manifest shared with the asset builder and routing checks.</summary>
public sealed record IllustratedThemeSpec(string Id,string Name,string Folder,string Prefix,
    uint Void,uint Deep,uint Accent,uint Secondary,uint Text,string Display,double Radius,string Surface)
{
    /// <summary>Native skins keep Default's own layout and pages; only colour, type, shapes, panels and backgrounds change.
    /// Otherwise the theme replaces the zone, buttons, curves, tools and studio shell with fixed-canvas layouts.</summary>
    public bool Native{get;init;}
    public static readonly IReadOnlyList<IllustratedThemeSpec> All = new[]
    {
        new IllustratedThemeSpec("channel","Channel","channel","channel",0x0A0A0A,0x161614,0xFFE11A,0xFFF06A,0xF4F1E6,"Bahnschrift Condensed, Segoe UI",4,"p4"){Native=true},
        new IllustratedThemeSpec("undertow","Undertow","undertow","undertow",0x062AA0,0x061E6E,0x8FF0FF,0xFFFFFF,0xFFFFFF,"Bahnschrift Condensed, Segoe UI",0,"p3"){Native=true},
        new IllustratedThemeSpec("alert","Alert","alert","alert",0x0C0716,0x160C2A,0xB4F03C,0xC9B3FF,0xEFEAFF,"Bahnschrift Condensed, Segoe UI",0,"alert"){Native=true},
        new IllustratedThemeSpec("slate","Slate","slate","slate",0x0A222B,0x08202A,0xFF8A1C,0x3FE0FF,0xF1F8F9,"Bahnschrift Light, Segoe UI",2,"slate"){Native=true},
        new IllustratedThemeSpec("multiverse","Multiverse","multiverse","multiverse",0x060506,0x0F0D10,0xFF1F3D,0xF4F1F1,0xF4F1F1,"Bahnschrift Condensed, Segoe UI",0,"spider"){Native=true}
    };
    public static IllustratedThemeSpec? Find(string? id)=>All.FirstOrDefault(t=>t.Id==id);
    public static readonly IReadOnlyList<(string Name,string Slug)> Pages = new (string,string)[]
    {
        ("Intro","intro"),("Controller Zone","controller-zone"),("Model selector","model-selector"),
        ("Dashboard","dashboard"),("Buttons","buttons"),("Curves","curves"),("Macros","macros"),
        ("Vibration","vibration"),("PC mode","pc-mode"),("RGB","rgb"),("Screen","screen"),
        ("Device","device"),("Mouse","mouse"),("Setups","setups"),("Diagnostics","diagnostics"),
        ("Tester","tester"),("Tools","tools"),("Agent XCTL","agent")
    };
    public string Wallpaper(string page)
    {
        static string Normalize(string value)=>value.Replace(" ","").Replace("-","").ToLowerInvariant();
        string key=Normalize(page);
        key=key switch {"zone"=>"controllerzone","selector"=>"modelselector","agent"=>"agentxctl","themes" or "about" or "feedback" or "readscan" or "writescan"=>"tools",_=>key};
        int index=Enumerable.Range(0,Pages.Count).FirstOrDefault(i=>Normalize(Pages[i].Name)==key,-1);
        if(index<0)throw new ArgumentException("No illustrated artwork is assigned to page "+page,nameof(page));
        return $"Brand/illustrated/{Id}/wallpapers/{Prefix}-{index+1:00}-{Pages[index].Slug}.jpg";
    }
    public string Logo(string variant="mark")
    {
        if(variant is not ("mark" or "full" or "mono"))throw new ArgumentException("Unknown logo variant",nameof(variant));
        return $"Brand/illustrated/{Id}/logo/{Prefix}-logo-{variant}.png";
    }
}
