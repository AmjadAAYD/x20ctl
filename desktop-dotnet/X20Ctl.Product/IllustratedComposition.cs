using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product;

// Reparent the existing controls, retaining their events, drafts, accessibility and capability gates.
internal static class IllustratedComposition
{
    internal static void Detach(FrameworkElement element)
    {
        if(element.Parent is Panel panel)panel.Children.Remove(element);
        else if(element.Parent is Decorator decorator)decorator.Child=null;
        else if(element.Parent is ContentControl content)content.Content=null;
    }
    internal static Grid Grid(double[] columns,double[] rows)
    {
        var grid=new Grid();
        foreach(double size in columns)grid.ColumnDefinitions.Add(new(){Width=new(size,GridUnitType.Star)});
        foreach(double size in rows)grid.RowDefinitions.Add(new(){Height=new(size,GridUnitType.Star)});
        return grid;
    }
    internal static void Put(Grid grid,FrameworkElement element,int row,int column,int rows=1,int columns=1)
    {
        Detach(element);System.Windows.Controls.Grid.SetRow(element,row);System.Windows.Controls.Grid.SetColumn(element,column);
        System.Windows.Controls.Grid.SetRowSpan(element,rows);System.Windows.Controls.Grid.SetColumnSpan(element,columns);
        grid.Children.Add(element);
    }
    internal static Border Plate(FrameworkElement child,string tag,double padding=16)
    {
        Detach(child);var spec=IllustratedThemeUI.Current!;
        return new Border{Tag="illustrated:"+tag,Child=child,Padding=new(padding),Margin=new(6),CornerRadius=new(spec.Radius),
            Background=IllustratedThemeUI.Brush(ThemeCatalog.Alpha(IllustratedThemeUI.C(spec.Deep),220)),
            BorderBrush=IllustratedThemeUI.Brush(ThemeCatalog.Current.Rule),BorderThickness=new(spec.Surface=="paper"?2:1)};
    }
    internal static ScrollViewer Scroll(FrameworkElement content)
    {
        Detach(content);return new(){Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    }
}
