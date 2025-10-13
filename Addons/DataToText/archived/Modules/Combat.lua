----------------------------------------------------------------------------
--  DataToText - Combat Module
--  Provides: GCD, Combo Points, Pet HP, Casting Info
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local C = DataToText.C
local U = DataToText.U

local CombatModule = {}

-- Track GCD
local gcdStart = 0
local gcdDuration = 0

-- Track casting
local castingSpellId = 0
local castingEndTime = 0

function CombatModule:OnInitialize()
    -- Register combat events
    local frame = CreateFrame("Frame")
    frame:RegisterEvent("SPELL_UPDATE_COOLDOWN")
    frame:RegisterEvent("UNIT_SPELLCAST_START")
    frame:RegisterEvent("UNIT_SPELLCAST_STOP")
    frame:RegisterEvent("UNIT_SPELLCAST_SUCCEEDED")
    frame:RegisterEvent("UNIT_SPELLCAST_CHANNEL_START")
    frame:RegisterEvent("UNIT_SPELLCAST_CHANNEL_STOP")

    frame:SetScript("OnEvent", function(self, event, unit)
        if event == "SPELL_UPDATE_COOLDOWN" then
            CombatModule:UpdateGCD()
        elseif event == "UNIT_SPELLCAST_START" then
            if unit == C.unitPlayer then
                CombatModule:OnCastStart()
            end
        elseif event == "UNIT_SPELLCAST_STOP" or event == "UNIT_SPELLCAST_SUCCEEDED" then
            if unit == C.unitPlayer then
                CombatModule:OnCastEnd()
            end
        elseif event == "UNIT_SPELLCAST_CHANNEL_START" then
            if unit == C.unitPlayer then
                CombatModule:OnChannelStart()
            end
        elseif event == "UNIT_SPELLCAST_CHANNEL_STOP" then
            if unit == C.unitPlayer then
                CombatModule:OnCastEnd()
            end
        end
    end)

    self.frame = frame
end

function CombatModule:UpdateGCD()
    -- Check a known spell for GCD (spell ID 61304 is GCD indicator)
    -- For classic, we'll use a common spell
    local start, duration = GetSpellCooldown(61304) -- GCD spell
    if start and start > 0 then
        gcdStart = start
        gcdDuration = duration
    end
end

function CombatModule:OnCastStart()
    local name, text, texture, startTime, endTime, isTradeSkill, castID, notInterruptible, spellId = UnitCastingInfo(C.unitPlayer)
    if spellId then
        castingSpellId = spellId
        castingEndTime = endTime / 1000 -- Convert to seconds
    end
end

function CombatModule:OnChannelStart()
    local name, text, texture, startTime, endTime, isTradeSkill, notInterruptible, spellId = UnitChannelInfo(C.unitPlayer)
    if spellId then
        castingSpellId = spellId
        castingEndTime = endTime / 1000
    end
end

function CombatModule:OnCastEnd()
    castingSpellId = 0
    castingEndTime = 0
end

function CombatModule:GetData()
    -- GCD remaining (ms)
    local gcdRemain = 0
    if gcdStart > 0 then
        local now = GetTime()
        local gcdEnd = gcdStart + gcdDuration
        if gcdEnd > now then
            gcdRemain = math.floor((gcdEnd - now) * 1000)
        end
    end

    -- Combo points (rogues, druids) or Holy Power (paladins)
    local comboPoints = 0
    if C.CHARACTER_CLASS_ID == 4 or C.CHARACTER_CLASS_ID == 11 then
        -- Rogue or Druid
        comboPoints = GetComboPoints(C.unitPlayer, C.unitTarget) or 0
    elseif C.CHARACTER_CLASS_ID == 2 then
        -- Paladin (WotLK+)
        if UnitPower then
            comboPoints = UnitPower(C.unitPlayer, Enum.PowerType.HolyPower or 9) or 0
        end
    end

    -- Pet health
    local petHP = 0
    local petHPMax = 0
    if U.UnitExists(C.unitPet) then
        petHP = U.GetUnitHealth(C.unitPet)
        petHPMax = U.GetUnitHealthMax(C.unitPet)
    end

    -- Casting spell ID
    local castSpellId = castingSpellId

    -- Cast remaining time (ms)
    local castRemain = 0
    if castingEndTime > 0 then
        local now = GetTime()
        if castingEndTime > now then
            castRemain = math.floor((castingEndTime - now) * 1000)
        end
    end

    -- Runes (Death Knight only)
    local runeStatus = "00"
    if C.CHARACTER_CLASS_ID == 6 and GetRuneCooldown then
        local blood, frost, unholy, death = 0, 0, 0, 0
        for i = 1, 6 do
            local startTime, duration, runeReady = GetRuneCooldown(i)
            if runeReady then
                local runeType = GetRuneType(i) or 0
                if runeType == 1 then blood = blood + 1
                elseif runeType == 2 then unholy = unholy + 1
                elseif runeType == 3 then frost = frost + 1
                elseif runeType == 4 then death = death + 1
                end
            end
        end
        -- Encode as 4 hex digits: Blood(1) Unholy(1) Frost(1) Death(1)
        runeStatus = string.format("%X%X%X%X", blood, unholy, frost, death)
    end

    -- Format: L3:GCD=XXXX CP=X PET=XXXX/XXXX CAST=XXXX CT=XXXX RUNE=XXXX
    return string.format(
        "L3:GCD=%s CP=%X PET=%s/%s CAST=%s CT=%s RUNE=%s",
        U.ToHex(gcdRemain, 4),
        comboPoints,
        U.ToHex(petHP, 4),
        U.ToHex(petHPMax, 4),
        U.ToHex(castSpellId, 4),
        U.ToHex(castRemain, 4),
        runeStatus
    )
end

-- Initialize on load
CombatModule:OnInitialize()

-- Register module
DataToText:RegisterModule("Combat", CombatModule)
