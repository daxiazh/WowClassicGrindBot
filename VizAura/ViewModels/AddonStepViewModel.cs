using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
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
    private readonly StartupValidator validator;
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
    /// <param name="validator">启动验证服务</param>
    public AddonStepViewModel(
        ILogger<AddonStepViewModel> logger,
        StartupValidator validator)
    {
        this.logger = logger;
        this.validator = validator;
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

        var (success, message) = await Task.Run(() =>
            validator.ValidateAddonInstallation(processInfo.WowPath), ct);

        if (!success)
        {
            ErrorMessage = message;
            return new CheckResult(false, processInfo);
        }

        AddonPath = Path.Combine(processInfo.WowPath, "Interface", "AddOns", "DataToColor");
        AddonVersion = "1.0";

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
