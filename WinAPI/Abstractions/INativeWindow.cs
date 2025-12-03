using SixLabors.ImageSharp;

namespace WinAPI;

public interface INativeWindow
{
    void GetWindowRect(nint hWnd, out Rectangle rect);
    
    void GetPosition(nint hWnd, ref Point point);
    
    bool ScreenToClient(nint hWnd, ref Point point);
    
    nint GetForegroundWindow();
    
    bool SetForegroundWindow(nint hWnd);
    
    bool PostMessage(nint hWnd, uint msg, int wParam, int lParam);
    
    nint MonitorFromWindow(nint hWnd, uint dwFlags);
}
