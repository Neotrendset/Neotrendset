using RomForge.Portable;
using System.IO;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Services.PS;

public static class AssetImageEditor
{
    public static (byte[] Bytes, BitmapImage Image) Resize(byte[] rawBytes, int targetWidth, int targetHeight)
    {
        using var image = Raster.Load(rawBytes);
        image.Resize(targetWidth, targetHeight);

        using var ms = new MemoryStream();
        image.SavePng(ms);

        var finalBytes = ms.ToArray();

        ms.Position = 0;
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = ms;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return (finalBytes, bitmapImage);
    }
}