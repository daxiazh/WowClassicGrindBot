using VizAura.Models;

namespace VizAura.Services;

/// <summary>
/// WowProcessInfo 提供者接口
/// 用于在 DI 容器中延迟提供运行时才知道的 WowProcessInfo
/// </summary>
public interface IWowProcessInfoProvider
{
    /// <summary>
    /// 获取当前的 WoW 进程信息
    /// </summary>
    WowProcessInfo? ProcessInfo { get; }
    
    /// <summary>
    /// 设置 WoW 进程信息 (由 ValidationViewModel 在验证成功后调用)
    /// </summary>
    /// <param name="processInfo">WoW 进程信息</param>
    void SetProcessInfo(WowProcessInfo processInfo);
}

/// <summary>
/// WowProcessInfo 提供者实现
/// </summary>
public sealed class WowProcessInfoProvider : IWowProcessInfoProvider
{
    private WowProcessInfo? processInfo;
    
    public WowProcessInfo? ProcessInfo => processInfo;
    
    public void SetProcessInfo(WowProcessInfo processInfo)
    {
        this.processInfo = processInfo;
    }
}
