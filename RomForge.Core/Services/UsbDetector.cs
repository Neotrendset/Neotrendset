namespace RomForge.Core.Services;
// Drive list changes are polled portably instead of handling a Win32 device message.
public sealed class UsbDetector:IDisposable
{
 System.Threading.Timer? timer;string snapshot="";
 public event Action? DeviceChanged;
 public void Register(Avalonia.Controls.Window window)
 {
  snapshot=Snapshot();timer=new(_=>{var next=Snapshot();if(next==snapshot)return;snapshot=next;Avalonia.Threading.Dispatcher.UIThread.Post(()=>DeviceChanged?.Invoke());},null,1000,1000);
  window.Closed+=(_,_)=>Dispose();
 }
 static string Snapshot()=>string.Join("|",DriveInfo.GetDrives().Select(x=>x.Name).OrderBy(x=>x));
 public void Dispose()=>timer?.Dispose();
}
