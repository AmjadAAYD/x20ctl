using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace InputDiagnostic {
    public sealed class InterfaceInfo {
        public string kind,path,container,product,manufacturer,hardwareIds,status,serial="Not collected";
        public int vid,pid,usagePage,usage,inputLength,outputLength,featureLength;
    }
    public sealed class Inventory {
        public List<InterfaceInfo> interfaces=new List<InterfaceInfo>();
        public List<object> rawInput=new List<object>();
        public List<string> errors=new List<string>();
        public string transportEvidence="Unknown - bus visibility does not establish cable versus receiver";
        public string originalReportDescriptors="Unavailable - parsed HID capabilities are not original descriptor bytes";
        public string directInput="Not enumerated separately; no DirectInput capability claim";
        public string ble="Not queried; GATT service discovery is an optional separate research capture";
        public static List<List<InterfaceInfo>> Group(IEnumerable<InterfaceInfo> rows) {
            return rows.GroupBy(x=>String.IsNullOrEmpty(x.container)?"interface:"+x.path:"container:"+x.container).Select(x=>x.ToList()).ToList();
        }
    }
    public static class WindowsInventory {
        [StructLayout(LayoutKind.Sequential)] struct DevInfo {public int size;public Guid guid;public uint instance;public IntPtr reserved;}
        [StructLayout(LayoutKind.Sequential)] struct DevInterface {public int size;public Guid guid;public int flags;public IntPtr reserved;}
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey {public Guid format;public uint id;}
        [StructLayout(LayoutKind.Sequential)] struct Attributes {public int size;public ushort vid,pid,version;}
        [StructLayout(LayoutKind.Sequential)] struct Caps {public ushort usage,page,input,output,feature;[MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] reserved;[MarshalAs(UnmanagedType.ByValArray,SizeConst=10)]public ushort[] counts;}
        [StructLayout(LayoutKind.Sequential)] struct RawDevice {public IntPtr handle;public uint type;}
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevsW(ref Guid guid,IntPtr enumerator,IntPtr hwnd,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr info,ref Guid guid,uint index,ref DevInterface item);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set,ref DevInterface item,IntPtr detail,uint size,out uint needed,ref DevInfo info);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set,ref DevInfo info,uint property,out uint type,byte[] value,uint size,out uint required);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDevicePropertyW(IntPtr set,ref DevInfo info,ref PropertyKey key,out uint type,byte[] value,uint size,out uint required,uint flags);
        [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("hid.dll")]static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attrs);
        [DllImport("hid.dll")]static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")]static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")]static extern int HidP_GetCaps(IntPtr data,out Caps caps);
        [DllImport("hid.dll")]static extern bool HidD_GetProductString(SafeFileHandle handle,byte[] buffer,int length);
        [DllImport("hid.dll")]static extern bool HidD_GetManufacturerString(SafeFileHandle handle,byte[] buffer,int length);
        [DllImport("hid.dll")]static extern bool HidD_GetSerialNumberString(SafeFileHandle handle,byte[] buffer,int length);
        [DllImport("user32.dll")]static extern uint GetRawInputDeviceList([Out]RawDevice[] devices,ref uint count,uint size);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint GetRawInputDeviceInfoW(IntPtr handle,uint command,IntPtr data,ref uint size);
        static string Text(byte[] bytes) {return Encoding.Unicode.GetString(bytes).TrimEnd('\0');}
        static string Property(IntPtr set,ref DevInfo info,uint property) {
            uint type,size;var bytes=new byte[8192];
            return SetupDiGetDeviceRegistryPropertyW(set,ref info,property,out type,bytes,(uint)bytes.Length,out size)?Encoding.Unicode.GetString(bytes,0,(int)Math.Min(size,(uint)bytes.Length)).TrimEnd('\0'):"Unknown";
        }
        public static Inventory Read(bool includeSerial=false) {
            var result=new Inventory();
            foreach(var type in new[]{Tuple.Create("HID","4d1e55b2-f16f-11cf-88cb-001111000030"),Tuple.Create("USB","a5dcbf10-6530-11d2-901f-00a0c906bed8")}) {
                Guid guid=new Guid(type.Item2);IntPtr set=SetupDiGetClassDevsW(ref guid,IntPtr.Zero,IntPtr.Zero,18);
                if(set==new IntPtr(-1)){result.errors.Add(type.Item1+" inventory unavailable");continue;}
                try {for(uint index=0;index<1024;index++) {
                    var item=new DevInterface{size=Marshal.SizeOf(typeof(DevInterface))};
                    if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,index,ref item))break;
                    var info=new DevInfo{size=Marshal.SizeOf(typeof(DevInfo))};uint size;
                    SetupDiGetDeviceInterfaceDetailW(set,ref item,IntPtr.Zero,0,out size,ref info);
                    if(size<8 || size>65536)continue;
                    IntPtr buffer=Marshal.AllocHGlobal((int)size);
                    try {
                        Marshal.WriteInt32(buffer,IntPtr.Size==8?8:6);
                        if(!SetupDiGetDeviceInterfaceDetailW(set,ref item,buffer,size,out size,ref info))continue;
                        string path=Marshal.PtrToStringUni(IntPtr.Add(buffer,4));
                        var row=new InterfaceInfo{kind=type.Item1,path=path,status="metadata_only",
                            product=Property(set,ref info,12),manufacturer=Property(set,ref info,11),hardwareIds=Property(set,ref info,1)};
                        if(row.product=="Unknown")row.product=Property(set,ref info,0);
                        var key=new PropertyKey{format=new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),id=2};uint propType,needed;var bytes=new byte[16];
                        if(SetupDiGetDevicePropertyW(set,ref info,ref key,out propType,bytes,16,out needed,0) && needed==16) {
                            Guid container=new Guid(bytes);if(container!=Guid.Empty)row.container=container.ToString();
                        }
                        if(type.Item1=="HID")using(var handle=CreateFileW(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                            if(!handle.IsInvalid) {
                                var attrs=new Attributes{size=Marshal.SizeOf(typeof(Attributes))};
                                if(HidD_GetAttributes(handle,ref attrs)){row.vid=attrs.vid;row.pid=attrs.pid;}
                                var text=new byte[512];if(HidD_GetProductString(handle,text,text.Length))row.product=Text(text);
                                text=new byte[512];if(HidD_GetManufacturerString(handle,text,text.Length))row.manufacturer=Text(text);
                                if(includeSerial){text=new byte[512];row.serial=HidD_GetSerialNumberString(handle,text,text.Length)?Text(text):"Unknown";}
                                IntPtr data;
                                if(HidD_GetPreparsedData(handle,out data))try {Caps caps;
                                    if(HidP_GetCaps(data,out caps)>=0){row.usagePage=caps.page;row.usage=caps.usage;row.inputLength=caps.input;row.outputLength=caps.output;row.featureLength=caps.feature;row.status="parsed_caps";}
                                }finally{HidD_FreePreparsedData(data);}
                            }else row.status="metadata_access_unavailable";
                        }
                        // Retain only controller-related HID/USB interfaces, never keyboard/mouse input.
                        bool gamepad=row.kind=="HID" && row.usagePage==1 && new[]{4,5,8}.Contains(row.usage);
                        bool xbox=(row.hardwareIds??"").IndexOf("XUSB",StringComparison.OrdinalIgnoreCase)>=0 || (row.product??"").IndexOf("Controller",StringComparison.OrdinalIgnoreCase)>=0;
                        var id=Regex.Match(row.hardwareIds??"",@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})",RegexOptions.IgnoreCase);
                        if(id.Success && row.vid==0){row.vid=Convert.ToInt32(id.Groups[1].Value,16);row.pid=Convert.ToInt32(id.Groups[2].Value,16);}
                        result.interfaces.Add(row);
                    }finally{Marshal.FreeHGlobal(buffer);}
                }}finally{SetupDiDestroyDeviceInfoList(set);}
            }
            var controllerContainers=new HashSet<string>(result.interfaces.Where(x=>
                (x.kind=="HID" && x.usagePage==1 && new[]{4,5,8}.Contains(x.usage)) ||
                (x.product??"").IndexOf("Controller",StringComparison.OrdinalIgnoreCase)>=0).Select(x=>x.container).Where(x=>!String.IsNullOrEmpty(x)));
            result.interfaces=result.interfaces.Where(x=>controllerContainers.Contains(x.container??"") ||
                (x.kind=="HID" && x.usagePage==1 && new[]{4,5,8}.Contains(x.usage)) ||
                (x.product??"").IndexOf("Controller",StringComparison.OrdinalIgnoreCase)>=0).ToList();
            // A shared Windows container groups interfaces; it does not associate an XInput slot or verify the printed model.
            ReadRaw(result);
            return result;
        }
        static void ReadRaw(Inventory result) {
            uint count=0,entrySize=(uint)Marshal.SizeOf(typeof(RawDevice));
            if(GetRawInputDeviceList(null,ref count,entrySize)==UInt32.MaxValue || count>4096){result.errors.Add("Raw Input inventory unavailable");return;}
            var entries=new RawDevice[count];uint found=GetRawInputDeviceList(entries,ref count,entrySize);
            if(found==UInt32.MaxValue){result.errors.Add("Raw Input inventory changed; retry detection");return;}
            foreach(var entry in entries.Take((int)found).Where(x=>x.type==2)) {
                uint length=0;GetRawInputDeviceInfoW(entry.handle,0x20000007,IntPtr.Zero,ref length);
                if(length==0 || length>32768)continue;
                IntPtr name=Marshal.AllocHGlobal((int)(length+1)*2),detail=Marshal.AllocHGlobal(32);
                try {
                    if(GetRawInputDeviceInfoW(entry.handle,0x20000007,name,ref length)==UInt32.MaxValue)continue;
                    uint bytes=32;Marshal.WriteInt32(detail,32);
                    if(GetRawInputDeviceInfoW(entry.handle,0x2000000b,detail,ref bytes)==UInt32.MaxValue)continue;
                    int page=(ushort)Marshal.ReadInt16(detail,20),usage=(ushort)Marshal.ReadInt16(detail,22);
                    if(page!=1 || !new[]{4,5,8}.Contains(usage))continue;
                    result.rawInput.Add(new {path=Marshal.PtrToStringUni(name),vid=Marshal.ReadInt32(detail,8),pid=Marshal.ReadInt32(detail,12),usagePage=page,usage,
                        status="enumerated_not_linked_to_xinput_slot"});
                }finally{Marshal.FreeHGlobal(name);Marshal.FreeHGlobal(detail);}
            }
        }
    }
}
