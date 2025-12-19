using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using Core.Database;
using Microsoft.Extensions.Logging;
using System.IO;
using VizAura.MacOS;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// 工作状态的 ViewModel
/// 说明: 所有依赖通过 DI 注入,生命周期为 Scoped (一次检测会话一个实例)
/// </summary>
public sealed partial class WorkViewModel : ViewModelBase
{
    private readonly ILogger<WorkViewModel> logger;
    private readonly WowProcessInfo processInfo;
    
    // 核心依赖 - 全部通过 DI 注入 (Scoped 生命周期)
    private readonly WowScreenMacOS screen;
    private readonly AddonDataSnapshot addonDataSnapshot;
    private readonly PlayerReader playerReader;
    private readonly HekiliReader hekiliReader;
    private readonly SpellDB spellDB;

    public string StatusMessage => $"正在监控进程: {processInfo.ProcessName} (PID: {processInfo.ProcessId})";

    public string WelcomeMessage =>
        $"WoW 路径: {processInfo.WowPath}\n窗口ID: {processInfo.WindowId}\n版本: {processInfo.Version}";

    /// <summary>
    /// 玩家最大生命值
    /// </summary>
    [ObservableProperty] private int playerHealthMax;

    /// <summary>
    /// 玩家当前生命值
    /// </summary>
    [ObservableProperty] private int playerHealthCurrent;

    /// <summary>
    /// 玩家最大法力值
    /// </summary>
    [ObservableProperty] private int playerManaMax;

    /// <summary>
    /// 玩家当前法力值
    /// </summary>
    [ObservableProperty] private int playerManaCurrent;

    /// <summary>
    /// 目标最大生命值
    /// </summary>
    [ObservableProperty] private int targetHealthMax;

    /// <summary>
    /// 目标当前生命值
    /// </summary>
    [ObservableProperty] private int targetHealthCurrent;

    /// <summary>
    /// 全局时间 (每帧递增)
    /// </summary>
    [ObservableProperty] private int globalTime;

    /// <summary>
    /// Hekili 是否启用自动模式
    /// </summary>
    [ObservableProperty] private bool isHekiliAutoMode;

    /// <summary>
    /// Hekili 推荐技能 1 ID
    /// </summary>
    [ObservableProperty] private int spell1;

    /// <summary>
    /// Hekili 推荐技能 2 ID
    /// </summary>
    [ObservableProperty] private int spell2;

    /// <summary>
    /// Hekili 推荐技能 1 名称
    /// </summary>
    [ObservableProperty] private string spell1Name = "-";

    /// <summary>
    /// Hekili 推荐技能 2 名称
    /// </summary>
    [ObservableProperty] private string spell2Name = "-";

    /// <summary>
    /// Hekili 推荐技能 1 冷却时间 (秒)
    /// </summary>
    [ObservableProperty] private double spell1CooldownSec;

    /// <summary>
    /// Hekili 推荐技能 2 冷却时间 (秒)
    /// </summary>
    [ObservableProperty] private double spell2CooldownSec;

    /// <summary>
    /// Hekili 推荐技能 1 快捷键
    /// </summary>
    [ObservableProperty] private string spell1Keybind = "";

    /// <summary>
    /// Hekili 推荐技能 2 快捷键
    /// </summary>
    [ObservableProperty] private string spell2Keybind = "";

    /// <summary>
    /// CRC 校验状态 (true=正常, false=数据异常/被遮挡)
    /// </summary>
    [ObservableProperty] private bool crcValid = true;
    
    /// <summary>
    /// Addon 读取失败计数
    /// </summary>
    [ObservableProperty] private int addonReadFailureCount;
    
    /// <summary>
    /// 是否显示 Addon 警告
    /// </summary>
    [ObservableProperty] private bool showAddonWarning;
    
    /// <summary>
    /// Addon 警告消息
    /// </summary>
    [ObservableProperty] private string addonWarningMessage = string.Empty;

    /// <summary>
    /// 构造函数 - 所有依赖通过 DI 注入
    /// 依赖解析链:
    ///   WorkViewModel (Scoped)
    ///     → WowScreenMacOS (Scoped) → DataFrame[] (Scoped) → WowProcessInfo (Scoped)
    ///     → AddonDataSnapshot (Scoped) → DataFrame[] (Scoped)
    ///     → PlayerReader (Scoped) → IAddonDataProvider (Scoped) → AddonDataSnapshot
    ///                               → WorldMapAreaDB (Singleton)
    ///                               → AreaDB (Singleton) → DataConfig (Scoped)
    ///                               → AddonBits/SpellInRange/Stance (Singleton)
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="processInfo">WoW 进程信息 (Scoped)</param>
    /// <param name="screen">macOS 屏幕捕获实例 (Scoped)</param>
    /// <param name="addonDataSnapshot">Addon 数据快照 (Scoped)</param>
    /// <param name="playerReader">玩家数据读取器 (Scoped)</param>
    /// <param name="hekiliReader">Hekili 数据读取器 (Scoped)</param>
    /// <param name="spellDB">技能数据库 (Singleton)</param>
    public WorkViewModel(
        ILogger<WorkViewModel> logger,
        WowProcessInfo processInfo,
        WowScreenMacOS screen,
        AddonDataSnapshot addonDataSnapshot,
        PlayerReader playerReader,
        HekiliReader hekiliReader,
        SpellDB spellDB)
    {
        screen.Enabled = true;
        this.logger = logger;
        this.processInfo = processInfo;
        this.screen = screen;
        this.addonDataSnapshot = addonDataSnapshot;
        this.playerReader = playerReader;
        this.hekiliReader = hekiliReader;
        this.spellDB = spellDB;
    }

    /// <summary>
    /// 状态进入时调用
    /// 说明: 所有依赖已通过 DI 注入,只需订阅事件即可
    /// </summary>
    public void OnEnter()
    {
        // 订阅帧更新事件 (每次 ScreenCaptureKit 捕获到新帧时触发)
        screen.OnFrameUpdated += OnScreenFrameUpdated;
    }

    /// <summary>
    /// 屏幕帧更新回调 (在 native 线程中被调用)
    /// 每次 ScreenCaptureKit 捕获到新帧时触发
    /// </summary>
    private void OnScreenFrameUpdated()
    {
        // 切换到 UI 线程,复制快照并读取数据
        Dispatcher.UIThread.Post(() =>
        {
            // 复制当前的 AddonDataSnapshot 到 UI 线程缓冲区
            screen.CopyAddonDataSnapshot(addonDataSnapshot);
            
            // 读取验证统计
            int failureCount = addonDataSnapshot.ValidationFailureCount;
            var result = addonDataSnapshot.LastValidationResult;
            
            AddonReadFailureCount = failureCount;
            
            if (result == AddonValidationResult.Success)
            {
                // 验证成功,从快照读取 Player 数据
                // GlobalTime 在倒数第二帧 (data.Length - 2)
                int currentGlobalTime = addonDataSnapshot.GetInt(addonDataSnapshot.Data.Length - 2);
                if (currentGlobalTime > 0)
                {
                    // 使用 PlayerReader 读取玩家数据
                    PlayerHealthMax = playerReader.HealthMax();
                    PlayerHealthCurrent = playerReader.HealthCurrent();
                    PlayerManaMax = playerReader.ManaMax();
                    PlayerManaCurrent = playerReader.ManaCurrent();
                    
                    TargetHealthMax = playerReader.TargetMaxHealth();
                    TargetHealthCurrent = playerReader.TargetHealth();
                    
                    // 读取 Hekili 自动模式状态
                    IsHekiliAutoMode = hekiliReader.IsAutoModeEnabled;
                    
                    if (IsHekiliAutoMode)
                    {
                        // 读取技能 1
                        Spell1 = hekiliReader.Spell1;
                        Spell1Name = GetSpellName(Spell1);
                        Spell1CooldownSec = hekiliReader.Spell1CD / 1000.0;
                        Spell1Keybind = hekiliReader.Spell1Keybind;
                        
                        // 读取技能 2
                        Spell2 = hekiliReader.Spell2;
                        Spell2Name = GetSpellName(Spell2);
                        Spell2CooldownSec = hekiliReader.Spell2CD / 1000.0;
                        Spell2Keybind = hekiliReader.Spell2Keybind;
                    }
                    else
                    {
                        // 清空显示
                        Spell1 = 0;
                        Spell1Name = "-";
                        Spell1CooldownSec = 0;
                        Spell1Keybind = "";
                        
                        Spell2 = 0;
                        Spell2Name = "-";
                        Spell2CooldownSec = 0;
                        Spell2Keybind = "";
                    }
                    
                    GlobalTime = currentGlobalTime;
                    ShowAddonWarning = false;
                }
            }
            else
            {
                // 失败次数超过阈值 (30 次) 才显示警告
                if (failureCount > 30)
                {
                    ShowAddonWarning = true;
                    AddonWarningMessage = GenerateWarningMessage(result);
                }
            }
        });
    }
    
    /// <summary>
    /// 获取技能名称
    /// </summary>
    /// <param name="actionId">技能 ID</param>
    /// <returns>技能名称,如果 ID 为 0 返回 "-",查不到返回 ID 字符串</returns>
    private string GetSpellName(int actionId)
    {
        if (actionId == 0)
            return "-";
        
        if (spellDB.Spells.TryGetValue(actionId, out var spell))
            return spell.Name;
        
        return actionId.ToString();
    }
    
    /// <summary>
    /// 根据验证结果生成警告消息
    /// </summary>
    /// <param name="result">验证结果</param>
    /// <returns>警告消息</returns>
    private static string GenerateWarningMessage(AddonValidationResult result)
    {
        return result switch
        {
            AddonValidationResult.FirstFrameFailed => 
                "❌ 未检测到 Addon 数据\n\n可能原因:\n• 不在游戏画面\n• 插件未加载\n• 窗口被遮挡",
            
            AddonValidationResult.BoundsOutOfRange => 
                "❌ Frame 坐标越界\n\n可能原因:\n• 分辨率已改变\n\n建议: 点击下方按钮重新配置 Frame",
            
            AddonValidationResult.CrcFailed => 
                "❌ 数据校验失败\n\n可能原因:\n• 窗口部分被遮挡\n• 分辨率变化",
            
            _ => "未知错误"
        };
    }
    
    /// <summary>
    /// 重新配置 Frame 命令
    /// </summary>
    [RelayCommand]
    private void ReconfigureFrame()
    {
        var frameConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "frame_config.json");
        if (File.Exists(frameConfigPath))
        {
            File.Delete(frameConfigPath);
            logger.LogInformation("已删除 frame_config.json,请重启应用重新配置");
        }
    }

    /// <summary>
    /// 状态退出时调用
    /// 资源释放:
    ///   1. 手动释放: screen.Dispose() - 释放 native 资源 (ScreenCaptureKit)
    ///   2. 自动释放: Scope.Dispose() 会释放所有 Scoped 服务
    /// </summary>
    public void OnExit()
    {
        // 取消事件订阅
        screen.OnFrameUpdated -= OnScreenFrameUpdated;
        
        // 手动释放 native 资源 (在 Scope 销毁前提前释放)
        screen.Dispose();
    }
}