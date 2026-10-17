# 검증 결과 — RomForge-Mac 1.9.1-mac.1

검증일: 2026-10-18 (한국 시간). 기준: RomForge v1.9.1 / 커밋 `1da80db6d558f0be95509393f3099fd189708319`.

| 항목 | 결과 |
|---|---|
| 제공 소스의 Release 빌드 | 성공, 오류 0. Nullable·async·RID·module initializer 관련 경고는 남아 있음 |
| 기능 화면 생성 | 31개 모두 성공 |
| 화면 입력·명령 연결 | 2개 성공: Inspector 입력/실행/JSON 버튼 갱신, 일반 패치의 상위 실행 제어와 출력 데이터 비교 |
| 관리형·네이티브 엔진 검사 | 18개 모두 성공 |
| Inspector·JSON 검사 | 26개 모두 성공, 결정적 변형 헤더 1,200개 포함 |
| 실제 macOS 창 | 실행·NCCH ID/SHA-256 분석·JSON 저장과 해시 일치 확인 |
| 네이티브 도구 | 22개 서명 및 상대 라이브러리 연결 확인 |

## 엔진 검사

- Darwin arm64 ioctl bridge uses correct variadic ABI on synthetic pipe
- IPS patch uses upstream UniversalPatcher
- native xdelta3 encode/decode roundtrip
- native BLAKE3 standard empty digest
- 7z compatibility reader extracts ZIP entries and matches bytes
- encrypted solid 7z reads multiple entries with managed decoder
- PSP ISO → CSO → ISO roundtrip uses native libdeflate
- PSP ISO → ZSO → ISO LZ4 roundtrip
- 3DS ExeFS pack/unpack validates stored SHA-256
- 3DS RomFS folder pack/unpack preserves nested UTF-8 names and data
- 3DS NCSD → ZCCI → NCSD roundtrip
- GameCube ISO → RVZ → ISO roundtrip
- CHD native creation, libchdr read and extraction roundtrip
- Switch PFS0 builder writes table and payload with correct alignment
- Wii U WUA archive write/read preserves payload
- PS/Vita PARAM.SFO metadata interoperability
- PBP pack/unpack uses recovered official base resources
- Portable raster image PNG roundtrip and resize

## 검증 범위

합성 데이터로 패치·압축·파일 구조 생성과 재읽기를 비교했습니다. 상용 ROM·사용자 콘솔 키를 사용하지 않았고 실제 SD 설치/복구, 외장 디스크 쓰기, 모든 암호화 게임의 재패킹, 외부 표지·티스토리 다운로드는 수행하지 않았습니다. 위 기능의 원본 엔진과 macOS 실행 경로를 연결했지만 실물 성공으로 표시하지 않습니다.

실행 검증 환경은 Apple Silicon, macOS 27.0.1, .NET SDK 8.0.426 / runtime 8.0.32입니다. 배포 최소 버전 macOS 15는 동봉 네이티브 라이브러리의 요구 사항에 따른 것으로 macOS 15 실기 검증을 의미하지 않습니다. Intel Mac 실행 파일은 포함하지 않습니다.

Apple Developer ID 서명·공증은 없으며 ad-hoc 서명으로 무결성을 검사합니다. 원본 WPF의 화면 장식·작업 표시줄·드라이브 문자는 macOS 대응 UI와 마운트 경로로 바뀌었습니다.

원시 로그는 `port/validation`에 포함합니다. 기능별 연결 및 미검증 항목은 `FEATURES.md`를 보세요.
