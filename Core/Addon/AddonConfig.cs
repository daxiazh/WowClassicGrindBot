using Newtonsoft.Json;

using System.IO;

using static Newtonsoft.Json.JsonConvert;

namespace Core;

/// <summary>
/// 插件配置元数据，定义配置文件的版本和默认文件名
/// </summary>
public static class AddonConfigMeta
{
    /// <summary>
    /// 配置文件版本号
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// 默认配置文件名
    /// </summary>
    public const string DefaultFileName = "addon_config.json";
}

/// <summary>
/// WoW 插件配置类，用于管理插件的基本元数据和运行时参数。
/// 插件在 WoW 窗口顶部绘制彩色像素网格，机器人通过解析这些像素读取游戏状态。
/// 该配置文件在首次设置时通过 AddonConfigurator（BlazorServer）生成并保存。
/// </summary>
public sealed class AddonConfig
{
    /// <summary>
    /// 配置文件版本号，用于兼容性检查
    /// </summary>
    public int Version { get; init; } = AddonConfigMeta.Version;

    /// <summary>
    /// 插件作者
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 像素单元格大小，影响插件在 WoW 窗口顶部绘制的彩色像素网格的尺寸
    /// </summary>
    public string CellSize { get; set; } = "1";

    /// <summary>
    /// 插件标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 插件命令前缀（如 "/botname"），用于在游戏中执行插件命令
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 自动生成的刷新命令，用于刷新插件状态
    /// </summary>
    [JsonIgnore]
    public string CommandFlush => Command + "flush";

    /// <summary>
    /// 检查配置是否为默认（未配置）状态
    /// </summary>
    /// <returns>如果 Author、Title 或 Command 为空，则返回 true</returns>
    public bool IsDefault()
    {
        return
            string.IsNullOrEmpty(Author) ||
            string.IsNullOrEmpty(Title) ||
            string.IsNullOrEmpty(Command);
    }

    /// <summary>
    /// 从 addon_config.json 文件加载配置
    /// </summary>
    /// <returns>如果文件存在且版本匹配则返回加载的配置，否则返回新的默认配置</returns>
    public static AddonConfig Load()
    {
        if (Exists())
        {
            var loaded = DeserializeObject<AddonConfig>(File.ReadAllText(AddonConfigMeta.DefaultFileName))!;
            if (loaded.Version == AddonConfigMeta.Version)
                return loaded;
        }

        return new AddonConfig();
    }

    /// <summary>
    /// 检查配置文件是否存在
    /// </summary>
    /// <returns>如果 addon_config.json 文件存在则返回 true</returns>
    public static bool Exists()
    {
        return File.Exists(AddonConfigMeta.DefaultFileName);
    }

    /// <summary>
    /// 删除配置文件
    /// </summary>
    public static void Delete()
    {
        if (Exists())
        {
            File.Delete(AddonConfigMeta.DefaultFileName);
        }
    }

    /// <summary>
    /// 保存配置到 addon_config.json 文件
    /// </summary>
    public void Save()
    {
        File.WriteAllText(AddonConfigMeta.DefaultFileName, SerializeObject(this));
    }
}