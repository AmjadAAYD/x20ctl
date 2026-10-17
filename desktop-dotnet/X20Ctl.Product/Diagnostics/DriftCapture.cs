using System.Diagnostics;
using System.IO;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.Diagnostics;
namespace X20Ctl.Product.Diagnostics;

/// <summary>Shared read-only XInput centre sampling. Results are observations, not hardware calibration.</summary>
public static class DriftCapture
{
 public static async Task<(DriftObservation Left,DriftObservation Right)?> Measure(int slot,IProgress<double>? progress,CancellationToken cancel)
 {
  return await Task.Run(()=>{
   using var reader=new WindowsReader(WindowsReader.Libraries[0]);
   if(slot is <0 or >3 || PcModeService.IsVirtual(slot)||!reader.Read(slot,out _))return ((DriftObservation,DriftObservation)?)null;
   var left=new DriftObservation();var right=new DriftObservation();var watch=Stopwatch.StartNew();long next=0;
   while(watch.ElapsedMilliseconds<5000){cancel.ThrowIfCancellationRequested();if(!reader.Read(slot,out var state))throw new IOException("The input source disconnected during sampling.");left.Add(state.Pad.LX,state.Pad.LY);right.Add(state.Pad.RX,state.Pad.RY);if(watch.ElapsedMilliseconds>=next){progress?.Report(watch.ElapsedMilliseconds/5000d);next+=100;}Thread.Sleep(2);}
   progress?.Report(1);return (left,right);
  },cancel);
 }
}
