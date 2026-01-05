using System.Collections.Generic;

namespace VizAura.MacOS;

/// <summary>
/// Hekili 快捷键到 macOS Virtual Key Code 的映射器
/// 参考: https://eastmanreference.com/complete-list-of-applescript-key-codes
/// </summary>
public static class KeybindMapper
{
    /// <summary>
    /// 字符到 macOS Virtual Key Code 的映射
    /// </summary>
    private static readonly Dictionary<char, ushort> CharToKeyCode = new()
    {
        // 字母键 (A-Z)
        ['A'] = 0x00, ['B'] = 0x0B, ['C'] = 0x08, ['D'] = 0x02,
        ['E'] = 0x0E, ['F'] = 0x03, ['G'] = 0x05, ['H'] = 0x04,
        ['I'] = 0x22, ['J'] = 0x26, ['K'] = 0x28, ['L'] = 0x25,
        ['M'] = 0x2E, ['N'] = 0x2D, ['O'] = 0x1F, ['P'] = 0x23,
        ['Q'] = 0x0C, ['R'] = 0x0F, ['S'] = 0x01, ['T'] = 0x11,
        ['U'] = 0x20, ['V'] = 0x09, ['W'] = 0x0D, ['X'] = 0x07,
        ['Y'] = 0x10, ['Z'] = 0x06,
        
        // 数字键 (0-9 主键盘区)
        ['0'] = 0x1D, ['1'] = 0x12, ['2'] = 0x13, ['3'] = 0x14,
        ['4'] = 0x15, ['5'] = 0x17, ['6'] = 0x16, ['7'] = 0x1A,
        ['8'] = 0x1C, ['9'] = 0x19,
        
        // 特殊字符
        ['-'] = 0x1B,  // Minus
        ['='] = 0x18,  // Equal
        ['['] = 0x21,  // Left Bracket
        [']'] = 0x1E,  // Right Bracket
        ['\\'] = 0x2A, // Backslash
        [';'] = 0x29,  // Semicolon
        ['\''] = 0x27, // Quote
        [','] = 0x2B,  // Comma
        ['.'] = 0x2F,  // Period
        ['/'] = 0x2C,  // Slash
        ['`'] = 0x32,  // Grave/Tilde
    };
    
    /// <summary>
    /// F 键到 macOS Virtual Key Code 的映射
    /// </summary>
    private static readonly Dictionary<string, ushort> FKeyToKeyCode = new()
    {
        ["F1"] = 0x7A, ["F2"] = 0x78, ["F3"] = 0x63, ["F4"] = 0x76,
        ["F5"] = 0x60, ["F6"] = 0x61, ["F7"] = 0x62, ["F8"] = 0x64,
        ["F9"] = 0x65, ["F10"] = 0x6D, ["F11"] = 0x67, ["F12"] = 0x6F,
        ["F13"] = 0x69, ["F14"] = 0x6B, ["F15"] = 0x71,
    };
    
    /// <summary>
    /// 特殊键名到 macOS Virtual Key Code 的映射
    /// </summary>
    private static readonly Dictionary<string, ushort> SpecialKeyToKeyCode = new()
    {
        ["SPACE"] = 0x31,
        ["RETURN"] = 0x24,
        ["ENTER"] = 0x4C,  // Numpad Enter
        ["TAB"] = 0x30,
        ["DELETE"] = 0x33, // Backspace
        ["ESCAPE"] = 0x35,
        ["ESC"] = 0x35,
        ["UP"] = 0x7E,
        ["DOWN"] = 0x7D,
        ["LEFT"] = 0x7B,
        ["RIGHT"] = 0x7C,
        ["HOME"] = 0x73,
        ["END"] = 0x77,
        ["PAGEUP"] = 0x74,
        ["PAGEDOWN"] = 0x79,
        
        // Numpad (Hekili 简化为 N + 数字)
        ["N0"] = 0x52, ["N1"] = 0x53, ["N2"] = 0x54, ["N3"] = 0x55,
        ["N4"] = 0x56, ["N5"] = 0x57, ["N6"] = 0x58, ["N7"] = 0x59,
        ["N8"] = 0x5B, ["N9"] = 0x5C,
    };
    
    /// <summary>
    /// 解析 Hekili 快捷键字符串
    /// </summary>
    /// <param name="keybind">Hekili 快捷键字符串 (如 "S3", "CF", "1", "F1")</param>
    /// <returns>解析结果 (keyCode, shift, ctrl, alt)，解析失败返回 null</returns>
    public static (ushort keyCode, bool shift, bool ctrl, bool alt)? Parse(string keybind)
    {
        if (string.IsNullOrEmpty(keybind))
            return null;
        
        keybind = keybind.ToUpper().Trim();
        
        bool shift = false;
        bool ctrl = false;
        bool alt = false;
        string keyPart = keybind;
        
        // 解析修饰键前缀
        // Hekili 简化规则: S=Shift, C=Ctrl, A=Alt
        if (keybind.Length >= 2)
        {
            char firstChar = keybind[0];
            
            if (firstChar == 'S' && char.IsDigit(keybind[1]))
            {
                // "S3" → Shift+3
                shift = true;
                keyPart = keybind.Substring(1);
            }
            else if (firstChar == 'C' && keybind.Length >= 2)
            {
                // "CF" → Ctrl+F
                ctrl = true;
                keyPart = keybind.Substring(1);
            }
            else if (firstChar == 'A' && keybind.Length >= 2)
            {
                // "AQ" → Alt+Q
                alt = true;
                keyPart = keybind.Substring(1);
            }
        }
        
        // 解析按键部分
        ushort keyCode;
        
        // 1. 检查 F 键 (F1-F15)
        if (FKeyToKeyCode.TryGetValue(keyPart, out keyCode))
            return (keyCode, shift, ctrl, alt);
        
        // 2. 检查特殊键
        if (SpecialKeyToKeyCode.TryGetValue(keyPart, out keyCode))
            return (keyCode, shift, ctrl, alt);
        
        // 3. 检查单字符键
        if (keyPart.Length == 1 && CharToKeyCode.TryGetValue(keyPart[0], out keyCode))
            return (keyCode, shift, ctrl, alt);
        
        // 解析失败
        return null;
    }
    
    /// <summary>
    /// 发送 Hekili 快捷键
    /// </summary>
    /// <param name="keybind">快捷键字符串 (如 "S3", "CF", "1")</param>
    /// <param name="targetPid">目标进程 PID（0 = 全局发送，> 0 = 直接发送到进程）</param>
    /// <returns>是否成功发送</returns>
    public static bool SendKeybind(string keybind, int targetPid = 0)
    {
        var parsed = Parse(keybind);
        if (!parsed.HasValue)
        {
            // 解析失败
            return false;
        }

        var (keyCode, shift, ctrl, alt) = parsed.Value;

        // 调用 Swift 底层函数
        return WinAPI.ScreenCaptureKitInterop.kb_send_key(
            keyCode,
            shift,
            ctrl,
            alt,
            cmdPressed: false,  // WoW 不使用 Command 键
            targetPid
        );
    }
}
