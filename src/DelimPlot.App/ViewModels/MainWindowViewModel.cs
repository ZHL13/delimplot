using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DelimPlot.Core.Models;
using DelimPlot.Core.Parsing;

namespace DelimPlot.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string ProjectFormat = "DelimPlot.Project";
    private const int ProjectVersion = 1;

    private static readonly JsonSerializerOptions ProjectJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TextDataParser _parser = new();
    private DataFileItemViewModel? _selectedFile;
    private ColumnOption? _selectedXColumn;
    private PlotSnapshotViewModel? _selectedSnapshot;
    private string _statusMessage = "Ready";
    private string _plotTitle = string.Empty;
    private string _xAxisLabel = string.Empty;
    private string _yAxisLabel = string.Empty;
    private bool _isLoading;
    private bool _canSavePlot;
    private bool _loadingConfig;
    private int _plotCounter;

    public MainWindowViewModel()
    {
        NewPlotCommand = new RelayCommand(_ => NewPlotSlot());
        AddSeriesCommand = new RelayCommand(_ => AddSeries(), _ => SelectedFile is not null);
        ConfirmPlotCommand = new RelayCommand(_ => ConfirmPlot(), _ => CurrentPlotConfig is not null);
        PlotAsNewGraphCommand = new RelayCommand(_ => PlotAsNewGraph(), _ => CurrentPlotConfig is not null);
        DeleteSelectedSnapshotCommand = new RelayCommand(_ =>
        {
            if (SelectedSnapshot is not null)
                DeleteSnapshot(SelectedSnapshot);
        }, _ => SelectedSnapshot is not null);
    }

    public event EventHandler<PlotConfig?>? PlotChanged;
    public event EventHandler? ThumbnailRefreshRequested;

    public ObservableCollection<DataFileItemViewModel> Files { get; } = [];
    public ObservableCollection<ColumnOption> Columns { get; } = [];
    public ObservableCollection<PreviewRowViewModel> PreviewRows { get; } = [];
    public ObservableCollection<PlotSeriesConfigViewModel> Series { get; } = [];
    public ObservableCollection<PlotSnapshotViewModel> Snapshots { get; } = [];

    public IReadOnlyList<string> ColorOptions { get; } =
    [
        "#2563EB",
        "#DC2626",
        "#059669",
        "#D97706",
        "#7C3AED",
        "#0891B2",
        "#111827",
        "#DB2777"
    ];

    public RelayCommand NewPlotCommand { get; }
    public RelayCommand AddSeriesCommand { get; }
    public RelayCommand ConfirmPlotCommand { get; }
    public RelayCommand PlotAsNewGraphCommand { get; }
    public RelayCommand DeleteSelectedSnapshotCommand { get; }

    public PlotConfig? CurrentPlotConfig { get; private set; }

    public DataFileItemViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!SetProperty(ref _selectedFile, value))
                return;

            if (!_loadingConfig)
                LoadFileConfiguration(value?.DataFile);

            AddSeriesCommand.RaiseCanExecuteChanged();
        }
    }

    public ColumnOption? SelectedXColumn
    {
        get => _selectedXColumn;
        set
        {
            if (SetProperty(ref _selectedXColumn, value))
            {
                XAxisLabel = value?.Name ?? string.Empty;
                NotifyPlotConfigChanged();
            }
        }
    }

    public PlotSnapshotViewModel? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (!SetProperty(ref _selectedSnapshot, value))
                return;

            if (value is not null && !_loadingConfig)
            {
                if (value.Snapshot is null)
                    StatusMessage = $"{value.Name} is empty. Select a file and columns, then confirm.";
                else
                    LoadSnapshot(value.Snapshot);
            }

            DeleteSelectedSnapshotCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(SuggestedPngName));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string PlotTitle
    {
        get => _plotTitle;
        set
        {
            if (SetProperty(ref _plotTitle, value))
                NotifyPlotConfigChanged();
        }
    }

    public string XAxisLabel
    {
        get => _xAxisLabel;
        set
        {
            if (SetProperty(ref _xAxisLabel, value))
                NotifyPlotConfigChanged();
        }
    }

    public string YAxisLabel
    {
        get => _yAxisLabel;
        set
        {
            if (SetProperty(ref _yAxisLabel, value))
                NotifyPlotConfigChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool CanSavePlot
    {
        get => _canSavePlot;
        private set => SetProperty(ref _canSavePlot, value);
    }

    public bool CanExportProject => Files.Count > 0 || Snapshots.Count > 0;
    public bool HasFiles => Files.Count > 0;
    public bool HasSnapshots => Snapshots.Count > 0;

    public string SuggestedPngName
    {
        get
        {
            var name = SelectedSnapshot?.Name ?? "Plot";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                name = name.Replace(invalidChar, '_');

            return $"DelimPlot_{name}.png";
        }
    }

    public async Task LoadFilesAsync(IEnumerable<string> paths)
    {
        var filePaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (filePaths.Length == 0)
        {
            StatusMessage = "No data files selected.";
            return;
        }

        IsLoading = true;

        try
        {
            foreach (var path in filePaths)
            {
                if (Files.Any(file => string.Equals(file.DataFile.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    StatusMessage = $"Parsing {Path.GetFileName(path)}...";
                    var dataFile = await _parser.ParseAsync(path);
                    var item = new DataFileItemViewModel(dataFile, DeleteFile);
                    Files.Add(item);

                    if (SelectedFile is null)
                        SelectedFile = item;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Could not load {Path.GetFileName(path)}: {ex.Message}";
                }
            }

            if (SelectedFile is not null)
                StatusMessage = $"Loaded {Files.Count} file(s).";

            OnWorkspaceContentsChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ExportProjectAsync(string path)
    {
        if (!CanExportProject)
        {
            StatusMessage = "Nothing to export.";
            return;
        }

        var dataFiles = Files.Select(file => file.DataFile).ToList();
        foreach (var snapshotDataFile in Snapshots
            .Select(snapshot => snapshot.Snapshot?.PlotConfig.DataFile)
            .Where(dataFile => dataFile is not null)
            .Cast<DataFile>())
        {
            if (dataFiles.All(dataFile => !string.Equals(dataFile.FilePath, snapshotDataFile.FilePath, StringComparison.OrdinalIgnoreCase)))
                dataFiles.Add(snapshotDataFile);
        }

        var fileIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectFiles = new List<ProjectDataFile>();

        for (var i = 0; i < dataFiles.Count; i++)
        {
            var dataFile = dataFiles[i];
            if (!File.Exists(dataFile.FilePath))
                throw new FileNotFoundException($"The source data file no longer exists: {dataFile.FilePath}", dataFile.FilePath);

            var id = $"file-{i + 1}";
            fileIds[dataFile.FilePath] = id;
            projectFiles.Add(new ProjectDataFile
            {
                Id = id,
                FileName = dataFile.FileName,
                OriginalPath = dataFile.FilePath,
                ContentBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(dataFile.FilePath))
            });
        }

        var project = new ProjectFile
        {
            Format = ProjectFormat,
            Version = ProjectVersion,
            PlotCounter = _plotCounter,
            SelectedFileId = SelectedFile is not null && fileIds.TryGetValue(SelectedFile.DataFile.FilePath, out var selectedFileId)
                ? selectedFileId
                : null,
            SelectedSnapshotIndex = SelectedSnapshot is null ? -1 : Snapshots.IndexOf(SelectedSnapshot),
            Files = projectFiles,
            Graphs = Snapshots.Select(snapshot => ToProjectGraph(snapshot, fileIds)).ToList()
        };

        var json = JsonSerializer.Serialize(project, ProjectJsonOptions);
        await File.WriteAllTextAsync(path, json);
        StatusMessage = $"Exported {Path.GetFileName(path)}.";
    }

    public async Task ImportProjectAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var project = JsonSerializer.Deserialize<ProjectFile>(json, ProjectJsonOptions)
            ?? throw new InvalidDataException("The project file is empty.");

        if (!string.Equals(project.Format, ProjectFormat, StringComparison.Ordinal) || project.Version > ProjectVersion)
            throw new InvalidDataException("This is not a supported DelimPlot project file.");

        IsLoading = true;
        var restoredFiles = new Dictionary<string, DataFileItemViewModel>();
        var restoreRoot = CreateImportDirectory();

        _loadingConfig = true;
        try
        {
            Files.Clear();
            Columns.Clear();
            PreviewRows.Clear();
            Series.Clear();
            Snapshots.Clear();
            CurrentPlotConfig = null;
            CanSavePlot = false;
            SelectedFile = null;
            SelectedXColumn = null;
            SelectedSnapshot = null;
            PlotTitle = string.Empty;
            XAxisLabel = string.Empty;
            YAxisLabel = string.Empty;
            _plotCounter = 0;

            for (var i = 0; i < project.Files.Count; i++)
            {
                var projectFile = project.Files[i];
                if (string.IsNullOrWhiteSpace(projectFile.Id) || string.IsNullOrWhiteSpace(projectFile.ContentBase64))
                    continue;

                var fileDirectory = Path.Combine(restoreRoot, SanitizeFileName(projectFile.Id, $"file-{i + 1}"));
                Directory.CreateDirectory(fileDirectory);

                var fileName = SanitizeFileName(projectFile.FileName, $"data-{i + 1}.dat");
                var restoredPath = Path.Combine(fileDirectory, fileName);
                await File.WriteAllBytesAsync(restoredPath, Convert.FromBase64String(projectFile.ContentBase64));

                var dataFile = await _parser.ParseAsync(restoredPath);
                var item = new DataFileItemViewModel(dataFile, DeleteFile);
                Files.Add(item);
                restoredFiles[projectFile.Id] = item;
            }

            foreach (var graph in project.Graphs)
            {
                var slotName = string.IsNullOrWhiteSpace(graph.SlotName)
                    ? $"Plot {++_plotCounter}"
                    : graph.SlotName;

                PlotSnapshotViewModel item;
                if (graph.IsEmpty || string.IsNullOrWhiteSpace(graph.FileId) || !restoredFiles.TryGetValue(graph.FileId, out var file))
                {
                    item = new PlotSnapshotViewModel(slotName, DeleteSnapshot);
                }
                else
                {
                    var config = new PlotConfig
                    {
                        DataFile = file.DataFile,
                        XColumnIndex = graph.XColumnIndex,
                        Title = graph.Title,
                        XAxisLabel = graph.XAxisLabel,
                        YAxisLabel = graph.YAxisLabel,
                        Series = graph.Series.Select(series => new PlotSeriesConfig
                        {
                            YColumnIndex = series.YColumnIndex,
                            Style = series.Style,
                            Color = series.Color,
                            LineWidth = series.LineWidth,
                            MarkerSize = series.MarkerSize
                        }).ToList()
                    };

                    item = new PlotSnapshotViewModel(new PlotSnapshot
                    {
                        Name = string.IsNullOrWhiteSpace(graph.Name) ? ResolveSnapshotName(slotName, config) : graph.Name,
                        PlotConfig = config
                    }, DeleteSnapshot, slotName);
                }

                if (!string.IsNullOrWhiteSpace(graph.ThumbnailPngBase64))
                    item.SetThumbnail(Convert.FromBase64String(graph.ThumbnailPngBase64));

                Snapshots.Add(item);
            }

            _plotCounter = Math.Max(project.PlotCounter, Snapshots.Count);
        }
        finally
        {
            _loadingConfig = false;
            IsLoading = false;
        }

        SelectedFile = project.SelectedFileId is not null && restoredFiles.TryGetValue(project.SelectedFileId, out var selectedFile)
            ? selectedFile
            : Files.FirstOrDefault();

        SelectedSnapshot = project.SelectedSnapshotIndex >= 0 && project.SelectedSnapshotIndex < Snapshots.Count
            ? Snapshots[project.SelectedSnapshotIndex]
            : Snapshots.FirstOrDefault();

        OnWorkspaceContentsChanged();
        StatusMessage = $"Imported {Path.GetFileName(path)}.";
    }

    public void DeleteSnapshots(IEnumerable<PlotSnapshotViewModel> snapshots)
    {
        var items = snapshots
            .Where(snapshot => Snapshots.Contains(snapshot))
            .Distinct()
            .ToArray();

        if (items.Length == 0)
            return;

        var selectedWasDeleted = SelectedSnapshot is not null && items.Contains(SelectedSnapshot);
        foreach (var item in items)
            Snapshots.Remove(item);

        if (selectedWasDeleted)
        {
            SelectedSnapshot = Snapshots.FirstOrDefault();
            if (SelectedSnapshot is null)
                NotifyPlotConfigChanged();
        }

        StatusMessage = items.Length == 1 ? $"Deleted {items[0].Name}." : $"Deleted {items.Length} graphs.";
        OnWorkspaceContentsChanged();
    }

    public void DeleteFiles(IEnumerable<DataFileItemViewModel> files)
    {
        var items = files
            .Where(file => Files.Contains(file))
            .Distinct()
            .ToArray();

        if (items.Length == 0)
            return;

        var linkedSnapshots = GetSnapshotsUsingFiles(items).ToArray();
        var selectedIndex = SelectedFile is null ? 0 : Files.IndexOf(SelectedFile);
        var selectedWasDeleted = SelectedFile is not null && items.Contains(SelectedFile);
        var selectedSnapshotWasDeleted = SelectedSnapshot is not null && linkedSnapshots.Contains(SelectedSnapshot);

        foreach (var snapshot in linkedSnapshots)
            Snapshots.Remove(snapshot);

        foreach (var item in items)
            Files.Remove(item);

        if (selectedWasDeleted)
        {
            SelectedFile = Files.Count == 0
                ? null
                : Files[Math.Clamp(selectedIndex, 0, Files.Count - 1)];
        }

        if (selectedSnapshotWasDeleted)
        {
            SelectedSnapshot = Snapshots.FirstOrDefault();
            if (SelectedSnapshot is null)
                NotifyPlotConfigChanged();
        }

        StatusMessage = FormatDeleteFilesStatus(items, linkedSnapshots);
        OnWorkspaceContentsChanged();
    }

    public void ClearFiles()
    {
        if (Files.Count == 0)
            return;

        DeleteFiles(Files.ToArray());
    }

    public IReadOnlyList<PlotSnapshotViewModel> GetSnapshotsUsingFiles(IEnumerable<DataFileItemViewModel> files)
    {
        var filePaths = files
            .Select(file => file.DataFile.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (filePaths.Count == 0)
            return [];

        return Snapshots
            .Where(snapshot => snapshot.Snapshot?.PlotConfig.DataFile.FilePath is string path && filePaths.Contains(path))
            .ToArray();
    }

    public void ClearSnapshots()
    {
        if (Snapshots.Count == 0)
            return;

        var count = Snapshots.Count;
        Snapshots.Clear();
        SelectedSnapshot = null;
        NotifyPlotConfigChanged();
        StatusMessage = $"Cleared {count} graphs.";
        OnWorkspaceContentsChanged();
    }

    public void NotifyPlotConfigChanged()
    {
        if (_loadingConfig)
            return;

        CurrentPlotConfig = BuildCurrentPlotConfig();
        CanSavePlot = CurrentPlotConfig is not null;
        ConfirmPlotCommand.RaiseCanExecuteChanged();
        PlotAsNewGraphCommand.RaiseCanExecuteChanged();
        PlotChanged?.Invoke(this, CurrentPlotConfig);
    }

    public bool CanUpdateSelectedSnapshotThumbnail()
    {
        return SelectedSnapshot is { IsEmpty: false } && CurrentPlotConfig is not null;
    }

    public void UpdateSelectedSnapshotThumbnail(byte[] pngBytes)
    {
        if (!CanUpdateSelectedSnapshotThumbnail())
            return;

        SelectedSnapshot?.SetThumbnail(pngBytes);
    }

    public void RemoveSeries(PlotSeriesConfigViewModel series)
    {
        if (Series.Count <= 1)
            return;

        Series.Remove(series);
        UpdateSeriesRemoveState();
        NotifyPlotConfigChanged();
    }

    private void DeleteFile(DataFileItemViewModel file)
    {
        DeleteFiles([file]);
    }

    private void AddSeries()
    {
        if (SelectedFile is null || Columns.Count == 0)
            return;

        var nextColumn = Columns
            .FirstOrDefault(column => column.Index != SelectedXColumn?.Index && Series.All(series => series.SelectedYColumn?.Index != column.Index))
            ?? Columns.First();

        Series.Add(new PlotSeriesConfigViewModel(this, new PlotSeriesConfig
        {
            YColumnIndex = nextColumn.Index,
            Color = ColorOptions[Series.Count % ColorOptions.Count]
        }));

        UpdateSeriesRemoveState();
        NotifyPlotConfigChanged();
    }

    private void NewPlotSlot()
    {
        var item = new PlotSnapshotViewModel($"Plot {++_plotCounter}", DeleteSnapshot);
        Snapshots.Add(item);
        SelectedSnapshot = item;
        StatusMessage = $"{item.Name} is empty. Select a file and columns, then confirm.";
        OnWorkspaceContentsChanged();
    }

    private void ConfirmPlot()
    {
        var config = CurrentPlotConfig?.Clone();
        if (config is null)
            return;

        if (SelectedSnapshot is not null)
        {
            var wasEmpty = SelectedSnapshot.IsEmpty;
            SelectedSnapshot.BindSnapshot(new PlotSnapshot
            {
                Name = ResolveSnapshotName(SelectedSnapshot.SlotName, config),
                PlotConfig = config
            });
            StatusMessage = wasEmpty ? $"Created {SelectedSnapshot.Name}." : $"Updated {SelectedSnapshot.Name}.";
            ThumbnailRefreshRequested?.Invoke(this, EventArgs.Empty);
            OnWorkspaceContentsChanged();
            return;
        }

        var slotName = $"Plot {++_plotCounter}";
        var snapshot = new PlotSnapshot
        {
            Name = ResolveSnapshotName(slotName, config),
            PlotConfig = config
        };
        var item = new PlotSnapshotViewModel(snapshot, DeleteSnapshot, slotName);
        Snapshots.Add(item);
        SelectedSnapshot = item;
        StatusMessage = $"Created {item.Name}.";
        ThumbnailRefreshRequested?.Invoke(this, EventArgs.Empty);
        OnWorkspaceContentsChanged();
    }

    private void PlotAsNewGraph()
    {
        var config = CurrentPlotConfig?.Clone();
        if (config is null)
            return;

        var slotName = $"Plot {++_plotCounter}";
        var snapshot = new PlotSnapshot
        {
            Name = ResolveSnapshotName(slotName, config),
            PlotConfig = config
        };

        var item = new PlotSnapshotViewModel(snapshot, DeleteSnapshot, slotName);
        Snapshots.Add(item);
        SelectedSnapshot = item;
        StatusMessage = $"Created {item.Name}.";
        ThumbnailRefreshRequested?.Invoke(this, EventArgs.Empty);
        OnWorkspaceContentsChanged();
    }

    private void DeleteSnapshot(PlotSnapshotViewModel snapshot)
    {
        DeleteSnapshots([snapshot]);
    }

    private void LoadFileConfiguration(DataFile? dataFile)
    {
        _loadingConfig = true;
        try
        {
            Columns.Clear();
            PreviewRows.Clear();
            Series.Clear();

            if (dataFile is null)
            {
                PlotTitle = string.Empty;
                XAxisLabel = string.Empty;
                YAxisLabel = string.Empty;
            }
            else
            {
                foreach (var column in dataFile.Columns)
                    Columns.Add(new ColumnOption(column.Index, column.Name));

                PreviewRows.Add(new PreviewRowViewModel(dataFile.Columns.Select(column => column.Name)));
                foreach (var row in dataFile.PreviewRows)
                    PreviewRows.Add(new PreviewRowViewModel(row));

                SelectedXColumn = Columns.FirstOrDefault();
                var defaultYColumn = Columns.Skip(1).FirstOrDefault() ?? Columns.FirstOrDefault();

                if (defaultYColumn is not null)
                {
                    Series.Add(new PlotSeriesConfigViewModel(this, new PlotSeriesConfig
                    {
                        YColumnIndex = defaultYColumn.Index,
                        Color = ColorOptions[0]
                    }));
                }

                PlotTitle = dataFile.FileName;
                XAxisLabel = SelectedXColumn?.Name ?? string.Empty;
                YAxisLabel = defaultYColumn?.Name ?? string.Empty;
            }
        }
        finally
        {
            _loadingConfig = false;
        }

        UpdateSeriesRemoveState();
        NotifyPlotConfigChanged();
    }

    private void LoadSnapshot(PlotSnapshot snapshot)
    {
        _loadingConfig = true;
        try
        {
            var dataFile = snapshot.PlotConfig.DataFile;
            var matchingFile = Files.FirstOrDefault(file =>
                string.Equals(file.DataFile.FilePath, dataFile.FilePath, StringComparison.OrdinalIgnoreCase));

            if (matchingFile is not null)
                SelectedFile = matchingFile;

            Columns.Clear();
            PreviewRows.Clear();
            Series.Clear();

            foreach (var column in dataFile.Columns)
                Columns.Add(new ColumnOption(column.Index, column.Name));

            PreviewRows.Add(new PreviewRowViewModel(dataFile.Columns.Select(column => column.Name)));
            foreach (var row in dataFile.PreviewRows)
                PreviewRows.Add(new PreviewRowViewModel(row));

            SelectedXColumn = Columns.FirstOrDefault(column => column.Index == snapshot.PlotConfig.XColumnIndex)
                ?? Columns.FirstOrDefault();

            PlotTitle = snapshot.PlotConfig.Title;
            XAxisLabel = snapshot.PlotConfig.XAxisLabel;
            YAxisLabel = snapshot.PlotConfig.YAxisLabel;

            foreach (var series in snapshot.PlotConfig.Series)
                Series.Add(new PlotSeriesConfigViewModel(this, series));
        }
        finally
        {
            _loadingConfig = false;
        }

        UpdateSeriesRemoveState();
        NotifyPlotConfigChanged();
    }

    private PlotConfig? BuildCurrentPlotConfig()
    {
        if (SelectedFile is null || SelectedXColumn is null || Series.Count == 0)
            return null;

        var series = Series
            .Where(item => item.SelectedYColumn is not null)
            .Select(item => item.ToConfig())
            .ToList();

        if (series.Count == 0)
            return null;

        return new PlotConfig
        {
            DataFile = SelectedFile.DataFile,
            XColumnIndex = SelectedXColumn.Index,
            Series = series,
            Title = PlotTitle,
            XAxisLabel = XAxisLabel,
            YAxisLabel = YAxisLabel
        };
    }

    private void UpdateSeriesRemoveState()
    {
        foreach (var series in Series)
            series.CanRemove = Series.Count > 1;
    }

    private void OnWorkspaceContentsChanged()
    {
        OnPropertyChanged(nameof(CanExportProject));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasSnapshots));
        DeleteSelectedSnapshotCommand.RaiseCanExecuteChanged();
    }

    private static string ResolveSnapshotName(string slotName, PlotConfig config)
    {
        var title = config.Title.Trim();
        if (string.IsNullOrWhiteSpace(title) || string.Equals(title, config.DataFile.FileName, StringComparison.Ordinal))
            return slotName;

        return title;
    }

    private static string FormatDeleteFilesStatus(
        IReadOnlyCollection<DataFileItemViewModel> files,
        IReadOnlyCollection<PlotSnapshotViewModel> linkedSnapshots)
    {
        var fileText = files.Count == 1 ? files.First().FileName : $"{files.Count} files";
        if (linkedSnapshots.Count == 0)
            return $"Removed {fileText}.";

        var graphText = linkedSnapshots.Count == 1 ? linkedSnapshots.First().Name : $"{linkedSnapshots.Count} graphs";
        return $"Removed {fileText} and {graphText}.";
    }

    private static ProjectGraph ToProjectGraph(PlotSnapshotViewModel snapshot, IReadOnlyDictionary<string, string> fileIds)
    {
        var graph = new ProjectGraph
        {
            Name = snapshot.Name,
            SlotName = snapshot.SlotName,
            IsEmpty = snapshot.IsEmpty,
            ThumbnailPngBase64 = snapshot.ThumbnailPngBytes is { Length: > 0 } pngBytes
                ? Convert.ToBase64String(pngBytes)
                : null
        };

        var config = snapshot.Snapshot?.PlotConfig;
        if (config is null)
            return graph;

        if (!fileIds.TryGetValue(config.DataFile.FilePath, out var fileId))
            throw new InvalidDataException($"Could not export graph '{snapshot.Name}' because its data file is missing.");

        graph.IsEmpty = false;
        graph.FileId = fileId;
        graph.XColumnIndex = config.XColumnIndex;
        graph.Title = config.Title;
        graph.XAxisLabel = config.XAxisLabel;
        graph.YAxisLabel = config.YAxisLabel;
        graph.Series = config.Series.Select(series => new ProjectSeries
        {
            YColumnIndex = series.YColumnIndex,
            Style = series.Style,
            Color = series.Color,
            LineWidth = series.LineWidth,
            MarkerSize = series.MarkerSize
        }).ToList();

        return graph;
    }

    private static string CreateImportDirectory()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            baseDirectory = Path.GetTempPath();

        var directory = Path.Combine(
            baseDirectory,
            "DelimPlot",
            "ImportedProjects",
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string SanitizeFileName(string? value, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(value) ? fallback : value;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            name = name.Replace(invalidChar, '_');

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private sealed class ProjectFile
    {
        public string Format { get; set; } = ProjectFormat;
        public int Version { get; set; } = ProjectVersion;
        public int PlotCounter { get; set; }
        public string? SelectedFileId { get; set; }
        public int SelectedSnapshotIndex { get; set; } = -1;
        public List<ProjectDataFile> Files { get; set; } = [];
        public List<ProjectGraph> Graphs { get; set; } = [];
    }

    private sealed class ProjectDataFile
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string ContentBase64 { get; set; } = string.Empty;
    }

    private sealed class ProjectGraph
    {
        public string Name { get; set; } = string.Empty;
        public string SlotName { get; set; } = string.Empty;
        public bool IsEmpty { get; set; }
        public string? FileId { get; set; }
        public int XColumnIndex { get; set; }
        public string Title { get; set; } = string.Empty;
        public string XAxisLabel { get; set; } = string.Empty;
        public string YAxisLabel { get; set; } = string.Empty;
        public List<ProjectSeries> Series { get; set; } = [];
        public string? ThumbnailPngBase64 { get; set; }
    }

    private sealed class ProjectSeries
    {
        public int YColumnIndex { get; set; }
        public PlotSeriesStyle Style { get; set; } = PlotSeriesStyle.Line;
        public string Color { get; set; } = "#2563EB";
        public double LineWidth { get; set; } = 2;
        public double MarkerSize { get; set; } = 5;
    }
}
