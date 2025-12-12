using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace VizAura.MacOS;

public sealed class MacOsProcessHelper
{
    #region CoreGraphics P/Invoke
    
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);
    
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern int CFArrayGetCount(IntPtr array);
    
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, int index);
    
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);
    
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out int value);
    
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
    
    // ReSharper disable once InconsistentNaming
    private const uint kCGWindowListOptionAll = 0;
    // ReSharper disable once InconsistentNaming
    private const int kCFNumberSInt32Type = 3;
    
    #endregion
    

    public string GetExecutablePath(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(fileName))
            {
                var path = Path.GetDirectoryName(fileName);
                return string.IsNullOrEmpty(path) ? string.Empty : path;
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    public Version GetVersion(Process process, string executablePath)
    {
        try
        {
            string? appBundlePath = FindAppBundle(executablePath);
            if (appBundlePath == null)
                return new Version();

            string plistPath = Path.Combine(appBundlePath, "Contents", "Info.plist");
            if (!File.Exists(plistPath))
                return new Version();

            return ParseVersionFromPlist(plistPath);
        }
        catch
        {
            return new Version();
        }
    }

    private static string? FindAppBundle(string executablePath)
    {
        DirectoryInfo? dir = new DirectoryInfo(executablePath).Parent;
        while (dir != null)
        {
            if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static Version ParseVersionFromPlist(string plistPath)
    {
        var doc = XDocument.Load(plistPath);
        var dict = doc.Root?.Element("dict");
        if (dict == null)
            return new Version();

        var keys = dict.Elements("key").ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Value == "CFBundleVersion")
            {
                var versionString = keys[i].ElementsAfterSelf("string").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(versionString) && 
                    Version.TryParse(versionString, out Version? v))
                {
                    return v;
                }
            }
        }

        return new Version();
    }
    
    /// <summary>
    /// 获取进程的主窗口 ID (CGWindowID)
    /// 用于 ScreenCaptureKit 捕获指定窗口
    /// </summary>
    /// <param name="process">目标进程</param>
    /// <returns>窗口 ID, 失败返回 0</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    public uint GetWindowId(Process process)
    {
        IntPtr windowList = IntPtr.Zero;
        
        try
        {
            // 获取所有窗口信息
            windowList = CGWindowListCopyWindowInfo(kCGWindowListOptionAll, 0);
            if (windowList == IntPtr.Zero)
                return 0;
            
            int count = CFArrayGetCount(windowList);
            uint largestWindowId = 0;
            int largestWindowArea = 0;
            
            // 遍历查找匹配进程 ID 的窗口,选择面积最大的
            for (int i = 0; i < count; i++)
            {
                IntPtr windowInfo = CFArrayGetValueAtIndex(windowList, i);
                if (windowInfo == IntPtr.Zero)
                    continue;
                
                // 获取窗口的 PID
                IntPtr pidKey = CreateCFString("kCGWindowOwnerPID");
                IntPtr pidValue = CFDictionaryGetValue(windowInfo, pidKey);
                CFRelease(pidKey);
                
                if (pidValue != IntPtr.Zero)
                {
                    if (CFNumberGetValue(pidValue, kCFNumberSInt32Type, out int windowPid))
                    {
                        if (windowPid == process.Id)
                        {
                            // 获取窗口边界
                            IntPtr boundsKey = CreateCFString("kCGWindowBounds");
                            IntPtr boundsValue = CFDictionaryGetValue(windowInfo, boundsKey);
                            CFRelease(boundsKey);
                            
                            if (boundsValue != IntPtr.Zero)
                            {
                                // 获取宽度和高度
                                IntPtr widthKey = CreateCFString("Width");
                                IntPtr heightKey = CreateCFString("Height");
                                IntPtr widthValue = CFDictionaryGetValue(boundsValue, widthKey);
                                IntPtr heightValue = CFDictionaryGetValue(boundsValue, heightKey);
                                CFRelease(widthKey);
                                CFRelease(heightKey);
                                
                                if (widthValue != IntPtr.Zero && heightValue != IntPtr.Zero)
                                {
                                    if (CFNumberGetValue(widthValue, kCFNumberSInt32Type, out int width) &&
                                        CFNumberGetValue(heightValue, kCFNumberSInt32Type, out int height))
                                    {
                                        int area = width * height;
                                        
                                        // 只考虑合理尺寸的窗口(至少 640x480)
                                        if (width >= 640 && height >= 480 && area > largestWindowArea)
                                        {
                                            // 获取窗口 ID
                                            IntPtr windowIdKey = CreateCFString("kCGWindowNumber");
                                            IntPtr windowIdValue = CFDictionaryGetValue(windowInfo, windowIdKey);
                                            CFRelease(windowIdKey);
                                            
                                            if (windowIdValue != IntPtr.Zero &&
                                                CFNumberGetValue(windowIdValue, kCFNumberSInt32Type, out int windowId))
                                            {
                                                largestWindowArea = area;
                                                largestWindowId = (uint)windowId;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return largestWindowId;
        }
        finally
        {
            if (windowList != IntPtr.Zero)
                CFRelease(windowList);
        }
    }
    
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CharSet = CharSet.Unicode)]
    private static extern IntPtr CFStringCreateWithCharacters(IntPtr alloc, string chars, int length);
    
    private static IntPtr CreateCFString(string str)
    {
        return CFStringCreateWithCharacters(IntPtr.Zero, str, str.Length);
    }
}
