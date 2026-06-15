#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET="${DOTNET:-dotnet}"
RID="${RID:-linux-x64}"
APPIMAGETOOL="${APPIMAGETOOL:-appimagetool}"
SKIP_APPIMAGE="${SKIP_APPIMAGE:-false}"

if ! command -v "$DOTNET" >/dev/null 2>&1; then
  echo "Missing dotnet SDK. Install .NET 8 SDK or set DOTNET=/path/to/dotnet." >&2
  exit 1
fi

VERSION="${VERSION:-}"
if [[ -z "$VERSION" ]]; then
  VERSION="$("$DOTNET" msbuild "$ROOT_DIR/src/DelimPlot.App/DelimPlot.App.csproj" -getProperty:Version 2>/dev/null || true)"
fi
VERSION="${VERSION:-0.1.1}"

PUBLISH_DIR="$ROOT_DIR/artifacts/$RID"
RELEASE_DIR="$ROOT_DIR/artifacts/release"
APPDIR="$ROOT_DIR/artifacts/appimage/DelimPlot.AppDir"
RELEASE_APP_DIR="$RELEASE_DIR/DelimPlot-$VERSION-$RID"
VERSIONED_TGZ="$RELEASE_DIR/DelimPlot-$VERSION-$RID.tar.gz"
VERSIONED_APPIMAGE="$RELEASE_DIR/DelimPlot-$VERSION-$RID.AppImage"
LATEST_APPIMAGE="$RELEASE_DIR/DelimPlot-latest-$RID.AppImage"
NUGET_PACKAGES_DIR="${NUGET_PACKAGES:-$HOME/.nuget/packages}"

rm -rf "$PUBLISH_DIR" "$APPDIR" "$RELEASE_APP_DIR"
rm -f \
  "$RELEASE_DIR/DelimPlot-$VERSION-$RID" \
  "$RELEASE_DIR/DelimPlot-latest-$RID" \
  "$VERSIONED_TGZ" \
  "$RELEASE_DIR/DelimPlot-latest-$RID.tar.gz" \
  "$VERSIONED_APPIMAGE" \
  "$LATEST_APPIMAGE"

"$DOTNET" publish "$ROOT_DIR/src/DelimPlot.App/DelimPlot.App.csproj" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishReadyToRun=false \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  --disable-build-servers \
  -maxcpucount:1 \
  -o "$PUBLISH_DIR"

SKIA_NATIVE="$NUGET_PACKAGES_DIR/skiasharp.nativeassets.linux.nodependencies/3.119.0/runtimes/$RID/native/libSkiaSharp.so"
if [[ ! -f "$SKIA_NATIVE" ]]; then
  SKIA_NATIVE="$(find "$NUGET_PACKAGES_DIR/skiasharp.nativeassets.linux.nodependencies" -path "*/runtimes/$RID/native/libSkiaSharp.so" -type f | sort -V | tail -n 1)"
fi
if [[ -z "$SKIA_NATIVE" || ! -f "$SKIA_NATIVE" ]]; then
  echo "Missing SkiaSharp.NativeAssets.Linux.NoDependencies native library for $RID." >&2
  exit 1
fi
cp -p "$SKIA_NATIVE" "$PUBLISH_DIR/libSkiaSharp.so"

mkdir -p "$RELEASE_DIR"
cp -a "$PUBLISH_DIR" "$RELEASE_APP_DIR"
tar -C "$RELEASE_DIR" -czf "$VERSIONED_TGZ" "DelimPlot-$VERSION-$RID"

if [[ "$SKIP_APPIMAGE" != "true" ]]; then
  if ! command -v "$APPIMAGETOOL" >/dev/null 2>&1; then
    echo "Missing appimagetool. Install it or set APPIMAGETOOL=/path/to/appimagetool-x86_64.AppImage." >&2
    echo "Set SKIP_APPIMAGE=true to build only the portable tar.gz bundle." >&2
    exit 1
  fi

  mkdir -p \
    "$APPDIR/usr/bin" \
    "$APPDIR/usr/share/applications" \
    "$APPDIR/usr/share/icons/hicolor/256x256/apps"
  cp -a "$PUBLISH_DIR/." "$APPDIR/usr/bin/"
  cp -p "$ROOT_DIR/assets/Icon.png" "$APPDIR/delimplot.png"
  cp -p "$ROOT_DIR/assets/Icon.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/delimplot.png"

  cat > "$APPDIR/delimplot.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=DelimPlot
Comment=Plot columns from plain-text data.
Exec=DelimPlot %F
Icon=delimplot
Categories=Science;DataVisualization;
Terminal=false
MimeType=application/x-delimplot;
DESKTOP
  cp -p "$APPDIR/delimplot.desktop" "$APPDIR/usr/share/applications/delimplot.desktop"

  cat > "$APPDIR/AppRun" <<'APPRUN'
#!/usr/bin/env bash
set -euo pipefail

APPDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_DIR="$APPDIR/usr/bin"
export LD_LIBRARY_PATH="$BIN_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

if [[ -z "${XDG_CACHE_HOME:-}" ]]; then
  CACHE_ROOT="${HOME:-}/.cache"
  if [[ -z "$CACHE_ROOT" || ! -w "$(dirname "$CACHE_ROOT")" ]]; then
    CACHE_ROOT="${TMPDIR:-/tmp}/delimplot-cache-${USER:-user}"
  fi
  mkdir -p "$CACHE_ROOT" 2>/dev/null || true
  export XDG_CACHE_HOME="$CACHE_ROOT"
fi

exec "$BIN_DIR/DelimPlot" "$@"
APPRUN
  chmod +x "$APPDIR/AppRun"

  env ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$APPIMAGETOOL" "$APPDIR" "$VERSIONED_APPIMAGE"
  chmod 0755 "$VERSIONED_APPIMAGE"
fi

echo "Linux release outputs:"
echo "  $RELEASE_APP_DIR"
echo "  $VERSIONED_TGZ"
if [[ "$SKIP_APPIMAGE" != "true" ]]; then
  echo "  $VERSIONED_APPIMAGE"
fi
