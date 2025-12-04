using System;
using System.Diagnostics;

namespace WinAPI;

public interface INativeProcess
{
    string GetExecutablePath(Process process);
    Version GetVersion(Process process, string executablePath);
}
