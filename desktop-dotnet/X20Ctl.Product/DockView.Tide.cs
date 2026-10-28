using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public partial class DockView
{
    private bool tide;
    private Canvas? tideFace;

    private void InitTide()
    {
        if (ThemeCatalog.Current.Id != "tide") return;
        tide = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        tideFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, tideFace);
    }

    private void PresentTide(bool hero)
    {
        if (!tide || tideFace == null) return;
        tideFace.Children.Clear();

        if (hero)
        {
            tideFace.Visibility = Visibility.Collapsed;
            return;
        }

        tideFace.Visibility = Visibility.Visible;
    }
}
