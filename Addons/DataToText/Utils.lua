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
-- 数据编码函数（带CRC8校验）
----------------------------------------------------------------------------

-- 将十六进制字符串编码为带CRC8的格式
-- 格式: [CRC8(2位)][数据]
-- @param hexStr: 十六进制字符串
-- @return: 编码后的字符串
function U.EncodeHexWithCRC(hexStr)
    if not hexStr or hexStr == "" then
        return "0000"  -- CRC8 00 + 空数据 00
    end

    -- 将十六进制字符串转换为字节数组
    local bytes = {}
    local len = string.len(hexStr)

    for i = 1, len do
        local char = string.sub(hexStr, i, i)
        table.insert(bytes, string.byte(char))
    end

    -- 计算CRC8
    local crc = U.CalculateCRC8(bytes)

    -- 返回: CRC8 + 原始数据
    return U.ToHex(crc, 2) .. hexStr
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

-- 计算 CRC32 校验值
-- @param str: 输入字符串
-- @return: CRC32 校验值 (0-4294967295)
function U.CalculateCRC32(str)
    if not str or str == "" then
        return 0
    end

    -- CRC32 多项式: 0x04C11DB7 (IEEE 802.3)
    -- 使用简化的查表法提高性能
    local crc = 4294967295  -- 0xFFFFFFFF

    for i = 1, string.len(str) do
        local byte = string.byte(str, i)
        crc = bit.bxor(crc, byte)

        for j = 1, 8 do
            if bit.band(crc, 1) ~= 0 then
                crc = bit.bxor(bit.rshift(crc, 1), 3988292384)  -- 0xEDB88320
            else
                crc = bit.rshift(crc, 1)
            end
        end
    end

    -- 返回补码
    return bit.bxor(crc, 4294967295)
end

-- 编码名称为十六进制格式（支持分行）
-- 格式: [CRC8(2位)][字节1(2位)][字节2(2位)]...
-- 限制: 最多10个字符（30字节UTF-8）
-- @param name: 要编码的名称字符串
-- @return: 编码字符串, 可能包含换行符分隔为多行
function U.EncodeNameHex(name)
    if not name or name == "" then
        return "0000"  -- CRC8 00 + 空数据 00
    end

    -- 转换为字节数组
    local bytes = U.EncodeUTF8String(name)
    local len = table.getn(bytes)

    -- 限制最大30字节（约10个字符）
    if len > 30 then
        len = 30
    end

    -- 计算 CRC8
    local crc = U.CalculateCRC8(bytes)

    -- 构建十六进制字符串: CRC8 + 字节数据
    local result = U.ToHex(crc, 2)

    -- 添加所有字节
    for i = 1, len do
        result = result .. U.ToHex(bytes[i], 2)
    end

    -- 如果数据太长，分成多行，每行包含：CRC8 + 行号 + 数据片段
    local maxCharsPerLine = 16  -- 每行最多16个十六进制字符（不含CRC和行号）
    local dataLen = string.len(result) - 2  -- 总数据长度（不含CRC8）

    if dataLen <= maxCharsPerLine then
        -- 短名称，单行显示: CRC8 + 数据
        return result
    end

    -- 长名称，分多行显示
    local lines = {}
    local offset = 2  -- 跳过CRC8
    local lineNum = 0

    while offset <= string.len(result) do
        local remaining = string.len(result) - offset + 1
        local chunkSize = remaining
        if chunkSize > maxCharsPerLine then
            chunkSize = maxCharsPerLine
        end

        local chunk = string.sub(result, offset, offset + chunkSize - 1)
        -- 格式: CRC8(2位) + 行号(1位) + 数据片段
        local line = U.ToHex(crc, 2) .. lineNum .. chunk
        table.insert(lines, line)

        offset = offset + chunkSize
        lineNum = lineNum + 1
    end

    -- 返回多行，用 | 分隔
    return table.concat(lines, "|")
end
