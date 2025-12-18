using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using AtomUI.Theme.Language;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VizAura.ViewModels;
using VizAura.Views;
using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace VizAura;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // 开启 AtomUI 的支持
        this.UseAtomUI(builder =>
        {
            builder.WithDefaultTheme(IThemeManager.DEFAULT_THEME_ID);
            builder.UseAlibabaSansFont(); // 配置字体
            builder.UseDesktopControls();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            // 注册全局异常处理器
            RegisterGlobalExceptionHandlers();
            
            var mainViewModel = Program.Services.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    /// <summary>
    /// 注册全局异常处理器
    /// </summary>
    private static void RegisterGlobalExceptionHandlers()
    {
        var logger = Program.Services.GetRequiredService<ILogger<App>>();
        
        // 1. AppDomain 未处理异常
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception ?? new Exception("未知异常");
            logger.LogCritical(exception, "AppDomain 未处理异常");
            ShowErrorWindow(exception);
        };
        
        // 2. 未观察的 Task 异常
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            logger.LogError(e.Exception, "未观察的 Task 异常");
            ShowErrorWindow(e.Exception);
            e.SetObserved();
        };
        
        // 3. Avalonia UI 线程异常
        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            logger.LogError(e.Exception, "UI 线程未处理异常");
            ShowErrorWindow(e.Exception);
            e.Handled = true;
        };
    }
    
    /// <summary>
    /// 显示错误窗口
    /// </summary>
    /// <param name="exception">异常对象</param>
    private static void ShowErrorWindow(Exception exception)
    {
        try
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var clipboard = (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow?.Clipboard 
                    : null);
                
                var viewModel = new ErrorWindowViewModel(exception, clipboard);
                var errorWindow = new ErrorWindow
                {
                    DataContext = viewModel
                };
                
                await errorWindow.ShowDialog(
                    (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime 
                        ? desktopLifetime.MainWindow 
                        : null) ?? throw new InvalidOperationException("主窗口未初始化"));
            });
        }
        catch (Exception ex)
        {
            var logger = Program.Services.GetRequiredService<ILogger<App>>();
            logger.LogCritical(ex, "显示错误窗口时发生异常");
        }
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