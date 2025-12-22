using System;
using System.IO;

namespace Core;

/// <summary>
/// 配置文件路径管理
/// 智能检测运行环境:
///   - .app Bundle: ~/Library/Application Support/VizAura/
///   - 开发模式: 当前工作目录
/// </summary>
public static class ConfigPaths
{
    private static readonly string BaseDirectory = GetBaseDirectory();

    static ConfigPaths()
    {
        if (!Directory.Exists(BaseDirectory))
        {
            Directory.CreateDirectory(BaseDirectory);
        }
    }

    /// <summary>
    /// 获取配置文件基础目录
    /// </summary>
    /// <returns>配置文件存储目录</returns>
    private static string GetBaseDirectory()
    {
        // 检测是否在 .app Bundle 内运行
        var execPath = Environment.ProcessPath;
        if (execPath != null && execPath.Contains(".app/Contents/MacOS"))
        {
            // .app Bundle 模式: 使用 Application Support 目录 (用户可写)
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "VizAura"
            );
        }
        
        // 开发模式: 使用当前工作目录
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// addon_config.json 完整路径
    /// </summary>
    public static string AddonConfigPath => Path.Combine(BaseDirectory, "addon_config.json");

    /// <summary>
    /// frame_config.json 完整路径
    /// </summary>
    public static string FrameConfigPath => Path.Combine(BaseDirectory, "frame_config.json");

    /// <summary>
    /// data_config.json 完整路径
    /// </summary>
    public static string DataConfigPath => Path.Combine(BaseDirectory, "data_config.json");
}
