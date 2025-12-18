using Avalonia.Media.Imaging;
using Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
public sealed class WowScreenMacOS
{
    private IntPtr streamHandle;
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly ScreenCaptureKitInterop.FrameCallback? frameCallback;

    private Image<Bgra32>? screenImage;
    
    private readonly AddonDataSnapshot? addonDataSnapshot;
    
    private Rectangle screenRect;
    
    private readonly Lock frameLock = new();
    
    /// <summary>
    /// 是否启用屏幕捕获
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// 帧更新事件 (每次 ScreenCaptureKit 捕获到新帧时触发)
    /// </summary>
    public event Action? OnFrameUpdated;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="windowId">窗口 ID</param>
    /// <param name="initialRect">初始窗口矩形</param>
    /// <param name="frames">每个 Frame 的配置</param>
    public WowScreenMacOS(uint windowId, Rectangle initialRect, DataFrame[]? frames = null)
    {
        this.screenRect = initialRect;
        
        // 要在 sc_create_stream 前创建, 因为会在回调中访问它
        if(frames != null)
            addonDataSnapshot = new AddonDataSnapshot(frames);
      
        // 启动 ScreenCaptureKit 流
        // 重要: 保存 callback 引用防止被 GC 回收
        frameCallback = OnFrameReceived;
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
            if (Enabled)
            { // 启用了全屏抓取, 复制数据到 ScreenImage
                if (screenRect.Width != width || screenRect.Height != height || screenImage == null)
                {
                    screenRect = new Rectangle(screenRect.X, screenRect.Y, width, height);

                    screenImage?.Dispose();

                    // 创建配置:强制使用连续内存缓冲区
                    var imageConfig = Configuration.Default.Clone();
                    imageConfig.PreferContiguousImageBuffers = true;

                    screenImage = new Image<Bgra32>(imageConfig, width, height);
                }

                // 复制帧数据到 ScreenImage
                if (screenImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> screenMemory))
                {
                    CopyFrameData(data, width, height, bytesPerRow, screenMemory);
                }
            }

            // 更新 addon 数据快照 (直接从原始像素指针读取,零拷贝)
            if (addonDataSnapshot != null)
            {
                addonDataSnapshot.UpdateFromRawData(data, width, height, bytesPerRow);
            }
        }
        
        // 触发帧更新事件 (在锁外触发,避免回调中的潜在死锁)
        OnFrameUpdated?.Invoke();
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
            
            screenImage?.ProcessPixelRows(accessor =>
            {
                unsafe
                {
                    // ReSharper disable once AccessToDisposedClosure
                    var destPtr = (byte*)frameBuffer.Address;
                    // ReSharper disable once AccessToDisposedClosure
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
    /// 复制当前的图片
    /// </summary>
    /// <returns>新的图像副本</returns>
    public Image<Bgra32> Clone()
    {
        lock (frameLock)
        {
            return screenImage!.Clone();
        }
    }
    
    /// <summary>
    /// 复制当前屏幕图像到目标图像 (高效,无GC分配)
    /// </summary>
    /// <param name="destination">目标图像 (必须与源图像尺寸一致)</param>
    public void CopyTo(Image<Bgra32> destination)
    {
        lock (frameLock)
        {
            if (destination.Width != screenImage!.Width || destination.Height != screenImage.Height)
            {
                throw new ArgumentException("目标图像尺寸必须与源图像一致");
            }
            
            // 使用 ProcessPixelRows 进行高效的逐行复制
            screenImage.ProcessPixelRows(destination, (sourceAccessor, destAccessor) =>
            {
                for (int y = 0; y < sourceAccessor.Height; y++)
                {
                    var sourceRow = sourceAccessor.GetRowSpan(y);
                    var destRow = destAccessor.GetRowSpan(y);
                    sourceRow.CopyTo(destRow);
                }
            });
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
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (streamHandle != IntPtr.Zero)
        {
            ScreenCaptureKitInterop.sc_stop_stream(streamHandle);
            streamHandle = IntPtr.Zero;
        }
        
        screenImage?.Dispose();
    }
    
    #region RGB 定位序列查找
    
    /// <summary>
    /// 判断像素是否为红色 (容错 ±5)
    /// </summary>
    /// <param name="p">像素</param>
    /// <returns>是否为红色</returns>
    private static bool IsRed(Bgra32 p) => p.R > 250 && p.G < 5 && p.B < 5;
    
    /// <summary>
    /// 判断像素是否为绿色 (容错 ±5)
    /// </summary>
    /// <param name="p">像素</param>
    /// <returns>是否为绿色</returns>
    private static bool IsGreen(Bgra32 p) => p.R < 5 && p.G > 250 && p.B < 5;
    
    /// <summary>
    /// 判断像素是否为蓝色 (容错 ±5)
    /// </summary>
    /// <param name="p">像素</param>
    /// <returns>是否为蓝色</returns>
    private static bool IsBlue(Bgra32 p) => p.R < 5 && p.G < 5 && p.B > 250;
    
    /// <summary>
    /// 在第 0 列查找 RGB 定位序列
    /// </summary>
    /// <returns>元组: (中心点 Y 坐标, cell 大小),未找到返回 (-1, 0)</returns>
    // ReSharper disable once InconsistentNaming
    public (int centerY, int cellSize) FindRGBPatternInColumn0()
    {
        lock (frameLock)
        {
            int maxY = Math.Min(screenImage!.Height / 10, 100);
            
            // screenImage.SaveAsBmp("screen.bmp"); // 调试代码
            
            for (int y = 0; y < maxY; y++)
            {
                // 1. 检查当前像素是否为红色
                if (!IsRed(screenImage[0, y]))
                    continue;
                
                // 2. 统计连续红色像素数量
                int redCount = CountConsecutiveColor(0, y, IsRed);
                if (redCount == 0) continue;
                
                // 3. 检查红色后面是否紧跟绿色
                int greenStartY = y + redCount;
                if (greenStartY >= screenImage.Height) continue;
                if (!IsGreen(screenImage[0, greenStartY])) continue;
                
                // 4. 统计连续绿色像素数量
                int greenCount = CountConsecutiveColor(0, greenStartY, IsGreen);
                if (greenCount == 0) continue;
                
                // 5. 检查绿色后面是否紧跟蓝色
                int blueStartY = greenStartY + greenCount;
                if (blueStartY >= screenImage.Height) continue;
                if (!IsBlue(screenImage[0, blueStartY])) continue;
                
                // 6. 统计连续蓝色像素数量
                int blueCount = CountConsecutiveColor(0, blueStartY, IsBlue);
                if (blueCount == 0) continue;
                
                // 7. 验证三种颜色的像素数量一致性 (±1px 容差)
                if (Math.Abs(redCount - greenCount) > 1 ||
                    Math.Abs(greenCount - blueCount) > 1)
                {
                    continue;
                }
                
                // 8. 找到了完整的 RGB 序列!
                // 返回红色块的中心点 Y 坐标(颜色最准确的位置)和 cell 大小
                int centerY = y + redCount / 2;
                return (centerY, redCount);
            }
            
            return (-1, 0);  // 未找到
        }
    }
    
    /// <summary>
    /// 从指定位置开始,统计连续满足颜色条件的像素数量
    /// </summary>
    /// <param name="x">X 坐标</param>
    /// <param name="startY">起始 Y 坐标</param>
    /// <param name="colorCheck">颜色判断函数</param>
    /// <returns>连续像素数量</returns>
    private int CountConsecutiveColor(int x, int startY, Func<Bgra32, bool> colorCheck)
    {
        int count = 0;
        for (int y = startY; y < screenImage!.Height; y++)
        {
            if (colorCheck(screenImage[x, y]))
                count++;
            else
                break;  // 遇到不同颜色,停止统计
        }
        return count;
    }
    
    #endregion
}
