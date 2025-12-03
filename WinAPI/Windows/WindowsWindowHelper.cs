using SixLabors.ImageSharp;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]

namespace WinAPI;

public sealed partial class WindowsWindowHelper : INativeWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct RECT
    {
        public readonly int left, top, right, bottom;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint hWnd, ref Point lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ScreenToClient(nint hWnd, ref Point lpPoint);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageA")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hWnd, uint Msg, int wParam, int lParam);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hWnd, uint dwFlags);

    public void GetWindowRect(nint hWnd, out Rectangle rect)
    {
        GetClientRect(hWnd, out RECT nRect);
        rect = Rectangle.FromLTRB(nRect.left, nRect.top, nRect.right, nRect.bottom);

        Point topLeft = new();
        ClientToScreen(hWnd, ref topLeft);
        if (IsWindowedMode(topLeft))
        {
            rect.X = topLeft.X;
            rect.Y = topLeft.Y;
        }
    }

    public void GetPosition(nint hWnd, ref Point point)
    {
        ClientToScreen(hWnd, ref point);
    }

    bool INativeWindow.ScreenToClient(nint hWnd, ref Point point)
    {
        return ScreenToClient(hWnd, ref point);
    }

    nint INativeWindow.GetForegroundWindow()
    {
        return GetForegroundWindow();
    }

    bool INativeWindow.SetForegroundWindow(nint hWnd)
    {
        return SetForegroundWindow(hWnd);
    }

    bool INativeWindow.PostMessage(nint hWnd, uint msg, int wParam, int lParam)
    {
        return PostMessage(hWnd, msg, wParam, lParam);
    }

    nint INativeWindow.MonitorFromWindow(nint hWnd, uint dwFlags)
    {
        return MonitorFromWindow(hWnd, dwFlags);
    }

    private static bool IsWindowedMode(Point point)
    {
        return point.X != 0 || point.Y != 0;
    }
}
