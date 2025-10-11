----------------------------------------------------------------------------
--  DataToText - Utility Functions
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local U = DataToText.U

-- Hexadecimal encoding (0-9, A-F only - OCR friendly)
-- Avoids confusing characters: O/0, I/1/l, S/5, B/8

-- Convert number to hex string with fixed width
function U.ToHex(number, width)
    if not number then return string.rep("0", width or 4) end

    -- Clamp to valid range
    local maxVal = (16 ^ (width or 4)) - 1
    number = math.max(0, math.min(number, maxVal))

    return string.format("%0" .. (width or 4) .. "X", math.floor(number))
end

-- Convert hex string to number
function U.FromHex(hexStr)
    if not hexStr or hexStr == "" then return 0 end
    return tonumber(hexStr, 16) or 0
end

-- Encode float as hex (multiply by 100 to preserve 2 decimal places)
function U.ToHexFloat(float, width)
    if not float then return string.rep("0", width or 4) end
    return U.ToHex(math.floor(float * 100), width)
end

-- Decode hex float
function U.FromHexFloat(hexStr)
    return U.FromHex(hexStr) / 100
end

-- Calculate simple checksum (XOR of all bytes)
function U.Checksum(text)
    if not text or text == "" then return 0 end

    local sum = 0
    local len = string.len(text)
    for i = 1, len do
        sum = bit.bxor(sum, string.byte(text, i))
    end

    return sum
end

-- Generate checksum hex string
function U.ChecksumHex(text)
    return U.ToHex(U.Checksum(text), 4)
end

-- Validate checksum
function U.ValidateChecksum(text, expectedChecksum)
    local actualChecksum = U.Checksum(text)
    return actualChecksum == U.FromHex(expectedChecksum)
end

-- Encode GUID (take last 8 hex digits)
function U.EncodeGUID(guid)
    if not guid or guid == "" then return "00000000" end

    -- GUID format: 0x00000000XXXXXXXX (or similar)
    -- Extract last 8 hex digits
    local hex = string.match(guid, "(%x+)$")
    if hex and string.len(hex) >= 8 then
        return string.sub(hex, -8) -- Last 8 characters
    end

    return "00000000"
end

-- Encode NPC ID from GUID
function U.EncodeNpcId(guid)
    if not guid or guid == "" then return "0000" end

    -- Extract creature ID from GUID
    -- Format: 0xF1300XXXYYYYZZZZ where YYY is NPC ID
    local npcId = tonumber(string.sub(guid, 8, 12), 16) or 0
    return U.ToHex(npcId, 4)
end

-- Encode unit name (first 8 characters, uppercase, ASCII only)
function U.EncodeName(name)
    if not name or name == "" then return "NONE" end

    -- Convert to uppercase and limit to 8 chars
    name = string.upper(name)
    name = string.sub(name, 1, 8)

    -- Remove non-ASCII characters
    name = string.gsub(name, "[^A-Z0-9]", "")

    -- Pad with underscores if too short
    local len = string.len(name)
    if len < 4 then
        name = name .. string.rep("_", 4 - len)
    end

    return name
end

-- Encode boolean flags as binary string
function U.EncodeBits(flags)
    local result = ""
    local count = table.getn(flags)
    for i = 1, count do
        result = result .. (flags[i] and "1" or "0")
    end
    return result
end

-- Convert binary string to hex
function U.BitsToHex(binaryStr)
    local decimal = tonumber(binaryStr, 2) or 0
    return U.ToHex(decimal, 4)
end

-- Format coordinate (multiply by 100, encode as hex)
function U.EncodeCoord(coord)
    if not coord then return "0000" end
    return U.ToHex(math.floor(coord * 100), 4)
end

-- Format time in ms as hex
function U.EncodeTime(seconds)
    if not seconds then return "0000" end
    return U.ToHex(math.floor(seconds * 1000), 4)
end

-- Safe unit data retrieval
function U.GetUnitHealth(unit)
    return UnitHealth(unit) or 0
end

function U.GetUnitHealthMax(unit)
    return UnitHealthMax(unit) or 0
end

function U.GetUnitPower(unit, powerType)
    return UnitPower(unit, powerType) or 0
end

function U.GetUnitPowerMax(unit, powerType)
    return UnitPowerMax(unit, powerType) or 0
end

function U.GetUnitLevel(unit)
    local level = UnitLevel(unit)
    if not level or level == -1 then
        return 99 -- Unknown/skull level
    end
    return level
end

-- Safe GUID retrieval
function U.GetGUID(unit)
    if not UnitGUID then return "" end
    return UnitGUID(unit) or ""
end

-- Safe name retrieval
function U.GetName(unit)
    return UnitName(unit) or ""
end

-- Get player position
function U.GetPlayerPosition()
    if GetPlayerMapPosition then
        local x, y = GetPlayerMapPosition("player")
        return x or 0, y or 0
    elseif C_Map and C_Map.GetPlayerMapPosition then
        local pos = C_Map.GetPlayerMapPosition(C_Map.GetBestMapForUnit("player"), "player")
        if pos then
            return pos:GetXY()
        end
    end
    return 0, 0
end

-- Get player facing (radians)
function U.GetPlayerFacing()
    if not GetPlayerFacing then return 0 end
    return GetPlayerFacing() or 0
end

-- Check if unit exists
function U.UnitExists(unit)
    return UnitExists(unit) == 1
end

-- Clamp value
function U.Clamp(value, min, max)
    return math.max(min, math.min(value, max))
end

-- Round to nearest integer
function U.Round(value)
    return math.floor(value + 0.5)
end
