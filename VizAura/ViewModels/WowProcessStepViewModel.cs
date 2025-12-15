using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VizAura.MacOS;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// WoW 进程检查步骤 ViewModel
/// </summary>
public sealed partial class WowProcessStepViewModel : ObservableObject, IStepViewModel
{
    private readonly ILogger<WowProcessStepViewModel> logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public WowProcessStepViewModel(ILogger<WowProcessStepViewModel> logger)
    {
        this.logger = logger;
    }

    public string StepId => "wow_process";
    public string StepName => "检查 World of Warcraft 进程";

    [ObservableProperty]
    private ValidationStatus status = ValidationStatus.Pending;

    partial void OnStatusChanged(ValidationStatus value)
    {
        OnPropertyChanged(nameof(ShowSuccessView));
        OnPropertyChanged(nameof(ShowFailureView));
    }

    [ObservableProperty]
    private string checkingMessage = "等待检查...";

    public bool ShowSuccessView => Status == ValidationStatus.Success;

    [ObservableProperty]
    private string? processName;

    partial void OnProcessNameChanged(string? value)
    {
        OnPropertyChanged(nameof(SuccessMessage));
    }

    [ObservableProperty]
    private int processId;

    partial void OnProcessIdChanged(int value)
    {
        OnPropertyChanged(nameof(SuccessMessage));
    }

    [ObservableProperty]
    private string? wowPath;

    [ObservableProperty]
    private uint windowId;

    public string SuccessMessage => $"✓ 进程: {ProcessName} (PID: {ProcessId})";

    public bool ShowFailureView => Status == ValidationStatus.Failed;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// 执行 WoW 进程检查
    /// </summary>
    /// <param name="context">上一步传递的上下文数据（本步骤不使用）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>检查结果</returns>
    public async Task<CheckResult> CheckAsync(WowProcessInfo? context, CancellationToken ct)
    {
        var (process, id, path, version, errorMsg) =
            await Task.Run(FindWowProcess, ct);

        if (process == null)
        {
            ErrorMessage = errorMsg ?? "未找到 WoW 进程";
            return new CheckResult(false, null);
        }

        ProcessName = process.ProcessName;
        ProcessId = process.Id;
        WowPath = path;
        WindowId = id;

        var processInfo = new WowProcessInfo
        {
            Process = process,
            WindowId = id,
            WowPath = path,
            Version = version
        };

        return new CheckResult(true, processInfo);
    }

    /// <summary>
    /// 查找 WoW 进程并获取完整信息
    /// </summary>
    private (Process? process, uint windowId, string wowPath, Version version, string? errorMsg) FindWowProcess()
    {
        try
        {
            var wowProcessNames = new[] { "World of Warcraft", "Wow", "WowClassic", "WowClassicT", "Wow-64", "WowClassicB", "World of Warcraft Classic" };
            var processes = Process.GetProcesses();
            Process? wowProcess = null;

            foreach (var p in processes)
            {
                try
                {
                    if (wowProcessNames.Any(name => p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        wowProcess = p;
                        break;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            if (wowProcess == null)
            {
                return (null, 0, string.Empty, new Version(), "未找到 World of Warcraft 进程");
            }

            var wowPath = MacOsProcessHelper.GetExecutablePath(wowProcess);
            if (string.IsNullOrEmpty(wowPath))
            {
                return (null, 0, string.Empty, new Version(), $"找到进程 {wowProcess.ProcessName} 但无法获取路径");
            }

            var windowId = MacOsProcessHelper.GetWindowId(wowProcess);
            if (windowId == 0)
            {
                return (null, 0, string.Empty, new Version(), "无法获取 WoW 窗口 ID");
            }

            var version = MacOsProcessHelper.GetVersion(wowProcess, wowPath);

            return (wowProcess, windowId, wowPath, version, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "查找 WoW 进程时发生错误");
            return (null, 0, string.Empty, new Version(), $"错误: {ex.Message}");
        }
    }
}
