namespace DelimPlot.App.ViewModels;

public sealed class PreviewRowViewModel
{
    public PreviewRowViewModel(IEnumerable<string> cells)
    {
        Cells = cells.ToArray();
    }

    public IReadOnlyList<string> Cells { get; }
}
