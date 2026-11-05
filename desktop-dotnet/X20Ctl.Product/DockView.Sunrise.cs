using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class DockView
{
    private bool sunrise;
    private Canvas? sunriseFace;

    private void InitSunrise()
    {
        if (ThemeCatalog.Current.Id != "sunrise") return;
        sunrise = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        sunriseFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, sunriseFace);
    }

    private void PresentSunrise(bool hero)
    {
        if (!sunrise || sunriseFace == null) return;
        sunriseFace.Children.Clear();

        if (hero)
        {
            sunriseFace.Visibility = Visibility.Collapsed;
            return;
        }

        sunriseFace.Visibility = Visibility.Visible;
    }
}
