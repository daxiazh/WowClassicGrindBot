using Microsoft.Extensions.Logging;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// 工作状态的 ViewModel
/// </summary>
public sealed class WorkViewModel : ViewModelBase
{
    private readonly ILogger<WorkViewModel> logger;
    private readonly WowProcessInfo processInfo;

    public string StatusMessage => $"正在监控进程: {processInfo.ProcessName} (PID: {processInfo.ProcessId})";
    public string WelcomeMessage => $"WoW 路径: {processInfo.WowPath}\n窗口ID: {processInfo.WindowId}\n版本: {processInfo.Version}";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="processInfo">WoW 进程信息</param>
    public WorkViewModel(ILogger<WorkViewModel> logger, WowProcessInfo processInfo)
    {
        this.logger = logger;
        this.processInfo = processInfo;
    }

    /// <summary>
    /// 状态进入时调用
    /// </summary>
    public void OnEnter()
    {
        logger.LogInformation($"进入 Running 状态 - WoW PID: {processInfo.ProcessId}, WindowID: {processInfo.WindowId}");
        
        // TODO: 使用 processInfo.WindowId 创建 ScreenCaptureKit 流
        // TODO: 启动 30fps 游戏循环
        // TODO: 启动进程监控
    }

    /// <summary>
    /// 状态退出时调用
    /// </summary>
    public void OnExit()
    {
        logger.LogInformation("退出 Running 状态");
        
        // TODO: 停止 ScreenCaptureKit 流
        // TODO: 停止游戏循环
        // TODO: 停止监控
    }
}
