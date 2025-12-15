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
    public AddonStepViewModel(ILogger<AddonStepViewModel> logger)
    {
        this.logger = logger;
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
            ValidateAddonInstallation(processInfo.WowPath), ct);

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
    /// 验证插件安装
    /// </summary>
    /// <param name="wowPath">WoW 安装路径</param>
    /// <returns>成功状态、消息、插件路径、版本</returns>
    private (bool success, string message, string addonPath, string version) ValidateAddonInstallation(string wowPath)
    {
        try
        {
            if (string.IsNullOrEmpty(wowPath))
            {
                return (false, "WoW 路径为空,无法检查插件", string.Empty, string.Empty);
            }

            logger.LogInformation($"检查 DataToColor 插件安装: {wowPath}");

            string addonsPath = Path.Combine(wowPath, "Interface", "AddOns");
            
            if (!Directory.Exists(addonsPath))
            {
                logger.LogWarning($"AddOns 目录不存在: {addonsPath}");
                return (false, $"AddOns 目录不存在\n路径: {addonsPath}", string.Empty, string.Empty);
            }

            // 查找 DataToColor 或其自定义版本
            var addonDirs = Directory.GetDirectories(addonsPath)
                .Where(dir => Path.GetFileName(dir).Contains("DataToColor", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (addonDirs.Length == 0)
            {
                logger.LogWarning("未找到 DataToColor 插件");
                return (false, $"未找到 DataToColor 插件\nAddOns 路径: {addonsPath}\n请确认插件已正确安装", string.Empty, string.Empty);
            }

            string addonDir = addonDirs[0];
            string addonName = Path.GetFileName(addonDir);
            string tocFile = Path.Combine(addonDir, $"{addonName}.toc");

            if (!File.Exists(tocFile))
            {
                logger.LogWarning($"找到插件目录但缺少 .toc 文件: {tocFile}");
                return (false, $"插件目录存在但缺少 .toc 文件\n目录: {addonDir}", string.Empty, string.Empty);
            }

            // 尝试读取版本信息
            string version = "未知";
            try
            {
                var tocLines = File.ReadAllLines(tocFile);
                var versionLine = tocLines.FirstOrDefault(line => line.StartsWith("## Version:", StringComparison.OrdinalIgnoreCase));
                if (versionLine != null)
                {
                    version = versionLine.Split(':', 2)[1].Trim();
                }
            }
            catch
            {
                // 忽略版本读取失败
            }

            logger.LogInformation($"找到 DataToColor 插件: {addonName}, 版本: {version}");
            return (true, $"找到插件: {addonName}\n版本: {version}\n路径: {addonDir}", addonDir, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "检查插件安装时发生错误");
            return (false, $"检查失败: {ex.Message}", string.Empty, string.Empty);
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
