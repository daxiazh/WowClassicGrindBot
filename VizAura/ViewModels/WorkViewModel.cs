using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using Core.Database;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
    private readonly AddonBits addonBits;
    private readonly IEnumerable<IReader> readers;

    // 自动按键相关
    private DateTime lastKeybindSentTime = DateTime.MinValue;
    private int lastSentSpellId = 0;  // 上次发送的技能 ID
    private const int KEYBIND_COOLDOWN_MS = 50; // 全局按键最小间隔 (50ms)
    private const int SAME_SPELL_COOLDOWN_MS = 500; // 同一技能强制冷却 (500ms)

    // UI 更新优化 (线程安全)
    private long _lastUIUpdateTicks = 0;  // 上次 UI 更新时间戳 (Ticks, 使用 Interlocked)
    private const int UI_UPDATE_INTERVAL_MS = 66;  // UI 更新间隔 (15 FPS)
    private volatile int _isUIUpdatePending = 0;  // UI 更新是否待处理 (0=false, 1=true, 使用 Interlocked)
    
    // 错误处理 (线程安全)
    private volatile bool _isExiting = false;  // 防止重复退出

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
    /// Hekili 推荐技能 1 快捷键
    /// </summary>
    [ObservableProperty] private string spell1Keybind = "";

    /// <summary>
    /// Hekili 推荐技能 2 快捷键
    /// </summary>
    [ObservableProperty] private string spell2Keybind = "";

    /// <summary>
    /// Hekili 推荐技能 1 是否可用
    /// </summary>
    [ObservableProperty] private bool spell1Usable;

    /// <summary>
    /// 最后一次按键发送时间（用于 UI 显示）
    /// </summary>
    [ObservableProperty] private string lastKeySentDisplay = "";

    /// <summary>
    /// 发送技能 1 快捷键命令
    /// </summary>
    [RelayCommand]
    private void SendSpell1Keybind()
    {
        if (string.IsNullOrEmpty(Spell1Keybind)) return;

        bool success = KeybindMapper.SendKeybind(Spell1Keybind);
        if (!success)
        {
            logger.LogWarning($"发送快捷键失败: {Spell1Keybind}");
        }
    }

    /// <summary>
    /// 发送技能 2 快捷键命令
    /// </summary>
    [RelayCommand]
    private void SendSpell2Keybind()
    {
        if (string.IsNullOrEmpty(Spell2Keybind)) return;

        bool success = KeybindMapper.SendKeybind(Spell2Keybind);
        if (!success)
        {
            logger.LogWarning($"发送快捷键失败: {Spell2Keybind}");
        }
    }

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
    /// VizAura 自动施法是否启用 (从 Lua 端读取)
    /// </summary>
    [ObservableProperty] private bool isAutoCastEnabled;

    /// <summary>
    /// 是否启用 UI 更新 (关闭可大幅减少内存分配)
    /// </summary>
    [ObservableProperty] private bool enableUIUpdates = false;

    /// <summary>
    /// 自动施法状态文本 (优化: 避免 Run 元素)
    /// </summary>
    [ObservableProperty] private string autoCastStatusText = "";

    /// <summary>
    /// 玩家生命值文本 (优化: 避免 Run 元素)
    /// </summary>
    [ObservableProperty] private string playerHealthText = "";

    /// <summary>
    /// 玩家法力值文本 (优化: 避免 Run 元素)
    /// </summary>
    [ObservableProperty] private string playerManaText = "";

    /// <summary>
    /// 目标生命值文本 (优化: 避免 Run 元素)
    /// </summary>
    [ObservableProperty] private string targetHealthText = "";

    /// <summary>
    /// 调试信息文本 (优化: 避免 Run 元素)
    /// </summary>
    [ObservableProperty] private string debugInfoText = "";

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
    /// <param name="addonBits">战斗状态判断 (Scoped)</param>
    /// <param name="readers">所有 IReader 实现集合 (Scoped)</param>
    public WorkViewModel(
        ILogger<WorkViewModel> logger,
        WowProcessInfo processInfo,
        WowScreenMacOS screen,
        AddonDataSnapshot addonDataSnapshot,
        PlayerReader playerReader,
        HekiliReader hekiliReader,
        SpellDB spellDB,
        AddonBits addonBits,
        IEnumerable<IReader> readers)
    {
        this.logger = logger;
        this.processInfo = processInfo;
        this.screen = screen;
        this.addonDataSnapshot = addonDataSnapshot;
        this.playerReader = playerReader;
        this.hekiliReader = hekiliReader;
        this.spellDB = spellDB;
        this.addonBits = addonBits;
        this.readers = readers;
    }

    /// <summary>
    /// 状态进入时调用
    /// 说明: 所有依赖已通过 DI 注入,只需订阅事件即可
    /// </summary>
    public void OnEnter()
    {
        // 订阅帧更新事件 (每次 ScreenCaptureKit 捕获到新帧时触发)
        screen.OnFrameUpdated += OnScreenFrameUpdated;
        
        // 订阅流错误事件 (当窗口关闭等错误发生时触发)
        screen.OnStreamError += OnScreenError;
    }

    /// <summary>
    /// 流错误回调 (在 native 线程中被调用)
    /// 当 ScreenCaptureKit 流发生错误时触发 (例如窗口关闭)
    /// </summary>
    /// <param name="errorCode">错误码</param>
    private void OnScreenError(int errorCode)
    {
        // 原子 CAS: 仅首次调用成功,防止重复退出
        if (Interlocked.CompareExchange(ref _isExiting, true, false))
            return;
        
        logger.LogError("ScreenCaptureKit 流错误: Code={ErrorCode}, 窗口可能已关闭", errorCode);
        
        // 切换到 UI 线程处理状态转换
        Dispatcher.UIThread.Post(() =>
        {
            // 二次检查: 确保当前仍是 WorkViewModel 状态
            var mainWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            
            var mainVm = mainWindow?.DataContext as MainWindowViewModel;
            
            if (mainVm?.CurrentViewModel == this)
            {
                logger.LogInformation("检测到窗口关闭,退出 Work 状态");
                
                // 先清理资源 (取消订阅+释放 screen)
                OnExit();
                
                // 再切换状态
                mainVm.TransitionTo(AppState.Validating);
            }
            else
            {
                logger.LogWarning("OnScreenError 回调时已不在 Work 状态,跳过处理");
            }
        }, DispatcherPriority.Normal);
    }
    
    /// <summary>
    /// 屏幕帧更新回调 (在 native 线程中被调用)
    /// 每次 ScreenCaptureKit 捕获到新帧时触发
    /// </summary>
    private void OnScreenFrameUpdated()
    {
        // 1. 同步执行 (native 线程): 数据读取
        screen.CopyAddonDataSnapshot(addonDataSnapshot);
        
        foreach (var reader in readers)
        {
            reader.Update(addonDataSnapshot);
        }
        
        // 2. 同步执行: 技能释放逻辑 (无 UI 更新)
        AutoSendKeybind();
        
        // 3. 异步执行 (UI 线程): UI 更新 (仅当开关启用时)
        if (EnableUIUpdates)
        {
            // 优化 2: UI 更新节流 (15 FPS) - 线程安全
            long nowTicks = DateTime.UtcNow.Ticks;
            long lastTicks = Interlocked.Read(ref _lastUIUpdateTicks);
            long elapsedMs = (nowTicks - lastTicks) / TimeSpan.TicksPerMillisecond;
            
            if (elapsedMs < UI_UPDATE_INTERVAL_MS)
                return;
            
            // 优化 3: Dispatcher 积压检测 - 线程安全 (CAS 操作)
            if (Interlocked.CompareExchange(ref _isUIUpdatePending, 1, 0) != 0)
                return; // 上一帧还未处理完，跳过
            
            // 更新时间戳
            Interlocked.Exchange(ref _lastUIUpdateTicks, nowTicks);
            
            // 优化 1: 消除 lambda 闭包 - 使用 InvokeAsync 调用方法
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    UpdateUI();
                }
                finally
                {
                    // 原子重置标志
                    Interlocked.Exchange(ref _isUIUpdatePending, 0);
                }
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// 更新 UI 显示（在 UI 线程中执行）
    /// 说明: 从 OnScreenFrameUpdated() 的 lambda 中提取,消除闭包分配
    /// </summary>
    private void UpdateUI()
    {
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
                
                // 读取 VizAura 自动施法开关状态
                IsAutoCastEnabled = addonBits.VizAuraAutoCast_Enabled();

                // 更新组合文本 (避免 AXAML 中使用 Run 元素)
                AutoCastStatusText = $"自动施法: {(IsAutoCastEnabled ? "已启用" : "已禁用")}";
                PlayerHealthText = $"{PlayerHealthCurrent}/{PlayerHealthMax}";
                PlayerManaText = $"{PlayerManaCurrent}/{PlayerManaMax}";
                TargetHealthText = $"{TargetHealthCurrent}/{TargetHealthMax}";
                DebugInfoText = $"GlobalTime: {currentGlobalTime}  |  Frames: {WelcomeMessage}";

                if (IsHekiliAutoMode)
                {
                    // 读取技能 1
                    Spell1 = hekiliReader.Spell1;
                    Spell1Name = GetSpellName(Spell1);
                    Spell1Keybind = hekiliReader.Spell1Keybind;
                    Spell1Usable = hekiliReader.Spell1Usable;

                    // 读取技能 2
                    Spell2 = hekiliReader.Spell2;
                    Spell2Name = GetSpellName(Spell2);
                    Spell2Keybind = hekiliReader.Spell2Keybind;
                }
                else
                {
                    // 清空显示
                    Spell1 = 0;
                    Spell1Name = "-";
                    Spell1Keybind = "";

                    Spell2 = 0;
                    Spell2Name = "-";
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
    /// 自动发送 Hekili 推荐的技能快捷键
    /// 条件: Lua开关启用 + 自动模式 + 战斗中 + 目标有效 + WoW激活 + 有快捷键 + 技能可用 + 防抖
    /// 说明: Usable 包含 Hekili 的所有检查 (能量/距离/条件/CD/GCD/施法等)
    /// </summary>
    private void AutoSendKeybind()
    {
        // 0. 检查 Lua 端是否允许自动施法 (通过 WoW 游戏内 UI 按钮控制)
        if (!addonBits.VizAuraAutoCast_Enabled()) return;

        // 1. 检查 Hekili 自动模式
        if (!hekiliReader.IsAutoModeEnabled) return;

        // 2. 检查战斗状态
        if (!addonBits.Combat()) return;

        // 3. 检查目标还活着且是敌对
        if (addonBits.Target_Dead() || !addonBits.Target_Hostile()) return;

        // 4. 检查 WoW 进程是否为前台活动窗口
        if (!WinAPI.ScreenCaptureKitInterop.is_process_frontmost(processInfo.ProcessId)) return;

        // 5. 检查技能 1 是否有快捷键
        if (string.IsNullOrEmpty(hekiliReader.Spell1Keybind)) return;

        // 6. 检查技能 1 是否可用
        // Hekili 的 unusable 状态已包含所有检查:
        // - 能量不足 (法力/怒气/能量)
        // - 距离不够
        // - 条件不满足
        // - CD/GCD/施法中
        if (!hekiliReader.Spell1Usable) return;

        // 7. 防抖: 避免短时间内重复发送
        var now = DateTime.UtcNow;
        var elapsed = (now - lastKeybindSentTime).TotalMilliseconds;
        
        // 7.1 全局按键最小间隔检查
        if (elapsed < KEYBIND_COOLDOWN_MS)
        {
            return;
        }
        
        // 7.2 同一技能强制冷却检查（防止技能释放后 GCD 延迟导致重复发送）
        if (Spell1 == lastSentSpellId && elapsed < SAME_SPELL_COOLDOWN_MS)
        {
            // logger.LogDebug("[自动按键] 同一技能冷却中: {Spell1Name} | 已过: {Elapsed}ms / {SameSpellCooldownMs}ms", Spell1Name, elapsed, SAME_SPELL_COOLDOWN_MS);
            return;
        }

        // 8. 发送快捷键
        // logger.LogDebug("[自动按键] 准备发送: {Spell1Keybind} ({Spell1Name}) [ID:{Spell1}] | 距上次: {Elapsed}ms", Spell1Keybind, Spell1Name, Spell1, elapsed);
        
        bool success = KeybindMapper.SendKeybind(hekiliReader.Spell1Keybind);
        if (success)
        {
            lastKeybindSentTime = now;
            lastSentSpellId = Spell1;  // 记录发送的技能 ID
            LastKeySentDisplay = $"⚡ {DateTime.Now:HH:mm:ss.fff}";
            // logger.LogDebug("[自动按键] \u2713 发送成功: {HekiliReaderSpell1Keybind} ({Spell1Name})", hekiliReader.Spell1Keybind, Spell1Name);
        }
        else
        {
            logger.LogWarning("[自动按键] \u2717 发送失败: {HekiliReaderSpell1Keybind} ({Spell1Name})", hekiliReader.Spell1Keybind, Spell1Name);
        }
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
        // 取消事件订阅 (防止回调到已释放的对象)
        screen.OnFrameUpdated -= OnScreenFrameUpdated;
        screen.OnStreamError -= OnScreenError;

        // 手动释放 native 资源 (在 Scope 销毁前提前释放)
        // 内部有原子检查,防止重复 Dispose
        screen.Dispose();
    }
}