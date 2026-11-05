using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class DockView
{
    private bool hollow;
    private Canvas? hollowFace;

    private void InitHollow()
    {
        if (ThemeCatalog.Current.Id != "hollow") return;
        hollow = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        hollowFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, hollowFace);
    }

    private void PresentHollow(bool hero)
    {
        if (!hollow || hollowFace == null) return;
        hollowFace.Children.Clear();

        if (hero)
        {
            hollowFace.Visibility = Visibility.Collapsed;
            return;
        }

        hollowFace.Visibility = Visibility.Visible;
    }
}
