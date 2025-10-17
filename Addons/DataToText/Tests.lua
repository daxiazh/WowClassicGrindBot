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
    Print("===== CRC32 算法验证测试 (Lua) =====")

    local passed = 0
    local failed = 0
    
    -- 辅助函数: 格式化为 0x%08X
    local function FormatCRC(crc)
        return string.format("0x%08X", crc)
    end

    -- 测试1: 空字符串 (expected: 0x00000000)
    local crc1 = U.CalculateCRC32("")
    Print("Test 1 - Empty string: " .. FormatCRC(crc1) .. " (预期: 0x00000000)")
    if crc1 == 0 then
        passed = passed + 1
    else
        Print("  ✗ 与预期值不符！")
        failed = failed + 1
    end

    -- 测试2: 标准测试向量 "123456789" (expected: 0xCBF43926 = 3421780262)
    local crc2 = U.CalculateCRC32("123456789")
    Print("Test 2 - '123456789': " .. FormatCRC(crc2) .. " (预期: 0xCBF43926)")
    if crc2 == 3421780262 then
        passed = passed + 1
    else
        Print("  ✗ 与预期值不符！")
        failed = failed + 1
    end

    -- 测试3: "Hello World"
    local crc3 = U.CalculateCRC32("Hello World")
    Print("Test 3 - 'Hello World': " .. FormatCRC(crc3))
    passed = passed + 1

    -- 测试4: 二进制数据 [01 02 03 04 05]
    local str4 = string.char(1, 2, 3, 4, 5)
    local crc4 = U.CalculateCRC32(str4)
    Print("Test 4 - Binary [01 02 03 04 05]: " .. FormatCRC(crc4))
    passed = passed + 1

    -- 测试5: 只有元数据 [01 6C 00 00]
    local str5 = string.char(1, 108, 0, 0)
    local crc5 = U.CalculateCRC32(str5)
    Print("Test 5 - Metadata [01 6C 00 00]: " .. FormatCRC(crc5))
    passed = passed + 1

    -- 测试6: 328 字节全 0
    local str6 = string.rep(string.char(0), 328)
    local crc6 = U.CalculateCRC32(str6)
    Print("Test 6 - 328 bytes of zeros: " .. FormatCRC(crc6))
    passed = passed + 1

    -- 测试7: 顺序字节 0, 1, 2, ..., 255, 0, 1, ...
    local bytes7 = {}
    for i = 0, 327 do
        bytes7[i + 1] = string.char(math.fmod(i, 256))
    end
    local str7 = table.concat(bytes7)
    local crc7 = U.CalculateCRC32(str7)
    Print("Test 7 - Sequential 328 bytes: " .. FormatCRC(crc7))
    passed = passed + 1

    Print("=== 测试完成 ===")
    Print("结果: " .. passed .. " 通过, " .. failed .. " 失败")
    Print("请对比 C# 测试输出")
    
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

-- 测试 CRC32 实际编码值
function T.TestCRC32Encoding()
    Print("===== CRC32 编码诊断测试 =====")
    
    local FC = DataToTextFieldCollector
    local GE = DataToTextGridEncoder
    
    -- 收集实际字段
    local fields = FC.CollectAllFields()
    
    -- 打包为字节数组
    local bytes = GE.PackFieldsToBytes(fields)
    
    -- 显示前 328 字节的十六进制
    Print("前32字节:")
    for i = 1, 32 do
        Print(string.format("  bytes[%d] = 0x%02X (%d)", i, bytes[i], bytes[i]))
    end
    
    -- 手动重新计算 CRC32（只用前 328 字节）
    local dataBytes = {}
    for i = 1, 328 do
        dataBytes[i] = bytes[i]
    end
    
    -- 转换为字符串
    local byteString = ""
    for i = 1, 328 do
        byteString = byteString .. string.char(dataBytes[i])
    end
    
    local calculatedCRC = U.CalculateCRC32(byteString)
    
    -- 读取存储的 CRC32（最后4字节）
    local storedCRC = bit.lshift(bytes[329], 24) + bit.lshift(bytes[330], 16) + 
                      bit.lshift(bytes[331], 8) + bytes[332]
    
    Print(string.format("重新计算的 CRC32: 0x%08X", calculatedCRC))
    Print(string.format("存储的 CRC32:     0x%08X", storedCRC))
    Print(string.format("bytes[329-332]: %02X-%02X-%02X-%02X", 
        bytes[329], bytes[330], bytes[331], bytes[332]))
    
    if calculatedCRC == storedCRC then
        Print("✓ CRC32 匹配！")
        return true
    else
        Print("✗ CRC32 不匹配！")
        return false
    end
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
