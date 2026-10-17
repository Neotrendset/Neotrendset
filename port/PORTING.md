# Port provenance and build notes

Modified 2026-10-18 for an unofficial macOS port. Upstream v1.9.1 commit `1da80db6d558f0be95509393f3099fd189708319`; source https://github.com/sinjunyoung/RomForge/tree/v1.9.1 . Preserve the upstream LICENSE (GNU GPL version 2) and all component notices; this file does not relicense third-party components.

## Changes

- Keep original managed engines and ViewModels; retarget reusable projects to net8.0.
- Add RomForge.Mac Avalonia UI and Portable.Desktop (storage pickers, dialogs, dispatching, image/clipboard/audio adapters). Exclude Windows-only WPF controls/Win32 hooks from the Mac build.
- Add workflow orchestration for original code-behind actions, Switch metadata/bulk patching, archive selection, GD-ROM layout selection, and original parent patch execution/cancellation.
- Use SharpCompress for actual ZIP/7z reading rather than Windows 7z.dll. Use SkiaSharp for raster operations.
- Invoke bundled macOS chdman; load macOS libchdr, libxdelta3 and libdeflate through NativeTools resolvers. Keep command arguments separate and cancel child processes.
- Use diskutil and Darwin block ioctls for external physical drives. The C bridge in native-source/romforge_native.c handles the Darwin arm64 variadic ioctl ABI and is tested with a synthetic pipe. Unmount before image writes, remount afterwards. No drive-letter assignment on macOS.
- Store mutable data and keys outside the app bundle. Import user keys only. Restore v0.7 read-only inspector as an additional tool.
- Store managed payload under Contents/Resources, with a compiled native launcher under Contents/MacOS; sign Mach-O files before sealing the app. No Developer ID or notarization.

## Native origins

The pinned Homebrew bottle URLs, SHA-256 and source URLs are in native-provenance.json. Formula snapshots and licenses are in licenses/native-*. Includes rom-tools 0.289 chdman, FLAC 1.5.0, libogg 1.3.6, zstd 1.5.7, xz 5.8.4, utf8proc 2.12.0, SDL3 3.4.18. Sequoia binaries set the distribution minimum to macOS 15.0.

libchdr 0.3.0: https://github.com/rtissera/libchdr/releases/tag/v0.3.0 . Its exact source archive is in native-source/libchdr.tar.gz. Build shared library using CMake, Release, CMAKE_OSX_DEPLOYMENT_TARGET=12.0; older vendored CMake requires CMAKE_POLICY_VERSION_MINIMUM=3.5.

xdelta3 v3.1.0: https://github.com/jmacd/xdelta/tree/v3.1.0 . Exact source archive and upstream RomForge wrapper are in native-source. Compile xdelta3.c and xdelta3_wrapper.c with clang -O2 -fPIC -dynamiclib, XD3_ENCODER=1, SECONDARY_LZMA=1, SECONDARY_DJW=1, XD3_USE_LARGEFILE64=1, XD3_STDIO=1, SIZEOF_SIZE_T=8, SIZEOF_USIZE_T=8, SIZEOF_XOFF_T=8; link the pinned xz liblzma. Preserve its COPYING notices.

libdeflate: universal macOS dylib from NuGet LibDeflate.Native 1.19.0. Package metadata/license inventory in licenses/nuget. macOS dylibs use @loader_path dependency references and are ad-hoc signed. NuGet supplies BLAKE3, SkiaSharp, Avalonia and the self-contained .NET runtime native libraries.

## Recovered build resources

Upstream source archive omitted build resources required by 3DS.Core and PBP.Core. The exact embedded byte arrays were extracted with PEReader/MetadataReader/ResourceReader from the author's official `RomForge_Dlls.zip` release asset for v1.9.1; upstream assemblies were not executed by the extraction tool. They include custom-install-finalize.3dsx and the PBP base/config/data/images/GamesDB resources. Resource hashes and release metadata are recorded beside this document. No console key or commercial game image was added.

## Tests

port/InspectorTests: 26 synthetic parser/export tests including 1,200 mutated headers. port/SmokeTests: all workflow forms; GUI bindings/parent patch orchestration; real managed/native engine roundtrips with synthetic data. The encrypted-solid.7z fixture was generated with official 7-Zip 26.04, password `fixture-password`, using two synthetic files; 7-Zip itself is not shipped or required.

Original Windows solution and batch files are preserved as reference. Build the Mac-specific solution or project shown in README instead.
