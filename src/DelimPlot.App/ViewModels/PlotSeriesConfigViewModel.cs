using System.Collections.ObjectModel;
using DelimPlot.Core.Models;

namespace DelimPlot.App.ViewModels;

public sealed class PlotSeriesConfigViewModel : ObservableObject
{
    private readonly MainWindowViewModel _owner;
    private ColumnOption? _selectedYColumn;
    private PlotStyleOption _selectedStyleOption;
    private string _color;
    private double _lineWidth;
    private double _markerSize;
    private bool _canRemove;

    public PlotSeriesConfigViewModel(MainWindowViewModel owner, PlotSeriesConfig config)
    {
        _owner = owner;
        _selectedYColumn = owner.Columns.FirstOrDefault(column => column.Index == config.YColumnIndex);
        _selectedStyleOption = StyleOptions.First(option => option.Style == config.Style);
        _color = config.Color;
        _lineWidth = config.LineWidth;
        _markerSize = config.MarkerSize;
        RemoveCommand = new RelayCommand(_ => _owner.RemoveSeries(this), _ => CanRemove);
    }

    public ObservableCollection<ColumnOption> ColumnOptions => _owner.Columns;

    public IReadOnlyList<PlotStyleOption> StyleOptions { get; } =
    [
        new(PlotSeriesStyle.Line, "line"),
        new(PlotSeriesStyle.ScatterPoints, "scatter points"),
        new(PlotSeriesStyle.LineAndPoints, "line + points")
    ];

    public IReadOnlyList<string> ColorOptions => _owner.ColorOptions;

    public RelayCommand RemoveCommand { get; }

    public ColumnOption? SelectedYColumn
    {
        get => _selectedYColumn;
        set
        {
            if (SetProperty(ref _selectedYColumn, value))
                _owner.NotifyPlotConfigChanged();
        }
    }

    public PlotStyleOption SelectedStyleOption
    {
        get => _selectedStyleOption;
        set
        {
            if (SetProperty(ref _selectedStyleOption, value))
                _owner.NotifyPlotConfigChanged();
        }
    }

    public string Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
                _owner.NotifyPlotConfigChanged();
        }
    }

    public double LineWidth
    {
        get => _lineWidth;
        set
        {
            if (SetProperty(ref _lineWidth, value))
                _owner.NotifyPlotConfigChanged();
        }
    }

    public double MarkerSize
    {
        get => _markerSize;
        set
        {
            if (SetProperty(ref _markerSize, value))
                _owner.NotifyPlotConfigChanged();
        }
    }

    public bool CanRemove
    {
        get => _canRemove;
        set
        {
            if (SetProperty(ref _canRemove, value))
                RemoveCommand.RaiseCanExecuteChanged();
        }
    }

    public PlotSeriesConfig ToConfig()
    {
        return new PlotSeriesConfig
        {
            YColumnIndex = SelectedYColumn?.Index ?? 0,
            Style = SelectedStyleOption.Style,
            Color = Color,
            LineWidth = Math.Max(0.5, LineWidth),
            MarkerSize = Math.Max(1, MarkerSize)
        };
    }
}
