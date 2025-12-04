using SixLabors.ImageSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Game;

public sealed class InputMacNative : IInput
{
    private const string CoreGraphicsFramework = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private readonly int maxDelay;
    private readonly WowProcess process;
    private readonly CancellationToken token;

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double x;
        public double y;
    }

    private enum CGEventType : uint
    {
        LeftMouseDown = 1,
        LeftMouseUp = 2,
        RightMouseDown = 3,
        RightMouseUp = 4,
        MouseMoved = 5,
        LeftMouseDragged = 6,
        RightMouseDragged = 7,
        KeyDown = 10,
        KeyUp = 11,
        FlagsChanged = 12,
    }

    private enum CGMouseButton : uint
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }

    private enum CGEventTapLocation : uint
    {
        HID = 0,
        Session = 1,
        AnnotatedSession = 2,
    }

    [DllImport(CoreGraphicsFramework)]
    private static extern nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, bool keyDown);

    [DllImport(CoreGraphicsFramework)]
    private static extern nint CGEventCreateMouseEvent(nint source, CGEventType mouseType, CGPoint mouseCursorPosition, CGMouseButton mouseButton);

    [DllImport(CoreGraphicsFramework)]
    private static extern void CGEventPost(CGEventTapLocation tap, nint eventRef);

    [DllImport(CoreGraphicsFramework)]
    private static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    [DllImport(CoreGraphicsFramework)]
    private static extern nint CGEventCreate(nint source);

    [DllImport(CoreGraphicsFramework)]
    private static extern CGPoint CGEventGetLocation(nint eventRef);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(nint cf);

    public InputMacNative(WowProcess process, CancellationTokenSource cts, int maxDelay)
    {
        this.process = process;
        this.token = cts.Token;
        this.maxDelay = maxDelay;
    }

    private int DelayTime(int milliseconds)
    {
        return milliseconds + Random.Shared.Next(maxDelay);
    }

    public void KeyDown(int key)
    {
        ushort keyCode = MapVirtualKeyToMac(key);
        nint keyEvent = CGEventCreateKeyboardEvent(nint.Zero, keyCode, true);
        CGEventPost(CGEventTapLocation.HID, keyEvent);
        CFRelease(keyEvent);
    }

    public void KeyUp(int key)
    {
        ushort keyCode = MapVirtualKeyToMac(key);
        nint keyEvent = CGEventCreateKeyboardEvent(nint.Zero, keyCode, false);
        CGEventPost(CGEventTapLocation.HID, keyEvent);
        CFRelease(keyEvent);
    }

    public int PressRandom(int key, int milliseconds)
    {
        return PressRandom(key, milliseconds, token);
    }

    public int PressRandom(int key, int milliseconds, CancellationToken token)
    {
        KeyDown(key);
        
        int delay = DelayTime(milliseconds);
        token.WaitHandle.WaitOne(delay);
        
        KeyUp(key);
        
        return delay;
    }

    public void PressFixed(int key, int milliseconds, CancellationToken token)
    {
        KeyDown(key);
        token.WaitHandle.WaitOne(milliseconds);
        KeyUp(key);
    }

    public void LeftClick(Point p)
    {
        SetCursorPos(p);
        
        CGPoint cgPoint = new() { x = p.X, y = p.Y };
        
        nint mouseDown = CGEventCreateMouseEvent(nint.Zero, CGEventType.LeftMouseDown, cgPoint, CGMouseButton.Left);
        CGEventPost(CGEventTapLocation.HID, mouseDown);
        CFRelease(mouseDown);
        
        token.WaitHandle.WaitOne(DelayTime(maxDelay));
        
        nint mouseUp = CGEventCreateMouseEvent(nint.Zero, CGEventType.LeftMouseUp, cgPoint, CGMouseButton.Left);
        CGEventPost(CGEventTapLocation.HID, mouseUp);
        CFRelease(mouseUp);
    }

    public void RightClick(Point p)
    {
        SetCursorPos(p);
        
        CGPoint cgPoint = new() { x = p.X, y = p.Y };
        
        nint mouseDown = CGEventCreateMouseEvent(nint.Zero, CGEventType.RightMouseDown, cgPoint, CGMouseButton.Right);
        CGEventPost(CGEventTapLocation.HID, mouseDown);
        CFRelease(mouseDown);
        
        token.WaitHandle.WaitOne(DelayTime(maxDelay));
        
        nint mouseUp = CGEventCreateMouseEvent(nint.Zero, CGEventType.RightMouseUp, cgPoint, CGMouseButton.Right);
        CGEventPost(CGEventTapLocation.HID, mouseUp);
        CFRelease(mouseUp);
    }

    public void SetCursorPos(Point p)
    {
        CGPoint point = new() { x = p.X, y = p.Y };
        CGWarpMouseCursorPosition(point);
    }

    public void SendText(string text)
    {
        throw new NotImplementedException("SendText not yet implemented for macOS");
    }

    public void SetClipboard(string text)
    {
        throw new NotImplementedException("SetClipboard not yet implemented for macOS");
    }

    public void PasteFromClipboard()
    {
        throw new NotImplementedException("PasteFromClipboard not yet implemented for macOS");
    }

    private static ushort MapVirtualKeyToMac(int windowsKey)
    {
        return windowsKey switch
        {
            0x57 => 13,
            0x41 => 0,
            0x53 => 1,
            0x44 => 2,
            0x51 => 12,
            0x45 => 14,
            0x52 => 15,
            0x31 => 18,
            0x32 => 19,
            0x33 => 20,
            0x34 => 21,
            0x35 => 23,
            0x36 => 22,
            0x37 => 26,
            0x38 => 28,
            0x39 => 25,
            0x30 => 29,
            0x20 => 49,
            0x0D => 36,
            0x1B => 53,
            0x08 => 51,
            0x09 => 48,
            _ => (ushort)windowsKey
        };
    }
}
