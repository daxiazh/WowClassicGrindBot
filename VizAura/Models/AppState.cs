namespace VizAura.Models;

/// <summary>
/// 应用状态
/// </summary>
public enum AppState
{
    /// <summary>
    /// 无效值
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 验证/等待状态 - 检查运行条件是否满足
    /// </summary>
    Validating,

    /// <summary>
    /// 运行状态 - 正常工作中
    /// </summary>
    Running
}
