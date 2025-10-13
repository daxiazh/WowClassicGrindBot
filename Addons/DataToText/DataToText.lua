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

----------------------------------------------------------------------------
-- 引用外部模块
----------------------------------------------------------------------------

-- 工具函数（定义在 Utils.lua）
local U = DataToTextUtils

-- 测试函数（定义在 Tests.lua）
local T = DataToTextTests

-- 本地快捷方式
local ToHex = U.ToHex
local GetUnitGUID = U.GetUnitGUID

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

    -- 编码名称为十六进制
    local nameHex = U.EncodeNameHex(name)

    local text = "TARGET: " .. name .. "\n"
    text = text .. "  NAME_HEX: " .. nameHex .. "\n"
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
    msg = U.Trim(msg or "")

    if msg == "test" then
        -- 运行所有测试
        T.RunAllTests()
    elseif msg == "testutf8" or msg == "utf8" then
        -- 运行UTF-8编码测试
        T.TestUTF8Encoding()
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
