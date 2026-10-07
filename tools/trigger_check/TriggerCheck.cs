// Local read-only gameplay capture. No configuration, output, feature or network API.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace TriggerCheck {
    sealed class Frame {
        public string Source, Action, Phase, Hex;
        public int Left, Right;
        public Dictionary<string, object> Values;
        public static Frame Hid(byte[] data) {
            if (data.Length != 10 || data[0] != 0 || (data[3] > 7 && data[3] != 15))
                throw new InvalidDataException("Unrecognized HID input layout; no trigger values inferred.");
            return new Frame { Source="hid_input", Left=data[8], Right=data[9], Hex=BitConverter.ToString(data).Replace("-", "").ToLowerInvariant() };
        }
    }

    static class Analysis {
        public static Dictionary<string, object> Build(List<Frame> frames) {
            if (frames.Select(f => f.Source).Distinct().Count() > 1)
                throw new InvalidDataException("Different input sources require separate runs.");
            var neutral=frames.Where(f => f.Action=="neutral").ToList();
            bool released=neutral.Count>0 && neutral.All(f => f.Left==0 && f.Right==0);
            return new Dictionary<string, object> {
                {"schema", "trigger-check/1"}, {"source", frames.Select(f=>f.Source).FirstOrDefault() ?? "unknown"},
                {"baseline_released", released}, {"LT", ForTrigger(frames, "LT", released)},
                {"RT", ForTrigger(frames, "RT", released)},
                {"interpretation", "Observed input values in this session, not proof of sensor type, calibration or configuration support."}
            };
        }
        static Dictionary<string, object> ForTrigger(List<Frame> frames, string action, bool released) {
            var selected=frames.Where(f=>f.Action==action).ToList();
            int[] values=selected.Select(f=>action=="LT" ? f.Left : f.Right).Distinct().OrderBy(v=>v).ToArray();
            int[] other=selected.Select(f=>action=="LT" ? f.Right : f.Left).Distinct().OrderBy(v=>v).ToArray();
            int[] middle=values.Where(v=>v>0 && v<255).ToArray();
            string status=selected.Count==0 ? "inconclusive_no_samples" : !released ? "inconclusive_baseline_not_released" :
                other.Any(v=>v!=0) ? "inconclusive_other_trigger_moved" : middle.Length>0 ? "intermediate_values_observed" :
                values.Contains(0) && values.Contains(255) ? "endpoints_only_observed" : "inconclusive_full_pull_not_observed";
            return new Dictionary<string, object> {
                {"status",status}, {"samples",selected.Count}, {"unique_values",values}, {"intermediate_values",middle},
                {"other_trigger_values",other}, {"released_observed",values.Contains(0)}, {"full_value_observed",values.Contains(255)}
            };
        }
        public static List<Frame> ReadFolder(string folder) {
            string[] files=Directory.GetFiles(folder,"*.jsonl",SearchOption.AllDirectories);
            if(files.Length>128) throw new InvalidDataException("Too many input files.");
            var frames=new List<Frame>(); long total=0;
            foreach(string file in files) {
                long size=new FileInfo(file).Length; total+=size;
                if(size>4*1024*1024 || total>32*1024*1024) throw new InvalidDataException("Input files exceed limits.");
                foreach(string line in File.ReadLines(file)) {
                    if(line.Length==0) continue;
                    if(line.Length>16384 || frames.Count>=20000) throw new InvalidDataException("Input record limit exceeded.");
                    var row=Program.Json.Deserialize<Dictionary<string,object>>(line);
                    string action=Convert.ToString(row["action"]);
                    if(action!="neutral" && action!="LT" && action!="RT") continue;
                    string source=Convert.ToString(row["source"]); Frame frame;
                    if(source=="hid_input") {
                        string hex=Convert.ToString(row["report_hex"]);
                        if(hex.Length!=20) throw new InvalidDataException("Unexpected HID report length.");
                        var bytes=new byte[10];
                        for(int i=0;i<10;i++) bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);
                        frame=Frame.Hid(bytes);
                    } else if(source=="xinput_state") {
                        var fields=(Dictionary<string,object>)row["values"];
                        int left=Convert.ToInt32(fields["lt"]), right=Convert.ToInt32(fields["rt"]);
                        if(left<0 || left>255 || right<0 || right>255) throw new InvalidDataException("Trigger value out of range.");
                        frame=new Frame { Source=source, Left=left, Right=right };
                    } else throw new InvalidDataException("Unknown input source.");
                    frame.Action=action; frames.Add(frame);
                }
            }
            return frames;
        }
    }

    sealed class Device {
        public string Path;
        public int Slot=-1;
        public ushort Vid, Pid, InputLength, OutputLength, FeatureLength, UsagePage, Usage;
        public string Key { get { return Slot>=0 ? "xinput:"+Slot : Path; } }
        public string Label { get { return Slot>=0 ? "XInput player slot "+(Slot+1) : String.Format("HID gamepad {0:X4}:{1:X4} (10-byte candidate layout)",Vid,Pid); } }
        public Dictionary<string,object> Export() {
            return new Dictionary<string,object> {
                {"input_source", Slot>=0 ? "xinput_state" : "hid_input"}, {"slot",Slot},
                {"vid", Slot>=0 ? null : Vid.ToString("X4")}, {"pid", Slot>=0 ? null : Pid.ToString("X4")},
                {"input_length",InputLength},{"output_length",OutputLength},{"feature_length",FeatureLength},
                {"usage_page",UsagePage},{"usage",Usage},
                {"model_detection",false},{"selection","disconnect/reconnect difference and explicit tester selection"},
                {"hid_layout",Slot>=0 ? "not applicable" : "candidate from X15-labelled capture; not a model identifier"}
            };
        }
    }

    interface Reader : IDisposable { Frame Read(int timeout); }
    sealed class XReader : Reader {
        readonly int slot;
        public XReader(int selected) { slot=selected; }
        public Frame Read(int timeout) {
            Native.XState state;
            if(!Native.TryState(slot,out state)) throw new IOException("Selected XInput slot disconnected.");
            return new Frame { Source="xinput_state",Left=state.Pad.Left,Right=state.Pad.Right,
                Values=new Dictionary<string,object>{{"slot",slot},{"packet",state.Packet},{"buttons",state.Pad.Buttons},
                    {"lt",state.Pad.Left},{"rt",state.Pad.Right}} };
        }
        public void Dispose() { }
    }
    sealed class HReader : Reader {
        readonly SafeFileHandle handle;
        readonly FileStream stream;
        public HReader(Device device) {
            handle=Native.CreateFileW(device.Path,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);
            if(handle.IsInvalid) { handle.Dispose(); throw new IOException("Selected HID input unavailable."); }
            try { stream=new FileStream(handle,FileAccess.Read,10,true); }
            catch { handle.Dispose(); throw; }
        }
        public Frame Read(int timeout) {
            // FileStream owns/pins the async buffer until completion, including cancellation.
            var bytes=new byte[10];
            IAsyncResult pending=stream.BeginRead(bytes,0,bytes.Length,null,null);
            using(WaitHandle finished=pending.AsyncWaitHandle) {
                if(!finished.WaitOne(timeout)) {
                    Native.CancelIoEx(handle,IntPtr.Zero);
                    if(!finished.WaitOne(1000)) throw new IOException("HID read cancellation did not finish; stop this run.");
                    try { stream.EndRead(pending); } catch(IOException) { }
                    return null;
                }
                int count=stream.EndRead(pending);
                if(count==0) throw new IOException("Selected HID device disconnected.");
                if(count!=10) throw new InvalidDataException("HID report length changed; no values inferred.");
                return Frame.Hid(bytes);
            }
        }
        public void Dispose() { Native.CancelIoEx(handle,IntPtr.Zero); stream.Dispose(); }
    }

    static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct Interface { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] public struct Attributes { public int Size; public ushort Vid,Pid,Version; }
        [StructLayout(LayoutKind.Sequential)] public struct Caps {
            public ushort Usage,UsagePage,Input,Output,Feature;
            [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)] public ushort[] Reserved;
            [MarshalAs(UnmanagedType.ByValArray,SizeConst=10)] public ushort[] Counts;
        }
        [StructLayout(LayoutKind.Sequential)] public struct Pad { public ushort Buttons; public byte Left,Right; public short LX,LY,RX,RY; }
        [StructLayout(LayoutKind.Sequential)] public struct XState { public uint Packet; public Pad Pad; }
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SetupDiGetClassDevsW(ref Guid guid,IntPtr enumerator,IntPtr hwnd,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr info,ref Guid guid,uint index,ref Interface item);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set,ref Interface item,IntPtr detail,uint size,out uint needed,IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)] public static extern bool CancelIoEx(SafeFileHandle handle,IntPtr overlap);
        [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attributes);
        [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data,out Caps caps);
        [DllImport("XInput1_4.dll",EntryPoint="XInputGetState")] static extern uint State14(uint slot,out XState state);
        [DllImport("XInput9_1_0.dll",EntryPoint="XInputGetState")] static extern uint State91(uint slot,out XState state);
        public static bool TryState(int slot,out XState state) {
            try { return State14((uint)slot,out state)==0; }
            catch(DllNotFoundException) { return State91((uint)slot,out state)==0; }
            catch(EntryPointNotFoundException) { return State91((uint)slot,out state)==0; }
        }
        public static List<Device> Inventory() {
            var rows=new List<Device>();
            for(int slot=0;slot<4;slot++) { XState state; if(TryState(slot,out state)) rows.Add(new Device { Slot=slot }); }
            Guid guid=new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
            IntPtr set=SetupDiGetClassDevsW(ref guid,IntPtr.Zero,IntPtr.Zero,0x12);
            if(set==new IntPtr(-1)) throw new IOException("HID inventory unavailable.");
            try {
                for(uint i=0;i<1024;i++) {
                    var item=new Interface { Size=Marshal.SizeOf(typeof(Interface)) };
                    if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref item)) {
                        if(Marshal.GetLastWin32Error()!=259) throw new IOException("HID enumeration failed.");
                        return rows;
                    }
                    uint needed;
                    SetupDiGetDeviceInterfaceDetailW(set,ref item,IntPtr.Zero,0,out needed,IntPtr.Zero);
                    if(needed<8 || needed>8192) continue;
                    IntPtr buffer=Marshal.AllocHGlobal((int)needed);
                    try {
                        Marshal.WriteInt32(buffer,IntPtr.Size==8 ? 8 : 6);
                        if(!SetupDiGetDeviceInterfaceDetailW(set,ref item,buffer,needed,out needed,IntPtr.Zero)) continue;
                        string path=Marshal.PtrToStringUni(IntPtr.Add(buffer,4));
                        using(SafeFileHandle handle=CreateFileW(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                            if(handle.IsInvalid) continue;
                            var attributes=new Attributes { Size=Marshal.SizeOf(typeof(Attributes)) };
                            if(!HidD_GetAttributes(handle,ref attributes)) continue;
                            // Never infer a model from this shared chip-family identity.
                            if(attributes.Vid!=0x0079 || attributes.Pid!=0x181c) continue;
                            IntPtr preparsed;
                            if(!HidD_GetPreparsedData(handle,out preparsed)) continue;
                            try {
                                Caps caps;
                                if(HidP_GetCaps(preparsed,out caps)!=0x110000 || caps.UsagePage!=1 || caps.Usage!=5 || caps.Input!=10 || caps.Output!=5 || caps.Feature!=0) continue;
                                rows.Add(new Device { Path=path,Vid=attributes.Vid,Pid=attributes.Pid,InputLength=caps.Input,
                                    OutputLength=caps.Output,FeatureLength=caps.Feature,UsagePage=caps.UsagePage,Usage=caps.Usage });
                            } finally { HidD_FreePreparsedData(preparsed); }
                        }
                    } finally { Marshal.FreeHGlobal(buffer); }
                }
                throw new IOException("Device inventory limit exceeded.");
            } finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }

    static class Program {
        public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
        static volatile bool cancelled;
        static readonly Encoding Utf8=new UTF8Encoding(false);
        static string Ask(string message) { Console.Write(message); string answer=Console.ReadLine(); if(answer==null) throw new OperationCanceledException(); return answer.Trim(); }
        static void Enter(string message) { Ask(message+" Then press ENTER: "); }
        static string ShortText(string text) { return new string(text.Take(80).Where(c=>!Char.IsControl(c)).ToArray()); }
        static void WriteJson(string path,object data) { File.WriteAllText(path,Json.Serialize(data),Utf8); }
        static void Check(bool condition) { if(!condition) throw new Exception("Offline self-test failed."); }
        static Frame TestFrame(string action,int left,int right) { return new Frame { Source="xinput_state",Action=action,Left=left,Right=right }; }
        static object SelfTest() {
            var frames=new List<Frame> { TestFrame("neutral",0,0),TestFrame("LT",0,0),TestFrame("LT",64,0),TestFrame("LT",255,0),TestFrame("RT",0,0),TestFrame("RT",0,255) };
            var report=Analysis.Build(frames);
            Check((string)((Dictionary<string,object>)report["LT"])["status"]=="intermediate_values_observed");
            Check((string)((Dictionary<string,object>)report["RT"])["status"]=="endpoints_only_observed");
            Check(Frame.Hid(new byte[]{0,0,0,15,128,128,128,128,64,192}).Right==192);
            bool rejected=false; try { Frame.Hid(new byte[]{1,0,0,15,128,128,128,128,64,192}); } catch(InvalidDataException) { rejected=true; }
            Check(rejected);
            Check(Marshal.SizeOf(typeof(Native.Pad))==12 && Marshal.SizeOf(typeof(Native.XState))==16 && Marshal.SizeOf(typeof(Native.Caps))==64);
            return new { status="passed",hardware_access=false,version="1.0.0" };
        }
        static void Capture(Reader reader,string action,string folder,List<Frame> samples) {
            string[] phases=action=="neutral" ? new[]{"released"} : new[]{"released","quarter","half","three_quarters","full","release"};
            int[] seconds=action=="neutral" ? new[]{3} : new[]{2,3,3,3,2,3};
            using(var file=new StreamWriter(Path.Combine(folder,action+".jsonl"),false,Utf8)) {
                var total=Stopwatch.StartNew(); int count=0;
                for(int step=0;step<phases.Length;step++) {
                    string phase=phases[step];
                    Console.WriteLine("\n"+(action=="neutral" ? "Release BOTH triggers; hands off." : action+": "+phase.Replace('_',' ')+". Move slowly to this position; keep the other trigger RELEASED.")+" ("+seconds[step]+" seconds)");
                    var clock=Stopwatch.StartNew(); long display=-1000;
                    while(clock.ElapsedMilliseconds<seconds[step]*1000) {
                        if(cancelled) throw new OperationCanceledException();
                        if(count>=2500) throw new IOException("Capture sample limit reached.");
                        Frame frame=reader.Read(100);
                        if(frame==null) continue;
                        frame.Action=action; frame.Phase=phase; samples.Add(frame); count++;
                        var row=new Dictionary<string,object> { {"source",frame.Source},{"action",action},{"phase",phase},
                            {"timestamp",DateTime.UtcNow.ToString("o")},{"elapsed_ms",total.ElapsedMilliseconds} };
                        if(frame.Source=="hid_input") { row["report_hex"]=frame.Hex; row["report_length"]=10; row["framing"]="Windows ReadFile collection bytes"; }
                        else row["values"]=frame.Values;
                        file.WriteLine(Json.Serialize(row)); file.Flush();
                        if(clock.ElapsedMilliseconds-display>=150) {
                            Console.Write("\r  LT: {0,3} /255    RT: {1,3} /255       ",frame.Left,frame.Right);
                            display=clock.ElapsedMilliseconds;
                        }
                        Thread.Sleep(20);
                    }
                    Console.WriteLine();
                }
            }
        }
        static string Describe(Dictionary<string,object> result) {
            string status=(string)result["status"];
            if(status=="intermediate_values_observed") return "INTERMEDIATE VALUES OBSERVED: this input path reported partial pulls.";
            if(status=="endpoints_only_observed") return "ONLY 0 AND 255 OBSERVED: released/full values only in this test; this does not prove the physical trigger is digital.";
            return "INCONCLUSIVE: "+status.Replace("inconclusive_","").Replace('_',' ')+". Repeat with both triggers released at the start and pull only the instructed trigger.";
        }
        static string Save(string folder,List<Frame> frames,Dictionary<string,object> metadata) {
            var report=Analysis.Build(frames); report["collection_complete"]=metadata["complete"];
            WriteJson(Path.Combine(folder,"summary.json"),report);
            string text="TRIGGER CHECK RESULTS\r\n\r\nLT: "+Describe((Dictionary<string,object>)report["LT"])+"\r\nRT: "+Describe((Dictionary<string,object>)report["RT"])+
                "\r\n\r\nSource: "+report["source"]+"\r\nComplete run: "+metadata["complete"]+
                "\r\nPartial runs are research evidence only. Firmware, other modes, calibration and configuration writes are not verified.\r\nHost sampling is not a polling-rate or latency measurement.\r\n";
            File.WriteAllText(Path.Combine(folder,"SUMMARY.txt"),text,Utf8);
            WriteJson(Path.Combine(folder,"metadata.json"),metadata);
            var files=new List<object>();
            foreach(string file in Directory.GetFiles(folder).OrderBy(p=>p)) {
                using(var hash=SHA256.Create()) files.Add(new { path=Path.GetFileName(file),size=new FileInfo(file).Length,sha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant() });
            }
            WriteJson(Path.Combine(folder,"manifest.json"),new { schema="trigger-check/1",files=files });
            string archive=folder+".zip";
            ZipFile.CreateFromDirectory(folder,archive,CompressionLevel.Optimal,false);
            Console.WriteLine("\n"+text+"\nSend this result ZIP to Amjad:\n"+archive);
            return archive;
        }
        static int Live() {
            Console.WriteLine("X15 Trigger Check 1.0.0 - Windows 10/11 x64\nThis reads gameplay input only. No settings changes or uploads.\nClose games, Steam and controller/remapping apps first.\nKeep your current mode. CTRL+C stops; partial results stay local.\n");
            if(!Ask("Record this controller's raw input locally? Type y to start: ").Equals("y",StringComparison.OrdinalIgnoreCase)) return 0;
            string model=ShortText(Ask("Printed controller model (ENTER for X15; do not enter a serial number): "));
            if(model.Length==0) model="X15";
            string mode=ShortText(Ask("Mode name IF KNOWN (ENTER for unknown; do not guess): ")); if(mode.Length==0) mode="unknown";
            string transport=ShortText(Ask("Connection: receiver / USB cable / Bluetooth: "));
            Enter("Disconnect ONLY the target controller: unplug its receiver/cable, or turn it off for Bluetooth.");
            var before=new HashSet<string>(Native.Inventory().Select(d=>d.Key),StringComparer.OrdinalIgnoreCase);
            Enter("Reconnect that controller using the same connection/mode; wait until it is connected.");
            var candidates=Native.Inventory().Where(d=>!before.Contains(d.Key)).ToList();
            if(candidates.Count==0) { Console.WriteLine("No new supported input path appeared. This small tool recognizes standard XInput and the recorded 0079:181C, 10-byte HID profile.\nDo not guess another device. Report this message and the mode to Amjad."); return 2; }
            for(int i=0;i<candidates.Count;i++) Console.WriteLine((i+1)+". "+candidates[i].Label);
            int index;
            if(!Int32.TryParse(Ask("Choose the input path for your controller: "),out index) || index<1 || index>candidates.Count) return 2;
            Device selected=candidates[index-1];
            Console.WriteLine("Selected "+selected.Label+". A shared VID/PID is not proof of a controller model.\nIf two paths belong to your controller, test one now and the other in a separate run.");
            Enter("Release both triggers. During each test, move slowly and hold the approximate positions shown. Touch no other controls.");
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"results"); Directory.CreateDirectory(root);
            string folder=Path.Combine(root,"trigger-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
            Directory.CreateDirectory(folder); WriteJson(Path.Combine(folder,"selected-device.json"),selected.Export());
            var metadata=new Dictionary<string,object> { {"version","1.0.0"},{"created_at_utc",DateTime.UtcNow.ToString("o")},
                {"claimed_model",model},{"claimed_mode",mode},{"claimed_transport",transport},{"model_detected",false},
                {"firmware","unknown"},{"hardware_revision","unknown"},{"input_source",selected.Slot>=0 ? "xinput_state" : "hid_input"},
                {"automatic_upload",false},{"configuration_writes",false},{"serials_requested",false},{"device_paths_exported",false},
                {"raw_input_consent",true},{"complete",false},{"host_sample_sleep_ms",20} };
            var frames=new List<Frame>(); int exit=0;
            try {
                using(Reader reader=selected.Slot>=0 ? (Reader)new XReader(selected.Slot) : new HReader(selected)) {
                    Capture(reader,"neutral",folder,frames);
                    Enter("Next: LEFT trigger only. Release both now."); Capture(reader,"LT",folder,frames);
                    Enter("Next: RIGHT trigger only. Release both now."); Capture(reader,"RT",folder,frames);
                }
                metadata["complete"]=true;
            } catch(Exception error) {
                metadata["stopped_reason"]=error is OperationCanceledException ? "cancelled" : error.GetType().Name;
                Console.WriteLine("\nCapture stopped ("+metadata["stopped_reason"]+"). Collected samples are kept; do not treat a partial run as a support verdict."); exit=1;
            }
            Save(folder,frames,metadata);
            Console.WriteLine("Open SUMMARY.txt if you want to check the result before sharing. To compare another normal mode, switch it yourself and run the tool again.");
            return exit;
        }
        public static int Main(string[] args) {
            try {
                // These branches deliberately precede every device API call.
                if(args.Length==1 && args[0]=="--self-test") { Console.WriteLine(Json.Serialize(SelfTest())); return 0; }
                if(args.Length==2 && args[0]=="--analyze") { Console.WriteLine(Json.Serialize(Analysis.Build(Analysis.ReadFolder(args[1])))); return 0; }
                if(args.Length!=0) { Console.Error.WriteLine("Usage: TriggerCheck.exe [--self-test | --analyze INPUT_FOLDER]"); return 2; }
                Console.CancelKeyPress+=(sender,e)=>{cancelled=true;e.Cancel=true;};
                return Live();
            } catch(Exception error) { Console.Error.WriteLine("Stopped: "+error.GetType().Name+". No upload or settings change. Existing result files remain local."); return 1; }
        }
    }
}
