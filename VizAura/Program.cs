using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using VizAura.ViewModels;

namespace VizAura;

sealed class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        Services = ConfigureServices();
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddTransient<MainWindowViewModel>();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services.BuildServiceProvider();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
