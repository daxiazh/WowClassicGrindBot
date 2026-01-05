----------------------------------------------------------------------------
--  ConfigUI.lua
--  DataToColor VizAura 自动施法开关 - 简化版（常驻按钮）
----------------------------------------------------------------------------

local Load = select(2, ...)
local DataToColor = unpack(Load)

-- 配置变量（使用 SavedVariables 持久化）
DataToColorDB = DataToColorDB or {
    VizAuraAutoCastEnabled = true  -- 默认启用 VizAura 自动施法
}

-- 常量
local CELL_SIZE = 5       -- 从 DataToColor.lua 获取
local BUTTON_WIDTH = 80   -- 按钮宽度
local BUTTON_HEIGHT = 20  -- 按钮高度
local BUTTON_X = 20       -- X 位置（左侧）
local BUTTON_Y = -(CELL_SIZE + 8)  -- Y 位置（数据条下方，留 8px 间距）

-- 创建常驻切换按钮
local toggleButton = CreateFrame("Button", "DataToColorVizAuraToggle", UIParent, BackdropTemplateMixin and "BackdropTemplate")
toggleButton:SetSize(BUTTON_WIDTH, BUTTON_HEIGHT)
toggleButton:SetPoint("TOPLEFT", BUTTON_X, BUTTON_Y)
toggleButton:SetBackdrop({
    bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
    edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border",
    tile = true,
    tileSize = 16,
    edgeSize = 12,
    insets = { left = 3, right = 3, top = 3, bottom = 3 }
})
toggleButton:SetFrameStrata("TOOLTIP")
toggleButton:EnableMouse(true)

-- 添加文字标签
local buttonText = toggleButton:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall")
buttonText:SetPoint("CENTER")
buttonText:SetText("AUTO")

--- 更新按钮颜色和文字
local function UpdateButtonColor()
    if DataToColorDB.VizAuraAutoCastEnabled then
        -- 启用: 绿色边框和文字
        toggleButton:SetBackdropBorderColor(0, 1, 0, 1)
        toggleButton:SetBackdropColor(0, 0, 0, 0.7)
        buttonText:SetTextColor(0, 1, 0, 1)
        buttonText:SetText("AUTO ON")
    else
        -- 禁用: 红色边框和文字
        toggleButton:SetBackdropBorderColor(1, 0, 0, 1)
        toggleButton:SetBackdropColor(0, 0, 0, 0.7)
        buttonText:SetTextColor(1, 0, 0, 1)
        buttonText:SetText("AUTO OFF")
    end
end

--- 点击事件: 切换自动施法状态
toggleButton:SetScript("OnClick", function(self)
    -- 切换状态
    DataToColorDB.VizAuraAutoCastEnabled = not DataToColorDB.VizAuraAutoCastEnabled
    DataToColor.DATA_CONFIG.VIZAURA_AUTO_CAST_ENABLED = DataToColorDB.VizAuraAutoCastEnabled
    
    -- 更新按钮颜色
    UpdateButtonColor()
    
    -- 聊天提示
    local status = DataToColorDB.VizAuraAutoCastEnabled and "|cff00ff00已启用|r" or "|cffff0000已禁用|r"
    DataToColor:Print("VizAura 自动施法: " .. status)
    
    -- 播放音效
    PlaySound(SOUNDKIT.IG_MAINMENU_OPTION_CHECKBOX_ON)
end)

--- 鼠标悬停: 显示 Tooltip
toggleButton:SetScript("OnEnter", function(self)
    GameTooltip:SetOwner(self, "ANCHOR_RIGHT")
    GameTooltip:SetText("|cff00b3ffVizAura 自动施法|r", 1, 1, 1)
    GameTooltip:AddLine("点击切换启用/禁用", 0.7, 0.7, 0.7)
    
    local status = DataToColorDB.VizAuraAutoCastEnabled and "|cff00ff00已启用|r" or "|cffff0000已禁用|r"
    GameTooltip:AddLine("当前状态: " .. status, 1, 1, 0)
    
    GameTooltip:Show()
end)

--- 鼠标离开: 隐藏 Tooltip
toggleButton:SetScript("OnLeave", function(self)
    GameTooltip:Hide()
end)

-- ============================================================================
-- VizAura 警告图标相关函数
-- ============================================================================

--- 创建 VizAura 目标不合法警告图标
--- 在 InitConfig() 中调用
local function CreateWarningFrame()
    local warningFrame = CreateFrame("Frame", "VizAuraWarningFrame", UIParent, BackdropTemplateMixin and "BackdropTemplate")
    warningFrame:SetPoint("CENTER", UIParent, "CENTER", 0, 0)
    warningFrame:SetSize(64, 64)
    warningFrame:SetFrameStrata("DIALOG")

    -- 创建纹理显示禁止图标
    local warningTexture = warningFrame:CreateTexture(nil, "OVERLAY")
    warningTexture:SetAllPoints(warningFrame)
    warningTexture:SetTexture("Interface\\RaidFrame\\ReadyCheck-NotReady") -- 红色禁止图标

    -- 创建文字提示
    local warningText = warningFrame:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge")
    warningText:SetPoint("TOP", warningFrame, "BOTTOM", 0, -8)
    warningText:SetTextColor(1, 0, 0, 1)
    warningText:SetText("目标不合法")

    -- 默认隐藏
    warningFrame:Hide()

    -- 保存到全局
    DataToColor.vizAuraWarningFrame = warningFrame
end

--- 更新警告图标显示状态（每帧调用）
--- @param show boolean 是否显示警告图标
function DataToColor:UpdateVizAuraWarning(show)
    if self.vizAuraWarningFrame then
        if show then
            self.vizAuraWarningFrame:Show()
        else
            self.vizAuraWarningFrame:Hide()
        end
    end
end

-- ============================================================================

--- 初始化配置（在 OnInitialize 中调用）
function DataToColor:InitConfig()
    -- 确保 SavedVariables 存在
    DataToColorDB = DataToColorDB or {}
    if DataToColorDB.VizAuraAutoCastEnabled == nil then
        DataToColorDB.VizAuraAutoCastEnabled = true  -- 默认启用
    end

    -- 同步到运行时配置
    DataToColor.DATA_CONFIG.VIZAURA_AUTO_CAST_ENABLED = DataToColorDB.VizAuraAutoCastEnabled

    -- 更新按钮颜色
    UpdateButtonColor()

    -- 创建警告图标框架
    CreateWarningFrame()

    -- 提示
    local status = DataToColorDB.VizAuraAutoCastEnabled and "|cff00ff00启用|r" or "|cffff0000禁用|r"
    DataToColor:Print("VizAura 自动施法: " .. status .. " (点击左下角按钮切换)")
end

DataToColor:Print("VizAura 自动施法开关已加载")
