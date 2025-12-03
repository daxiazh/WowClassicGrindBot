using SixLabors.ImageSharp;
using System;
using System.Runtime.InteropServices;

namespace WinAPI;

public static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public nint hCursor;
        public Point ptScreenPos;
    }

    private static readonly INativeWindow _window = CreateWindowHelper();
    private static readonly INativeCursor _cursor = CreateCursorHelper();

    private static INativeWindow CreateWindowHelper()
    {
#if WINDOWS
        return new WindowsWindowHelper();
#elif MACOS
        return new MacOSWindowHelper();
#else
        throw new PlatformNotSupportedException("Only Windows and macOS are supported");
#endif
    }

    private static INativeCursor CreateCursorHelper()
    {
#if WINDOWS
        return new WindowsCursorHelper();
#elif MACOS
        return new MacOSCursorHelper();
#else
        throw new PlatformNotSupportedException("Only Windows and macOS are supported");
#endif
    }

    public const int CURSOR_SHOWING = 0x0001;
    public const int DI_NORMAL = 0x0003;

    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_LBUTTONDOWN = 0x201;
    public const uint WM_LBUTTONUP = 0x202;
    public const uint WM_RBUTTONDOWN = 0x204;
    public const uint WM_RBUTTONUP = 0x205;

    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;

    public const int MONITOR_DEFAULT_TO_NULL = 0;
    public const int MONITOR_DEFAULT_TO_PRIMARY = 1;
    public const int MONITOR_DEFAULT_TO_NEAREST = 2;

    public static int MakeLParam(int x, int y) => (y << 16) | (x & 0xFFFF);

    public static nint GetForegroundWindow() => _window.GetForegroundWindow();

    public static bool SetForegroundWindow(nint hWnd) => _window.SetForegroundWindow(hWnd);

    public static bool PostMessage(nint hWnd, uint msg, int wParam, int lParam) => _window.PostMessage(hWnd, msg, wParam, lParam);

    public static bool SetCursorPos(int x, int y) => _cursor.SetCursorPos(x, y);

    public static bool GetCursorPos(out Point p) => _cursor.GetCursorPos(out p);

    public static bool GetCursorInfo(ref CURSORINFO pci) => _cursor.GetCursorInfo(ref pci);

    public static bool ScreenToClient(nint hWnd, ref Point lpPoint) => _window.ScreenToClient(hWnd, ref lpPoint);

    public static void GetPosition(nint hWnd, ref Point point) => _window.GetPosition(hWnd, ref point);

    public static void GetWindowRect(nint hWnd, out Rectangle rect) => _window.GetWindowRect(hWnd, out rect);

    public static Size GetCursorSize() => _cursor.GetCursorSize();

    public static nint MonitorFromWindow(nint hWnd, uint dwFlags) => _window.MonitorFromWindow(hWnd, dwFlags);

    public static float DPI2PPI(int dpi) => dpi / 96f;

    public static bool DrawIconEx(nint hdc, int xLeft, int yTop, nint hIcon, int cxWidth, int cyHeight, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags)
        => _cursor.DrawIconEx(hdc, xLeft, yTop, hIcon, cxWidth, cyHeight, istepIfAniCur, hbrFlickerFreeDraw, diFlags);

    public static bool DrawIcon(nint hDC, int x, int y, nint hIcon)
        => _cursor.DrawIcon(hDC, x, y, hIcon);

    public static int GetDpi()
    {
#if WINDOWS
        return WindowsCursorHelper.GetDpiInternal();
#else
        return 96;
#endif
    }
}