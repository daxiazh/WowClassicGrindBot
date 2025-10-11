----------------------------------------------------------------------------
--  DataToText - OCR-Friendly Game Data Display
----------------------------------------------------------------------------

-- Create addon namespace
local AddOnName = "DataToText"
local DataToText = {}
_G[AddOnName] = DataToText

-- Core references
DataToText.C = {} -- Constants
DataToText.M = {} -- Modules
DataToText.U = {} -- Utils

-- Version detection
local WOW_PROJECT_ID = WOW_PROJECT_ID
local WOW_PROJECT_CLASSIC = WOW_PROJECT_CLASSIC or 2
local WOW_PROJECT_BURNING_CRUSADE_CLASSIC = WOW_PROJECT_BURNING_CRUSADE_CLASSIC or 5
local WOW_PROJECT_WRATH_CLASSIC = WOW_PROJECT_WRATH_CLASSIC or 11
local WOW_PROJECT_CATACLYSM_CLASSIC = WOW_PROJECT_CATACLYSM_CLASSIC or 14

-- Detect client version
if WOW_PROJECT_ID == WOW_PROJECT_CATACLYSM_CLASSIC then
    DataToText.ClientVersion = 5 -- Cata
elseif WOW_PROJECT_ID == WOW_PROJECT_WRATH_CLASSIC then
    DataToText.ClientVersion = 4 -- WotLK
elseif WOW_PROJECT_ID == WOW_PROJECT_BURNING_CRUSADE_CLASSIC then
    DataToText.ClientVersion = 3 -- TBC
elseif WOW_PROJECT_ID == WOW_PROJECT_CLASSIC then
    DataToText.ClientVersion = 2 -- Classic
else
    DataToText.ClientVersion = 1 -- Retail/Unknown
end

-- Constants
DataToText.C.FONT_PATH = "Interface\\AddOns\\DataToText\\Fonts\\Tiny-Bold.ttf"
DataToText.C.FONT_SIZE = 12
DataToText.C.FONT_FLAGS = "MONOCHROME, OUTLINE"

DataToText.C.FRAME_WIDTH = 900
DataToText.C.FRAME_HEIGHT = 100
DataToText.C.LINE_HEIGHT = 16

DataToText.C.UPDATE_INTERVAL = 0.05 -- 20 FPS (50ms)

-- Unit IDs
DataToText.C.unitPlayer = "player"
DataToText.C.unitTarget = "target"
DataToText.C.unitPet = "pet"
DataToText.C.unitPetTarget = "pettarget"
DataToText.C.unitTargetTarget = "targettarget"
DataToText.C.unitMouseover = "mouseover"

if WOW_PROJECT_ID == WOW_PROJECT_CLASSIC then
    DataToText.C.unitFocus = "party1" -- Classic doesn't have focus
    DataToText.C.unitFocusTarget = "party1target"
else
    DataToText.C.unitFocus = "focus"
    DataToText.C.unitFocusTarget = "focustarget"
end

-- API compatibility
-- Classic/TBC/WotLK use global functions, Retail uses C_Container namespace
DataToText.GetContainerNumSlots = GetContainerNumSlots or (C_Container and C_Container.GetContainerNumSlots)
DataToText.GetContainerNumFreeSlots = GetContainerNumFreeSlots or (C_Container and C_Container.GetContainerNumFreeSlots)

if GetContainerItemInfo then
    -- Classic/TBC/WotLK API
    DataToText.GetContainerItemInfo = GetContainerItemInfo
else
    -- Retail API
    DataToText.GetContainerItemInfo = function(bagID, slot)
        if not C_Container then return nil end
        local info = C_Container.GetContainerItemInfo(bagID, slot)
        if not info then return nil end
        return info.iconFileID, info.stackCount, info.isLocked, info.quality,
               info.isReadable, info.hasLoot, info.hyperlink, info.isFiltered,
               info.hasNoValue, info.itemID, info.isBound
    end
end

-- Character info
DataToText.C.CHARACTER_NAME = UnitName(DataToText.C.unitPlayer) or "Unknown"
DataToText.C.CHARACTER_GUID = (UnitGUID and UnitGUID(DataToText.C.unitPlayer)) or "0x0000000000000000"
_, DataToText.C.CHARACTER_CLASS, DataToText.C.CHARACTER_CLASS_ID = UnitClass(DataToText.C.unitPlayer)
_, _, DataToText.C.CHARACTER_RACE_ID = UnitRace(DataToText.C.unitPlayer)

-- Saved variables
DataToTextDB = DataToTextDB or {
    enabled = true,
    posX = 0,
    posY = -5,
}

-- Print helper
function DataToText:Print(msg)
    DEFAULT_CHAT_FRAME:AddMessage("|cff00ff00[DataToText]|r " .. tostring(msg))
end

-- Event frame
local eventFrame = CreateFrame("Frame")
DataToText.eventFrame = eventFrame

eventFrame:RegisterEvent("ADDON_LOADED")
eventFrame:RegisterEvent("PLAYER_ENTERING_WORLD")
eventFrame:RegisterEvent("PLAYER_LEAVING_WORLD")

eventFrame:SetScript("OnEvent", function(self, event, arg1)
    if event == "ADDON_LOADED" then
        if arg1 == AddOnName then
            DataToText:OnInitialize()
        end
    elseif event == "PLAYER_ENTERING_WORLD" then
        DataToText:OnEnable()
    elseif event == "PLAYER_LEAVING_WORLD" then
        DataToText:OnDisable()
    end
end)

function DataToText:OnInitialize()
    self:Print("Loaded v1.0.0")
    self:Print("Client Version: " .. tostring(self.ClientVersion))

    -- Register slash commands
    SLASH_DATATOTEXT1 = "/dtt"
    SLASH_DATATOTEXT2 = "/datatotext"
    SlashCmdList["DATATOTEXT"] = function(msg)
        if msg == "toggle" or msg == "" then
            DataToTextDB.enabled = not DataToTextDB.enabled
            if DataToTextDB.enabled then
                self:OnEnable()
                self:Print("Enabled")
            else
                self:OnDisable()
                self:Print("Disabled")
            end
        elseif msg == "reset" then
            DataToTextDB.posX = 0
            DataToTextDB.posY = -5
            if self.displayFrame then
                self.displayFrame:ClearAllPoints()
                self.displayFrame:SetPoint("TOP", UIParent, "TOP", DataToTextDB.posX, DataToTextDB.posY)
            end
            self:Print("Position reset")
        elseif msg == "debug" then
            self:Print("Debug Info:")
            self:Print("  displayFrame: " .. tostring(self.displayFrame ~= nil))
            self:Print("  Visible: " .. tostring(self.displayFrame and self.displayFrame:IsVisible() or false))
            self:Print("  Modules: " .. tostring(self.M and "loaded" or "nil"))
            local moduleCount = 0
            if self.M then
                for name, _ in pairs(self.M) do
                    moduleCount = moduleCount + 1
                    self:Print("    - " .. name)
                end
            end
            self:Print("  Total modules: " .. moduleCount)
        else
            self:Print("Commands:")
            self:Print("/dtt toggle - Toggle display")
            self:Print("/dtt reset - Reset position")
            self:Print("/dtt debug - Show debug info")
        end
    end

    self:Print("Slash commands registered")
end

function DataToText:OnEnable()
    if not DataToTextDB.enabled then
        self:Print("OnEnable: Disabled in DB, aborting")
        return
    end

    self:Print("OnEnable: Creating display frame...")

    if not self.displayFrame then
        self:CreateDisplayFrame()
        self:Print("OnEnable: Display frame created")
    else
        self:Print("OnEnable: Display frame already exists")
    end

    if self.displayFrame then
        self.displayFrame:Show()
        self:Print("OnEnable: Display frame shown")
        self:Print("OnEnable: Frame visible: " .. tostring(self.displayFrame:IsVisible()))
    else
        self:Print("OnEnable: ERROR - Display frame is nil!")
    end

    self.updateTimer = 0
    self:Print("Display enabled")
end

function DataToText:OnDisable()
    if self.displayFrame then
        self.displayFrame:Hide()
    end
    self:Print("Display disabled")
end

-- Placeholder for CreateDisplayFrame (will be defined in Core.lua)
function DataToText:CreateDisplayFrame()
    -- Implemented in Core.lua
end
