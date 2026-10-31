using System.Windows.Controls;

namespace X20Ctl.Product;

public sealed partial class PcModePage
{
    internal ComboBox HybridProfiles => profiles;
    internal TextBlock HybridStatus => statusLabel;
    internal TextBlock HybridStatusDetail => statusDetail;
    internal void HybridRevealNotice() => notice.BringIntoView();
}
