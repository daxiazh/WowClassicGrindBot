using System.Threading;
using System.Threading.Tasks;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// 验证步骤 ViewModel 接口
/// </summary>
public interface IStepViewModel
{
    /// <summary>
    /// 步骤唯一标识
    /// </summary>
    string StepId { get; }

    /// <summary>
    /// 步骤显示名称
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// 当前验证状态
    /// </summary>
    ValidationStatus Status { get; set; }

    /// <summary>
    /// 是否显示成功视图
    /// </summary>
    bool ShowSuccessView { get; }

    /// <summary>
    /// 成功消息
    /// </summary>
    string SuccessMessage { get; }

    /// <summary>
    /// 执行检查逻辑
    /// </summary>
    /// <param name="context">上一步传递的上下文数据</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>检查结果</returns>
    Task<CheckResult> CheckAsync(WowProcessInfo? context, CancellationToken ct);
}
