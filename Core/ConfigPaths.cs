using System;
using System.IO;

namespace Core;

/// <summary>
/// 配置文件路径管理
/// macOS: ~/Library/Application Support/VizAura/
/// </summary>
public static class ConfigPaths
{
    private static readonly string BaseDirectory = 
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "Application Support",
            "VizAura"
        );

    static ConfigPaths()
    {
        if (!Directory.Exists(BaseDirectory))
        {
            Directory.CreateDirectory(BaseDirectory);
        }
    }

    /// <summary>
    /// addon_config.json 完整路径
    /// </summary>
    public static string AddonConfigPath => Path.Combine(BaseDirectory, "addon_config.json");

    /// <summary>
    /// frame_config.json 完整路径
    /// </summary>
    public static string FrameConfigPath => Path.Combine(BaseDirectory, "frame_config.json");
}
