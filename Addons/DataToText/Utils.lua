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
