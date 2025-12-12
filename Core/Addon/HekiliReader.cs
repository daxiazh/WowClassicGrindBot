using SharedLib;

namespace Core;

public sealed class HekiliReader : IReader
{
    private readonly IAddonDataProvider reader;

    public HekiliReader(IAddonDataProvider reader)
    {
        this.reader = reader;
    }

    public int PrimarySpell1 => reader.GetInt(106);
    public int PrimarySpell2 => reader.GetInt(107);

    public int AOESpell1 => reader.GetInt(108);
    public int AOESpell2 => reader.GetInt(109);

    public int CooldownsSpell1 => reader.GetInt(110);
    public int CooldownsSpell2 => reader.GetInt(111);

    public int DefensivesSpell1 => reader.GetInt(112);
    public int DefensivesSpell2 => reader.GetInt(113);

    public int InterruptsSpell1 => reader.GetInt(114);
    public int InterruptsSpell2 => reader.GetInt(115);

    public void Update(IAddonDataProvider reader) { }
}
