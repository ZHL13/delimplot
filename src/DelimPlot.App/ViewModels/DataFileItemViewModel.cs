using DelimPlot.Core.Models;

namespace DelimPlot.App.ViewModels;

public sealed class DataFileItemViewModel
{
    public DataFileItemViewModel(DataFile dataFile, Action<DataFileItemViewModel> delete)
    {
        DataFile = dataFile;
        DeleteCommand = new RelayCommand(_ => delete(this));
    }

    public DataFile DataFile { get; }
    public string FileName => DataFile.FileName;
    public string Summary => DataFile.SkippedColumns.Count == 0
        ? $"{DataFile.Columns.Count} columns, {DataFile.Rows.Count} rows"
        : $"{DataFile.Columns.Count} columns, {DataFile.Rows.Count} rows ({DataFile.SkippedColumns.Count} text columns ignored)";
    public RelayCommand DeleteCommand { get; }
}
