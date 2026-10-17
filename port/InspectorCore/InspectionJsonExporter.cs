using System.Text;

namespace RomForge.Mac.Core;

public static class InspectionJsonExporter
{
    // Never replace an existing path, including a source image, symlink or hard link.
    // Build a sibling temporary file, then move without overwrite to publish a complete JSON.
    public static async Task ExportAsync(ContainerInspection report, string outputPath, CancellationToken ct = default)
    {
        string path = Path.GetFullPath(outputPath);
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("JSON 결과는 .json 확장자로 저장하세요.", nameof(outputPath));
        if (File.Exists(path) || Directory.Exists(path))
            throw new IOException("기존 파일을 덮어쓰지 않습니다. 새 JSON 파일명을 선택하세요.");
        string directory = Path.GetDirectoryName(path)!;
        string temp = Path.Combine(directory, $".romforge-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                var data = Encoding.UTF8.GetBytes(report.ToJson());
                await stream.WriteAsync(data, ct);
                await stream.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
