using DelimPlot.Core.Models;
using ScottPlot;

namespace DelimPlot.Plotting.Rendering;

public static class PlotRenderer
{
    public static void Render(Plot plot, PlotConfig? config, bool autoScale = true)
    {
        plot.Clear();

        if (config is null || config.DataFile.Columns.Count == 0 || config.Series.Count == 0)
        {
            plot.Title("DelimPlot");
            plot.XLabel("Drop or open a text data file");
            plot.YLabel("Select columns to plot");
            ApplyTextFonts(plot);
            return;
        }

        var dataFile = config.DataFile;
        var xColumn = dataFile.Columns[ClampIndex(config.XColumnIndex, dataFile.Columns.Count)];

        foreach (var seriesConfig in config.Series)
        {
            var yColumn = dataFile.Columns[ClampIndex(seriesConfig.YColumnIndex, dataFile.Columns.Count)];
            var color = ParseColor(seriesConfig.Color);
            var scatter = seriesConfig.Style switch
            {
                PlotSeriesStyle.ScatterPoints => plot.Add.ScatterPoints(xColumn.Values, yColumn.Values, color),
                PlotSeriesStyle.LineAndPoints => plot.Add.Scatter(xColumn.Values, yColumn.Values, color),
                _ => plot.Add.ScatterLine(xColumn.Values, yColumn.Values, color)
            };

            scatter.LegendText = yColumn.Name;
            scatter.LineWidth = seriesConfig.Style == PlotSeriesStyle.ScatterPoints
                ? 0
                : Math.Max(0.5f, (float)seriesConfig.LineWidth);
            scatter.MarkerSize = seriesConfig.Style == PlotSeriesStyle.Line
                ? 0
                : Math.Max(1f, (float)seriesConfig.MarkerSize);
        }

        plot.Title(string.IsNullOrWhiteSpace(config.Title) ? dataFile.FileName : config.Title);
        plot.XLabel(string.IsNullOrWhiteSpace(config.XAxisLabel) ? xColumn.Name : config.XAxisLabel);
        plot.YLabel(config.Series.Count == 1
            ? dataFile.Columns[ClampIndex(config.Series[0].YColumnIndex, dataFile.Columns.Count)].Name
            : string.IsNullOrWhiteSpace(config.YAxisLabel) ? "Y" : config.YAxisLabel);

        if (config.Series.Count > 1)
            plot.ShowLegend();

        ApplyTextFonts(plot);
        if (autoScale)
            plot.Axes.AutoScale();
    }

    private static void ApplyTextFonts(Plot plot)
    {
        plot.Axes.Title.Label.SetBestFont();
        plot.Axes.Bottom.Label.SetBestFont();
        plot.Axes.Left.Label.SetBestFont();
        plot.Legend.SetBestFontOnEachRender = true;
    }

    private static int ClampIndex(int index, int count)
    {
        return Math.Clamp(index, 0, count - 1);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return Color.FromHex(hex);
        }
        catch
        {
            return Colors.Blue;
        }
    }
}
