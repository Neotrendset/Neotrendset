using RomForge.Portable;



using System.IO;

using System.Windows.Media.Imaging;

namespace NSW.WPF.Services;

public static class Extensions
{
    public static BitmapImage? ToBitmapImage(this byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            return null;

        var image = new BitmapImage();
        using var memoryStream = new MemoryStream(imageData);

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memoryStream;
        image.EndInit();
        image.Freeze();

        return image;
    }

    public static byte[] ResizePng(this byte[] imageBytes,int width,int height)
    {
        using var image=Raster.Load(imageBytes);
        image.Resize(width,height);using var output=new MemoryStream();image.SavePng(output);return output.ToArray();
    }
    public static byte[] ToImageBytes(this string filePath)
    {
        using var image=Raster.Load(filePath);
        image.Resize(256,256);using var output=new MemoryStream();image.SaveJpeg(output);return output.ToArray();
    }
}
