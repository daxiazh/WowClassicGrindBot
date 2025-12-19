using SharedLib;
using System.Text;

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

    /// <summary>
    /// 推荐技能 1 快捷键
    /// </summary>
    public string Spell1Keybind => DecodeKeybind(reader.GetInt(111));

    /// <summary>
    /// 推荐技能 2 快捷键
    /// </summary>
    public string Spell2Keybind => DecodeKeybind(reader.GetInt(112));

    /// <summary>
    /// 解码快捷键 (从整数解码为字符串, 最多3字符)
    /// 逆向 Lua 的 EncodeKeybind 函数
    /// </summary>
    /// <param name="encoded">编码后的整数</param>
    /// <returns>快捷键字符串</returns>
    private static string DecodeKeybind(int encoded)
    {
        if (encoded == 0)
            return string.Empty;

        var sb = new StringBuilder(3);
        
        // 提取 3 个字节 (从高位到低位)
        for (int i = 2; i >= 0; i--)
        {
            int shift = i * 8;
            int byteValue = (encoded >> shift) & 0xFF;
            
            if (byteValue > 0)
                sb.Append((char)byteValue);
        }

        return sb.ToString();
    }

    public void Update(IAddonDataProvider reader) { }
}
