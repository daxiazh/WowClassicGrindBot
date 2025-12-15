using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// DataToColor 插件检查步骤 ViewModel
/// </summary>
public sealed partial class AddonStepViewModel : ObservableObject, IStepViewModel
{
    private readonly ILogger<AddonStepViewModel> logger;
    private readonly IServiceProvider serviceProvider;
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
    public AddonStepViewModel(ILogger<AddonStepViewModel> logger, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 执行插件检查
    /// </summary>
    /// <param name="context">上一步传递的 WoW 进程信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>检查结果</returns>
    public async Task<CheckResult> CheckAsync(WowProcessInfo? context, CancellationToken ct)
    {
        var processInfo = context!;

        SetContext(processInfo.WowPath);

        var (success, message, addonPath, version) = await Task.Run(() =>
        {
            try
            {
                // 1. 检查配置文件是否存在
                if (!Core.AddonConfig.Exists())
                {
                    logger.LogWarning("未找到插件配置文件 addon_config.json");
                    return (false, "未找到插件配置文件 (addon_config.json)\n请先通过菜单「配置 → AddOns 管理」配置插件", string.Empty, string.Empty);
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
                    return (false, "插件配置不完整\n请先通过菜单「配置 → AddOns 管理」完成配置", string.Empty, string.Empty);
                }

                // 3. 验证配置内容是否合法
                if (!configurator.Validate())
                {
                    logger.LogWarning("插件配置格式不正确");
                    return (false, "插件配置格式不正确\n请检查 Author、Title、CellSize 的格式\n通过菜单「配置 → AddOns 管理」修改", string.Empty, string.Empty);
                }

                // 4. 检查插件是否已安装
                if (!configurator.Installed())
                {
                    string addonsPath = Path.Combine(processInfo.WowPath, "Interface", "AddOns");
                    logger.LogWarning($"未找到 DataToColor 插件: {addonsPath}");
                    return (false, $"未找到 DataToColor 插件\nAddOns 路径: {addonsPath}\n请通过菜单「配置 → AddOns 管理」安装插件", string.Empty, string.Empty);
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
            return new CheckResult(false, processInfo);
        }

        AddonPath = addonPath;
        AddonVersion = version;

        return new CheckResult(true, processInfo);
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
    /// 安装插件命令
    /// </summary>
    [RelayCommand]
    private void InstallAddon()
    {
        logger.LogInformation($"开始安装插件到: {currentWowPath}");

        var sourceAddon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Addons", "DataToColor");
        var targetAddon = Path.Combine(currentWowPath, "Interface", "AddOns", "DataToColor");

        try
        {
            if (!Directory.Exists(sourceAddon))
            {
                ErrorMessage = $"找不到插件源文件: {sourceAddon}";
                logger.LogError(ErrorMessage);
                return;
            }

            CopyDirectory(sourceAddon, targetAddon);
            logger.LogInformation("插件安装成功");

            ErrorMessage = "✓ 插件已安装成功！\n请在游戏中输入 /reload 重载界面";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "安装插件失败");
            ErrorMessage = $"安装失败: {ex.Message}";
        }
    }


    /// <summary>
    /// 递归复制目录
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }
}
