namespace X20Ctl.Zone;

/// <summary>Turns a macro record read from an X20 Pro (entry count, loop bytes, then five bytes per entry) into events.
/// Key positions follow the macro key list the pad reports; a press and the rest after it become one event.</summary>
public static class MacroRecord
{
    // Bits 8 upward of the entry mask. Bits 0-7 are the two stick nibbles, where 8 means idle and 0-7 a direction.
    static readonly string[] Keys=["DPAD_DOWN","DPAD_UP","DPAD_RIGHT","DPAD_LEFT","A","B","X","Y","LB","RB","LT","RT","L3","R3","SELECT","START"];
    const int EntrySize=5,TickMs=5;

    public static List<MacroEvent> Parse(byte[] raw)
    {
        if(raw.Length==0)throw new FormatException("The macro record is empty.");
        int count=raw[0];
        if(count==0)return [];
        if(raw.Length!=3+EntrySize*count)throw new FormatException("The macro record has an unexpected length.");
        var events=new List<MacroEvent>();
        for(int i=0;i<count;i++)
        {
            int at=3+i*EntrySize,mask=raw[at]|raw[at+1]<<8|raw[at+2]<<16,ms=(raw[at+3]|raw[at+4]<<8)*TickMs;
            var buttons=Keys.Where((_,bit)=>(mask>>(bit+8)&1)!=0).ToArray();
            StickHeading left=Heading(mask&0xf),right=Heading(mask>>4&0xf);
            bool rest=buttons.Length==0&&left==StickHeading.Neutral&&right==StickHeading.Neutral;
            if(rest&&events.Count>0)events[^1]=events[^1] with{PauseMs=events[^1].PauseMs+ms};
            else events.Add(new(Guid.NewGuid(),Array.AsReadOnly(buttons),left,right,ms,0));
        }
        return events;
    }
    /// <summary>One stored step: the mask low byte first, then the time in 5 ms ticks.</summary>
    public static byte[] Entry(IEnumerable<string> buttons,StickHeading left,StickHeading right,int ms)
    {
        int mask=Nibble(left)|Nibble(right)<<4,ticks=ms/TickMs;
        foreach(var button in buttons)mask|=1<<(Array.IndexOf(Keys,button)+8);
        return [(byte)mask,(byte)(mask>>8),(byte)(mask>>16),(byte)ticks,(byte)(ticks>>8)];
    }
    static int Nibble(StickHeading heading)=>heading==StickHeading.Neutral?8:(int)heading-1;
    static StickHeading Heading(int nibble)=>nibble==8?StickHeading.Neutral:(StickHeading)((nibble&7)+1);
}
