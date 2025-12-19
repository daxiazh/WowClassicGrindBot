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
    /// 当前检测会话的 Scope
    /// 生命周期: 每次进入 Validating 状态时创建,重新检测或退出时销毁
    /// 说明: Scope 销毁时会自动释放所有 Scoped 服务 (WowScreenMacOS, PlayerReader 等)
    /// </summary>
    private IServiceScope? currentScope;

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

        try
        {
            // 退出旧状态
            ExitCurrentState();

            // 更新状态
            currentState = newState;

            // 进入新状态
            EnterNewState(newState);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "状态转换失败: {CurrentState} → {NewState}", currentState, newState);
            
            // 尝试回退到 Validating 状态
            currentState = AppState.Validating;
            currentScope = serviceProvider.CreateScope();
            CurrentViewModel = CreateValidationViewModel(currentScope);
            if (CurrentViewModel is ValidationViewModel vm)
                vm.OnEnter();
        }
    }

    /// <summary>
    /// 退出当前状态
    /// 资源释放流程:
    ///   1. 调用 ViewModel.OnExit() - 手动释放 native 资源 (如 WowScreenMacOS.Dispose())
    ///   2. 销毁 Scope - 自动释放所有 Scoped 服务
    /// </summary>
    private void ExitCurrentState()
    {
        // 1. 调用 OnExit 手动清理 (如 WowScreenMacOS.Dispose)
        if (CurrentViewModel is ValidationViewModel validationVm)
        {
            validationVm.OnExit();
        }
        else if (CurrentViewModel is WorkViewModel workVm)
        {
            workVm.OnExit();
        }
        
        // 2. 销毁 Scope - 自动释放所有 Scoped 服务
        // 包括: WorkViewModel, WowScreenMacOS, PlayerReader, AddonDataSnapshot, DataFrame[], 
        //       DataConfig, StartupClientVersion, WowProcessInfo
        currentScope?.Dispose();
        currentScope = null;
        
        logger.LogInformation("已销毁当前 Scope,所有 Scoped 服务已释放");
    }

    /// <summary>
    /// 进入新状态
    /// 资源创建流程:
    ///   1. 创建新 Scope - 全新的检测会话环境,无旧数据污染
    ///   2. 从 Scope 解析 ViewModel - 自动创建所有 Scoped 依赖
    /// </summary>
    /// <param name="state">新状态</param>
    private void EnterNewState(AppState state)
    {
        // 1. 创建新 Scope (新的检测会话)
        currentScope = serviceProvider.CreateScope();
        logger.LogInformation("已创建新 Scope,开始新的检测会话");
        
        // 2. 从 Scope 中解析 ViewModel
        CurrentViewModel = state switch
        {
            AppState.Validating => CreateValidationViewModel(currentScope),
            AppState.Running => CreateWorkViewModel(currentScope),
            _ => throw new ArgumentException($"未知状态: {state}")
        };

        // 3. 调用 OnEnter
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
    /// <param name="scope">当前检测会话的 Scope</param>
    private ValidationViewModel CreateValidationViewModel(IServiceScope scope)
    {
        // 从 Scope 中获取服务 (注意: 使用 scope.ServiceProvider 而不是根容器)
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ValidationViewModel>>();
        var processInfoProvider = scope.ServiceProvider.GetRequiredService<VizAura.Services.IWowProcessInfoProvider>();

        return new ValidationViewModel(
            logger,
            serviceProvider,
            processInfoProvider,
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
    /// 从 Scope 中解析所有 Scoped 依赖:
    ///   WorkViewModel → WowScreenMacOS, PlayerReader, AddonDataSnapshot
    ///                 → DataFrame[], WowProcessInfo, DataConfig...
    /// </summary>
    /// <param name="scope">当前检测会话的 Scope</param>
    private WorkViewModel CreateWorkViewModel(IServiceScope scope)
    {
        if (currentProcessInfo == null)
        {
            throw new InvalidOperationException("无法创建 WorkViewModel: 缺少 WoW 进程信息");
        }

        // 从 Scope 中解析 WorkViewModel (及其所有 Scoped 依赖)
        // 注意: 必须使用 scope.ServiceProvider 而不是根容器,以确保获取 Scoped 服务
        return scope.ServiceProvider.GetRequiredService<WorkViewModel>();
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

    /// <summary>
    /// 清除所有配置命令
    /// 删除 addon_config.json 和 frame_config.json,强制重新验证
    /// </summary>
    [RelayCommand]
    private void ResetAllConfig()
    {
        logger.LogInformation("用户请求清除所有配置");

        try
        {
            // 删除 addon_config.json
            if (Core.AddonConfig.Exists())
            {
                Core.AddonConfig.Delete();
                logger.LogInformation("已删除 addon_config.json");
            }

            // 删除 frame_config.json
            if (Core.FrameConfig.Exists())
            {
                Core.FrameConfig.Delete();
                logger.LogInformation("已删除 frame_config.json");
            }

            // 重置状态
            currentProcessInfo = null;
            CanConfigureAddons = false;

            // 转换到 Validating 状态,重新开始验证流程
            TransitionTo(AppState.Validating);

            logger.LogInformation("配置已清除,请重新验证环境");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清除配置时出错");
        }
    }
}
