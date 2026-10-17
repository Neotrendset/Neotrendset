using Common;
using Common.WPF.ViewModels;
using RomForge.Core.UI.Command;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.ViewModels.PS;

// Native macOS audio playback with PCM WAV seeking; no WinMM/WASAPI dependency.
public class BgmPreviewViewModel : ViewModelBase
{
 readonly Action<string,LogLevel> log;
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
 readonly Stopwatch clock=new();
 byte[]? wav;Process? player;string? temporary;
 bool seeking,playing;double position,duration,start;
 public BgmPreviewViewModel(Action<string,LogLevel> log)
 {
  this.log=log;TogglePlayCommand=new RelayCommand(_=>{if(IsPlaying)Pause();else Play();});
  timer.Tick+=(_,_)=>{if(player is {} p&&p.HasExited){Stop();return;}if(!seeking)SetPosition(start+clock.Elapsed.TotalSeconds);};
 }
 public ICommand TogglePlayCommand{get;}
 public bool HasPreview=>wav is {Length:>0};
 public bool IsPlaying{get=>playing;private set=>SetProperty(ref playing,value);}
 public double DurationSeconds{get=>duration;private set{SetProperty(ref duration,value);OnPropertyChanged(nameof(DurationText));}}
 public double PositionSeconds{get=>position;set{SetPosition(value);if(!seeking&&playing){Pause();Play();}}}
 public string PositionText=>Format(position);public string DurationText=>Format(duration);
 public void Load(byte[]? bytes){Stop();wav=bytes;DurationSeconds=bytes==null?0:Parse(bytes).Length/(double)Parse(bytes).Rate;OnPropertyChanged(nameof(HasPreview));}
 public void Clear(){Load(null);}
 public void BeginSeek()=>seeking=true;
 public void EndSeek(){seeking=false;if(playing){Pause();Play();}}
 public void Stop(){Release();SetPosition(0);}
 void Pause(){double next=start+clock.Elapsed.TotalSeconds;Release();SetPosition(next);}
 void Play()
 {
  if(wav==null)return;
  try
  {
   var format=Parse(wav);
   int skip=(int)Math.Min(format.Length,(long)(position*format.Rate)/format.Align*format.Align);
   var slice=new byte[format.Offset+format.Length-skip];
   wav.AsSpan(0,format.Offset).CopyTo(slice);wav.AsSpan(format.Offset+skip,format.Length-skip).CopyTo(slice.AsSpan(format.Offset));
   BinaryPrimitives.WriteInt32LittleEndian(slice.AsSpan(4),slice.Length-8);
   BinaryPrimitives.WriteInt32LittleEndian(slice.AsSpan(format.Offset-4),format.Length-skip);
   temporary=Path.Combine(Path.GetTempPath(),"romforge-preview-"+Guid.NewGuid().ToString("N")+".wav");File.WriteAllBytes(temporary,slice);
   var psi=new ProcessStartInfo(OperatingSystem.IsMacOS()?"/usr/bin/afplay":"ffplay"){UseShellExecute=false};
   if(!OperatingSystem.IsMacOS()){psi.ArgumentList.Add("-nodisp");psi.ArgumentList.Add("-autoexit");}psi.ArgumentList.Add(temporary);
   player=Process.Start(psi);start=position;clock.Restart();IsPlaying=true;timer.Start();
  }
  catch(Exception ex){log("미리듣기 재생 오류: "+ex.Message,LogLevel.Error);Release();}
 }
 void Release()
 {
  timer.Stop();clock.Stop();if(player is {} p){if(!p.HasExited)p.Kill();p.Dispose();player=null;}
  if(temporary is {} path){File.Delete(path);temporary=null;}IsPlaying=false;
 }
 void SetPosition(double value){position=Math.Clamp(value,0,duration);OnPropertyChanged(nameof(PositionSeconds));OnPropertyChanged(nameof(PositionText));}
 static string Format(double value){var time=TimeSpan.FromSeconds(Math.Max(0,value));return $"{(int)time.TotalMinutes}:{time.Seconds:00}";}
 static (int Offset,int Length,int Rate,int Align) Parse(byte[] bytes)
 {
  int rate=0,align=0;
  if(bytes.Length<12||!bytes.AsSpan(0,4).SequenceEqual("RIFF"u8))throw new InvalidDataException("WAV 헤더 오류");
  for(int p=12;p+8<=bytes.Length;)
  {
   int length=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p+4,4));if(length<0||length>bytes.Length-p-8)throw new InvalidDataException("잘린 WAV");
   if(bytes.AsSpan(p,4).SequenceEqual("fmt "u8)&&length>=16){rate=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p+16,4));align=BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(p+20,2));}
   if(bytes.AsSpan(p,4).SequenceEqual("data"u8)&&rate>0&&align>0)return(p+8,length,rate,align);
   p=checked(p+8+length+(length&1));
  }
  throw new InvalidDataException("WAV 오디오 데이터 없음");
 }
}
