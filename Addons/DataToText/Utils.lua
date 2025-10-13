----------------------------------------------------------------------------
--  DataToText - 工具函数库
--  提供共享的十六进制转换和其他实用函数
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextUtils = DataToTextUtils or {}
local U = DataToTextUtils

----------------------------------------------------------------------------
-- 十六进制转换函数
----------------------------------------------------------------------------

-- 将数字转换为固定宽度的十六进制字符串
-- @param num: 要转换的数字
-- @param digits: 输出的位数（会在左侧补0）
-- @return: 十六进制字符串
function U.ToHex(num, digits)
    local hex = string.format("%X", num or 0)
    while string.len(hex) < digits do
        hex = "0" .. hex
    end
    return hex
end

-- 将十六进制字符串转换为数字
-- @param hexStr: 十六进制字符串（例如："FF", "0010"）
-- @return: 转换后的数字
function U.FromHex(hexStr)
    if not hexStr or hexStr == "" then
        return 0
    end
    return tonumber(hexStr, 16) or 0
end

----------------------------------------------------------------------------
-- GUID 相关函数
----------------------------------------------------------------------------

-- 获取单位GUID（兼容经典版）
-- @param unit: 单位ID（例如："player", "target"）
-- @return: GUID字符串
function U.GetUnitGUID(unit)
    if UnitGUID then
        return UnitGUID(unit) or "0x0000000000000000"
    end
    return "0x0000000000000000"
end

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
-- UTF-8 编码函数
----------------------------------------------------------------------------

-- 将 UTF-8 字符串转换为字节数组
-- @param str: UTF-8 编码的字符串
-- @return: 字节数组表 {byte1, byte2, byte3, ...}
function U.EncodeUTF8String(str)
    if not str or str == "" then
        return {}
    end

    local bytes = {}
    local len = string.len(str)

    for i = 1, len do
        local byte = string.byte(str, i)
        table.insert(bytes, byte)
    end

    return bytes
end

-- 计算 CRC8 校验值
-- @param bytes: 字节数组 {byte1, byte2, ...}
-- @return: CRC8 校验值 (0-255)
function U.CalculateCRC8(bytes)
    if not bytes or table.getn(bytes) == 0 then
        return 0
    end

    local crc = 0

    for i = 1, table.getn(bytes) do
        crc = bit.bxor(crc, bytes[i])

        -- CRC8 多项式处理 (简化版)
        -- 0x80 = 128, 0x07 = 7, 0xFF = 255
        for j = 1, 8 do
            if bit.band(crc, 128) ~= 0 then
                crc = bit.bxor(bit.lshift(crc, 1), 7)
            else
                crc = bit.lshift(crc, 1)
            end
            -- 保持在 8 位范围内
            crc = bit.band(crc, 255)
        end
    end

    return crc
end

-- 编码名称为十六进制格式
-- 格式: [长度(2位)][CRC8(2位)][字节1(2位)][字节2(2位)]...
-- @param name: 要编码的名称字符串
-- @return: 十六进制编码字符串
function U.EncodeNameHex(name)
    if not name or name == "" then
        return "0000"
    end

    -- 转换为字节数组
    local bytes = U.EncodeUTF8String(name)
    local len = table.getn(bytes)

    -- 限制最大长度
    if len > 255 then
        len = 255
    end

    -- 计算 CRC8
    local crc = U.CalculateCRC8(bytes)

    -- 构建十六进制字符串
    local result = U.ToHex(len, 2) .. U.ToHex(crc, 2)

    -- 添加所有字节
    for i = 1, len do
        result = result .. U.ToHex(bytes[i], 2)
    end

    return result
end
