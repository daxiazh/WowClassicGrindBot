using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VizAura.Models;
using VizAura.Services;

namespace VizAura.ViewModels;

/// <summary>
/// DataToColor 插件检查步骤 ViewModel
/// </summary>
public sealed partial class AddonStepViewModel : ObservableObject, IStepViewModel
{
    private readonly ILogger<AddonStepViewModel> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IWowProcessInfoProvider processInfoProvider;
    private string currentWowPath = string.Empty;

    public string StepId => "addon_installation";
    public string StepName => "检查 DataToColor 插件";

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
    private string? addonVersion;

    [ObservableProperty]
    private string? addonPath;

    public string SuccessMessage => $"✓ 插件版本: {AddonVersion}";

    public bool ShowFailureView => Status == ValidationStatus.Failed;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool canInstall;

    /// <summary>
    /// 构造函数 (通过 DI 注入)
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="processInfoProvider">WoW 进程信息提供者</param>
    public AddonStepViewModel(
        ILogger<AddonStepViewModel> logger,
        IServiceProvider serviceProvider,
        IWowProcessInfoProvider processInfoProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.processInfoProvider = processInfoProvider;
    }

    /// <summary>
    /// 执行插件检查
    /// </summary>
    /// <param name="context">上一步传递的 WoW 进程信息(已弃用,从 Provider 获取)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>检查结果</returns>
    public async Task<CheckResult> CheckAsync(WowProcessInfo? context, CancellationToken ct)
    {
        // 直接从 Provider 获取,不使用 context 参数
        var processInfo = processInfoProvider.ProcessInfo 
            ?? throw new InvalidOperationException("WowProcessInfo 未设置,请先完成进程检测步骤");

        SetContext(processInfo.WowPath);

        var (success, message, addonPath, version) = await Task.Run(() =>
        {
            try
            {
                // 1. 检查配置文件是否存在
                if (!Core.AddonConfig.Exists())
                {
                    logger.LogWarning("未找到插件配置文件 addon_config.json");
                    return (false, "❌ 未找到插件配置文件\n点击下方按钮进行配置", string.Empty, string.Empty);
                }

                // 使用 AddonConfigurator 来检查插件
                var wowProcess = new VizAura.Services.VizAuraWowProcess(processInfo);
                var configuratorLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<ILogger<Core.AddonConfigurator>>(serviceProvider);
                var configurator = new Core.AddonConfigurator(configuratorLogger, wowProcess);

                // 2. 检查配置是否为默认值
                if (configurator.IsDefault())
                {
                    logger.LogWarning("插件配置不完整");
                    return (false, "❌ 插件配置不完整\n请完成 Author、Title 等配置", string.Empty, string.Empty);
                }

                // 3. 验证配置内容是否合法
                if (!configurator.Validate())
                {
                    logger.LogWarning("插件配置格式不正确");
                    return (false, "❌ 插件配置格式不正确\nAuthor、Title、CellSize 格式有误", string.Empty, string.Empty);
                }

                // 4. 检查插件是否已安装
                if (!configurator.Installed())
                {
                    string addonsPath = Path.Combine(processInfo.WowPath, "Interface", "AddOns");
                    logger.LogWarning($"未找到 DataToColor 插件: {addonsPath}");
                    return (false, $"❌ 插件未安装\n目标路径: {addonsPath}", string.Empty, string.Empty);
                }

                var installedVersion = configurator.GetInstallVersion();
                var versionStr = installedVersion?.ToString() ?? "未知";

                logger.LogInformation($"找到 DataToColor 插件: {configurator.Config.Title}, 版本: {versionStr}");
                
                return (true, $"找到插件: {configurator.Config.Title}\n版本: {versionStr}\n路径: {configurator.FinalAddonPath}",
                    configurator.FinalAddonPath, versionStr);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查插件安装时发生错误");
                return (false, $"检查失败: {ex.Message}", string.Empty, string.Empty);
            }
        }, ct);

        if (!success)
        {
            ErrorMessage = message;
            return new CheckResult(false, null);
        }

        AddonPath = addonPath;
        AddonVersion = version;

        return new CheckResult(true, null);
    }

    /// <summary>
    /// 设置上下文信息
    /// </summary>
    /// <param name="wowPath">WoW 安装路径</param>
    private void SetContext(string wowPath)
    {
        currentWowPath = wowPath;
        CanInstall = !string.IsNullOrEmpty(wowPath);
    }

    /// <summary>
    /// 打开插件配置窗口命令
    /// </summary>
    [RelayCommand]
    private async Task InstallAddon()
    {
        var processInfo = processInfoProvider.ProcessInfo;
        if (processInfo == null)
        {
            logger.LogWarning("无法打开插件配置:缺少 WoW 进程信息");
            return;
        }

        try
        {
            logger.LogInformation("打开插件配置窗口");

            // 创建 VizAuraWowProcess 适配器
            var wowProcess = new VizAura.Services.VizAuraWowProcess(processInfo);

            // 创建 AddonConfigurator
            var configuratorLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ILogger<Core.AddonConfigurator>>(serviceProvider);
            var configurator = new Core.AddonConfigurator(configuratorLogger, wowProcess);

            // 创建 ViewModel
            var viewModelLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ILogger<AddonConfigViewModel>>(serviceProvider);
            var viewModel = new AddonConfigViewModel(viewModelLogger, configurator);

            // 创建并显示对话框
            var window = new VizAura.Views.AddonConfigWindow
            {
                DataContext = viewModel
            };

            // 模态显示窗口
            await window.ShowDialog(Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null);

            // 窗口关闭后,重新验证
            logger.LogInformation("插件配置窗口已关闭,重新验证插件");
            await RecheckAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开插件配置窗口时出错");
            ErrorMessage = $"打开配置窗口失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 重新验证插件
    /// </summary>
    private async Task RecheckAsync()
    {
        var processInfo = processInfoProvider.ProcessInfo;
        if (processInfo == null)
        {
            logger.LogWarning("无法重新验证:缺少进程信息");
            return;
        }

        try
        {
            Status = ValidationStatus.InProgress;
            var result = await CheckAsync(null, CancellationToken.None);
            
            // CheckAsync 内部已经设置了 Status 和相关属性
            logger.LogInformation($"重新验证完成,结果: {(result.Success ? "成功" : "失败")}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "重新验证插件时出错");
            Status = ValidationStatus.Failed;
            ErrorMessage = $"验证失败: {ex.Message}";
        }
    }
}
