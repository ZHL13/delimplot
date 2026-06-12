using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DelimPlot.App.Views;

namespace DelimPlot.App;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;

            var startupPath = Program.StartupArgs.FirstOrDefault(File.Exists);
            if (startupPath is not null)
                window.Opened += async (_, _) => await window.OpenStartupFileAsync(startupPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
