using System;
using System.Diagnostics;

namespace WinAPI;

public static class ExecutableVersion
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

    public static Version Get(Process process, string executablePath)
    {
        return _helper.GetVersion(process, executablePath);
    }
}
