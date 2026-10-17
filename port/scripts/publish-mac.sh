#!/bin/bash
set -euo pipefail
export MSBuildEnableWorkloadResolver=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_CLI_USE_MSBUILD_SERVER=0
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DEST="${1:-$ROOT/artifacts}"
DOTNET="${DOTNET:-dotnet}"
[ "$(uname -m)" = arm64 ] || { echo 'This distribution targets Apple Silicon.' >&2; exit 1; }
mkdir -p "$DEST"
APP="$DEST/RomForge-Mac.app"
[ ! -e "$APP" ] || { echo "Output already exists: $APP" >&2; exit 1; }
if [ "${ROMFORGE_SKIP_PUBLISH:-0}" != 1 ]; then
"$DOTNET" publish "$ROOT/RomForge.Mac/RomForge.Mac.csproj" -c Release -r osx-arm64 --self-contained true --disable-build-servers -p:UseSharedCompilation=false -m:1 -nr:false -p:PublishTrimmed=false -p:PublishSingleFile=false -o "$DEST/publish"
fi
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
mkdir -p "$APP/Contents/Resources/payload"
cp -R "$DEST/publish/." "$APP/Contents/Resources/payload/"
clang -O2 -mmacosx-version-min=15.0 "$ROOT/port/scripts/launcher.c" -o "$APP/Contents/MacOS/RomForge-Mac"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>org.romforge.mac.port</string>
<key>CFBundleName</key><string>RomForge-Mac</string>
<key>CFBundleExecutable</key><string>RomForge-Mac</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.9.1</string>
<key>CFBundleVersion</key><string>1.9.1</string>
<key>LSMinimumSystemVersion</key><string>15.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSPrincipalClass</key><string>NSApplication</string>
<key>NSHumanReadableCopyright</key><string>RomForge contributors; macOS port under GPL-2.0</string>
</dict></plist>
PLIST
cp "$ROOT/LICENSE" "$APP/Contents/Resources/LICENSE"
[ ! -d "$ROOT/port/licenses" ] || cp -R "$ROOT/port/licenses" "$APP/Contents/Resources/"
find "$APP/Contents/Resources/payload/native" -type f -exec chmod 755 {} \;
chmod 755 "$APP/Contents/MacOS/RomForge-Mac"
while IFS= read -r -d '' binary; do
 if file "$binary" | grep -q 'Mach-O'; then codesign --force --sign - "$binary"; fi
done < <(find "$APP" -type f -print0)
xattr -d com.apple.FinderInfo "$APP" 2>/dev/null || true
codesign --force --sign - "$APP"
codesign --verify --deep --strict "$APP"
echo "$APP"
