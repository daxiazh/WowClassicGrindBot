using System;
using System.Runtime.InteropServices;

namespace WinAPI;

/// <summary>
/// ScreenCaptureKit Swift 动态库的 P/Invoke 绑定
/// 用于在 macOS 上实现高性能屏幕捕获
/// </summary>
public static class ScreenCaptureKitInterop
{
    private const string LibName = "libScreenCapture";
    
    /// <summary>
    /// 帧数据回调委托
    /// 当 ScreenCaptureKit 捕获到新帧时被调用
    /// </summary>
    /// <param name="data">像素数据指针 (BGRA32 格式)</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="bytesPerRow">每行字节数 (可能包含填充字节)</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FrameCallback(
        IntPtr data,
        int width,
        int height,
        int bytesPerRow
    );
    
    /// <summary>
    /// 创建屏幕捕获流
    /// </summary>
    /// <param name="windowID">目标窗口的 CGWindowID</param>
    /// <param name="callback">帧数据回调函数</param>
    /// <returns>管理器句柄,失败返回 IntPtr.Zero</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr sc_create_stream(
        uint windowID,
        FrameCallback callback
    );
    
    /// <summary>
    /// 停止并释放屏幕捕获流
    /// </summary>
    /// <param name="handle">由 sc_create_stream 返回的管理器句柄</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void sc_stop_stream(IntPtr handle);
}
