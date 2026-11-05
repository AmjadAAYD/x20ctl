using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class DockView
{
    private bool nocturne;
    private Canvas? nocturneFace;

    private void InitNocturne()
    {
        if (ThemeCatalog.Current.Id != "nocturne") return;
        nocturne = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        nocturneFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, nocturneFace);
    }

    private void PresentNocturne(bool hero)
    {
        if (!nocturne || nocturneFace == null) return;
        nocturneFace.Children.Clear();

        if (hero)
        {
            nocturneFace.Visibility = Visibility.Collapsed;
            return;
        }

        nocturneFace.Visibility = Visibility.Visible;
    }
}
