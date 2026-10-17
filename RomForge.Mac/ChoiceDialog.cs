using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
namespace RomForge.Portable;
public static class ChoiceDialog
{
 public static async Task<int?> ChooseAsync(string title,IReadOnlyList<string> choices)
 {
  if(!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=>ChooseAsync(title,choices));
  var window=new Window{Title=title,Width=600,Height=380,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel{Margin=new Thickness(20)};var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};DockPanel.SetDock(buttons,Dock.Bottom);
  var list=new ListBox{ItemsSource=choices,SelectedIndex=0};var ok=new Button{Content="선택"};ok.Click+=(_,_)=>window.Close((int?)list.SelectedIndex);var cancel=new Button{Content="취소"};cancel.Click+=(_,_)=>window.Close(null);buttons.Children.Add(ok);buttons.Children.Add(cancel);panel.Children.Add(buttons);panel.Children.Add(list);window.Content=panel;
  return await window.ShowDialog<int?>(Desktop.Owner!);
 }
}
