Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public class HidScan {
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS {
        public ushort Usage; public ushort UsagePage;
        public ushort InputReportByteLength; public ushort OutputReportByteLength; public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps; public ushort NumberInputValueCaps; public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps; public ushort NumberOutputValueCaps; public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps; public ushort NumberFeatureValueCaps; public ushort NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll")] public static extern bool HidD_GetAttributes(IntPtr h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr pp);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr pp);
    [DllImport("hid.dll")] public static extern int  HidP_GetCaps(IntPtr pp, out HIDP_CAPS c);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetProductString(IntPtr h, char[] b, int len);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetManufacturerString(IntPtr h, char[] b, int len);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetSerialNumberString(IntPtr h, char[] b, int len);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr h, int f);
    [DllImport("setupapi.dll")] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, int i, ref SP_DEVICE_INTERFACE_DATA a);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref SP_DEVICE_INTERFACE_DATA a, IntPtr d, int size, ref int req, IntPtr dev);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateFile(string n, uint acc, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

    public static string[] GetPaths() {
        Guid g; HidD_GetHidGuid(out g);
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
        var list = new System.Collections.Generic.List<string>();
        var did = new SP_DEVICE_INTERFACE_DATA();
        did.cbSize = Marshal.SizeOf(did);
        for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref did); i++) {
            int req = 0;
            SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, ref req, IntPtr.Zero);
            IntPtr buf = Marshal.AllocHGlobal(req);
            Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
            if (SetupDiGetDeviceInterfaceDetail(set, ref did, buf, req, ref req, IntPtr.Zero))
                list.Add(Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4)));
            Marshal.FreeHGlobal(buf);
        }
        SetupDiDestroyDeviceInfoList(set);
        return list.ToArray();
    }

    public static string Describe(string path) {
        // open with no access so we can query even if the pad is exclusively grabbed
        IntPtr h = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.ToInt64() == -1) return path + "\n    <cannot open>";
        var a = new HIDD_ATTRIBUTES(); a.Size = Marshal.SizeOf(a);
        HidD_GetAttributes(h, ref a);
        var prod = new char[128]; var man = new char[128];
        HidD_GetProductString(h, prod, 256); HidD_GetManufacturerString(h, man, 256);
        string s = path + "\n";
        s += string.Format("    VID_{0:X4}  PID_{1:X4}  rev {2:X4}\n", a.VendorID, a.ProductID, a.VersionNumber);
        s += "    manufacturer : " + new string(man).Trim('\0') + "\n";
        s += "    product      : " + new string(prod).Trim('\0') + "\n";
        IntPtr pp;
        if (HidD_GetPreparsedData(h, out pp)) {
            HIDP_CAPS c;
            if (HidP_GetCaps(pp, out c) == 0x110000) {
                s += string.Format("    UsagePage    : 0x{0:X4}   Usage: 0x{1:X2}   {2}\n", c.UsagePage, c.Usage,
                        c.UsagePage >= 0xFF00 ? "  <<< VENDOR-DEFINED" : "");
                s += string.Format("    report bytes : input={0}  output={1}  feature={2}\n",
                        c.InputReportByteLength, c.OutputReportByteLength, c.FeatureReportByteLength);
            }
            HidD_FreePreparsedData(pp);
        }
        CloseHandle(h);
        return s;
    }
}
'@

foreach ($p in [HidScan]::GetPaths()) {
    if ($p -match 'vid_045e|vid_045E|VID_045E') { [HidScan]::Describe($p) }
}
"=== all other HID collections (compact) ==="
foreach ($p in [HidScan]::GetPaths()) {
    if ($p -notmatch '045e') {
        $d = [HidScan]::Describe($p)
        if ($d -match 'VENDOR-DEFINED') { $d }
    }
}
