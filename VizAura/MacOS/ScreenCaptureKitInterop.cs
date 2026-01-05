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
    /// 错误回调委托
    /// 当 ScreenCaptureKit 流发生错误时被调用
    /// </summary>
    /// <param name="errorCode">错误码 (SCStreamErrorCode)</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ErrorCallback(int errorCode);
    
    /// <summary>
    /// 创建屏幕捕获流
    /// </summary>
    /// <param name="windowID">目标窗口的 CGWindowID</param>
    /// <param name="frameCallback">帧数据回调函数</param>
    /// <param name="errorCallback">错误回调函数</param>
    /// <returns>管理器句柄,失败返回 IntPtr.Zero</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr sc_create_stream(
        uint windowID,
        FrameCallback frameCallback,
        ErrorCallback errorCallback
    );
    
    /// <summary>
    /// 停止并释放屏幕捕获流
    /// </summary>
    /// <param name="handle">由 sc_create_stream 返回的管理器句柄</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void sc_stop_stream(IntPtr handle);
    
    /// <summary>
    /// 检查指定进程是否为前台活动应用
    /// </summary>
    /// <param name="pid">进程 ID</param>
    /// <returns>是否为前台活动应用</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool is_process_frontmost(int pid);
    
    /// <summary>
    /// 发送按键
    /// </summary>
    /// <param name="keyCode">macOS Virtual Key Code (0-127)</param>
    /// <param name="shiftPressed">是否按下 Shift 键</param>
    /// <param name="ctrlPressed">是否按下 Ctrl 键</param>
    /// <param name="altPressed">是否按下 Alt/Option 键</param>
    /// <param name="cmdPressed">是否按下 Command 键</param>
    /// <param name="targetPid">目标进程 PID（0 = 全局发送，> 0 = 直接发送到进程）</param>
    /// <returns>是否成功发送</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool kb_send_key(
        ushort keyCode,
        [MarshalAs(UnmanagedType.I1)] bool shiftPressed,
        [MarshalAs(UnmanagedType.I1)] bool ctrlPressed,
        [MarshalAs(UnmanagedType.I1)] bool altPressed,
        [MarshalAs(UnmanagedType.I1)] bool cmdPressed,
        int targetPid
    );
}
