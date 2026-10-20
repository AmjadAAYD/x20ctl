using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using X20Ctl.Linux;

// X20Ctl.Linux.Review --shots DIRECTORY [WIDTHxHEIGHT]: the window's own --shots run, drawn by Skia on the headless platform
// X20Ctl.Linux.Review --stress REPORT: clicks and keys through the app, mid-animation, and checks it stays whole (Stress.cs)
// Nothing here opens a window on the screen.
int[] size = args.Length == 3 && args[0] == "--shots" ? args[2].Split('x').Select(int.Parse).ToArray() : [1600, 900];
if (args.Length is not (2 or 3) || args[0] is not ("--shots" or "--stress") || size.Length != 2) { Console.Error.WriteLine("usage: X20Ctl.Linux.Review --shots DIRECTORY [WIDTHxHEIGHT] | --stress REPORT"); return 2; }
ReviewMode.HideLibrary = true; ReviewMode.Headless = true;
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();
var window = new MainWindow { Width = size[0], Height = size[1] };
window.Show();
// animations run on the window's render clock; headless, that clock only ticks when told to, so tick it at 60 Hz
DispatcherTimer.Run(() => { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); return true; }, TimeSpan.FromMilliseconds(1000 / 60.0));
var run = args[0] == "--shots" ? window.Shots(args[1]) : Stress.Run(window, args[1]);
using var stop = new CancellationTokenSource();
_ = run.ContinueWith(_ => Dispatcher.UIThread.Post(stop.Cancel));
Dispatcher.UIThread.MainLoop(stop.Token);
if (run.Exception is { } failure) { Console.Error.WriteLine(failure); return 1; }
Console.WriteLine("written to " + Path.GetFullPath(args[1]));
return 0;
