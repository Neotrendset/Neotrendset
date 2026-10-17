using Common.WPF.ViewModels;
using RomForge.ViewModels;

namespace RomForge.Portable;
public sealed record WorkflowPage(string Title,object Model,ToolTabViewModel Original,IReadOnlyList<ToolTabViewModel> Parents)
{
 public void Activate()
 {
  for(int i=0;i<Parents.Count;i++)
  {
   var parent=Parents[i];var child=i+1<Parents.Count?Parents[i+1]:Original;
   if(parent is MainViewModel main)main.SelectedViewModel=child;
   else if(parent is MultiToolTabViewModel multi)multi.SelectedViewModel=child;
  }
 }
 public override string ToString()=>Title;
}
public static class WorkflowCatalog
{
 public static IReadOnlyList<WorkflowPage> Build(MainViewModel root)
 {
  var result=new List<WorkflowPage>();
  foreach(var tool in root.Tools)Visit(tool,new[]{(ToolTabViewModel)root},new List<string>());
  return result;
  void Visit(ToolTabViewModel tool,IReadOnlyList<ToolTabViewModel> parents,List<string> path)
  {
   string name=Name(tool);var next=new List<string>(path){name};
   if(tool.Tools.Count==0||tool is RomForge.ViewModels._3DS.InstallerMainViewModel)
   {
    object model=tool;
    if(tool is RomForge.ViewModels.Switch.RepackMainViewModel or RomForge.ViewModels.Switch.MergeMainViewModel or RomForge.ViewModels.Switch.KeygenMainViewModel)model=new SwitchWorkspace(tool);
    result.Add(new(string.Join(" › ",next),model,tool,parents));
   }
   else foreach(var child in tool.Tools)Visit(child,parents.Concat(new[]{tool}).ToArray(),next);
  }
 }
 static string Name(ToolTabViewModel tool)
 {
  var type=tool.GetType();string name=type.Name;
  return name switch
  {
   "PatchMainViewModel"=>"패치", "NormalPatchMainViewModel"=>"일반", "ArcadePatchMainViewModel"=>"아케이드", "VitaPatchMainViewModel"=>"Vita", "Pc98PatchMainViewModel"=>"PC-98", "CompressMainViewModel"=>"압축 / 해제", "ConvertMainViewModel"=>"디스크 형식 변환", "SwitchMainViewModel"=>"Switch", "WiiUMainViewModel"=>"Wii U", "WiiMainViewModel"=>"Wii", "_3DSMainViewModel"=>"3DS", "InstallerMainViewModel"=>"SD 설치 / 추출", "PS1MainViewModel"=>"PS / PSP", "UtilMainViewModel"=>"유틸리티", "SettingsMainViewModel"=>"설정", "RepackMainViewModel"=>"리팩", "MergeMainViewModel"=>"병합 / 분리", "ConverterMainViewModel"=>"형식 변환", "ConvertSaturnMainViewModel"=>"새턴 → NSP", "KeygenMainViewModel"=>"버전 요구 변경", "DecryptorMainViewModel"=>"복호화", "PackingMainViewModel"=>"PBP 패킹", "UnpackingMainViewModel"=>"PBP 언팩", "PSPConverterViewModel"=>"CSO / ZSO 변환", "HashMainViewModel"=>"해시 계산", "TistoryMainViewModel"=>"티스토리 첨부파일", "ZipImageToolMainViewModel"=>"디스크 이미지", "CertsMainViewModel"=>"CIA 인증서 추출", "CueMainViewModel"=>"CUE 도구", "GeneralSettingsMainViewModel"=>"일반", "PatchSettingsMainViewModel"=>"패치", "CompressSettingsMainViewModel"=>"압축", "PSSettingsMainViewModel"=>"PS / PSP", _=>Labels.For(name.Replace("MainViewModel","").Replace("ViewModel",""))
  };
 }
}
