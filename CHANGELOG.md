# Changelog

All notable changes to DelimPlot will be documented in this file.

## 0.1.1 - 2026-06-15

### Changed

- Data import now accepts plain-text files with any file extension from the file picker, drag and drop, and startup file paths.
- Text parsing now detects CRLF, LF, and CR line endings before splitting lines.

## 0.1.0 - 2026-06-13

### Added

- Avalonia desktop app for plotting columns from delimited text files.
- Text parser with delimiter detection and numeric row preview.
- Multi-file import with file list management.
- Plot configuration for X column, multiple Y series, style, color, line width, marker size, title, and axis labels.
- Graph Browser for saving and managing multiple graph configurations.
- PNG export for the current plot.
- `.delimplot` project import/export with source data, graph settings, and thumbnails.
- Direct opening of `.delimplot` files through command-line file association.
- Windows standalone publish target.
- Sample data files for release screenshots and demos.

### Notes

- Initial release target version: `0.1.0`.
- License: Apache-2.0.
