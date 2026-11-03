using System.Windows.Controls;

namespace X20Ctl.Product;

public sealed partial class PcModePage
{
    internal ComboBox ProfileList => profiles;
    internal TextBlock StatusLabel => statusLabel;
    internal TextBlock StatusDetailLabel => statusDetail;
    internal void RevealNotice() => notice.BringIntoView();
}
