using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Product;
using X20Ctl.Zone;
static class FollowupChecks
{
 static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
 static readonly string Output=System.IO.Path.Combine(Environment.CurrentDirectory,"artifacts","refinement-followup-20261016");
 public static void Run(){
 System.IO.Directory.CreateDirectory(Output);
 var catalog=new ControllerCatalog(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"Data/catalog.json")));var model=catalog.Find("x20")!;
 var scene=new StudioScene(0,model,new ButtonDraft(model.Id){PcMode=true},true);scene.ShowCurves(true);Layout(scene);
 var before=scene.Curves.PlotBounds;
 typeof(CurveWorkbench).GetMethod("SelectPoint",Private)!.Invoke(scene.Curves,[0]);Layout(scene);var after=scene.Curves.PlotBounds;
 Console.WriteLine($"Graph before {before.Width:0.00} x {before.Height:0.00}; selected {after.Width:0.00} x {after.Height:0.00}");
 if(Math.Abs(before.Width-after.Width)>.5||Math.Abs(before.Height-after.Height)>.5)throw new Exception("Selecting a point resizes the response graph");
 scene.Curves.SetPoint(0,100,70);Layout(scene);if(Math.Abs(scene.Curves.PlotBounds.Width-before.Width)>.5)throw new Exception("Editing a point resizes graph");Render(scene,"curves-edited");
 scene.ShowButtons();Layout(scene);var buttons=(ButtonsConsolePage)typeof(StudioScene).GetField("buttonsPage",Private)!.GetValue(scene)!;
 if(((StackPanel)typeof(ButtonsConsolePage).GetField("outputPicker",Private)!.GetValue(buttons)!).Visibility!=Visibility.Collapsed)throw new Exception("Outputs shown before source selection");
 buttons.SelectPhysical("A");if(buttons.Source!="A")throw new Exception("Physical selection does not select editor");
 typeof(ButtonsConsolePage).GetMethod("SetTarget",Private)!.Invoke(buttons,["B"]);Layout(scene);
 if(scene.Draft.Target("A")!="B")throw new Exception("Output editor did not update mapping");
 var markers=(Canvas)typeof(ButtonsConsolePage).GetField("markers",Private)!.GetValue(buttons)!;if(!markers.Children.OfType<System.Windows.Shapes.Ellipse>().Any())throw new Exception("Modified button badge missing");Render(scene,"buttons-mapped");Render(scene,"buttons-compact",1100,760);if(!Descendants<TextBlock>(buttons).Any(t=>t.Text.Contains("Buttons showcase")))throw new Exception("Buttons showcase missing");
 if(!scene.CapturingButtonEditor || scene.InputContext!=InputOwner.RemapCapture)throw new Exception("Physical mapping selection can leak navigation");
 var root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"x20ctl-followup-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(root);
 var original=BitmapSource.Create(600,900,96,96,PixelFormats.Bgra32,null,new byte[600*900*4],600*4);var imageFile=System.IO.Path.Combine(root,"portrait.png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(original));using(var stream=System.IO.File.Create(imageFile))encoder.Save(stream);
 var decoded=(BitmapSource)typeof(DashboardConsolePage).GetMethod("Bitmap",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[imageFile])!;if(decoded.PixelWidth!=600||decoded.PixelHeight!=900)throw new Exception("Game cover decoder resamples native pixels");
 var page=new DashboardConsolePage(model,0,0,0,true);var art=new Border{Child=new Image{Source=decoded}};typeof(DashboardConsolePage).GetMethod("SizeCover",Private)!.Invoke(page,[art,new TextBlock()]);if(Math.Abs(art.Width-art.Height)>.01||((Image)art.Child).Stretch!=Stretch.UniformToFill)throw new Exception("Game covers do not share a filled square frame");
 var store=new X20Ctl.Zone.PcMode.PcModeStore(System.IO.Path.Combine(root,"pc.json"));store.Load();using var pc=new PcModeService(store);PcModePage.UsePlayer(0,store);
 var advanced=new StickToolsPanel(new(store,pc,null),()=>{});Layout(advanced,1100,850);Render(advanced,"advanced-left",1100,850);advanced.SelectStick(CurveChannel.RightStick);Render(advanced,"advanced-right",1100,850);
 var cards=(System.Collections.IDictionary)typeof(StickToolsPanel).GetField("cards",Private)!.GetValue(advanced)!;if(cards.Values.Cast<Border>().Count(b=>b.Visibility==Visibility.Visible)!=1)throw new Exception("Advanced displays both stick editors");
 var rows=(System.Collections.IDictionary)typeof(StickToolsPanel).GetField("rows",Private)!.GetValue(advanced)!;
 object Row(CurveChannel channel)=>rows[channel]!;
 Slider Inner(CurveChannel channel)=>(Slider)Row(channel).GetType().GetProperty("Inner")!.GetValue(Row(channel))!;
 advanced.SelectStick(CurveChannel.LeftStick);Inner(CurveChannel.LeftStick).Value=7;typeof(StickToolsPanel).GetMethod("CopyStick",Private)!.Invoke(advanced,[CurveChannel.LeftStick,CurveChannel.RightStick]);if(Inner(CurveChannel.RightStick).Value!=7)throw new Exception("Stick copy failed");
 ((Button)typeof(StickToolsPanel).GetField("undoCopy",Private)!.GetValue(advanced)!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Inner(CurveChannel.RightStick).Value!=0)throw new Exception("Stick copy Undo failed");
 var linked=(CheckBox)typeof(StickToolsPanel).GetField("linkSticks",Private)!.GetValue(advanced)!;linked.IsChecked=true;Inner(CurveChannel.LeftStick).Value=3;if(Inner(CurveChannel.RightStick).Value!=3)throw new Exception("Linked sticks diverged");
 var row=Row(CurveChannel.LeftStick);((CheckBox)row.GetType().GetProperty("Raw")!.GetValue(row)!).IsChecked=true;if(((Panel)row.GetType().GetProperty("Tuning")!.GetValue(row)!).IsEnabled)throw new Exception("Bypass leaves processing controls enabled");
 using var mouseService=new MouseModeService(System.IO.Path.Combine(root,"mouse.json"),false);var nav=new StudioScene(0,model,new ButtonDraft(model.Id){PcMode=true},false){MouseService=mouseService,PcMode=new(store,pc,null)};var window=new Window{Content=nav,Width=1500,Height=900,Left=-10000,Top=0,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false};window.Show();Pump(30);
 typeof(StudioScene).GetMethod("UpdateNavigation",Private)!.Invoke(nav,null);Console.WriteLine("Animation enabled: "+nav.AnimationsEnabled);
 var slide=(TranslateTransform)typeof(StudioScene).GetField("navigationSlide",Private)!.GetValue(nav)!;
 if(((RadioButton)nav.FindName("DiagnosticsNav")).Visibility!=Visibility.Collapsed)throw new Exception("Diagnostics tab remains visible");
 var routes=(RadioButton[])typeof(StudioScene).GetField("navigation",Private)!.GetValue(nav)!;if(routes.Length!=9||routes.Any(t=>t.Name=="DiagnosticsNav"))throw new Exception("Diagnostics remains in tab cycling");
 nav.ShowManagement("Diagnostics");typeof(StudioScene).GetMethod("UpdateNavigation",Private)!.Invoke(nav,null);if(((FrameworkElement)nav.FindName("NavigationIndicator")).Visibility!=Visibility.Collapsed)throw new Exception("Diagnostics leaves a stale tab indicator");
 if(((Button)nav.FindName("NavigationMore")).ContextMenu!.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Diagnostics")))throw new Exception("Diagnostics remains in tab overflow");
 string[] names=["DashboardNav","ButtonsNav","CurvesNav","MacrosNav","VibrationNav","PcModeNav","DeviceNav","MouseNav","SetupsNav"];
 ((RadioButton)nav.FindName("SetupsNav")).RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));typeof(StudioScene).GetMethod("UpdateNavigation",Private)!.Invoke(nav,null);Pump(260);
 foreach(string name in names){((RadioButton)nav.FindName(name)).RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));typeof(StudioScene).GetMethod("UpdateNavigation",Private)!.Invoke(nav,null);Pump(35);typeof(StudioScene).GetMethod("UpdateNavigation",Private)!.Invoke(nav,null);if(nav.AnimationsEnabled && !slide.HasAnimatedProperties)throw new Exception(name+" animation cancelled by duplicate layout notification");Pump(230);var target=(FrameworkElement)nav.FindName(name);double wanted=target.TranslatePoint(new Point(),(FrameworkElement)nav.FindName("NavigationTrack")).X+14;if(Math.Abs(slide.X-wanted)>1)throw new Exception(name+" lens settled at wrong tab");
 var scale=(ScaleTransform)typeof(StudioScene).GetField("navigationLensScale",Private)!.GetValue(nav)!;if(scale.HasAnimatedProperties||scale.ScaleX!=1)throw new Exception("Tab switch still distorts the selector");
 var previous=(Button)nav.FindName("PreviousTab");if(previous.Visibility==Visibility.Visible){var point=previous.TranslatePoint(new Point(previous.ActualWidth/2,previous.ActualHeight/2),nav);DependencyObject? hit=nav.InputHitTest(point) as DependencyObject;bool hits=false;for(;hit!=null;hit=VisualTreeHelper.GetParent(hit)){if(ReferenceEquals(hit,previous)){hits=true;break;}}if(!hits)throw new Exception("LB label head is clipped or blocked");var next=(Button)nav.FindName("NextTab");if(Math.Abs(previous.ActualWidth-next.ActualWidth)>.1||Math.Abs(previous.ActualHeight-next.ActualHeight)>.1||Math.Abs(previous.TranslatePoint(new Point(),nav).Y-next.TranslatePoint(new Point(),nav).Y)>.1)throw new Exception("LB/RB sizing or vertical placement differs");}
 Render(nav,"matte-"+name);}
 foreach(string name in names){((RadioButton)nav.FindName(name)).RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));Pump(240);Render(nav,"compact-"+name,1100,760);}

 nav.ShowManagement("Device");Layout(nav);if(!Descendants<PhotoController>(nav.Device!.Body).Any())throw new Exception("Device does not reuse Dashboard renderer");Render(nav,"device");nav.ShowVibration(true);nav.Vibration!.SelectRegion("All");if(nav.Vibration.PreviewRegion!="All")throw new Exception("All region missing");Render(nav,"vibration-all");
 var pro=catalog.Find("x20_pro")!;var proScene=new StudioScene(0,pro,new ButtonDraft(pro.Id){PcMode=true},true){MouseService=mouseService,PcMode=new(store,pc,null)};
 proScene.ShowManagement("Device");Render(proScene,"pro-device",1920,1080);proScene.ShowCurves(true);Render(proScene,"pro-curves",1920,1080);Render(proScene,"pro-curves-compact",1100,760);
 var rim=new CyclingSupportOutline();var rimWindow=new Window{Content=rim,Width=190,Height=60,Left=-10000,Top=0,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false};rimWindow.Show();Pump(40);if(SystemParameters.ClientAreaAnimation&&!rim.IsCycling)throw new Exception("Support outline does not animate while loaded");rimWindow.Close();Pump(10);if(rim.IsCycling)throw new Exception("Support outline animation survives unload");
 window.Content=null;window.Close();
 System.IO.Directory.Delete(root,true);
 Console.WriteLine("PASS: stable edited graph; contextual mapping and physical capture; modified markers; selected advanced editor; nine animated routes survive duplicate layout; shared Device renderer; All vibration preview.");
 }
 static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T found)yield return found;foreach(var next in Descendants<T>(child))yield return next;}}
 static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static void Layout(FrameworkElement e,int w=1500,int h=900){e.Measure(new Size(w,h));e.Arrange(new Rect(0,0,w,h));e.UpdateLayout();}
 static void Render(FrameworkElement e,string name,int w=1500,int h=900){if(Window.GetWindow(e) is {} host){host.Width=w;host.Height=h;host.UpdateLayout();Pump(30);}Layout(e,w,h);if(e is StudioScene studio){typeof(StudioScene).GetMethod("UpdateNavigation",Private)!.Invoke(studio,null);studio.UpdateLayout();}var bmp=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bmp.Render(e);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=System.IO.File.Create(System.IO.Path.Combine(Output,name+".png"));png.Save(file);}
}
