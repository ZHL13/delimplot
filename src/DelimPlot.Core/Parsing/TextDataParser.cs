using System.Globalization;
using System.Text.RegularExpressions;
using DelimPlot.Core.Models;

namespace DelimPlot.Core.Parsing;

public sealed class TextDataParser
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public Task<DataFile> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Parse(filePath, cancellationToken), cancellationToken);
    }

    private static DataFile Parse(string filePath, CancellationToken cancellationToken)
    {
        var validLines = new List<string>();

        foreach (var rawLine in File.ReadLines(filePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || IsComment(line))
                continue;

            validLines.Add(line);
        }

        if (validLines.Count == 0)
            throw new InvalidDataException("The file does not contain any data lines.");

        var delimiter = DetectDelimiter(validLines.Take(10));
        var firstTokens = Split(validLines[0], delimiter);
        var hasHeader = LooksLikeHeader(firstTokens);
        var headerTokens = hasHeader ? firstTokens : [];
        var rows = new List<double[]>();
        var columnCount = hasHeader ? headerTokens.Count : 0;

        foreach (var line in validLines.Skip(hasHeader ? 1 : 0))
        {
            var tokens = Split(line, delimiter);
            if (tokens.Count == 0)
                continue;

            if (columnCount == 0)
                columnCount = tokens.Count;

            if (tokens.Count < columnCount)
                continue;

            var values = new double[columnCount];
            var parsed = true;

            for (var i = 0; i < columnCount; i++)
            {
                if (!TryParseDouble(tokens[i], out values[i]))
                {
                    parsed = false;
                    break;
                }
            }

            if (parsed)
                rows.Add(values);
        }

        if (rows.Count == 0 || columnCount == 0)
            throw new InvalidDataException("The file does not contain enough numeric rows to plot.");

        var columnNames = BuildColumnNames(headerTokens, columnCount);
        var columns = new List<DataColumn>(columnCount);

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var values = new double[rows.Count];
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                values[rowIndex] = rows[rowIndex][columnIndex];

            columns.Add(new DataColumn
            {
                Index = columnIndex,
                Name = columnNames[columnIndex],
                Values = values
            });
        }

        return new DataFile
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Columns = columns,
            Rows = rows,
            PreviewRows = rows.Take(20).Select(row => row.Select(FormatPreviewValue).ToArray()).ToArray(),
            Delimiter = GetDisplayName(delimiter),
            HasHeader = hasHeader
        };
    }

    private static bool IsComment(string line)
    {
        return line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('%');
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
            .Select(line => Split(line, delimiter).Count)
            .Where(count => count > 1)
            .ToArray();

        if (counts.Length == 0)
            return 0;

        var consistency = counts.GroupBy(count => count).Max(group => group.Count());
        var width = counts.GroupBy(count => count).OrderByDescending(group => group.Count()).First().Key;

        return consistency * 100 + width;
    }

    private static IReadOnlyList<string> Split(string line, DelimiterKind delimiter)
    {
        var tokens = delimiter switch
        {
            DelimiterKind.Comma => line.Split(','),
            DelimiterKind.Tab => line.Split('\t'),
            DelimiterKind.Semicolon => line.Split(';'),
            _ => WhitespaceRegex.Split(line.Trim())
        };

        return tokens
            .Select(token => token.Trim().Trim('"'))
            .Where(token => token.Length > 0)
            .ToArray();
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            return false;

        var nonNumericCount = tokens.Count(token => !TryParseDouble(token, out _));
        return nonNumericCount > tokens.Count / 2;
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
