using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using VizAura.Models;
using VizAura.Services;

namespace VizAura.ViewModels;

/// <summary>
/// 主窗口 ViewModel - 状态机管理器
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// 当前应用状态
    /// </summary>
    private AppState currentState = AppState.None;

    /// <summary>
    /// 当前活动的 ViewModel
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? currentViewModel;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceProvider">服务提供者</param>
    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;

        logger.LogInformation("MainWindowViewModel (状态机) 初始化");

        // 初始状态
        TransitionTo(AppState.Validating);
    }

    /// <summary>
    /// 状态转换
    /// </summary>
    /// <param name="newState">新状态</param>
    public void TransitionTo(AppState newState)
    {
        if (currentState == newState)
            return;

        logger.LogInformation($"状态转换: {currentState} → {newState}");

        // 退出旧状态
        ExitCurrentState();

        // 更新状态
        currentState = newState;

        // 进入新状态
        EnterNewState(newState);
    }

    /// <summary>
    /// 退出当前状态
    /// </summary>
    private void ExitCurrentState()
    {
        if (CurrentViewModel is ValidationViewModel validationVm)
        {
            validationVm.OnExit();
        }
        else if (CurrentViewModel is WorkViewModel workVm)
        {
            workVm.OnExit();
        }
    }

    /// <summary>
    /// 进入新状态
    /// </summary>
    /// <param name="state">新状态</param>
    private void EnterNewState(AppState state)
    {
        CurrentViewModel = state switch
        {
            AppState.Validating => CreateValidationViewModel(),
            AppState.Running => CreateWorkViewModel(),
            _ => throw new ArgumentException($"未知状态: {state}")
        };

        // 调用 OnEnter
        if (CurrentViewModel is ValidationViewModel validationVm)
        {
            validationVm.OnEnter();
        }
        else if (CurrentViewModel is WorkViewModel workVm)
        {
            workVm.OnEnter();
        }
    }

    /// <summary>
    /// 创建 ValidationViewModel
    /// </summary>
    private ValidationViewModel CreateValidationViewModel()
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ValidationViewModel>>();
        var validator = serviceProvider.GetRequiredService<StartupValidator>();

        return new ValidationViewModel(
            logger,
            validator,
            onAllValid: () => TransitionTo(AppState.Running)
        );
    }

    /// <summary>
    /// 创建 WorkViewModel
    /// </summary>
    private WorkViewModel CreateWorkViewModel()
    {
        var logger = serviceProvider.GetRequiredService<ILogger<WorkViewModel>>();
        return new WorkViewModel(logger);
    }
}
