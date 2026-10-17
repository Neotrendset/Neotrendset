namespace RomForge.Core.Services.Util;
public static class ClipboardHelper
{
 public static bool CopyText(string text)
 {
  if(string.IsNullOrEmpty(text))return false;
  System.Windows.Application.Current.Dispatcher.Invoke(()=>System.Windows.Clipboard.SetText(text));return true;
 }
}
