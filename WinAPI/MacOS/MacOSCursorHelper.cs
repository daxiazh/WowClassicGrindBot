using SixLabors.ImageSharp;
using System;
using System.Runtime.InteropServices;

namespace WinAPI;

public sealed class MacOSCursorHelper : INativeCursor
{
    private const string CoreGraphicsFramework = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double x;
        public double y;
    }

    [DllImport(CoreGraphicsFramework)]
    private static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    [DllImport(CoreGraphicsFramework)]
    private static extern nint CGEventCreate(nint source);

    [DllImport(CoreGraphicsFramework)]
    private static extern CGPoint CGEventGetLocation(nint eventRef);

    [DllImport(CoreFoundationFramework)]
    private static extern void CFRelease(nint cf);

    [DllImport(CoreGraphicsFramework)]
    private static extern void CGAssociateMouseAndMouseCursorPosition(bool connected);

    public bool SetCursorPos(int x, int y)
    {
        CGPoint point = new() { x = x, y = y };
        int result = CGWarpMouseCursorPosition(point);
        return result == 0;
    }

    public bool GetCursorPos(out Point p)
    {
        nint mouseEvent = CGEventCreate(nint.Zero);
        if (mouseEvent != nint.Zero)
        {
            CGPoint location = CGEventGetLocation(mouseEvent);
            CFRelease(mouseEvent);
            p = new Point((int)location.x, (int)location.y);
            return true;
        }
        
        p = new Point(0, 0);
        return false;
    }

    public bool GetCursorInfo(ref NativeMethods.CURSORINFO pci)
    {
        pci.flags = NativeMethods.CURSOR_SHOWING;
        
        if (GetCursorPos(out Point pos))
        {
            pci.ptScreenPos = pos;
            return true;
        }
        
        pci.ptScreenPos = new Point(0, 0);
        return false;
    }

    public Size GetCursorSize()
    {
        return new Size(32, 32);
    }

    public bool DrawIcon(nint hDC, int x, int y, nint hIcon)
    {
        throw new NotImplementedException("DrawIcon is not yet implemented on macOS");
    }

    public bool DrawIconEx(nint hdc, int xLeft, int yTop, nint hIcon, int cxWidth, int cyHeight, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags)
    {
        throw new NotImplementedException("DrawIconEx is not yet implemented on macOS");
    }
}
