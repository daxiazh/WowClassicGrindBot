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

-- Encode NPC ID from GUID (compatible with DataToColor)
-- GUID format: Creature-0-4488-530-222-19350-000005C0D70
-- NPC ID is the second-to-last segment
function U.EncodeNpcId(guid)
    if not guid or guid == "" then return "0000" end

    -- Extract NPC ID from GUID using pattern matching
    local id = guid:match("-(%d+)-[^-]+$")

    if id and not guid:find("^Player") then
        return U.ToHex(tonumber(id, 10), 4)
    end

    return "0000"
end

-- Encode unit name using ASCII values (compatible with DataToColor)
-- Converts name to uppercase ASCII values for OCR-friendly display
-- Chinese and special characters will be encoded but may not be readable
-- DEPRECATED: Use EncodeNameHex for full UTF-8 support
function U.EncodeName(name)
    if not name or name == "" then return "000000" end

    -- Take first 6 characters and convert to uppercase
    local str = string.upper(string.sub(name, 1, 6))

    -- Convert to ASCII value encoding
    local asciiValue = 0
    for i = 1, string.len(str) do
        local byteVal = string.byte(str, i)
        -- Limit to max 90 (Z character) to prevent overflow
        asciiValue = asciiValue * 100 + math.min(byteVal, 90)
    end

    -- Return as 6-digit hex (can store up to 16,777,215)
    return U.ToHex(asciiValue, 6)
end

-- Calculate CRC8 checksum for error detection
-- Uses CRC-8-CCITT polynomial: x^8 + x^2 + x + 1 (0x07)
function U.CalculateCRC8(str)
    if not str or str == "" then return 0 end

    local crc = 0
    local polynomial = 0x07

    for i = 1, string.len(str) do
        local byte = string.byte(str, i)
        crc = bit.bxor(crc, byte)

        for j = 1, 8 do
            if bit.band(crc, 0x80) ~= 0 then
                crc = bit.bxor(bit.lshift(crc, 1), polynomial)
            else
                crc = bit.lshift(crc, 1)
            end
            crc = bit.band(crc, 0xFF)
        end
    end

    return crc
end

-- Encode unit name as hexadecimal UTF-8 byte stream (NEW METHOD)
-- Supports full UTF-8 encoding including Chinese, Japanese, Korean characters
-- Returns format: "<HEX>:<CRC>" for error detection
-- Example: "野猪" -> "E9878EE78CAA:A5"
function U.EncodeNameHex(name, maxBytes)
    if not name or name == "" then return "NONE:00" end

    maxBytes = maxBytes or 18  -- Default 18 bytes (6 Chinese chars or 18 ASCII chars)

    -- Convert name to hex string
    local hex = ""
    local byteCount = 0

    for i = 1, maxBytes do
        local byte = string.byte(name, i)
        if not byte then break end

        hex = hex .. string.format("%02X", byte)
        byteCount = byteCount + 1
    end

    if hex == "" then
        return "NONE:00"
    end

    -- Calculate CRC8 checksum on original bytes (not hex string)
    local checksum = U.CalculateCRC8(string.sub(name, 1, byteCount))

    -- Return format: HEX:CRC
    return hex .. ":" .. string.format("%02X", checksum)
end

-- Decode hexadecimal string back to UTF-8 string (for testing)
-- This is mainly for debugging in Lua, actual decoding happens in C#
function U.DecodeNameHex(hexWithCrc)
    if not hexWithCrc or hexWithCrc == "NONE:00" then return "" end

    -- Split hex data and CRC
    local colonPos = string.find(hexWithCrc, ":")
    if not colonPos then return "" end

    local hex = string.sub(hexWithCrc, 1, colonPos - 1)
    local crcHex = string.sub(hexWithCrc, colonPos + 1)

    -- Convert hex to bytes
    local result = ""
    local bytes = {}

    for i = 1, string.len(hex), 2 do
        local byteHex = string.sub(hex, i, i + 1)
        local byte = tonumber(byteHex, 16)
        if byte then
            result = result .. string.char(byte)
            table.insert(bytes, byte)
        end
    end

    -- Verify CRC (optional, for debugging)
    local expectedCrc = U.CalculateCRC8(result)
    local actualCrc = tonumber(crcHex, 16) or 0

    if expectedCrc ~= actualCrc then
        -- CRC mismatch, return empty string
        return ""
    end

    return result
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
