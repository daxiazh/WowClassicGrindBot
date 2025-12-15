using System;
using System.Diagnostics;

namespace VizAura.Models;

/// <summary>
/// WoW 进程信息
/// 用于在状态之间传递进程数据
/// </summary>
public sealed class WowProcessInfo
{
    /// <summary>
    /// WoW 进程对象
    /// </summary>
    public Process Process { get; init; } = null!;

    /// <summary>
    /// WoW 窗口 ID (用于 ScreenCaptureKit)
    /// </summary>
    public uint WindowId { get; init; }

    /// <summary>
    /// WoW 安装路径
    /// </summary>
    public string WowPath { get; init; } = string.Empty;

    /// <summary>
    /// 版本信息
    /// </summary>
    public Version Version { get; init; } = new();

    /// <summary>
    /// 进程 ID
    /// </summary>
    public int ProcessId => Process.Id;

    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName => Process.ProcessName;
}
