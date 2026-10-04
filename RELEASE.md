# Release Notes

Use this checklist before publishing a GitHub release.

## 0.2.1 Release Summary

The `v0.2.1` GitHub release currently includes:

- `DelimPlot-0.2.1-win-x64.exe`
- `DelimPlot-latest-win-x64.exe`

macOS and Linux assets are pending and can be attached to this release later without recreating it.

## 0.2.1 Changes

### Added

- Linear/log scale toggle for the X axis and the left and right Y axes.
- Assign each Y series to the left or right Y axis for dual-axis plots.
- The right Y axis is labeled and only shown when at least one series uses it.

### Changed

- Project files now store axis scales and series Y axis side (project format version 2). Older projects import with linear axes and left-side series.

### Fixed

- Switching between linear and log scale now re-fits the axis limits instead of leaving the data squeezed together.
- Tick labels reset to automatic numeric ticks when switching back to linear.
- Non-positive values are clamped to 1e-13 in log mode to avoid axis range errors.

## Before Release

- Confirm `src/DelimPlot.App/DelimPlot.App.csproj` version fields are set to the release version.
- Refresh screenshots in `screenshots/`.
- Verify sample files in `samples/` load and plot correctly.
- Run a Release build.
- Publish the Windows standalone executable.
- Test opening a `.delimplot` file with `DelimPlot.exe`.
- Publish the macOS app bundle on Apple Silicon.
- Test opening the macOS `.app` directly.
- Test opening a `.delimplot` file with the macOS app bundle.
- Publish the Linux AppImage and portable tarball on Ubuntu.
- Test opening the Linux AppImage directly.
- Test opening a `.delimplot` file with the Linux AppImage.

## Build Commands

```powershell
dotnet build DelimPlot.sln --configuration Release
```

```powershell
dotnet publish src\DelimPlot.App\DelimPlot.App.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\win-x64
New-Item -ItemType Directory -Force -Path artifacts\release | Out-Null
Copy-Item artifacts\win-x64\DelimPlot.exe artifacts\release\DelimPlot-0.2.1-win-x64.exe -Force
Copy-Item artifacts\win-x64\DelimPlot.exe artifacts\release\DelimPlot-latest-win-x64.exe -Force
```

```bash
./build/macos/package-osx-arm64.sh
```

```bash
./build/linux/package-linux-x64.sh
```

## Release Artifact

The 0.2.1 release publishes Windows x64 assets. macOS and Linux packaging commands are retained below for local or future release preparation.

Attach these Windows files to the GitHub release:

```text
artifacts\release\DelimPlot-0.2.1-win-x64.exe
artifacts\release\DelimPlot-latest-win-x64.exe
```

Keep these macOS release outputs:

```text
artifacts/release/DelimPlot.app
artifacts/release/DelimPlot-0.2.1-osx-arm64.zip
```

Attach the macOS zip to the GitHub release. The `.app` bundle is kept uncompressed in `artifacts/release` for local smoke testing.

Optional Linux files, if building Linux packages for a later release:

```text
artifacts/release/DelimPlot-0.2.1-linux-x64.AppImage
artifacts/release/DelimPlot-0.2.1-linux-x64.tar.gz
```

## Suggested Tag

```text
v0.2.1
```
