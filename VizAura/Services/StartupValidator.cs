using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VizAura.MacOS;

namespace VizAura.Services;

/// <summary>
/// 启动验证服务,检查 WoW 进程和 DataToColor 插件
/// </summary>
public sealed class StartupValidator
{
    private readonly ILogger<StartupValidator> logger;
    private static readonly string[] wowProcessNames = 
    [
        "World of Warcraft",
        "Wow",
        "WowClassic"
    ];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public StartupValidator(ILogger<StartupValidator> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// 检查 DataToColor 插件是否安装
    /// </summary>
    /// <param name="wowPath">WoW 安装路径</param>
    /// <returns>成功状态和消息</returns>
    public (bool success, string message) ValidateAddonInstallation(string wowPath)
    {
        try
        {
            if (string.IsNullOrEmpty(wowPath))
            {
                return (false, "WoW 路径为空,无法检查插件");
            }

            logger.LogInformation($"检查 DataToColor 插件安装: {wowPath}");

            string addonsPath = Path.Combine(wowPath, "Interface", "AddOns");
            
            if (!Directory.Exists(addonsPath))
            {
                logger.LogWarning($"AddOns 目录不存在: {addonsPath}");
                return (false, $"AddOns 目录不存在\n路径: {addonsPath}");
            }

            // 查找 DataToColor 或其自定义版本
            var addonDirs = Directory.GetDirectories(addonsPath)
                .Where(dir => Path.GetFileName(dir).Contains("DataToColor", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (addonDirs.Length == 0)
            {
                logger.LogWarning("未找到 DataToColor 插件");
                return (false, $"未找到 DataToColor 插件\nAddOns 路径: {addonsPath}\n请确认插件已正确安装");
            }

            string addonDir = addonDirs[0];
            string addonName = Path.GetFileName(addonDir);
            string tocFile = Path.Combine(addonDir, $"{addonName}.toc");

            if (!File.Exists(tocFile))
            {
                logger.LogWarning($"找到插件目录但缺少 .toc 文件: {tocFile}");
                return (false, $"插件目录存在但缺少 .toc 文件\n目录: {addonDir}");
            }

            // 尝试读取版本信息
            string version = "未知";
            try
            {
                var tocLines = File.ReadAllLines(tocFile);
                var versionLine = tocLines.FirstOrDefault(line => line.StartsWith("## Version:", StringComparison.OrdinalIgnoreCase));
                if (versionLine != null)
                {
                    version = versionLine.Split(':', 2)[1].Trim();
                }
            }
            catch
            {
                // 忽略版本读取失败
            }

            logger.LogInformation($"找到 DataToColor 插件: {addonName}, 版本: {version}");
            return (true, $"找到插件: {addonName}\n版本: {version}\n路径: {addonDir}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "检查插件安装时发生错误");
            return (false, $"检查失败: {ex.Message}");
        }
    }
}
