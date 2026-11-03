using System;

namespace X20Ctl.Product;

/// <summary>The clock every themed status bar shows: the real time, frozen only for review renders.</summary>
internal static class ThemeClock
{
    public static string Now => ReviewSandbox.Active ? "19:24" : DateTime.Now.ToString("HH:mm");
}
