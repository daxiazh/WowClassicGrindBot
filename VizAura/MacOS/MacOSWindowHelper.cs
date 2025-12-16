using System;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;

namespace VizAura.MacOS;

public sealed class MacOSWindowHelper
{
    private const string CoreGraphicsFramework = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double x;
        public double y;
        public double width;
        public double height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double x;
        public double y;
    }

    [DllImport(CoreGraphicsFramework)]
    private static extern nint CGMainDisplayID();

    [DllImport(CoreGraphicsFramework)]
    private static extern CGRect CGDisplayBounds(nint display);

    [DllImport(AppKitFramework)]
    private static extern nint NSApp();

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_IntPtr(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_Bool(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void(nint receiver, nint selector);

    [DllImport(AppKitFramework)]
    private static extern nint sel_registerName(string name);

    [DllImport(AppKitFramework)]
    private static extern nint objc_getClass(string name);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend_stret")]
    private static extern CGRect objc_msgSend_CGRect(nint receiver, nint selector);

    [DllImport(CoreGraphicsFramework)]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreFoundationFramework)]
    private static extern int CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundationFramework)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, int index);

    [DllImport(CoreFoundationFramework)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);

    [DllImport(CoreFoundationFramework)]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out uint value);

    [DllImport(CoreFoundationFramework)]
    private static extern bool CFDictionaryGetValueIfPresent(IntPtr dict, IntPtr key, out IntPtr value);

    [DllImport(CoreFoundationFramework)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationFramework, CharSet = CharSet.Unicode)]
    private static extern IntPtr CFStringCreateWithCharacters(IntPtr alloc, string chars, int length);

    private const uint kCGWindowListOptionAll = 0;
    private const int kCFNumberSInt32Type = 3;

    private static IntPtr CreateCFString(string str)
    {
        return CFStringCreateWithCharacters(IntPtr.Zero, str, str.Length);
    }

    /// <summary>
    /// 获取窗口客户区矩形
    /// 与 Windows GetClientRect 行为一致,返回窗口内容区域(不含标题栏和边框)
    /// </summary>
    /// <param name="hWnd">窗口ID(CGWindowID),必须是有效的窗口ID</param>
    /// <param name="rect">输出窗口客户区矩形,使用屏幕坐标(原点在左上角)</param>
    /// <exception cref="ArgumentException">当 hWnd 为 nint.Zero 时抛出</exception>
    /// <exception cref="InvalidOperationException">当无法获取窗口矩形时抛出</exception>
    public static void GetWindowRect(nint hWnd, out Rectangle rect)
    {
        if (hWnd == nint.Zero)
        {
            throw new ArgumentException("Window ID cannot be zero", nameof(hWnd));
        }

        uint windowId = (uint)hWnd;
        IntPtr windowList = IntPtr.Zero;
        
        try
        {
            // 获取所有窗口信息
            windowList = CGWindowListCopyWindowInfo(kCGWindowListOptionAll, 0);
            if (windowList == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get window list");
            }

            int count = CFArrayGetCount(windowList);
            
            // 查找匹配的窗口ID
            for (int i = 0; i < count; i++)
            {
                IntPtr windowInfo = CFArrayGetValueAtIndex(windowList, i);
                if (windowInfo == IntPtr.Zero)
                    continue;

                // 获取窗口ID
                IntPtr windowNumberKey = CreateCFString("kCGWindowNumber");
                IntPtr windowNumberValue = CFDictionaryGetValue(windowInfo, windowNumberKey);
                CFRelease(windowNumberKey);

                if (windowNumberValue != IntPtr.Zero)
                {
                    if (CFNumberGetValue(windowNumberValue, kCFNumberSInt32Type, out uint foundWindowId))
                    {
                        if (foundWindowId == windowId)
                        {
                            // 找到匹配的窗口,获取边界
                            IntPtr boundsKey = CreateCFString("kCGWindowBounds");
                            IntPtr boundsValue = CFDictionaryGetValue(windowInfo, boundsKey);
                            CFRelease(boundsKey);
                            
                            if (boundsValue != IntPtr.Zero)
                            {
                                // 从字典中提取 X, Y, Width, Height
                                IntPtr xKey = CreateCFString("X");
                                IntPtr yKey = CreateCFString("Y");
                                IntPtr widthKey = CreateCFString("Width");
                                IntPtr heightKey = CreateCFString("Height");

                                IntPtr xValue = CFDictionaryGetValue(boundsValue, xKey);
                                IntPtr yValue = CFDictionaryGetValue(boundsValue, yKey);
                                IntPtr widthValue = CFDictionaryGetValue(boundsValue, widthKey);
                                IntPtr heightValue = CFDictionaryGetValue(boundsValue, heightKey);

                                CFRelease(xKey);
                                CFRelease(yKey);
                                CFRelease(widthKey);
                                CFRelease(heightKey);

                                if (xValue != IntPtr.Zero && yValue != IntPtr.Zero && 
                                    widthValue != IntPtr.Zero && heightValue != IntPtr.Zero)
                                {
                                    CFNumberGetValue(xValue, kCFNumberSInt32Type, out uint x);
                                    CFNumberGetValue(yValue, kCFNumberSInt32Type, out uint y);
                                    CFNumberGetValue(widthValue, kCFNumberSInt32Type, out uint width);
                                    CFNumberGetValue(heightValue, kCFNumberSInt32Type, out uint height);

                                    rect = new Rectangle((int)x, (int)y, (int)(width), (int)(height));
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException($"Window with ID {windowId} not found");
        }
        finally
        {
            if (windowList != IntPtr.Zero)
            {
                CFRelease(windowList);
            }
        }
    }

    public void GetPosition(nint hWnd, ref Point point)
    {
        throw new NotImplementedException("GetPosition is not yet implemented on macOS. Need to implement NSWindow frame conversion.");
    }

    public bool ScreenToClient(nint hWnd, ref Point point)
    {
        throw new NotImplementedException("ScreenToClient is not yet implemented on macOS. macOS uses different coordinate system (origin at bottom-left).");
    }

    public static nint GetForegroundWindow()
    {
        try
        {
            nint nsApp = objc_getClass("NSApplication");
            if (nsApp == nint.Zero)
                return nint.Zero;

            nint sharedApp = objc_msgSend_IntPtr(nsApp, sel_registerName("sharedApplication"));
            if (sharedApp == nint.Zero)
                return nint.Zero;

            nint keyWindow = objc_msgSend_IntPtr(sharedApp, sel_registerName("keyWindow"));
            return keyWindow;
        }
        catch
        {
            return nint.Zero;
        }
    }

    public static bool SetForegroundWindow(nint hWnd)
    {
        try
        {
            nint nsApp = objc_getClass("NSApplication");
            if (nsApp == nint.Zero)
                return false;

            nint sharedApp = objc_msgSend_IntPtr(nsApp, sel_registerName("sharedApplication"));
            if (sharedApp == nint.Zero)
                return false;

            objc_msgSend_Void(sharedApp, sel_registerName("activateIgnoringOtherApps:"));
            
            if (hWnd != nint.Zero)
            {
                objc_msgSend_Void(hWnd, sel_registerName("makeKeyAndOrderFront:"));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static nint MonitorFromWindow(nint hWnd, uint dwFlags)
    {
        return CGMainDisplayID();
    }

    /// <summary>
    /// 获取窗口边界矩形 (便捷方法)
    /// </summary>
    /// <param name="windowId">窗口 ID</param>
    /// <returns>窗口矩形</returns>
    public static Rectangle GetWindowBounds(int windowId)
    {
        GetWindowRect((nint)windowId, out Rectangle rect);
        return rect;
    }
}
