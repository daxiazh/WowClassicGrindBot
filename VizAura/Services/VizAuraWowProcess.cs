using SharedLib;
using System;
using VizAura.Models;

namespace VizAura.Services;

/// <summary>
/// VizAura 的 WoW 进程适配器
/// 将 WowProcessInfo 适配为 IWowProcess 接口
/// </summary>
public sealed class VizAuraWowProcess : IWowProcess
{
    private readonly WowProcessInfo processInfo;

    /// <summary>
    /// WoW 根目录路径 (包含 Interface/AddOns 的目录)
    /// </summary>
    public string Path => processInfo.WowPath;

    /// <summary>
    /// 游戏版本
    /// </summary>
    public Version FileVersion => processInfo.Version;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="processInfo">WoW 进程信息</param>
    public VizAuraWowProcess(WowProcessInfo processInfo)
    {
        this.processInfo = processInfo ?? throw new ArgumentNullException(nameof(processInfo));
    }
}
