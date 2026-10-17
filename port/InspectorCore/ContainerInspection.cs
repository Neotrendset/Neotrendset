using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomForge.Mac.Core;

public sealed record InspectionIssue(string Severity, string Location, string Message);

// All offsets are absolute file offsets. Sizes are bytes, never media units.
public sealed record ContainerRegion(string Name, long Offset, long Size, bool IsWithinBounds)
{
    public long EndExclusive => checked(Offset + Size);
}

public sealed record ExeFsEntry(int Index, string Name, long RelativeOffset,
    ContainerRegion Data, string StoredSha256);

public sealed record ExeFsInspection(ContainerRegion Region, bool HeaderRead,
    IReadOnlyList<ExeFsEntry> Entries, string Note);

public sealed record NcchInspection(long Offset, string PartitionId, string ProgramId,
    string ProductCode, ushort Version, long MediaUnitSize, long DeclaredSize,
    bool IsEncrypted, ContainerRegion ExeFs, ContainerRegion RomFs,
    ExeFsInspection? ExeFsDetails);

public sealed record NcsdPartition(int Index, bool IsPresent, byte FileSystemType,
    byte CryptoType, ContainerRegion Region, NcchInspection? Ncch);

public sealed record CiaInspection(ushort Type, ushort Version,
    IReadOnlyList<ContainerRegion> Sections);

public sealed class ContainerInspection
{
    public string SchemaVersion { get; init; } = "1.0";
    public string ApplicationVersion { get; init; } = "0.7.0";
    public required string FilePath { get; init; }
    public long Size { get; init; }
    public required string Extension { get; init; }
    public string Kind { get; set; } = "Unknown";
    public bool HeaderRecognized { get; set; }
    public bool IsStructurallyValid => HeaderRecognized && Issues.All(x => x.Severity != "Error");
    public string? Sha256 { get; set; }
    public long? MediaUnitSize { get; set; }
    public long? DeclaredSize { get; set; }
    public List<NcsdPartition> Partitions { get; } = new();
    public NcchInspection? Ncch { get; set; }
    public CiaInspection? Cia { get; set; }
    public ExeFsInspection? ExeFs { get; set; }
    public List<InspectionIssue> Issues { get; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });
}
