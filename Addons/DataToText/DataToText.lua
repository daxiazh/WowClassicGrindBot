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

-- 注意：移除了 CollectAllFieldData() 函数，因为 GridEncoder 已被移除

-- Get all data in flat format with global CRC32
local function GetAllData()
    -- Player data
    local pHp = UnitHealth("player") or 0
    local pMaxHp = UnitHealthMax("player") or 1
    local pMana = UnitMana("player") or 0
    local pMaxMana = UnitManaMax("player") or 1
    local pLevel = UnitLevel("player") or 1
    local pXp = UnitXP and UnitXP("player") or 0
    local pMaxXp = UnitXPMax and UnitXPMax("player") or 1
    local pGuid = GetUnitGUID("player")

    -- Target data
    local tExists = UnitExists("target")
    local tName = tExists and (UnitName("target") or "Unknown") or "None"
    local tHp = tExists and (UnitHealth("target") or 0) or 0
    local tMaxHp = tExists and (UnitHealthMax("target") or 1) or 0
    local tLevel = tExists and (UnitLevel("target") or 0) or 0
    local tDead = tExists and (UnitIsDead("target") and "1" or "0") or "0"
    local tGuid = tExists and GetUnitGUID("target") or "0x0000000000000000"

    -- 名称编码（简化，不分行）
    local tNameBytes = U.EncodeUTF8String(tName)
    local tNameHex = ""
    for i = 1, table.getn(tNameBytes) do
        tNameHex = tNameHex .. U.ToHex(tNameBytes[i], 2)
    end

    -- Bag data
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

    -- 构建所有数据字符串（用 | 分隔）
    local dataFields = {}
    table.insert(dataFields, "P_HP:" .. Hex(pHp) .. "/" .. Hex(pMaxHp))
    table.insert(dataFields, "P_MANA:" .. Hex(pMana) .. "/" .. Hex(pMaxMana))
    table.insert(dataFields, "P_LEVEL:" .. Hex(pLevel))
    table.insert(dataFields, "P_XP:" .. Hex(pXp) .. "/" .. Hex(pMaxXp))
    table.insert(dataFields, "P_GUID:" .. pGuid)
    table.insert(dataFields, "T_NAME:" .. tNameHex)
    table.insert(dataFields, "T_HP:" .. Hex(tHp) .. "/" .. Hex(tMaxHp))
    table.insert(dataFields, "T_LEVEL:" .. Hex(tLevel))
    table.insert(dataFields, "T_DEAD:" .. tDead)
    table.insert(dataFields, "T_GUID:" .. tGuid)
    table.insert(dataFields, "BAG_USED:" .. Hex(usedSlots) .. "/" .. Hex(totalSlots))
    table.insert(dataFields, "BAG_FREE:" .. Hex(freeSlots))

    -- 合并所有数据
    local allData = table.concat(dataFields, "|")

    -- 计算 CRC32
    local crc32 = U.CalculateCRC32(allData)

    -- 固定行宽（每行60个字符）
    local lineWidth = 60
    local lines = {}

    -- 第一行: CRC32
    table.insert(lines, "CRC:" .. U.ToHex(crc32, 8))

    -- 分割数据为多行，每行添加前缀 D1, D2, D3...
    local offset = 1
    local lineNum = 1
    while offset <= string.len(allData) do
        local remaining = string.len(allData) - offset + 1
        local chunkSize = remaining
        if chunkSize > lineWidth then
            chunkSize = lineWidth
        end

        local chunk = string.sub(allData, offset, offset + chunkSize - 1)
        table.insert(lines, "D" .. lineNum .. ":" .. chunk)

        offset = offset + chunkSize
        lineNum = lineNum + 1
    end

    return table.concat(lines, "\n")
end

-- 删除了 InitializeGrid() 和 RenderGrid() - 已被 FontString 方案替代

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
    local LINE_SPACING = 8      -- 行间距（像素，与字符宽度相同形成正方形）
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

    -- 使用EditBox显示数据（文本格式）
    DataToText_DataEditBox:SetText(GetAllData())
    -- 设置为只读（禁用编辑）
    DataToText_DataEditBox:SetAutoFocus(false)
    DataToText_DataEditBox:ClearFocus()

    -- 注意：网格渲染已移除，使用 /dtt gridtext 手动触发 FontString 网格显示
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
        -- 测试 FontString 字符网格（性能优化方案）
        TestGridText()
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
        DataToText_Print("  /dtt - 切换显示")
        DataToText_Print("  /dtt test - 运行所有单元测试")
        DataToText_Print("  /dtt utf8 - 运行UTF-8编码测试")
        DataToText_Print("  /dtt crc32 - 运行CRC32校验测试")
        DataToText_Print("  /dtt grid 或 /dtt gridtext - 显示字符网格")
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
