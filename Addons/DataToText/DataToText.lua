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

-- 本地快捷方式
local GetUnitGUID = U.GetUnitGUID

-- 十六进制转换（不补0，自然宽度）
local function Hex(num)
    return string.format("%X", num or 0)
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

-- Update display
local function UpdateDisplay()
    if not DataToTextFrame or not DataToTextFrame:IsVisible() then
        return
    end

    DataToText_PlayerInfo:SetText(GetAllData())
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
    end
end

DataToText_Print("DataToText loaded!")

-- OnLoad function
function DataToText_OnLoad()
    -- Set custom font (黑底白字不需要OUTLINE)
    DataToText_PlayerInfo:SetFont(FONT_PATH, FONT_SIZE, "MONOCHROME")
    DataToText_TargetInfo:SetFont(FONT_PATH, FONT_SIZE, "MONOCHROME")
    DataToText_BagInfo:SetFont(FONT_PATH, FONT_SIZE, "MONOCHROME")

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
    end
end
