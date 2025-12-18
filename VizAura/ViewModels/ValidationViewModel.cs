using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VizAura.Models;
using VizAura.Services;

namespace VizAura.ViewModels;

/// <summary>
/// 验证状态的 ViewModel - 协调器
/// </summary>
public sealed partial class ValidationViewModel : ViewModelBase
{
    private readonly ILogger<ValidationViewModel> logger;
    private readonly IWowProcessInfoProvider processInfoProvider;
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
    /// <param name="processInfoProvider">WoW 进程信息提供者</param>
    /// <param name="onAllValid">所有验证通过时的回调</param>
    public ValidationViewModel(
        ILogger<ValidationViewModel> logger,
        IServiceProvider serviceProvider,
        IWowProcessInfoProvider processInfoProvider,
        Action<WowProcessInfo> onAllValid)
    {
        this.logger = logger;
        this.processInfoProvider = processInfoProvider;
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
            // 直接从 Provider 获取 WowProcessInfo (第一步已经设置了)
            var context = processInfoProvider.ProcessInfo;
            if (context != null)
            {
                onAllValid(context);
            }
            return;
        }

        var currentStep = Steps[CurrentStepIndex];
        
        // 不再需要手动收集 previousContext,各步骤直接从 Provider 获取
        currentStep.Status = ValidationStatus.InProgress;
        var result = await currentStep.CheckAsync(null, ct);
        currentStep.Status = result.Success ? ValidationStatus.Success : ValidationStatus.Failed;

        if (result.Success)
        {
            // 成功后推进到下一步
            CurrentStepIndex++;
            UpdateExpandedState();
            logger.LogInformation("步骤 {CurrentStepIndex} 成功,推进到步骤 {CurrentStepIndex}", CurrentStepIndex, CurrentStepIndex + 1);
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
