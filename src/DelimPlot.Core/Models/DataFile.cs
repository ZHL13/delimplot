namespace DelimPlot.Core.Models;

public sealed class DataFile
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required IReadOnlyList<DataColumn> Columns { get; init; }
    public required IReadOnlyList<double[]> Rows { get; init; }
    public required IReadOnlyList<string[]> PreviewRows { get; init; }
    public required string Delimiter { get; init; }
    public required bool HasHeader { get; init; }
}
