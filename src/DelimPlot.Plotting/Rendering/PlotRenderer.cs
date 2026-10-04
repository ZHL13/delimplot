using System.Globalization;
using DelimPlot.Core.Models;
using ScottPlot;
using ScottPlot.TickGenerators;

namespace DelimPlot.Plotting.Rendering;

public static class PlotRenderer
{
    private const double LogFloor = 1e-13;

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

        var xIsLog = config.XAxisScale == AxisScale.Log;
        var hasLeftSeries = config.Series.Any(series => series.YAxisSide == YAxisSide.Left);
        var hasRightSeries = config.Series.Any(series => series.YAxisSide == YAxisSide.Right);

        foreach (var seriesConfig in config.Series)
        {
            var yColumn = dataFile.Columns[ClampIndex(seriesConfig.YColumnIndex, dataFile.Columns.Count)];
            var color = ParseColor(seriesConfig.Color);
            var isRight = seriesConfig.YAxisSide == YAxisSide.Right;
            var yIsLog = isRight
                ? config.YRightAxisScale == AxisScale.Log
                : config.YLeftAxisScale == AxisScale.Log;

            var (xs, ys) = BuildAlignedSeries(xColumn.Values, yColumn.Values, xIsLog, yIsLog);

            if (xs.Length == 0)
                continue;

            var scatter = seriesConfig.Style switch
            {
                PlotSeriesStyle.ScatterPoints => plot.Add.ScatterPoints(xs, ys, color),
                PlotSeriesStyle.LineAndPoints => plot.Add.Scatter(xs, ys, color),
                _ => plot.Add.ScatterLine(xs, ys, color)
            };

            scatter.Axes.YAxis = isRight ? plot.Axes.Right : plot.Axes.Left;
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
        plot.Axes.Left.Label.Text = hasLeftSeries ? ResolveYAxisLabel(config, YAxisSide.Left, dataFile) : string.Empty;
        plot.Axes.Right.Label.Text = hasRightSeries ? ResolveYAxisLabel(config, YAxisSide.Right, dataFile) : string.Empty;

        if (config.Series.Count > 1)
            plot.ShowLegend();

        ResetTickGenerators(plot);
        ApplyTextFonts(plot);

        if (autoScale)
            plot.Axes.AutoScale();

        if (xIsLog)
            ApplyLogTicks(plot.Axes.Bottom);

        if (hasLeftSeries && config.YLeftAxisScale == AxisScale.Log)
            ApplyLogTicks(plot.Axes.Left);

        if (hasRightSeries && config.YRightAxisScale == AxisScale.Log)
            ApplyLogTicks(plot.Axes.Right);
    }

    private static void ApplyTextFonts(Plot plot)
    {
        plot.Axes.Title.Label.SetBestFont();
        plot.Axes.Bottom.Label.SetBestFont();
        plot.Axes.Left.Label.SetBestFont();
        plot.Axes.Right.Label.SetBestFont();
        plot.Legend.SetBestFontOnEachRender = true;
    }

    private static void ResetTickGenerators(Plot plot)
    {
        plot.Axes.Bottom.TickGenerator = new NumericAutomatic();
        plot.Axes.Left.TickGenerator = new NumericAutomatic();
        plot.Axes.Right.TickGenerator = new NumericAutomatic();
    }

    private static (double[] Xs, double[] Ys) BuildAlignedSeries(
        double[] xValues, double[] yValues, bool xIsLog, bool yIsLog)
    {
        var length = Math.Min(xValues.Length, yValues.Length);
        var xs = new List<double>(length);
        var ys = new List<double>(length);

        for (var i = 0; i < length; i++)
        {
            var x = xValues[i];
            var y = yValues[i];

            if (double.IsNaN(x) || double.IsNaN(y))
                continue;

            if (xIsLog)
                x = Math.Log10(Math.Max(x, LogFloor));

            if (yIsLog)
                y = Math.Log10(Math.Max(y, LogFloor));

            xs.Add(x);
            ys.Add(y);
        }

        return (xs.ToArray(), ys.ToArray());
    }

    private static string ResolveYAxisLabel(PlotConfig config, YAxisSide side, DataFile dataFile)
    {
        var sideSeries = config.Series.Where(series => series.YAxisSide == side).ToList();

        if (sideSeries.Count == 1)
        {
            var column = dataFile.Columns[ClampIndex(sideSeries[0].YColumnIndex, dataFile.Columns.Count)];
            return column.Name;
        }

        if (side == YAxisSide.Left)
            return string.IsNullOrWhiteSpace(config.YAxisLabel) ? "Y" : config.YAxisLabel;

        return "Y (right)";
    }

    private static void ApplyLogTicks(IAxis axis)
    {
        var min = axis.Min;
        var max = axis.Max;

        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
            return;

        var range = max - min;
        var mantissas = range >= 4
            ? new[] { 1 }
            : range >= 1
                ? new[] { 1, 2, 5 }
                : new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        var minExponent = (int)Math.Floor(min);
        var maxExponent = (int)Math.Ceiling(max);

        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (var exponent = minExponent; exponent <= maxExponent; exponent++)
        {
            foreach (var mantissa in mantissas)
            {
                var position = exponent + Math.Log10(mantissa);
                if (position < min || position > max)
                    continue;

                tickPositions.Add(position);
                tickLabels.Add(FormatLogTickLabel(mantissa, exponent));
            }
        }

        if (tickPositions.Count > 0)
            axis.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
    }

    private static string FormatLogTickLabel(int mantissa, int exponent)
    {
        if (exponent is >= -3 and <= 3)
        {
            var value = mantissa * Math.Pow(10, exponent);
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return mantissa == 1 ? $"1e{exponent}" : $"{mantissa}e{exponent}";
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
