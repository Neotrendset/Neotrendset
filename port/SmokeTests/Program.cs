using Avalonia;
using Avalonia.Headless;
using RomForge.Portable;
Environment.SetEnvironmentVariable("ROMFORGE_TEST_MODE","1");
Environment.SetEnvironmentVariable("ROMFORGE_DATA_DIR",Path.Combine(Path.GetTempPath(),"romforge-parity-tests-"+Guid.NewGuid().ToString("N")));
Common.NativeTools.Initialize();
AppBuilder.Configure<MacApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var window=new MacWindow();
Console.WriteLine("PASS native MacWindow + upstream MainViewModel initialization");

Console.WriteLine($"Workflow pages: {window.Pages.Count}");
foreach(var page in window.Pages)
{
 window.Show(page);
 Console.WriteLine($"PASS page {page.Title}: {window.CurrentForm!.ActionNames.Count} actions");
}
Console.WriteLine("PASS all workflow forms materialized");

RomForge.Portable.Desktop.Wait(GuiChecks.Run(window));
RomForge.Portable.Desktop.Wait(Task.Run(()=>{EngineChecks.Run();return true;}));

window.ViewModel.UtilMainVM.ZipImageToolVM.Dispose();
Common.WPF.SoundManager.Stop();
try{Directory.Delete(Common.PortablePaths.DataDirectory,true);}catch(IOException){}
