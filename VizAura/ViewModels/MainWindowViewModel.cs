using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using VizAura.Models;

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
    private AppState currentState;

    /// <summary>
    /// 当前 WoW 进程信息
    /// </summary>
    private WowProcessInfo? currentProcessInfo;

    /// <summary>
    /// 是否可以配置 AddOns (只有在有 WoW 进程信息时才可用)
    /// </summary>
    [ObservableProperty]
    private bool canConfigureAddons;

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

        return new ValidationViewModel(
            logger,
            serviceProvider,
            (processInfo) =>
            {
                currentProcessInfo = processInfo;
                CanConfigureAddons = true;
                TransitionTo(AppState.Running);
            }
        );
    }

    /// <summary>
    /// 创建 WorkViewModel
    /// </summary>
    private WorkViewModel CreateWorkViewModel()
    {
        if (currentProcessInfo == null)
        {
            throw new InvalidOperationException("无法创建 WorkViewModel: 缺少 WoW 进程信息");
        }

        var logger = serviceProvider.GetRequiredService<ILogger<WorkViewModel>>();
        return new WorkViewModel(logger, currentProcessInfo);
    }

    /// <summary>
    /// 重新验证环境命令
    /// </summary>
    [RelayCommand]
    private void Revalidate()
    {
        logger.LogInformation("用户请求重新验证环境");
        currentProcessInfo = null;
        CanConfigureAddons = false;
        TransitionTo(AppState.Validating);
    }

    /// <summary>
    /// 显示 AddOns 配置命令
    /// </summary>
    [RelayCommand]
    private async void ShowAddonsConfig()
    {
        logger.LogInformation("显示 AddOns 配置");

        if (currentProcessInfo == null)
        {
            logger.LogWarning("无法打开 AddOns 配置:缺少 WoW 进程信息");
            return;
        }

        try
        {
            // 创建 VizAuraWowProcess 适配器
            var wowProcess = new VizAura.Services.VizAuraWowProcess(currentProcessInfo);

            // 创建 AddonConfigurator
            var configuratorLogger = serviceProvider.GetRequiredService<ILogger<Core.AddonConfigurator>>();
            var configurator = new Core.AddonConfigurator(configuratorLogger, wowProcess);

            // 创建 ViewModel
            var viewModelLogger = serviceProvider.GetRequiredService<ILogger<AddonConfigViewModel>>();
            var viewModel = new AddonConfigViewModel(viewModelLogger, configurator);

            // 创建并显示对话框
            var window = new VizAura.Views.AddonConfigWindow
            {
                DataContext = viewModel
            };

            // 使用 ShowDialog 模态显示
            await window.ShowDialog((Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null) ?? throw new InvalidOperationException());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开 AddOns 配置对话框时出错");
        }
    }
}
