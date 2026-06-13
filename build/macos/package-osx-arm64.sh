#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET="${DOTNET:-/opt/homebrew/opt/dotnet@8/libexec/dotnet}"
VERSION="${VERSION:-0.1.0}"
RID="${RID:-osx-arm64}"

PUBLISH_DIR="$ROOT_DIR/artifacts/$RID"
BUILD_DIR="$ROOT_DIR/src/DelimPlot.App/bin/Release/net8.0/$RID"
RELEASE_DIR="$ROOT_DIR/artifacts/release"
APP_DIR="$RELEASE_DIR/DelimPlot.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"
RESOURCES_DIR="$APP_DIR/Contents/Resources"

"$DOTNET" publish "$ROOT_DIR/src/DelimPlot.App/DelimPlot.App.csproj" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --disable-build-servers \
  -maxcpucount:1 \
  -o "$PUBLISH_DIR"

rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -p "$PUBLISH_DIR/DelimPlot" "$MACOS_DIR/"
for dylib in libAvaloniaNative.dylib libHarfBuzzSharp.dylib libSkiaSharp.dylib; do
  if [[ -e "$PUBLISH_DIR/$dylib" ]]; then
    cp -p "$PUBLISH_DIR/$dylib" "$MACOS_DIR/"
  elif [[ -e "$BUILD_DIR/$dylib" ]]; then
    cp -p "$BUILD_DIR/$dylib" "$MACOS_DIR/"
  else
    echo "Missing required native library: $dylib" >&2
    exit 1
  fi
done
cp -p "$ROOT_DIR/build/macos/Info.plist" "$APP_DIR/Contents/Info.plist"
cp -p "$ROOT_DIR/assets/DelimPlot.icns" "$RESOURCES_DIR/DelimPlot.icns"

plutil -lint "$APP_DIR/Contents/Info.plist"
codesign --force --deep --sign - "$APP_DIR"

ditto -c -k --norsrc --keepParent "$APP_DIR" "$RELEASE_DIR/DelimPlot-$VERSION-$RID.zip"
rm -f "$RELEASE_DIR/DelimPlot-latest-$RID.zip"
