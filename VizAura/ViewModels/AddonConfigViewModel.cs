using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using Microsoft.Extensions.Logging;
using System;

namespace VizAura.ViewModels;

/// <summary>
/// AddOns 配置 ViewModel
/// </summary>
public sealed partial class AddonConfigViewModel : ViewModelBase
{
    private readonly ILogger<AddonConfigViewModel> logger;
    private readonly AddonConfigurator configurator;

    /// <summary>
    /// 配置对象
    /// </summary>
    [ObservableProperty]
    private AddonConfig config;

    /// <summary>
    /// 作者名称
    /// </summary>
    public string Author
    {
        get => config.Author;
        set
        {
            if (config.Author != value)
            {
                config.Author = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }
    }

    /// <summary>
    /// 插件标题
    /// </summary>
    public string Title
    {
        get => config.Title;
        set
        {
            if (config.Title != value)
            {
                config.Title = value;
                
                // Title 修改后,自动更新 Command
                if (!string.IsNullOrWhiteSpace(value))
                {
                    config.Command = value.Trim().ToLower();
                }
                
                OnPropertyChanged();
                OnPropertyChanged(nameof(Command));
                UpdateStatus();
            }
        }
    }

    /// <summary>
    /// 单元格大小
    /// </summary>
    public string CellSize
    {
        get => config.CellSize;
        set
        {
            if (config.CellSize != value)
            {
                config.CellSize = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }
    }

    /// <summary>
    /// 命令名称 (只读,由 Title 自动生成)
    /// </summary>
    public string Command => config.Command;

    /// <summary>
    /// 安装路径
    /// </summary>
    public string InstallPath => configurator.FinalAddonPath;

    /// <summary>
    /// 配置状态文本
    /// </summary>
    [ObservableProperty]
    private string statusText = "未知";

    /// <summary>
    /// 配置状态颜色
    /// </summary>
    [ObservableProperty]
    private string statusColor = "Gray";

    /// <summary>
    /// 是否已安装
    /// </summary>
    [ObservableProperty]
    private bool isInstalled;

    /// <summary>
    /// 是否可以安装/保存
    /// </summary>
    [ObservableProperty]
    private bool canSave;

    /// <summary>
    /// 反馈消息
    /// </summary>
    [ObservableProperty]
    private string? feedbackMessage;

    /// <summary>
    /// 反馈消息颜色
    /// </summary>
    [ObservableProperty]
    private string feedbackColor = "Gray";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="configurator">插件配置器</param>
    public AddonConfigViewModel(
        ILogger<AddonConfigViewModel> logger,
        AddonConfigurator configurator)
    {
        this.logger = logger;
        this.configurator = configurator;
        
        config = configurator.Config;
        
        UpdateStatus();
    }

    /// <summary>
    /// 更新状态显示
    /// </summary>
    private void UpdateStatus()
    {
        IsInstalled = configurator.Installed();

        bool hasUpdate = false;
        
        if (AddonConfig.Exists() && IsInstalled)
        {
            if (configurator.UpdateAvailable())
            {
                StatusText = "有可用更新";
                StatusColor = "Orange";
                hasUpdate = true;
            }
            else
            {
                StatusText = "已是最新";
                StatusColor = "Green";
            }
        }
        else
        {
            StatusText = "未安装";
            StatusColor = "Red";
        }

        // 只有在配置有效且(未安装 或 有更新)时才可保存
        CanSave = !string.IsNullOrWhiteSpace(Author) && 
                  !string.IsNullOrWhiteSpace(Title) &&
                  !string.IsNullOrWhiteSpace(CellSize) &&
                  (!IsInstalled || hasUpdate);

        OnPropertyChanged(nameof(InstallPath));
    }

    /// <summary>
    /// 安装/保存命令
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        try
        {
            if (configurator.Validate())
            {
                configurator.Install();
                configurator.Save();
                
                logger.LogInformation("插件安装成功");
                FeedbackMessage = "✅ 安装成功!请在游戏内执行 /reload";
                FeedbackColor = "Green";
                
                UpdateStatus();
            }
            else
            {
                logger.LogWarning("插件配置验证失败");
                FeedbackMessage = "❌ 配置格式错误,请检查输入";
                FeedbackColor = "Red";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "安装插件时出错");
            FeedbackMessage = $"❌ 安装失败: {ex.Message}";
            FeedbackColor = "Red";
        }
    }

    /// <summary>
    /// 删除命令
    /// </summary>
    [RelayCommand]
    private void Delete()
    {
        try
        {
            configurator.Delete();
            logger.LogInformation("插件已删除");
            FeedbackMessage = "✅ 已删除!请在游戏内执行 /reload";
            FeedbackColor = "Green";
            UpdateStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除插件时出错");
            FeedbackMessage = $"❌ 删除失败: {ex.Message}";
            FeedbackColor = "Red";
        }
    }

    /// <summary>
    /// 属性变化时更新状态
    /// </summary>
    partial void OnConfigChanged(AddonConfig value)
    {
        UpdateStatus();
    }
}
