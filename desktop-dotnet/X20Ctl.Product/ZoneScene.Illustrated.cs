using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product;

public partial class ZoneScene
{
    private IllustratedDesignPage? matchedZone;
    private readonly Dictionary<int,Viewbox> matchedPlayerArt=new();
    private readonly Dictionary<int,string> matchedPlayerModels=new();
    private readonly Dictionary<int,Button> matchedPlayerButtons=new();
    private readonly List<(Button button,string page)> matchedZoneNavigation=[];
    private void RefreshIllustratedZone()
    {
        if(!IllustratedThemeUI.Matched)return;
        if(matchedZone==null)
        {
            matchedZone=new("zone",slot=>
            {
                if(slot.Key.StartsWith("player:")&&int.TryParse(slot.Key[7..],out int index))
                {
                    var button=new Button{Tag="illustrated:player:"+index,ToolTip="Open or assign this player. Right-click for controller options."};
                    matchedPlayerButtons[index]=button;
                    button.Click+=(_,_)=>ControllerActionRequested?.Invoke(index,false);
                    var menu=new ContextMenu();
                    void Item(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}
                    Item("Switch controller",()=>ControllerActionRequested?.Invoke(index,true));
                    Item("Remove controller",()=>RemoveController(index));
                    Item("Change color",async()=>{await docks[index].ChangeColour();RefreshIllustratedZone();});
                    button.ContextMenu=menu;return button;
                }
                if(slot.Key.StartsWith("nav:"))
                {string page=slot.Key[4..];var button=new Button();matchedZoneNavigation.Add((button,page));button.Click+=(_,_)=>{if(page=="Tools")SettingsRequested?.Invoke();else if(page=="Controller Zone")_ = FocusPlayerAsync(null);else{int player=State.FocusedPlayer??Enumerable.Range(0,4).FirstOrDefault(i=>State.Players[i].Model!=null,-1);if(player>=0)StudioPageRequested?.Invoke(player,page);}};return button;}
                if(slot.Key.StartsWith("chrome:"))
                {
                    var button=new Button();System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button,true);
                    button.Click+=(_,_)=>{if(Window.GetWindow(this) is not {} window)return;if(slot.Key=="chrome:close")window.Close();else if(slot.Key=="chrome:minimize")SystemCommands.MinimizeWindow(window);else window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;};return button;
                }
                return null;
            });
            matchedZone.TextValue=(key,text)=>
            {
                if(key=="clock")return DateTime.Now.ToString("HH:mm");
                if(key=="player-name")return State.FocusedPlayer is int focused?$"Player {focused+1}":"All players";
                if(key=="player-number")return State.FocusedPlayer is int number?$"{number+1}":"4";
                if(key=="controller-state")return "Controller Zone";
                if(text.Contains("Controller offline")&&key.Length==0)return State.Players.Any(p=>p.Model!=null)?"Input status unavailable":"No controller assigned";
                var pair=key.Split(':');if(pair.Length!=2||!int.TryParse(pair[1],out int i))return text;
                var player=State.Players[i];
                return pair[0] switch
                {
                    "player-model"=>player.ModelId?.Replace('_',' ').ToUpperInvariant()??(text.Trim()=="X20"?"Unassigned":text),
                    "player-status"=>player.Model==null?"No controller assigned":player.IsMockConnected?"Connected · mock":"Input status unavailable",
                    "player-action"=>player.Model==null?"+ Add controller":"Open studio",_=>text
                };
            };
            foreach(var slot in matchedZone.Pads)
            {int index=int.Parse(slot.Key[11..]);var art=new Viewbox{Stretch=Stretch.Uniform,IsHitTestVisible=false};matchedPlayerArt[index]=art;matchedZone.Place(art,slot);}
            Surface.Children.Add(new Viewbox{Child=matchedZone,Stretch=Stretch.Uniform});
        }
        foreach(var (index,art) in matchedPlayerArt)
        {
            var player=State.Players[index];string id=player.ModelId??"x20",key=id+"/"+PadColours.For(index,id);
            if(!matchedPlayerModels.TryGetValue(index,out var old)||old!=key)
            {
                int? previous=PadColours.ActivePlayer;PadColours.ActivePlayer=index;
                try{art.Child=new PhotoController(id);}finally{PadColours.ActivePlayer=previous;}
                matchedPlayerModels[index]=key;
            }
            art.Opacity=player.Model==null?.13:1;
        }
        foreach(var (button,page) in matchedZoneNavigation)button.IsEnabled=page is "Tools" or "Controller Zone"||State.Players.Any(p=>p.Model!=null);
        matchedZone.Refresh();
    }
    private int FocusedDockIndex()
    {
        if(matchedZone==null)return Array.FindIndex(docks,d=>d.IsKeyboardFocusWithin);
        foreach(var pair in matchedPlayerButtons)if(pair.Value.IsKeyboardFocusWithin)return pair.Key;
        return -1;
    }
}
