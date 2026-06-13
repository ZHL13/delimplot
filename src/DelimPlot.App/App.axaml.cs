using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DelimPlot.App.Views;

namespace DelimPlot.App;

public sealed partial class App : Application
{
    private readonly Queue<string> _pendingStartupFiles = new();
    private MainWindow? _mainWindow;
    private bool _mainWindowOpened;
    private bool _openingStartupFiles;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            _mainWindow = window;
            desktop.MainWindow = window;

            window.Opened += (_, _) =>
            {
                _mainWindowOpened = true;
                _ = OpenPendingStartupFilesAsync();
            };

            QueueStartupFiles(desktop.Args);
            SubscribeFileActivation();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SubscribeFileActivation()
    {
        var activatable = this.TryGetFeature<IActivatableLifetime>();
        if (activatable is null)
            return;

        activatable.Activated += (_, args) =>
        {
            if (args is not FileActivatedEventArgs fileArgs)
                return;

            var paths = fileArgs.Files
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();

            Dispatcher.UIThread.Post(() => QueueStartupFiles(paths));
        };
    }

    private void QueueStartupFiles(IEnumerable<string>? paths)
    {
        if (paths is null)
            return;

        foreach (var path in paths.Where(File.Exists))
            _pendingStartupFiles.Enqueue(path);

        _ = OpenPendingStartupFilesAsync();
    }

    private async Task OpenPendingStartupFilesAsync()
    {
        if (_openingStartupFiles || !_mainWindowOpened || _mainWindow is null)
            return;

        _openingStartupFiles = true;
        try
        {
            while (_pendingStartupFiles.TryDequeue(out var path))
                await _mainWindow.OpenStartupFileAsync(path);
        }
        finally
        {
            _openingStartupFiles = false;
        }
    }
}
