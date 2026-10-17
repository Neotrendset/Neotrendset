using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Runtime.CompilerServices;
using ADispatcher = Avalonia.Threading.Dispatcher;

namespace RomForge.Portable
{
 public static class Desktop
 {
  public static Avalonia.Controls.Window? Owner { get; set; }
  public static Func<string,string,bool>? TestQuestion { get; set; }
  [ModuleInitializer] public static void Initialize()
  {
   if(!UriParser.IsKnownScheme("pack")) UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority),"pack",-1);
  }
  public static T Wait<T>(Task<T> task)
  {
   if (!ADispatcher.UIThread.CheckAccess())return task.GetAwaiter().GetResult();
   if(task.IsCompleted)return task.GetAwaiter().GetResult();
   var frame=new DispatcherFrame();
   _=task.ContinueWith(_=>ADispatcher.UIThread.Post(()=>frame.Continue=false));
   ADispatcher.UIThread.PushFrame(frame);
   return task.GetAwaiter().GetResult();
  }
  public static IReadOnlyList<FilePickerFileType> Filters(string filter)
  {
   var parts=filter.Split('|');var result=new List<FilePickerFileType>();
   for(int i=0;i+1<parts.Length;i+=2)result.Add(new FilePickerFileType(parts[i]){Patterns=parts[i+1].Split(';').Select(p=>p=="*.*"?"*":p).ToArray()});
   return result;
  }
  public static async Task<IStorageFolder?> Folder(IStorageProvider storage,string path)=>Directory.Exists(path)?await storage.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(path))):null;
  public static string ResolveAsset(string uri)
  {
   string path=uri.Replace("pack://application:,,,/","").TrimStart('/');
   string assembly="RomForge";
   if(path.Contains(";component/")){var parts=path.Split(";component/");assembly=parts[0];path=parts[1];}
   string candidate=Path.Combine(AppContext.BaseDirectory,"compat-assets",assembly,path);
   return File.Exists(candidate)?candidate:Path.Combine(AppContext.BaseDirectory,path);
  }
  public static async Task<System.Windows.MessageBoxResult> AskAsync(string text,string title,System.Windows.MessageBoxButton buttons)
  {
   if(TestQuestion is {} test)return test(text,title)?System.Windows.MessageBoxResult.Yes:System.Windows.MessageBoxResult.No;
   var win=new Avalonia.Controls.Window{Title=title,Width=520,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};
   var panel=new StackPanel{Margin=new Avalonia.Thickness(24),Spacing=16};
   panel.Children.Add(new TextBlock{Text=text,TextWrapping=Avalonia.Media.TextWrapping.Wrap,MaxHeight=500});
   var actions=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=8};
   var choices=buttons switch
   {System.Windows.MessageBoxButton.YesNo=>new[]{System.Windows.MessageBoxResult.Yes,System.Windows.MessageBoxResult.No},System.Windows.MessageBoxButton.YesNoCancel=>new[]{System.Windows.MessageBoxResult.Yes,System.Windows.MessageBoxResult.No,System.Windows.MessageBoxResult.Cancel},System.Windows.MessageBoxButton.OKCancel=>new[]{System.Windows.MessageBoxResult.OK,System.Windows.MessageBoxResult.Cancel},_=>new[]{System.Windows.MessageBoxResult.OK}};
   foreach(var choice in choices){var b=new Button{Content=choice.ToString()};b.Click+=(_,_)=>win.Close(choice);actions.Children.Add(b);}
   panel.Children.Add(actions);win.Content=panel;
   if(Owner is null)throw new InvalidOperationException("No desktop window is available for this dialog.");
   return await win.ShowDialog<System.Windows.MessageBoxResult>(Owner);
  }
 }
}
namespace System.Windows
{
 public enum Visibility{Visible,Hidden,Collapsed}
 public enum MessageBoxButton{OK,OKCancel,YesNo,YesNoCancel}
 public enum MessageBoxImage{None,Information,Warning,Error,Question}
 public enum MessageBoxResult{None,OK,Cancel,Yes,No}
 public class Window : Avalonia.Controls.Window { }
 public class Application
 {
  public static Application Current { get; }=new();
  public Threading.Dispatcher Dispatcher=>Threading.Dispatcher.CurrentDispatcher;
  public Avalonia.Controls.Window? MainWindow=>RomForge.Portable.Desktop.Owner;
  public IEnumerable<Avalonia.Controls.Window> Windows=>MainWindow is {} w?new[]{w}:Array.Empty<Avalonia.Controls.Window>();
  public void Shutdown(){if(Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)d.Shutdown();}
 }
 public static class MessageBox
 {
  public static MessageBoxResult Show(string message,string title="RomForge",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None)=>RomForge.Portable.Desktop.Wait(RomForge.Portable.Desktop.AskAsync(message,title,buttons));
 }
 public readonly record struct Int32Rect(int X,int Y,int Width,int Height);
 public interface IDataObject{object? GetData(string format); bool GetDataPresent(string format);}
 public static class DataFormats{public const string FileDrop="Files";}
 public static class Clipboard
 {
  public static void SetText(string value){var t=RomForge.Portable.Desktop.Owner?.Clipboard?.SetTextAsync(value);if(t!=null)RomForge.Portable.Desktop.Wait(t.ContinueWith(_=>true));}
  public static string GetText()=>RomForge.Portable.Desktop.Owner?.Clipboard is {} c?RomForge.Portable.Desktop.Wait(c.GetTextAsync())??"":"";
  public static bool ContainsText()=>!string.IsNullOrEmpty(GetText());
 }
}
namespace System.Windows.Input
{
 public static class CommandManager
 {
  public static event EventHandler? RequerySuggested;
  public static void InvalidateRequerySuggested()=>ADispatcher.UIThread.Post(()=>RequerySuggested?.Invoke(null,EventArgs.Empty));
 }
}
namespace System.Windows.Threading
{
 public enum DispatcherPriority{Background,Normal,Loaded,Render,Send,DataBind}
 public sealed class Dispatcher
 {
  public static Dispatcher CurrentDispatcher{get;}=new();
  public bool CheckAccess()=>ADispatcher.UIThread.CheckAccess();
  public void Invoke(Action action)=>ADispatcher.UIThread.Invoke(action);
  public T Invoke<T>(Func<T> action)=>ADispatcher.UIThread.Invoke(action);
  public Task InvokeAsync(Action action,DispatcherPriority priority=DispatcherPriority.Normal)=>ADispatcher.UIThread.InvokeAsync(action).GetTask();
  public Task InvokeAsync(Func<Task> action,DispatcherPriority priority=DispatcherPriority.Normal)=>ADispatcher.UIThread.InvokeAsync(action);
  public void BeginInvoke(Action action)=>ADispatcher.UIThread.Post(action);
  public void BeginInvoke(DispatcherPriority priority,Action action)=>ADispatcher.UIThread.Post(action);
 }
 public class DispatcherTimer
 {
  readonly Avalonia.Threading.DispatcherTimer timer=new();
  public TimeSpan Interval{get=>timer.Interval;set=>timer.Interval=value;}
  public event EventHandler? Tick {add=>timer.Tick+=value;remove=>timer.Tick-=value;}
  public void Start()=>timer.Start();public void Stop()=>timer.Stop();
 }
}
namespace System.Windows.Shell{public enum TaskbarItemProgressState{None,Indeterminate,Normal,Error,Paused}}
namespace Microsoft.Win32
{
 public class OpenFileDialog
 {
  public string Title{get;set;}="파일 선택";public string Filter{get;set;}="";public bool Multiselect{get;set;}
  public string FileName{get;set;}="";public string[] FileNames{get;set;}=Array.Empty<string>();
  public string InitialDirectory{get;set;}="";public bool CheckFileExists{get;set;}=true;
  public bool? ShowDialog()=>RomForge.Portable.Desktop.Wait(Show());
  public bool? ShowDialog(Avalonia.Controls.Window? owner)=>ShowDialog();
  async Task<bool?> Show()
  {
   var storage=RomForge.Portable.Desktop.Owner?.StorageProvider??throw new InvalidOperationException("No desktop window.");
   var files=await storage.OpenFilePickerAsync(new FilePickerOpenOptions{Title=Title,AllowMultiple=Multiselect,FileTypeFilter=RomForge.Portable.Desktop.Filters(Filter),SuggestedStartLocation=await RomForge.Portable.Desktop.Folder(storage,InitialDirectory)});
   FileNames=files.Select(x=>x.TryGetLocalPath()).OfType<string>().ToArray();FileName=FileNames.FirstOrDefault()??"";
   return FileNames.Length>0;
  }
 }
 public class OpenFolderDialog
 {
  public string Title{get;set;}="폴더 선택";public string FolderName{get;set;}="";public bool Multiselect{get;set;}
  public bool? ShowDialog()=>RomForge.Portable.Desktop.Wait(Show());
  async Task<bool?> Show(){var files=await RomForge.Portable.Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{Title=Title,AllowMultiple=Multiselect});FolderName=files.FirstOrDefault()?.TryGetLocalPath()??"";return FolderName.Length>0;}
 }
 public class SaveFileDialog
 {
  public string Title{get;set;}="파일 저장";public string Filter{get;set;}="";public string FileName{get;set;}="";public string DefaultExt{get;set;}="";public bool AddExtension{get;set;}=true;public string InitialDirectory{get;set;}="";
  public bool? ShowDialog()=>RomForge.Portable.Desktop.Wait(Show());
  async Task<bool?> Show(){var file=await RomForge.Portable.Desktop.Owner!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title=Title,SuggestedFileName=Path.GetFileName(FileName),DefaultExtension=DefaultExt.TrimStart('.'),FileTypeChoices=RomForge.Portable.Desktop.Filters(Filter),SuggestedStartLocation=await RomForge.Portable.Desktop.Folder(RomForge.Portable.Desktop.Owner!.StorageProvider,InitialDirectory)});FileName=file?.TryGetLocalPath()??"";return FileName.Length>0;}
 }
}
namespace Ookii.Dialogs.Wpf
{
 public class VistaFolderBrowserDialog
 {
  public string Description{get;set;}="폴더 선택";public bool UseDescriptionForTitle{get;set;}=true;public string SelectedPath{get;set;}="";
  public bool? ShowDialog(){var d=new Microsoft.Win32.OpenFolderDialog{Title=Description};var r=d.ShowDialog();SelectedPath=d.FolderName;return r;}
 }
}
