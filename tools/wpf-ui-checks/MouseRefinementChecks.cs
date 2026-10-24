using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product;
using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;
static class MouseRefinementChecks
{
 public static void Run(){
 var temp=Path.Combine(Path.GetTempPath(),"mouse-review-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
 using var service=new MouseModeService(Path.Combine(temp,"mouse.json"),false);
 var profile=new MouseProfile{SourceSlot=2,Speed=2400};service.SaveFor(0,"x20",profile);
 if(service.ProfileFor(0,"x20_pro").SourceSlot!=-1 || service.ProfileFor(1,"x20").SourceSlot!=-1)throw new Exception("Mouse profiles leaked across player/model");
 service.TakeControl(0,"x20");if(!service.Active||service.OwnerModel!="x20")throw new Exception("Source ownership missing");
 service.SaveFor(1,"x20_pro",new MouseProfile{SourceSlot=1});service.TakeControl(1,"x20_pro");if(service.OwnerPlayer!=1||service.Profile.Speed!=1500)throw new Exception("Source takeover reused another profile");service.Active=false;
 var catalog=new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json")));var model=catalog.Find("x20_pro")!;
 var layout=ControlRegions.Owned(model,File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/control-shapes.json")),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/x20-rear.json")));
 service.SaveFor(0,model.Id,new MouseProfile{SourceSlot=2});
 var mouse=new MouseWorkbench(service,_=>{},0,model,layout);
 var testMethod=typeof(MouseWorkbench).GetMethod("ToggleTest",BindingFlags.NonPublic|BindingFlags.Instance)!;testMethod.Invoke(mouse,null);
 if(!service.Previewing || !service.Active)throw new Exception("Contained preview failed to pause Windows output");
 typeof(MouseModeService).GetProperty("Connected")!.SetValue(service,true);typeof(MouseModeService).GetField("latest",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(service,new X20Ctl.Zone.PcMode.PadState(0,0,0,32767,32767,0,0));
 var tick=typeof(MouseWorkbench).GetMethod("Tick",BindingFlags.NonPublic|BindingFlags.Instance)!;for(int i=0;i<1000;i++)tick.Invoke(mouse,null);
 double x=(double)typeof(MouseWorkbench).GetField("px",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(mouse)!;if(x>415||x<5)throw new Exception("Preview escaped its surface");
 testMethod.Invoke(mouse,null);if(service.Previewing||service.Active)throw new Exception("Preview exit reenabled Windows output");

 var output=Path.Combine(Environment.CurrentDirectory,"artifacts","studio-refinement-20261016");Directory.CreateDirectory(output);
 foreach(var size in new[]{new Size(1500,900),new Size(1100,760)}){
 var surface=new Border{Background=new SolidColorBrush(Color.FromRgb(9,19,44)),Child=mouse.Body};surface.Measure(size);surface.Arrange(new Rect(size));surface.UpdateLayout();
 var bmp=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bmp.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(output,$"mouse-{size.Width:0}.png")))encoder.Save(file);surface.Child=null;
 }
 if(MouseWorkbench.Friendly(MouseAction.OnScreenKeyboard)!="On-screen keyboard")throw new Exception("Action display leak");
 var scene=new StudioScene(0,model,new ButtonDraft(model.Id),true){MouseService=service};scene.ShowManagement("Mouse");
 foreach(var name in new[]{"DraftBar","Apply"})if(((FrameworkElement)scene.FindName(name)).Visibility!=Visibility.Collapsed)throw new Exception("Mouse contains irrelevant "+name);
 var pcPath=Path.Combine(temp,"pc.json");var store=new X20Ctl.Zone.PcMode.PcModeStore(pcPath);store.Load();using var pc=new PcModeService(store);scene.PcMode=new(store,pc,null);
 void Render(FrameworkElement surface,string name,int width=1500,int height=900){surface.Measure(new Size(width,height));surface.Arrange(new Rect(0,0,width,height));surface.UpdateLayout();if(surface is StudioScene nav)typeof(StudioScene).GetMethod("UpdateNavigation",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(nav,null);surface.UpdateLayout();var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));png.Save(file);}
 foreach(string page in new[]{"Mouse","Device","PcMode","Diagnostics"}){
 scene.ShowManagement(page);Render(scene,page.ToLowerInvariant()+"-studio");Render(scene,page.ToLowerInvariant()+"-compact",1100,760);
 if(((FrameworkElement)scene.FindName("DraftBar")).Visibility!=Visibility.Collapsed || ((FrameworkElement)scene.FindName("StatusNote")).Visibility!=Visibility.Collapsed)throw new Exception(page+" restored duplicate chrome on resize");
 if(((FrameworkElement)scene.FindName("Apply")).Visibility!=Visibility.Collapsed)throw new Exception(page+" contains Apply");
 if(page=="Diagnostics"){if(scene.InputContext!=InputOwner.TesterCapture)throw new Exception("Diagnostics input ownership missing");scene.Diagnostics!.Stop();}
 }
 scene.ShowCurves(true);Render(scene,"curves-studio");if(((Grid)scene.FindName("StudioBody")).ColumnDefinitions[2].Width.Value<=1)throw new Exception("Curves graph not primary");
 // the X20 Pro now has hardware logs and its draft is editable, so the unverified case is a pad that has none
 var unverified=catalog.Find("x15")!;var unverifiedScene=new StudioScene(0,unverified,new ButtonDraft(unverified.Id),true);unverifiedScene.ShowVibration(true);int before=unverifiedScene.Vibration!.Draft.Strength;unverifiedScene.Vibration.Set(25);if(unverifiedScene.Vibration.Draft.Strength!=before)throw new Exception("Preview mutated unverified native model");Render(unverifiedScene,"vibration-studio");
 scene.ShowManagement("Dashboard");Render(scene,"dashboard-studio");
 typeof(StudioScene).GetMethod("UpdateNavigation",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);scene.UpdateLayout();
 var rb=(FrameworkElement)scene.FindName("NextTab");var tab=(FrameworkElement)scene.FindName("SetupsNav");if(tab.Visibility==Visibility.Visible && rb.TranslatePoint(new Point(),scene).X-tab.TranslatePoint(new Point(tab.ActualWidth,0),scene).X>60)throw new Exception("RB detached from final tab");
 var editable=new CurveWorkbench(new CurveDraft("x20"),scene,false);typeof(CurveWorkbench).GetMethod("SelectPoint",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editable,[0]);
 ((TextBox)typeof(CurveWorkbench).GetField("pointInput",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editable)!).Text="25";((TextBox)typeof(CurveWorkbench).GetField("pointOutput",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editable)!).Text="40";
 typeof(CurveWorkbench).GetMethod("CommitPoint",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editable,null);if(Math.Abs(editable.Draft[CurveChannel.LeftStick].Point1.X-64)>1||Math.Abs(editable.Draft[CurveChannel.LeftStick].Point1.Y-102)>1)throw new Exception("Percentage point entry failed");
 Console.WriteLine("PASS: read-only/immediate pages hide Apply; Diagnostics capture; Curves hierarchy; unsupported Vibration preview does not mutate draft; RB stays beside tabs; six Studio page renders.");
 Console.WriteLine("PASS: mouse profiles isolated by player/model; explicit takeover; friendly action labels; immediate-save chrome; rendered at 1500 and 1100.");
 Directory.Delete(temp,true);
 }
}
