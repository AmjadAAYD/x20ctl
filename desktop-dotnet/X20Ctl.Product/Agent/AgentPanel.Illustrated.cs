using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildIllustratedAgent(Border screen)
    {
        // Rehost the existing chat, ideas, check-up, settings, input and send actions.
        FeedbackPage.Detach(screen);screen.Clip=null;screen.Width=984;screen.Height=631;
        var board=new Grid{Width=1120,Height=760};board.RowDefinitions.Add(new(){Height=new(76)});board.RowDefinitions.Add(new());
        IllustratedThemeUI.Surface(board,"Agent XCTL");
        var header=new DockPanel{Margin=new(30,18,30,12)};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        void Action(string text,System.Action action)
        {var button=new Button{Content=text,Style=DS.Style("DS.Secondary"),Margin=new(6,0,0,0),Padding=new(14,8,14,8)};button.Click+=(_,_)=>action();register(button);actions.Children.Add(button);}
        Action("Undo",UndoLast);Action("Ideas",()=>ShowView(View.Ideas));Action("Settings now",()=>ShowSettingsPage(!SettingsOpen));Action("Close",()=>CloseRequested?.Invoke());
        DockPanel.SetDock(actions,Dock.Right);header.Children.Add(actions);
        header.Children.Add(new TextBlock{Text="Agent XCTL",FontFamily=new FontFamily(ThemeCatalog.Current.Display),FontSize=26,Foreground=IllustratedThemeUI.Brush(ThemeCatalog.Current.Text),VerticalAlignment=VerticalAlignment.Center});
        board.Children.Add(header);
        var content=new Viewbox{Stretch=Stretch.Uniform,Margin=new(26,0,26,22),Child=screen};Grid.SetRow(content,1);board.Children.Add(content);
        Body.Child=new Viewbox{Stretch=Stretch.Uniform,Child=board};Body.Background=IllustratedThemeUI.Brush(ThemeCatalog.Current.Void);
        Body.CornerRadius=new(ThemeCatalog.Current.Radius);IllustratedThemeUI.QueueSkin(board);
    }
}
