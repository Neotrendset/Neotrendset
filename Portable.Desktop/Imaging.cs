using RomForge.Portable;
using System.Runtime.InteropServices;
namespace System.Windows.Media
{
 public readonly record struct Color(byte A,byte R,byte G,byte B)
 {
  public static Color FromRgb(byte r,byte g,byte b)=>new(255,r,g,b);
  public static Color FromArgb(byte a,byte r,byte g,byte b)=>new(a,r,g,b);
 }
 public static class ColorConverter{public static object ConvertFromString(string hex){var c=Avalonia.Media.Color.Parse(hex);return new Color(c.A,c.R,c.G,c.B);}}
 public class Brush{public void Freeze(){} }
 public class SolidColorBrush(Color color):Brush{public Color Color{get;}=color;}
 public static class Brushes
 {
  public static readonly Brush Transparent=new SolidColorBrush(new(0,0,0,0)),White=new SolidColorBrush(new(255,255,255,255)),Black=new SolidColorBrush(new(255,0,0,0)),Gray=new SolidColorBrush(new(255,128,128,128)),Red=new SolidColorBrush(new(255,255,0,0)),Green=new SolidColorBrush(new(255,0,128,0)),Orange=new SolidColorBrush(new(255,255,165,0)),Blue=new SolidColorBrush(new(255,0,0,255));
 }
 public readonly record struct PixelFormat(string Name,int BitsPerPixel);
 public static class PixelFormats{public static PixelFormat Bgra32=>new("Bgra32",32);public static PixelFormat Bgr32=>new("Bgr32",32);public static PixelFormat Rgb24=>new("Rgb24",24);public static PixelFormat Pbgra32=>Bgra32;}
 public abstract class ImageSource : Avalonia.Media.IImage
 {
  internal Raster? Data;
  Avalonia.Media.Imaging.Bitmap? bitmap;
  public Avalonia.Size Size=>new(Data?.Width??0,Data?.Height??0);
  public void Draw(Avalonia.Media.DrawingContext context,Avalonia.Rect sourceRect,Avalonia.Rect destRect)
  {
   if(Data==null)return;
   if(bitmap==null){using var s=new MemoryStream();Data.SavePng(s);s.Position=0;bitmap=new Avalonia.Media.Imaging.Bitmap(s);}
   context.DrawImage(bitmap,sourceRect,destRect);
  }
  public void Freeze(){} public bool IsFrozen=>true;
 }
}
namespace System.Windows.Media.Imaging
{
 using System.Windows.Media;
 public enum BitmapCacheOption{Default,OnLoad}
 public enum BitmapCreateOptions{None,PreservePixelFormat}
 public class BitmapSource:ImageSource
 {
  public int PixelWidth=>Data?.Width??0;public int PixelHeight=>Data?.Height??0;
  public double DpiX=>96;public double DpiY=>96;public PixelFormat Format=>PixelFormats.Bgra32;
  public static BitmapSource Create(int w,int h,double dx,double dy,PixelFormat format,object? palette,Array pixels,int stride)
  {
   byte[] bytes=new byte[pixels.Length*System.Buffer.ByteLength(pixels)/Math.Max(1,pixels.Length)];System.Buffer.BlockCopy(pixels,0,bytes,0,bytes.Length);
   if(format.Name=="Bgr32")for(int i=3;i<bytes.Length;i+=4)bytes[i]=255;
   var compact=new byte[w*h*4];for(int y=0;y<h;y++)bytes.AsSpan(y*stride,w*4).CopyTo(compact.AsSpan(y*w*4));
   return new BitmapSource{Data=Raster.FromBgra(compact,w,h)};
  }
  public void CopyPixels(Array pixels,int stride,int offset)
  {
   if(Data==null)return;byte[] bytes=new byte[PixelWidth*PixelHeight*4];Data.CopyBgraTo(bytes);
   System.Buffer.BlockCopy(bytes,0,pixels,offset,bytes.Length);
  }
  public void CopyPixels(System.Windows.Int32Rect rect,Array pixels,int stride,int offset)=>CopyPixels(pixels,stride,offset);
 }
 public sealed class BitmapImage:BitmapSource
 {
  public Stream? StreamSource{get;set;}public Uri? UriSource{get;set;}public BitmapCacheOption CacheOption{get;set;}
  public int DecodePixelWidth{get;set;}public int DecodePixelHeight{get;set;}
  public BitmapImage(){}public BitmapImage(Uri uri){UriSource=uri;EndInit();}
  public void BeginInit(){}
  public void EndInit()
  {
   if(StreamSource!=null)Data=Raster.Load(StreamSource);
   else if(UriSource!=null){string path=UriSource.IsFile?UriSource.LocalPath:RomForge.Portable.Desktop.ResolveAsset(UriSource.OriginalString);if(File.Exists(path))Data=Raster.Load(path);}
   if(Data!=null&&DecodePixelWidth>0)Data.Resize(DecodePixelWidth,DecodePixelHeight>0?DecodePixelHeight:0);
  }
 }
 public sealed class BitmapFrame:BitmapSource
 {
  public static BitmapFrame Create(BitmapSource source)=>new(){Data=source.Data?.Clone()};
  public static BitmapFrame Create(Stream source)=>new(){Data=Raster.Load(source)};
 }
 public sealed class BitmapDecoder
 {
  public List<BitmapFrame> Frames{get;}=new();
  public static BitmapDecoder Create(Stream stream,BitmapCreateOptions options,BitmapCacheOption cache){var d=new BitmapDecoder();d.Frames.Add(BitmapFrame.Create(stream));return d;}
 }
 public sealed class PngBitmapEncoder
 {
  public List<BitmapFrame> Frames{get;}=new();
  public void Save(Stream output){if(Frames.Count==0)throw new InvalidDataException("No image frame.");Frames[0].Data!.SavePng(output);}
 }
}
