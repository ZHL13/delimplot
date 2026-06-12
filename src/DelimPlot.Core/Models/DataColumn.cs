namespace DelimPlot.Core.Models;

public sealed class DataColumn
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required double[] Values { get; init; }
}
