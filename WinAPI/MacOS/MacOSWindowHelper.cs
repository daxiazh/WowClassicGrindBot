using SixLabors.ImageSharp;
using System;
using System.Runtime.InteropServices;

namespace WinAPI;

public sealed class MacOSWindowHelper : INativeWindow
{
    private const string CoreGraphicsFramework = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
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

    public void GetWindowRect(nint hWnd, out Rectangle rect)
    {
        if (hWnd == nint.Zero)
        {
            nint display = CGMainDisplayID();
            CGRect bounds = CGDisplayBounds(display);
            rect = new Rectangle(
                (int)bounds.x,
                (int)bounds.y,
                (int)bounds.width,
                (int)bounds.height
            );
        }
        else
        {
            throw new NotImplementedException("Getting window rect for specific window is not yet implemented on macOS");
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

    public nint GetForegroundWindow()
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

    public bool SetForegroundWindow(nint hWnd)
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

    public bool PostMessage(nint hWnd, uint msg, int wParam, int lParam)
    {
        throw new NotSupportedException("PostMessage is not supported on macOS (Windows-only API)");
    }

    public nint MonitorFromWindow(nint hWnd, uint dwFlags)
    {
        return CGMainDisplayID();
    }
}
