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

-- Grid state
local gridPixels = {}  -- 存储网格像素帧
local gridData = nil   -- 存储当前的网格数据

-- FontString 测试状态
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

-- 网格编码器（定义在 GridEncoder.lua）
local GridEncoder = DataToTextGridEncoder

-- 测试函数（定义在 Tests.lua）
local T = DataToTextTests

-- 本地快捷方式
local GetUnitGUID = U.GetUnitGUID

-- 十六进制转换（不补0，自然宽度）
local function Hex(num)
    return string.format("%X", num or 0)
end

-- 收集所有 108 个字段数据（用于黑白网格编码）
-- 返回 fieldData[0..107]，每个字段为 24 位整数
local function CollectAllFieldData()
    local fieldData = {}

    -- 初始化所有字段为 0
    for i = 0, 107 do
        fieldData[i] = 0
    end

    -- 基础信息 (0-9)
    fieldData[0] = 0  -- INIT_FLAG: 0 = 正常模式

    -- 玩家位置和朝向（暂时填充占位数据，后续需要实现）
    fieldData[1] = 0  -- P_X: 需要实现 GetPlayerMapPosition
    fieldData[2] = 0  -- P_Y: 需要实现 GetPlayerMapPosition
    fieldData[3] = 0  -- P_FACING: 需要实现 GetPlayerFacing

    -- 地图ID和等级
    local mapId = GetCurrentMapZone() or 0
    fieldData[4] = mapId  -- P_MAP
    fieldData[5] = UnitLevel("player") or 1  -- P_LEVEL

    -- 尸体位置（暂时占位）
    fieldData[6] = 0  -- CORPSE_X
    fieldData[7] = 0  -- CORPSE_Y

    -- 布尔标志位（暂时占位，后续需要打包多个布尔值）
    fieldData[8] = 0  -- BITS1
    fieldData[9] = 0  -- BITS2

    -- 玩家属性 (10-15)
    fieldData[10] = UnitHealthMax("player") or 0  -- P_HP_MAX
    fieldData[11] = UnitHealth("player") or 0     -- P_HP
    fieldData[12] = UnitManaMax("player") or 0    -- P_POWER_MAX
    fieldData[13] = UnitMana("player") or 0       -- P_POWER
    fieldData[14] = UnitManaMax("player") or 0    -- P_MANA_MAX (暂时重复)
    fieldData[15] = UnitMana("player") or 0       -- P_MANA (暂时重复)

    -- 目标信息 (16-19)
    local tExists = UnitExists("target")
    if tExists then
        local tName = UnitName("target") or ""
        -- 目标名称编码为两个 24 位字段（简化处理）
        local nameBytes = U.EncodeUTF8String(tName)
        local name1 = 0
        local name2 = 0
        if table.getn(nameBytes) >= 1 then name1 = bit.lshift(nameBytes[1], 16) end
        if table.getn(nameBytes) >= 2 then name1 = bit.bor(name1, bit.lshift(nameBytes[2], 8)) end
        if table.getn(nameBytes) >= 3 then name1 = bit.bor(name1, nameBytes[3]) end
        if table.getn(nameBytes) >= 4 then name2 = bit.lshift(nameBytes[4], 16) end
        if table.getn(nameBytes) >= 5 then name2 = bit.bor(name2, bit.lshift(nameBytes[5], 8)) end
        if table.getn(nameBytes) >= 6 then name2 = bit.bor(name2, nameBytes[6]) end

        fieldData[16] = name1  -- T_NAME_1
        fieldData[17] = name2  -- T_NAME_2
        fieldData[18] = UnitHealthMax("target") or 0  -- T_HP_MAX
        fieldData[19] = UnitHealth("target") or 0     -- T_HP
    end

    -- 背包系统 (20-24)
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

    -- BAG_INFO: 打包背包信息（简化：只存储使用/总数）
    fieldData[20] = bit.bor(bit.lshift(usedSlots, 12), totalSlots)  -- BAG_INFO
    fieldData[21] = 0  -- BAG_SLOT (队列系统，暂时占位)
    fieldData[22] = 0  -- BAG_ITEM_ID (队列系统，暂时占位)
    fieldData[23] = 0  -- EQUIP_SLOT (队列系统，暂时占位)
    fieldData[24] = 0  -- EQUIP_ITEM_ID (队列系统，暂时占位)

    -- 动作条状态 (25-37) - 暂时占位
    for i = 25, 37 do
        fieldData[i] = 0
    end

    -- 宠物信息 (38-39)
    if UnitExists("pet") then
        fieldData[38] = UnitHealthMax("pet") or 0  -- PET_HP_MAX
        fieldData[39] = UnitHealth("pet") or 0     -- PET_HP
    end

    -- 其余字段 (40-107) - 暂时占位
    -- 这些字段需要后续逐步实现，参考 DataToColor.lua

    -- 经验值 (50-51)
    if UnitXP then
        fieldData[50] = UnitXP("player") or 0      -- P_XP
        fieldData[51] = UnitXPMax("player") or 0   -- P_XP_MAX
    end

    -- 目标等级 (43)
    if tExists then
        local tLevel = UnitLevel("target") or 0
        fieldData[43] = tLevel  -- T_LEVEL_CLASS
    end

    -- 金钱 (44-45)
    local money = GetMoney() or 0
    fieldData[44] = math.mod(money, 1000000)      -- MONEY_COPPER (铜币部分)
    fieldData[45] = math.floor(money / 1000000)  -- MONEY_GOLD (金币部分)

    -- 全局时间戳 (106)
    fieldData[106] = math.mod(math.floor(GetTime() * 1000), 16777216)  -- GLOBAL_TIME (毫秒，取24位)

    -- 元数据/验证标志 (107)
    fieldData[107] = 65793  -- METADATA: 版本标识 (0x010101 = 65793)

    return fieldData
end

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

-- 初始化网格像素
local function InitializeGrid()
    local gridFrame = DataToText_GridFrame
    if not gridFrame then
        return
    end

    -- 清理旧的像素
    for i = 1, table.getn(gridPixels) do
        if gridPixels[i] then
            gridPixels[i]:Hide()
        end
    end
    gridPixels = {}

    -- 创建 65×65 的像素帧
    local GRID_SIZE = 65
    local pixelSize = 3  -- 每个网格单元 3×3 像素
    local totalSize = GRID_SIZE * pixelSize  -- 195×195 像素

    for row = 1, GRID_SIZE do
        for col = 1, GRID_SIZE do
            local index = (row - 1) * GRID_SIZE + col
            local pixel = gridFrame:CreateTexture(nil, "OVERLAY")
            pixel:SetWidth(pixelSize)
            pixel:SetHeight(pixelSize)
            pixel:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", (col - 1) * pixelSize, -(row - 1) * pixelSize)
            pixel:SetTexture(1, 1, 1, 1)  -- 默认白色
            gridPixels[index] = pixel
        end
    end

    DataToText_Print("网格初始化完成: " .. GRID_SIZE .. "x" .. GRID_SIZE .. " = " .. (GRID_SIZE * GRID_SIZE) .. " 像素")
end

-- 渲染网格
local function RenderGrid(grid)
    if not grid or table.getn(gridPixels) == 0 then
        return
    end

    local GRID_SIZE = 65
    for row = 1, GRID_SIZE do
        for col = 1, GRID_SIZE do
            local index = (row - 1) * GRID_SIZE + col
            local pixel = gridPixels[index]
            if pixel and grid[row] and grid[row][col] ~= nil then
                local value = grid[row][col]
                if value == 1 then
                    -- 黑色
                    pixel:SetTexture(0, 0, 0, 1)
                else
                    -- 白色
                    pixel:SetTexture(1, 1, 1, 1)
                end
            end
        end
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

    -- 隐藏所有 Texture 像素（旧方案）
    DataToText_Print("隐藏 Texture 网格（" .. table.getn(gridPixels) .. " 个对象）...")
    for i = 1, table.getn(gridPixels) do
        if gridPixels[i] then
            gridPixels[i]:Hide()
        end
    end

    -- 清理旧的 FontString 对象
    for i = 1, table.getn(testGridTextStrings) do
        if testGridTextStrings[i] then
            testGridTextStrings[i]:Hide()
        end
    end
    testGridTextStrings = {}

    -- 测试参数
    local TEST_GRID_SIZE = 32  -- 32×32 测试网格
    local CHAR_BLACK = "#"      -- 使用 # 代表黑色（更可靠）
    local CHAR_WHITE = "."      -- 使用 . 代表白色
    local FONT_SIZE_GRID = 10   -- 字体大小（像素，增大到10以便看清）

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

    -- 逐行生成 FontString（每行一个对象，减少对象数量）
    DataToText_Print("开始生成 FontString 网格...")

    for row = 1, TEST_GRID_SIZE do
        local rowText = ""
        for col = 1, TEST_GRID_SIZE do
            if testGrid[row][col] == 1 then
                rowText = rowText .. CHAR_BLACK
            else
                rowText = rowText .. CHAR_WHITE
            end
        end

        -- 创建 FontString
        local fontString = gridFrame:CreateFontString(nil, "OVERLAY")
        fontString:SetFont(FONT_PATH, FONT_SIZE_GRID, "OUTLINE")  -- 使用 OUTLINE 增加可见性
        fontString:SetText(rowText)
        fontString:SetTextColor(1, 1, 0)  -- 黄色文字（在深灰背景上更清晰）
        fontString:SetJustifyH("LEFT")
        fontString:SetJustifyV("TOP")
        fontString:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", 5, -5 - (row - 1) * FONT_SIZE_GRID)  -- 添加5像素边距
        fontString:Show()  -- 确保显示

        table.insert(testGridTextStrings, fontString)
    end

    -- 调试：显示第一个 FontString 的信息
    if table.getn(testGridTextStrings) > 0 then
        local first = testGridTextStrings[1]
        DataToText_Print("调试: 第一个 FontString 文本 = " .. (first:GetText() or "无"))
        DataToText_Print("调试: 第一个 FontString 可见 = " .. tostring(first:IsVisible()))
    end

    DataToText_Print("FontString 测试网格已生成: " .. TEST_GRID_SIZE .. "x" .. TEST_GRID_SIZE)
    DataToText_Print("对象数量: " .. table.getn(testGridTextStrings) .. " (vs 原方案 4225)")
    DataToText_Print("请截图并使用 CoreTests 中的解码器分析")
    DataToText_Print("或使用 Node.js 解码器: node grid_text_decoder.js <截图路径>")
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

    -- 生成并渲染黑白网格
    local fieldData = CollectAllFieldData()
    gridData = GridEncoder:EncodeToGrid(fieldData)
    RenderGrid(gridData)
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
    elseif msg == "testgrid" or msg == "grid" then
        -- 测试网格编码
        DataToText_Print("测试网格编码器...")
        local testGrid = GridEncoder:Test()
        if testGrid then
            DataToText_Print("网格编码测试完成！")
        end
    elseif msg == "testgridtext" or msg == "gridtext" then
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
        DataToText_Print("  /dtt grid - 测试网格编码器 (Texture方案)")
        DataToText_Print("  /dtt gridtext - 测试字符网格 (FontString方案)")
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

    -- 初始化网格
    InitializeGrid()

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
