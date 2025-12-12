using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VizAura.Models;
using VizAura.Services;

namespace VizAura.ViewModels;

/// <summary>
/// 验证状态的 ViewModel
/// </summary>
public sealed partial class ValidationViewModel : ViewModelBase
{
    private readonly ILogger<ValidationViewModel> logger;
    private readonly StartupValidator validator;
    private readonly Action onAllValid;

    /// <summary>
    /// 验证步骤集合
    /// </summary>
    public ObservableCollection<ValidationStep> ValidationSteps { get; } = [];

    /// <summary>
    /// 是否正在验证
    /// </summary>
    [ObservableProperty]
    private bool isValidating;

    /// <summary>
    /// 当前检测到的 WoW 路径
    /// </summary>
    [ObservableProperty]
    private string currentWowPath = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="validator">启动验证服务</param>
    /// <param name="onAllValid">所有验证通过时的回调</param>
    public ValidationViewModel(
        ILogger<ValidationViewModel> logger,
        StartupValidator validator,
        Action onAllValid)
    {
        this.logger = logger;
        this.validator = validator;
        this.onAllValid = onAllValid;

        InitializeValidationSteps();
    }

    /// <summary>
    /// 状态进入时调用
    /// </summary>
    public void OnEnter()
    {
        logger.LogInformation("进入 Validating 状态");
        
        // 自动开始验证
        _ = RunValidationAsync();
    }

    /// <summary>
    /// 状态退出时调用
    /// </summary>
    public void OnExit()
    {
        logger.LogInformation("退出 Validating 状态");
    }

    /// <summary>
    /// 初始化验证步骤列表
    /// </summary>
    private void InitializeValidationSteps()
    {
        ValidationSteps.Clear();
        ValidationSteps.Add(new ValidationStep
        {
            Id = "wow_process",
            StepName = "检查 World of Warcraft 进程",
            Status = ValidationStatus.Pending,
            Message = "等待开始...",
            CanRetry = false
        });

        ValidationSteps.Add(new ValidationStep
        {
            Id = "addon_installation",
            StepName = "检查 DataToColor 插件安装",
            Status = ValidationStatus.Pending,
            Message = "等待开始...",
            CanRetry = false
        });
    }

    /// <summary>
    /// 开始验证命令
    /// </summary>
    [RelayCommand]
    private async Task StartValidationAsync()
    {
        await RunValidationAsync();
    }

    /// <summary>
    /// 重试指定步骤命令
    /// </summary>
    /// <param name="step">要重试的步骤</param>
    [RelayCommand]
    private async Task RetryStepAsync(ValidationStep step)
    {
        if (step == null || !step.CanRetry)
            return;

        logger.LogInformation($"重试步骤: {step.StepName}");

        // 重置该步骤及后续步骤
        bool resetFollowing = false;
        foreach (var s in ValidationSteps)
        {
            if (s.Id == step.Id)
            {
                resetFollowing = true;
            }

            if (resetFollowing)
            {
                s.Status = ValidationStatus.Pending;
                s.Message = "等待重试...";
                s.CanRetry = false;
            }
        }

        await RunValidationAsync();
    }

    /// <summary>
    /// 执行验证流程
    /// </summary>
    private async Task RunValidationAsync()
    {
        if (IsValidating)
            return;

        IsValidating = true;

        try
        {
            // 步骤 1: 检查 WoW 进程
            await ValidateWowProcessAsync();

            // 只有步骤 1 成功才继续
            var wowStep = ValidationSteps.FirstOrDefault(s => s.Id == "wow_process");
            if (wowStep?.Status != ValidationStatus.Success)
            {
                logger.LogWarning("WoW 进程检查失败,停止验证");
                return;
            }

            // 步骤 2: 检查插件安装
            await ValidateAddonInstallationAsync();

            // 检查所有步骤是否成功
            bool allValid = ValidationSteps.All(s => s.Status == ValidationStatus.Success);

            if (allValid)
            {
                logger.LogInformation("所有验证步骤完成,准备就绪");
                onAllValid?.Invoke();
            }
        }
        finally
        {
            IsValidating = false;
        }
    }

    /// <summary>
    /// 验证 WoW 进程
    /// </summary>
    private async Task ValidateWowProcessAsync()
    {
        var step = ValidationSteps.FirstOrDefault(s => s.Id == "wow_process");
        if (step == null || step.Status == ValidationStatus.Success)
            return;

        step.Status = ValidationStatus.InProgress;
        step.Message = "正在检查...";
        step.CanRetry = false;

        await Task.Run(() =>
        {
            var (success, message, wowPath) = validator.ValidateWowProcess();

            step.Status = success ? ValidationStatus.Success : ValidationStatus.Failed;
            step.Message = message;
            step.CanRetry = !success;

            if (success)
            {
                CurrentWowPath = wowPath;
            }
        });
    }

    /// <summary>
    /// 验证插件安装
    /// </summary>
    private async Task ValidateAddonInstallationAsync()
    {
        var step = ValidationSteps.FirstOrDefault(s => s.Id == "addon_installation");
        if (step == null || step.Status == ValidationStatus.Success)
            return;

        step.Status = ValidationStatus.InProgress;
        step.Message = "正在检查...";
        step.CanRetry = false;

        await Task.Run(() =>
        {
            var (success, message) = validator.ValidateAddonInstallation(CurrentWowPath);

            step.Status = success ? ValidationStatus.Success : ValidationStatus.Failed;
            step.Message = message;
            step.CanRetry = !success;
        });
    }
}
