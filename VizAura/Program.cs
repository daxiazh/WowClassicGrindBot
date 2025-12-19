using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using Core;
using Core.Database;
using SharedLib;
using VizAura.MacOS;
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
        
        // === 应用级单例服务 (Singleton - 全局共享) ===
        
        // WowProcessInfo 提供者 (用于在验证阶段延迟设置进程信息)
        services.AddSingleton<IWowProcessInfoProvider, WowProcessInfoProvider>();
        
        // === 检测会话级服务 (Scoped - 每次重新检测时重新创建) ===
        // 说明: MainWindowViewModel 管理 IServiceScope,每次进入 Validating 状态时创建新 Scope
        //      重新检测时销毁旧 Scope (释放所有 Scoped 服务),创建新 Scope (全新环境)
        
        // WowProcessInfo - 一次检测会话对应一个 WoW 进程
        // 生命周期: 验证阶段通过 IWowProcessInfoProvider 设置,在当前 Scope 内有效
        services.AddScoped<WowProcessInfo>(sp =>
        {
            var provider = sp.GetRequiredService<IWowProcessInfoProvider>();
            if (provider.ProcessInfo == null)
                throw new InvalidOperationException("WowProcessInfo 尚未设置,请先完成验证流程");
            return provider.ProcessInfo;
        });
        
        // StartupClientVersion - 基于 WowProcessInfo.Version
        // 生命周期: 一次检测会话确定一个客户端版本,用于加载对应的数据配置
        services.AddScoped<StartupClientVersion>(sp =>
        {
            var processInfo = sp.GetRequiredService<WowProcessInfo>();
            return new StartupClientVersion(processInfo.Version);
        });
        
        // DataConfig - 基于 StartupClientVersion 加载对应版本的数据配置
        // 生命周期: 一次检测会话加载一次配置 (vanilla/tbc/wrath 等不同路径)
        services.AddScoped<DataConfig>(sp =>
        {
            var startupVersion = sp.GetRequiredService<StartupClientVersion>();
            return DataConfig.Load(startupVersion.Path);
        });
        
        // DataFrame[] - 从 frame_config.json 加载帧配置
        // 生命周期: 一次检测会话加载一次配置 (分辨率改变需要重新检测)
        services.AddScoped<DataFrame[]>(sp =>
        {
            var frames = FrameConfig.LoadFrames();
            if (frames.Length == 0)
                throw new InvalidOperationException("Frame 配置不存在或无效,请先完成 Frame 配置步骤");
            return frames;
        });
        
        // AddonDataSnapshot - Addon 数据快照,用于读取插件传递的游戏状态
        // 生命周期: 一次检测会话一个快照实例,在 Scope 销毁时释放
        services.AddScoped<AddonDataSnapshot>(sp =>
        {
            var frames = sp.GetRequiredService<DataFrame[]>();
            return new AddonDataSnapshot(frames);
        });
        
        // IAddonDataProvider - 绑定到 AddonDataSnapshot
        // 说明: PlayerReader 等组件依赖此接口读取 Addon 数据
        services.AddScoped<IAddonDataProvider>(sp => 
            sp.GetRequiredService<AddonDataSnapshot>());
        
        // WowScreenMacOS - macOS 屏幕捕获实例
        // 生命周期: 一次检测会话一个捕获实例,持有 native 资源,需要在 OnExit 中手动 Dispose
        //          Scope 销毁时会再次确保释放 (双重保险)
        services.AddScoped<WowScreenMacOS>(sp =>
        {
            var processInfo = sp.GetRequiredService<WowProcessInfo>();
            var frames = sp.GetRequiredService<DataFrame[]>();
            var rect = MacOSWindowHelper.GetWindowBounds((int)processInfo.WindowId);
            return new WowScreenMacOS(processInfo.WindowId, rect, frames);
        });
        
        // PlayerReader - 玩家状态数据读取器
        // 生命周期: 一次检测会话一个读取器实例,依赖 IAddonDataProvider
        services.AddScoped<PlayerReader>();
        
        // HekiliReader - Hekili 技能推荐读取器
        // 生命周期: 一次检测会话一个读取器实例,依赖 IAddonDataProvider
        services.AddScoped<HekiliReader>();
        
        // WorkViewModel - 工作状态的 ViewModel
        // 生命周期: 一次检测会话一个实例,在 Running 状态时活跃
        services.AddScoped<WorkViewModel>();
        
        // === 应用级单例服务 (Singleton - 全局共享,无状态) ===
        
        services.AddSingleton<CancellationTokenSource>();
        
        // 数据库服务 - 无状态查询服务,全局共享
        // 说明: 虽然依赖 DataConfig (Scoped),但数据库本身是无状态的,可以安全共享
        //      DI 容器会在第一次解析时从当前 Scope 获取 DataConfig
        services.AddSingleton<CreatureDB>();
        services.AddSingleton<WorldMapAreaDB>();
        services.AddSingleton<FactionTemplateDB>();
        services.AddSingleton<AreaDB>();
        services.AddSingleton<SpellDB>();
        
        // Addon 组件服务 - 无状态工具类,全局共享
        // 说明: AddonBits/SpellInRange/Stance 只是数据解析工具,不持有状态
        services.AddSingleton<AddonBits>();
        services.AddSingleton<SpellInRange>();
        services.AddSingleton<Stance>();

        return services.BuildServiceProvider();
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
