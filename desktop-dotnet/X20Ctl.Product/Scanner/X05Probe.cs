using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace X20Ctl.Product.Scanner;

/// <summary>Windows side of the X05 Pro D-Input probe (see <see cref="X05Report"/>): opens the 0xFFA0 channel, sends the one reported command once, records the input reports around it.</summary>
public static class X05Probe
{
    public static InterfaceInfo? Find(Inventory inventory) =>
        inventory.interfaces.FirstOrDefault(i => i.kind == "HID" && i.vid == X05Report.Vid && i.pid == X05Report.Pid && i.usagePage == X05Report.UsagePage && i.path.Length > 0);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_SetOutputReport(SafeFileHandle handle, byte[] report, int length);
    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;

    public static async Task<X05Result> RunAsync(Inventory inventory)
    {
        if (ReviewSandbox.Active) return X05Report.Summarize([], [], "review sandbox: nothing sent");
        var channel = Find(inventory);
        if (channel is null) return X05Report.Summarize([], [], "channel not found: put the pad in D-Input (hold Back + Start), connect it by USB and try again");
        using var read = CreateFileW(channel.path, GENERIC_READ, 3, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        using var write = CreateFileW(channel.path, GENERIC_WRITE, 3, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (read.IsInvalid || write.IsInvalid) return X05Report.Summarize([], [], $"could not open the channel (Windows error {Marshal.GetLastWin32Error()})");
        int length = channel.inputLength > 1 ? channel.inputLength : X05Report.ReportLength;
        var before = new List<byte[]>(); var after = new List<byte[]>(); var target = before; var gate = new object();
        using var stream = new FileStream(read, FileAccess.Read, 0, true);
        using var stop = new CancellationTokenSource();
        var loop = Task.Run(async () =>
        {
            var buffer = new byte[length];
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    int n = await stream.ReadAsync(buffer.AsMemory(0, length), stop.Token);
                    if (n <= 0) return;
                    lock (gate) if (target.Count < 48) target.Add(buffer[..n]);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        });
        await Task.Delay(700); // what the stream looks like before
        lock (gate) target = after;
        bool sent = HidD_SetOutputReport(write, X05Report.Command(), X05Report.ReportLength);
        string status = sent ? "sent once" : $"the pad refused the write (Windows error {Marshal.GetLastWin32Error()})";
        await Task.Delay(1500);
        stop.Cancel();
        try { await loop.WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { }
        lock (gate) return X05Report.Summarize(before, after, status);
    }
}
