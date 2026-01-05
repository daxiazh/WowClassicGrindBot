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

-- 状态指示器常量
local DOT_SIZE = 16                        -- 圆点尺寸
local DOT_X = BUTTON_X + BUTTON_WIDTH + 4 -- 按钮右侧 4px
local DOT_Y = BUTTON_Y - 2                -- 垂直居中（向下偏移2px）

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
-- VizAura 状态指示器
-- ============================================================================

-- 引用状态枚举，避免魔法数字
local VizAuraStatus = DataToColor.VizAuraStatus

-- 状态颜色映射
local StatusColors = {
    [VizAuraStatus.WORKING] = {0, 1, 0},           -- 绿色
    [VizAuraStatus.DISABLED] = {0.5, 0.5, 0.5},    -- 灰色
    [VizAuraStatus.PAUSED_MODIFIER] = {1, 0, 0},   -- 红色
    [VizAuraStatus.PAUSED_MOUNTED] = {1, 0, 0},    -- 红色
    [VizAuraStatus.PAUSED_NO_TARGET] = {1, 0, 0},  -- 红色
    [VizAuraStatus.PAUSED_DEAD_TARGET] = {1, 0, 0},-- 红色
    [VizAuraStatus.PAUSED_FRIENDLY] = {1, 0, 0},   -- 红色
    [VizAuraStatus.PAUSED_INVALID_COMBAT] = {1, 0, 0} -- 红色
}

-- Tooltip 文本映射
local StatusTooltips = {
    [VizAuraStatus.WORKING] = "VizAura: 工作中",
    [VizAuraStatus.DISABLED] = "VizAura: 已禁用",
    [VizAuraStatus.PAUSED_MODIFIER] = "VizAura: 已暂停 (按住修饰键)",
    [VizAuraStatus.PAUSED_MOUNTED] = "VizAura: 已暂停 (骑乘中)",
    [VizAuraStatus.PAUSED_NO_TARGET] = "VizAura: 已暂停 (无目标)",
    [VizAuraStatus.PAUSED_DEAD_TARGET] = "VizAura: 已暂停 (目标已死亡)",
    [VizAuraStatus.PAUSED_FRIENDLY] = "VizAura: 已暂停 (目标非敌对)",
    [VizAuraStatus.PAUSED_INVALID_COMBAT] = "VizAura: 已暂停 (目标未进入战斗)"
}

-- 状态指示器框架
local statusIndicator

--- 创建状态指示器
local function CreateStatusIndicator()
    -- 创建父框架
    statusIndicator = CreateFrame("Frame", "DataToColorVizAuraStatusDot", UIParent)
    statusIndicator:SetSize(DOT_SIZE, DOT_SIZE)
    statusIndicator:SetPoint("TOPLEFT", DOT_X, DOT_Y)
    statusIndicator:SetFrameStrata("TOOLTIP")
    statusIndicator:EnableMouse(true)
    statusIndicator.currentStatus = -1

    -- 创建圆形纹理
    statusIndicator.dot = statusIndicator:CreateTexture(nil, "ARTWORK")
    statusIndicator.dot:SetAllPoints(statusIndicator)
    statusIndicator.dot:SetTexture("Interface\\Buttons\\WHITE8x8")
    statusIndicator.dot:SetVertexColor(0.5, 0.5, 0.5, 1)  -- 默认灰色

    -- Tooltip 事件
    statusIndicator:SetScript("OnEnter", function(self)
        GameTooltip:SetOwner(self, "ANCHOR_RIGHT")
        local tooltipText = StatusTooltips[self.currentStatus] or "VizAura: 状态未知"
        GameTooltip:SetText(tooltipText, 1, 1, 1)
        GameTooltip:Show()
    end)

    statusIndicator:SetScript("OnLeave", function(self)
        GameTooltip:Hide()
    end)
end

--- 更新状态指示器
--- @param status number 当前状态代码
local function UpdateStatusIndicator(status)
    if not statusIndicator then return end
    if statusIndicator.currentStatus == status then return end -- 避免重复更新

    local color = StatusColors[status] or {0.5, 0.5, 0.5}
    statusIndicator.dot:SetVertexColor(color[1], color[2], color[3], 1)
    statusIndicator.currentStatus = status
end

--- 暴露更新接口（供 DataToColor.lua 调用）
--- @param status number 当前状态代码
function DataToColor:UpdateVizAuraStatusIndicator(status)
    UpdateStatusIndicator(status)
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

    -- 创建状态指示器
    CreateStatusIndicator()

    -- 提示
    local status = DataToColorDB.VizAuraAutoCastEnabled and "|cff00ff00启用|r" or "|cffff0000禁用|r"
    DataToColor:Print("VizAura 自动施法: " .. status .. " (点击左下角按钮切换)")
end

DataToColor:Print("VizAura 自动施法开关已加载")
