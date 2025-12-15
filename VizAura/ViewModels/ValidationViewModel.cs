using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VizAura.Models;

namespace VizAura.ViewModels;

/// <summary>
/// 验证状态的 ViewModel - 协调器
/// </summary>
public sealed partial class ValidationViewModel : ViewModelBase
{
    private readonly ILogger<ValidationViewModel> logger;
    private readonly Action<WowProcessInfo> onAllValid;
    private CancellationTokenSource? cts;

    /// <summary>
    /// 步骤 ViewModels 集合
    /// </summary>
    public ObservableCollection<IStepViewModel> Steps { get; } = [];

    /// <summary>
    /// 是否正在验证
    /// </summary>
    [ObservableProperty]
    private bool isValidating;


    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="onAllValid">所有验证通过时的回调</param>
    public ValidationViewModel(
        ILogger<ValidationViewModel> logger,
        IServiceProvider serviceProvider,
        Action<WowProcessInfo> onAllValid)
    {
        this.logger = logger;
        this.onAllValid = onAllValid;

        Steps.Add(serviceProvider.GetRequiredService<WowProcessStepViewModel>());
        Steps.Add(serviceProvider.GetRequiredService<AddonStepViewModel>());
    }


    /// <summary>
    /// 状态进入时调用
    /// </summary>
    public void OnEnter()
    {
        logger.LogInformation("进入 Validating 状态");

        cts = new CancellationTokenSource();
        _ = ValidationLoopAsync(cts.Token);
    }

    /// <summary>
    /// 状态退出时调用
    /// </summary>
    public void OnExit()
    {
        logger.LogInformation("退出 Validating 状态");
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    /// <summary>
    /// 验证循环协程
    /// </summary>
    private async Task ValidationLoopAsync(CancellationToken ct)
    {
        using var checkTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        IsValidating = true;

        try
        {
            while (await checkTimer.WaitForNextTickAsync(ct))
            {
                // 如果有步骤失败,停止循环,让用户修复问题
                if (Steps.Any(s => s.Status == ValidationStatus.Failed))
                {
                    logger.LogInformation("检测到失败步骤,停止自动检查");
                    break;
                }

                await CheckAllStepsAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("验证循环已取消");
        }
        finally
        {
            IsValidating = false;
        }
    }

    /// <summary>
    /// 检查所有步骤
    /// </summary>
    private async Task CheckAllStepsAsync(CancellationToken ct)
    {
        WowProcessInfo? context = null;

        foreach (var step in Steps)
        {
            step.Status = ValidationStatus.InProgress;
            var result = await step.CheckAsync(context, ct);
            step.Status = result.Success ? ValidationStatus.Success : ValidationStatus.Failed;
            
            if (!result.Success)
                return;

            context = result.Data;
        }

        if (context != null)
        {
            onAllValid(context);
        }
    }

}
