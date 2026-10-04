using DelimPlot.Core.Models;

namespace DelimPlot.App.ViewModels;

public sealed record AxisScaleOption(AxisScale Scale, string Name)
{
    public override string ToString() => Name;
}
