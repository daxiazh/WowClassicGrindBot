using SixLabors.ImageSharp;

namespace WinAPI;

public interface INativeCursor
{
    bool SetCursorPos(int x, int y);
    
    bool GetCursorPos(out Point p);
    
    bool GetCursorInfo(ref NativeMethods.CURSORINFO pci);
    
    Size GetCursorSize();
    
    bool DrawIcon(nint hDC, int x, int y, nint hIcon);
    
    bool DrawIconEx(nint hdc, int xLeft, int yTop, nint hIcon, int cxWidth, int cyHeight, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags);
}
