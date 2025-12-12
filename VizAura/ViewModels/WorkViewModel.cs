using Microsoft.Extensions.Logging;

namespace VizAura.ViewModels;

/// <summary>
/// 工作状态的 ViewModel
/// </summary>
public sealed class WorkViewModel : ViewModelBase
{
    private readonly ILogger<WorkViewModel> logger;

    public string StatusMessage { get; } = "VizAura 已就绪,准备工作中...";
    public string WelcomeMessage { get; } = "所有检查已通过,系统正常运行。";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public WorkViewModel(ILogger<WorkViewModel> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// 状态进入时调用
    /// </summary>
    public void OnEnter()
    {
        logger.LogInformation("进入 Running 状态");
        
        // TODO: 启动工作循环
        // TODO: 启动监控
    }

    /// <summary>
    /// 状态退出时调用
    /// </summary>
    public void OnExit()
    {
        logger.LogInformation("退出 Running 状态");
        
        // TODO: 停止工作循环
        // TODO: 停止监控
    }
}
