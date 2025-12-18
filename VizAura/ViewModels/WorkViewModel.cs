using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Core;
using Microsoft.Extensions.Logging;
using VizAura.MacOS;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// 工作状态的 ViewModel
/// </summary>
public sealed partial class WorkViewModel : ViewModelBase
{
    private readonly ILogger<WorkViewModel> logger;
    private readonly WowProcessInfo processInfo;
    
    private WowScreenMacOS? screen;
    private AddonDataSnapshot? addonDataSnapshot;
    private DataFrame[] frames = [];
    private PlayerReader? playerReader;

    public string StatusMessage => $"正在监控进程: {processInfo.ProcessName} (PID: {processInfo.ProcessId})";
    public string WelcomeMessage => $"WoW 路径: {processInfo.WowPath}\n窗口ID: {processInfo.WindowId}\n版本: {processInfo.Version}";
    
    /// <summary>
    /// 玩家最大生命值
    /// </summary>
    [ObservableProperty]
    private int playerHealthMax;
    
    /// <summary>
    /// 玩家当前生命值
    /// </summary>
    [ObservableProperty]
    private int playerHealthCurrent;
    
    /// <summary>
    /// 玩家最大法力值
    /// </summary>
    [ObservableProperty]
    private int playerManaMax;
    
    /// <summary>
    /// 玩家当前法力值
    /// </summary>
    [ObservableProperty]
    private int playerManaCurrent;
    
    /// <summary>
    /// 目标最大生命值
    /// </summary>
    [ObservableProperty]
    private int targetHealthMax;
    
    /// <summary>
    /// 目标当前生命值
    /// </summary>
    [ObservableProperty]
    private int targetHealthCurrent;
    
    /// <summary>
    /// 全局时间 (每帧递增)
    /// </summary>
    [ObservableProperty]
    private int globalTime;
    
    /// <summary>
    /// CRC 校验状态 (true=正常, false=数据异常/被遮挡)
    /// </summary>
    [ObservableProperty]
    private bool crcValid = true;

    /// <summary>
    /// 构造函数 (所有依赖通过 DI 注入)
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="processInfo">WoW 进程信息</param>
    public WorkViewModel(
        ILogger<WorkViewModel> logger, 
        WowProcessInfo processInfo)
    {
        this.logger = logger;
        this.processInfo = processInfo;
    }

    /// <summary>
    /// 状态进入时调用
    /// </summary>
    public void OnEnter()
    {
        // 1. 加载 Frame 配置
        frames = FrameConfig.LoadFrames();
        if (frames.Length == 0)
        {
            logger.LogError("Frame 配置不存在,无法启动 addon 数据读取");
            return;
        }
        
        // 2. 创建 WowScreenMacOS 实例 (传递 frames 参数以启用 AddonDataSnapshot)
        var rect = MacOSWindowHelper.GetWindowBounds((int)processInfo.WindowId);
        screen = new WowScreenMacOS(processInfo.WindowId, rect, frames);
        
        // 4. 订阅帧更新事件 (关键: 每次屏幕捕获完成时立即读取 addon 数据)
        screen.OnFrameUpdated += OnScreenFrameUpdated;
    }
    
    /// <summary>
    /// 屏幕帧更新回调 (在 native 线程中被调用)
    /// 每次 ScreenCaptureKit 捕获到新帧时触发
    /// </summary>
    private void OnScreenFrameUpdated()
    {
        if (addonDataSnapshot == null)
            return;
        
        // AddonDataSnapshot 已在 OnFrameReceived 中自动更新
        // 检查数据有效性: GlobalTime > 0 (GlobalTime 在 Frame[frames.Length - 2])
        int currentGlobalTime = addonDataSnapshot.GetInt(frames.Length - 2);
        bool dataValid = currentGlobalTime > 0;
        
        // 切换到 UI 线程更新属性
        Dispatcher.UIThread.Post(() =>
        {
            return; // TODO: 下面的代码还不正确, 等待修正
            if (dataValid)
            {
                // 使用 PlayerReader 封装的方法读取数据
                PlayerHealthMax = playerReader.HealthMax();
                PlayerHealthCurrent = playerReader.HealthCurrent();
                PlayerManaMax = playerReader.ManaMax();
                PlayerManaCurrent = playerReader.ManaCurrent();
                TargetHealthMax = playerReader.TargetMaxHealth();
                TargetHealthCurrent = playerReader.TargetHealth();
                GlobalTime = currentGlobalTime;
                
                CrcValid = true;
            }
            else
            {
                // CRC 校验失败或数据无效
                CrcValid = false;
                
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Addon 数据校验失败,可能窗口被遮挡或 Frame 位置失效");
                }
            }
        });
    }

    /// <summary>
    /// 状态退出时调用
    /// </summary>
    public void OnExit()
    {
        logger.LogInformation("退出 Running 状态");
        
        if (screen != null)
        {
            screen.OnFrameUpdated -= OnScreenFrameUpdated;
            screen.Dispose();
            screen = null;
        }
    }
}
