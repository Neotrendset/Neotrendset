using Avalonia.Controls;
using Patch.Core.Services;
namespace RomForge.Views;
public static class PasswordPromptWindow
{
 public static async Task<(IArchivePatchSource Archive,string? Password)?> OpenWithPasswordPromptAsync(string path,Avalonia.Controls.Window? owner)
 {
  string? password=null;
  while(true)
  {
   try{return(await Task.Run(()=>ArchivePatchSourceFactory.Open(path,password)),password);}
   catch(ArchivePasswordRequiredException)
   {
    var window=new Window{Title="압축 파일 비밀번호",Width=420,SizeToContent=SizeToContent.Height};
    var panel=new StackPanel{Margin=new Avalonia.Thickness(20),Spacing=12};
    panel.Children.Add(new TextBlock{Text=Path.GetFileName(path)});
    var input=new TextBox{PasswordChar='●',Watermark=password==null?"비밀번호":"비밀번호가 맞지 않습니다. 다시 입력하세요."};panel.Children.Add(input);
    var ok=new Button{Content="열기"};ok.Click+=(_,_)=>window.Close(input.Text);panel.Children.Add(ok);
    var cancel=new Button{Content="취소"};cancel.Click+=(_,_)=>window.Close(null);panel.Children.Add(cancel);window.Content=panel;
    if(owner==null)throw new InvalidOperationException("No desktop window.");
    password=await window.ShowDialog<string?>(owner);if(password==null)return null;
   }
  }
 }
}
