----------------------------------------------------------------------------
--  DataToText - 测试函数
--  包含所有单元测试和自动化测试
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextTests = DataToTextTests or {}
local T = DataToTextTests

-- 引用工具函数
local U = DataToTextUtils

-- 打印辅助函数
local function Print(msg)
    if not DEFAULT_CHAT_FRAME then
        return
    end
    DEFAULT_CHAT_FRAME:AddMessage("|cff00ff00[DataToText-Test]|r " .. tostring(msg))
end

----------------------------------------------------------------------------
-- 工具函数测试
----------------------------------------------------------------------------

-- 测试十六进制转换函数
function T.TestHexConversion()
    local tests = {
        {name = "ToHex(255, 2)", func = function() return U.ToHex(255, 2) end, expected = "FF"},
        {name = "ToHex(16, 4)", func = function() return U.ToHex(16, 4) end, expected = "0010"},
        {name = "ToHex(0, 2)", func = function() return U.ToHex(0, 2) end, expected = "00"},
        {name = "ToHex(4095, 4)", func = function() return U.ToHex(4095, 4) end, expected = "0FFF"},
        {name = "FromHex('FF')", func = function() return U.FromHex("FF") end, expected = 255},
        {name = "FromHex('0010')", func = function() return U.FromHex("0010") end, expected = 16},
        {name = "FromHex('00')", func = function() return U.FromHex("00") end, expected = 0},
        {name = "FromHex('0FFF')", func = function() return U.FromHex("0FFF") end, expected = 4095},
    }

    local passed = 0
    local failed = 0

    Print("===== 十六进制转换测试 =====")

    for _, test in ipairs(tests) do
        local result = test.func()
        if result == test.expected then
            passed = passed + 1
            Print("✓ " .. test.name .. " = " .. tostring(result))
        else
            failed = failed + 1
            Print("✗ " .. test.name .. " | 期望: " .. tostring(test.expected) .. ", 实际: " .. tostring(result))
        end
    end

    Print("结果: " .. passed .. " 通过, " .. failed .. " 失败")
    return failed == 0
end

-- 测试字符串处理函数
function T.TestStringUtils()
    local tests = {
        {name = "Trim(' test ')", func = function() return U.Trim(" test ") end, expected = "test"},
        {name = "Trim('test')", func = function() return U.Trim("test") end, expected = "test"},
        {name = "Trim('  ')", func = function() return U.Trim("  ") end, expected = ""},
    }

    local passed = 0
    local failed = 0

    Print("===== 字符串处理测试 =====")

    for _, test in ipairs(tests) do
        local result = test.func()
        if result == test.expected then
            passed = passed + 1
            Print("✓ " .. test.name .. " = '" .. tostring(result) .. "'")
        else
            failed = failed + 1
            Print("✗ " .. test.name .. " | 期望: '" .. tostring(test.expected) .. "', 实际: '" .. tostring(result) .. "'")
        end
    end

    Print("结果: " .. passed .. " 通过, " .. failed .. " 失败")
    return failed == 0
end

----------------------------------------------------------------------------
-- 环境检测测试
----------------------------------------------------------------------------

-- 检测位运算库的可用性
function T.TestBitLibrary()
    Print("===== 位运算库检测 =====")

    -- 检测 bit 库
    if bit then
        Print("✓ 全局 'bit' 库可用")

        -- 测试各个函数
        local functions = {
            "band", "bor", "bxor", "bnot",
            "lshift", "rshift", "arshift",
            "rol", "ror", "bswap"
        }

        for _, funcName in ipairs(functions) do
            if bit[funcName] then
                Print("  ✓ bit." .. funcName .. " 可用")
            else
                Print("  ✗ bit." .. funcName .. " 不可用")
            end
        end

        -- 测试基本功能
        local testPassed = true
        local ok, result

        ok, result = pcall(function() return bit.band(15, 7) end)
        if ok and result == 7 then
            Print("  ✓ bit.band(15, 7) = " .. result .. " (正确)")
        else
            Print("  ✗ bit.band 测试失败")
            testPassed = false
        end

        ok, result = pcall(function() return bit.bxor(15, 7) end)
        if ok and result == 8 then
            Print("  ✓ bit.bxor(15, 7) = " .. result .. " (正确)")
        else
            Print("  ✗ bit.bxor 测试失败")
            testPassed = false
        end

        ok, result = pcall(function() return bit.lshift(1, 4) end)
        if ok and result == 16 then
            Print("  ✓ bit.lshift(1, 4) = " .. result .. " (正确)")
        else
            Print("  ✗ bit.lshift 测试失败")
            testPassed = false
        end

        return testPassed
    else
        Print("✗ 全局 'bit' 库不可用")

        -- 检测 bit32 库 (Lua 5.2+)
        if bit32 then
            Print("✓ 'bit32' 库可用（Lua 5.2+）")
            return true
        else
            Print("✗ 'bit32' 库也不可用")
        end

        -- 检测取模操作符（使用math.fmod或math.mod）
        local hasModulo = false
        if math.fmod then
            local result = math.fmod(10, 3)
            if result == 1 then
                Print("✓ math.fmod 函数可用")
                hasModulo = true
            end
        elseif math.mod then
            local result = math.mod(10, 3)
            if result == 1 then
                Print("✓ math.mod 函数可用")
                hasModulo = true
            end
        end

        if not hasModulo then
            Print("✗ 取模函数不可用")
        end

        -- 检测 math.mod
        if math.mod then
            Print("✓ math.mod 函数可用")
        else
            Print("✗ math.mod 函数不可用")
        end

        Print("结论: 需要实现自定义位运算兼容层")
        return false
    end
end

----------------------------------------------------------------------------
-- 运行所有测试
----------------------------------------------------------------------------

function T.RunAllTests()
    Print("========================================")
    Print("开始运行 DataToText 单元测试")
    Print("========================================")

    local allPassed = true

    -- 首先检测环境
    Print("")
    T.TestBitLibrary()

    -- 运行各个测试套件
    Print("")
    if not T.TestHexConversion() then
        allPassed = false
    end

    Print("")

    if not T.TestStringUtils() then
        allPassed = false
    end

    Print("========================================")
    if allPassed then
        Print("✓ 所有测试通过！")
    else
        Print("✗ 部分测试失败，请检查上面的输出")
    end
    Print("========================================")

    return allPassed
end
