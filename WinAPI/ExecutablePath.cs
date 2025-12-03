using System;
using System.Diagnostics;

namespace WinAPI;

public static class ExecutablePath
{
    private static readonly INativeProcess _helper = CreateHelper();

    private static INativeProcess CreateHelper()
    {
#if WINDOWS
        return new WindowsProcessHelper();
#elif MACOS
        return new MacOSProcessHelper();
#else
        throw new PlatformNotSupportedException("Only Windows and macOS are supported");
#endif
    }

    public static string Get(Process process)
    {
        return _helper.GetExecutablePath(process);
    }
}
