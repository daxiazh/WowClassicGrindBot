----------------------------------------------------------------------------
--  DataToText - Player Module
--  Provides: HP, MP, Position, Facing, Level, XP
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local C = DataToText.C
local U = DataToText.U

local PlayerModule = {}

function PlayerModule:GetData()
    local player = C.unitPlayer

    -- Health
    local hp = U.GetUnitHealth(player)
    local hpMax = U.GetUnitHealthMax(player)

    -- Power (mana/rage/energy)
    local mp = U.GetUnitPower(player, nil)
    local mpMax = U.GetUnitPowerMax(player, nil)

    -- Position
    local x, y = U.GetPlayerPosition()

    -- Facing (radians, 0-2π)
    local facing = U.GetPlayerFacing()

    -- Level
    local level = U.GetUnitLevel(player)

    -- Experience
    local xp = UnitXP(player) or 0
    local xpMax = UnitXPMax(player) or 1

    -- Money (copper)
    local money = GetMoney() or 0

    -- Format: L1:HP=XXXX/XXXX MP=XXXX/XXXX X=XXXX Y=XXXX F=XXXX LV=XX XP=XXXX/XXXX $=XXXXXXXX
    return string.format(
        "L1:HP=%s/%s MP=%s/%s X=%s Y=%s F=%s LV=%s XP=%s/%s $=%s",
        U.ToHex(hp, 4),
        U.ToHex(hpMax, 4),
        U.ToHex(mp, 4),
        U.ToHex(mpMax, 4),
        U.EncodeCoord(x),
        U.EncodeCoord(y),
        U.ToHexFloat(facing, 4),
        U.ToHex(level, 2),
        U.ToHex(xp, 4),
        U.ToHex(xpMax, 4),
        U.ToHex(money, 8)
    )
end

-- Register module
DataToText:RegisterModule("Player", PlayerModule)
