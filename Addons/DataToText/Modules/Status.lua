----------------------------------------------------------------------------
--  DataToText - Status Module
--  Provides: Boolean flags (combat, moving, dead, etc), Shapeshift, Buffs/Debuffs
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local C = DataToText.C
local U = DataToText.U

local StatusModule = {}

function StatusModule:GetData()
    -- Boolean flags (16 bits)
    local flags = {}

    flags[1] = UnitAffectingCombat(C.unitPlayer) == 1         -- In combat
    flags[2] = GetUnitSpeed(C.unitPlayer) > 0                 -- Moving
    flags[3] = UnitIsDeadOrGhost(C.unitPlayer) == 1           -- Dead/Ghost
    flags[4] = IsStealthed()                                  -- Stealthed
    flags[5] = IsMounted()                                    -- Mounted
    flags[6] = UnitIsPlayer(C.unitTarget) == 1                -- Target is player
    flags[7] = UnitIsPVP(C.unitPlayer) == 1                   -- PVP flagged
    flags[8] = IsResting()                                    -- Resting (in city)
    flags[9] = UnitOnTaxi(C.unitPlayer) == 1                  -- On taxi
    flags[10] = IsFlying()                                    -- Flying
    flags[11] = IsSwimming()                                  -- Swimming
    flags[12] = IsIndoors()                                   -- Indoors
    flags[13] = UnitChannelInfo(C.unitPlayer) ~= nil          -- Channeling
    flags[14] = UnitCastingInfo(C.unitPlayer) ~= nil          -- Casting
    flags[15] = UnitExists(C.unitPet) == 1                    -- Has pet
    flags[16] = UnitIsDead(C.unitTarget) == 1                 -- Target is dead

    -- Convert flags to hex
    local flagsHex = U.BitsToHex(U.EncodeBits(flags))

    -- Shapeshift/Stance form (0 = no form)
    local form = GetShapeshiftForm() or 0

    -- Range to target (0-99 yards, 99 = out of range)
    local range = 99
    if U.UnitExists(C.unitTarget) then
        local minRange, maxRange = UnitDistanceSquared(C.unitTarget)
        if maxRange then
            range = math.min(99, math.floor(math.sqrt(maxRange)))
        end
    end

    -- Number of buffs/debuffs on player
    local buffCount = 0
    for i = 1, 40 do
        if UnitBuff(C.unitPlayer, i) then
            buffCount = buffCount + 1
        else
            break
        end
    end

    local debuffCount = 0
    for i = 1, 40 do
        if UnitDebuff(C.unitPlayer, i) then
            debuffCount = debuffCount + 1
        else
            break
        end
    end

    -- Number of buffs/debuffs on target
    local targetBuffCount = 0
    local targetDebuffCount = 0
    if U.UnitExists(C.unitTarget) then
        for i = 1, 40 do
            if UnitBuff(C.unitTarget, i) then
                targetBuffCount = targetBuffCount + 1
            else
                break
            end
        end

        for i = 1, 40 do
            if UnitDebuff(C.unitTarget, i) then
                targetDebuffCount = targetDebuffCount + 1
            else
                break
            end
        end
    end

    -- UI Error message code (last error)
    local uiError = 0 -- TODO: Track UI errors if needed

    -- Format: L5:FLG=XXXX FORM=XX RNG=XX PB=XX PD=XX TB=XX TD=XX ERR=XXXX
    return string.format(
        "L5:FLG=%s FORM=%s RNG=%s PB=%s PD=%s TB=%s TD=%s ERR=%s",
        flagsHex,
        U.ToHex(form, 2),
        U.ToHex(range, 2),
        U.ToHex(buffCount, 2),
        U.ToHex(debuffCount, 2),
        U.ToHex(targetBuffCount, 2),
        U.ToHex(targetDebuffCount, 2),
        U.ToHex(uiError, 4)
    )
end

-- Register module
DataToText:RegisterModule("Status", StatusModule)
