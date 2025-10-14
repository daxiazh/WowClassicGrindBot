----------------------------------------------------------------------------
--  DataToText - OCR-Friendly Game Data Display
--  Version 2.0 - 使用模块化架构
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

-- 性能监控
local enableProfiling = false
local profileData = {}
local profileCount = 0

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

-- 工具函数
local U = DataToTextUtils

-- 测试函数
local T = DataToTextTests

-- 数据收集器（108个字段）
local FC = DataToTextFieldCollector

-- 网格编码器（65×65）
local GE = DataToTextGridEncoder

-- 网格渲染器
local GR = DataToTextGridRenderer

-- 初始化渲染器
GR.SetFontPath(FONT_PATH)

----------------------------------------------------------------------------
-- 性能分析工具
----------------------------------------------------------------------------

-- 重置性能数据
local function ResetProfileData()
    profileData = {
        CollectFields = 0,
        EncodeGrid = 0,
        RenderGrid = 0,
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
    local avgCollect = profileData.CollectFields / profileCount * 1000
    local avgEncode = profileData.EncodeGrid / profileCount * 1000
    local avgRender = profileData.RenderGrid / profileCount * 1000

    DataToText_Print(string.format("总耗时: %.2f ms", avgTotal))
    DataToText_Print(string.format("  CollectFields: %.2f ms (%.1f%%)", avgCollect, avgCollect/avgTotal*100))
    DataToText_Print(string.format("  EncodeGrid: %.2f ms (%.1f%%)", avgEncode, avgEncode/avgTotal*100))
    DataToText_Print(string.format("  RenderGrid: %.2f ms (%.1f%%)", avgRender, avgRender/avgTotal*100))
    DataToText_Print(string.format("预计帧率影响: %.1f FPS", 1 / avgTotal * 1000))
end

-- 显示容量信息
local function ShowCapacityInfo()
    local info = GE.GetCapacityInfo()

    DataToText_Print("=== 网格容量信息 ===")
    DataToText_Print(string.format("网格尺寸: %dx%d (%d cells)", info.gridSize, info.gridSize, info.totalCells))
    DataToText_Print(string.format("角标记: %d cells (4个3×3)", info.markerCells))
    DataToText_Print(string.format("数据区: %d cells (%d bytes)", info.dataCells, info.capacityBytes))
    DataToText_Print("")
    DataToText_Print("数据使用:")
    DataToText_Print(string.format("  元数据: %d bits (版本+字段数)", info.metadataBits))
    DataToText_Print(string.format("  字段数据: %d bits (108字段×24bit)", info.fieldBits))
    DataToText_Print(string.format("  CRC32: %d bits", info.crc32Bits))
    DataToText_Print(string.format("  总使用: %d bits (%d bytes)", info.usedBits, info.usedBytes))
    DataToText_Print(string.format("  剩余: %d bits (%d bytes)", info.remainingBits, info.remainingBytes))
    DataToText_Print(string.format("  使用率: %.1f%%", info.utilizationPercent))
end

----------------------------------------------------------------------------
-- 主更新函数
----------------------------------------------------------------------------

-- Update display
local function UpdateDisplay()
    if not DataToTextFrame or not DataToTextFrame:IsVisible() then
        return
    end

    if isPaused then
        return
    end

    local t0, t1, t2, t3
    if enableProfiling then
        t0 = GetTime()
    end

    -- 1. 收集108个字段数据
    local fields = FC.CollectAllFields()

    if enableProfiling then
        t1 = GetTime()
        profileData.CollectFields = profileData.CollectFields + (t1 - t0)
    end

    -- 2. 编码为65×65网格（含CRC32）
    local grid, stats = GE.EncodeToGrid(fields)

    if enableProfiling then
        t2 = GetTime()
        profileData.EncodeGrid = profileData.EncodeGrid + (t2 - t1)
    end

    -- 3. 渲染网格
    local success, msg = GR.RenderGrid(grid, DataToText_GridFrame)

    if not success then
        DataToText_Print("渲染错误: " .. msg)
    end

    if enableProfiling then
        t3 = GetTime()
        profileData.RenderGrid = profileData.RenderGrid + (t3 - t2)
        profileData.Total = profileData.Total + (t3 - t0)
        profileCount = profileCount + 1
    end

    -- 4. 更新EditBox文本显示（简化）
    local textLines = {
        "DataToText v2.0 - 65x65 Grid Encoding",
        "",
        string.format("Fields: %d / %d", stats.totalFields, FC.GetFieldCount()),
        string.format("Data: %d bytes (%d bits)", stats.totalBytes, stats.totalBits),
        string.format("Grid: %dx%d", stats.gridSize, stats.gridSize),
        string.format("Used: %d bits", stats.usedBits),
        string.format("Capacity: %d bits", stats.capacity),
        "",
        "Use /dtt help for commands"
    }

    DataToText_DataEditBox:SetText(table.concat(textLines, "\n"))
    DataToText_DataEditBox:SetAutoFocus(false)
    DataToText_DataEditBox:ClearFocus()
end

----------------------------------------------------------------------------
-- 斜杠命令
----------------------------------------------------------------------------

SLASH_DATATOTEXT1 = "/dtt"
SLASH_DATATOTEXT2 = "/datatotext"
SlashCmdList["DATATOTEXT"] = function(msg)
    msg = U.Trim(msg or "")

    if msg == "test" then
        T.RunAllTests()
    elseif msg == "utf8" then
        T.TestUTF8Encoding()
    elseif msg == "crc32" then
        T.TestCRC32()
    elseif msg == "profile" or msg == "perf" then
        enableProfiling = not enableProfiling
        if enableProfiling then
            ResetProfileData()
            DataToText_Print("性能分析已启用")
        else
            DataToText_Print("性能分析已禁用")
        end
    elseif msg == "report" then
        ShowProfileReport()
    elseif msg == "capacity" or msg == "cap" then
        ShowCapacityInfo()
    elseif msg == "help" then
        DataToText_Print("=== DataToText 命令列表 ===")
        DataToText_Print("/dtt - 切换显示")
        DataToText_Print("/dtt profile - 启用/禁用性能分析")
        DataToText_Print("/dtt report - 显示性能报告")
        DataToText_Print("/dtt capacity - 显示网格容量信息")
        DataToText_Print("/dtt test - 运行所有测试")
        DataToText_Print("/dtt utf8 - UTF-8编码测试")
        DataToText_Print("/dtt crc32 - CRC32测试")
        DataToText_Print("/dtt help - 显示此帮助")
    elseif msg == "" then
        if DataToTextFrame:IsShown() then
            PlaySound("igMainMenuContinue")
            DataToTextFrame:Hide()
        else
            PlaySound("igMainMenuOpen")
            DataToTextFrame:Show()
        end
    else
        DataToText_Print("未知命令: " .. msg)
        DataToText_Print("使用 /dtt help 查看可用命令")
    end
end

DataToText_Print("DataToText v2.0 loaded! (65x65 Grid, 108 Fields)")

----------------------------------------------------------------------------
-- 事件处理
----------------------------------------------------------------------------

-- OnLoad function
function DataToText_OnLoad()
    DataToText_DataEditBox:SetFont(FONT_PATH, FONT_SIZE, "MONOCHROME")
    DataToText_DataEditBox:SetTextColor(1, 1, 1)
    DataToText_DataEditBox:SetMultiLine(true)
    DataToText_DataEditBox:SetAutoFocus(false)
    DataToText_DataEditBox:EnableMouse(true)

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
        if DataToTextDB == nil then
            DataToTextDB = {
                version = 2,
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
        DataToText_Print("Initializing v2.0...")

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

        DataToText_Print("Ready! Use /dtt to toggle display.")
        DataToText_Print("Use /dtt help for commands.")
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
        isPaused = not isPaused
        if isPaused then
            widget:SetText("Resume")
            PlaySound("igMainMenuOptionCheckBoxOn")
            DataToText_Print("数据刷新已暂停")
        else
            widget:SetText("Pause")
            PlaySound("igMainMenuOptionCheckBoxOff")
            DataToText_Print("数据刷新已恢复")
        end
    end
end
