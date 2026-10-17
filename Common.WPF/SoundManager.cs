using System.Diagnostics;
namespace Common.WPF;
public static class SoundManager
{
 static Process? player;
 public static Task PlayFromWpfResourceAsync(string path)=>PlayFromFileAsync(File.Exists(path)?path:RomForge.Portable.Desktop.ResolveAsset(path));
 public static Task PlayFromFileAsync(string path)
 {
  Stop();if(!File.Exists(path))return Task.CompletedTask;
  var start=new ProcessStartInfo(OperatingSystem.IsMacOS()?"/usr/bin/afplay":"ffplay"){UseShellExecute=false};
  if(!OperatingSystem.IsMacOS()){start.ArgumentList.Add("-nodisp");start.ArgumentList.Add("-autoexit");}
  start.ArgumentList.Add(path);player=Process.Start(start);return Task.CompletedTask;
 }
 public static void Stop(){if(player is {} p){if(!p.HasExited)p.Kill();p.Dispose();player=null;}}
}
