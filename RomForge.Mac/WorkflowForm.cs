using Avalonia.Input;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Common.WPF.ViewModels;

namespace RomForge.Portable;

public sealed class WorkflowForm : StackPanel
{
 readonly object model;
 readonly RomForge.ViewModels.Patch.PatchMainViewModel? patchParent;
 readonly Action<Exception> error;
 readonly List<object> selected=new();
 readonly List<Action> updateCommands=new();
 readonly HashSet<object> rendered=new(ReferenceEqualityComparer.Instance);
 public IReadOnlyList<string> ActionNames=>actions;
 readonly List<string> actions=new();
 static readonly HashSet<string> MethodNames=new(StringComparer.Ordinal)
 {"StartAsync","RunAsync","ConvertAsync","MergeAsync","SplitAsync","ExecuteStartWorkAsync","Cancel","CancelExtract","Clear","ClearItems","RemoveAllFiles","AddPatchSlot","ReloadMetadataAsync","LoadTitlesAsync","RecoverTitleDbAsync","ExtractTitleAsync","InstallAsync","RefreshKeysStatus","ResetAll","ResetBaseImages","ResetBootLogo","UpdateFromGameIdAsync","SetPathAsync","RemoveItems","RemoveFile","Remove","RemoveRow","SetFile","ClearFile","ChangeSaveDirectory","Reset","ManualMatch","ClearPatch","AssignPatchAsync","BulkPatchFolderAsync","BulkPatchArchiveAsync","RefreshDrives"};
 static readonly HashSet<string> Hidden=new(StringComparer.Ordinal)
 {"Tools","SelectedTool","SelectedViewModel","SubTabIndex","SelectedTabIndex","ActiveLogEntries","LogEntries","Context","Metadata","IsLocked","IsUnlocked","IsIdle","ProgressPct","ProgressLabel","ProgressPercent","ProgressTime","ProgressSpeed","ProgressText","StatusMessage","IsLoading","IsConverting","IsDownloading","IsPlaying","Progress","TaskbarProgressValue","TaskbarProgressState","LogBoxHeight"};
 public WorkflowForm(object model,Action<Exception> error,RomForge.ViewModels.Patch.PatchMainViewModel? patchParent=null)
 {
  this.model=model;this.error=error;this.patchParent=patchParent;Spacing=12;
  Render(model,this,0);
 }
 void Render(object target,Panel panel,int depth)
 {
  if(depth>4||!rendered.Add(target))return;
  var properties=target.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.GetIndexParameters().Length==0&&p.GetMethod!=null).ToArray();
  var commands=new WrapPanel{Orientation=Orientation.Horizontal};
  if(ReferenceEquals(target,model)&&patchParent!=null)
   foreach(var (name,command) in new (string,ICommand?)[]{("RunCommand",patchParent.RunCommand),("CancelCommand",patchParent.CancelCommand),("ClearCommand",patchParent.ClearCommand),("CalculateHashCommand",patchParent.CalculateHashCommand)})
    if(command!=null){commands.Children.Add(new Button{Content=Labels.For(name),Command=command,Margin=new Thickness(0,0,8,8)});actions.Add(name);}
  foreach(var property in properties.Where(p=>typeof(ICommand).IsAssignableFrom(p.PropertyType)))
  {
   if(Get(property,target) is not ICommand command)continue;
   var button=new Button{Content=Labels.For(property.Name),Margin=new Thickness(0,0,8,8),Command=command};
   void Update()=>button.CommandParameter=selected.Count==1?selected[0]:selected.ToList();
   button.PointerEntered+=(_,_)=>Update();updateCommands.Add(Update);Update();commands.Children.Add(button);actions.Add(property.Name);
  }
  foreach(var method in target.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance).Where(m=>MethodNames.Contains(m.Name)))
  {
   if(patchParent!=null&&target is RomForge.Core.Services.Patch.IPatchViewModel&&method.Name is "RunAsync" or "Cancel" or "Clear")continue;
   if(method.Name=="RunAsync"&&properties.Any(p=>p.Name=="RunCommand"))continue;
   if(target is SwitchWorkspace sw)
   {
    if(method.Name=="StartAsync"&&sw.Engine is not RomForge.ViewModels.Switch.RepackMainViewModel)continue;
    if(method.Name is "MergeAsync" or "SplitAsync"&&sw.Engine is not RomForge.ViewModels.Switch.MergeMainViewModel)continue;
    if(method.Name=="ExecuteStartWorkAsync"&&sw.Engine is not RomForge.ViewModels.Switch.KeygenMainViewModel)continue;
   }
   if(target is RomForge.ViewModels.Switch.RepackMainViewModel or RomForge.ViewModels.Switch.MergeMainViewModel or RomForge.ViewModels.Switch.KeygenMainViewModel)continue;
   var enumParameter=method.GetParameters().FirstOrDefault(p=>p.ParameterType.IsEnum);
   if(enumParameter!=null)
    foreach(var choice in Enum.GetValues(enumParameter.ParameterType))AddAction(Labels.For(choice.ToString()!),method,choice);
   else if(method.Name=="ExtractTitleAsync")
   {AddAction("선택 게임 → CIA",method,false);AddAction("선택 게임 → CCI",method,true);}
   else AddAction(Labels.For(method.Name),method,null);
  }
  var add=target.GetType().GetMethods().FirstOrDefault(m=>m.Name is "AddPaths" or "AddPathsAsync" or "AddFiles" or "AddFilesAsync"&&m.GetParameters().Length==1&&typeof(IEnumerable<string>).IsAssignableFrom(m.GetParameters()[0].ParameterType));
  if(add!=null)
  {
   var files=MakeButton("파일 추가…",async()=>
   {
    var chosen=await Desktop.Owner!.StorageProvider.OpenFilePickerAsync(new(){Title="입력 파일 추가",AllowMultiple=true});
    await InvokeResult(add.Invoke(target,new object?[]{Common.Utils.ExpandPaths(chosen.Select(x=>x.TryGetLocalPath()).OfType<string>()).ToArray()}));
   });commands.Children.Insert(0,files);
   var folder=MakeButton("폴더 추가…",async()=>
   {
    var chosen=await Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new(){Title="입력 폴더 추가",AllowMultiple=true});
    await InvokeResult(add.Invoke(target,new object?[]{Common.Utils.ExpandPaths(chosen.Select(x=>x.TryGetLocalPath()).OfType<string>()).ToArray()}));
   });commands.Children.Insert(1,folder);actions.Add(add.Name);
   DragDrop.SetAllowDrop(panel,true);
   panel.AddHandler(DragDrop.DropEvent,async(_,e)=>
   {
    var paths=e.Data.GetFiles()?.Select(f=>f.TryGetLocalPath()).OfType<string>().ToArray();
    if(paths is not {Length:>0})return;
    try{await InvokeResult(add.Invoke(target,new object?[]{Common.Utils.ExpandPaths(paths).ToArray()}));}catch(Exception ex){error(Unwrap(ex));}e.Handled=true;
   });
  }
  var addOne=target.GetType().GetMethods().FirstOrDefault(m=>m.Name is "AddDroppedItemAsync" or "AddSourceFile"&&m.GetParameters().Length==1);
  if(addOne!=null)
  {
   commands.Children.Insert(0,MakeButton("파일 추가…",async()=>{var chosen=await Desktop.Owner!.StorageProvider.OpenFilePickerAsync(new(){Title="입력 파일 추가",AllowMultiple=true});foreach(var file in chosen){var path=file.TryGetLocalPath();if(path!=null)await InvokeResult(addOne.Invoke(target,new object[]{path}));}}));
   commands.Children.Insert(1,MakeButton("폴더 추가…",async()=>{var chosen=await Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new(){Title="입력 폴더 추가",AllowMultiple=true});foreach(var file in chosen){var path=file.TryGetLocalPath();if(path!=null)await InvokeResult(addOne.Invoke(target,new object[]{path}));}}));actions.Add(addOne.Name);
  }
  if(commands.Children.Count>0)panel.Children.Add(commands);
  var fields=new StackPanel{Spacing=9};
  foreach(var property in properties)
  {
   if(Hidden.Contains(property.Name)||typeof(ICommand).IsAssignableFrom(property.PropertyType))continue;
   var type=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
   bool writable=IsWritable(property);
   if((type==typeof(string)||type==typeof(bool)||type.IsEnum||type.IsPrimitive||type==typeof(decimal))&&writable)
   {
    if(property.Name.StartsWith("Progress")||property.Name.EndsWith("HintVisibility"))continue;
    fields.Children.Add(Field(target,property));
   }
   else if(writable&&!type.IsPrimitive&&type!=typeof(byte[]))
   {
    var source=properties.FirstOrDefault(p=>ElementType(p.PropertyType)==type);
    if(source!=null&&Get(source,target) is IEnumerable options)
    {
     var combo=new ComboBox{ItemsSource=options,HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=new FuncDataTemplate<object>((x,_)=>new TextBlock{Text=Labels.Display(x)})};
     combo.Bind(ComboBox.SelectedItemProperty,new Binding(property.Name){Source=target,Mode=BindingMode.TwoWay});fields.Children.Add(new TextBlock{Text=Labels.For(property.Name)});fields.Children.Add(combo);
    }
   }
   else if(type==typeof(byte[])&&writable)
   {
    var image=MakeButton(Labels.For(property.Name)+" 선택…",async()=>
    {
     string? path=await PickFile();if(path==null)return;
     var bytes=await File.ReadAllBytesAsync(path);
     if(property.Name is "LogoData" or "Logo"){using var raster=Raster.Load(bytes);raster.Resize(256,256);using var stream=new MemoryStream();raster.SaveJpeg(stream);bytes=stream.ToArray();}
     property.SetValue(target,bytes);
    });fields.Children.Add(image);
   }
   else if(!writable&&type==typeof(string)&&property.Name=="Result")
   {var output=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=280,MaxHeight=700};output.Bind(TextBox.TextProperty,new Binding(property.Name){Source=target});fields.Children.Add(output);}
   else if(!writable&&type==typeof(string)&&property.Name is "Status" or "KindText" or "WarningText" or "StatusText" or "FileCountText" or "SourceSha256" or "SourceCrc32")
   {var text=new TextBlock{TextWrapping=TextWrapping.Wrap};text.Bind(TextBlock.TextProperty,new Binding(property.Name){Source=target});fields.Children.Add(text);}
  }
  if(fields.Children.Count>0)panel.Children.Add(fields);
  // Public metadata image setters were invoked by WPF drag-and-drop code-behind.
  foreach(var method in target.GetType().GetMethods().Where(m=>m.Name is "SetIcon0FromBytes" or "SetPic0FromBytes" or "SetPic1FromBytes" or "SetBootLogoFromBytes" or "SetCover"))
  {
   string label=method.Name switch{"SetIcon0FromBytes"=>"ICON0 이미지", "SetPic0FromBytes"=>"PIC0 이미지", "SetPic1FromBytes"=>"PIC1 이미지","SetCover"=>"표지 이미지",_=>"부팅 로고 이미지"};
   panel.Children.Add(MakeButton(label+" 선택…",async()=>{string? path=await PickFile();if(path!=null)method.Invoke(target,new object[]{await File.ReadAllBytesAsync(path)});}));actions.Add(method.Name);
  }
  foreach(var property in properties)
  {
   if(Hidden.Contains(property.Name))continue;
   object? value=Get(property,target);if(value==null||value is string||value is byte[])continue;
   if(value is IEnumerable items && property.Name is not ("SupportedExtensions" or "Drives" or "AvailableDrives" or "Indices" or "ProgramIdOffsets")&&!property.Name.EndsWith("Options")&&!property.Name.EndsWith("Formats"))
   {
    Type? element=ElementType(property.PropertyType);
    if(element==null||element.IsPrimitive||element.IsEnum||element==typeof(string))continue;
    panel.Children.Add(Collection(target,property,items,element));
   }
   else if(value is ViewModelBase&&property.Name is not ("Primary" or "SelectedItem"))
   {
    var content=new StackPanel{Spacing=10};Render(value,content,depth+1);
    if(content.Children.Count>0)panel.Children.Add(new Expander{Header=Labels.For(property.Name),Content=content,IsExpanded=true,HorizontalAlignment=HorizontalAlignment.Stretch});
   }
  }
  void AddAction(string label,MethodInfo method,object? option)
  {commands.Children.Add(MakeButton(label,()=>Invoke(target,method,option)));actions.Add(method.Name);}
 }
 Control Field(object target,PropertyInfo property)
 {
  var type=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
  if(type==typeof(bool))
  {
   var check=new CheckBox{Content=Labels.For(property.Name)};check.Bind(CheckBox.IsCheckedProperty,new Binding(property.Name){Source=target,Mode=BindingMode.TwoWay});return check;
  }
  var row=new Grid{ColumnDefinitions=new ColumnDefinitions("180,*")};
  row.Children.Add(new TextBlock{Text=Labels.For(property.Name),VerticalAlignment=VerticalAlignment.Center});
  Control editor;
  if(type.IsEnum)
  {
   var combo=new ComboBox{ItemsSource=Enum.GetValues(type),HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=new FuncDataTemplate<object>((x,_)=>new TextBlock{Text=Labels.Display(x)})};
   combo.Bind(ComboBox.SelectedItemProperty,new Binding(property.Name){Source=target,Mode=BindingMode.TwoWay});editor=combo;
  }
  else
  {
   var text=new TextBox{Watermark=Labels.For(property.Name),HorizontalAlignment=HorizontalAlignment.Stretch};
   if(property.Name.Contains("Password",StringComparison.OrdinalIgnoreCase))text.PasswordChar='●';
   text.Bind(TextBox.TextProperty,new Binding(property.Name){Source=target,Mode=BindingMode.TwoWay});
   if(type==typeof(string)&&(property.Name.Contains("Path")||property.Name.Contains("Directory")))
   {
    DragDrop.SetAllowDrop(text,true);text.AddHandler(DragDrop.DropEvent,(_,e)=>{var dropped=e.Data.GetFiles()?.FirstOrDefault()?.TryGetLocalPath();if(dropped!=null){property.SetValue(target,dropped);e.Handled=true;}});
    var paths=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto")};paths.Children.Add(text);
    var file=MakeButton("파일…",async()=>{string? path=await PickFile();if(path!=null)property.SetValue(target,path);});Grid.SetColumn(file,1);paths.Children.Add(file);
    var folder=MakeButton("폴더…",async()=>{var chosen=await Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new(){Title=Labels.For(property.Name)});var path=chosen.FirstOrDefault()?.TryGetLocalPath();if(path!=null)property.SetValue(target,path);});Grid.SetColumn(folder,2);paths.Children.Add(folder);editor=paths;
   }
   else editor=text;
  }
  Grid.SetColumn(editor,1);row.Children.Add(editor);return row;
 }
 Control Collection(object target,PropertyInfo property,IEnumerable items,Type element)
 {
  var content=new StackPanel{Spacing=8};
  var grid=new DataGrid{ItemsSource=items,AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Extended,CanUserResizeColumns=true,MinHeight=120,MaxHeight=300,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal};
  var columnProperties=element.GetProperties().Where(p=>p.GetIndexParameters().Length==0&&p.GetMethod!=null&&(p.PropertyType==typeof(string)||p.PropertyType==typeof(bool)||p.PropertyType.IsEnum||p.Name is "No" or "Progress" or "Version")).Take(20);
  foreach(var p in columnProperties)
  {
   bool editable=IsWritable(p);
   var binding=new Binding(p.Name){Mode=editable?BindingMode.TwoWay:BindingMode.OneWay};
   DataGridColumn column=p.PropertyType==typeof(bool)?new DataGridCheckBoxColumn{Binding=binding}:new DataGridTextColumn{Binding=binding};
   column.Header=Labels.For(p.Name);column.IsReadOnly=!editable;grid.Columns.Add(column);
  }
  var detail=new ContentControl();
  grid.SelectionChanged+=(_,_)=>
  {
   selected.Clear();if(grid.SelectedItems!=null)selected.AddRange(grid.SelectedItems.Cast<object>());
   foreach(var update in updateCommands)update();
   if(grid.SelectedItem is {} item)
   {
    var form=new StackPanel{Spacing=8};
    rendered.Remove(item);Render(item,form,1);
    detail.Content=new Expander{Header="선택 항목 편집",Content=form,IsExpanded=false};
   }
   foreach(var p in target.GetType().GetProperties().Where(p=>p.Name.StartsWith("Selected")&&p.SetMethod?.IsPublic==true&&p.PropertyType.IsInstanceOfType(grid.SelectedItem)))p.SetValue(target,grid.SelectedItem);
  };
  content.Children.Add(grid);content.Children.Add(detail);
  return new Expander{Header=Labels.For(property.Name),Content=content,IsExpanded=true,HorizontalAlignment=HorizontalAlignment.Stretch};
 }
 async Task Invoke(object target,MethodInfo method,object? option)
 {
  if(method.Name=="ManualMatch"&&target is RomForge.ViewModels.Patch.ArcadePatchMainViewModel arcade)
  {
   var item=selected.OfType<RomForge.Core.Models.Patch.ArcadeMatchItem>().FirstOrDefault()??throw new InvalidOperationException("원본 목록에서 항목을 선택하세요.");
   string? file=await PickFile();if(file==null)return;
   arcade.ManualMatch(item,new RomForge.Core.Models.Patch.PatchEntry{DisplayName=Path.GetFileName(file),EntryPath=file});return;
  }
  var values=new List<object?>();
  foreach(var p in method.GetParameters())
  {
   Type t=p.ParameterType;
   if(t==typeof(CancellationToken)){values.Add(CancellationToken.None);continue;}
   if(t.IsEnum){values.Add(option);continue;}
   if(t==typeof(bool)&&option is bool flag){values.Add(flag);continue;}
   if(t==typeof(string))
   {
    if(target is RomForge.ViewModels.Patch.SourceHashViewModel&&method.Name=="StartAsync")
    {var source=model.GetType().GetProperty("SourcePath")?.GetValue(model) as string;values.Add(source);continue;}
    if(p.Name?.Contains("output",StringComparison.OrdinalIgnoreCase)==true)
    {var file=await Desktop.Owner!.StorageProvider.SaveFilePickerAsync(new(){Title="출력 파일 저장",SuggestedFileName=option is true?"title.cci":"title.cia"});var outputPath=file?.TryGetLocalPath();if(outputPath==null)return;values.Add(outputPath);continue;}
    if(method.Name=="ChangeSaveDirectory")
    {var folders=await Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new(){Title="저장 폴더 선택"});string? directory=folders.FirstOrDefault()?.TryGetLocalPath();if(directory==null)return;values.Add(directory);continue;}
    if(method.Name=="UpdateFromGameIdAsync")
    {string? gameId=(model as RomForge.ViewModels.PS.PackingMainViewModel)?.Files.Primary?.GameId;values.Add(gameId);continue;}
    string? path=await PickFile();if(path==null)return;values.Add(path);continue;
   }
   if(t.IsGenericType&&typeof(IEnumerable).IsAssignableFrom(t))
   {
    Type itemType=t.GetGenericArguments()[0];var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
    foreach(var item in selected.Where(itemType.IsInstanceOfType))list.Add(item);
    if(list.Count==0)foreach(var item in FindItems(model,itemType,new HashSet<object>(ReferenceEqualityComparer.Instance),0))list.Add(item);
    values.Add(list);continue;
   }
   var selection=selected.FirstOrDefault(t.IsInstanceOfType);
   if(selection!=null){values.Add(selection);continue;}
   if(p.HasDefaultValue){values.Add(p.DefaultValue);continue;}
   throw new InvalidOperationException("먼저 처리할 항목을 목록에서 선택하세요.");
  }
  await InvokeResult(method.Invoke(target,values.ToArray()));
 }
 static bool IsWritable(PropertyInfo p)=>p.SetMethod?.IsPublic==true&&!p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
 internal static IEnumerable<object> FindItems(object target,Type type,HashSet<object> seen,int depth)
 {
  if(depth>3||!seen.Add(target))yield break;
  foreach(var p in target.GetType().GetProperties().Where(p=>p.GetIndexParameters().Length==0&&p.GetMethod!=null&&!Hidden.Contains(p.Name)))
  {
   object? value=Get(p,target);
   if(value is IEnumerable items&&value is not string)
    foreach(var item in items)if(item!=null&&type.IsInstanceOfType(item))yield return item;
   if(value is ViewModelBase)foreach(var item in FindItems(value,type,seen,depth+1))yield return item;
  }
 }
 Button MakeButton(string text,Func<Task> action)
 {
  var button=new Button{Content=text,Margin=new Thickness(0,0,8,8)};
  button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){error(Unwrap(ex));}finally{button.IsEnabled=true;}};return button;
 }
 static object? Get(PropertyInfo p,object target){try{return p.GetValue(target);}catch{return null;}}
 static Type? ElementType(Type t)=>t.IsArray?t.GetElementType():t.GetInterfaces().Append(t).FirstOrDefault(x=>x.IsGenericType&&x.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GetGenericArguments()[0];
 static async Task InvokeResult(object? result){if(result is Task task)await task;}
 static Exception Unwrap(Exception ex)=>ex is TargetInvocationException{InnerException:{} inner}?inner:ex;
 static async Task<string?> PickFile(){var chosen=await Desktop.Owner!.StorageProvider.OpenFilePickerAsync(new(){Title="파일 선택"});return chosen.FirstOrDefault()?.TryGetLocalPath();}
}
