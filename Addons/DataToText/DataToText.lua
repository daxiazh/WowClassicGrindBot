----------------------------------------------------------------------------
--  DataToText - OCR-Friendly Game Data Display
--  Simplified version based on UnitXP_SP3_Addon structure
----------------------------------------------------------------------------

-- SavedVariables
DataToTextDB = nil
DataToTextIcon = nil

-- Tooltip text
local DATATOTEXTTOOLTIP = "DataToText - Click to toggle display"

-- Constants
local FONT_PATH = "Interface\\AddOns\\DataToText\\Fonts\\Tiny-Bold.ttf"
local FONT_SIZE = 12
local UPDATE_INTERVAL = 0.1  -- Update every 0.1 seconds

-- Update timer
local updateTimer = 0

-- Pause state
local isPaused = false

-- FontString 网格状态
local testGridTextStrings = {}  -- 存储 FontString 对象

-- 性能监控
local enableProfiling = false  -- 是否启用性能分析
local profileData = {}  -- 性能数据累积
local profileCount = 0  -- 采样次数

-- Print helper
local function DataToText_Print(msg)
    if not DEFAULT_CHAT_FRAME then
        return
    end
    DEFAULT_CHAT_FRAME:AddMessage("|cff00ff00[DataToText]|r " .. tostring(msg))
end

----------------------------------------------------------------------------
-- 引用外部模块
----------------------------------------------------------------------------

-- 工具函数（定义在 Utils.lua）
local U = DataToTextUtils

-- 测试函数（定义在 Tests.lua）
local T = DataToTextTests

-- 注意：GridEncoder.lua 已移除，使用 FontString 方案替代

-- 本地快捷方式
local GetUnitGUID = U.GetUnitGUID

-- 十六进制转换（不补0，自然宽度）
local function Hex(num)
    return string.format("%X", num or 0)
end

-- Get all data (使用字段定义系统，保留用于兼容性)
-- 注意：推荐直接使用 CollectBinaryData() + FormatDataAsText()
local function GetAllData()
    local fields = CollectBinaryData()
    return FormatDataAsText(fields)
end

-- 删除了 InitializeGrid() 和 RenderGrid() - 已被 FontString 方案替代

----------------------------------------------------------------------------
-- 性能分析工具
----------------------------------------------------------------------------

-- 重置性能数据
local function ResetProfileData()
    profileData = {
        CollectBinaryData = 0,
        FormatDataAsText = 0,
        EncodeDataToBytes = 0,
        EncodeDataToGrid = 0,
        RenderDataGrid = 0,
        Total = 0
    }
    profileCount = 0
end

-- 显示性能报告
local function ShowProfileReport()
    if profileCount == 0 then
        DataToText_Print("性能分析：没有数据")
        return
    end

    DataToText_Print("=== 性能分析报告 (采样次数: " .. profileCount .. ") ===")

    local avgTotal = profileData.Total / profileCount * 1000
    local avgCollect = profileData.CollectBinaryData / profileCount * 1000
    local avgFormat = profileData.FormatDataAsText / profileCount * 1000
    local avgEncode = profileData.EncodeDataToBytes / profileCount * 1000
    local avgGrid = profileData.EncodeDataToGrid / profileCount * 1000
    local avgRender = profileData.RenderDataGrid / profileCount * 1000

    DataToText_Print(string.format("总耗时: %.2f ms", avgTotal))
    DataToText_Print(string.format("  CollectBinaryData: %.2f ms (%.1f%%)", avgCollect, avgCollect/avgTotal*100))
    DataToText_Print(string.format("  FormatDataAsText: %.2f ms (%.1f%%)", avgFormat, avgFormat/avgTotal*100))
    DataToText_Print(string.format("  EncodeDataToBytes: %.2f ms (%.1f%%)", avgEncode, avgEncode/avgTotal*100))
    DataToText_Print(string.format("  EncodeDataToGrid: %.2f ms (%.1f%%)", avgGrid, avgGrid/avgTotal*100))
    DataToText_Print(string.format("  RenderDataGrid: %.2f ms (%.1f%%)", avgRender, avgRender/avgTotal*100))
    DataToText_Print(string.format("预计帧率影响: %.1f FPS", 1 / avgTotal * 1000))
end

----------------------------------------------------------------------------
-- 基于字段定义的数据编码系统
----------------------------------------------------------------------------

-- 辅助函数：获取背包信息
local function GetBagInfo()
    local totalSlots = 0
    local freeSlots = 0
    for bag = 0, 4 do
        local slots = GetContainerNumSlots(bag) or 0
        totalSlots = totalSlots + slots
        for slot = 1, slots do
            if not GetContainerItemLink(bag, slot) then
                freeSlots = freeSlots + 1
            end
        end
    end
    local usedSlots = totalSlots - freeSlots
    return usedSlots, totalSlots
end

-- 字节编码辅助函数
local function AppendUInt8(bytes, value)
    value = value or 0
    table.insert(bytes, bit.band(value, 255))
end

local function AppendUInt16(bytes, value)
    value = value or 0
    -- Big-endian (高字节在前)
    table.insert(bytes, bit.band(bit.rshift(value, 8), 255))
    table.insert(bytes, bit.band(value, 255))
end

local function AppendUInt32(bytes, value)
    value = value or 0
    table.insert(bytes, bit.band(bit.rshift(value, 24), 255))
    table.insert(bytes, bit.band(bit.rshift(value, 16), 255))
    table.insert(bytes, bit.band(bit.rshift(value, 8), 255))
    table.insert(bytes, bit.band(value, 255))
end

-- 收集游戏数据（字段定义格式）
local function CollectBinaryData()
    local fields = {}

    -- 格式: {index, type, value, name}
    -- index: 字段序号
    -- type: 数据类型 ("uint8", "uint16", "uint32")
    -- value: 实际值
    -- name: 字段名（用于文本显示）

    -- 玩家基础属性
    table.insert(fields, {1, "uint8", UnitLevel("player"), "P_LEVEL"})
    table.insert(fields, {2, "uint16", UnitHealth("player"), "P_HP"})
    table.insert(fields, {3, "uint16", UnitHealthMax("player"), "P_MAXHP"})
    table.insert(fields, {4, "uint16", UnitMana("player"), "P_MANA"})
    table.insert(fields, {5, "uint16", UnitManaMax("player"), "P_MAXMANA"})
    table.insert(fields, {6, "uint16", UnitXP and UnitXP("player") or 0, "P_XP"})
    table.insert(fields, {7, "uint16", UnitXPMax and UnitXPMax("player") or 0, "P_MAXXP"})

    -- 目标信息
    table.insert(fields, {10, "uint16", UnitHealth("target") or 0, "T_HP"})
    table.insert(fields, {11, "uint16", UnitHealthMax("target") or 0, "T_MAXHP"})
    table.insert(fields, {12, "uint8", UnitLevel("target") or 0, "T_LEVEL"})
    table.insert(fields, {13, "uint8", (UnitExists("target") and UnitIsDead("target")) and 1 or 0, "T_DEAD"})

    -- 背包信息
    local usedSlots, totalSlots = GetBagInfo()
    table.insert(fields, {20, "uint8", usedSlots, "BAG_USED"})
    table.insert(fields, {21, "uint8", totalSlots, "BAG_TOTAL"})

    return fields
end

-- 将字段编码为字节数组
local function EncodeDataToBytes(fields)
    local bytes = {}

    -- 按序号排序
    table.sort(fields, function(a, b) return a[1] < b[1] end)

    -- 遍历所有字段，根据类型编码
    for i = 1, table.getn(fields) do
        local field = fields[i]
        local fieldType = field[2]
        local value = field[3]

        if fieldType == "uint8" then
            AppendUInt8(bytes, value)
        elseif fieldType == "uint16" then
            AppendUInt16(bytes, value)
        elseif fieldType == "uint32" then
            AppendUInt32(bytes, value)
        end
    end

    return bytes
end

-- 将字段格式化为可读文本
local function FormatDataAsText(fields)
    local lines = {}

    -- 按序号排序
    table.sort(fields, function(a, b) return a[1] < b[1] end)

    -- 遍历所有字段，格式化为文本
    for i = 1, table.getn(fields) do
        local field = fields[i]
        local index = field[1]
        local name = field[4]
        local value = field[3]

        table.insert(lines, string.format("[%d] %s: %s", index, name, tostring(value)))
    end

    return table.concat(lines, "\n")
end

-- 检查是否在角标记区域
local function InCorner(row, col)
    -- 左上角 3×3
    if row <= 3 and col <= 3 then
        return true
    end
    -- 右上角 3×3
    if row <= 3 and col >= 30 then
        return true
    end
    -- 左下角 3×3
    if row >= 30 and col <= 3 then
        return true
    end
    -- 右下角 3×3
    if row >= 30 and col >= 30 then
        return true
    end
    return false
end

-- 添加4个3×3角标记到网格
local function AddCornerMarkers(grid)
    -- 左上角
    for row = 1, 3 do
        for col = 1, 3 do
            if not (row == 2 and col == 2) then
                grid[row][col] = 1  -- 黑色边框
            end
        end
    end

    -- 右上角
    for row = 1, 3 do
        for col = 30, 32 do
            if not (row == 2 and col == 31) then
                grid[row][col] = 1
            end
        end
    end

    -- 左下角
    for row = 30, 32 do
        for col = 1, 3 do
            if not (row == 31 and col == 2) then
                grid[row][col] = 1
            end
        end
    end

    -- 右下角
    for row = 30, 32 do
        for col = 30, 32 do
            if not (row == 31 and col == 31) then
                grid[row][col] = 1
            end
        end
    end
end

-- 将字节数组编码为32×32网格
local function EncodeDataToGrid(bytes)
    local grid = {}

    -- 初始化32×32网格为0（白色）
    for row = 1, 32 do
        grid[row] = {}
        for col = 1, 32 do
            grid[row][col] = 0
        end
    end

    -- 添加4个3×3角标记
    AddCornerMarkers(grid)

    -- 字节转位，填充到网格（跳过角标记区域）
    local bitIndex = 1
    for row = 1, 32 do
        for col = 1, 32 do
            if not InCorner(row, col) then
                local byteIndex = math.floor((bitIndex - 1) / 8) + 1
                if byteIndex <= table.getn(bytes) then
                    local byte = bytes[byteIndex]
                    local bitPos = math.mod(bitIndex - 1, 8)
                    local bitValue = bit.band(bit.rshift(byte, 7 - bitPos), 1)
                    grid[row][col] = bitValue
                    bitIndex = bitIndex + 1
                end
            end
        end
    end

    return grid
end

-- 渲染网格为FontString（优化版：重用对象）
local function RenderDataGrid(grid)
    local gridFrame = DataToText_GridFrame
    if not gridFrame then
        return
    end

    -- 确保 GridFrame 可见
    gridFrame:Show()

    -- 渲染参数
    local CHAR_BLOCK = "█"
    local FONT_SIZE_GRID = 8
    local LINE_SPACING = 8.5
    local COLOR_WHITE = "|cFFFFFFFF"  -- 白色（数据位1）
    local COLOR_BLACK = "|cFF000000"  -- 黑色（数据位0）
    local COLOR_RESET = "|r"

    -- 如果 FontString 对象尚未创建，则创建它们（只创建一次）
    if table.getn(testGridTextStrings) == 0 then
        for row = 1, 32 do
            local fontString = gridFrame:CreateFontString(nil, "OVERLAY")
            fontString:SetFont(FONT_PATH, FONT_SIZE_GRID, "MONOCHROME")
            fontString:SetJustifyH("LEFT")
            fontString:SetJustifyV("TOP")
            fontString:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", 5, -5 - (row - 1) * LINE_SPACING)
            table.insert(testGridTextStrings, fontString)
        end
    end

    -- 更新每行的文本内容（重用现有对象 + 优化字符串拼接）
    local rowChars = {}  -- 重用table减少内存分配
    for row = 1, 32 do
        -- 清空table
        for i = 1, table.getn(rowChars) do
            rowChars[i] = nil
        end

        -- 使用table存储字符，避免字符串重复拼接
        local idx = 1
        for col = 1, 32 do
            -- 为每个字符添加颜色代码（1=白色，0=黑色）
            if grid[row][col] == 1 then
                rowChars[idx] = COLOR_WHITE
                rowChars[idx + 1] = CHAR_BLOCK
                rowChars[idx + 2] = COLOR_RESET
            else
                rowChars[idx] = COLOR_BLACK
                rowChars[idx + 1] = CHAR_BLOCK
                rowChars[idx + 2] = COLOR_RESET
            end
            idx = idx + 3
        end

        -- 一次性拼接字符串
        testGridTextStrings[row]:SetText(table.concat(rowChars))
        testGridTextStrings[row]:Show()
    end
end

----------------------------------------------------------------------------
-- FontString 字符网格测试（性能优化方案）
----------------------------------------------------------------------------

-- 使用 FontString + 特殊字符显示黑白网格，减少对象数量
local function TestGridText()
    local gridFrame = DataToText_GridFrame
    if not gridFrame then
        DataToText_Print("错误: GridFrame 未找到")
        return
    end

    -- 确保 GridFrame 可见
    gridFrame:Show()

    -- 清理旧的 FontString 对象
    for i = 1, table.getn(testGridTextStrings) do
        if testGridTextStrings[i] then
            testGridTextStrings[i]:Hide()
        end
    end
    testGridTextStrings = {}

    -- 测试参数
    local TEST_GRID_SIZE = 32  -- 32×32 测试网格
    local CHAR_BLOCK = "█"      -- U+2588 全方块（黑白都用这个字符，通过颜色区分）
    local FONT_SIZE_GRID = 8    -- 字体大小（像素）
    local LINE_SPACING = 8.5    -- 行间距（像素，与字符宽度相同形成正方形）
    local CHAR_SPACING = 0      -- 字符间距（0表示紧密排列）

    -- WoW 颜色代码
    local COLOR_WHITE = "|cFFFFFFFF"  -- 白色（数据位1）
    local COLOR_BLACK = "|cFF000000"  -- 黑色（数据位0）
    local COLOR_RESET = "|r"          -- 重置颜色

    -- 创建测试图案（带3×3角标记 + 对角线）
    local testGrid = {}
    for row = 1, TEST_GRID_SIZE do
        testGrid[row] = {}
        for col = 1, TEST_GRID_SIZE do
            -- 默认白色
            testGrid[row][col] = 0

            -- 四个角的3×3定位标记（黑色边框，中心白色）
            local inCorner = false

            -- 左上角
            if row <= 3 and col <= 3 then
                if not (row == 2 and col == 2) then
                    testGrid[row][col] = 1  -- 黑色
                end
                inCorner = true
            end

            -- 右上角
            if row <= 3 and col > TEST_GRID_SIZE - 3 then
                if not (row == 2 and col == TEST_GRID_SIZE - 1) then
                    testGrid[row][col] = 1  -- 黑色
                end
                inCorner = true
            end

            -- 左下角
            if row > TEST_GRID_SIZE - 3 and col <= 3 then
                if not (row == TEST_GRID_SIZE - 1 and col == 2) then
                    testGrid[row][col] = 1  -- 黑色
                end
                inCorner = true
            end

            -- 右下角
            if row > TEST_GRID_SIZE - 3 and col > TEST_GRID_SIZE - 3 then
                if not (row == TEST_GRID_SIZE - 1 and col == TEST_GRID_SIZE - 1) then
                    testGrid[row][col] = 1  -- 黑色
                end
                inCorner = true
            end

            -- 中间区域: 对角线测试图案
            if not inCorner then
                if row == col and row > 5 and row < TEST_GRID_SIZE - 5 then
                    testGrid[row][col] = 1  -- 对角线
                end
            end
        end
    end

    -- 逐行生成 FontString，使用颜色代码控制每个字符的颜色
    DataToText_Print("开始生成 FontString 网格（使用颜色代码）...")

    for row = 1, TEST_GRID_SIZE do
        local rowText = ""
        for col = 1, TEST_GRID_SIZE do
            -- 为每个字符添加颜色代码
            if testGrid[row][col] == 1 then
                rowText = rowText .. COLOR_WHITE .. CHAR_BLOCK .. COLOR_RESET
            else
                rowText = rowText .. COLOR_BLACK .. CHAR_BLOCK .. COLOR_RESET
            end
        end

        -- 创建 FontString
        local fontString = gridFrame:CreateFontString(nil, "OVERLAY")
        fontString:SetFont(FONT_PATH, FONT_SIZE_GRID, "MONOCHROME")
        fontString:SetText(rowText)
        fontString:SetJustifyH("LEFT")
        fontString:SetJustifyV("TOP")
        fontString:SetSpacing(CHAR_SPACING)  -- 字符间距，0=紧密排列
        fontString:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", 5, -5 - (row - 1) * LINE_SPACING)
        fontString:Show()

        table.insert(testGridTextStrings, fontString)
    end

    DataToText_Print("✅ FontString 网格已生成: " .. TEST_GRID_SIZE .. "x" .. TEST_GRID_SIZE)
    DataToText_Print("📊 对象数量: " .. table.getn(testGridTextStrings) .. " 个 FontString")
    DataToText_Print("🎯 显示在左侧黑色区域")
    DataToText_Print("💡 提示: 使用 CoreTests 或截图工具进行识别测试")
end

-- Update display
local function UpdateDisplay()
    if not DataToTextFrame or not DataToTextFrame:IsVisible() then
        return
    end

    -- 如果暂停，则不更新
    if isPaused then
        return
    end

    local t0, t1, t2, t3, t4, t5
    if enableProfiling then
        t0 = GetTime()
    end

    -- 收集字段数据（单一数据源）
    local fields = CollectBinaryData()

    if enableProfiling then
        t1 = GetTime()
        profileData.CollectBinaryData = profileData.CollectBinaryData + (t1 - t0)
    end

    -- 更新EditBox（文本显示）
    DataToText_DataEditBox:SetText(FormatDataAsText(fields))
    DataToText_DataEditBox:SetAutoFocus(false)
    DataToText_DataEditBox:ClearFocus()

    if enableProfiling then
        t2 = GetTime()
        profileData.FormatDataAsText = profileData.FormatDataAsText + (t2 - t1)
    end

    -- 更新二维码（二进制网格显示）
    local bytes = EncodeDataToBytes(fields)

    if enableProfiling then
        t3 = GetTime()
        profileData.EncodeDataToBytes = profileData.EncodeDataToBytes + (t3 - t2)
    end

    local grid = EncodeDataToGrid(bytes)

    if enableProfiling then
        t4 = GetTime()
        profileData.EncodeDataToGrid = profileData.EncodeDataToGrid + (t4 - t3)
    end

    RenderDataGrid(grid)

    if enableProfiling then
        t5 = GetTime()
        profileData.RenderDataGrid = profileData.RenderDataGrid + (t5 - t4)
        profileData.Total = profileData.Total + (t5 - t0)
        profileCount = profileCount + 1
    end
end

-- Slash commands
SLASH_DATATOTEXT1 = "/dtt"
SLASH_DATATOTEXT2 = "/datatotext"
SlashCmdList["DATATOTEXT"] = function(msg)
    -- 去除首尾空格
    msg = U.Trim(msg or "")

    if msg == "test" then
        -- 运行所有测试
        T.RunAllTests()
    elseif msg == "testutf8" or msg == "utf8" then
        -- 运行UTF-8编码测试
        T.TestUTF8Encoding()
    elseif msg == "testcrc32" or msg == "crc32" then
        -- 运行CRC32测试
        T.TestCRC32()
    elseif msg == "testgridtext" or msg == "gridtext" or msg == "grid" then
        -- 测试 FontString 字符网格（显示测试图案：角标记+对角线）
        TestGridText()
    elseif msg == "profile" or msg == "perf" then
        -- 切换性能分析
        enableProfiling = not enableProfiling
        if enableProfiling then
            ResetProfileData()
            DataToText_Print("✅ 性能分析已启用 - 10秒后运行 /dtt report 查看报告")
        else
            DataToText_Print("❌ 性能分析已禁用")
        end
    elseif msg == "report" then
        -- 显示性能报告
        ShowProfileReport()
    elseif msg == "" then
        -- 切换显示
        if DataToTextFrame:IsShown() then
            PlaySound("igMainMenuContinue")
            DataToTextFrame:Hide()
        else
            PlaySound("igMainMenuOpen")
            DataToTextFrame:Show()
        end
    else
        -- 显示帮助
        DataToText_Print("可用命令:")
        DataToText_Print("  /dtt - 切换显示（自动刷新文本和二维码）")
        DataToText_Print("  /dtt profile - 启用/禁用性能分析")
        DataToText_Print("  /dtt report - 显示性能分析报告")
        DataToText_Print("  /dtt test - 运行所有单元测试")
        DataToText_Print("  /dtt utf8 - 运行UTF-8编码测试")
        DataToText_Print("  /dtt crc32 - 运行CRC32校验测试")
        DataToText_Print("  /dtt grid - 显示测试图案（角标记+对角线）")
    end
end

DataToText_Print("DataToText loaded!")

-- OnLoad function
function DataToText_OnLoad()
    -- Set custom font for EditBox (黑底白字不需要OUTLINE)
    DataToText_DataEditBox:SetFont(FONT_PATH, FONT_SIZE, "MONOCHROME")
    DataToText_DataEditBox:SetTextColor(1, 1, 1)  -- 白色文字

    -- 设置EditBox为多行模式，禁用自动聚焦
    DataToText_DataEditBox:SetMultiLine(true)
    DataToText_DataEditBox:SetAutoFocus(false)
    DataToText_DataEditBox:EnableMouse(true)  -- 允许鼠标选择文本

    -- 注意：移除了 InitializeGrid() 调用，使用 FontString 方案替代

    DataToTextFrame:RegisterEvent("ADDON_LOADED")
end

-- OnUpdate handler
function DataToText_OnUpdate(elapsed)
    updateTimer = updateTimer + elapsed
    if updateTimer >= UPDATE_INTERVAL then
        updateTimer = 0
        UpdateDisplay()
    end
end

-- OnEvent handler
function DataToText_OnEvent(event)
    if event == "ADDON_LOADED" and arg1 == "DataToText" then
        -- Initialize SavedVariables
        if DataToTextDB == nil then
            DataToTextDB = {
                version = 1,
                enabled = true
            }
        end

        if DataToTextIcon == nil then
            DataToTextIcon = {
                hide = false
            }
        end

        DataToTextFrame:UnregisterEvent("ADDON_LOADED")
        DataToTextFrame:RegisterEvent("PLAYER_LOGIN")
        return

    elseif event == "PLAYER_LOGIN" then
        DataToText_Print("Initializing...")

        -- Initialize minimap button
        local libIcon = LibStub("LibDBIcon-1.0")
        local libData = LibStub("LibDataBroker-1.1")

        local iconData = libData:NewDataObject("DataToText icon data", {
            OnClick = function()
                if DataToTextFrame:IsShown() then
                    PlaySound("igMainMenuContinue")
                    DataToTextFrame:Hide()
                else
                    PlaySound("igMainMenuOpen")
                    DataToTextFrame:Show()
                end
            end,
            OnTooltipShow = function(tooltip)
                tooltip:SetText(DATATOTEXTTOOLTIP)
            end,
            icon = "Interface\\Icons\\INV_Misc_Note_01"
        })

        libIcon:Register("DataToText icon", iconData, DataToTextIcon)

        DataToText_Print("Minimap button created. Use /dtt to toggle display.")
        return
    end
end

-- UI button click handler
function DataToText_UI_OnClick(widget)
    if widget == nil or string.find(widget:GetName(), "DataToText") == nil then
        return
    end

    if string.find(widget:GetName(), "_buttonClose") then
        PlaySound("gsTitleOptionExit")
        DataToTextFrame:Hide()
    elseif string.find(widget:GetName(), "_buttonPause") then
        -- 切换暂停状态
        isPaused = not isPaused
        if isPaused then
            widget:SetText("Resume")
            PlaySound("igMainMenuOptionCheckBoxOn")
            DataToText_Print("数据刷新已暂停 - 现在可以选择和复制数据")
        else
            widget:SetText("Pause")
            PlaySound("igMainMenuOptionCheckBoxOff")
            DataToText_Print("数据刷新已恢复")
        end
    end
end
