using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace InputDiagnostic {
    static class SelfTest {
        static int checks;
        static void Check(bool result,string name) {if(!result)throw new Exception("FAILED: "+name);checks++;}
        static Sample Row(int slot=0,ushort buttons=0,byte lt=0,byte rt=0,short lx=0,short ly=0,short rx=0,short ry=0) {
            return new Sample("preflight",slot,new State{Packet=1,Pad=new Pad{Buttons=buttons,LT=lt,RT=rt,LX=lx,LY=ly,RX=rx,RY=ry}},0);
        }
        public static void Run() {
            Check(Marshal.SizeOf(typeof(Pad))==12,"gamepad ABI");
            Check(Marshal.SizeOf(typeof(State))==16,"state ABI");
            Check(Marshal.SizeOf(typeof(Capabilities))==20,"capabilities ABI");
            Check(Marshal.OffsetOf(typeof(State),"Pad").ToInt32()==4,"state offset");
            byte[] raw={0x7b,0,0,0,0,0x10,71,212,0,0x80,0xff,0x7f,0x2e,0xfb,0x29,9};
            IntPtr memory=Marshal.AllocHGlobal(16);
            try {Marshal.Copy(raw,0,memory,16);var state=(State)Marshal.PtrToStructure(memory,typeof(State));
                Check(state.Packet==123 && state.Pad.Buttons==4096 && state.Pad.LT==71 && state.Pad.RT==212
                    && state.Pad.LX==-32768 && state.Pad.LY==32767 && state.Pad.RX==-1234 && state.Pad.RY==2345,"native byte decoding");
            }finally{Marshal.FreeHGlobal(memory);}
            var good=new List<Sample>{Row(),Row(buttons:4096),Row(),Row(lt:127),Row(),Row(rt:255),Row()};
            Check(Analysis.Verify(good),"preflight success");
            Check(!Analysis.Verify(new List<Sample>{Row(),Row(lx:256),Row(ly:128)}),"drift cannot verify source");
            Check(!Analysis.Verify(new List<Sample>{Row(),Row(lt:128),Row(),Row(rt:255),Row()}),"triggers alone cannot verify source");
            Check(!Analysis.Verify(new List<Sample>{Row(),Row(buttons:4096,lt:255,rt:255)}),"held controls require release");
            Check(!Analysis.Verify(new List<Sample>{Row(),Row(buttons:8192,lt:255,rt:255),Row()}),"wrong button cannot verify A");
            Check(!Analysis.Verify(new List<Sample>{Row(),Row(buttons:4096,lt:3,rt:3),Row()}),"trigger noise cannot verify source");
            var mixed=new List<Sample>(good);mixed.Add(Row(slot:1));
            Check(!Analysis.Verify(mixed),"slot switch invalidates source");
            Check((string)Analysis.Summary("A",mixed)["status"]=="inconclusive_source_changed","mixed-slot summary");
            Check(((string)Analysis.Summary("A",new List<Sample>{Row(),Row(lx:256)})["status"]).StartsWith("inconclusive"),"drift cannot verify button");
            Check((string)Analysis.Summary("A",new List<Sample>{Row(),Row(buttons:4096),Row()})["status"]=="requested_control_observed","button cycle");
            Check(((string)Analysis.Summary("dpad_up_right",new List<Sample>{Row(),Row(buttons:1),Row(buttons:8),Row()})["status"]).StartsWith("inconclusive"),"diagonal must be simultaneous");
            Check((string)Analysis.Summary("LT",new List<Sample>{Row(),Row(lt:128),Row()})["status"]=="requested_control_observed","analog cycle");
            Check(((string)Analysis.Summary("left_stick",new List<Sample>{Row(),Row(lx:256)})["status"]).StartsWith("inconclusive"),"stick centre noise");
            Check((string)Analysis.Summary("left_stick",new List<Sample>{Row(),Row(lx:-32768),Row()})["status"]=="requested_control_observed","stick travel");
            Check((string)Analysis.Summary("neutral",new List<Sample>{Row(),Row(lx:256)})["status"]=="requested_control_observed","quiet buttons baseline");
            Check((string)Analysis.Summary("RT",new List<Sample>())["status"]=="no_samples","empty capture");
            var tracker=new PressTracker();
            Check(tracker.Update(new State(),0).Count==0,"no phantom press");
            var press=tracker.Update(new State{Pad=new Pad{Buttons=4096}},100);
            Check(press.Count==1 && press[0].control=="A" && press[0].down,"live press edge");
            Check(tracker.Update(new State{Pad=new Pad{Buttons=4096}},600).Count==0 && tracker.HeldText.Contains("0.50 s"),"live hold duration");
            var release=tracker.Update(new State(),1100);
            Check(release.Count==1 && !release[0].down && release[0].duration_ms==1000,"release duration");
            tracker.Update(new State{Pad=new Pad{LT=128}},1200);tracker.Disconnect();
            Check(tracker.Update(new State(),1300).Count==0,"disconnect is not a successful release");
            string temp=Path.Combine(Path.GetTempPath(),"x20ctl-input-selftest-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try {
                var evidence=new Evidence{Model="X05 Pro",Transport="USB cable",Reader="synthetic; no Windows DLL loaded",Verified=true};
                foreach(var row in good)evidence.Add("preflight",row);
                evidence.Event("disconnected","synthetic disconnect retained");
                string archive=evidence.Export(temp);string folder=archive.Substring(0,archive.Length-4);
                var json=new JavaScriptSerializer();
                var manifest=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"manifest.json")));
                using(var zip=ZipFile.OpenRead(archive)) {
                    Check(zip.Entries.Count==5,"export file count");
                    foreach(var entry in zip.Entries)using(var stream=entry.Open())using(var buffer=new MemoryStream()) {
                        stream.CopyTo(buffer);Check(buffer.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(folder,entry.FullName))),"archive bytes "+entry.FullName);
                    }
                }
                foreach(Dictionary<string,object> item in (System.Collections.IEnumerable)manifest["files"]) {
                    byte[] bytes=File.ReadAllBytes(Path.Combine(folder,(string)item["path"]));
                    Check(bytes.Length==Convert.ToInt32(item["size"]) && Evidence.Hash(bytes)==(string)item["sha256"],"manifest digest");
                }
            }finally{Directory.Delete(temp,true);}
            var stalled=new System.Threading.Tasks.TaskCompletionSource<Inventory>();
            var timeout=AsyncInventory.Bound(stalled.Task,20).GetAwaiter().GetResult();
            Check(timeout.errors.Count==1 && timeout.interfaces.Count==0,"bounded inventory timeout");
            var known=new Inventory();
            Check(Object.ReferenceEquals(AsyncInventory.Bound(System.Threading.Tasks.Task.FromResult(known),100).GetAwaiter().GetResult(),known),"completed inventory retained");
            var grouped=Inventory.Group(new[]{new InterfaceInfo{path="one",container="shared"},new InterfaceInfo{path="two",container="shared"},new InterfaceInfo{path="three"},new InterfaceInfo{path="four"}});
            Check(grouped.Count==3 && grouped[0].Count==2,"shared container grouping");
            Check(Inventory.Group(new[]{new InterfaceInfo{path="one",container="first",vid=1,pid=2},new InterfaceInfo{path="two",container="second",vid=1,pid=2}}).Count==2,"VID/PID does not merge devices");
            Check(RawObserver.ValidReports(15,2,62,24),"Raw Input framing");
            Check(!RawObserver.ValidReports(15,2,61,24),"truncated Raw Input framing");
            Check(!RawObserver.ValidReports(0,1,32,24),"empty Raw Input framing");
            Check(!RawObserver.ValidReports(5000,1,5032,24),"oversized Raw Input report");
            var bounded=new Evidence();Check(!bounded.AddRaw(new string('x',4*1024*1024)),"raw evidence size cap");
            var rear=Analysis.Summary("rear_left",new List<Sample>{Row(),Row(buttons:4096),Row()});
            Check((string)rear["status"]=="ordinary_output_observed" && (bool)rear["independent_rear_control_verified"]==false,"rear aliases do not prove independent controls");
            Console.WriteLine("{\"status\":\"passed\",\"hardware_access\":false,\"checks\":"+checks+",\"version\":\"2.0.0-local\"}");
        }
    }
}
