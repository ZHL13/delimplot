namespace DelimPlot.App.ViewModels;

public sealed record ColumnOption(int Index, string Name)
{
    public override string ToString() => Name;
}
