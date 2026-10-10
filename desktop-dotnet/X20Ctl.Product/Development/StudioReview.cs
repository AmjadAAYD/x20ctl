using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

internal static class StudioReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new StringWriter(); using var listener = new TextWriterTraceListener(log);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var checks = new List<object>(); var shots = new List<object>(); var frames = new List<BitmapFrame>(); var motion = new List<object>();
        void Check(bool passed, string name) { checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException(name); }
        string persistencePath = Path.Combine(directory, "review-assignments-" + Guid.NewGuid().ToString("N") + ".json");
        var window = new ProductWindow(false, persistencePath,false);
        try
        {
            window.Show(); await Ready();
            await window.Scene.FocusPlayerAsync(2); window.Scene.Docks[2].Primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
            Check(window.Host.Selector?.Player == 2, "selector belongs to clicked Player 3");
            window.Host.Selector!.ChooseModel("x20"); await Ready();
            Check(window.Scene.State.Players[2].ModelId == "x20" && window.Scene.State.Players.Where((p,i)=>i!=2).All(p=>p.Model==null), "only Player 3 assigned");
            Check(new AssignmentStore(persistencePath).Load()[2] == "x20", "assignment persistence separate from hardware");
            window.Scene.Docks[2].Primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
            var studio = window.Host.Studio!;
            Check(studio.Player==2 && !studio.Apply.IsEnabled && !studio.Draft.HardwareStateKnown, "exact player/model context with no write authority");
            studio.ClearSelection(); await Capture("01-front-no-selection.png");
            foreach(var (key,file) in new[]{("A","02-front-A.png"),("DPAD_UP","03-front-dpad-up.png"),("LB","04-front-LB.png"),("LT","05-front-LT.png"),("L3","06-front-L3.png"),("SELECT","07-front-view.png"),("START","08-front-menu.png"),("HOME","09-front-home.png"),("CAPTURE","10-front-C.png"),("TURBO","11-front-T.png"),("LSTICK_ANALOG","12-front-left-axis.png"),("RSTICK_ANALOG","13-front-right-axis.png")})
            { studio.SelectControl(key); await Capture(file); }
            Check(!studio.TargetChoice.IsEnabled, "analog/system controls inspectable but not writable");
            studio.SelectControl("A"); studio.TargetChoice.SelectedValue="B"; await Ready();
            Check(studio.Draft.UnsentChanges==1 && studio.Draft.Target("A")=="B" && !studio.Apply.IsEnabled,"A to B is a local unsent draft");
            await Capture("14-front-A-to-B.png");
            studio.ShowAssignments(true); await Capture("15-all-assignments.png"); Check(studio.AssignmentCount==14 && studio.AssignmentRows.Children.Count==4,"full mapping overview grouped by physical category"); studio.ShowAssignments(false);
            studio.ShowController(true); studio.ClearSelection(); await Capture("16-back-no-selection.png");
            foreach(var (key,file) in new[]{("M1","17-back-M1.png"),("M2","18-back-M2.png"),("M3","19-back-M3.png"),("M4","20-back-M4.png")})
            { studio.SelectControl(key); await Capture(file); Check(!studio.TargetChoice.IsEnabled,"macro "+key+" remains read only"); Check(studio.DraftRows.Children.OfType<TextBlock>().All(t=>!t.Text.Contains("A → B")) && studio.DraftRows.Children.OfType<TextBlock>().Any(t=>t.Text.Contains("Not read")),key+" inspector contains only selected macro context"); }
            Check(studio.SystemInventory.Children.OfType<Button>().Select(b=>b.Tag as string).SequenceEqual(new[]{"HOME"}) && studio.SystemInventory.Children.OfType<Button>().All(b=>b.IsEnabled),"Guide inspectable; Capture and Turbo are onboard functions and stay out of remapping");
            Check(studio.AssignmentCount+studio.InventoryOverview.Children.Count+studio.SystemInventory.Children.Count==23,"overview exposes all 23 remappable physical roles (Capture and Turbo are onboard functions)");
            studio.SelectControl("A"); studio.FocusSelectedControl();
            var keyEvent=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,0,Key.F6){RoutedEvent=Keyboard.PreviewKeyDownEvent};
            studio.OnKeyDown(window,keyEvent); await Capture("21-console-focus.png"); Check(Keyboard.FocusedElement is PhysicalHotspot,"geometry focus visible");
            var output = studio.TargetButtons.Single(b=>(string)b.Tag=="B"); output.Focus(); await Capture("22-detached-target-focus.png"); Check(Keyboard.FocusedElement==output,"detached target focus");
            studio.FocusSelectedControl(); SendKey(Key.Enter); Check(Keyboard.FocusedElement == output,"confirm physical input focuses its current draft output"); SendKey(Key.B); Check(Keyboard.FocusedElement is PhysicalHotspot,"back from output shelf returns to physical map"); output.Focus();
            window.Width=1040;window.Height=700;await Ready();Check(studio.ControllerCanvas.ActualWidth>=300,"minimum controller remains usable");
            File.WriteAllText(Path.Combine(directory,"target-sizes.json"),JsonSerializer.Serialize(studio.TargetButtons.Select(b=>new{key=b.Tag,width=b.ActualWidth,height=b.ActualHeight}),new JsonSerializerOptions{WriteIndented=true}));
            Check(studio.TargetButtons.Count==16 && studio.TargetButtons.All(b=>b.ActualWidth>=49.5 && b.ActualHeight>=41.5),"sixteen spatial targets retain usable minimum size within half-DIP layout rounding");await Capture("23-minimum.png");
            Check(studio.OutputTile.TransformToAncestor(studio.MappingPair).TransformBounds(new Rect(studio.OutputTile.RenderSize)).Bottom<=studio.MappingPair.ActualHeight+.5,"minimum inspector displays complete output glyph without clipping");
            studio.ShowAssignments(true); await Capture("25-minimum-assignments.png"); studio.ShowAssignments(false);
            window.WindowState=WindowState.Maximized;await Capture("24-maximized.png");Check(studio.Apply.IsVisible && studio.ChangeCount.IsVisible,"maximized footer retained");
            studio.SelectControl("M1");await Capture("26-maximized-rear.png");
            window.WindowState=WindowState.Normal;window.Width=1220;window.Height=800;await Ready();
            // Capture actual intermediate WPF animation states, without desktop input injection.
            var timer=Stopwatch.StartNew();
            foreach(string key in new[]{"A","B","DPAD_UP","LB","LT","L3","SELECT","START","HOME","CAPTURE","TURBO","M1","M2","M3","M4","A"})
            {
                studio.SelectControl(key);
                for(int i=0;i<4;i++)
                {
                    if(studio.Draft.CanEdit(key) && i==2) studio.TargetButtons.Single(b=>(string)b.Tag==studio.Draft.Target(key)).Focus();
                    await Task.Delay(55);window.UpdateLayout();frames.Add(BitmapFrame.Create(Render(.7)));
                    motion.Add(new{timeMs=timer.Elapsed.TotalMilliseconds,key,frame=frames.Count});
                }
            }
            studio.SetDraftTarget("X"); await Task.Delay(180);frames.Add(BitmapFrame.Create(Render(.7)));
            var gif=new GifBitmapEncoder();foreach(var frame in frames)gif.Frames.Add(frame);
            using(var stream=File.Create(Path.Combine(directory,"console-selection-sequence.gif")))gif.Save(stream);SetGifDelays(Path.Combine(directory,"console-selection-sequence.gif"),6);
            window.Host.ReturnToZone();window.Host.EnterStudio(2);Check(window.Host.Studio!.Draft.UnsentChanges==1,"draft retained across Zone/Studio");
            window.Host.ReturnToZone();window.Host.OpenSelector(1);window.Host.Selector!.ChooseModel("x15");window.Host.EnterStudio(1);await Ready();
            Check(!window.Host.Studio!.Apply.IsEnabled,"research model never gets native writes");
            window.Host.ReturnToZone();window.Host.EnterStudio(2);studio=window.Host.Studio!;studio.ShowCurves(true);await Ready();
            Check(studio.IsCurves && studio.SelectedControl=="LSTICK_ANALOG" && !studio.Apply.IsEnabled,"Curves opens on owned left stick with no write authority");
            Check(!studio.OverviewChoice.IsVisible && !studio.FrontChoice.IsVisible && !studio.RearChoice.IsVisible && !studio.HardwareShortcuts.IsVisible,"Curves omits inherited remapping and view controls");
            Check(studio.ResetCurveChoice.IsVisible && (string)studio.ResetCurveChoice.Content=="Reset curve" && !studio.ResetAllChoice.IsVisible && (string)studio.ResetAllCurvesAction.Header=="Reset all curves","current reset and secondary global reset have explicit scope");
            Check(studio.Curves.OutputAxisLabel.Text=="OUTPUT %" && studio.Curves.PreviewNotice.Text.StartsWith("Preview") && !studio.Curves.PreviewNotice.Text.Contains("Firmware"),"plot output axis and human-facing preview copy present");
            Check(studio.Curves.Buttons.Any(b=>Equals(b.Content,"L Stick")) && studio.Curves.Buttons.Any(b=>Equals(b.Content,"R Stick")),"stick channel labels understandable without LS RS acronyms");
            studio.Curves.SetPreset("quick");await Capture("28-curves-left-stick-quick.png");
            studio.Curves.Inner.Value=12;studio.Curves.Outer.Value=85;await Capture("29-curves-deadzones.png");
            Check(studio.Curves.Draft[CurveChannel.LeftStick].BasePreset=="quick" && studio.Curves.PresetDescription.Contains("based on Quick"),"deadzone modifications retain and describe original preset");
            var leftCurve=studio.Curves.Draft[CurveChannel.LeftStick];
            studio.Curves.SelectChannel(CurveChannel.RightStick);studio.Curves.SetPreset("fine");await Capture("30-curves-right-stick.png");
            Check(studio.Curves.Draft[CurveChannel.LeftStick]==leftCurve && studio.Curves.Draft[CurveChannel.RightStick].Point2==(72,106),"right stick edits retain independent left stick draft");
            studio.Curves.SelectChannel(CurveChannel.LeftTrigger);studio.Curves.SetPreset("smooth");await Capture("31-curves-left-trigger.png");Check(studio.IsRear && studio.SelectedControl=="LT","trigger channel illuminates actual owned rear contour");
            studio.Curves.SelectChannel(CurveChannel.RightTrigger);studio.Curves.SetPreset("slow");await Capture("32-curves-right-trigger.png");
            studio.Curves.SetPoint(0,62,40);studio.Curves.SetPoint(1,180,210);await Capture("33-curves-custom.png");
            Check(studio.Curves.Draft[CurveChannel.RightTrigger].Preset=="custom" && studio.Curves.PreviewPoints.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&p.Y>=15.5&&p.Y<=256.5),"custom curve illustration remains finite and inside graph range");
            SendKey(Key.F6);studio.Curves.Buttons.First().Focus();await Capture("34-curves-console-focus.png");
            studio.Curves.PointHandles.First().Focus();int pointY=studio.Curves.Draft[studio.Curves.Channel].Point1.Y;SendKey(Key.Up);Check(studio.Curves.Draft[studio.Curves.Channel].Point1.Y==pointY+1,"focused curve point adjusts with keyboard without navigating away");
            Check(studio.Curves.SelectedPoint==0 && studio.Curves.ActivePointReadout.Text.Contains("Input") && studio.Curves.ActivePointReadout.Text.Contains("Output"),"selected point has contextual input and output readout");await Capture("40-curves-point-1.png");
            studio.Curves.PointHandles.Last().Focus();await Capture("41-curves-point-2.png");Check(studio.Curves.SelectedPoint==1,"point 2 gets its own active context");
            window.Width=1040;window.Height=700;await Capture("35-curves-minimum.png");Check(studio.Curves.Inner.ActualWidth>350 && studio.Curves.Inspector.IsVisible,"minimum Curves retains editor and useful sliders");
            Check(studio.Curves.ActivePointReadout.ActualWidth>250 && studio.Curves.PreviewNotice.TransformToAncestor(studio.Curves.Inspector).TransformBounds(new Rect(studio.Curves.PreviewNotice.RenderSize)).Bottom<=studio.Curves.Inspector.ActualHeight+.5,"minimum active-point context and preview copy remain inside inspector");
            Check(studio.Curves.PlotBounds.Width>=230,"selected-point context retains minimum graph width");
            window.WindowState=WindowState.Maximized;await Capture("36-curves-maximized.png");window.WindowState=WindowState.Normal;window.Width=1220;window.Height=800;await Ready();
            studio.Curves.Inner.Focus();int oldInner=studio.Curves.Draft[studio.Curves.Channel].Inner;SendKey(Key.Right);Check(studio.Curves.Draft[studio.Curves.Channel].Inner==oldInner+1 && studio.Curves.SelectedPoint==null,"deadzone keyboard adjustment is one percentage point and clears point context");await Capture("42-curves-numeric-adjustment.png");
            SendKey(Key.B);Check(window.Host.Studio==studio && Keyboard.FocusedElement is PhysicalHotspot,"back from curve adjustment returns to controller without leaving Studio");
            int modifiedCurves=studio.Curves.Draft.ModifiedChannels;window.Host.ReturnToZone();window.Host.EnterStudio(2);studio=window.Host.Studio!;
            Check(studio.Curves.Draft.ModifiedChannels==modifiedCurves && studio.Draft.UnsentChanges==1,"both draft types retained separately across Studio visits");
            studio.ShowCurves(true);var retainedRightTrigger=studio.Curves.Draft[CurveChannel.RightTrigger];studio.ResetCurveChoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(studio.Curves.Draft[studio.Curves.Channel]==CurveSetting.Default && studio.Curves.Draft[CurveChannel.RightTrigger]==retainedRightTrigger,"Reset curve action retains other channel drafts");await Capture("37-curves-reset.png");
            studio.ResetAllCurvesAction.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(studio.Curves.Draft.ModifiedChannels==0,"secondary Reset all curves clears all four local channels");await Capture("43-curves-reset-all.png");
            studio.ShowCurves(false);await Capture("38-return-to-buttons.png");Check(!studio.IsCurves && studio.FrontChoice.IsVisible && studio.RearChoice.IsVisible && studio.OverviewChoice.IsVisible && studio.ResetAllChoice.IsVisible && !studio.ResetCurveChoice.IsVisible && studio.FrontChoice.IsEnabled && !studio.Apply.IsEnabled,"return to Buttons restores approved actions without hardware authority");
            var curveFrames=new List<BitmapFrame>();studio.ShowCurves(true);
            foreach(var (channel,preset) in new[]{(CurveChannel.LeftStick,"default"),(CurveChannel.LeftStick,"quick"),(CurveChannel.RightStick,"fine"),(CurveChannel.LeftTrigger,"smooth"),(CurveChannel.RightTrigger,"slow"),(CurveChannel.LeftStick,"default")})
            {
                studio.Curves.SelectChannel(channel);studio.Curves.SetPreset(preset);studio.Curves.Buttons.First(b=>b.Tag is CurveChannel c && c==channel).Focus();
                for(int i=0;i<6;i++){await Task.Delay(55);window.UpdateLayout();curveFrames.Add(BitmapFrame.Create(Render(.7)));}
            }
            studio.ShowCurves(false);for(int i=0;i<6;i++){await Task.Delay(55);window.UpdateLayout();curveFrames.Add(BitmapFrame.Create(Render(.7)));}
            var curveGif=new GifBitmapEncoder();foreach(var frame in curveFrames)curveGif.Frames.Add(frame);using(var stream=File.Create(Path.Combine(directory,"curves-selection-sequence.gif")))curveGif.Save(stream);SetGifDelays(Path.Combine(directory,"curves-selection-sequence.gif"),6);
            studio.ShowMacros(true);var macro=studio.Macros!;await Capture("44-macros-empty.png");
            Check(studio.IsMacros && macro.Draft["M1"].Count==0 && !macro.Record.IsEnabled && !studio.Apply.IsEnabled,"Macros opens empty with recording and hardware Apply unavailable");
            macro.AddEventButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));macro.InputButtons.Single(b=>Equals(b.Tag,"B")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));macro.Update(macro.Draft["M1"][0] with {HoldMs=120,PauseMs=40,Left=StickHeading.Up});
            macro.Add();macro.Update(macro.Draft["M1"][1] with {Buttons=new[]{"RB"},HoldMs=80,PauseMs=20,Right=StickHeading.Right});macro.Add();macro.Update(macro.Draft["M1"][2] with {Buttons=Array.Empty<string>(),Left=StickHeading.Neutral,Right=StickHeading.Neutral,HoldMs=60,PauseMs=0});await Capture("45-macros-sequence.png");
            Check(macro.Draft.Duration("M1")==320 && macro.Draft.EntryCount("M1")==5 && macro.Draft["M1"][0].Buttons.Contains("B"),"macro chords directions hold and release pauses remain local events");
            macro.ShowEditor("Timing");await Capture("55-macros-timing.png");macro.ShowEditor("Actions");await Capture("56-macros-actions.png");
            macro.Duplicate();Check(macro.Draft["M1"].Count==4 && macro.Draft["M1"][2].Id!=macro.Draft["M1"][3].Id,"duplicate event has separate identity");macro.Remove();
            macro.ShowEditor("Inputs");macro.ShowInputKind(true);macro.LeftSelector.Focus();SendKey(Key.Right);Check(macro.Draft["M1"][macro.SelectedIndex].Left==StickHeading.Right,"circular stick selector edits programmed direction through keyboard");await Capture("57-macros-stick-selectors.png");SendKey(Key.Home);macro.ShowInputKind(false);
            Check(!macro.Record.IsVisible && macro.EditorMode=="Inputs","recording stays subordinate and editing views progressively reveal controls");
            macro.Select(0);macro.Move(1);Check(macro.Draft["M1"][1].Left==StickHeading.Up,"macro reorder preserves the selected event");await Capture("46-macros-selected-event.png");
            foreach(string slot in new[]{"M2","M3","M4"}) {macro.SelectSlot(slot);macro.Add();await Capture("47-macros-"+slot+".png");}
            Check(macro.Draft["M1"].Count==3 && macro.Draft["M2"].Count==1 && macro.Draft["M3"].Count==1 && macro.Draft["M4"].Count==1,"four macro slots retain independent sequences");
            macro.SelectSlot("M1");SendKey(Key.F6);macro.FocusSelection();await Capture("48-macros-console-focus.png");
            window.Width=1040;window.Height=700;await Capture("49-macros-minimum.png");Check(macro.Dock.IsVisible && macro.Body.ActualWidth>900,"minimum macro sequence and contextual dock fit the full workspace");window.WindowState=WindowState.Maximized;await Capture("50-macros-maximized.png");window.WindowState=WindowState.Normal;window.Width=1220;window.Height=800;await Ready();
            var macroFrames=new List<BitmapFrame>();macro.PreviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(macro.IsPreviewing && !studio.Apply.IsEnabled,"timeline preview starts without hardware authority");
            Check(macro.PlayheadVisible && macro.PreviewIndex==0 && macro.HighlightedLaneCount>0,"preview playhead and corresponding lane highlights start together");
            var visitedEvents=new HashSet<int>{macro.PreviewIndex};
            for(int i=0;i<12;i++){await Task.Delay(40);window.UpdateLayout();if(macro.PreviewIndex>=0)visitedEvents.Add(macro.PreviewIndex);macroFrames.Add(BitmapFrame.Create(Render(.7)));}
            Check(visitedEvents.Count>=2 && macro.SelectedIndex==0,"preview follows events without changing editing selection");
            Check(!macro.IsPreviewing,"timeline preview stops after one local sequence");
            foreach(string slot in new[]{"M2","M3","M4","M1"}) {macro.SelectSlot(slot);for(int i=0;i<4;i++){await Task.Delay(55);window.UpdateLayout();macroFrames.Add(BitmapFrame.Create(Render(.7)));}}
            var macroGif=new GifBitmapEncoder();foreach(var frame in macroFrames)macroGif.Frames.Add(frame);using(var stream=File.Create(Path.Combine(directory,"macros-sequence-preview.gif")))macroGif.Save(stream);SetGifDelays(Path.Combine(directory,"macros-sequence-preview.gif"),6);
            macro.Preview();studio.ShowCurves(true);Check(!macro.IsPreviewing && studio.IsCurves && !studio.IsMacros,"leaving Macros cancels timeline preview and restores Curves");
            studio.ShowMacros(true);macro.SelectSlot("M4");
            foreach(int count in new[]{10,20,47})
            {
                macro.Draft.Clear("M4");for(int i=0;i<count;i++)macro.Draft.Add("M4",count==47?0:20);macro.Refresh();macro.Select(count-1);await Capture($"58-macros-{count}-events.png");
                Check(macro.EventButtons.Count()==count && macro.EventButtons.All(b=>b.ActualWidth>=189.5 && b.ActualHeight>=51.5),$"{count} events keep fixed readable card dimensions");
                Check(macro.EventShelfOffset>0 && macro.SelectedCardInView,$"{count} event shelf reveals the complete selected card in both dimensions");
            }
            Check(macro.Draft.EntryCount("M4")==47 && macro.CapacityDescription.Contains("47 / 47") && !macro.AddEventButton.IsEnabled,"full reference capacity visible and further additions disabled");
            window.Width=1040;window.Height=700;macro.Select(46);await Capture("59-macros-47-minimum.png");Check(macro.SelectedCardInView && macro.EventButtons.All(b=>b.ActualWidth>=189.5),"minimum layout preserves and reveals last of 47 event cards");
            window.WindowState=WindowState.Maximized;await Capture("60-macros-47-maximized.png");window.WindowState=WindowState.Normal;window.Width=1220;window.Height=800;await Ready();macro.SelectSlot("M1");
            window.Host.ReturnToZone();window.Host.EnterStudio(2);studio=window.Host.Studio!;studio.ShowMacros(true);Check(studio.Macros!.Draft["M1"].Count==3 && studio.Curves.Draft.ModifiedChannels>0 && studio.Draft.UnsentChanges==1,"all three independent draft types retained across Studio visits");await Capture("51-macros-retained.png");
            studio.ShowVibration(true);var vibration=studio.Vibration!;await Capture("63-vibration-default.png");Check(studio.IsVibration && !studio.IsMacros && !vibration.Test.IsEnabled && !studio.Apply.IsEnabled,"Vibration opens as a local linked draft with hardware actions disabled");
            vibration.PresetButtons.Single(b=>Equals(b.Tag,25)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("64-vibration-25.png");vibration.Strength.Value=100;await Capture("65-vibration-100.png");vibration.Strength.Value=0;await Capture("66-vibration-off.png");
            Check(vibration.Draft.Strength==0 && vibration.Readback.Text.Contains("—"),"zero local strength never claims hardware off or a readback");
            vibration.Strength.Focus();SendKey(Key.Right);Check(vibration.Draft.Strength==1,"vibration slider supports one percentage-point keyboard adjustment");SendKey(Key.F6);vibration.FocusPreset();await Capture("67-vibration-console-focus.png");
            vibration.Set(75);window.Width=1040;window.Height=700;await Capture("68-vibration-minimum.png");Check(vibration.Strength.ActualWidth>800 && vibration.Test.IsVisible && !vibration.Test.IsEnabled,"minimum Vibration retains broad strength control and explicit unavailable hardware test");window.WindowState=WindowState.Maximized;await Capture("69-vibration-maximized.png");window.WindowState=WindowState.Normal;window.Width=1220;window.Height=800;await Ready();
            window.Host.ReturnToZone();window.Host.EnterStudio(2);studio=window.Host.Studio!;studio.ShowVibration(true);Check(studio.Vibration!.Draft.Strength==75 && studio.Macros==null && studio.Curves.Draft.ModifiedChannels>0,"vibration draft retained separately across Studio visits");studio.VibrationResetChoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!studio.Vibration.Draft.Modified && !studio.Apply.IsEnabled,"vibration reset restores only its local default");await Capture("70-vibration-reset.png");
            studio.ShowMacros(true);Check(studio.IsMacros && !studio.IsVibration && studio.Macros!.Draft["M1"].Count==3,"return from Vibration restores frozen Macro draft");
            var vibrationFrames=new List<BitmapFrame>();studio.ShowVibration(true);
            foreach(int percent in new[]{0,25,50,75,100,70}){studio.Vibration!.Set(percent);studio.Vibration.FocusPreset();for(int i=0;i<4;i++){await Task.Delay(55);window.UpdateLayout();vibrationFrames.Add(BitmapFrame.Create(Render(.7)));}}
            var vibrationGif=new GifBitmapEncoder();foreach(var frame in vibrationFrames)vibrationGif.Frames.Add(frame);using(var stream=File.Create(Path.Combine(directory,"vibration-draft-sequence.gif")))vibrationGif.Save(stream);SetGifDelays(Path.Combine(directory,"vibration-draft-sequence.gif"),6);
            var reduced = new ProductWindow(true,persistencePath,false);
            try
            {
                reduced.Show();await reduced.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);reduced.Host.EnterStudio(2);var reducedStudio=reduced.Host.Studio!;
                reducedStudio.SelectControl("A");reducedStudio.SetDraftTarget("B");reducedStudio.SelectControl("M1");
                Check(!reducedStudio.AnimationsEnabled && reducedStudio.MappingHero.Opacity==1 && reducedStudio.ControllerCanvas.RenderTransform.Value.IsIdentity,"reduced motion uses immediate view and inspector state");
                await Task.Delay(240);reduced.UpdateLayout();
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(reduced.Host.ActualWidth),(int)Math.Ceiling(reduced.Host.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(reduced.Host);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(directory,"27-reduced-motion.png")))encoder.Save(stream);
                shots.Add(new{file="27-reduced-motion.png",width=bitmap.PixelWidth,height=bitmap.PixelHeight});
                reducedStudio.ShowCurves(true);reducedStudio.Curves.SelectChannel(CurveChannel.LeftTrigger);reducedStudio.Curves.SetPreset("fine");Check(!reducedStudio.Curves.AnimationsEnabled,"reduced motion also disables curve and point morphing");await Task.Delay(240);reduced.UpdateLayout();
                var curveBitmap=new RenderTargetBitmap((int)Math.Ceiling(reduced.Host.ActualWidth),(int)Math.Ceiling(reduced.Host.ActualHeight),96,96,PixelFormats.Pbgra32);curveBitmap.Render(reduced.Host);var curveEncoder=new PngBitmapEncoder();curveEncoder.Frames.Add(BitmapFrame.Create(curveBitmap));using(var stream=File.Create(Path.Combine(directory,"39-curves-reduced-motion.png")))curveEncoder.Save(stream);shots.Add(new{file="39-curves-reduced-motion.png",width=curveBitmap.PixelWidth,height=curveBitmap.PixelHeight});
                reducedStudio.ShowMacros(true);reducedStudio.Macros!.SelectSlot("M2");reducedStudio.Macros.Add();await Task.Delay(240);reduced.UpdateLayout();Check(reducedStudio.Macros.IsReducedMotion && !reducedStudio.Apply.IsEnabled,"reduced-motion Macro UI retains local editing without hardware authority");var macroBitmap=new RenderTargetBitmap((int)Math.Ceiling(reduced.Host.ActualWidth),(int)Math.Ceiling(reduced.Host.ActualHeight),96,96,PixelFormats.Pbgra32);macroBitmap.Render(reduced.Host);var macroEncoder=new PngBitmapEncoder();macroEncoder.Frames.Add(BitmapFrame.Create(macroBitmap));using(var stream=File.Create(Path.Combine(directory,"52-macros-reduced-motion.png")))macroEncoder.Save(stream);shots.Add(new{file="52-macros-reduced-motion.png",width=macroBitmap.PixelWidth,height=macroBitmap.PixelHeight});
                reducedStudio.ShowVibration(true);reducedStudio.Vibration!.Set(50);await Task.Delay(240);reduced.UpdateLayout();var vibrationBitmap=new RenderTargetBitmap((int)Math.Ceiling(reduced.Host.ActualWidth),(int)Math.Ceiling(reduced.Host.ActualHeight),96,96,PixelFormats.Pbgra32);vibrationBitmap.Render(reduced.Host);var vibrationEncoder=new PngBitmapEncoder();vibrationEncoder.Frames.Add(BitmapFrame.Create(vibrationBitmap));using(var stream=File.Create(Path.Combine(directory,"71-vibration-reduced-motion.png")))vibrationEncoder.Save(stream);shots.Add(new{file="71-vibration-reduced-motion.png",width=vibrationBitmap.PixelWidth,height=vibrationBitmap.PixelHeight});Check(!reducedStudio.AnimationsEnabled && !reducedStudio.Vibration.Test.IsEnabled,"reduced-motion Vibration maintains local-only authority");
            }
            finally {reduced.Close();}
            listener.Flush();Check(log.ToString().Length==0,"no WPF binding errors");
            File.WriteAllText(Path.Combine(directory,"bindings.txt"),log.ToString());
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonSerializer.Serialize(new{
                status="passed",hardwareAccess=false,desktopInputInjected=false,desktopCapture=false,windowShown=true,
                screenshotKind="shown WPF window client renders",sequenceKind="actual intermediate WPF animation frames at timed intervals, not OS screen recording",
                systemDpiChanged=false,physicalGamepadVerified=false,checks,shots,motion,frames=frames.Count,curveFrames=curveFrames.Count,macroFrames=macroFrames.Count,vibrationFrames=vibrationFrames.Count
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        finally {window.Close();PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);}
        async Task Ready(){await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(240);window.UpdateLayout();}
        void SendKey(Key key){var input=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};window.Host.Studio!.OnKeyDown(window,input);}
        RenderTargetBitmap Render(double scale){
            var root=window.Host;var image=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*scale),(int)Math.Ceiling(root.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(root);return image;
        }
        async Task Capture(string file){await Ready();var image=Render(1);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(directory,file)))encoder.Save(stream);shots.Add(new{file,width=image.PixelWidth,height=image.PixelHeight});}
    }

    // WIC's encoder omitted frame delay metadata. Update GIF89a control blocks only;
    // palette and compressed image bytes remain identical. See w3.org/Graphics/GIF/spec-gif89a.txt.
    internal static void SetGifDelays(string path, ushort delay,IReadOnlyList<ushort>? delays=null)
    {
        byte[] input = File.ReadAllBytes(path);
        int position = 13 + ((input[10] & 128) != 0 ? 3 * (1 << ((input[10] & 7) + 1)) : 0);
        using var output = new MemoryStream(); output.Write(input, 0, position);
        bool control = false;int frameIndex=0;
        while (position < input.Length)
        {
            int start = position;
            if (input[position] == 0x3b) { output.WriteByte(0x3b); break; }
            if (input[position] == 0x21)
            {
                if (input[position + 1] == 0xf9)
                {
                    if (input[position + 2] != 4) throw new InvalidDataException("Invalid GIF control block.");
                    if(delays!=null)delay=delays[Math.Min(frameIndex,delays.Count-1)];
                    input[position + 3] = (byte)((input[position + 3] & 1) | 4);
                    input[position + 4] = (byte)delay; input[position + 5] = (byte)(delay >> 8);
                    position += 8; control = true;
                }
                else { position += 2; SkipBlocks(); }
            }
            else if (input[position] == 0x2c)
            {
                if(delays!=null)delay=delays[Math.Min(frameIndex,delays.Count-1)];
                if (!control) output.Write(new byte[] { 0x21, 0xf9, 4, 4, (byte)delay, (byte)(delay >> 8), 0, 0 });
                int packed = input[position + 9]; position += 10;
                if ((packed & 128) != 0) position += 3 * (1 << ((packed & 7) + 1));
                position++; SkipBlocks(); control = false;frameIndex++;
            }
            else throw new InvalidDataException("Unexpected GIF block.");
            output.Write(input, start, position - start);
        }
        File.WriteAllBytes(path, output.ToArray());
        void SkipBlocks() { while (position < input.Length) { int length = input[position++]; if (length == 0) return; position += length; } throw new InvalidDataException("Truncated GIF data."); }
    }
}


