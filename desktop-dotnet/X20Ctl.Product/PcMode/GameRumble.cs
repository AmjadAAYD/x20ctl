namespace X20Ctl.Product;

/// <summary>The last vibration a game asked of each virtual pad in PC mode, and what the real pad got, for the Vibration
/// tab's live readout. Written from ViGEm's feedback thread, read by the UI; nothing else depends on it.</summary>
public static class GameRumble
{
    public sealed record Sample(int GameLeft, int GameRight, int PadLeft, int PadRight, long At);
    static readonly Sample?[] last = new Sample?[4];

    internal static void Record(int slot, byte gameLeft, byte gameRight, int padLeft, int padRight)
    {
        if (slot is >= 0 and < 4) Volatile.Write(ref last[slot], new Sample(gameLeft, gameRight, padLeft, padRight, Environment.TickCount64));
    }

    /// <summary>The newest sample from any pad, or null when no game has asked for vibration.</summary>
    public static Sample? Latest => Enumerable.Range(0, 4).Select(i => Volatile.Read(ref last[i])).Where(s => s != null).MaxBy(s => s!.At);
}
