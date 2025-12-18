using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using Core;
using Core.Database;
using SharedLib;
using VizAura.Models;
using VizAura.Services;
using VizAura.ViewModels;

namespace VizAura;

static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Services = ConfigureServices();
            
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // 程序启动阶段的异常 (DI 配置错误、Avalonia 初始化失败等)
            Console.Error.WriteLine("=== 程序启动失败 ===");
            Console.Error.WriteLine($"类型: {ex.GetType().FullName}");
            Console.Error.WriteLine($"消息: {ex.Message}");
            Console.Error.WriteLine($"堆栈: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine("\n=== 内部异常 ===");
                Console.Error.WriteLine($"类型: {ex.InnerException.GetType().FullName}");
                Console.Error.WriteLine($"消息: {ex.InnerException.Message}");
                Console.Error.WriteLine($"堆栈: {ex.InnerException.StackTrace}");
            }
            
            Environment.Exit(1);
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<WowProcessStepViewModel>();
        services.AddTransient<AddonStepViewModel>();
        services.AddTransient<FrameStepViewModel>();
        
        // 日志服务
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // 注册非泛型 ILogger (AreaDB 需要)
        services.AddSingleton<ILogger>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("VizAura"));
        
        // WowProcessInfo 提供者 (用于延迟提供运行时依赖)
        services.AddSingleton<IWowProcessInfoProvider, WowProcessInfoProvider>();
        
        // WowProcessInfo (通过 provider 延迟获取). IWowProcessInfoProvider 在重新检测时可能会更新, 比如重启了 WoW
        services.AddScoped<WowProcessInfo>(sp =>
        {
            var provider = sp.GetRequiredService<IWowProcessInfoProvider>();
            if (provider.ProcessInfo == null)
                throw new InvalidOperationException("WowProcessInfo 尚未设置,请先完成验证流程");
            return provider.ProcessInfo;
        });
        
        // 基于 WowProcessInfo 的依赖链
        services.AddSingleton<StartupClientVersion>(sp =>
        {
            var processInfo = sp.GetRequiredService<WowProcessInfo>();
            return new StartupClientVersion(processInfo.Version);
        });
        
        services.AddSingleton<DataConfig>(sp =>
        {
            var startupVersion = sp.GetRequiredService<StartupClientVersion>();
            return DataConfig.Load(startupVersion.Path);
        });
        
        services.AddSingleton<CancellationTokenSource>();
        
        // 数据库服务 (DI 自动解析 DataConfig 依赖)
        services.AddSingleton<CreatureDB>();
        services.AddSingleton<WorldMapAreaDB>();

        return services.BuildServiceProvider();
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
