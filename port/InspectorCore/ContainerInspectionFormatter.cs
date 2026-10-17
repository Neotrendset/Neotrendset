using System.Text;

namespace RomForge.Mac.Core;

public static class ContainerInspectionFormatter
{
    public static string Format(ContainerInspection report)
    {
        var text = new StringBuilder();
        text.AppendLine($"파일: {report.FilePath}");
        text.AppendLine($"형식: {report.Kind}  |  크기: {report.Size:N0} bytes");
        text.AppendLine($"헤더 인식: {report.HeaderRecognized}  |  구조 검사: {(report.IsStructurallyValid ? "오류 없음" : "오류 있음/미지원")}");
        text.AppendLine("모든 위치는 파일 시작 기준 절대 오프셋입니다. 서명/콘텐츠 무결성 인증 결과가 아닙니다.");
        if (report.DeclaredSize is { } size) text.AppendLine($"선언된 크기: {size:N0} bytes / media unit: {report.MediaUnitSize}");
        if (report.Sha256 is { } hash) text.AppendLine($"파일 SHA-256: {hash}");
        else text.AppendLine("파일 SHA-256: 미계산 (SHA-256 포함 검사 버튼 사용)");
        foreach (var p in report.Partitions)
        {
            text.AppendLine($"\n[NCSD 파티션 {p.Index}] {(p.IsPresent ? "사용" : "비어 있음")} / FS={p.FileSystemType} / Crypto={p.CryptoType}");
            Region(p.Region);
            if (p.Ncch is { } ncch) Ncch(ncch);
        }
        if (report.Ncch is { } standalone) Ncch(standalone);
        if (report.Cia is { } cia)
        {
            text.AppendLine($"\n[CIA] Type={cia.Type}, Version={cia.Version}");
            foreach (var section in cia.Sections) Region(section);
        }
        if (report.ExeFs is { } exefs) ExeFs(exefs);
        foreach (var issue in report.Issues) text.AppendLine($"\n[{issue.Severity}] {issue.Location}: {issue.Message}");
        return text.ToString();

        void Region(ContainerRegion r) => text.AppendLine($"  {r.Name}: 0x{r.Offset:X} .. 0x{r.EndExclusive:X} (끝 제외), {r.Size:N0} bytes{(r.IsWithinBounds ? "" : " [범위 오류]")}");
        void Ncch(NcchInspection n)
        {
            text.AppendLine($"\n[NCCH @ 0x{n.Offset:X}] v{n.Version} / media unit={n.MediaUnitSize}");
            text.AppendLine($"  Program ID: {n.ProgramId}\n  Partition ID: {n.PartitionId}\n  Product Code: {n.ProductCode}");
            text.AppendLine($"  선언된 크기: {n.DeclaredSize:N0} / 암호화 플래그: {n.IsEncrypted}");
            Region(n.ExeFs); Region(n.RomFs);
            if (n.ExeFsDetails is { } details) ExeFs(details);
        }
        void ExeFs(ExeFsInspection e)
        {
            text.AppendLine($"  ExeFS 엔트리: {e.Entries.Count}/10 — {e.Note}");
            foreach (var entry in e.Entries)
            {
                text.AppendLine($"    #{entry.Index} {entry.Name} / 상대 위치 0x{entry.RelativeOffset:X}");
                Region(entry.Data);
                text.AppendLine($"    저장된 SHA-256 (미검증): {entry.StoredSha256}");
            }
        }
    }
}
