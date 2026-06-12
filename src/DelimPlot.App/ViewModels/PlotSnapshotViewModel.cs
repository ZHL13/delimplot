using DelimPlot.Core.Models;
using Avalonia.Media.Imaging;

namespace DelimPlot.App.ViewModels;

public sealed class PlotSnapshotViewModel : ObservableObject
{
    private readonly Action<PlotSnapshotViewModel> _delete;
    private readonly string _slotName;
    private PlotSnapshot? _snapshot;
    private Bitmap? _thumbnail;
    private byte[]? _thumbnailPngBytes;

    public PlotSnapshotViewModel(string name, Action<PlotSnapshotViewModel> delete)
    {
        _slotName = name;
        _delete = delete;
        DeleteCommand = new RelayCommand(_ => _delete(this));
    }

    public PlotSnapshotViewModel(PlotSnapshot snapshot, Action<PlotSnapshotViewModel> delete, string? slotName = null)
        : this(slotName ?? snapshot.Name, delete)
    {
        _snapshot = snapshot;
    }

    public PlotSnapshot? Snapshot
    {
        get => _snapshot;
    }

    public string SlotName => _slotName;
    public string Name => Snapshot?.Name ?? _slotName;
    public bool IsEmpty => Snapshot is null;
    public bool HasThumbnail => Thumbnail is not null;
    public bool HasStatusText => !HasThumbnail;
    public string StatusText => IsEmpty ? "Empty" : "No Preview";
    public Bitmap? Thumbnail => _thumbnail;
    public byte[]? ThumbnailPngBytes => _thumbnailPngBytes;
    public RelayCommand DeleteCommand { get; }

    public void BindSnapshot(PlotSnapshot snapshot)
    {
        _snapshot = snapshot;
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatusText));
    }

    public void SetThumbnail(byte[] pngBytes)
    {
        _thumbnailPngBytes = pngBytes.ToArray();
        using var stream = new MemoryStream(_thumbnailPngBytes);
        var nextThumbnail = new Bitmap(stream);
        var previousThumbnail = _thumbnail;
        _thumbnail = nextThumbnail;
        previousThumbnail?.Dispose();

        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(StatusText));
    }
}
