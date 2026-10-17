using System.Text.RegularExpressions;
namespace RomForge.Portable;
public static class Labels
{
 static readonly Dictionary<string,string> Text=new(StringComparer.Ordinal)
 {
  ["NamingEnabled"]="출력 파일명 규칙 사용",["NamingFormat"]="출력 파일명 규칙",["Entries"]="항목",["Name"]="이름",["SelectedSegmentIndex"]="이미지 분할 크기",["RefreshDrives"]="디스크 목록 새로고침",["AssignPatchAsync"]="선택 게임 패치 지정…",["ClearPatch"]="선택 패치 해제",["BulkPatchFolderAsync"]="폴더에서 패치 일괄 지정…",["BulkPatchArchiveAsync"]="압축파일에서 패치 일괄 지정…",["ChdmanCompression"]="CHD 압축 방식",["InspectCommand"]="구조 검사",["ExportCommand"]="JSON 내보내기",["IncludeHash"]="SHA-256 포함",["SourcePath"]="원본 파일",["InputPath"]="입력 파일",["FilePath"]="파일",["OutputPath"]="출력 위치",["PatchPath"]="패치 파일 또는 폴더",["PatchDir"]="패치 폴더",["WorkPath"]="작업 폴더",["SaveDirectory"]="저장 폴더",["SdPath"]="SD 카드 폴더",["MovablePath"]="movable.sed 파일",["CuePath"]="CUE / CHD 파일",["NspPath"]="기본 NSP / NSZ 파일",
  ["FileName"]="파일명",["FileSize"]="크기",["FileSizeBytes"]="크기 (bytes)",["TitleName"]="게임 이름",["TitleID"]="타이틀 ID",["TitleId"]="타이틀 ID",["GameId"]="게임 ID",["GameTitle"]="게임 이름",["Publisher"]="배급사",["GameVersion"]="게임 버전",["NspTitleId"]="NSP 타이틀 ID",["Status"]="상태",["StatusMessage"]="상태",["No"]="순서",["Progress"]="진행도",["Extension"]="형식",["FileType"]="종류",["Icon"]="아이콘",["LogoData"]="아이콘 이미지",["Logo"]="아이콘 이미지",
  ["OutputFormat"]="출력 형식",["CompressLevel"]="압축 수준",["CompressionLevel"]="압축 수준",["VerifyCompress"]="압축 후 검사",["UseBlockMode"]="블록 압축",["UseBlocklessMode"]="스트림 압축",["ForceKeyGen0"]="키 세대 0으로 저장",["ForcedLanguage"]="강제 언어",["TargetIdOffset"]="멀티롬 인덱스",["OverrideTitleId"]="변경할 타이틀 ID (16진수)",["WideScreen"]="와이드 화면",["Language"]="언어",["Flag"]="사용",["IsSelected"]="선택",["IsChecked"]="선택",["Version"]="버전",
  ["RunCommand"]="실행",["CancelCommand"]="취소",["ClearCommand"]="목록 비우기",["CalculateHashCommand"]="해시 계산",["OpenWorkSpaceCommand"]="작업 폴더 열기",["BrowseOutputCommand"]="출력 폴더 선택",["BrowseInputCommand"]="원본 선택",["BrowsePatchFolderCommand"]="패치 폴더 선택",["BrowsePatchFileCommand"]="패치 파일 선택",["SettingsCommand"]="설정",["TogglePlayCommand"]="미리듣기 / 일시정지",["ReadCommand"]="디스크 이미지 읽기",["WriteCommand"]="디스크 이미지 쓰기",["OpenReadFileCommand"]="이미지 저장 위치",["OpenWriteFileCommand"]="쓸 이미지 선택",
  ["ManualMatch"]="수동 패치 지정",["SetFile"]="파일 선택",["ClearFile"]="파일 비우기",["ChangeSaveDirectory"]="저장 폴더 선택",["Reset"]="초기화",["RemoveRow"]="선택 항목 삭제",["StartAsync"]="실행",["MergeAsync"]="병합",["SplitAsync"]="분리",["ExecuteStartWorkAsync"]="버전 요구 변경",["ConvertAsync"]="변환 실행",["RunAsync"]="실행",["Cancel"]="취소",["CancelExtract"]="추출 취소",["Clear"]="비우기",["ClearItems"]="목록 비우기",["RemoveAllFiles"]="목록 비우기",["RemoveItems"]="선택 항목 삭제",["RemoveFile"]="선택 항목 삭제",["Remove"]="선택 항목 삭제",["AddPatchSlot"]="패치 단계 추가",["ReloadMetadataAsync"]="게임 정보 다시 읽기",["LoadTitlesAsync"]="설치된 게임 읽기",["RecoverTitleDbAsync"]="타이틀 DB 복구",["ExtractTitleAsync"]="선택한 게임 추출",["InstallAsync"]="선택한 게임 설치",["RefreshKeysStatus"]="키 다시 읽기",["ResetAll"]="이미지 모두 초기화",["ResetBootLogo"]="부팅 로고 초기화",["ResetBaseImages"]="기본 이미지 복원",["UpdateFromGameIdAsync"]="게임 ID로 표지 받기",["SetPathAsync"]="배경음 선택",["FullProcess"]="전체 실행",["UnpackOnly"]="추출",["RebuildOnly"]="리팩",["PatchOnly"]="패치만 적용",
  ["FileItems"]="파일 목록",["GameFiles"]="게임 목록",["FilteredTitles"]="설치된 게임",["AllTitles"]="전체 게임",["PatchSlots"]="연속 패치",["Languages"]="언어별 게임 정보",["LanguagesList"]="언어",["Files"]="입력 파일",["Images"]="PBP 이미지",["Bgm"]="배경음",["Preview"]="미리듣기",["PopsOptions"]="POPS 실행 설정",["SourceHash"]="원본 해시",["InstalledTitles"]="설치된 게임",["Install"]="새 게임 설치",["Engine"]="출력 및 실행",["Title"]="이름",["AutoCompress"]="완료 후 자동 압축",["Topmost"]="항상 위에 표시",["AutoCheckUpdate"]="업데이트 확인",["IsMuted"]="효과음 끄기",["CompleteSoundPath"]="완료 효과음",["CancelSoundPath"]="취소 효과음",["ErrorSoundPath"]="오류 효과음",["ReadImagePath"]="이미지 저장 파일",["WriteImagePath"]="쓸 이미지 파일",["SelectedDrive"]="대상 외장 디스크"
 };
 public static string For(string value)=>Text.TryGetValue(value,out var label)?label:Regex.Replace(value,"(?<=[a-z0-9])([A-Z])"," $1").Replace("Command","").Replace("Async","");
 public static string Display(object? value)
 {
  if(value==null)return "";
  var type=value.GetType();
  foreach(string name in new[]{"DisplayName","Display","TitleName","Name","Label"})
   if(type.GetProperty(name)?.GetValue(value) is string s&&!string.IsNullOrWhiteSpace(s))return s;
  return value is Enum?For(value.ToString()!):value.ToString()??"";
 }
}
