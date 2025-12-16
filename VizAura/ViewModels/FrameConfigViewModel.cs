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
            // 1. 读取屏幕左上角像素,获取 DataFrameMeta (线程安全)
            var pixel = screen.GetPixel(0, 0);
            var meta = FrameConfig.GetMeta(pixel);

            // 2. 检测是否进入配置模式
            if (meta != DataFrameMeta.Empty)
            {
                // 首次进入配置模式
                if (!IsConfigMode)
                {
                    logger.LogInformation($"检测到配置模式: {meta}");
                    IsConfigMode = true;
                    CurrentStep = "步骤 2: 配置模式已激活";
                    currentMeta = meta;
                }

                DetectedFrameCount = meta.Count;
                StatusMessage = $"检测到 {meta.Count} 个数据帧\nSpacing: {meta.Spacing}, Size: {meta.Sizes}, Rows: {meta.Rows}";

                // 3. 创建 DataFrames
                screen.GetRectangle(out screenRect);
                var size = meta.EstimatedSize(screenRect);
                
                if (!size.IsEmpty)
                {
                    // 裁剪 addon 区域 (线程安全)
                    var cropped = screen.CloneAndCrop(size.Width, size.Height);
                    
                    lock (addonImageLock)
                    {
                        // 释放旧的 addon 图像
                        currentAddonImage?.Dispose();
                        currentAddonImage = cropped;
                    }
                    
                    // 创建数据帧
                    currentFrames = FrameConfig.CreateFrames(meta, cropped);
                    
                    // 创建 AddonBitmap (只创建一次)
                    if (AddonBitmap == null || AddonBitmap.PixelSize.Width != cropped.Width || AddonBitmap.PixelSize.Height != cropped.Height)
                    {
                        AddonBitmap?.Dispose();
                        AddonBitmap = new WriteableBitmap(
                            new PixelSize(cropped.Width, cropped.Height),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Premul
                        );
                    }
                    
                    logger.LogDebug($"创建了 {currentFrames.Length} 个数据帧");
                }
            }
            else
            {
                // 退出配置模式
                if (IsConfigMode)
                {
                    logger.LogInformation("退出配置模式");
                    IsConfigMode = false;
                    CurrentStep = "步骤 3: 验证数据";
                    StatusMessage = $"再次输入 {Command} 已退出配置模式,正在验证数据...";
                    
                    // 4. 初始化 AddonDataProvider
                    if (currentFrames.Length > 0)
                    {
                        screen.InitFrames(currentFrames);
                    }
                }

                // 5. 尝试读取玩家信息
                if (currentFrames.Length > 0 && currentMeta != DataFrameMeta.Empty)
                {
                    screen.UpdateData();
                    
                    if (TryResolveRaceAndClass(out UnitRace race, out UnitClass @class, out ClientVersion version))
                    {
                        PlayerInfo = $"{version.ToStringF()} {race.ToStringF()} {@class.ToStringF()}";
                        CurrentStep = "步骤 4: 检测成功!";
                        StatusMessage = $"检测到玩家: {PlayerInfo}\n可以保存配置了";
                        CanSave = true;
                        
                        logger.LogInformation($"检测到玩家信息: {PlayerInfo}");
                    }
                    else
                    {
                        PlayerInfo = "读取中...";
                        StatusMessage = "正在读取玩家数据...";
                    }
                }
            }
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
            StatusMessage = "✅ 配置已保存!";
            
            // 停止配置
            StopManual();
            
            // 触发事件
            OnConfigSaved?.Invoke();
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
        updateTimer?.Stop();
        screen.OnFrameUpdated -= OnScreenFrameUpdated;
        screen?.Dispose();
        currentAddonImage?.Dispose();
        FullScreenBitmap?.Dispose();
        AddonBitmap?.Dispose();
    }
}
