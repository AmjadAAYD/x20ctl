using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace InputDiagnostic {
    public sealed class PressEdge {
        public string control; public bool down; public double duration_ms;
    }
    public sealed class PressTracker {
        readonly Dictionary<string,double> started=new Dictionary<string,double>();
        readonly Dictionary<string,double> last=new Dictionary<string,double>();
        public string HeldText="None",LastRelease="No completed press yet";
        public List<PressEdge> Update(State state,double now) {
            var pressed=new HashSet<string>(Analysis.Masks.Where(x=>(state.Pad.Buttons&x.Value)==x.Value).Select(x=>x.Key));
            if(state.Pad.LT>0)pressed.Add("LT");if(state.Pad.RT>0)pressed.Add("RT");
            var edges=new List<PressEdge>();
            foreach(string key in started.Keys.ToArray())if(!pressed.Contains(key)) {
                double duration=Math.Max(0,now-started[key]);started.Remove(key);
                last[key]=duration;
                edges.Add(new PressEdge{control=key,down=false,duration_ms=duration});
                LastRelease=key+" released after "+(duration/1000).ToString("0.00")+" s";
            }
            foreach(string key in pressed.OrderBy(x=>x))if(!started.ContainsKey(key)) {
                started[key]=now;edges.Add(new PressEdge{control=key,down=true,duration_ms=0});
            }
            HeldText=started.Count==0?"None":String.Join("  ",started.Select(x=>x.Key+" "+((now-x.Value)/1000).ToString("0.00")+" s"));
            return edges;
        }
        public void Disconnect() {started.Clear();HeldText="Disconnected";LastRelease="Connection lost; unfinished holds are inconclusive";}
        public string Duration(string control,double now) {
            return started.ContainsKey(control)?"PRESSED  "+Math.Max(0,now-started[control]).ToString("0")+" ms":
                last.ContainsKey(control)?"Last: "+last[control].ToString("0")+" ms":"Released";
        }
    }
    [StructLayout(LayoutKind.Sequential)] public struct Pad {
        public ushort Buttons; public byte LT,RT; public short LX,LY,RX,RY;
    }
    [StructLayout(LayoutKind.Sequential)] public struct State { public uint Packet; public Pad Pad; }
    [StructLayout(LayoutKind.Sequential)] public struct Capabilities {public byte type,subtype;public ushort flags;public Pad pad;public ushort leftMotor,rightMotor;}
    public sealed class Sample {
        public string timestamp, action, source="xinput_state";
        public double elapsed_ms;
        public Dictionary<string,object> values;
        public Sample(string label,int slot,State state,double elapsed) {
            timestamp=DateTime.UtcNow.ToString("o"); action=label; elapsed_ms=elapsed;
            values=new Dictionary<string,object>{{"slot",slot},{"packet",state.Packet},
                {"buttons",state.Pad.Buttons},{"lt",state.Pad.LT},{"rt",state.Pad.RT},
                {"lx",state.Pad.LX},{"ly",state.Pad.LY},{"rx",state.Pad.RX},{"ry",state.Pad.RY}};
        }
        public int Get(string key) { return Convert.ToInt32(values[key]); }
    }
    public static class Analysis {
        public static readonly Dictionary<string,int> Masks=new Dictionary<string,int>{
            {"A",4096},{"B",8192},{"X",16384},{"Y",32768},{"LB",256},{"RB",512},
            {"Start",16},{"Back",32},{"L3",64},{"R3",128},{"dpad_up",1},{"dpad_down",2},
            {"dpad_left",4},{"dpad_right",8},{"dpad_up_right",9},{"dpad_down_right",10},
            {"dpad_down_left",6},{"dpad_up_left",5}};
        public static bool Cycle(List<Sample> rows,string key,int mask) {
            bool off=false,on=false;
            foreach(var row in rows) {
                int v=row.Get(key); bool active=mask==0 ? v>30 : (v&mask)==mask;
                bool released=mask==0 ? v==0 : (v&mask)==0;
                if(released) { if(on) return true; off=true; }
                else if(active && off) on=true;
            }
            return false;
        }
        public static bool Verify(List<Sample> rows) {
            return rows.Count>=3 && rows.Select(x=>x.Get("slot")).Distinct().Count()==1
                && rows.Any(x=>x.Get("buttons")==0 && x.Get("lt")==0 && x.Get("rt")==0)
                && Cycle(rows,"buttons",4096) && Cycle(rows,"lt",0) && Cycle(rows,"rt",0);
        }
        public static Dictionary<string,object> Summary(string action,List<Sample> rows) {
            var result=new Dictionary<string,object>{{"action",action},{"samples",rows.Count},
                {"status","no_samples"},{"configuration_support_verified",false}};
            if(rows.Count==0) return result;
            if(rows.Select(x=>x.Get("slot")).Distinct().Count()!=1) {
                result["status"]="inconclusive_source_changed";return result;
            }
            var ranges=new Dictionary<string,object>();
            foreach(string key in new[]{"buttons","lt","rt","lx","ly","rx","ry"})
                ranges[key]=new { min=rows.Min(x=>x.Get(key)),max=rows.Max(x=>x.Get(key)) };
            result["ranges"]=ranges;
            var intervals=rows.Zip(rows.Skip(1),(a,b)=>b.elapsed_ms-a.elapsed_ms).Where(x=>x>0).OrderBy(x=>x).ToArray();
            if(intervals.Length>0)result["hostSampleIntervalMs"]=new {min=intervals[0],median=intervals[intervals.Length/2],max=intervals[intervals.Length-1],hardwarePollingRateVerified=false};
            bool observed;
            if(action=="preflight") observed=Verify(rows);
            else if(action=="neutral") observed=rows.All(x=>x.Get("buttons")==0 && x.Get("lt")==0 && x.Get("rt")==0);
            else if(Masks.ContainsKey(action)) observed=Cycle(rows,"buttons",Masks[action]);
            else if(action=="LT" || action=="RT") observed=Cycle(rows,action.ToLowerInvariant(),0);
            else if(action=="rear_left" || action=="rear_right" || action=="turbo")
                observed=Masks.Values.Any(mask=>Cycle(rows,"buttons",mask)) || Cycle(rows,"lt",0) || Cycle(rows,"rt",0);
            else if(action=="left_stick" || action=="right_stick") {
                string x=action=="left_stick"?"lx":"rx",y=action=="left_stick"?"ly":"ry";
                // Above both SDK stick deadzones; small centre noise is not a stick sweep.
                observed=rows.Any(v=>Math.Abs(v.Get(x))>8689 || Math.Abs(v.Get(y))>8689);
            } else observed=false;
            result["status"]=observed?"requested_control_observed":"inconclusive_requested_control_not_observed";
            if(action=="rear_left" || action=="rear_right" || action=="turbo") {
                result["status"]=observed?"ordinary_output_observed":"inconclusive_no_ordinary_output";
                result["independent_rear_control_verified"]=false;
            }
            result["interpretation"]="Standard logical input observed in this run; not a sensor, calibration, physical model or configuration verdict.";
            return result;
        }
    }
    public interface Reader : IDisposable { bool Read(int slot,out State state); string Identity {get;} }
    public sealed class WindowsReader : Reader {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint GetState(uint slot,out State state);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryW(string path);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint GetCapabilities(uint slot,uint flags,out Capabilities caps);
        IntPtr module; GetState function;GetCapabilities capabilities; public string Identity {get;private set;}
        public WindowsReader(string library) {
            if(!new[]{"XInput1_4.dll","XInput9_1_0.dll","XInput1_3.dll"}.Contains(library)) throw new ArgumentException("Unknown Windows input reader");
            Identity=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),library);
            module=LoadLibraryW(Identity);
            if(module==IntPtr.Zero) throw new IOException("This Windows input reader is unavailable. Choose another reader.");
            IntPtr address=GetProcAddress(module,"XInputGetState");
            if(address==IntPtr.Zero) {Dispose();throw new IOException("Documented input function unavailable.");}
            function=(GetState)Marshal.GetDelegateForFunctionPointer(address,typeof(GetState));
            address=GetProcAddress(module,"XInputGetCapabilities");
            if(address!=IntPtr.Zero)capabilities=(GetCapabilities)Marshal.GetDelegateForFunctionPointer(address,typeof(GetCapabilities));
        }
        public bool Read(int slot,out State state) {return function((uint)slot,out state)==0;}
        public object ReportedCapabilities(int slot) {
            Capabilities caps;
            if(capabilities==null || capabilities((uint)slot,1,out caps)!=0)return new {slot,status="unavailable"};
            return new {slot,status="driver_reported_not_physical_acceptance",type=caps.type,subtype=caps.subtype,flags=caps.flags,
                standardButtons=caps.pad.Buttons,leftTrigger=caps.pad.LT,rightTrigger=caps.pad.RT,leftMotor=caps.leftMotor,rightMotor=caps.rightMotor,
                motorResponseVerified=false,configurationProtocolVerified=false};
        }
        public void Dispose() {if(module!=IntPtr.Zero){FreeLibrary(module);module=IntPtr.Zero;}}
    }
    public sealed class Evidence {
        public readonly Dictionary<string,List<Sample>> Records=new Dictionary<string,List<Sample>>();
        public readonly List<object> Events=new List<object>();
        public string Model="Unknown",Transport="unknown",Reader="unknown"; public int Slot;
        public Inventory Inventory=new Inventory();
        public readonly List<object> XInputCapabilities=new List<object>();
        public readonly List<object> RawRecords=new List<object>();
        int rawBytes;
        public bool AddRaw(object row) {
            int size;
            try {size=Encoding.UTF8.GetByteCount(new JavaScriptSerializer{MaxJsonLength=4*1024*1024+1024}.Serialize(row));}
            catch(InvalidOperationException){return false;}
            if(RawRecords.Count>=10000 || rawBytes+size>4*1024*1024)return false;
            RawRecords.Add(row);rawBytes+=size;return true;
        }
        public bool Verified;
        public void Add(string action,Sample row) {
            if(!Records.ContainsKey(action)) Records[action]=new List<Sample>();
            if(Records.Values.Sum(x=>x.Count)>=30000) throw new IOException("Capture limit reached; save these results.");
            Records[action].Add(row);
        }
        public void Event(string status,string reason) {Events.Add(new {timestamp=DateTime.UtcNow.ToString("o"),status,reason});}
        public string Export(string parent) {
            var json=new JavaScriptSerializer();
            string id="input-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            string folder=Path.Combine(parent,id);Directory.CreateDirectory(folder);
            foreach(var entry in Records) {
                if(!new[]{"preflight","neutral","LT","RT","left_stick","right_stick","rear_left","rear_right","turbo"}.Contains(entry.Key) && !Analysis.Masks.ContainsKey(entry.Key))
                    throw new IOException("Unknown evidence action");
                File.WriteAllLines(Path.Combine(folder,entry.Key+".jsonl"),entry.Value.Select(json.Serialize),new UTF8Encoding(false));
            }
            File.WriteAllText(Path.Combine(folder,"metadata.json"),json.Serialize(new {
                collectorVersion="2.0.0-local",claimedModel=Model,claimedTransport=Transport,
                modelDetected=false,source="xinput_state",slot=Slot,reader=Reader,preflightPassed=Verified,
                configurationWrites=false,automaticUpload=false,hostTimingIsPollingRate=false,
                sessions=Records.Select(x=>Analysis.Summary(x.Key,x.Value)),events=Events,
                rawInputReports=RawRecords.Count,rawInputAssociationWithSelectedSlot="unverified"
            }),new UTF8Encoding(false));
            if(XInputCapabilities.Count>0)File.WriteAllText(Path.Combine(folder,"xinput-capabilities.json"),json.Serialize(XInputCapabilities),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"windows-inventory.json"),json.Serialize(Inventory),new UTF8Encoding(false));
            if(RawRecords.Count>0)File.WriteAllLines(Path.Combine(folder,"raw-input.jsonl"),RawRecords.Select(json.Serialize),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"README.txt"),
                "Local input evidence only. Model/transport are owner claims. No HID decoding, configuration, vibration, firmware, or upload.\r\n"
                +"Incomplete checks are retained. Review files before sharing. Host samples are not polling-rate or latency measurements.\r\n");
            var files=Directory.GetFiles(folder).OrderBy(x=>x).Select(path=>new {
                path=Path.GetFileName(path),size=new FileInfo(path).Length,
                sha256=Hash(File.ReadAllBytes(path))}).ToArray();
            File.WriteAllText(Path.Combine(folder,"manifest.json"),json.Serialize(new {schema="input-diagnostic/1",files}),new UTF8Encoding(false));
            string archive=folder+".zip";ZipFile.CreateFromDirectory(folder,archive);return archive;
        }
        public static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
