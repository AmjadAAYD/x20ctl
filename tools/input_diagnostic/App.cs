using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace InputDiagnostic {
    public sealed class ScanForm : Form {
        readonly ComboBox slot=new ComboBox(), library=new ComboBox(), transport=new ComboBox();
        readonly ComboBox model=new ComboBox();
        readonly Label instruction=new Label(), outcome=new Label();
        readonly Label[] live=new Label[4];
        readonly PressTracker[] trackers={new PressTracker(),new PressTracker(),new PressTracker(),new PressTracker()};
        readonly Stopwatch liveClock=Stopwatch.StartNew();
        readonly ControllerView diagram=new ControllerView();
        readonly Inventory inventory;
        readonly Button monitor=new Button(), verify=new Button(), record=new Button(), skip=new Button(),save=new Button(), fresh=new Button();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly Stopwatch clock=new Stopwatch();
        readonly string[] actions={"neutral","A","B","X","Y","LB","RB","dpad_up","dpad_right","dpad_down","dpad_left",
            "dpad_up_right","dpad_down_right","dpad_down_left","dpad_up_left","Start","Back","L3","R3","LT","RT","left_stick","right_stick","rear_left","rear_right","turbo"};
        Reader reader; Evidence evidence=new Evidence(); string active; int index;
        string lastSaved; bool dirty;
        static readonly Color Navy=Color.FromArgb(13,24,40),TextColor=Color.FromArgb(225,235,246);
        public ScanForm(bool preview=false,Inventory inventory=null,string claimedTransport="Receiver",string claimedModel="Unknown") {
            this.inventory=inventory??new Inventory();evidence.Inventory=this.inventory;
            Text="X20CTL | Live input diagnostic";Size=new Size(1080,790);MinimumSize=new Size(1030,770);
            BackColor=Navy;ForeColor=TextColor;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=10};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            for(int i=0;i<4;i++)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,77));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));
            Controls.Add(layout);
            layout.Controls.Add(new Label {Text="LIVE INPUT CHECK   /   Verify the source before scanning",Dock=DockStyle.Fill,Font=new Font("Segoe UI",17,FontStyle.Bold)},0,0);
            var choices=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
            model.DropDownStyle=ComboBoxStyle.DropDownList;model.Items.AddRange(new object[]{"Unknown","X05","X05 Pro","X10","X15","D10","Dune / D15","X20 Pro","X20"});model.SelectedItem=claimedModel;model.Width=130;
            transport.DropDownStyle=ComboBoxStyle.DropDownList;transport.Items.AddRange(new object[]{"Receiver","USB cable","Bluetooth","Not sure"});transport.SelectedItem=claimedTransport;transport.Width=130;
            library.DropDownStyle=ComboBoxStyle.DropDownList;library.Items.AddRange(new object[]{"XInput1_4.dll","XInput9_1_0.dll","XInput1_3.dll"});library.SelectedIndex=0;library.Width=170;
            slot.DropDownStyle=ComboBoxStyle.DropDownList;slot.Items.AddRange(new object[]{"Player slot 1","Player slot 2","Player slot 3","Player slot 4"});slot.SelectedIndex=0;slot.Width=125;
            foreach(Control c in new Control[]{model,transport,library,slot})choices.Controls.Add(c);
            layout.Controls.Add(choices,0,1);
            layout.Controls.Add(new Label {Text="Use one connection method. Other controllers may occupy separate slots. The selected model is your claim, not automatic detection.",Dock=DockStyle.Fill},0,2);
            var liveArea=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4};
            liveArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));liveArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));
            for(int i=0;i<4;i++) {liveArea.RowStyles.Add(new RowStyle(SizeType.Percent,25));live[i]=new Label {Text="Slot "+(i+1)+"  |  Monitoring has not started",Dock=DockStyle.Fill,Padding=new Padding(8),AutoEllipsis=true,BackColor=Color.FromArgb(25,43,65)};liveArea.Controls.Add(live[i],0,i);}
            diagram.Dock=DockStyle.Fill;liveArea.Controls.Add(diagram,1,0);liveArea.SetRowSpan(diagram,4);layout.Controls.Add(liveArea,0,3);layout.SetRowSpan(liveArea,4);
            instruction.Dock=DockStyle.Fill;instruction.Padding=new Padding(0,14,0,0);instruction.Font=new Font("Segoe UI",13);
            instruction.Text="1. Click Start live check.\r\n2. Press A and pull each trigger. Watch which slot responds.\r\n3. Select that slot, then verify it before the full scan.";layout.Controls.Add(instruction,0,7);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true};
            foreach(var pair in new[]{Tuple.Create(monitor,"Start live check"),Tuple.Create(verify,"Verify A + LT + RT"),Tuple.Create(record,"Record next control"),Tuple.Create(skip,"Skip control"),Tuple.Create(save,"Save results"),Tuple.Create(fresh,"New source")}) {
                pair.Item1.Text=pair.Item2;pair.Item1.AutoSize=true;pair.Item1.Height=35;pair.Item1.ForeColor=TextColor;pair.Item1.BackColor=Color.FromArgb(38,59,81);pair.Item1.FlatStyle=FlatStyle.Flat;buttons.Controls.Add(pair.Item1);
            }
            layout.Controls.Add(buttons,0,8);outcome.Dock=DockStyle.Fill;outcome.Text="Read-only input. No settings, vibration, firmware changes, or uploads. Sampling begins after READY and a three-second countdown.";layout.Controls.Add(outcome,0,9);
            var rawButton=new Button{Text="Raw Input fallback",AutoSize=true,Height=35,ForeColor=TextColor,BackColor=Color.FromArgb(38,59,81),FlatStyle=FlatStyle.Flat};buttons.Controls.Add(rawButton);
            rawButton.Click+=(s,e)=>{if(active!=null)return;evidence.Model=model.Text;evidence.Transport=transport.SelectedItem.ToString();using(var raw=new RawForm(evidence))raw.ShowDialog(this);dirty=dirty||evidence.RawRecords.Count>0;save.Enabled=dirty;};
            verify.Enabled=record.Enabled=skip.Enabled=save.Enabled=fresh.Enabled=false;
            monitor.Click+=(s,e)=>StartMonitoring();verify.Click+=(s,e)=>Begin("preflight");
            record.Click+=(s,e)=> {if(evidence.Verified && index<actions.Length)Begin(actions[index]);};
            skip.Click+=(s,e)=> {if(active==null && index<actions.Length){evidence.Event("skipped",actions[index++]);dirty=true;UpdateControls();}};
            save.Click+=(s,e)=>Save();fresh.Click+=(s,e)=>NewSource();
            slot.SelectedIndexChanged+=(s,e)=> {evidence.Verified=false;UpdateControls();};
            timer.Interval=20;timer.Tick+=(s,e)=>TickInput();
            FormClosing+=(s,e)=> {timer.Stop();if(active!=null){evidence.Event("cancelled",active);evidence.Verified=false;dirty=true;}if(dirty){Save();if(dirty){e.Cancel=true;timer.Start();return;}}if(reader!=null)reader.Dispose();};
            if(preview){live[0].Text="Slot 1 | Connected\r\nHELD: A 0.43 s\r\nLT 128 / 255   RT 0 / 255";diagram.State=new State{Pad=new Pad{Buttons=4096,LT=128,LX=16000,LY=-8000}};diagram.Connected=true;diagram.Tracker=new PressTracker();diagram.Tracker.Update(diagram.State,0);diagram.Now=432;outcome.Text="Offline interface preview. Synthetic values only; no device access.";}
        }
        void StartMonitoring() {
            try {
                reader=new WindowsReader(library.SelectedItem.ToString());
                evidence.Reader=reader.Identity;monitor.Enabled=false;library.Enabled=false;
                evidence.XInputCapabilities.Clear();for(int i=0;i<4;i++)evidence.XInputCapabilities.Add(((WindowsReader)reader).ReportedCapabilities(i));
                timer.Start();verify.Enabled=fresh.Enabled=save.Enabled=true;
                outcome.Text="Reader READY. Source: documented Windows XInput. Watch live input and select the responsive slot. Monitoring alone is not saved.";
            }catch(Exception error){outcome.Text=error.Message;}
        }
        void Begin(string action) {
            if(reader==null || active!=null)return;
            State state;
            if(!reader.Read(slot.SelectedIndex,out state)){outcome.Text="Selected slot is disconnected. Choose a responsive slot first.";return;}
            active=action;evidence.Slot=slot.SelectedIndex;evidence.Model=model.Text;evidence.Transport=transport.SelectedItem.ToString();
            if(action=="preflight")evidence.Verified=false;
            evidence.Records[action]=new List<Sample>();dirty=true;
            clock.Restart();UpdateControls();
        }
        void TickInput() {
            try {
                State selected=new State();bool connected=false;
                for(int i=0;i<4;i++) {
                    State state;bool present=reader.Read(i,out state);
                    var edges=present?trackers[i].Update(state,liveClock.Elapsed.TotalMilliseconds):new List<PressEdge>();
                    if(!present)trackers[i].Disconnect();
                    if(i==slot.SelectedIndex && active!=null && clock.Elapsed.TotalSeconds>=3)
                        foreach(var edge in edges)evidence.Events.Add(new {timestamp=DateTime.UtcNow.ToString("o"),slot=i,action=active,control=edge.control,down=edge.down,duration_ms=edge.duration_ms});
                    live[i].Text="Slot "+(i+1)+"  |  "+(present ?
                        "Connected\r\nHELD: "+trackers[i].HeldText+"\r\nLT "+state.Pad.LT+" / 255   RT "+state.Pad.RT+" / 255" : "Disconnected");
                    live[i].BackColor=present && (state.Pad.Buttons!=0 || state.Pad.LT>0 || state.Pad.RT>0)?Color.FromArgb(24,78,68):Color.FromArgb(25,43,65);
                    if(i==slot.SelectedIndex){selected=state;connected=present;}
                }
                diagram.State=selected;diagram.Connected=connected;diagram.Tracker=trackers[slot.SelectedIndex];diagram.Now=liveClock.Elapsed.TotalMilliseconds;diagram.Invalidate();
                if(!connected) {
                    evidence.Verified=false;
                    if(active!=null){evidence.Event("disconnected",active);active=null;dirty=true;outcome.Text="Source disconnected. Partial results retained; use New source and verify again.";}
                    UpdateControls();return;
                }
                if(active==null)return;
                double seconds=clock.Elapsed.TotalSeconds;
                string prompt=active=="preflight"?"Release everything, press/release A, pull/release LT, then pull/release RT.":
                    active.StartsWith("rear_")?"Optional: press/release the "+active.Replace('_',' ')+" button (M1/M2 if present). Skip if absent. This records ordinary output, not independent M keys.":
                    active=="turbo"?"Optional: hold a normal button with turbo already enabled using controls you know. No settings are changed. Skip if unavailable.":
                    active=="neutral"?"Keep hands off the controller.":active.EndsWith("stick")?"Move the "+active.Replace('_',' ')+" fully in a circle, then centre.":"Press / pull and release "+active+" three times.";
                instruction.Text=(seconds<3 ? "READY - starts in "+Math.Ceiling(3-seconds)+" seconds" : "RECORDING - "+Math.Ceiling((active=="preflight"?23:11)-seconds)+" seconds left")+"\r\n"+prompt;
                if(seconds<3)return;
                evidence.Add(active,new Sample(active,evidence.Slot,selected,(seconds-3)*1000));
                if(seconds<(active=="preflight"?23:11))return;
                string completed=active;active=null;clock.Stop();
                var rows=evidence.Records[completed];var summary=Analysis.Summary(completed,rows);
                if(completed=="preflight") {
                    evidence.Verified=Analysis.Verify(rows);
                    outcome.Text=evidence.Verified?"Source VERIFIED: A and both triggers pressed and released. You can record the controls now.":
                        "Source NOT verified. Check live values / selected slot. Save this failed check; use New source to compare another Windows reader.";
                } else {index++;outcome.Text=completed+": "+summary["status"]+". "+(index<actions.Length?"Next: "+actions[index]+". Click Record when ready.":"Input pass complete. Save results.");}
                UpdateControls();
            }catch(Exception error) {timer.Stop();evidence.Verified=false;if(active!=null){evidence.Event("failed",active+": "+error.Message);active=null;dirty=true;}outcome.Text="Read stopped: "+error.Message;UpdateControls();}
        }
        void UpdateControls() {
            bool idle=active==null;
            verify.Enabled=reader!=null && idle && !evidence.Records.ContainsKey("preflight");record.Enabled=skip.Enabled=reader!=null && idle && evidence.Verified && index<actions.Length;
            slot.Enabled=idle && evidence.Records.Count==0;model.Enabled=transport.Enabled=idle && evidence.Records.Count==0;
            fresh.Enabled=reader!=null && idle;save.Enabled=idle && (evidence.Records.Count>0 || evidence.RawRecords.Count>0);
        }
        void Save() {
            try {string parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"X20CTLInputReports");
                Directory.CreateDirectory(parent);lastSaved=evidence.Export(parent);dirty=false;outcome.Text="Saved locally: "+lastSaved;
            }catch(Exception error){outcome.Text="Save failed: "+error.Message;}
        }
        void NewSource() {
            if(dirty){Save();if(dirty)return;}
            timer.Stop();if(reader!=null){reader.Dispose();reader=null;}
            evidence=new Evidence{Inventory=inventory};index=0;monitor.Enabled=library.Enabled=true;UpdateControls();
            outcome.Text="Previous results retained"+(lastSaved==null?".":": "+lastSaved)+" Choose a slot / Windows reader, then Start live check.";
        }
    }
    static class Program {
        [STAThread] static int Main(string[] args) {
            try {
                if(args.SequenceEqual(new[]{"--self-test"})){SelfTest.Run();return 0;}
                if(args.Length==2 && args[0]=="--preview") {
                    using(var form=new ScanForm(true))using(var bitmap=new Bitmap(form.Width,form.Height)) {
                        var content=form.Controls[0];content.BackColor=form.BackColor;content.ForeColor=form.ForeColor;content.Font=form.Font;form.Controls.Remove(content);content.Size=form.ClientSize;content.CreateControl();LayoutAll(content);content.DrawToBitmap(bitmap,new Rectangle(Point.Empty,content.Size));bitmap.Save(args[1]);
                    }return 0;
                }
                if(args.Length!=0)throw new ArgumentException("Use --self-test, --preview PATH, or launch normally.");
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new WizardForm());return 0;
            }catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        static void LayoutAll(Control control) {control.PerformLayout();foreach(Control child in control.Controls){child.CreateControl();LayoutAll(child);}}
    }
}
