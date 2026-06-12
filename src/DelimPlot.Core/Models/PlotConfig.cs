namespace DelimPlot.Core.Models;

public sealed class PlotConfig
{
    public required DataFile DataFile { get; init; }
    public int XColumnIndex { get; set; }
    public List<PlotSeriesConfig> Series { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string XAxisLabel { get; set; } = string.Empty;
    public string YAxisLabel { get; set; } = string.Empty;

    public PlotConfig Clone()
    {
        return new PlotConfig
        {
            DataFile = DataFile,
            XColumnIndex = XColumnIndex,
            Series = Series.Select(series => series.Clone()).ToList(),
            Title = Title,
            XAxisLabel = XAxisLabel,
            YAxisLabel = YAxisLabel
        };
    }
}
