using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32.SafeHandles;

namespace PickPack.Disk;

internal static class MacDisk
{
 [DllImport("romforge_native",EntryPoint="romforge_block_size",SetLastError=true)]static extern int BlockSize(int fd,ref uint value);
 [DllImport("romforge_native",EntryPoint="romforge_block_count",SetLastError=true)]static extern int BlockCount(int fd,ref ulong value);
 public static string PathFor(int number)=>$"/dev/rdisk{number}";
 public static List<DriveInfos> List()
 {
  if(Environment.GetEnvironmentVariable("ROMFORGE_TEST_MODE")=="1")return new();
  string text=Run("list","-plist","external","physical");
  var root=Dictionary(XDocument.Parse(text).Root!.Element("dict")!);
  var results=new List<DriveInfos>();
  if(!root.TryGetValue("AllDisksAndPartitions",out var disks))return results;
  foreach(var entry in disks.Elements("dict"))
  {
   var d=Dictionary(entry);string id=d["DeviceIdentifier"].Value;
   if(!id.StartsWith("disk")||!int.TryParse(id[4..],out int number))continue;
   var info=Dictionary(XDocument.Parse(Run("info","-plist",id)).Root!.Element("dict")!);
   if(!info.TryGetValue("Internal",out var internalFlag)||internalFlag.Name.LocalName!="false")continue;
   string model=info.GetValueOrDefault("MediaName")?.Value??id;
   long size=long.Parse(d["Size"].Value,System.Globalization.CultureInfo.InvariantCulture);
   results.Add(new DriveInfos(PathFor(number),model,size,number,info.GetValueOrDefault("MountPoint")?.Value));
  }
  return results;
 }
 static Dictionary<string,XElement> Dictionary(XElement element)
 {
  var nodes=element.Elements().ToArray();var values=new Dictionary<string,XElement>();
  for(int i=0;i+1<nodes.Length;i+=2)values[nodes[i].Value]=nodes[i+1];return values;
 }
 public static string Run(params string[] args)
 {
  var start=new ProcessStartInfo("/usr/sbin/diskutil"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in args)start.ArgumentList.Add(arg);
  using var process=Process.Start(start)??throw new IOException("diskutil 실행 실패");
  var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();
  if(process.ExitCode!=0)throw new IOException(error.GetAwaiter().GetResult());return output.GetAwaiter().GetResult();
 }
 public static int Sector(SafeFileHandle handle)
 {uint size=0;return BlockSize(handle.DangerousGetHandle().ToInt32(),ref size)==0?(int)size:512;}
 public static long Length(string path)
 {
  using var handle=File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
  ulong blocks=0;return BlockCount(handle.DangerousGetHandle().ToInt32(),ref blocks)==0?checked((long)blocks*Sector(handle)):RandomAccess.GetLength(handle);
 }
 public static void UnmountExternal(int number)
 {
  if(!List().Any(d=>d.DiskNumber==number))throw new IOException("외장 디스크만 이미지 쓰기를 지원합니다.");
  _=Run("unmountDisk","disk"+number);
 }
}
