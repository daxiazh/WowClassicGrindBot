using Avalonia.Media.Imaging;
using Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using WinAPI;

namespace VizAura.MacOS;

/// <summary>
/// macOS 平台的 WoW 屏幕捕获实现
/// 使用 ScreenCaptureKit 进行窗口捕获
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class WowScreenMacOS : Game.IWowScreen, IAddonDataProvider
{
    private readonly uint windowId;
    private IntPtr streamHandle;
    private readonly ScreenCaptureKitInterop.FrameCallback? frameCallback;
    
    private Image<Bgra32> screenImage;
    private Image<Bgra32> addonImage;
    private readonly Image<Bgra32> minimapImage;
    
    private DataFrame[] frames = [];
    private int[] data = [];
    
    private Rectangle screenRect;
    private SixLabors.ImageSharp.Size addonSize;
    
    private readonly Lock frameLock = new();
    
    /// <summary>
    /// 是否启用屏幕捕获
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// 是否启用小地图捕获
    /// </summary>
    public bool MinimapEnabled { get; set; }
    
    /// <summary>
    /// 是否启用后处理
    /// </summary>
    public bool EnablePostProcess { get; set; }
    
    /// <summary>
    /// 屏幕图像 (实现接口,但不建议直接使用)
    /// 警告: 直接访问此属性不是线程安全的,请使用 GetPixel/CloneAndCrop 等方法
    /// </summary>
    public Image<Bgra32> ScreenImage
    {
        get
        {
            lock (frameLock)
            {
                return screenImage;
            }
        }
    }
    
    /// <summary>
    /// 小地图图像
    /// </summary>
    public Image<Bgra32> MiniMapImage => minimapImage;
    
    /// <summary>
    /// 屏幕矩形区域
    /// </summary>
    public Rectangle ScreenRect => screenRect;
    
    /// <summary>
    /// 小地图矩形区域
    /// </summary>
    public Rectangle MiniMapRect { get; private set; }
    
    /// <summary>
    /// 数据数组
    /// </summary>
    public int[] Data => data;
    
    /// <summary>
    /// 变更事件
    /// </summary>
    public event Action? OnChanged;
    
    /// <summary>
    /// 帧更新事件 (每次 ScreenCaptureKit 捕获到新帧时触发)
    /// </summary>
    public event Action? OnFrameUpdated;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="windowId">窗口 ID</param>
    /// <param name="initialRect">初始窗口矩形</param>
    public WowScreenMacOS(uint windowId, Rectangle initialRect)
    {
        this.windowId = windowId;
        this.screenRect = initialRect;
        
        // 初始化图像
        screenImage = new Image<Bgra32>(initialRect.Width, initialRect.Height);
        minimapImage = new Image<Bgra32>(200, 200);
        addonImage = new Image<Bgra32>(1, 1); // 初始化为最小尺寸,后续会调整
        
        MiniMapRect = new Rectangle(initialRect.Width - 200, 0, 200, 200);
        
        // 启动 ScreenCaptureKit 流
        // 重要: 保存 callback 引用防止被 GC 回收
        frameCallback = new ScreenCaptureKitInterop.FrameCallback(OnFrameReceived);
        streamHandle = ScreenCaptureKitInterop.sc_create_stream(windowId, frameCallback);
        
        if (streamHandle == IntPtr.Zero)
        {
            throw new Exception("Failed to create ScreenCaptureKit stream");
        }
    }
    
    /// <summary>
    /// ScreenCaptureKit 帧回调
    /// </summary>
    /// <param name="data">像素数据指针</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="bytesPerRow">每行字节数</param>
    private void OnFrameReceived(IntPtr data, int width, int height, int bytesPerRow)
    {
        lock (frameLock)
        {
            // 更新屏幕矩形尺寸
            if (screenRect.Width != width || screenRect.Height != height)
            {
                screenRect = new Rectangle(screenRect.X, screenRect.Y, width, height);
                
                // 重新创建图像
                screenImage.Dispose();
                screenImage = new Image<Bgra32>(width, height);
                
                // 更新小地图位置
                MiniMapRect = new Rectangle(width - 200, 0, 200, 200);
            }
            
            // 如果启用,复制帧数据到 ScreenImage
            if (Enabled && screenImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> screenMemory))
            {
                CopyFrameData(data, width, height, bytesPerRow, screenMemory);
            }
            
            // 更新 addon 区域
            if (frames.Length > 0 && addonImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> addonMemory))
            {
                CopyAddonData(data, width, height, bytesPerRow, addonMemory);
            }
            
            // 更新小地图区域
            if (MinimapEnabled && minimapImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> minimapMemory))
            {
                CopyMinimapData(data, width, height, bytesPerRow, minimapMemory);
            }
            
            // 触发帧更新事件
            OnFrameUpdated?.Invoke();
        }
    }
    
    /// <summary>
    /// 将屏幕图像复制到 WriteableBitmap (线程安全)
    /// </summary>
    /// <param name="bitmap">WriteableBitmap</param>
    public void CopyToWriteableBitmap(WriteableBitmap bitmap)
    {
        lock (frameLock)
        {
            using var frameBuffer = bitmap.Lock();
            
            screenImage.ProcessPixelRows(accessor =>
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
    }
    
    /// <summary>
    /// 读取屏幕指定位置的像素 (线程安全)
    /// </summary>
    /// <param name="x">x 坐标</param>
    /// <param name="y">y 坐标</param>
    /// <returns>像素值</returns>
    public Bgra32 GetPixel(int x, int y)
    {
        lock (frameLock)
        {
            return screenImage[x, y];
        }
    }
    
    /// <summary>
    /// 克隆并裁剪屏幕区域 (线程安全)
    /// </summary>
    /// <param name="width">裁剪宽度</param>
    /// <param name="height">裁剪高度</param>
    /// <returns>裁剪后的图像</returns>
    public Image<Bgra32> CloneAndCrop(int width, int height)
    {
        lock (frameLock)
        {
            var cropRect = new SixLabors.ImageSharp.Rectangle(0, 0, width, height);
            var cloned = screenImage.Clone();
            cloned.Mutate(x => x.Crop(cropRect));
            return cloned;
        }
    }
    
    /// <summary>
    /// 复制完整帧数据
    /// </summary>
    private static unsafe void CopyFrameData(IntPtr sourcePtr, int width, int height, int bytesPerRow, Memory<Bgra32> destination)
    {
        byte* source = (byte*)sourcePtr.ToPointer();
        Span<Bgra32> dest = destination.Span;
        
        int pixelSize = sizeof(Bgra32);
        int destRowBytes = width * pixelSize;
        
        for (int y = 0; y < height; y++)
        {
            byte* srcRow = source + (y * bytesPerRow);
            Span<byte> destRow = MemoryMarshal.Cast<Bgra32, byte>(dest.Slice(y * width, width));
            
            new Span<byte>(srcRow, destRowBytes).CopyTo(destRow);
        }
    }
    
    /// <summary>
    /// 复制 addon 区域数据
    /// </summary>
    private unsafe void CopyAddonData(IntPtr sourcePtr, int width, int height, int bytesPerRow, Memory<Bgra32> destination)
    {
        if (addonSize.Width == 0 || addonSize.Height == 0)
            return;
        
        byte* source = (byte*)sourcePtr.ToPointer();
        Span<Bgra32> dest = destination.Span;
        
        int pixelSize = sizeof(Bgra32);
        int copyWidth = Math.Min(addonSize.Width, width);
        int copyHeight = Math.Min(addonSize.Height, height);
        int destRowBytes = copyWidth * pixelSize;
        
        for (int y = 0; y < copyHeight; y++)
        {
            byte* srcRow = source + (y * bytesPerRow);
            Span<byte> destRow = MemoryMarshal.Cast<Bgra32, byte>(dest.Slice(y * addonSize.Width, copyWidth));
            
            new Span<byte>(srcRow, destRowBytes).CopyTo(destRow);
        }
    }
    
    /// <summary>
    /// 复制小地图区域数据
    /// </summary>
    private unsafe void CopyMinimapData(IntPtr sourcePtr, int width, int height, int bytesPerRow, Memory<Bgra32> destination)
    {
        byte* source = (byte*)sourcePtr.ToPointer();
        Span<Bgra32> dest = destination.Span;
        
        int pixelSize = sizeof(Bgra32);
        int minimapWidth = MiniMapRect.Width;
        int minimapHeight = MiniMapRect.Height;
        int minimapX = MiniMapRect.X;
        int minimapY = MiniMapRect.Y;
        int destRowBytes = minimapWidth * pixelSize;
        
        for (int y = 0; y < minimapHeight; y++)
        {
            int srcY = minimapY + y;
            if (srcY >= height) break;
            
            byte* srcRow = source + (srcY * bytesPerRow) + (minimapX * pixelSize);
            Span<byte> destRow = MemoryMarshal.Cast<Bgra32, byte>(dest.Slice(y * minimapWidth, minimapWidth));
            
            new Span<byte>(srcRow, destRowBytes).CopyTo(destRow);
        }
    }
    
    /// <summary>
    /// 更新 (空实现,帧通过回调更新)
    /// </summary>
    public void Update()
    {
        // 帧数据通过 ScreenCaptureKit 回调异步更新
        // 此方法保持空实现以兼容 IWowScreen 接口
    }
    
    /// <summary>
    /// 后处理
    /// </summary>
    public void PostProcess()
    {
        OnChanged?.Invoke();
    }
    
    /// <summary>
    /// 获取窗口矩形
    /// </summary>
    /// <param name="rect">输出矩形</param>
    public void GetRectangle(out Rectangle rect)
    {
        lock (frameLock)
        {
            rect = screenRect;
        }
    }
    
    /// <summary>
    /// 获取窗口位置
    /// </summary>
    /// <param name="point">输出位置</param>
    public void GetPosition(ref SixLabors.ImageSharp.Point point)
    {
        lock (frameLock)
        {
            point = new SixLabors.ImageSharp.Point(screenRect.X, screenRect.Y);
        }
    }
    
    /// <summary>
    /// 初始化数据帧
    /// </summary>
    /// <param name="frames">数据帧数组</param>
    public void InitFrames(DataFrame[] frames)
    {
        lock (frameLock)
        {
            this.frames = frames;
            data = new int[frames.Length];
            
            // 计算 addon 区域大小
            addonSize = new SixLabors.ImageSharp.Size();
            for (int i = 0; i < frames.Length; i++)
            {
                addonSize.Width = Math.Max(addonSize.Width, frames[i].X);
                addonSize.Height = Math.Max(addonSize.Height, frames[i].Y);
            }
            addonSize.Width++;
            addonSize.Height++;
            
            // 重新创建 addon 图像
            addonImage.Dispose();
            addonImage = new Image<Bgra32>(addonSize.Width, addonSize.Height);
        }
    }
    
    /// <summary>
    /// 更新数据
    /// </summary>
    public void UpdateData()
    {
        if (frames.Length <= 2)
            return;
        
        lock (frameLock)
        {
            IAddonDataProvider.InternalUpdate(addonImage, frames, data);
        }
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (streamHandle != IntPtr.Zero)
        {
            ScreenCaptureKitInterop.sc_stop_stream(streamHandle);
            streamHandle = IntPtr.Zero;
        }
        
        screenImage.Dispose();
        addonImage.Dispose();
        minimapImage.Dispose();
    }
}
