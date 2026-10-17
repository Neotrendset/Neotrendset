using Common.WPF.ViewModels;
using RomForge.Core.UI.Command;
using RomForge.Mac.Core;
using System.Windows.Input;
using Avalonia.Platform.Storage;
namespace RomForge.Portable;
public sealed class InspectorTool:ToolTabViewModel
{
 string source="",result="";ContainerInspection? inspection;CancellationTokenSource? cancellation;
 public string SourcePath{get=>source;set{SetProperty(ref source,value);inspection=null;Result="";System.Windows.Input.CommandManager.InvalidateRequerySuggested();}}
 public bool IncludeHash{get;set;}=true;
 public string Result{get=>result;private set=>SetProperty(ref result,value);}
 public ICommand InspectCommand{get;}
 public ICommand ExportCommand{get;}
 public InspectorTool()
 {
  InspectCommand=new RelayCommand(async _=>await Inspect(),_=>!IsLocked&&!string.IsNullOrWhiteSpace(SourcePath));
  ExportCommand=new RelayCommand(async _=>await Export(),_=>inspection!=null&&!IsLocked);
  CancelCommand=new RelayCommand(_=>cancellation?.Cancel());
 }
 async Task Inspect()
 {
  using(BeginWork())
  {
   cancellation=new();string path=SourcePath;
   try{var data=await new ContainerInspectorService().InspectAsync(path,IncludeHash,cancellation.Token);if(path!=SourcePath)return;inspection=data;Result=ContainerInspectionFormatter.Format(data);}
   catch(Exception ex){Result=ex is OperationCanceledException?"취소됨":ex.Message;}
   finally{cancellation.Dispose();cancellation=null;System.Windows.Input.CommandManager.InvalidateRequerySuggested();}
  }
 }
 async Task Export()
 {
  var data=inspection;if(data==null)return;
  try
  {
   var file=await Desktop.Owner!.StorageProvider.SaveFilePickerAsync(new(){Title="분석 JSON 저장 (새 파일)",SuggestedFileName=Path.GetFileName(data.FilePath)+".inspection.json"});
   var path=file?.TryGetLocalPath();if(path!=null)await InspectionJsonExporter.ExportAsync(data,path);
  }
  catch(Exception ex){Result+="\n"+ex.Message;}
 }
}
