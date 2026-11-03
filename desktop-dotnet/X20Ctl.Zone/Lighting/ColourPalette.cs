namespace X20Ctl.Zone.Lighting;

public static class ColourPalette
{
    public sealed record Preset(string Name,int Rgb);
    public static IReadOnlyList<Preset> Presets { get; } = Array.AsReadOnly<Preset>([
        new("Red",0xff0000),new("Green",0x00ff00),new("Blue",0x0000ff),new("Cyan",0x00ffff),
        new("Magenta",0xff00ff),new("Yellow",0xffff00),new("Orange",0xff8000),new("Purple",0x8000ff),new("Pink",0xff4080),new("White",0xffffff),
        new("Sky blue",0x00bfff),new("Ice blue",0x80dfff),new("Navy",0x000080),new("Teal",0x008080),new("Turquoise",0x40e0d0),new("Mint",0x80ffbf),new("Lime",0x80ff00),new("Gold",0xffd700),new("Amber",0xffbf00),new("Coral",0xff7f50),new("Salmon",0xfa8072),new("Crimson",0xdc143c),new("Hot pink",0xff1493),new("Lavender",0xb580ff),new("Indigo",0x4b0082),new("Warm white",0xffddb0),
        new("Electric blue",0x0080ff),new("Royal blue",0x4169e1),new("Aqua mint",0x00ffbf),new("Sea green",0x2e8b57),new("Emerald",0x00c060),new("Forest green",0x228b22),new("Chartreuse",0x7fff00),new("Lemon",0xfff44f),new("Peach",0xffcba4),new("Tangerine",0xff9500),new("Vermilion",0xff3f00),new("Ruby",0xe0115f),new("Rose",0xff007f),new("Raspberry",0xe30b5c),new("Burgundy",0x800020),new("Orchid",0xda70d6),new("Lilac",0xc8a2c8),new("Periwinkle",0xccccff),new("Violet",0x8f00ff),new("Electric purple",0xbf00ff),new("Ivory",0xfffff0),new("Silver",0xc0c0c0)
    ]);

    public static int FromHsv(double hue,double saturation,double value)
    {
        hue=((hue%360)+360)%360;
        saturation=Math.Clamp(saturation,0,1); value=Math.Clamp(value,0,1);
        double c=value*saturation,x=c*(1-Math.Abs((hue/60)%2-1)),m=value-c;
        var (r,g,b)=hue switch {
            <60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),
            <240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)
        };
        int Byte(double n)=>(int)Math.Round((n+m)*255);
        return (Byte(r)<<16)|(Byte(g)<<8)|Byte(b);
    }
}
