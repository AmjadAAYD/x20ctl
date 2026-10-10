using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed class ProductWindow : Window
{
    public ZoneScene Scene { get; }
    public ProductHost Host { get; }
    public int DpiTransitionCount {get;private set;}
    public bool StartsMaximized {get;}
    /// <summary>Big Picture style: maximized over the whole monitor (taskbar covered), not just the work area.</summary>
    public bool Fullscreen {get;}
    private TrayIcon? tray;
    public DesignStage Stage { get; } = new();
    private readonly WindowChrome chrome = new(){CaptionHeight=58,ResizeBorderThickness=new(0),GlassFrameThickness=new(0),CornerRadius=new(16),UseAeroCaptionButtons=false};
    public ProductWindow(bool reducedMotion=false,string? assignmentPath=null,bool startMaximized=true,bool firstRun=false,Action<string>? openSupport=null,bool nativeEngine=false,bool fullscreen=false,bool trayIcon=false)
    {
        StartsMaximized=startMaximized;Fullscreen=fullscreen;Title="X20CTL · Controller Studio";
        // a fixed design (1600 x 900, scaled to the window) and a fixed window: it cannot be resized, only made fullscreen
        Width=1440;Height=810;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanMinimize;WindowStartupLocation=WindowStartupLocation.Manual;
        Fx.RegisterPressFeedback();Background=(System.Windows.Media.Brush)Application.Current.FindResource("Ink");Scene=new ZoneScene(reducedMotion);Host=new ProductHost(Scene,assignmentPath,openSupport);
        Stage.Child=Host;Content=Stage;Stage.ScaleChanged+=scale=>chrome.CaptionHeight=58*scale; // the title bar drag area scales with the design
        if(firstRun)Host.Startup(); // before the window ever shows: starting on Loaded (after the first render) flashed the zone
        WindowChrome.SetWindowChrome(this,chrome);
        SourceInitialized+=(_,_)=>
        {
            var hwnd=new WindowInteropHelper(this).Handle;int preference=2;_=DwmSetWindowAttribute(hwnd,33,ref preference,sizeof(int));HwndSource.FromHwnd(hwnd)?.AddHook(WindowMessages);
            GetCursorPos(out var cursor);var monitor=MonitorFromPoint(cursor,2);var info=ReadMonitor(monitor);
            if(info!=null)
            {
                double dpi=96;if(GetDpiForMonitor(monitor,0,out uint x,out _)==0)dpi=x;
                var work=Bounds(info.Value.Work);var size=WindowedSize(work,dpi);SetWindowPos(hwnd,IntPtr.Zero,work.Left+(work.Width-size.Width)/2,work.Top+(work.Height-size.Height)/2,size.Width,size.Height,0x14);
            }
            if(startMaximized)WindowState=WindowState.Maximized;
        };
        PreviewKeyDown+=(_,e)=>{if((e.Key==System.Windows.Input.Key.System?e.SystemKey:e.Key)==InputMap.Fullscreen){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;e.Handled=true;}};PreviewKeyDown+=Host.OnKeyDown;
        Host.HomeRequested+=BringHome;PreviewKeyUp+=Host.OnKeyUp;Deactivated+=(_,_)=>Host.ResetTransientInput();
        StateChanged+=(_,_)=>{Scene.OuterFrame.CornerRadius=new(WindowState==WindowState.Maximized?0:16);if(WindowState==WindowState.Maximized)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(ApplyWorkArea));else if(WindowState==WindowState.Normal)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(ApplyWindowedSize));};
        // it always opens fullscreen (F11 or the restore button still give a window for those who want one)
        Loaded+=(_,_)=>{if(StartsMaximized&&WindowState!=WindowState.Maximized)WindowState=WindowState.Maximized;if(WindowState==WindowState.Maximized)ApplyWorkArea();if(nativeEngine)_=Host.InitializeNativeEngineAsync();};
        Closed+=async(_,_)=>{tray?.Dispose();await Host.StopNativeEngineAsync();};
        if(trayIcon){tray=new TrayIcon(this);Closing+=(_,e)=>{if(tray.Quitting)return;e.Cancel=true;tray.HideToTray();};}
    }
    /// <summary>Home / Guide: show X20CTL fullscreen in front of everything (from the tray too), then go to the Dashboard.</summary>
    public void BringHome()
    {
        bool wasHidden=!IsVisible||WindowState==WindowState.Minimized||!IsActive;
        if(tray!=null&&!IsVisible)tray.Restore();else{Show();if(WindowState!=WindowState.Maximized)WindowState=WindowState.Maximized;}
        // a background process may not take the foreground directly; a brief Topmost flip is the standard workaround
        Topmost=true;Activate();Topmost=false;Focus();
        if(!wasHidden||Host.Studio!=null){if(Host.Studio!=null)Host.Studio.ShowManagement("Dashboard");}
    }
    private IntPtr WindowMessages(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==0x02e0)
        {
            DpiTransitionCount++; // PerMonitorV2 WPF handles the suggested rectangle and root DPI.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(WindowState==WindowState.Maximized)ApplyWorkArea();}));return IntPtr.Zero;
        }
        if(message!=0x24)return IntPtr.Zero;
        var info=ReadMonitor(MonitorFromWindow(hwnd,2));if(info==null)return IntPtr.Zero;
        var metrics=WindowPresentation.Maximize(Bounds(info.Value.Monitor),Bounds(Fullscreen?info.Value.Monitor:info.Value.Work),GetDpiForWindow(hwnd));
        var limits=Marshal.PtrToStructure<MinMaxInfo>(lParam);limits.MaxPosition=new(){X=metrics.X,Y=metrics.Y};limits.MaxSize=new(){X=metrics.Width,Y=metrics.Height};limits.MinTrackSize=new(){X=320,Y=180};Marshal.StructureToPtr(limits,lParam,false);handled=true;return IntPtr.Zero;
    }
    /// <summary>The windowed size (device pixels): exactly the 1600 x 900 design when it fits comfortably on the screen, so
    /// everything shows at its true size; on a smaller screen, the largest 16:9 window that fits in 90% of the work area.</summary>
    private static PixelBounds WindowedSize(PixelBounds work,double dpi)
    {
        double design=DesignStage.DesignWidth*dpi/96,fit=Math.Min(work.Width*.9,work.Height*.9*16/9),w=Math.Min(design,fit);
        int width=(int)Math.Round(w),height=(int)Math.Round(w*9/16);
        return new(0,0,width,height);
    }
    /// <summary>Back from fullscreen, the window always returns to its fixed 16:9 size, centred on its monitor.</summary>
    private void ApplyWindowedSize()
    {
        if(WindowState!=WindowState.Normal)return;var hwnd=new WindowInteropHelper(this).Handle;var info=ReadMonitor(MonitorFromWindow(hwnd,2));if(info==null)return;
        var work=Bounds(info.Value.Work);var size=WindowedSize(work,GetDpiForWindow(hwnd));SetWindowPos(hwnd,IntPtr.Zero,work.Left+(work.Width-size.Width)/2,work.Top+(work.Height-size.Height)/2,size.Width,size.Height,0x14);
    }
    private void ApplyWorkArea()
    {
        if(WindowState!=WindowState.Maximized)return;var hwnd=new WindowInteropHelper(this).Handle;var info=ReadMonitor(MonitorFromWindow(hwnd,2));if(info==null)return;
        var work=Bounds(Fullscreen?info.Value.Monitor:info.Value.Work);GetWindowRect(hwnd,out var current);if(current.Left!=work.Left||current.Top!=work.Top||current.Right-current.Left!=work.Width||current.Bottom-current.Top!=work.Height)SetWindowPos(hwnd,IntPtr.Zero,work.Left,work.Top,work.Width,work.Height,0x34);
    }
    public object NativeSnapshot()
    {
        var hwnd=new WindowInteropHelper(this).Handle;GetWindowRect(hwnd,out var rect);var info=ReadMonitor(MonitorFromWindow(hwnd,2));return new{state=WindowState.ToString(),dpi=GetDpiForWindow(hwnd),window=Bounds(rect),work=info.HasValue?Bounds(info.Value.Work):null,DpiTransitionCount};
    }
    public static IReadOnlyList<PixelBounds> AvailableWorkAreas()
    {
        var result=new List<PixelBounds>();EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr monitor,IntPtr dc,ref NativeRect rect,IntPtr data)=>{var info=ReadMonitor(monitor);if(info.HasValue)result.Add(Bounds(info.Value.Work));return true;},IntPtr.Zero);return result;
    }
    private static PixelBounds Bounds(NativeRect r)=>new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
    private static MonitorInfo? ReadMonitor(IntPtr monitor){var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};return GetMonitorInfo(monitor,ref info)?info:null;}
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MinMaxInfo{public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    private delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref NativeRect rect,IntPtr data);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
}
