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
        return "L2:THP=0000/0000 TN=NONE TGUID=00000000 TID=0000 TLV=00 TCLS=0"
    end

    -- Target health
    local hp = U.GetUnitHealth(target)
    local hpMax = U.GetUnitHealthMax(target)

    -- Target name (first 8 chars, uppercase)
    local name = U.EncodeName(U.GetName(target))

    -- Target GUID (last 8 hex digits)
    local guid = U.EncodeGUID(U.GetGUID(target))

    -- Target NPC ID (from GUID)
    local npcId = U.EncodeNpcId(U.GetGUID(target))

    -- Target level
    local level = U.GetUnitLevel(target)

    -- Target classification (0=normal, 1=trivial, 2=elite, 3=rare, 4=worldboss)
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

    -- Format: L2:THP=XXXX/XXXX TN=NAME TGUID=XXXXXXXX TID=XXXX TLV=XX TCLS=X TT=X
    return string.format(
        "L2:THP=%s/%s TN=%s TGUID=%s TID=%s TLV=%s TCLS=%X TT=%X",
        U.ToHex(hp, 4),
        U.ToHex(hpMax, 4),
        name,
        guid,
        npcId,
        U.ToHex(level, 2),
        clsCode,
        ttCode
    )
end

-- Register module
DataToText:RegisterModule("Target", TargetModule)
