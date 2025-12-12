using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VizAura.ViewModels;
using VizAura.Views;

namespace VizAura;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var mainViewModel = Program.Services.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}