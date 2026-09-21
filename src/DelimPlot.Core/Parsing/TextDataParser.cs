using System.Globalization;
using System.Text.RegularExpressions;
using DelimPlot.Core.Models;

namespace DelimPlot.Core.Parsing;

public sealed class TextDataParser
{
    private const int DelimiterSampleSize = 10;
    private const int PreviewRowLimit = 20;

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public Task<DataFile> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Parse(filePath, cancellationToken), cancellationToken);
    }

    private static DataFile Parse(string filePath, CancellationToken cancellationToken)
    {
        var validLines = new List<string>();
        var text = File.ReadAllText(filePath);
        var newLine = DetectNewLine(text);

        foreach (var rawLine in SplitLines(text, newLine))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || IsComment(line))
                continue;

            validLines.Add(line);
        }

        if (validLines.Count == 0)
            throw new InvalidDataException("The file does not contain any data lines.");

        var delimiter = DetectDelimiter(validLines.Take(DelimiterSampleSize));
        var firstCells = SplitCells(validLines[0], delimiter);
        var hasHeader = LooksLikeHeader(firstCells);
        var headerCells = hasHeader ? firstCells : [];

        var dataLines = validLines.Skip(hasHeader ? 1 : 0).ToArray();
        var columnCount = hasHeader
            ? Math.Max(headerCells.Length, 1)
            : dataLines
                .Take(DelimiterSampleSize)
                .Select(line => SplitCells(line, delimiter).Length)
                .DefaultIfEmpty(0)
                .Max();

        if (columnCount == 0)
            throw new InvalidDataException("The file does not contain any columns.");

        var columnNames = BuildColumnNames(headerCells, columnCount);
        var normalizedRows = dataLines
            .Select(line => NormalizeCells(SplitCells(line, delimiter), columnCount))
            .ToArray();

        var numericColumns = DetectNumericColumns(normalizedRows, columnCount);

        var numericColumnIndexes = new List<int>();
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            if (numericColumns[columnIndex])
                numericColumnIndexes.Add(columnIndex);
        }

        if (numericColumnIndexes.Count == 0)
            throw new InvalidDataException("The file does not contain any numeric columns to plot.");

        var rows = new List<double[]>(normalizedRows.Length);

        foreach (var normalizedRow in normalizedRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new double[numericColumnIndexes.Count];

            for (var i = 0; i < numericColumnIndexes.Count; i++)
            {
                var cell = normalizedRow[numericColumnIndexes[i]];
                values[i] = cell.Length > 0 && TryParseDouble(cell, out var value) ? value : double.NaN;
            }

            rows.Add(values);
        }

        if (rows.Count == 0)
            throw new InvalidDataException("The file does not contain enough numeric rows to plot.");

        var columns = new List<DataColumn>(numericColumnIndexes.Count);
        var skippedColumns = new List<string>();

        for (var i = 0; i < numericColumnIndexes.Count; i++)
        {
            var sourceIndex = numericColumnIndexes[i];
            var values = new double[rows.Count];
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                values[rowIndex] = rows[rowIndex][i];

            columns.Add(new DataColumn
            {
                Index = i,
                Name = columnNames[sourceIndex],
                Values = values
            });
        }

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            if (!numericColumns[columnIndex])
                skippedColumns.Add(columnNames[columnIndex]);
        }

        var previewRows = normalizedRows
            .Take(PreviewRowLimit)
            .Select(row => row.Select(FormatPreviewCell).ToArray())
            .ToArray();

        return new DataFile
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Columns = columns,
            Rows = rows,
            PreviewColumns = columnNames,
            PreviewRows = previewRows,
            SkippedColumns = skippedColumns,
            Delimiter = GetDisplayName(delimiter),
            HasHeader = hasHeader
        };
    }

    private static bool IsComment(string line)
    {
        return line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('%');
    }

    private static string DetectNewLine(string text)
    {
        var crlfCount = 0;
        var lfCount = 0;
        var crCount = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlfCount++;
                    i++;
                }
                else
                {
                    crCount++;
                }
            }
            else if (ch == '\n')
            {
                lfCount++;
            }
        }

        if (crlfCount >= lfCount && crlfCount >= crCount && crlfCount > 0)
            return "\r\n";

        if (lfCount >= crCount && lfCount > 0)
            return "\n";

        return crCount > 0 ? "\r" : Environment.NewLine;
    }

    private static IEnumerable<string> SplitLines(string text, string newLine)
    {
        if (text.Length == 0)
            yield break;

        var start = 0;
        while (start <= text.Length)
        {
            var index = text.IndexOf(newLine, start, StringComparison.Ordinal);
            if (index < 0)
                break;

            yield return text[start..index];
            start = index + newLine.Length;
        }

        if (start < text.Length)
            yield return text[start..];
    }

    private static DelimiterKind DetectDelimiter(IEnumerable<string> lines)
    {
        var candidates = new[]
        {
            DelimiterKind.Comma,
            DelimiterKind.Tab,
            DelimiterKind.Semicolon,
            DelimiterKind.Whitespace
        };

        return candidates
            .Select(candidate => new
            {
                Delimiter = candidate,
                Score = ScoreDelimiter(lines, candidate)
            })
            .OrderByDescending(item => item.Score)
            .First()
            .Delimiter;
    }

    private static int ScoreDelimiter(IEnumerable<string> lines, DelimiterKind delimiter)
    {
        var counts = lines
            .Select(line => SplitCells(line, delimiter).Length)
            .Where(count => count > 1)
            .ToArray();

        if (counts.Length == 0)
            return 0;

        var consistency = counts.GroupBy(count => count).Max(group => group.Count());
        var width = counts.GroupBy(count => count).OrderByDescending(group => group.Count()).First().Key;

        return consistency * 100 + width;
    }

    private static string[] SplitCells(string line, DelimiterKind delimiter)
    {
        var rawCells = delimiter switch
        {
            DelimiterKind.Comma => line.Split(','),
            DelimiterKind.Tab => line.Split('\t'),
            DelimiterKind.Semicolon => line.Split(';'),
            _ => WhitespaceRegex.Split(line.Trim())
        };

        var cells = new string[rawCells.Length];
        for (var i = 0; i < rawCells.Length; i++)
            cells[i] = TrimCell(rawCells[i]);

        return cells;
    }

    private static string TrimCell(string cell)
    {
        var trimmed = cell.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1].Trim();

        return trimmed;
    }

    private static string[] NormalizeCells(string[] cells, int columnCount)
    {
        if (cells.Length == columnCount)
            return cells;

        var normalized = new string[columnCount];
        var copyCount = Math.Min(cells.Length, columnCount);

        for (var i = 0; i < copyCount; i++)
            normalized[i] = cells[i];

        for (var i = copyCount; i < columnCount; i++)
            normalized[i] = string.Empty;

        return normalized;
    }

    private static bool[] DetectNumericColumns(IReadOnlyList<string[]> rows, int columnCount)
    {
        var numeric = new bool[columnCount];

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var numericCount = 0;
            var nonNumericCount = 0;

            foreach (var row in rows)
            {
                var cell = row[columnIndex];
                if (cell.Length == 0)
                    continue;

                if (TryParseDouble(cell, out _))
                    numericCount++;
                else
                    nonNumericCount++;
            }

            numeric[columnIndex] = numericCount > 0 && nonNumericCount == 0;
        }

        return numeric;
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> cells)
    {
        var nonEmpty = cells.Where(cell => cell.Length > 0).ToArray();
        if (nonEmpty.Length == 0)
            return false;

        var nonNumericCount = nonEmpty.Count(cell => !TryParseDouble(cell, out _));
        return nonNumericCount > nonEmpty.Length / 2;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(
            text,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string[] BuildColumnNames(IReadOnlyList<string> headerTokens, int columnCount)
    {
        var names = new string[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            var headerName = i < headerTokens.Count ? headerTokens[i].Trim() : string.Empty;
            names[i] = string.IsNullOrWhiteSpace(headerName) ? $"Column {i + 1}" : headerName;
        }

        return names;
    }

    private static string FormatPreviewValue(double value)
    {
        return value.ToString("G6", CultureInfo.InvariantCulture);
    }

    private static string FormatPreviewCell(string cell)
    {
        if (cell.Length == 0)
            return string.Empty;

        return TryParseDouble(cell, out var value) ? FormatPreviewValue(value) : cell;
    }

    private static string GetDisplayName(DelimiterKind delimiter)
    {
        return delimiter switch
        {
            DelimiterKind.Comma => "Comma",
            DelimiterKind.Tab => "Tab",
            DelimiterKind.Semicolon => "Semicolon",
            _ => "Whitespace"
        };
    }

    private enum DelimiterKind
    {
        Comma,
        Tab,
        Semicolon,
        Whitespace
    }
}
