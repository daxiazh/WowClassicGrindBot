using CommunityToolkit.Mvvm.ComponentModel;

namespace VizAura.Models;

/// <summary>
/// 验证步骤状态
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// 等待执行
    /// </summary>
    Pending,
    
    /// <summary>
    /// 执行中
    /// </summary>
    InProgress,
    
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    
    /// <summary>
    /// 失败
    /// </summary>
    Failed
}

/// <summary>
/// 验证步骤数据模型
/// </summary>
public sealed partial class ValidationStep : ObservableObject
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    [ObservableProperty]
    private string stepName = string.Empty;

    /// <summary>
    /// 当前状态
    /// </summary>
    [ObservableProperty]
    private ValidationStatus status = ValidationStatus.Pending;

    /// <summary>
    /// 状态描述消息
    /// </summary>
    [ObservableProperty]
    private string message = string.Empty;

    /// <summary>
    /// 是否可以重试
    /// </summary>
    [ObservableProperty]
    private bool canRetry;

    /// <summary>
    /// 步骤标识符
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 状态图标
    /// </summary>
    public string Icon => Status switch
    {
        ValidationStatus.Pending => "⏳",
        ValidationStatus.InProgress => "⚙️",
        ValidationStatus.Success => "✓",
        ValidationStatus.Failed => "✗",
        _ => "?"
    };
}
