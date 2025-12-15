using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using Microsoft.Extensions.Logging;
using System;
using VizAura.Services;

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
                OnPropertyChanged();
                OnPropertyChanged(nameof(Command));
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

        if (AddonConfig.Exists() && IsInstalled)
        {
            if (configurator.UpdateAvailable())
            {
                StatusText = "有可用更新";
                StatusColor = "Orange";
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

        CanSave = !string.IsNullOrWhiteSpace(Author) && 
                  !string.IsNullOrWhiteSpace(Title) &&
                  !string.IsNullOrWhiteSpace(CellSize);

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
                UpdateStatus();
            }
            else
            {
                logger.LogWarning("插件配置验证失败");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "安装插件时出错");
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
            UpdateStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除插件时出错");
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
