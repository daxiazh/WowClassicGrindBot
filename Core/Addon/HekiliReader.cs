using SharedLib;

namespace Core;

/// <summary>
/// Hekili 技能推荐读取器 (仅自动模式)
/// 说明: 只读取自动模式下 Primary 队列的前 2 个推荐技能及其冷却时间
/// </summary>
public sealed class HekiliReader : IReader
{
    private readonly IAddonDataProvider reader;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="reader">Addon 数据提供者</param>
    public HekiliReader(IAddonDataProvider reader)
    {
        this.reader = reader;
    }

    /// <summary>
    /// Hekili 是否已启用且为自动模式
    /// </summary>
    public bool IsAutoModeEnabled => reader.GetInt(106) == 1;

    /// <summary>
    /// 推荐技能 1 ID
    /// </summary>
    public int Spell1 => reader.GetInt(107);

    /// <summary>
    /// 推荐技能 2 ID
    /// </summary>
    public int Spell2 => reader.GetInt(108);

    /// <summary>
    /// 推荐技能 1 冷却时间 (毫秒)
    /// </summary>
    public int Spell1CD => reader.GetInt(109);

    /// <summary>
    /// 推荐技能 2 冷却时间 (毫秒)
    /// </summary>
    public int Spell2CD => reader.GetInt(110);

    public void Update(IAddonDataProvider reader) { }
}
