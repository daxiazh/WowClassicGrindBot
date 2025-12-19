namespace Core;

/// <summary>
/// Addon 数据验证结果枚举
/// </summary>
public enum AddonValidationResult
{
    /// <summary>
    /// 验证成功
    /// </summary>
    Success,
    
    /// <summary>
    /// 首帧校验失败 (Frame[0] 不是黑色)
    /// 可能原因: 不在游戏画面、插件未加载、窗口被遮挡
    /// </summary>
    FirstFrameFailed,
    
    /// <summary>
    /// CRC 校验失败 (数据被破坏)
    /// 可能原因: 窗口部分被遮挡、分辨率变化
    /// </summary>
    CrcFailed,
    
    /// <summary>
    /// Frame 坐标越界 (分辨率改变导致配置失效)
    /// 可能原因: 分辨率已改变
    /// </summary>
    BoundsOutOfRange
}
