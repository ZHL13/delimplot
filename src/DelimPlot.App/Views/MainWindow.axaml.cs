using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DelimPlot.App.ViewModels;
using DelimPlot.Core.Models;
using DelimPlot.Plotting.Rendering;
using ScottPlot;

namespace DelimPlot.App.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _thumbnailTimer;
    private PlotShape? _lastPlotShape;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        _viewModel.PlotChanged += ViewModel_PlotChanged;
        _viewModel.ThumbnailRefreshRequested += (_, _) => QueueThumbnailRefresh();

        _thumbnailTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(160)
        };
        _thumbnailTimer.Tick += (_, _) =>
        {
            _thumbnailTimer.Stop();
            UpdateSelectedThumbnail();
        };

        DragDrop.SetAllowDrop(DropZone, true);
        DropZone.AddHandler(DragDrop.DragOverEvent, DropZone_DragOver);
        DropZone.AddHandler(DragDrop.DropEvent, DropZone_Drop);

        RenderPlot(null);
    }

    private void ViewModel_PlotChanged(object? sender, PlotConfig? config)
    {
        RenderPlot(config);
    }

    private void RenderPlot(PlotConfig? config)
    {
        var nextShape = PlotShape.FromConfig(config);
        var autoScale = nextShape is null || !nextShape.Equals(_lastPlotShape);
        PlotRenderer.Render(PlotControl.Plot, config, autoScale);
        _lastPlotShape = nextShape;
        PlotControl.Refresh();
    }

    private void QueueThumbnailRefresh()
    {
        if (!_viewModel.CanUpdateSelectedSnapshotThumbnail())
            return;

        _thumbnailTimer.Stop();
        _thumbnailTimer.Start();
    }

    private void UpdateSelectedThumbnail()
    {
        if (!_viewModel.CanUpdateSelectedSnapshotThumbnail())
            return;

        try
        {
            var pngBytes = PlotControl.Plot.GetImageBytes(300, 180, ImageFormat.Png);
            _viewModel.UpdateSelectedSnapshotThumbnail(pngBytes);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Could not update thumbnail: {ex.Message}";
        }
    }

    private async void OpenFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Data Files",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Text data")
                {
                    Patterns = ["*.txt", "*.dat", "*.csv", "*.tsv", "*"],
                    MimeTypes = ["text/plain", "text/csv", "text/tab-separated-values"]
                }
            ]
        });

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

        await _viewModel.LoadFilesAsync(paths);
    }

    private async void SavePng_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.CurrentPlotConfig is null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Plot as PNG",
            SuggestedFileName = _viewModel.SuggestedPngName,
            DefaultExtension = "png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG image")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"]
                }
            ]
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        PlotControl.Plot.SavePng(path, 1400, 900);
        _viewModel.StatusMessage = $"Saved {Path.GetFileName(path)}.";
    }

    private async void ImportProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import DelimPlot Project",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DelimPlot project")
                {
                    Patterns = ["*.delimplot"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await OpenProjectFileAsync(path);
    }

    private async void ExportProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export DelimPlot Project",
            SuggestedFileName = "DelimPlotProject.delimplot",
            DefaultExtension = "delimplot",
            FileTypeChoices =
            [
                new FilePickerFileType("DelimPlot project")
                {
                    Patterns = ["*.delimplot"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await _viewModel.ExportProjectAsync(path);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Could not export project: {ex.Message}";
        }
    }

    private void DeleteSelectedSnapshots_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = GraphBrowserList.SelectedItems?
            .OfType<PlotSnapshotViewModel>()
            .ToArray();

        if (selected is { Length: > 0 })
        {
            _viewModel.DeleteSnapshots(selected);
            return;
        }

        if (_viewModel.SelectedSnapshot is not null)
            _viewModel.DeleteSnapshots([_viewModel.SelectedSnapshot]);
    }

    private void ClearAllPlots_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.ClearSnapshots();
    }

    private async void DeleteFileItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DataFileItemViewModel file })
            await DeleteFilesWithConfirmationAsync([file]);
    }

    private async void DeleteSelectedFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = FileListBox.SelectedItems?
            .OfType<DataFileItemViewModel>()
            .ToArray();

        if (selected is { Length: > 0 })
        {
            await DeleteFilesWithConfirmationAsync(selected);
            return;
        }

        if (_viewModel.SelectedFile is not null)
            await DeleteFilesWithConfirmationAsync([_viewModel.SelectedFile]);
    }

    private async void ClearFileList_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await DeleteFilesWithConfirmationAsync(_viewModel.Files.ToArray());
    }

    private async Task DeleteFilesWithConfirmationAsync(IEnumerable<DataFileItemViewModel> files)
    {
        var items = files
            .Where(file => _viewModel.Files.Contains(file))
            .Distinct()
            .ToArray();

        if (items.Length == 0)
            return;

        var linkedSnapshots = _viewModel.GetSnapshotsUsingFiles(items);
        if (!await ConfirmFileDeletionAsync(items, linkedSnapshots))
            return;

        _viewModel.DeleteFiles(items);
    }

    private async Task<bool> ConfirmFileDeletionAsync(
        IReadOnlyList<DataFileItemViewModel> files,
        IReadOnlyList<PlotSnapshotViewModel> linkedSnapshots)
    {
        if (linkedSnapshots.Count == 0)
            return true;

        var confirmed = false;
        var dialog = new Window
        {
            Title = "Delete Data Files",
            Width = 460,
            Height = 360,
            MinWidth = 420,
            MinHeight = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            MinWidth = 86,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        deleteButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 86,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = new Grid
        {
            Margin = new Thickness(18),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                new TextBlock
                {
                    Text = "Some saved graphs use the selected data file(s). Deleting the data will also delete these graphs.",
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    [Grid.RowProperty] = 1,
                    Text = $"Data files: {string.Join(", ", files.Select(file => file.FileName))}",
                    Margin = new Thickness(0, 10, 0, 8),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.DimGray
                },
                new ScrollViewer
                {
                    [Grid.RowProperty] = 2,
                    Content = new TextBlock
                    {
                        Text = string.Join(Environment.NewLine, linkedSnapshots.Select(snapshot => $"- {snapshot.Name}")),
                        TextWrapping = TextWrapping.Wrap
                    }
                },
                new StackPanel
                {
                    [Grid.RowProperty] = 3,
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 14, 0, 0),
                    Children = { cancelButton, deleteButton }
                }
            }
        };

        await dialog.ShowDialog(this);
        return confirmed;
    }

    public async Task OpenProjectFileAsync(string path)
    {
        try
        {
            await _viewModel.ImportProjectAsync(path);
            RenderPlot(_viewModel.CurrentPlotConfig);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Could not import project: {ex.Message}";
        }
    }

    public async Task OpenStartupFileAsync(string path)
    {
        if (!File.Exists(path))
            return;

        if (string.Equals(Path.GetExtension(path), ".delimplot", StringComparison.OrdinalIgnoreCase))
        {
            await OpenProjectFileAsync(path);
            return;
        }

        await _viewModel.LoadFilesAsync([path]);
    }

    private void AutoScalePlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.CurrentPlotConfig is null)
            return;

        PlotControl.Plot.Axes.AutoScale();
        PlotControl.Refresh();
        _viewModel.StatusMessage = "Auto-scaled current plot.";
    }

    private async void About_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "About DelimPlot",
            Width = 360,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            MinWidth = 82
        };
        closeButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "DelimPlot", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock { Text = "Version 0.1.0" },
                new TextBlock { Text = "License: Apache-2.0" },
                new TextBlock { Text = "Plain-text column plotting for desktop workflows.", Foreground = Brushes.DimGray },
                closeButton
            }
        };

        await dialog.ShowDialog(this);
    }

    private void Exit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void DropZone_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DropZone_Drop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files is null)
            return;

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

        await _viewModel.LoadFilesAsync(paths);
        e.Handled = true;
    }

    private sealed record PlotShape(string FilePath, int XColumnIndex, string SeriesColumns)
    {
        public static PlotShape? FromConfig(PlotConfig? config)
        {
            if (config is null)
                return null;

            var seriesColumns = string.Join(",", config.Series.Select(series => series.YColumnIndex));
            return new PlotShape(config.DataFile.FilePath, config.XColumnIndex, seriesColumns);
        }
    }
}
