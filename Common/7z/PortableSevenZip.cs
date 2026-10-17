using SharpCompress.Archives;
using SharpCompress.Readers;

namespace SevenZip;

public sealed class ArchiveFileInfo
{
    public uint Index { get; init; }
    public string FileName { get; init; } = "";
    public bool IsDirectory { get; init; }
    public ulong Size { get; init; }
}
public sealed class ProgressEventArgs(byte percent) : EventArgs
{
    public byte PercentDone { get; } = percent;
    public bool Cancel { get; set; }
}

// Compatibility surface for upstream's archive callers. Actual decompression is managed.
public sealed class SevenZipExtractor : IDisposable
{
    readonly IArchive archive;
    readonly IArchiveEntry[] entries;
    public IReadOnlyList<ArchiveFileInfo> ArchiveFileData { get; }
    public event EventHandler<ProgressEventArgs>? Extracting;
    public SevenZipExtractor(string path) : this(path, null) { }
    public SevenZipExtractor(string path, string? password)
    {
        archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        entries = archive.Entries.ToArray();
        ArchiveFileData = entries.Select((x, i) => new ArchiveFileInfo
        { Index = (uint)i, FileName = x.Key ?? "", IsDirectory = x.IsDirectory, Size = (ulong)Math.Max(0, x.Size) }).ToArray();
    }
    public void ExtractFile(int index, Stream output)
    {
        var entry = entries[index];
        using var input = entry.OpenEntryStream();
        byte[] buffer = new byte[1024 * 1024];
        long done = 0;
        int count;
        while ((count = input.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, count); done += count;
            var args = new ProgressEventArgs((byte)(entry.Size > 0 ? Math.Min(100, done * 100 / entry.Size) : 100));
            Extracting?.Invoke(this, args);
            if (args.Cancel) throw new OperationCanceledException();
        }
    }
    public void ExtractFiles(string directory, params int[] indexes)
    {
        string root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        foreach (int index in indexes)
        {
            var entry = entries[index];
            string name = (entry.Key ?? "").Replace('\\', '/');
            string target = Path.GetFullPath(Path.Combine(root, name));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Archive entry escapes output directory: " + name);
            if (entry.IsDirectory) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write);
            ExtractFile(index, output);
        }
    }
    public void ExtractArchive(string directory) => ExtractFiles(directory, Enumerable.Range(0, entries.Length).ToArray());
    public void Dispose() => archive.Dispose();
}
