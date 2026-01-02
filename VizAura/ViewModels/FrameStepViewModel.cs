using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using VizAura.Models;
using VizAura.Services;

namespace VizAura.ViewModels;

/// <summary>
/// Frame 配置检查步骤 ViewModel
/// </summary>
public sealed partial class FrameStepViewModel : ObservableObject, IStepViewModel
{
    private readonly ILogger<FrameStepViewModel> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IWowProcessInfoProvider processInfoProvider;

    public string StepId => "frame_configuration";
    public string StepName => "检查 Frame 配置";

    [ObservableProperty]
    private ValidationStatus status = ValidationStatus.Pending;

    partial void OnStatusChanged(ValidationStatus value)
    {
        OnPropertyChanged(nameof(ShowSuccessView));
        OnPropertyChanged(nameof(ShowFailureView));
    }

    public bool ShowSuccessView => Status == ValidationStatus.Success;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private int frameCount;

    [ObservableProperty]
    private string? addonVersion;

    [ObservableProperty]
    private string? configPath;

    public string SuccessMessage => $"✓ 配置文件: {ConfigPath}\n帧数: {FrameCount}, 插件版本: {AddonVersion}";

    public bool ShowFailureView => Status == ValidationStatus.Failed;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool canConfigure;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="processInfoProvider">WoW 进程信息提供者</param>
    public FrameStepViewModel(
        ILogger<FrameStepViewModel> logger,
        IServiceProvider serviceProvider,
        IWowProcessInfoProvider processInfoProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.processInfoProvider = processInfoProvider;
    }

    /// <summary>
    /// 执行 Frame 配置检查
    /// </summary>
    /// <param name="context">上一步传递的 WoW 进程信息(已弃用,从 Provider 获取)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>检查结果</returns>
    public async Task<CheckResult> CheckAsync(WowProcessInfo? context, CancellationToken ct)
    {
        // 直接从 Provider 获取,不使用 context 参数
        var processInfo = processInfoProvider.ProcessInfo 
            ?? throw new InvalidOperationException("WowProcessInfo 未设置,请先完成进程检测步骤");

        var (success, message, frameCount, version, path) = await Task.Run(() =>
        {
            try
            {
                // 1. 检查 frame_config.json 是否存在
                if (!Core.FrameConfig.Exists())
                {
                    logger.LogWarning("未找到 frame_config.json");
                    return (false, "❌ 未找到 frame_config.json\n点击下方按钮进行配置", 0, string.Empty, string.Empty);
                }

                // 2. 加载并验证配置
                var config = Core.FrameConfig.Load();
                
                if (config.Frames.Length == 0)
                {
                    logger.LogWarning("配置文件损坏: Frames 为空");
                    return (false, "❌ 配置文件损坏\nFrames 数量为 0", 0, string.Empty, string.Empty);
                }

                if (config.Meta.Count == 0)
                {
                    logger.LogWarning("配置文件损坏: Meta.Count 为 0");
                    return (false, "❌ 配置文件损坏\nMeta.Count 为 0", 0, string.Empty, string.Empty);
                }
                
                // 3. 验证分辨率是否匹配
                var currentRect = MacOS.MacOSWindowHelper.GetWindowBounds((int)processInfo.WindowId);
                if (config.Rect.Width != currentRect.Width || config.Rect.Height != currentRect.Height)
                {
                    logger.LogWarning("分辨率已变化, 需要重新配置");
                    Core.FrameConfig.Delete();
                    return (false, "❌ 分辨率已变化\n请重新配置 Frame", 0, string.Empty, string.Empty);
                }

                string configPath = Core.FrameConfigMeta.DefaultFilename;
                string versionStr = config.AddonVersion?.ToString() ?? "未知";
                int frameCount = config.Frames.Length;

                logger.LogInformation($"找到 Frame 配置: {frameCount} 帧, 插件版本: {versionStr}");
                
                return (true, $"找到配置文件\n帧数: {frameCount}\n插件版本: {versionStr}\n路径: {configPath}",
                    frameCount, versionStr, configPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查 Frame 配置时发生错误");
                return (false, $"检查失败: {ex.Message}", 0, string.Empty, string.Empty);
            }
        }, ct);

        if (!success)
        {
            ErrorMessage = message;
            CanConfigure = processInfo != null;
            return new CheckResult(false, null);
        }

        FrameCount = frameCount;
        AddonVersion = version;
        ConfigPath = path;

        return new CheckResult(true, null);
    }

    /// <summary>
    /// 打开 Frame 配置窗口命令
    /// </summary>
    [RelayCommand]
    private async Task OpenFrameConfig()
    {
        var processInfo = processInfoProvider.ProcessInfo;
        if (processInfo == null)
        {
            logger.LogWarning("无法打开 Frame 配置: 缺少 WoW 进程信息");
            return;
        }

        try
        {
            logger.LogInformation("打开 Frame 配置窗口");

            // 1. 加载 AddonConfig
            if (!Core.AddonConfig.Exists())
            {
                logger.LogError("无法打开 Frame 配置: 未找到 addon_config.json");
                ErrorMessage = "请先完成插件配置";
                return;
            }

            var addonConfig = Core.AddonConfig.Load();

            // 2. 创建 FrameConfigViewModel
            var viewModelLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ILogger<FrameConfigViewModel>>(serviceProvider);
            var viewModel = new FrameConfigViewModel(viewModelLogger, processInfo, addonConfig);

            // 3. 创建并显示对话框
            var window = new Views.FrameConfigWindow
            {
                DataContext = viewModel
            };

            // 4. 模态显示窗口
            await window.ShowDialog(Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null);

            // 5. 窗口关闭后,重新验证
            logger.LogInformation("Frame 配置窗口已关闭,重新验证配置");
            await RecheckAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开 Frame 配置窗口时出错");
            ErrorMessage = $"打开配置窗口失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 重新验证 Frame 配置
    /// </summary>
    private async Task RecheckAsync()
    {
        var processInfo = processInfoProvider.ProcessInfo;
        if (processInfo == null)
        {
            logger.LogWarning("无法重新验证: 缺少进程信息");
            return;
        }

        try
        {
            Status = ValidationStatus.InProgress;
            var result = await CheckAsync(null, CancellationToken.None);
            
            logger.LogInformation($"重新验证完成, 结果: {(result.Success ? "成功" : "失败")}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "重新验证 Frame 配置时出错");
            Status = ValidationStatus.Failed;
            ErrorMessage = $"验证失败: {ex.Message}";
        }
    }
}
