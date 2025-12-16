using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    /// 当前步骤索引
    /// </summary>
    [ObservableProperty]
    private int currentStepIndex;

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
        Steps.Add(serviceProvider.GetRequiredService<FrameStepViewModel>());

        // 初始化展开状态
        UpdateExpandedState();
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
                await CheckCurrentStepAsync(ct);
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
    /// 检查当前步骤
    /// </summary>
    private async Task CheckCurrentStepAsync(CancellationToken ct)
    {
        // 如果已经全部成功
        if (CurrentStepIndex >= Steps.Count)
        {
            // 收集上下文信息
            WowProcessInfo? context = null;
            foreach (var step in Steps)
            {
                if (step is WowProcessStepViewModel wowStep)
                {
                    context = new WowProcessInfo
                    {
                        Process = System.Diagnostics.Process.GetProcessById(wowStep.ProcessId),
                        WindowId = wowStep.WindowId,
                        WowPath = wowStep.WowPath ?? string.Empty,
                        Version = new Version()
                    };
                    break;
                }
            }

            if (context != null)
            {
                onAllValid(context);
            }
            return;
        }

        var currentStep = Steps[CurrentStepIndex];
        
        // 收集上下文: 向前查找第一个 WowProcessStepViewModel
        WowProcessInfo? previousContext = null;
        for (int i = CurrentStepIndex - 1; i >= 0; i--)
        {
            if (Steps[i] is WowProcessStepViewModel wowStep)
            {
                previousContext = new WowProcessInfo
                {
                    Process = System.Diagnostics.Process.GetProcessById(wowStep.ProcessId),
                    WindowId = wowStep.WindowId,
                    WowPath = wowStep.WowPath ?? string.Empty,
                    Version = new Version()
                };
                break;
            }
        }

        currentStep.Status = ValidationStatus.InProgress;
        var result = await currentStep.CheckAsync(previousContext, ct);
        currentStep.Status = result.Success ? ValidationStatus.Success : ValidationStatus.Failed;

        if (result.Success)
        {
            // 成功后推进到下一步
            CurrentStepIndex++;
            UpdateExpandedState();
            logger.LogInformation($"步骤 {CurrentStepIndex} 成功,推进到步骤 {CurrentStepIndex + 1}");
        }
        else
        {
            logger.LogInformation($"步骤 {CurrentStepIndex + 1} 失败,等待用户修复");
        }
    }

    /// <summary>
    /// 更新展开状态：只展开当前步骤
    /// </summary>
    private void UpdateExpandedState()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsExpanded = (i == CurrentStepIndex);
        }
    }

}
