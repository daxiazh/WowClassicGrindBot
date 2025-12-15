using System;

namespace SharedLib;

/// <summary>
/// WoW 进程接口
/// 为跨平台实现提供抽象
/// </summary>
public interface IWowProcess
{
    /// <summary>
    /// WoW 根目录路径 (包含 Interface/AddOns 的目录)
    /// </summary>
    string Path { get; }

    /// <summary>
    /// 游戏版本
    /// </summary>
    Version FileVersion { get; }
}
