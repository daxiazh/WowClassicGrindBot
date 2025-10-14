----------------------------------------------------------------------------
--  DataToText - 测试函数
--  包含单元测试和环境检测
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
            "lshift", "rshift", "arshift"
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
        Print("结论: WoW 1.12 Vanilla 需要 bit 库支持")
        return false
    end
end

----------------------------------------------------------------------------
-- CRC32 测试
----------------------------------------------------------------------------

-- 测试 CRC32 校验功能
function T.TestCRC32()
    Print("===== CRC32 校验测试 =====")

    local passed = 0
    local failed = 0

    -- 测试1: 空字符串
    local result = U.CalculateCRC32("")
    if result == 0 then
        Print("✓ CalculateCRC32('') = 0")
        passed = passed + 1
    else
        Print("✗ CalculateCRC32('') | 期望: 0, 实际: " .. result)
        failed = failed + 1
    end

    -- 测试2: 简单字符串 "ABC"
    local crc1 = U.CalculateCRC32("ABC")
    Print("  CalculateCRC32('ABC') = " .. crc1)
    passed = passed + 1

    -- 测试3: 不同的字符串应该有不同的 CRC32
    local crc2 = U.CalculateCRC32("XYZ")
    if crc1 ~= crc2 then
        Print("✓ CalculateCRC32('ABC') != CalculateCRC32('XYZ')")
        passed = passed + 1
    else
        Print("✗ 不同字符串产生了相同的 CRC32")
        failed = failed + 1
    end

    -- 测试4: 相同字符串应该产生相同的 CRC32
    local crc3 = U.CalculateCRC32("ABC")
    if crc1 == crc3 then
        Print("✓ 相同字符串产生相同 CRC32")
        passed = passed + 1
    else
        Print("✗ 相同字符串产生了不同的 CRC32")
        failed = failed + 1
    end

    -- 测试5: 长字符串
    local longStr = string.rep("A", 328)  -- 328字节数据（与GridEncoder大小相同）
    local crc4 = U.CalculateCRC32(longStr)
    Print("  CalculateCRC32(328字节) = " .. crc4)
    passed = passed + 1

    Print("结果: " .. passed .. " 通过, " .. failed .. " 失败")
    return failed == 0
end

----------------------------------------------------------------------------
-- 数据收集测试
----------------------------------------------------------------------------

-- 测试字段收集器
function T.TestFieldCollector()
    Print("===== 字段收集器测试 =====")

    local FC = DataToTextFieldCollector
    if not FC then
        Print("✗ FieldCollector 模块未加载")
        return false
    end

    local passed = 0
    local failed = 0

    -- 测试1: 收集所有字段
    local fields = FC.CollectAllFields()
    if fields then
        Print("✓ CollectAllFields() 成功")
        passed = passed + 1
    else
        Print("✗ CollectAllFields() 失败")
        failed = failed + 1
        return false
    end

    -- 测试2: 检查字段数量
    local fieldCount = FC.GetFieldCount()
    if fieldCount == 108 then
        Print("✓ GetFieldCount() = 108")
        passed = passed + 1
    else
        Print("✗ GetFieldCount() | 期望: 108, 实际: " .. fieldCount)
        failed = failed + 1
    end

    -- 测试3: 检查字段值范围
    local outOfRange = 0
    for i = 0, 107 do
        local value = fields[i] or 0
        if value < 0 or value > 16777215 then
            outOfRange = outOfRange + 1
        end
    end

    if outOfRange == 0 then
        Print("✓ 所有字段值在 24-bit 范围内 (0-16777215)")
        passed = passed + 1
    else
        Print("✗ " .. outOfRange .. " 个字段值超出范围")
        failed = failed + 1
    end

    -- 测试4: 显示部分字段值（调试）
    Print("  部分字段值示例:")
    Print("    Field[0] (InitFlag): " .. (fields[0] or 0))
    Print("    Field[5] (PlayerLevel): " .. (fields[5] or 0))
    Print("    Field[11] (PlayerHP): " .. (fields[11] or 0))
    Print("    Field[106] (GlobalTime): " .. (fields[106] or 0))

    Print("结果: " .. passed .. " 通过, " .. failed .. " 失败")
    return failed == 0
end

-- 测试网格编码器
function T.TestGridEncoder()
    Print("===== 网格编码器测试 =====")

    local GE = DataToTextGridEncoder
    if not GE then
        Print("✗ GridEncoder 模块未加载")
        return false
    end

    local passed = 0
    local failed = 0

    -- 测试1: 获取容量信息
    local info = GE.GetCapacityInfo()
    if info then
        Print("✓ GetCapacityInfo() 成功")
        Print("  网格尺寸: " .. info.gridSize .. "x" .. info.gridSize)
        Print("  总容量: " .. info.capacityBits .. " bits (" .. info.capacityBytes .. " bytes)")
        Print("  已使用: " .. info.usedBits .. " bits (" .. info.usedBytes .. " bytes)")
        Print("  使用率: " .. string.format("%.1f%%", info.utilizationPercent))
        passed = passed + 1
    else
        Print("✗ GetCapacityInfo() 失败")
        failed = failed + 1
    end

    -- 测试2: 编码测试字段
    local testFields = {}
    for i = 0, 107 do
        testFields[i] = i * 1000
    end

    local grid, stats = GE.EncodeToGrid(testFields)
    if grid and stats then
        Print("✓ EncodeToGrid() 成功")
        Print("  总字段: " .. stats.totalFields)
        Print("  总字节: " .. stats.totalBytes)
        Print("  总位数: " .. stats.totalBits)
        passed = passed + 1
    else
        Print("✗ EncodeToGrid() 失败")
        failed = failed + 1
    end

    Print("结果: " .. passed .. " 通过, " .. failed .. " 失败")
    return failed == 0
end

----------------------------------------------------------------------------
-- 运行所有测试
----------------------------------------------------------------------------

function T.RunAllTests()
    Print("========================================")
    Print("开始运行 DataToText v2.0 单元测试")
    Print("========================================")

    local allPassed = true

    -- 1. 环境检测
    Print("")
    if not T.TestBitLibrary() then
        allPassed = false
    end

    -- 2. 工具函数测试
    Print("")
    if not T.TestStringUtils() then
        allPassed = false
    end

    Print("")
    if not T.TestCRC32() then
        allPassed = false
    end

    -- 3. 模块测试
    Print("")
    if not T.TestFieldCollector() then
        allPassed = false
    end

    Print("")
    if not T.TestGridEncoder() then
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
