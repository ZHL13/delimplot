using DelimPlot.Core.Models;

namespace DelimPlot.App.ViewModels;

public sealed record YAxisSideOption(YAxisSide Side, string Name)
{
    public override string ToString() => Name;
}
