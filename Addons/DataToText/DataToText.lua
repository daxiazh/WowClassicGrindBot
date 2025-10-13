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
local FONT_SIZE = 14
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

-- Helper: Get unit GUID (compatible with Classic)
local function GetUnitGUID(unit)
    if UnitGUID then
        return UnitGUID(unit) or "0x0000000000000000"
    end
    return "0x0000000000000000"
end

----------------------------------------------------------------------------
-- 工具函数 (Utils)
----------------------------------------------------------------------------

-- 将数字转换为固定宽度的十六进制字符串
local function ToHex(num, digits)
    local hex = string.format("%X", num or 0)
    while string.len(hex) < digits do
        hex = "0" .. hex
    end
    return hex
end

-- 将十六进制字符串转换为数字
local function FromHex(hexStr)
    if not hexStr or hexStr == "" then
        return 0
    end
    return tonumber(hexStr, 16) or 0
end

-- 运行工具函数自测
local function TestUtils()
    local tests = {
        {name = "ToHex(255, 2)", func = function() return ToHex(255, 2) end, expected = "FF"},
        {name = "ToHex(16, 4)", func = function() return ToHex(16, 4) end, expected = "0010"},
        {name = "ToHex(0, 2)", func = function() return ToHex(0, 2) end, expected = "00"},
        {name = "FromHex('FF')", func = function() return FromHex("FF") end, expected = 255},
        {name = "FromHex('0010')", func = function() return FromHex("0010") end, expected = 16},
        {name = "FromHex('00')", func = function() return FromHex("00") end, expected = 0},
    }

    local passed = 0
    local failed = 0

    for _, test in ipairs(tests) do
        local result = test.func()
        if result == test.expected then
            passed = passed + 1
            DataToText_Print("✓ " .. test.name .. " = " .. tostring(result))
        else
            failed = failed + 1
            DataToText_Print("✗ " .. test.name .. " 期望: " .. tostring(test.expected) .. ", 实际: " .. tostring(result))
        end
    end

    DataToText_Print("测试完成: " .. passed .. " 通过, " .. failed .. " 失败")
end

-- Get player data
local function GetPlayerData()
    local hp = UnitHealth("player") or 0
    local maxHp = UnitHealthMax("player") or 1
    local mana = UnitMana("player") or 0
    local maxMana = UnitManaMax("player") or 1
    local level = UnitLevel("player") or 1
    local xp = UnitXP and UnitXP("player") or 0
    local maxXp = UnitXPMax and UnitXPMax("player") or 1

    local text = "PLAYER:\n"
    text = text .. "  HP:    " .. hp .. " / " .. maxHp .. " (" .. ToHex(hp, 4) .. "/" .. ToHex(maxHp, 4) .. ")\n"
    text = text .. "  MANA:  " .. mana .. " / " .. maxMana .. " (" .. ToHex(mana, 4) .. "/" .. ToHex(maxMana, 4) .. ")\n"
    text = text .. "  LEVEL: " .. level .. " (0x" .. ToHex(level, 2) .. ")\n"
    text = text .. "  XP:    " .. xp .. " / " .. maxXp .. " (" .. ToHex(xp, 4) .. "/" .. ToHex(maxXp, 4) .. ")\n"
    text = text .. "  GUID:  " .. GetUnitGUID("player")

    return text
end

-- Get target data
local function GetTargetData()
    if not UnitExists("target") then
        return "TARGET: None"
    end

    local name = UnitName("target") or "Unknown"
    local hp = UnitHealth("target") or 0
    local maxHp = UnitHealthMax("target") or 1
    local level = UnitLevel("target") or 0
    local isDead = UnitIsDead("target")

    local text = "TARGET: " .. name .. "\n"
    text = text .. "  HP:    " .. hp .. " / " .. maxHp .. " (" .. ToHex(hp, 4) .. "/" .. ToHex(maxHp, 4) .. ")\n"
    text = text .. "  LEVEL: " .. level .. " (0x" .. ToHex(level, 2) .. ")\n"
    text = text .. "  DEAD:  " .. (isDead and "YES" or "NO") .. "\n"
    text = text .. "  GUID:  " .. GetUnitGUID("target")

    return text
end

-- Get bag data
local function GetBagData()
    local totalSlots = 0
    local freeSlots = 0

    -- Count bag slots (0-4 for classic)
    for bag = 0, 4 do
        local slots = GetContainerNumSlots(bag) or 0
        totalSlots = totalSlots + slots

        for slot = 1, slots do
            local itemLink = GetContainerItemLink(bag, slot)
            if not itemLink then
                freeSlots = freeSlots + 1
            end
        end
    end

    local usedSlots = totalSlots - freeSlots

    local text = "BAG:\n"
    text = text .. "  USED:  " .. usedSlots .. " / " .. totalSlots .. "\n"
    text = text .. "  FREE:  " .. freeSlots .. " (0x" .. ToHex(freeSlots, 2) .. ")"

    return text
end

-- Update display
local function UpdateDisplay()
    if not DataToTextFrame or not DataToTextFrame:IsVisible() then
        return
    end

    DataToText_PlayerInfo:SetText(GetPlayerData())
    DataToText_TargetInfo:SetText(GetTargetData())
    DataToText_BagInfo:SetText(GetBagData())
end

-- Slash commands
SLASH_DATATOTEXT1 = "/dtt"
SLASH_DATATOTEXT2 = "/datatotext"
SlashCmdList["DATATOTEXT"] = function(msg)
    -- 去除首尾空格
    msg = string.gsub(msg or "", "^%s*(.-)%s*$", "%1")

    if msg == "test" then
        -- 运行工具函数测试
        DataToText_Print("运行工具函数测试...")
        TestUtils()
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
        DataToText_Print("  /dtt test - 运行工具函数测试")
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
