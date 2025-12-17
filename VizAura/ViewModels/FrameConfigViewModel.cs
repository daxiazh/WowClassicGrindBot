using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using Microsoft.Extensions.Logging;
using SharedLib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using VizAura.MacOS;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// Frame 配置窗口 ViewModel (Manual 模式)
/// </summary>
public sealed partial class FrameConfigViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<FrameConfigViewModel> logger;
    private readonly WowScreenMacOS screen;
    private readonly WowProcessInfo processInfo;
    private readonly AddonConfig addonConfig;
    private readonly object addonImageLock = new();
    private DispatcherTimer? updateTimer;
    private Image<Bgra32>? currentAddonImage;

    private DataFrameMeta currentMeta = DataFrameMeta.Empty;
    private DataFrame[] currentFrames = [];
    private Rectangle screenRect;
    private Image<Bgra32>? currentScreenImage;
    private bool waitingForNormalMode = false;

    /// <summary>
    /// 当前步骤提示
    /// </summary>
    [ObservableProperty]
    private string currentStep = "准备就绪";

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string statusMessage = "点击\"开始手动配置\"启动实时监测";

    /// <summary>
    /// 检测到的数据帧数量
    /// </summary>
    [ObservableProperty]
    private int detectedFrameCount;

    /// <summary>
    /// 玩家信息 (种族/职业/版本)
    /// </summary>
    [ObservableProperty]
    private string playerInfo = "未检测到";

    /// <summary>
    /// 完整屏幕预览图
    /// </summary>
    [ObservableProperty]
    private WriteableBitmap? fullScreenBitmap;

    /// <summary>
    /// DataToColor 区域预览图
    /// </summary>
    [ObservableProperty]
    private WriteableBitmap? addonBitmap;

    /// <summary>
    /// 是否可以保存
    /// </summary>
    [ObservableProperty]
    private bool canSave;

    /// <summary>
    /// 是否处于配置模式 (插件显示彩色标记)
    /// </summary>
    [ObservableProperty]
    private bool isConfigMode;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    [ObservableProperty]
    private bool isRunning;

    /// <summary>
    /// 插件命令
    /// </summary>
    public string Command => $"/{addonConfig.Command}";

    /// <summary>
    /// 配置保存完成事件
    /// </summary>
    public event Action? OnConfigSaved;
    
    /// <summary>
    /// Bitmap 更新事件 (用于触发 UI 重绘)
    /// </summary>
    public event Action? OnBitmapUpdated;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="processInfo">WoW 进程信息</param>
    /// <param name="addonConfig">插件配置</param>
    public FrameConfigViewModel(
        ILogger<FrameConfigViewModel> logger,
        WowProcessInfo processInfo,
        AddonConfig addonConfig)
    {
        this.logger = logger;
        this.processInfo = processInfo;
        this.addonConfig = addonConfig;

        // 获取窗口矩形 (从 macOS 获取窗口位置和大小)
        screenRect = MacOSWindowHelper.GetWindowBounds((int)processInfo.WindowId);
        
        // 创建屏幕捕获实例
        screen = new WowScreenMacOS(processInfo.WindowId, screenRect);
        
        logger.LogInformation($"FrameConfigViewModel 初始化完成, 窗口: {screenRect}");
        
        // 创建 WriteableBitmap
        FullScreenBitmap = new WriteableBitmap(
            new PixelSize(screenRect.Width, screenRect.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul
        );
        
        // 立即启用屏幕捕获并订阅帧更新事件
        screen.Enabled = true;
        screen.OnFrameUpdated += OnScreenFrameUpdated;
        
        // 仍然使用定时器进行配置检测 (但不用于预览更新)
        StartConfigDetectionTimer();
    }

    /// <summary>
    /// 开始手动配置
    /// </summary>
    [RelayCommand]
    private void StartManual()
    {
        logger.LogInformation("开始手动配置");

        IsRunning = true;
        CurrentStep = "步骤 1: 等待进入配置模式";
        StatusMessage = $"请在游戏中输入: {Command}";
    }

    /// <summary>
    /// 帧更新事件处理 (在 native 线程中被调用)
    /// </summary>
    private void OnScreenFrameUpdated()
    {
        // 切换到 UI 线程
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 获取当前屏幕尺寸
                screen.GetRectangle(out var currentRect);
                
                // 检查是否需要重新创建 Bitmap (尺寸变化)
                if (FullScreenBitmap == null || 
                    FullScreenBitmap.PixelSize.Width != currentRect.Width || 
                    FullScreenBitmap.PixelSize.Height != currentRect.Height)
                {
                    FullScreenBitmap?.Dispose();
                    FullScreenBitmap = new WriteableBitmap(
                        new PixelSize(currentRect.Width, currentRect.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul
                    );
                }
                
                // 复制屏幕图像到 Bitmap (复用对象)
                screen.CopyToWriteableBitmap(FullScreenBitmap!);
                
                // 触发 Bitmap 更新事件,让 View 调用 InvalidateVisual
                OnBitmapUpdated?.Invoke();
                
                // 如果有 Addon 图像,也更新
                lock (addonImageLock)
                {
                    if (currentAddonImage != null && AddonBitmap != null)
                    {
                        UpdateBitmapPixels(AddonBitmap, currentAddonImage);
                        OnPropertyChanged(nameof(AddonBitmap));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新屏幕预览失败");
            }
        });
    }
    
    /// <summary>
    /// 启动配置检测定时器 (用于检测配置模式,不用于预览更新)
    /// </summary>
    private void StartConfigDetectionTimer()
    {
        // 启动定时器,每 500ms 检测一次配置状态
        updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        updateTimer.Tick += OnConfigDetectionTick;
        updateTimer.Start();
    }

    /// <summary>
    /// 停止手动配置
    /// </summary>
    [RelayCommand]
    private void StopManual()
    {
        logger.LogInformation("停止手动配置");

        IsRunning = false;
        CurrentStep = "已停止";
        StatusMessage = "手动配置已停止";
        currentScreenImage?.Dispose();
        currentScreenImage = null;

        // 不停止定时器,继续预览
        // updateTimer?.Stop();
        // 不禁用屏幕捕获,继续预览
        // screen.Enabled = false;
    }

    /// <summary>
    /// 配置检测定时器回调 - 检测配置模式
    /// </summary>
    private void OnConfigDetectionTick(object? sender, EventArgs e)
    {
        try
        {
            // TODO: 提示玩家应该在游戏中输出"/dc"指令来切换到检测模式
            // 1. 查找 RGB 定位序列,获取中心点 Y 坐标和 cell 大小
            var (idx0Y, cellSize) = screen.FindRGBPatternInColumn0();
            if (idx0Y == -1)
            {
                // 未找到 RGB 定位序列
                if (waitingForNormalMode)
                {
                    // 用户已切换回正常模式
                    logger.LogInformation("检测到用户已切换回正常模式");
                    StatusMessage = "✅ 已切换回正常模式";
                    CurrentStep = "配置完成";
                    
                    // 停止配置并触发事件
                    StopManual();
                    OnConfigSaved?.Invoke();
                    waitingForNormalMode = false;
                    return;
                }
                
                if (IsRunning)
                {
                    StatusMessage = "未检测到 DataToColor 的配置模式, 请在 WOW 中输入\"/dc\"来激活配置模式";
                }
                return;
            }
            
            // 如果正在等待正常模式,但仍然检测到 RGB 序列,继续等待
            if (waitingForNormalMode)
            {
                StatusMessage = $"✅ 配置已保存!\n请在游戏中输入 {Command} 切换回正常模式";
                return;
            }
            
            // 需要复制一份 screen 的数据到 currentScreenImage, 用于消除线程安全问题
            screen.GetRectangle(out var currentRect);
            if (currentScreenImage == null || 
                currentScreenImage.Width != currentRect.Width ||
                currentScreenImage.Height != currentRect.Height)
            {
                // 尺寸变化,重新创建图像
                currentScreenImage?.Dispose();
                currentScreenImage = screen.Clone();
            }
            else
            {
                // 尺寸未变化,高效复制数据 (无 GC 分配)
                screen.CopyTo(currentScreenImage);
            }
            
            // 2. 查找 Idx1 frame (B=1, R=0, G=0) - 只在 centerY 这一行查找
            if (!FrameConfig.TryGetNextPoint(currentScreenImage, 1, cellSize, idx0Y, out int idx1X, out int idx1Y))
            {
                if(logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("找到 RGB 定位序列 (Y={CenterY}, cellSize={CellSize}),但未找到 Idx[1] 定位帧", idx0Y, cellSize);
                StatusMessage = $"找到 RGB 序列 (Y={idx0Y})\n未找到 Idx[1] 定位帧";
                return;
            }
            
            // 检查idx1X 与 idx1Y 是否合法
            if (idx1X > cellSize * 4)
            {
                StatusMessage = $"找到 RGB 序列 (Y={idx0Y})\nIdx[1] X={idx1X} 非法";
                if(logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Idx[1] X={Idx1X} 值超出范围({MaxValue})", idx1X, cellSize * 4);
                return;
            }
            
            // 3. 获取 Meta 信息
            var idx0X = idx1X - cellSize / 2;
            var dataFrameMeta = FrameConfig.GetMeta(currentScreenImage[idx0X, idx0Y]);
            if (dataFrameMeta == DataFrameMeta.Empty)
            {
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("找到 Idx[1] (X={Idx1X}),但读取 Meta 信息失败", idx1X);
                StatusMessage = $"找到 Idx[1] (X={idx1X})\n但读取 Meta 信息失败";
                return;
            }
            
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("读取 Meta 信息成功: Spacing={Spacing}, Size={Size}, Rows={Rows}, Count={Count}", 
                    dataFrameMeta.Spacing, dataFrameMeta.Sizes, dataFrameMeta.Rows, dataFrameMeta.Count);
            
            // 4. 定位所有的 Frame
            var dataFrames = FrameConfig.CreateFrames(dataFrameMeta, currentScreenImage, idx0X, idx0Y);
            if (dataFrames.Length != dataFrameMeta.Count)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Frame 数量不匹配: 期望={Expected}, 实际={Actual}", dataFrameMeta.Count, dataFrames.Length);
                StatusMessage = $"检测到部分 Frame\n期望:{dataFrameMeta.Count}, 实际:{dataFrames.Length}";
                return;
            }
            
            // 5. 验证 Frame 间隔是否合法 (每个 frame 的 X 间隔应该大致相同,误差不超过 1 像素)
            if (!ValidateFrameSpacing(dataFrames, dataFrameMeta, out string? spacingError))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Frame 间隔验证失败: {Error}", spacingError);
                StatusMessage = $"Frame 间隔异常\n{spacingError}";
                return;
            }

            // 6. 成功检测到完整配置!
            currentMeta = dataFrameMeta;
            currentFrames = dataFrames;
            DetectedFrameCount = dataFrames.Length;
            CanSave = true;
            
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("✅ 成功检测到完整配置: Y={CenterY}, CellSize={CellSize}, Frames={Count}", 
                    idx0Y, cellSize, dataFrames.Length);
            StatusMessage = $"✅ 检测成功!\nFrames: {dataFrames.Length}, CellSize: {cellSize}";
            
            // 自动保存配置
            SaveCommand.Execute(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "定时器更新时出错");
            StatusMessage = $"错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        try
        {
            if (currentFrames.Length == 0 || currentMeta == DataFrameMeta.Empty)
            {
                logger.LogWarning("无法保存: 数据帧为空");
                StatusMessage = "❌ 无法保存: 请先完成配置";
                return;
            }

            // 获取插件版本
            var addonVersion = new Version(1, 0, 0); // TODO: 从 AddonConfigurator 获取
            if (AddonConfig.Exists())
            {
                var config = AddonConfig.Load();
                // addonVersion = addonConfigurator.GetInstallVersion() ?? addonVersion;
            }

            // 保存配置
            screen.GetRectangle(out screenRect);
            FrameConfig.Save(screenRect, addonVersion, currentMeta, currentFrames);
            
            logger.LogInformation($"Frame 配置已保存: {currentFrames.Length} 帧");
            StatusMessage = $"✅ 配置已保存!\n请在游戏中输入 {Command} 切换回正常模式";
            CurrentStep = "等待切换回正常模式";
            
            // 设置等待状态,不立即停止
            waitingForNormalMode = true;
            // 不调用 StopManual(),继续检测
            // 不触发 OnConfigSaved,等待用户切换回正常模式
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "保存配置时出错");
            StatusMessage = $"❌ 保存失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 更新 WriteableBitmap 的像素数据 (复用 Bitmap,不创建新对象)
    /// </summary>
    /// <param name="bitmap">WriteableBitmap</param>
    /// <param name="image">ImageSharp 图像</param>
    private static void UpdateBitmapPixels(WriteableBitmap bitmap, Image<Bgra32> image)
    {
        using var frameBuffer = bitmap.Lock();
        
        // 逐行复制像素数据
        image.ProcessPixelRows(accessor =>
        {
            unsafe
            {
                var destPtr = (byte*)frameBuffer.Address;
                var stride = frameBuffer.RowBytes;
                
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    fixed (Bgra32* srcPtr = row)
                    {
                        Buffer.MemoryCopy(
                            srcPtr,
                            destPtr + (y * stride),
                            stride,
                            row.Length * 4
                        );
                    }
                }
            }
        });
    }

    /// <summary>
    /// 验证 Frame 间隔是否合法
    /// </summary>
    /// <param name="frames">Frame 数组</param>
    /// <param name="meta">Meta 信息</param>
    /// <param name="error">错误信息</param>
    /// <returns>是否合法</returns>
    private static bool ValidateFrameSpacing(DataFrame[] frames, DataFrameMeta meta, out string? error)
    {
        if (frames.Length < 2)
        {
            error = null;
            return true;
        }
        
        // 期望的间隔 = cellSize + spacing
        int expectedSpacing = meta.Sizes + meta.Spacing;
        
        // 检查连续 frame 的 X 间隔
        for (int i = 2; i < frames.Length; i++)
        {
            int actualSpacing = frames[i].X - frames[i - 1].X;
            int difference = Math.Abs(actualSpacing - expectedSpacing);
            
            // 允许 ±1 像素的误差
            if (difference > 1)
            {
                error = $"Frame[{i}] 间隔异常\n期望:{expectedSpacing}, 实际:{actualSpacing}";
                return false;
            }
        }
        
        error = null;
        return true;
    }
    
    /// <summary>
    /// 尝试解析种族和职业
    /// </summary>
    /// <param name="race">种族</param>
    /// <param name="class">职业</param>
    /// <param name="version">客户端版本</param>
    /// <returns>是否成功</returns>
    private bool TryResolveRaceAndClass(out UnitRace race, out UnitClass @class, out ClientVersion version)
    {
        if (screen.Data.Length < 46)
        {
            race = 0;
            @class = 0;
            version = 0;
            return false;
        }

        int value = screen.Data[46];

        // RACE_ID * 10000 + CLASS_ID * 100 + ClientVersion
        race = (UnitRace)(value / 10000);
        @class = (UnitClass)(value / 100 % 100);
        version = (ClientVersion)(value % 10);

        return Enum.IsDefined(race) && Enum.IsDefined(@class) && Enum.IsDefined(version) &&
            race != UnitRace.None && @class != UnitClass.None && version != ClientVersion.None;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        currentScreenImage?.Dispose();
        currentScreenImage = null;
        updateTimer?.Stop();
        screen.OnFrameUpdated -= OnScreenFrameUpdated;
        screen?.Dispose();
        currentAddonImage?.Dispose();
        FullScreenBitmap?.Dispose();
        AddonBitmap?.Dispose();
    }
}
