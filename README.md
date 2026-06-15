# DelimPlot

<p align="center">
  <img src="assets/Icon.png" alt="DelimPlot icon" width="120">
</p>

DelimPlot is a standalone desktop app for plotting columns from plain-text data files on Windows, macOS, and Linux.

It is designed for quick scientific and engineering plotting workflows: import delimited text, choose one X column and one or more Y series, tune the style, and save multiple graphs in a reusable project file.

## Features

- Import plain-text data files regardless of file extension.
- Auto-detect comma, tab, semicolon, and whitespace delimiters.
- Auto-detect CRLF, LF, and CR line endings before parsing.
- Ignore common comment lines beginning with `#`, `//`, or `%`.
- Preview parsed numeric rows before plotting.
- Plot one X column against one or more Y columns.
- Configure line, scatter, and line + marker styles.
- Adjust line width, marker size, color, title, and axis labels.
- Manage multiple saved graphs in the Graph Browser.
- Save the current plot as PNG.
- Export and import `.delimplot` project files containing source data, graph settings, and graph thumbnails.
- Open `.delimplot` files directly with the DelimPlot executable or macOS app bundle.
- Render Chinese and other multilingual plot titles using system font selection.

## Screenshots

![DelimPlot graph workspace](screenshots/Snap1.png)

![DelimPlot saved graph browser](screenshots/Snap2.png)

## Sample Data

Example files are available in `samples/`:

- `trigonometric_waves.tsv`: tab-delimited sine, cosine, harmonic, and damped sine curves.
- `damped_oscillation.tsv`: tab-delimited damped displacement, velocity, and envelope curves.
- `signals_saw_square.csv`: comma-delimited sawtooth, square wave, random step, and pulse train signals.
- `conic_sections.dat`: whitespace-delimited ellipse branches, parabola, and hyperbola curves.
- `power_root_hyperbola.txt`: semicolon-delimited power, square-root, and inverse curves.

Each sample file uses the first column as the X coordinate and the remaining columns as Y series.

The sample project `samples/defaultProject.delimplot` can be opened directly with DelimPlot or imported from `File -> Import Project...`.

## Repository Layout

- `src/DelimPlot.Core`: data models and text parser.
- `src/DelimPlot.Plotting`: ScottPlot rendering helper.
- `src/DelimPlot.App`: Avalonia desktop application.
- `assets`: repository-level branding assets.
- `build/macos`: macOS app bundle metadata and packaging script.
- `build/linux`: Linux standalone packaging script.
- `samples`: example data files.
- `screenshots`: release screenshots.

## Build

Install the .NET 8 SDK, then restore and build the solution:

The .NET SDK and restored NuGet packages are not vendored in this repository.

```powershell
dotnet restore DelimPlot.sln
dotnet build DelimPlot.sln --configuration Release
```

## Publish Windows Standalone

```powershell
dotnet publish src\DelimPlot.App\DelimPlot.App.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\win-x64
```

The standalone executable is:

```text
artifacts\win-x64\DelimPlot.exe
```

Release builds are published as:

```text
DelimPlot-0.1.1-win-x64.exe
DelimPlot-latest-win-x64.exe
```

These files are copied from the publish output before being attached to the GitHub release.

## Publish macOS App Bundle

The macOS release is built on Apple Silicon with the .NET 8 SDK. If .NET 8 was installed with Homebrew, the package script uses `/opt/homebrew/opt/dotnet@8/libexec/dotnet` by default. Override `DOTNET` when using another SDK path:

```bash
DOTNET=dotnet ./build/macos/package-osx-arm64.sh
```

The script publishes the app for `osx-arm64`, assembles a standard `.app` bundle, copies the required Avalonia/Skia native libraries, installs the macOS `.icns` icon, registers `.delimplot` documents in `Info.plist`, ad-hoc signs the bundle, and creates the release zip.

The macOS release output is:

```text
artifacts/release/DelimPlot.app
artifacts/release/DelimPlot-0.1.1-osx-arm64.zip
```

The `.app` bundle is useful for local smoke testing. Attach the zip to the GitHub release. Because the bundle is ad-hoc signed rather than Developer ID notarized, macOS may require right-clicking and choosing Open on first launch after download.

## Publish Linux Standalone

The Linux release is built on Ubuntu with the .NET 8 SDK:

```bash
./build/linux/package-linux-x64.sh
```

The script needs `appimagetool` to create the AppImage. If it is not on `PATH`, provide it explicitly:

```bash
APPIMAGETOOL=/path/to/appimagetool-x86_64.AppImage ./build/linux/package-linux-x64.sh
```

To build only the portable tarball, skip AppImage packaging:

```bash
SKIP_APPIMAGE=true ./build/linux/package-linux-x64.sh
```

Linux packaging intentionally publishes a self-contained app directory rather than a .NET single-file executable. Avalonia and ScottPlot both bring SkiaSharp native assets, and a single-file Linux publish can select an older `libSkiaSharp.so` or fail when the bundle extraction cache is not writable. The Linux script copies the `SkiaSharp.NativeAssets.Linux.NoDependencies` native library that matches the managed SkiaSharp version before packaging.

The Linux release output is:

```text
artifacts/release/DelimPlot-0.1.1-linux-x64/
artifacts/release/DelimPlot-0.1.1-linux-x64.tar.gz
artifacts/release/DelimPlot-0.1.1-linux-x64.AppImage
```

Use the AppImage for a one-file desktop app. If the file manager does not launch downloaded executables by default, mark the AppImage as executable from the file properties dialog or run `chmod +x DelimPlot-0.1.1-linux-x64.AppImage`. Use the tarball as a fallback on systems that do not support AppImage/FUSE; after extraction, launch the bundled `DelimPlot` executable.

## Project Files

Use `File -> Export Project...` to save a `.delimplot` file. This file bundles:

- imported source data files
- saved graph configurations
- graph browser thumbnails

Use `File -> Import Project...` or open the `.delimplot` file with DelimPlot to restore the workspace.

## License

DelimPlot is licensed under the Apache License 2.0. See `LICENSE`.

## Technology And Third-Party Licenses

DelimPlot is built with .NET 8, Avalonia UI, and ScottPlot. The Windows standalone executable, macOS app bundle, and Linux standalone executable include the .NET runtime and required GUI/rendering dependencies.

The direct NuGet dependencies are:

- `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, and `Avalonia.Fonts.Inter` under the MIT license.
- `ScottPlot` and `ScottPlot.Avalonia` under the MIT license.

The platform publish outputs also include permissive transitive dependencies, including SkiaSharp, HarfBuzzSharp, MicroCom.Runtime, Tmds.DBus.Protocol, System.IO.Pipelines, Avalonia native assets, and platform-specific rendering libraries. These are MIT or BSD-style licensed. No extra commercial license acquisition is required for normal use or redistribution, but third-party notices should be preserved. See `THIRD-PARTY-NOTICES.md`.
