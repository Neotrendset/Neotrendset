using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RomForge.Portable;
static class GuiChecks
{
 public static async Task<bool> Run(MacWindow window)
 {
  RomForge.Core.AppConfig.Instance.Common.IsMuted=true;
  string folder=Path.Combine(Common.PortablePaths.DataDirectory,"gui-fixtures");Directory.CreateDirectory(folder);
  string ncch=Path.Combine(folder,"inspector.ncch");byte[] header=new byte[1024];"NCCH"u8.CopyTo(header.AsSpan(0x100));System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x104),2);header[0x18f]=4;File.WriteAllBytes(ncch,header);
  var page=window.Pages.Last();window.Show(page);var inspector=(InspectorTool)page.Model;
  var input=window.CurrentForm!.GetVisualDescendants().OfType<TextBox>().First(x=>Equals(x.Watermark,"원본 파일"));input.Text=ncch;
  var inspect=window.CurrentForm.GetVisualDescendants().OfType<Button>().Single(x=>Equals(x.Content,"구조 검사"));
  await Until(()=>inspect.IsEnabled,"inspector button enabled after input");
  await Dispatcher.UIThread.InvokeAsync(()=>inspect.Command!.Execute(inspect.CommandParameter));
  await Until(()=>inspector.Result.Contains("NCCH")&&!inspector.IsLocked,"inspector button runs analysis");
  var export=window.CurrentForm.GetVisualDescendants().OfType<Button>().Single(x=>Equals(x.Content,"JSON 내보내기"));await Until(()=>export.IsEnabled,"JSON export becomes available");
  Console.WriteLine("PASS GUI input binding, Inspector execution and JSON export button state");
  var patchPage=window.Pages.First();window.Show(patchPage);
  var parent=window.ViewModel.PatchVM;var vm=parent.NormalVM;string original=Path.Combine(folder,"source.bin"),patch=Path.Combine(folder,"fix.ips");byte[] source=Enumerable.Range(0,128).Select(x=>(byte)x).ToArray();File.WriteAllBytes(original,source);File.WriteAllBytes(patch,"PATCH"u8.ToArray().Concat(new byte[]{0,0,2,0,1,99}).Concat("EOF"u8.ToArray()).ToArray());
  vm.SourcePath=original;vm.PatchSlots[0].FilePath=patch;vm.NamingEnabled=false;vm.AutoCompress=false;
  var run=window.CurrentForm!.GetVisualDescendants().OfType<Button>().Single(x=>ReferenceEquals(x.Command,parent.RunCommand));
  await Until(()=>run.IsEnabled,"patch run button enabled");await Dispatcher.UIThread.InvokeAsync(()=>run.Command!.Execute(run.CommandParameter));
  string output=Path.Combine(folder,"output","source.bin");await Until(()=>File.Exists(output)&&!parent.IsLocked,"patch command completed");source[2]=99;if(!File.ReadAllBytes(output).SequenceEqual(source))throw new Exception("GUI patch output bytes differ");
  Console.WriteLine("PASS GUI patch RunCommand uses upstream parent orchestration and writes expected bytes");
  return true;
 }
 static async Task Until(Func<bool> condition,string label)
 {
  var limit=DateTime.UtcNow.AddSeconds(20);
  while(!await Dispatcher.UIThread.InvokeAsync(condition)){if(DateTime.UtcNow>limit)throw new Exception("Timeout: "+label);await Task.Delay(20);}
 }
}
