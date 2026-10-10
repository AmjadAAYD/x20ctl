using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace InputDiagnostic {
    public sealed class RawDecoded {
        public string source="windows_raw_input",path,status;
        public List<int> buttons=new List<int>();public Dictionary<string,uint> axes=new Dictionary<string,uint>();
        public string report_hex;
    }
    public sealed class RawObserver : IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct Registration {public ushort page,usage;public uint flags;public IntPtr target;}
        [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterRawInputDevices(Registration[] devices,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)]static extern uint GetRawInputData(IntPtr handle,uint command,IntPtr data,ref uint size,uint headerSize);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint GetRawInputDeviceInfoW(IntPtr handle,uint command,IntPtr data,ref uint size);
        [DllImport("hid.dll")]static extern int HidP_GetUsages(int type,ushort page,ushort link,[Out]ushort[] usages,ref uint count,IntPtr preparsed,byte[] report,uint length);
        [DllImport("hid.dll")]static extern int HidP_GetUsageValue(int type,ushort page,ushort link,ushort usage,out uint value,IntPtr preparsed,byte[] report,uint length);
        bool registered;
        public static bool ValidReports(int stride,int count,uint length,uint header) {
            return stride>=1 && stride<=4096 && count>=1 && count<=1024 && length>=header+8 && (long)stride*count<=length-header-8;
        }
        public RawObserver(IntPtr target) {
            var items=new ushort[]{4,5,8}.Select(x=>new Registration{page=1,usage=x,target=target}).ToArray();
            if(!RegisterRawInputDevices(items,(uint)items.Length,(uint)Marshal.SizeOf(typeof(Registration))))throw new IOException("Raw Input registration unavailable.");registered=true;
        }
        public List<RawDecoded> Read(IntPtr message,bool includeBytes) {
            uint length=0,header=(uint)(8+IntPtr.Size*2);
            if(GetRawInputData(message,0x10000003,IntPtr.Zero,ref length,header)==UInt32.MaxValue || length<header+8 || length>65536)return new List<RawDecoded>();
            IntPtr buffer=Marshal.AllocHGlobal((int)length);
            try {
                if(GetRawInputData(message,0x10000003,buffer,ref length,header)==UInt32.MaxValue || Marshal.ReadInt32(buffer)!=2)return new List<RawDecoded>();
                IntPtr device=Marshal.ReadIntPtr(buffer,8);int stride=Marshal.ReadInt32(buffer,(int)header),count=Marshal.ReadInt32(buffer,(int)header+4);
                if(!ValidReports(stride,count,length,header))return new List<RawDecoded>();
                uint chars=0;GetRawInputDeviceInfoW(device,0x20000007,IntPtr.Zero,ref chars);if(chars==0 || chars>32768)return new List<RawDecoded>();
                IntPtr name=Marshal.AllocHGlobal((int)(chars+1)*2);string path;
                try{if(GetRawInputDeviceInfoW(device,0x20000007,name,ref chars)==UInt32.MaxValue)return new List<RawDecoded>();path=Marshal.PtrToStringUni(name);}finally{Marshal.FreeHGlobal(name);}
                uint capSize=0;GetRawInputDeviceInfoW(device,0x20000005,IntPtr.Zero,ref capSize);
                if(capSize==0 || capSize>65536)return new List<RawDecoded>();
                IntPtr caps=Marshal.AllocHGlobal((int)capSize);
                try {
                    if(GetRawInputDeviceInfoW(device,0x20000005,caps,ref capSize)==UInt32.MaxValue)return new List<RawDecoded>();
                    var results=new List<RawDecoded>();
                    for(int i=0;i<count;i++) {
                        byte[] bytes=new byte[stride];Marshal.Copy(IntPtr.Add(buffer,(int)header+8+i*stride),bytes,0,stride);
                        var row=new RawDecoded{path=path,status="descriptor_declared_usages_only",report_hex=includeBytes?BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant():null};
                        uint usagesCount=128;var usages=new ushort[128];
                        if(HidP_GetUsages(0,9,0,usages,ref usagesCount,caps,bytes,(uint)stride)==0x110000)row.buttons=usages.Take((int)Math.Min(usagesCount,128)).Select(x=>(int)x).ToList();
                        foreach(ushort usage in new ushort[]{0x30,0x31,0x32,0x33,0x34,0x35,0x36,0x39}) {
                            uint value;if(HidP_GetUsageValue(0,1,0,usage,out value,caps,bytes,(uint)stride)==0x110000)row.axes["usage_"+usage.ToString("X2")]=value;
                        }
                        results.Add(row);
                    }
                    return results;
                }finally{Marshal.FreeHGlobal(caps);}
            }finally{Marshal.FreeHGlobal(buffer);}
        }
        public void Dispose() {
            if(!registered)return;
            var items=new ushort[]{4,5,8}.Select(x=>new Registration{page=1,usage=x,flags=1,target=IntPtr.Zero}).ToArray();
            RegisterRawInputDevices(items,(uint)items.Length,(uint)Marshal.SizeOf(typeof(Registration)));registered=false;
        }
    }
    public sealed class RawForm : Form {
        readonly Label status=new Label(),values=new Label();readonly CheckBox consent=new CheckBox();
        readonly Dictionary<string,Dictionary<int,double>> holds=new Dictionary<string,Dictionary<int,double>>();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Evidence evidence;RawObserver observer;int received;
        public RawForm(Evidence evidence) {
            this.evidence=evidence;Text="Raw Input fallback | separate Windows input stream";Size=new Size(960,520);Font=new Font("Segoe UI",11);
            BackColor=Color.FromArgb(13,24,40);ForeColor=Color.FromArgb(225,235,246);
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20)};Controls.Add(panel);
            panel.Controls.Add(new Label{Width=890,Height=90,Text="Keep this window focused and press buttons. Raw Input is separate from XInput; numbered HID buttons do not establish A/B/X/Y or independent rear buttons. Values below come from descriptor-declared usages, not a guessed byte layout."});
            consent.Width=880;consent.Height=40;consent.Text="Also retain raw gameplay report bytes in the private results (optional)";panel.Controls.Add(consent);
            status.Width=890;status.Height=80;status.Text="Click Start to monitor controller usages only. Keyboard/mouse input is excluded.";panel.Controls.Add(status);
            values.Width=890;values.Height=150;panel.Controls.Add(values);
            var start=new Button{Text="Start Raw Input check",Width=230,Height=40,ForeColor=BackColor};panel.Controls.Add(start);
            start.Click+=(s,e)=>{try{observer=new RawObserver(Handle);start.Enabled=false;status.Text="READY. Focus this window, then press/release buttons and move sticks. Results attach to the main local report.";evidence.Event("raw_input_started","Controller usage pages only; no XInput slot association");}catch(Exception error){status.Text=error.Message;}};
            FormClosed+=(s,e)=>{if(observer!=null)observer.Dispose();};
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0xFF && observer!=null)try {
                foreach(var row in observer.Read(m.LParam,consent.Checked)) {
                    received++;
                    bool saved=evidence.AddRaw(new {timestamp=DateTime.UtcNow.ToString("o"),input=row});
                    double now=clock.Elapsed.TotalMilliseconds;if(!holds.ContainsKey(row.path))holds[row.path]=new Dictionary<int,double>();
                    var active=holds[row.path];string last="";
                    foreach(int button in active.Keys.ToArray())if(!row.buttons.Contains(button)){last="Button "+button+" released after "+(now-active[button]).ToString("0")+" ms";active.Remove(button);}
                    foreach(int button in row.buttons)if(!active.ContainsKey(button))active[button]=now;
                    values.Text="PRESSED: "+String.Join("   ",active.Select(x=>"Button "+x.Key+"  "+(now-x.Value).ToString("0")+" ms"))+"\r\n"+last+"\r\nRaw usages: "+String.Join("   ",row.axes.Select(x=>x.Key+" = "+x.Value));
                    status.Text=received+" controller reports observed. "+(saved?"":"Capture limit reached; further reports are live only. ")+"Source: Windows Raw Input (separate from XInput).\r\nInterface: "+row.path;
                }
            }catch(Exception error){status.Text="Raw Input error: "+error.Message;}
            base.WndProc(ref m);
        }
    }
}
