using RomForge.Portable;



using System.IO;
using System.Net.Http;


namespace RomForge.Core.Services.Switch;

public static class SaturnCoverArtFetcher
{
    private const string BaseUrl = "https://raw.githubusercontent.com/sinjunyoung/ss-covers/main/covers/default";
    private static readonly HttpClient Http = new();

    public static async Task<byte[]?> TryDownloadCoverPngAsync(string gameId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameId)) 
            return null;

        try 
        {
            byte[] imageBytes = await Http.GetByteArrayAsync($"{BaseUrl}/{gameId}.jpg", ct);

            using var image=Raster.Load(imageBytes);
            image.Resize(256,256);
            using var msOutput=new MemoryStream();
            image.SaveJpeg(msOutput);
            return msOutput.ToArray();
        }
        catch 
        { 
            return null; 
        }
    }
}