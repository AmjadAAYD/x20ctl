namespace X20Ctl.Zone.Telemetry;

public static class NativeTelemetry
{
    public static string? PreferMeasuredBattery(string? measured,string? incoming)=>measured??incoming;
    public static byte[]? Reply(byte[] raw,byte expectedSerial)
    {
        var frame=X20Ctl.Zone.Capture.KeyLinkerCodec.TryDecode(raw);
        return frame is {CrcValid:true,Opcode:0x19} && frame.DeclaredLength==raw.Length && frame.Serial==expectedSerial ? frame.Payload : null;
    }
    public sealed record Power(int Level,bool Charging,byte RawStatus);
    public sealed record Hardware(string HardwareId,string ModelCode,string Firmware);
    public static Power ParsePower(byte[] payload)
    {
        if(payload.Length<5||payload[0]!=payload.Length-1||payload[1]!=0xdf||payload[2]!=0xaa)
            throw new ArgumentException("Incomplete or unrelated power reply.");
        byte status=payload[4];
        int level=(status&0x20)!=0?2:(status&0x40)!=0?3:(status&0x80)!=0?4:1;
        return new(level,(status&0x10)!=0,status);
    }
    public static Hardware ParseHardware(byte[] record)
    {
        if(record.Length!=18)throw new ArgumentException("Expected a complete 18-byte hardware record.");
        int version=record[16]|(record[17]<<8);
        return new(Convert.ToHexString(record[..16]),Convert.ToHexString(record[14..16]),$"{version/100}.{version%100:00}");
    }
}
