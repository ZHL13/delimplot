using DelimPlot.Core.Models;

namespace DelimPlot.App.ViewModels;

public sealed record PlotStyleOption(PlotSeriesStyle Style, string Name)
{
    public override string ToString() => Name;
}
