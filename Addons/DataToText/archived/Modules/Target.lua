----------------------------------------------------------------------------
--  DataToText - Target Module
--  Provides: Target HP, Name, GUID, Level, Classification
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local C = DataToText.C
local U = DataToText.U

local TargetModule = {}

function TargetModule:GetData()
    local target = C.unitTarget

    if not U.UnitExists(target) then
        return "L2:THP=0000/0000 TN=NONE:00 TGUID=00000000 TID=0000 TLV=00 TCLS=0 TT=0"
    end

    -- Target health
    local hp = U.GetUnitHealth(target)
    local hpMax = U.GetUnitHealthMax(target)

    -- Get target GUID once (used for multiple operations)
    local targetGuid = U.GetGUID(target)

    -- Target name (NEW: Full UTF-8 hexadecimal encoding with CRC8 checksum)
    -- Supports Chinese, Japanese, Korean and all UTF-8 characters
    -- Format: "HEX:CRC" - Example: "E9878EE78CAA:A5" for "野猪"
    -- Max 18 bytes (6 Chinese chars or 18 ASCII chars)
    local name = U.EncodeNameHex(U.GetName(target), 18)

    -- Target GUID (last 8 hex digits for unique identification)
    local guid = U.EncodeGUID(targetGuid)

    -- Target NPC ID (extracted from GUID, compatible with DataToColor)
    -- For creatures: extracts NPC ID from GUID pattern
    -- For players: returns 0000
    local npcId = U.EncodeNpcId(targetGuid)

    -- Target level
    local level = U.GetUnitLevel(target)

    -- Target classification (0=normal, 1=trivial, 2=elite, 3=rare, 4=rareelite, 5=worldboss)
    local classification = UnitClassification(target)
    local clsCode = 0
    if classification == "elite" then
        clsCode = 2
    elseif classification == "rare" then
        clsCode = 3
    elseif classification == "rareelite" then
        clsCode = 4
    elseif classification == "worldboss" then
        clsCode = 5
    elseif classification == "trivial" then
        clsCode = 1
    end

    -- Target's target (0=none, 1=player, 2=other)
    local targetTarget = C.unitTargetTarget
    local ttCode = 0
    if U.UnitExists(targetTarget) then
        if UnitIsUnit(targetTarget, C.unitPlayer) then
            ttCode = 1 -- Targeting player
        else
            ttCode = 2 -- Targeting someone else
        end
    end

    -- Format: L2:THP=XXXX/XXXX TN=<HEXDATA>:<CRC> TGUID=XXXXXXXX TID=XXXX TLV=XX TCLS=X TT=X
    -- TN: UTF-8 hexadecimal with CRC8 checksum for error detection
    -- Example: TN=E9878EE78CAA:A5 (野猪 with checksum)
    return string.format(
        "L2:THP=%s/%s TN=%s TGUID=%s TID=%s TLV=%s TCLS=%X TT=%X",
        U.ToHex(hp, 4),
        U.ToHex(hpMax, 4),
        name,  -- Now includes CRC checksum
        guid,
        npcId,
        U.ToHex(level, 2),
        clsCode,
        ttCode
    )
end

-- Register module
DataToText:RegisterModule("Target", TargetModule)
