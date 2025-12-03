using System;
using System.Diagnostics;
using System.IO;

namespace WinAPI;

public sealed class MacOSProcessHelper : INativeProcess
{
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
        }

        return string.Empty;
    }
}
