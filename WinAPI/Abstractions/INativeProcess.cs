using System.Diagnostics;

namespace WinAPI;

public interface INativeProcess
{
    string GetExecutablePath(Process process);
}
