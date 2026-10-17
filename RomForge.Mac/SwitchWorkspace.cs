using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using Common.WPF.ViewModels;
using LibHac.Ns;
using NSW.Core;
using NSW.Core.Enums;
using NSW.HacPack.Models;
using NSW.WPF.Services;
using NSW.WPF.ViewModels;
using RomForge.ViewModels.Switch;

namespace RomForge.Portable;

// Replaces the original WPF code-behind that joins the file manager, metadata editor and engine.
public sealed class SwitchWorkspace : ViewModelBase
{
 public ToolTabViewModel Engine { get; }
 public SwitchTitleListViewModel Files { get; }=new(new(StringComparer.OrdinalIgnoreCase){".nsp",".nsz",".xci",".xcz"});
 public ObservableCollection<LanguageInfo> Languages { get; }=new();
 Task metadataLoad=Task.CompletedTask;
 public GameMetadata? Metadata { get; private set; }
 public ApplicationControlProperty.Language ForcedLanguage { get; set; }=ApplicationControlProperty.Language.None;
 public byte? TargetIdOffset { get; set; }
 public string OverrideTitleId { get; set; }="";
 public SwitchWorkspace(ToolTabViewModel engine)
 {
  Engine=engine;Files.LogAction=(message,level)=>
  {
   var method=Engine.GetType().GetMethod("Log");
   if(method!=null){var args=method.GetParameters().Select(p=>p.HasDefaultValue?p.DefaultValue:null).ToArray();args[0]=message;args[1]=level;method.Invoke(Engine,args);}
  };
  Files.FileListChanged+=()=>metadataLoad=ReadMetadataSafelyAsync();
 }
 async Task ReadMetadataSafelyAsync(){try{await ReloadMetadataAsync();}catch(Exception ex){Files.Log(ex.Message,Common.LogLevel.Error);}}
 public async Task ReloadMetadataAsync()
 {
  var keys=KeySetProvider.Instance.KeySet;
  if(keys==null)return;
  var paths=Files.GameFiles.Where(x=>!x.IsKeyMissing).Select(x=>x.FilePath).ToList();
  Metadata=await Task.Run(()=>paths.Count>0?MetadataService.GetGameMetadata(keys,paths):Engine is RepackMainViewModel repack?MetadataService.GetGameMetadataFromUnpacked(Path.Combine(repack.OutputPath,"unpacked")):null);
  Languages.Clear();if(Metadata!=null)foreach(var language in Metadata.Languages)Languages.Add(language);
  OnPropertyChanged(nameof(Metadata));
 }
 public async Task StartAsync(BuildMode mode)
 {
  if(Engine is not RepackMainViewModel repack)throw new InvalidOperationException("리팩 화면에서 실행하세요.");
  ulong? title=null;
  if(!string.IsNullOrWhiteSpace(OverrideTitleId))
  {
   if(!ulong.TryParse(OverrideTitleId,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out ulong value))throw new ArgumentException("타이틀 ID는 16진수로 입력하세요.");
   title=value;
  }
  await PrepareAsync();
  repack.Context=new(Files.GameFiles,Metadata,ForcedLanguage,TargetIdOffset,title);
  await repack.StartAsync(mode);
 }
 public async Task MergeAsync(){if(Engine is not MergeMainViewModel vm)throw new InvalidOperationException("병합 화면에서 실행하세요.");await PrepareAsync();await vm.MergeAsync(Files.GameFiles);}
 public async Task SplitAsync(){if(Engine is not MergeMainViewModel vm)throw new InvalidOperationException("분리 화면에서 실행하세요.");await PrepareAsync();await vm.SplitAsync(Files.GameFiles);}
 public async Task ExecuteStartWorkAsync(){if(Engine is not KeygenMainViewModel vm)throw new InvalidOperationException("버전 변경 화면에서 실행하세요.");await PrepareAsync();await vm.ExecuteStartWorkAsync(Files.GameFiles);}
 async Task PrepareAsync()
 {
  // Re-import missing-key rows so title IDs, program offsets and metadata are refreshed together.
  if(KeySetProvider.Instance.KeySet!=null){var paths=Files.GameFiles.Where(x=>x.IsKeyMissing).Select(x=>x.FilePath).Distinct().ToArray();if(paths.Length>0)await Files.AddFilesAsync(paths);}
  await metadataLoad;
 }
 public async Task AssignPatchAsync(NSW.WPF.ViewModels.GameFile file)
 {
  var folders=await Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new(){Title="선택 게임의 패치 폴더"});
  var path=folders.FirstOrDefault()?.TryGetLocalPath();if(path!=null)Files.TryAssignPatch(file,RomForge.Core.Services.Switch.SwitchPatchSource.FromFolder(path),null);
 }
 public async Task BulkPatchFolderAsync()
 {
  var folders=await Desktop.Owner!.StorageProvider.OpenFolderPickerAsync(new(){Title="타이틀 ID별 패치 루트 폴더"});
  var path=folders.FirstOrDefault()?.TryGetLocalPath();if(path==null)return;
  Files.ProcessBulkPatchResult(RomForge.Core.Services.Switch.SwitchBulkPatchMatcher.MatchFromFolder(Files.GameFiles.ToList(),path,Files.GetExpectedIds));
 }
 public async Task BulkPatchArchiveAsync()
 {
  var files=await Desktop.Owner!.StorageProvider.OpenFilePickerAsync(new(){Title="타이틀 ID별 패치 ZIP / 7z"});
  var path=files.FirstOrDefault()?.TryGetLocalPath();if(path==null)return;
  var opened=await RomForge.Views.PasswordPromptWindow.OpenWithPasswordPromptAsync(path,Desktop.Owner);if(opened==null)return;
  using var archive=opened.Value.Archive;
  Files.ProcessBulkPatchResult(RomForge.Core.Services.Switch.SwitchBulkPatchMatcher.MatchFromArchive(Files.GameFiles.ToList(),archive,path,opened.Value.Password,Files.GetExpectedIds));
 }
 public void Cancel()=>Engine.GetType().GetMethod("Cancel")?.Invoke(Engine,null);
}
