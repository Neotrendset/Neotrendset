using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RomForge.Mac.Core;

// Synthetic metadata only: no commercial ROM, console keys or network dependencies.
var directory = Path.Combine(Path.GetTempPath(), "romforge-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var inspector = new ContainerInspectorService();
int passed = 0;
try
{
    await Test("NCCH identifiers, byte regions, ten ExeFS slots and reversed hashes", async () =>
    {
        var bytes = Ncch();
        var report = await inspector.InspectAsync(Save("plain.cxi", bytes), true);
        Check(report.Kind == "NCCH" && report.IsStructurallyValid);
        var ncch = report.Ncch!;
        Check(ncch.ProgramId == "0004000001234567" && ncch.ProductCode == "CTR-P-TEST");
        Check(ncch.ExeFs.Offset == 0x200 && ncch.ExeFs.Size == 0x400);
        Check(ncch.RomFs.Offset == 0x600 && ncch.RomFs.Size == 0x200);
        var entries = ncch.ExeFsDetails!.Entries;
        Check(entries.Count == 10 && entries[9].Index == 9);
        Check(entries[0].Data.Offset == 0x400 && entries[9].Data.Offset == 0x490);
        Check(entries[0].StoredSha256 == new string('1', 64) && entries[9].StoredSha256 == new string('a', 64));
        Check(report.Sha256 == Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var json = JsonDocument.Parse(report.ToJson());
        Check(json.RootElement.GetProperty("ncch").GetProperty("programId").GetString() == ncch.ProgramId);
        Check(ContainerInspectionFormatter.Format(report).Contains("CTR-P-TEST"));
    });
    await TestSync("NCCH media unit exponent changes all relative regions", () =>
    {
        var bytes = Ncch(); bytes[0x18e] = 1;
        Array.Resize(ref bytes, 0x1000);
        var report = Inspect("unit.ncch", bytes);
        Check(report.IsStructurallyValid && report.Ncch!.ExeFs.Offset == 0x400 && report.Ncch.RomFs.Offset == 0xc00);
    });
    await TestSync("encrypted ExeFS is never parsed as plaintext", () =>
    {
        var bytes = Ncch(); bytes[0x18f] = 0;
        var report = Inspect("encrypted.ncch", bytes);
        Check(report.Ncch!.IsEncrypted && !report.Ncch.ExeFsDetails!.HeaderRead);
        Check(report.Ncch.ExeFsDetails.Entries.Count == 0);
    });
    await TestSync("NCSD keeps eight slots, including slot seven, with absolute NCCH offsets", () =>
    {
        var report = Inspect("card.3ds", Ncsd());
        Check(report.IsStructurallyValid && report.Partitions.Count == 8);
        Check(report.Partitions[0].IsPresent && !report.Partitions[1].IsPresent && report.Partitions[7].IsPresent);
        Check(report.Partitions[7].Ncch!.ExeFsDetails!.Entries[0].Data.Offset == 0xe00);
    });
    await TestSync("trimmed NCSD padding produces warning but preserves bounded partitions", () =>
    {
        var bytes = Ncsd(); Put32(bytes, 0x104, 0x1000);
        var report = Inspect("trimmed.3ds", bytes);
        Check(report.IsStructurallyValid && report.Issues.Any(x => x.Severity == "Warning"));
    });
    await TestSync("NCSD overlapping partitions are errors", () =>
    {
        var bytes = Ncsd(); Put32(bytes, 0x158, 1);
        Check(!Inspect("overlap.3ds", bytes).IsStructurallyValid);
    });
    await TestSync("NCSD partition outside the file is not dereferenced", () =>
    {
        var bytes = Ncsd(); Put32(bytes, 0x120, 0xffffff00);
        var report = Inspect("outside.3ds", bytes);
        Check(!report.IsStructurallyValid && report.Partitions[0].Ncch == null);
    });
    await TestSync("NCSD partition may not read outside parent even when file has bytes", () =>
    {
        var bytes = Ncsd(); Put32(bytes, 0x124, 1);
        Check(!Inspect("small-partition.3ds", bytes).IsStructurallyValid);
    });
    await TestSync("CIA unaligned sizes use 64-byte section alignment", () =>
    {
        var report = Inspect("layout.cia", Cia());
        Check(report.IsStructurallyValid && report.Cia!.Sections.Count == 6);
        var sections = report.Cia.Sections;
        Check(sections[0].Offset == 0 && sections[0].Size == 0x2020);
        Check(sections[1].Offset == 0x2040 && sections[2].Offset == 0x2080);
        Check(sections[3].Offset == 0x20c0 && sections[4].Offset == 0x2140 && sections[5].Offset == 0x21c0);
    });
    await TestSync("CIA optional zero Meta may start after EOF padding", () =>
    {
        var bytes = Cia(); Put32(bytes, 0x14, 0); Array.Resize(ref bytes, 0x2181);
        Check(Inspect("no-meta.cia", bytes).IsStructurallyValid);
    });
    await TestSync("CIA missing Content Index is rejected", () =>
    {
        var bytes = Cia(); Array.Resize(ref bytes, 0x100);
        var report = Inspect("truncated.cia", bytes);
        Check(!report.HeaderRecognized && !report.IsStructurallyValid);
    });
    await TestSync("CIA content outside file is rejected", () =>
    {
        var bytes = Cia(); Put64(bytes, 0x18, 0xffffffff);
        Check(!Inspect("bad-content.cia", bytes).IsStructurallyValid);
    });
    await TestSync("CIA hostile uint64 sizes yield serializable diagnostics", () =>
    {
        foreach (ulong size in new[] { ulong.MaxValue, (ulong)long.MaxValue, (ulong)long.MaxValue - 63 })
        {
            var bytes = Cia(); Put64(bytes, 0x18, size);
            var report = Inspect("overflow.cia", bytes);
            Check(!report.IsStructurallyValid);
            _ = report.ToJson(); _ = ContainerInspectionFormatter.Format(report);
        }
    });
    await TestSync("NCCH hostile exponent and media sums yield diagnostics", () =>
    {
        var bytes = Ncch(); bytes[0x18e] = 255;
        Check(!Inspect("bad-unit.ncch", bytes).IsStructurallyValid);
        var ncsd = Ncsd(); ncsd[0x18e] = 22;
        Put32(ncsd, 0x120, uint.MaxValue); Put32(ncsd, 0x124, uint.MaxValue);
        var report = Inspect("sum-overflow.3ds", ncsd);
        Check(!report.IsStructurallyValid); _ = report.ToJson();
    });
    await TestSync("NCCH region outside declared NCCH is rejected even inside file", () =>
    {
        var bytes = Ncch(); Put32(bytes, 0x104, 2);
        Check(!Inspect("parent-bounds.ncch", bytes).IsStructurallyValid);
    });
    await TestSync("NCCH ExeFS and RomFS overlap is rejected", () =>
    {
        var bytes = Ncch(); Put32(bytes, 0x1b0, 2);
        Check(!Inspect("region-overlap.ncch", bytes).IsStructurallyValid);
    });
    await TestSync("ExeFS entry cannot point outside ExeFS", () =>
    {
        var bytes = Ncch(); Put32(bytes, 0x208, uint.MaxValue);
        Check(!Inspect("bad-entry.ncch", bytes).IsStructurallyValid);
    });
    await TestSync("ExeFS duplicate/overlapping entries are rejected", () =>
    {
        var bytes = Ncch(); Array.Copy(bytes, 0x200, bytes, 0x210, 16);
        Check(!Inspect("duplicate.ncch", bytes).IsStructurallyValid);
    });
    await TestSync("raw ExeFS file supports ten entries without NCCH", () =>
    {
        var report = Inspect("raw.exefs", Ncch()[0x200..0x600]);
        Check(report.IsStructurallyValid && report.ExeFs!.Entries.Count == 10);
    });
    await TestSync("unknown extension cannot override a recognized header", () =>
    {
        Check(Inspect("image.bin", Ncch()).Kind == "NCCH");
        Check(Inspect("archive.bin", Cia()).Kind == "CIA");
        Check(!Inspect("fake.cxi", new byte[0x400]).HeaderRecognized);
    });
    await TestSync("zero/short files and partial NCCH headers are rejected", () =>
    {
        foreach (int length in new[] { 0, 1, 0x20, 0x103, 0x104, 0x1ff })
            Check(!Inspect("short.ncch", Ncch()[..length]).IsStructurallyValid);
    });
    await Test("inspection with hash leaves original bytes and modification time unchanged", async () =>
    {
        string path = Save("immutable.ncch", Ncch());
        var before = File.ReadAllBytes(path); var modified = File.GetLastWriteTimeUtc(path);
        await inspector.InspectAsync(path, true);
        Check(before.SequenceEqual(File.ReadAllBytes(path)) && File.GetLastWriteTimeUtc(path) == modified);
    });
    await Test("JSON export is parseable and includes matching SHA-256", async () =>
    {
        var report = inspector.Inspect(Save("export.ncch", Ncch()), true);
        string output = Path.Combine(directory, "report.json");
        await InspectionJsonExporter.ExportAsync(report, output);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(output));
        Check(json.RootElement.GetProperty("sha256").GetString() == report.Sha256);
    });
    await Test("JSON export refuses existing source, report, and symlink targets", async () =>
    {
        string source = Save("source.json", Ncch());
        var report = inspector.Inspect(source);
        await Throws<IOException>(() => InspectionJsonExporter.ExportAsync(report, source));
        string existing = Save("existing.json", Encoding.UTF8.GetBytes("keep"));
        await Throws<IOException>(() => InspectionJsonExporter.ExportAsync(report, existing));
        string link = Path.Combine(directory, "alias.json");
        File.CreateSymbolicLink(link, source);
        await Throws<IOException>(() => InspectionJsonExporter.ExportAsync(report, link));
        Check(File.ReadAllText(existing) == "keep" && File.ReadAllBytes(source).SequenceEqual(Ncch()));
    });
    await Test("cancellation does not publish partial JSON", async () =>
    {
        using var ct = new CancellationTokenSource(); ct.Cancel();
        var report = Inspect("cancel.ncch", Ncch());
        await Throws<OperationCanceledException>(() => inspector.InspectAsync(report.FilePath, true, ct.Token));
        string output = Path.Combine(directory, "cancel.json");
        await Throws<OperationCanceledException>(() => InspectionJsonExporter.ExportAsync(report, output, ct.Token));
        Check(!File.Exists(output) && !Directory.EnumerateFiles(directory, ".romforge-*.tmp").Any());
    });
    await TestSync("bounded parser handles 1200 deterministic mutated headers and JSON formatting", () =>
    {
        var random = new Random(703);
        for (int i = 0; i < 1200; i++)
        {
            byte[] bytes = i % 3 == 0 ? Ncch() : i % 3 == 1 ? Ncsd() : Cia();
            for (int j = 0; j < 8; j++) bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            string ext = i % 3 == 2 ? ".cia" : ".bin";
            var report = Inspect("fuzz" + ext, bytes);
            _ = report.ToJson(); _ = ContainerInspectionFormatter.Format(report);
        }
    });
    Console.WriteLine($"PASS {passed} tests");
}
finally { Directory.Delete(directory, true); }

ContainerInspection Inspect(string name, byte[] bytes) => inspector.Inspect(Save(name, bytes));
string Save(string name, byte[] bytes) { var path = Path.Combine(directory, name); File.WriteAllBytes(path, bytes); return path; }
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
async Task Test(string name, Func<Task> body) { await body(); passed++; Console.WriteLine($"PASS {name}"); }
Task TestSync(string name, Action body) => Test(name, () => { body(); return Task.CompletedTask; });
async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
static void Put32(byte[] b, int p, uint n) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p, 4), n);
static void Put64(byte[] b, int p, ulong n) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(p, 8), n);
static byte[] Ncch()
{
    var b = new byte[0x800];
    "NCCH"u8.CopyTo(b.AsSpan(0x100)); Put32(b, 0x104, 4);
    Put64(b, 0x108, 0x0004000001234567); Put64(b, 0x118, 0x0004000001234567);
    b[0x112] = 2; b[0x18f] = 4; "CTR-P-TEST"u8.CopyTo(b.AsSpan(0x150));
    Put32(b, 0x1a0, 1); Put32(b, 0x1a4, 2); Put32(b, 0x1b0, 3); Put32(b, 0x1b4, 1);
    for (int i = 0; i < 10; i++)
    {
        Encoding.ASCII.GetBytes($"f{i}").CopyTo(b, 0x200 + i * 16);
        Put32(b, 0x208 + i * 16, (uint)i * 16); Put32(b, 0x20c + i * 16, 16);
        b.AsSpan(0x200 + 0x1e0 - i * 32, 32).Fill((byte)((i + 1) * 17));
    }
    return b;
}
static byte[] Ncsd()
{
    var b = new byte[0x1200]; "NCSD"u8.CopyTo(b.AsSpan(0x100)); Put32(b, 0x104, 9);
    Put32(b, 0x120, 1); Put32(b, 0x124, 4); Put32(b, 0x158, 5); Put32(b, 0x15c, 4);
    Ncch().CopyTo(b, 0x200); Ncch().CopyTo(b, 0xa00);
    return b;
}
static byte[] Cia()
{
    var b = new byte[0x2200]; Put32(b, 0, 0x2020);
    Put32(b, 8, 3); Put32(b, 0xc, 5); Put32(b, 0x10, 65); Put32(b, 0x14, 7); Put64(b, 0x18, 65);
    return b;
}
