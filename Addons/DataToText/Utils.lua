----------------------------------------------------------------------------
--  DataToText - 工具函数库
--  提供字符串处理和CRC32校验函数
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextUtils = DataToTextUtils or {}
local U = DataToTextUtils

----------------------------------------------------------------------------
-- 字符串处理函数
----------------------------------------------------------------------------

-- 去除字符串首尾空格
-- @param str: 输入字符串
-- @return: 去除空格后的字符串
function U.Trim(str)
    return string.gsub(str or "", "^%s*(.-)%s*$", "%1")
end

----------------------------------------------------------------------------
-- CRC32 校验函数
----------------------------------------------------------------------------

-- CRC32 查找表（懒加载）
local CRC32_TABLE = nil

-- 预缓存函数引用（性能优化）
local band = bit.band
local bxor = bit.bxor
local rshift = bit.rshift
local strlen = string.len
local strbyte = string.byte

-- 初始化 CRC32 查找表
local function InitCRC32Table()
    if CRC32_TABLE then
        return
    end

    CRC32_TABLE = {}
    for i = 0, 255 do
        local crc = i
        for j = 1, 8 do
            if band(crc, 1) ~= 0 then
                crc = bxor(rshift(crc, 1), 3988292384)  -- 0xEDB88320
            else
                crc = rshift(crc, 1)
            end
        end
        CRC32_TABLE[i] = crc
    end
end

-- 计算 CRC32 校验值（优化版，使用查找表）
-- 使用 IEEE 802.3 多项式 (0xEDB88320)
-- @param str: 输入字符串
-- @return: CRC32 校验值 (0-4294967295)
function U.CalculateCRC32(str)
    if not str or str == "" then
        return 0
    end

    -- 初始化查找表（首次调用）
    InitCRC32Table()

    local crc = 4294967295  -- 0xFFFFFFFF
    local len = strlen(str)

    for i = 1, len do
        local byte = strbyte(str, i)
        local index = band(bxor(crc, byte), 255)
        crc = bxor(rshift(crc, 8), CRC32_TABLE[index])
    end

    -- 返回补码
    return bxor(crc, 4294967295)
end
