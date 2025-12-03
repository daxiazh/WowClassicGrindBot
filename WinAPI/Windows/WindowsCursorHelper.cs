using SixLabors.ImageSharp;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace WinAPI;

public sealed partial class WindowsCursorHelper : INativeCursor
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point p);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorInfo(ref NativeMethods.CURSORINFO pci);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndexn);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDeviceCaps(nint hDC, int nIndex);

    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;
    private const int LOGPIXELSX = 88;

    bool INativeCursor.SetCursorPos(int x, int y)
    {
        return SetCursorPos(x, y);
    }

    bool INativeCursor.GetCursorPos(out Point p)
    {
        return GetCursorPos(out p);
    }

    bool INativeCursor.GetCursorInfo(ref NativeMethods.CURSORINFO pci)
    {
        return GetCursorInfo(ref pci);
    }

    public Size GetCursorSize()
    {
        int dpi = GetDpi();
        SizeF size = new(GetSystemMetrics(SM_CXCURSOR), GetSystemMetrics(SM_CYCURSOR));
        size *= DPI2PPI(dpi);
        return (Size)size;
    }

    private static int GetDpi()
    {
        using System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(nint.Zero);
        return GetDeviceCaps(g.GetHdc(), LOGPIXELSX);
    }

    private static float DPI2PPI(int dpi)
    {
        return dpi / 96f;
    }

    public static int GetDpiInternal()
    {
        using System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(nint.Zero);
        return GetDeviceCaps(g.GetHdc(), LOGPIXELSX);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DrawIconExNative(nint hdc, int xLeft, int yTop, nint hIcon, int cxWidth, int cyHeight, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DrawIconNative(nint hDC, int x, int y, nint hIcon);

    bool INativeCursor.DrawIcon(nint hDC, int x, int y, nint hIcon)
    {
        return DrawIconNative(hDC, x, y, hIcon);
    }

    bool INativeCursor.DrawIconEx(nint hdc, int xLeft, int yTop, nint hIcon, int cxWidth, int cyHeight, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags)
    {
        return DrawIconExNative(hdc, xLeft, yTop, hIcon, cxWidth, cyHeight, istepIfAniCur, hbrFlickerFreeDraw, diFlags);
    }
}
