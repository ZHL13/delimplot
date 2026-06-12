using Avalonia;
using Avalonia.Fonts.Inter;
using System;

namespace DelimPlot.App;

internal static class Program
{
    public static IReadOnlyList<string> StartupArgs { get; private set; } = [];

    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
