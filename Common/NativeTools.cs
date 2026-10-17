using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace Common;

public static class NativeTools
{
 static bool initialized;
 public static string DirectoryPath=>Environment.GetEnvironmentVariable("ROMFORGE_NATIVE_DIR")??Path.Combine(AppContext.BaseDirectory,"native",OperatingSystem.IsMacOS()?"osx-arm64":"linux-x64");
 public static string Find(string name)
 {
  string path=Path.Combine(DirectoryPath,name);
  if(File.Exists(path))return path;
  foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
  {path=Path.Combine(dir,name);if(File.Exists(path))return path;}
  throw new FileNotFoundException($"네이티브 도구를 찾지 못했습니다: {name} ({DirectoryPath})");
 }
 public static void Initialize()
 {
  if(initialized)return;initialized=true;
  foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())Register(assembly);
  AppDomain.CurrentDomain.AssemblyLoad+=(_,e)=>Register(e.LoadedAssembly);
 }
 static void Register(Assembly assembly)
 {
  if(assembly.GetName().Name is not ("CHD.Core" or "Patch.Core" or "LibDeflate.NET" or "PickPack.Disk"))return;
  try {NativeLibrary.SetDllImportResolver(assembly,(name,_,_)=>
  {
   string? file=name=="romforge_native"?"libromforge.dylib":name.Contains("deflate",StringComparison.OrdinalIgnoreCase)?"libdeflate.dylib":name is "chdr.dll"?"libchdr.dylib":name is "xdelta3"?"libxdelta3.dylib":null;
   return file!=null&&NativeLibrary.TryLoad(Path.Combine(DirectoryPath,file),out var handle)?handle:IntPtr.Zero;
  });}catch(InvalidOperationException){}
 }
 public static async Task<(int Code,string Output)> RunAsync(string tool,IEnumerable<string> args,Action<string>? log=null,CancellationToken ct=default)
 {
  var start=new ProcessStartInfo(Find(tool)){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in args)start.ArgumentList.Add(arg);
  using var process=Process.Start(start)??throw new IOException("도구 실행 실패: "+tool);
  using var registration=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
  var output=new System.Text.StringBuilder();var gate=new object();
  async Task Pump(StreamReader reader)
  {
   char[] buffer=new char[1024];int count;
   while((count=await reader.ReadAsync(buffer))>0)
   {string part=new(buffer,0,count);lock(gate)output.Append(part);log?.Invoke(part);}
  }
  await Task.WhenAll(Pump(process.StandardOutput),Pump(process.StandardError),process.WaitForExitAsync());
  ct.ThrowIfCancellationRequested();return(process.ExitCode,output.ToString());
 }
}
