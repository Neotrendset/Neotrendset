using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Common;
using RomForge.ViewModels;

namespace RomForge.Portable;
public sealed class MacWindow:System.Windows.Window
{
 public MainViewModel ViewModel{get;}
 public IReadOnlyList<WorkflowPage> Pages{get;}
 public WorkflowForm? CurrentForm{get;private set;}
 readonly ContentControl host=new();
 readonly TextBlock heading=new(){FontSize=24,FontWeight=FontWeight.SemiBold};
 readonly TextBlock message=new(){TextWrapping=TextWrapping.Wrap};
 readonly ListBox navigation=new();
 readonly Dictionary<WorkflowPage,WorkflowForm> forms=new();
 public MacWindow()
 {
  Directory.CreateDirectory(PortablePaths.DataDirectory);Directory.CreateDirectory(PortablePaths.KeyDirectory);
  Desktop.Owner=this;Title="RomForge-Mac · v1.9.1 엔진";Width=1280;Height=900;MinWidth=980;MinHeight=650;
  var cfg=RomForge.Core.AppConfig.Instance;Width=Math.Max(980,cfg.Window.Width);Height=Math.Max(650,cfg.Window.Height);Topmost=cfg.Common.Topmost;
  cfg.Common.PropertyChanged+=(_,e)=>{if(e.PropertyName=="Topmost")Topmost=cfg.Common.Topmost;};
  ViewModel=new MainViewModel();DataContext=ViewModel;var pages=WorkflowCatalog.Build(ViewModel).ToList();var inspector=new InspectorTool();pages.Add(new WorkflowPage("3DS › Container Inspector",inspector,inspector,Array.Empty<Common.WPF.ViewModels.ToolTabViewModel>()));Pages=pages;
  ViewModel.PatchVM.NormalVM.RequestSourceSelectionAsync=async choices=>{var index=await ChoiceDialog.ChooseAsync("압축 파일 안의 원본 선택",choices.Select(x=>$"{x.EntryKey} ({x.Size:N0} bytes)").ToArray());return index is >=0?choices[index.Value].EntryKey:null;};
  ViewModel.PatchVM.NormalVM.SourceHash.RequestGdRomLayoutAsync=async()=>{var index=await ChoiceDialog.ChooseAsync("GD-ROM CHD 해시 기준",new[]{"GDI (TOSEC / 패치 파일 기준)","CUE (Redump / 원본 비교 기준)"});return index==1?RomForge.Core.Models.Patch.GdRomHashLayout.Cue:RomForge.Core.Models.Patch.GdRomHashLayout.Gdi;};
  var root=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto"),Margin=new Thickness(20)};
  var toolbar=new DockPanel{LastChildFill=true};
  var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
  var keys=new Button{Content="키 파일 관리…"};keys.Click+=async(_,_)=>await ImportKeys();buttons.Children.Add(keys);
  var cancel=new Button{Content="전체 작업 취소"};cancel.Click+=(_,_)=>CancelOperations();buttons.Children.Add(cancel);
  DockPanel.SetDock(buttons,Dock.Right);toolbar.Children.Add(buttons);
  toolbar.Children.Add(new TextBlock{Text="RomForge-Mac",FontSize=30,FontWeight=FontWeight.Bold});root.Children.Add(toolbar);
  var body=new Grid{ColumnDefinitions=new ColumnDefinitions("270,*")};Grid.SetRow(body,1);root.Children.Add(body);
  navigation.ItemsSource=Pages;navigation.ItemTemplate=new FuncDataTemplate<WorkflowPage>((p,_)=>new TextBlock{Text=p?.Title,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
  navigation.SelectionChanged+=(_,_)=>{if(navigation.SelectedItem is WorkflowPage p)Show(p);};body.Children.Add(navigation);
  var right=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto")};Grid.SetColumn(right,1);body.Children.Add(right);right.Children.Add(heading);
  var scroll=new ScrollViewer{Content=host,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);right.Children.Add(scroll);
  var log=new TextBox{IsReadOnly=true,AcceptsReturn=true,Height=150,TextWrapping=TextWrapping.Wrap,Watermark="작업 로그"};Grid.SetRow(log,2);right.Children.Add(log);
  var footer=new StackPanel{Spacing=6};footer.Children.Add(message);
  var progress=new ProgressBar{Minimum=0,Maximum=1,Height=5};progress.Bind(ProgressBar.ValueProperty,new Binding(nameof(MainViewModel.TaskbarProgressValue)){Source=ViewModel});footer.Children.Add(progress);Grid.SetRow(footer,2);root.Children.Add(footer);
  ViewModel.PropertyChanged+=(_,e)=>
  {
   if(e.PropertyName==nameof(MainViewModel.ActiveLogEntries))BindLog();
   if(e.PropertyName==nameof(MainViewModel.SelectedTabIndex))NavigateToSelected();
  };
  foreach(var tool in ViewModel.Tools.OfType<MultiToolTabViewModel>())tool.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(MultiToolTabViewModel.SubTabIndex))NavigateToSelected();};
  var timer=new Avalonia.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
  timer.Tick+=(_,_)=>{System.Windows.Input.CommandManager.InvalidateRequerySuggested();log.Text=string.Join(Environment.NewLine,ViewModel.ActiveLogEntries.Select(x=>$"{x.Timestamp:HH:mm:ss} {x.Message}"));progress.IsIndeterminate=!ViewModel.IsIdle&&progress.Value==0;};timer.Start();
  Closed+=(_,_)=>{timer.Stop();CancelOperations();ViewModel.UtilMainVM.ZipImageToolVM.Dispose();ViewModel.PSMainVM.PackingVM.Bgm.Preview.Stop();cfg.Window.Width=Width;cfg.Window.Height=Height;cfg.Window.IsMaximized=WindowState==WindowState.Maximized;cfg.Save();Common.WPF.SoundManager.Stop();PickPack.Disk.ETC.DiskUtil.AllowSleep();};
  Content=root;navigation.SelectedIndex=0;
  Opened+=async(_,_)=>
  {
   if(cfg.Window.IsMaximized)WindowState=WindowState.Maximized;
   if(!cfg.Common.AutoCheckUpdate||Environment.GetEnvironmentVariable("ROMFORGE_TEST_MODE")=="1")return;
   try{if(await RomForge.Core.Services.VersionHelper.IsUpdateAvailableAsync())message.Text="원본 RomForge에 새 버전이 있습니다: https://github.com/sinjunyoung/RomForge/releases";}catch(Exception){/* Offline startup remains available. */}
  };
  void BindLog()=>log.Text=string.Join(Environment.NewLine,ViewModel.ActiveLogEntries.Select(x=>x.Message));
 }
 public void Show(WorkflowPage page)
 {
  page.Activate();heading.Text=page.Title;
  if(!forms.TryGetValue(page,out var form)){form=new WorkflowForm(page.Model,ex=>message.Text="오류: "+ex.Message,page.Parents.OfType<RomForge.ViewModels.Patch.PatchMainViewModel>().FirstOrDefault());forms.Add(page,form);}
  CurrentForm=form;host.Content=form;message.Text="파일을 선택하고 출력 옵션을 확인한 뒤 실행하세요.";
 }
 void NavigateToSelected()
 {
  var tool=ViewModel.Tools.ElementAtOrDefault(ViewModel.SelectedTabIndex);
  while(tool is MultiToolTabViewModel multi&&multi.SelectedTool!=null)tool=multi.SelectedTool;
  var page=Pages.FirstOrDefault(x=>ReferenceEquals(x.Original,tool));
  if(page!=null&&!ReferenceEquals(navigation.SelectedItem,page))navigation.SelectedItem=page;
 }
 void CancelOperations()
 {
  ViewModel.CancelAll();
  foreach(var page in Pages)
  {
   var tool=page.Original;if(!tool.IsLocked)continue;
   try{if(tool.CancelCommand?.CanExecute(null)==true)tool.CancelCommand.Execute(null);else tool.GetType().GetMethod("Cancel",Type.EmptyTypes)?.Invoke(tool,null);}catch(System.Reflection.TargetInvocationException){}
  }
 }
 async Task ImportKeys()
 {
  try
  {
   var files=await StorageProvider.OpenFilePickerAsync(new(){Title="사용자 키 파일 추가",AllowMultiple=true});
   var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"prod.keys","title.keys","boot9.bin","aes_keys.txt","seeddb.bin","certs.bin","keys.txt"};
   int count=0;
   foreach(var file in files)
   {
    string? path=file.TryGetLocalPath();if(path==null)continue;string name=Path.GetFileName(path);
    if(!allowed.Contains(name))throw new ArgumentException("지원하는 키 파일명을 확인하세요: "+name);
    File.Copy(path,Path.Combine(PortablePaths.KeyDirectory,name),overwrite:true);count++;
   }
   _3DS.Core.Crypto.KeyStoreProvider.Instance.Reload();ViewModel.SwitchMainVM.RefreshKeysStatus();
   message.Text=$"키 파일 {count}개 추가: {PortablePaths.KeyDirectory}";
  }
  catch(Exception ex){message.Text="키 파일 추가 오류: "+ex.Message;}
 }
}
