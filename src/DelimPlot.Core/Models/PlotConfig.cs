namespace DelimPlot.Core.Models;

public sealed class PlotConfig
{
    public required DataFile DataFile { get; init; }
    public int XColumnIndex { get; set; }
    public List<PlotSeriesConfig> Series { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string XAxisLabel { get; set; } = string.Empty;
    public string YAxisLabel { get; set; } = string.Empty;
    public AxisScale XAxisScale { get; set; } = AxisScale.Linear;
    public AxisScale YLeftAxisScale { get; set; } = AxisScale.Linear;
    public AxisScale YRightAxisScale { get; set; } = AxisScale.Linear;

    public PlotConfig Clone()
    {
        return new PlotConfig
        {
            DataFile = DataFile,
            XColumnIndex = XColumnIndex,
            Series = Series.Select(series => series.Clone()).ToList(),
            Title = Title,
            XAxisLabel = XAxisLabel,
            YAxisLabel = YAxisLabel,
            XAxisScale = XAxisScale,
            YLeftAxisScale = YLeftAxisScale,
            YRightAxisScale = YRightAxisScale
        };
    }
}
