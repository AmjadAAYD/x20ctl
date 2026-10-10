namespace X20Ctl.Product;

/// <summary>Set only by the development stress harness: while it is on, nothing leaves the app. Support links,
/// game launches and the Import / Export file dialogs become no-ops, so a run that clicks every button can never
/// open the browser, start a game or block on a modal dialog.</summary>
internal static class ReviewSandbox
{
    public static bool Active { get; set; }
    /// <summary>README screenshots: the game shelf stays empty, so no one's real library is pictured.</summary>
    public static bool HideLibrary { get; set; }
}
