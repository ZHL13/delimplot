namespace DelimPlot.Core.Models;

public sealed class PlotSeriesConfig
{
    public int YColumnIndex { get; set; }
    public PlotSeriesStyle Style { get; set; } = PlotSeriesStyle.Line;
    public string Color { get; set; } = "#2563EB";
    public double LineWidth { get; set; } = 2;
    public double MarkerSize { get; set; } = 5;

    public PlotSeriesConfig Clone()
    {
        return new PlotSeriesConfig
        {
            YColumnIndex = YColumnIndex,
            Style = Style,
            Color = Color,
            LineWidth = LineWidth,
            MarkerSize = MarkerSize
        };
    }
}
