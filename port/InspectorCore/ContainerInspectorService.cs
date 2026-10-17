using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RomForge.Mac.Core;

/// <summary>Bounded, read-only container metadata inspection. No decryption or extraction.</summary>
public sealed class ContainerInspectorService
{
    public Task<ContainerInspection> InspectAsync(string path, bool includeSha256 = false,
        CancellationToken ct = default) => Task.Run(() => Inspect(path, includeSha256, ct), ct);

    public ContainerInspection Inspect(string path, bool includeSha256 = false,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var report = new ContainerInspection
        {
            FilePath = fullPath, Size = stream.Length,
            Extension = Path.GetExtension(fullPath).ToLowerInvariant()
        };
        var reader = new Reader(stream, report, ct);
        var prefix = reader.Read(0, (int)Math.Min(0x200, stream.Length));
        try
        {
            if (prefix.Length >= 0x104 && prefix.AsSpan(0x100, 4).SequenceEqual("NCSD"u8))
            {
                report.Kind = "NCSD";
                if (prefix.Length < 0x200) reader.Error("NCSD", "헤더가 잘렸습니다 (최소 0x200 bytes).");
                else { report.HeaderRecognized = true; ReadNcsd(reader, prefix); }
            }
            else if (prefix.Length >= 0x104 && prefix.AsSpan(0x100, 4).SequenceEqual("NCCH"u8))
            {
                report.Kind = "NCCH";
                if (prefix.Length < 0x200) reader.Error("NCCH", "헤더가 잘렸습니다 (최소 0x200 bytes).");
                else
                {
                    report.HeaderRecognized = true;
                    report.Ncch = ReadNcch(reader, 0, stream.Length);
                }
            }
            // CIA has no magic. A non-.cia input must match the standard header size.
            else if (report.Extension == ".cia" || (prefix.Length >= 0x20 && U32(prefix, 0) == 0x2020))
            {
                report.Kind = "CIA";
                ReadCia(reader, prefix);
            }
            else if (report.Extension == ".exefs")
            {
                report.Kind = "ExeFS";
                var region = new ContainerRegion("ExeFS", 0, stream.Length, true);
                report.ExeFs = ReadExeFs(reader, region);
                report.HeaderRecognized = report.ExeFs.HeaderRead;
                reader.Warn("ExeFS", "Raw ExeFS에는 magic이 없습니다. .exefs 확장자와 엔트리 구조를 기준으로 검사했습니다.");
            }
            else reader.Error("File", "지원하는 컨테이너 헤더를 찾지 못했습니다. 확장자만으로 형식을 확정하지 않습니다.");
        }
        catch (OverflowException)
        {
            reader.Error(report.Kind, "헤더의 크기 또는 오프셋 계산이 64-bit 범위를 초과합니다.");
        }
        if (includeSha256)
        {
            stream.Position = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, count);
            }
            ct.ThrowIfCancellationRequested();
            report.Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        return report;
    }

    private static void ReadNcsd(Reader r, byte[] h)
    {
        long unit = MediaUnit(h[0x18e]);
        r.Report.MediaUnitSize = unit;
        long declared = checked(U32(h, 0x104) * unit);
        r.Report.DeclaredSize = declared;
        if (declared < 0x200) r.Error("NCSD", "선언된 이미지 크기가 헤더보다 작습니다.");
        if (declared > r.Length) r.Warn("NCSD", "선언된 이미지 크기가 파일보다 큽니다. 트리밍 또는 잘림 여부를 확인하세요.");
        if (declared < r.Length) r.Warn("NCSD", "선언된 이미지 크기 뒤에 추가 데이터가 있습니다.");
        for (int i = 0; i < 8; i++)
        {
            r.CheckCancellation();
            long offset = checked(U32(h, 0x120 + i * 8) * unit);
            long size = checked(U32(h, 0x124 + i * 8) * unit);
            bool present = offset != 0 || size != 0;
            bool valid = !present || (offset >= 0x200 && size > 0 &&
                Fits(offset, size, r.Length) && Fits(offset, size, declared));
            _ = checked(offset + size);
            var region = new ContainerRegion($"Partition {i}", offset, size, valid);
            if (!valid) r.Error(region.Name, "파티션 범위가 파일/선언된 이미지 크기를 벗어나거나 헤더와 겹칩니다.");
            if (present && valid)
            {
                foreach (var other in r.Report.Partitions.Where(x => x.IsPresent && x.Region.IsWithinBounds))
                    if (Overlaps(region, other.Region)) r.Error(region.Name, $"Partition {other.Index}와 겹칩니다.");
            }
            NcchInspection? ncch = null;
            if (present && valid)
            {
                if (size >= 0x200)
                {
                    var magic = r.Read(checked(offset + 0x100), 4);
                    if (magic.AsSpan().SequenceEqual("NCCH"u8)) ncch = ReadNcch(r, offset, size);
                    else r.Warn(region.Name, "NCCH 헤더가 아닙니다. 파티션의 파일 시스템을 분석하지 않았습니다.");
                }
                else r.Error(region.Name, "NCCH 헤더를 담기에 너무 작습니다.");
            }
            r.Report.Partitions.Add(new(i, present, h[0x110 + i], h[0x118 + i], region, ncch));
        }
    }

    private static NcchInspection ReadNcch(Reader r, long offset, long available)
    {
        var h = r.Read(offset, 0x200);
        long unit = MediaUnit(h[0x18e]);
        long size = checked(U32(h, 0x104) * unit);
        var location = $"NCCH @ 0x{offset:X}";
        if (size < 0x200 || size > available) r.Error(location, "선언된 NCCH 크기가 헤더/부모 컨테이너 범위를 벗어납니다.");
        long limit = Math.Min(size, available);
        var exefs = NcchRegion(r, h, offset, limit, unit, 0x1a0, "ExeFS");
        var romfs = NcchRegion(r, h, offset, limit, unit, 0x1b0, "RomFS");
        if (exefs.Size > 0 && romfs.Size > 0 && Overlaps(exefs, romfs))
            r.Error(location, "ExeFS와 RomFS 영역이 겹칩니다.");
        bool encrypted = (h[0x18f] & 4) == 0;
        ExeFsInspection? details = null;
        if (exefs.Size > 0 && exefs.IsWithinBounds)
        {
            details = encrypted
                ? new(exefs, false, Array.Empty<ExeFsEntry>(), "암호화 플래그 설정: ExeFS 엔트리 분석을 건너뛰었습니다.")
                : ReadExeFs(r, exefs);
        }
        return new(offset, U64(h, 0x108).ToString("X16"), U64(h, 0x118).ToString("X16"),
            Text(h.AsSpan(0x150, 16)), U16(h, 0x112), unit, size, encrypted, exefs, romfs, details);
    }

    private static ContainerRegion NcchRegion(Reader r, byte[] h, long parentOffset,
        long parentSize, long unit, int field, string name)
    {
        long relative = checked(U32(h, field) * unit);
        long size = checked(U32(h, field + 4) * unit);
        long absolute = checked(parentOffset + relative);
        bool absent = relative == 0 && size == 0;
        bool valid = absent || (relative >= 0x200 && size > 0 && Fits(relative, size, parentSize) && Fits(absolute, size, r.Length));
        _ = checked(absolute + size);
        if (!valid) r.Error($"{name} @ 0x{parentOffset:X}", "영역이 NCCH/파일 범위를 벗어나거나 헤더와 겹칩니다.");
        return new(name, absolute, size, valid);
    }

    private static ExeFsInspection ReadExeFs(Reader r, ContainerRegion region)
    {
        var entries = new List<ExeFsEntry>();
        if (!region.IsWithinBounds || region.Size < 0x200)
        {
            r.Error(region.Name, "ExeFS 헤더가 잘렸습니다 (최소 0x200 bytes).");
            return new(region, false, entries, "잘린 헤더");
        }
        var h = r.Read(region.Offset, 0x200);
        for (int i = 0; i < 10; i++)
        {
            r.CheckCancellation();
            var raw = h.AsSpan(i * 16, 16);
            if (raw.IndexOfAnyExcept((byte)0) < 0) continue;
            string name = Text(raw[..8]);
            long relative = U32(h, i * 16 + 8);
            long size = U32(h, i * 16 + 12);
            long dataOffset = checked(0x200 + relative);
            long absolute = checked(region.Offset + dataOffset);
            bool valid = Fits(dataOffset, size, region.Size) && Fits(absolute, size, r.Length);
            if (string.IsNullOrEmpty(name)) r.Error("ExeFS", $"엔트리 {i}의 파일명이 비어 있습니다.");
            if (raw[..8].ToArray().Any(b => b != 0 && (b < 0x20 || b > 0x7e)))
                r.Error("ExeFS", $"엔트리 {i}에 올바르지 않은 파일명 바이트가 있습니다.");
            var data = new ContainerRegion(name, absolute, size, valid);
            if (!valid) r.Error("ExeFS", $"엔트리 {i} ({name})가 ExeFS 범위를 벗어납니다.");
            foreach (var other in entries)
            {
                if (name == other.Name) r.Error("ExeFS", $"중복 파일명: {name}");
                if (size > 0 && other.Data.Size > 0 && Overlaps(data, other.Data))
                    r.Error("ExeFS", $"{name}과 {other.Name}의 데이터가 겹칩니다.");
            }
            // Hash slots are reversed: entry 0's hash is at 0x1e0.
            string hash = Convert.ToHexString(h.AsSpan(0x1e0 - i * 32, 32)).ToLowerInvariant();
            entries.Add(new(i, name, relative, data, hash));
        }
        return new(region, true, entries, "저장된 엔트리 SHA-256을 표시합니다. 엔트리 데이터 해시 검증은 수행하지 않습니다.");
    }

    private static void ReadCia(Reader r, byte[] h)
    {
        if (h.Length < 0x20) { r.Error("CIA", "CIA 고정 헤더가 잘렸습니다."); return; }
        long headerSize = U32(h, 0);
        if (headerSize < 0x2020 || !Fits(0, headerSize, r.Length))
        {
            r.Error("CIA", "CIA 헤더/Content Index가 없거나 잘렸습니다 (최소 0x2020 bytes).");
            return;
        }
        r.Report.HeaderRecognized = true;
        if (headerSize != 0x2020) r.Warn("CIA", "표준 0x2020과 다른 CIA 헤더 크기입니다.");
        if (U16(h, 4) != 0 || U16(h, 6) != 0) r.Warn("CIA", "비표준 Type/Version: 섹션 배치만 확인했습니다.");
        var sections = new List<ContainerRegion>();
        void Add(string name, long offset, long size)
        {
            _ = checked(offset + size);
            bool valid = size == 0 || Fits(offset, size, r.Length);
            sections.Add(new(name, offset, size, valid));
            if (!valid && size > 0) r.Error($"CIA {name}", "섹션이 파일 범위를 벗어납니다.");
        }
        Add("Header", 0, headerSize);
        long cert = Align64(headerSize);
        Add("CertificateChain", cert, U32(h, 8));
        long ticket = Align64(checked(cert + U32(h, 8)));
        Add("Ticket", ticket, U32(h, 0x0c));
        long tmd = Align64(checked(ticket + U32(h, 0x0c)));
        Add("TMD", tmd, U32(h, 0x10));
        long content = Align64(checked(tmd + U32(h, 0x10)));
        ulong rawContentSize = U64(h, 0x18);
        if (rawContentSize > long.MaxValue) throw new OverflowException();
        long contentSize = (long)rawContentSize;
        Add("Content", content, contentSize);
        long meta = Align64(checked(content + contentSize));
        Add("Meta", meta, U32(h, 0x14));
        r.Report.Cia = new(U16(h, 4), U16(h, 6), sections);
        if (U32(h, 0x0c) == 0 || U32(h, 0x10) == 0) r.Error("CIA", "필수 Ticket 또는 TMD 섹션이 비어 있습니다.");
        r.Warn("CIA", "섹션 위치만 분석했습니다. Ticket/TMD 서명, 콘텐츠 목록 및 암호화된 데이터는 검증하지 않았습니다.");
    }

    private static long MediaUnit(byte exponent)
    {
        if (exponent > 22) throw new OverflowException();
        return 0x200L << exponent;
    }
    private static long Align64(long value) => checked(value + 63) & ~63L;
    private static bool Fits(long offset, long size, long length) => offset >= 0 && size >= 0 && offset <= length && size <= length - offset;
    private static bool Overlaps(ContainerRegion a, ContainerRegion b) => a.Offset < b.EndExclusive && b.Offset < a.EndExclusive;
    private static ushort U16(byte[] b, int p) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p, 2));
    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p, 4));
    private static ulong U64(byte[] b, int p) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(p, 8));
    private static string Text(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        if (end >= 0) bytes = bytes[..end];
        return Encoding.ASCII.GetString(bytes);
    }

    private sealed class Reader(FileStream stream, ContainerInspection report, CancellationToken ct)
    {
        public ContainerInspection Report => report;
        public long Length => stream.Length;
        public void CheckCancellation() => ct.ThrowIfCancellationRequested();
        public byte[] Read(long offset, int size)
        {
            CheckCancellation();
            if (!Fits(offset, size, Length)) throw new InvalidDataException("읽기 범위가 파일을 벗어납니다.");
            var data = new byte[size];
            stream.Position = offset;
            stream.ReadExactly(data);
            return data;
        }
        public void Error(string location, string message) => report.Issues.Add(new("Error", location, message));
        public void Warn(string location, string message) => report.Issues.Add(new("Warning", location, message));
    }
}
