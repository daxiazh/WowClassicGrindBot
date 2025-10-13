----------------------------------------------------------------------------
--  DataToText - Core Display Framework
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local C = DataToText.C
local U = DataToText.U
local M = DataToText.M

-- Create main display frame
function DataToText:CreateDisplayFrame()
    local frame = CreateFrame("Frame", "DataToTextFrame", UIParent)
    frame:SetWidth(C.FRAME_WIDTH)
    frame:SetHeight(C.FRAME_HEIGHT)
    frame:SetPoint("TOP", UIParent, "TOP", DataToTextDB.posX, DataToTextDB.posY)
    frame:SetFrameStrata("TOOLTIP")
    frame:SetMovable(true)
    frame:EnableMouse(true)

    -- Background
    frame.bg = frame:CreateTexture(nil, "BACKGROUND")
    frame.bg:SetAllPoints()
    frame.bg:SetColorTexture(0, 0, 0, 1) -- Pure black

    -- Border
    frame.border = frame:CreateTexture(nil, "BORDER")
    frame.border:SetColorTexture(0.3, 0.3, 0.3, 1)
    frame.border:SetPoint("TOPLEFT", frame, "TOPLEFT", -1, 1)
    frame.border:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", 1, -1)

    -- Make frame draggable
    frame:SetScript("OnMouseDown", function(self, button)
        if button == "LeftButton" then
            self:StartMoving()
        end
    end)

    frame:SetScript("OnMouseUp", function(self, button)
        if button == "LeftButton" then
            self:StopMovingOrSizing()
            local point, _, _, x, y = self:GetPoint()
            DataToTextDB.posX = x
            DataToTextDB.posY = y
        end
    end)

    -- Create text lines (6 lines for 6 data modules)
    frame.lines = {}
    for i = 1, 6 do
        local line = frame:CreateFontString(nil, "OVERLAY")

        -- Load custom font
        local fontLoaded = line:SetFont(C.FONT_PATH, C.FONT_SIZE, C.FONT_FLAGS)
        if not fontLoaded then
            -- Fallback to system monospace font
            line:SetFont("Fonts\\ARIALN.TTF", C.FONT_SIZE, "MONOCHROME, OUTLINE")
            if i == 1 then
                DataToText:Print("Warning: Custom font not found, using fallback")
            end
        end

        line:SetTextColor(1, 1, 1, 1) -- Pure white
        line:SetJustifyH("LEFT")
        line:SetPoint("TOPLEFT", frame, "TOPLEFT", 5, -5 - (i - 1) * C.LINE_HEIGHT)
        line:SetWidth(C.FRAME_WIDTH - 10)
        line:SetText("L" .. i .. ": Initializing...")

        frame.lines[i] = line
    end

    -- Update timer
    frame.elapsed = 0
    frame:SetScript("OnUpdate", function(self, elapsed)
        self.elapsed = self.elapsed + elapsed

        if self.elapsed >= C.UPDATE_INTERVAL then
            DataToText:UpdateDisplay()
            self.elapsed = 0
        end
    end)

    self.displayFrame = frame
    return frame
end

-- Main update function
function DataToText:UpdateDisplay()
    if not self.displayFrame or not self.displayFrame:IsVisible() then
        return
    end

    -- Build data strings from modules
    local lines = {}

    -- Line 1: Player data
    lines[1] = M.Player and M.Player:GetData() or "L1: Player module not loaded"

    -- Line 2: Target data
    lines[2] = M.Target and M.Target:GetData() or "L2: Target module not loaded"

    -- Line 3: Combat data
    lines[3] = M.Combat and M.Combat:GetData() or "L3: Combat module not loaded"

    -- Line 4: Inventory data
    lines[4] = M.Inventory and M.Inventory:GetData() or "L4: Inventory module not loaded"

    -- Line 5: Status flags
    lines[5] = M.Status and M.Status:GetData() or "L5: Status module not loaded"

    -- Line 6: Checksum
    local dataText = table.concat(lines, " ")
    local checksum = U.ChecksumHex(dataText)
    lines[6] = "L6:CHK=" .. checksum

    -- Update display
    for i = 1, 6 do
        if self.displayFrame.lines[i] then
            self.displayFrame.lines[i]:SetText(lines[i])
        end
    end
end

-- Module registration
function DataToText:RegisterModule(name, module)
    M[name] = module
    self:Print("Module registered: " .. name)
end
