using SkiaSharp;
using System.Runtime.InteropServices;
namespace RomForge.Portable;

// Shared raster processing for metadata icons and PBP assets. Skia runs natively on macOS.
public sealed class Raster : IDisposable
{
 SKBitmap bitmap;
 Raster(SKBitmap bitmap)=>this.bitmap=bitmap;
 public int Width=>bitmap.Width;public int Height=>bitmap.Height;
 public static Raster Load(byte[] data)=>Load(new MemoryStream(data));
 public static Raster Load(string path){using var stream=File.OpenRead(path);return Load(stream);}
 public static Raster Load(Stream stream)
 {
  using var codec=SKCodec.Create(stream)??throw new InvalidDataException("지원하지 않는 이미지입니다.");
  var info=new SKImageInfo(codec.Info.Width,codec.Info.Height,SKColorType.Bgra8888,SKAlphaType.Unpremul);
  var bitmap=new SKBitmap(info);
  if(codec.GetPixels(info,bitmap.GetPixels())!=SKCodecResult.Success){bitmap.Dispose();throw new InvalidDataException("잘린 이미지입니다.");}
  return new(bitmap);
 }
 public static Raster FromBgra(byte[] bytes,int width,int height)
 {
  var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul));
  Marshal.Copy(bytes,0,bitmap.GetPixels(),checked(width*height*4));return new(bitmap);
 }
 public Raster Clone()=>new(bitmap.Copy());
 public void CopyBgraTo(byte[] bytes)=>Marshal.Copy(bitmap.GetPixels(),bytes,0,checked(Width*Height*4));
 public void Resize(int width,int height)
 {
  if(height<=0)height=Math.Max(1,(int)((long)Height*width/Width));
  var resized=bitmap.Resize(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul),SKFilterQuality.High)??throw new InvalidOperationException("이미지 크기 변경 실패");
  bitmap.Dispose();bitmap=resized;
 }
 public void SavePng(Stream stream){using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);data.SaveTo(stream);}
 public void SaveJpeg(Stream stream,int quality=95){using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Jpeg,quality);data.SaveTo(stream);}
 public void Dispose()=>bitmap.Dispose();
}
