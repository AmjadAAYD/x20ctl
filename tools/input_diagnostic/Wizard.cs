using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace InputDiagnostic {
    public sealed class WizardForm : Form {
        readonly Panel body=new Panel();readonly Label step=new Label();
        readonly CheckBox serial=new CheckBox();Inventory inventory;string transport="Not sure",model="Unknown";
        readonly Color navy=Color.FromArgb(13,24,40),text=Color.FromArgb(225,235,246);
        ScanForm live;
        public WizardForm() {
            Text="X20CTL Controller Scanner";Size=new Size(1160,980);MinimumSize=new Size(1060,880);
            BackColor=navy;ForeColor=text;Font=new Font("Segoe UI",11);StartPosition=FormStartPosition.CenterScreen;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(20)};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));Controls.Add(layout);
            step.Dock=DockStyle.Fill;step.Font=new Font("Segoe UI",17,FontStyle.Bold);layout.Controls.Add(step,0,0);
            body.Dock=DockStyle.Fill;body.AutoScroll=true;layout.Controls.Add(body,0,1);
            layout.Controls.Add(new Label{Text="Local research tool  |  No firmware changes  |  No hidden uploads  |  Reported model and connection remain owner context",Dock=DockStyle.Fill},0,2);
            FormClosing+=(s,e)=>{if(live!=null && !live.IsDisposed)live.Close();};
            Welcome();
        }
        FlowLayoutPanel Page(string title) {
            body.Controls.Clear();step.Text=title;
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(20)};body.Controls.Add(panel);return panel;
        }
        Label Copy(string value,int height=70) {return new Label{Text=value,Width=990,Height=height,Font=new Font("Segoe UI",12),Margin=new Padding(0,12,0,12)};}
        Button Action(string text,Action clicked) {var button=new Button{Text=text,AutoSize=true,Height=42,MinimumSize=new Size(220,42),ForeColor=this.text,BackColor=Color.FromArgb(38,59,81),FlatStyle=FlatStyle.Flat,Margin=new Padding(0,12,10,12)};button.Click+=(s,e)=>clicked();return button;}
        void Welcome() {
            var p=Page("X20CTL Controller Scanner");p.Controls.Add(Copy("Help improve EasySMX controller support.",55));
            p.Controls.Add(Copy("This tool checks how your controller communicates with Windows and records controller inputs for analysis.",85));
            p.Controls.Add(Copy("No firmware changes. No flashing. No hidden uploads.\r\nResults stay on your PC. Review the generated files before sharing.",100));
            p.Controls.Add(Action("Start scan",Before));p.Controls.Add(Action("Advanced / About",()=>MessageBox.Show(this,
                "Local standalone scanner, version 2.0.0-local.\nStandard Windows input only; configuration backends remain disabled.\nRaw interface paths can contain identifiers; exports stay local.\nOriginal HID descriptors, DirectInput and BLE discovery have explicit limitations.\nPolling/report timing is host observation, not hardware latency.","About this scanner")));
        }
        void Before() {
            var p=Page("1 / 4   Before you start");p.Controls.Add(Copy("1. Disconnect other game controllers if possible.\r\n2. Connect only the controller you want to test.\r\n3. Use one connection method: USB cable, receiver, or Bluetooth.\r\n4. Keep it powered on and do not switch modes during the scan.",175));
            var done=new CheckBox{Text="I have done this",Width=700,Height=40};p.Controls.Add(done);
            var next=Action("Continue",Connection);next.Enabled=false;done.CheckedChanged+=(s,e)=>next.Enabled=done.Checked;p.Controls.Add(next);
        }
        void Connection() {
            var p=Page("2 / 4   How is it connected right now?");p.Controls.Add(Copy("Choose what you are using. Windows evidence is collected separately; this choice will not overwrite detected device information.",90));
            foreach(var item in new[]{Tuple.Create("Wired USB","USB cable"),Tuple.Create("2.4 GHz receiver","Receiver"),Tuple.Create("Bluetooth","Bluetooth"),Tuple.Create("Not sure","Not sure")}) {
                string value=item.Item2;p.Controls.Add(Action(item.Item1,()=>{transport=value;Detect();}));
            }
        }
        void Detect() {
            var p=Page("3 / 4   Detect Windows interfaces");
            p.Controls.Add(Copy("Printed controller model (optional). Choose Unknown if unsure; Windows names and IDs do not prove an EasySMX model.",90));
            var models=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=250};models.Items.AddRange(new object[]{"Unknown","X05","X05 Pro","X10","X15","D10","Dune / D15","X20 Pro","X20"});models.SelectedItem=model;models.SelectedIndexChanged+=(s,e)=>model=models.SelectedItem.ToString();p.Controls.Add(models);
            serial.Text="Include serial strings where available (optional; private identifiers)";serial.Width=850;serial.Height=40;p.Controls.Add(serial);
            p.Controls.Add(Copy("Interface paths are retained for private analysis and may contain identifiers. No keyboard/mouse input is recorded. Nothing is sent.",85));
            var status=Copy("Ready to inspect Windows. This does not change controller settings.",60);p.Controls.Add(status);
            var list=new ListBox{Width=980,Height=180};p.Controls.Add(list);
            var detect=Action("Detect devices",()=>{});p.Controls.Add(detect);
            var details=Action("Technical details",()=>{if(inventory!=null)Details(new JavaScriptSerializer().Serialize(inventory));});details.Enabled=false;p.Controls.Add(details);
            var next=Action("Continue to live tester",Live);next.Enabled=false;p.Controls.Add(next);
            detect.Click+=(s,e)=>{
                detect.Enabled=false;serial.Enabled=false;status.Text="Inspecting HID, USB, SetupAPI and Raw Input controller interfaces...";bool include=serial.Checked;
                AsyncInventory.Bound(Task.Factory.StartNew(()=>WindowsInventory.Read(include)),15000).ContinueWith(task=>{
                    if(IsDisposed || !IsHandleCreated)return;
                    BeginInvoke(new Action(()=>{
                        detect.Enabled=true;serial.Enabled=true;
                        if(task.IsFaulted){status.Text="Windows inventory failed. Live XInput testing is still available; save its results.";inventory=new Inventory();inventory.errors.Add(task.Exception.GetBaseException().Message);}
                        else inventory=task.Result;
                        list.Items.Clear();int index=0;
                        foreach(var group in Inventory.Group(inventory.interfaces)) {
                            var pad=group.FirstOrDefault(x=>x.usagePage==1 && new[]{4,5,8}.Contains(x.usage))??group[0];
                            list.Items.Add("Windows group "+(++index)+": "+pad.product+"  |  "+group.Count+" interface(s)  |  Model: unverified");
                        }
                        list.Items.Add("Raw Input: "+inventory.rawInput.Count+" controller collection(s); association with XInput slots is unverified.");
                        status.Text="Windows can expose one controller through multiple interfaces. Groups use shared container IDs, not matching names or VID/PID.\r\nLive XInput slots are listed separately; press a button to identify the responsive source.";
                        status.Height=90;details.Enabled=next.Enabled=true;
                    }));
                });
            };
        }
        void Details(string value) {
            using(var form=new Form{Text="Private Windows evidence",Size=new Size(940,680),StartPosition=FormStartPosition.CenterParent}) {
                var box=new TextBox{Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Text=value};form.Controls.Add(box);form.ShowDialog(this);
            }
        }
        void Live() {
            body.Controls.Clear();step.Text="4 / 4   Live input / guided checks / local results";
            live=new ScanForm(false,inventory,transport,model){TopLevel=false,FormBorderStyle=FormBorderStyle.None,Dock=DockStyle.Fill};body.Controls.Add(live);live.Show();
        }
    }
}
