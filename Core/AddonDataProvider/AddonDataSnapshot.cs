using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace Core;

/// <summary>
/// Addon 数据快照 - 线程安全的 IAddonDataProvider 实现
/// 封装了 DataFrame 配置和数据数组,提供线程安全的数据读取和更新
/// </summary>
public sealed class AddonDataSnapshot : IAddonDataProvider
{
    private readonly DataFrame[] frames;
    private readonly int[] data;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="frames">DataFrame 配置数组</param>
    public AddonDataSnapshot(DataFrame[] frames)
    {
        this.frames = frames;
        this.data = new int[frames.Length];
    }

    /// <summary>
    /// 从原始像素指针更新数据 (线程安全,零拷贝,高性能)
    /// </summary>
    /// <param name="dataPtr">像素数据指针</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="bytesPerRow">每行字节数</param>
    public unsafe void UpdateFromRawData(IntPtr dataPtr, int width, int height, int bytesPerRow)
    {
        byte* source = (byte*)dataPtr.ToPointer();
        int pixelSize = sizeof(Bgra32);
        
        // 1. 首帧校验 (Frame[0] 必须为 0,即黑色)
        DataFrame firstFrame = frames[0];
        if (firstFrame.Y >= height || firstFrame.X >= width)
            return; // 坐标越界
        
        byte* firstPixelPtr = source + (firstFrame.Y * bytesPerRow) + (firstFrame.X * pixelSize);
        Bgra32* firstPixel = (Bgra32*)firstPixelPtr;
        
        if (firstPixel->R != 0 || firstPixel->G != 0 || firstPixel->B != 0)
        {
            return; // 首帧校验失败
        }
        
        // 2. 读取所有帧数据
        for (int i = 0; i < frames.Length; i++)
        {
            DataFrame frame = frames[i];
            
            // 边界检查
            if (frame.Y >= height || frame.X >= width)
            {
                data[frame.Index] = 0;
                continue;
            }
            
            // 计算像素地址
            byte* pixelPtr = source + (frame.Y * bytesPerRow) + (frame.X * pixelSize);
            Bgra32* pixel = (Bgra32*)pixelPtr;
            
            // 解析颜色值到整数: value = R * 65536 + G * 256 + B
            data[frame.Index] = pixel->B | (pixel->G << 8) | (pixel->R << 16);
        }
        
        // 3. CRC16 校验 (Frame[0..n-2] 的数据 vs Frame[n-1] 的 CRC)
        int receivedCRC = data[^1];
        int calculatedCRC = IAddonDataProvider.CalculateCRC16(data[..^1]);
        
        if (receivedCRC != calculatedCRC)
        {
            // CRC 校验失败,清空数据防止使用脏数据
            Array.Clear(data);
            return;
        }
    }
    
    /// <summary>
    /// 从屏幕图像更新数据 (用于兼容性,如 FrameConfigViewModel)
    /// </summary>
    /// <param name="screenImage">完整的屏幕图像</param>
    public void UpdateFromScreen(Image<Bgra32> screenImage)
    {
        // 使用 IAddonDataProvider 的静态方法解析数据
        // 包含 CRC16 校验
        IAddonDataProvider.InternalUpdate(screenImage, frames, data);
    }

    /// <summary>
    /// 数据数组 (线程安全访问)
    /// </summary>
    public int[] Data
    {
        get { return data; }
    }

    public void UpdateData()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 初始化 Frames (已在构造函数中初始化)
    /// </summary>
    /// <param name="frames">DataFrame 数组</param>
    public void InitFrames(DataFrame[] frames)
    {
        // 已在构造函数中初始化,此方法保留用于接口兼容
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 不持有非托管资源,无需释放
    }

    // IAddonDataProvider 的辅助方法 - 线程安全实现

    /// <summary>
    /// 读取整数值 (线程安全)
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>整数值</returns>
    public int GetInt(int index)
    {
        return data[index];
    }

    /// <summary>
    /// 读取定点数 (线程安全)
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>浮点数值</returns>
    public float GetFixed(int index)
    {
        return data[index] / 100000f;
    }

    /// <summary>
    /// 读取字符串 (线程安全)
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>字符串值</returns>
    public string GetString(int index)
    {
        int color = data[index];
        if ((uint)color > 999999)
            return string.Empty;

        Span<char> buffer = stackalloc char[3];
        int count = 0;

        int n1 = color / 10000;
        int n2 = color / 100 % 100;
        int n3 = color % 100;

        if (n1 > 0) buffer[count++] = (char)n1;
        if (n2 > 0) buffer[count++] = (char)n2;
        if (n3 > 0) buffer[count++] = (char)n3;

        return buffer[..count].ToString();
    }
}