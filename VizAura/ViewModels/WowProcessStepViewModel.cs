using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VizAura.MacOS;
using VizAura.Models;
using VizAura.Services;

namespace VizAura.ViewModels;

/// <summary>
/// WoW 进程检查步骤 ViewModel
/// </summary>
public sealed partial class WowProcessStepViewModel : ObservableObject, IStepViewModel
{
    private readonly ILogger<WowProcessStepViewModel> logger;
    private readonly IWowProcessInfoProvider processInfoProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="processInfoProvider">WoW 进程信息提供者</param>
    public WowProcessStepViewModel(
        ILogger<WowProcessStepViewModel> logger,
        IWowProcessInfoProvider processInfoProvider)
    {
        this.logger = logger;
        this.processInfoProvider = processInfoProvider;
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
    private bool isExpanded;

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
    
    [ObservableProperty]
    private Version version = new();

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
        Version = version;

        // 直接设置到 Provider,供其他步骤使用
        var processInfo = new WowProcessInfo
        {
            Process = process,
            WindowId = id,
            WowPath = path,
            Version = version
        };
        processInfoProvider.SetProcessInfo(processInfo);

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

            var wowExecutablePath = MacOsProcessHelper.GetExecutablePath(wowProcess);
            if (string.IsNullOrEmpty(wowExecutablePath))
            {
                return (null, 0, string.Empty, new Version(), $"找到进程 {wowProcess.ProcessName} 但无法获取路径");
            }

            // 转换为 WoW 根目录 (包含 Interface 的目录)
            var wowRootPath = GetWowRootDirectory(wowExecutablePath);

            var windowId = MacOsProcessHelper.GetWindowId(wowProcess);
            if (windowId == 0)
            {
                return (null, 0, string.Empty, new Version(), "无法获取 WoW 窗口 ID");
            }

            var version = MacOsProcessHelper.GetVersion(wowProcess, wowExecutablePath);

            return (wowProcess, windowId, wowRootPath, version, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "查找 WoW 进程时发生错误");
            return (null, 0, string.Empty, new Version(), $"错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 从可执行文件路径获取 WoW 根目录
    /// </summary>
    /// <param name="executablePath">可执行文件路径(在 .app 包内)</param>
    /// <returns>WoW 根目录(包含 Interface 的目录)</returns>
    private static string GetWowRootDirectory(string executablePath)
    {
        // macOS: .../World of Warcraft Classic.app/Contents/MacOS
        // 向上导航到 .app 父目录
        var dir = new DirectoryInfo(executablePath);
        
        // MacOS -> Contents -> .app -> 根目录
        var rootDir = dir.Parent?.Parent?.Parent;
        
        return rootDir?.FullName ?? executablePath;
    }
}
