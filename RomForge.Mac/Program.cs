using Avalonia;
namespace RomForge.Portable;
internal static class Program
{
 [STAThread]public static void Main(string[] args){Directory.CreateDirectory(Common.PortablePaths.DataDirectory);Environment.CurrentDirectory=Common.PortablePaths.DataDirectory;Common.NativeTools.Initialize();AppBuilder.Configure<MacApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);}
}
public sealed class MacApp:Avalonia.Application
{
 public override void Initialize()
 {
  Name="RomForge-Mac";
  Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
  Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://RomForge-Mac/")){Source=new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")});
 }
 public override void OnFrameworkInitializationCompleted()
 {
  if(ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
   desktop.MainWindow=new MacWindow();
  base.OnFrameworkInitializationCompleted();
 }
}
