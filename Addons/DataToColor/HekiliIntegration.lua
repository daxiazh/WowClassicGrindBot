----------------------------------------------------------------------------
--  HekiliIntegration.lua
--  Hekili 插件集成模块 - 获取技能推荐数据供 DataToColor 编码使用
----------------------------------------------------------------------------

local Load = select(2, ...)
local DataToColor = unpack(Load)

--- 获取 Hekili Primary 队列前2个推荐(仅自动模式)
-- 用于 DataToColor 编码,供 C# 端识别当前推荐的技能及冷却时间
-- @return table|nil 自动模式下返回技能列表,否则返回 nil
--   格式: { {actionID=123, cooldown=1500}, {actionID=456, cooldown=0} }
--   actionID: 技能 ID
--   cooldown: 冷却时间(毫秒)
function DataToColor:GetHekiliRecommendations()
    -- 检查 Hekili 是否加载
    if not _G.Hekili then
        return nil
    end
    
    -- 检查是否为自动模式
    local mode = _G.Hekili.DB.profile.toggles.mode.value
    if mode ~= "automatic" then
        return nil
    end
    
    -- 获取 Primary 显示框架 (通过 DisplayPool 访问)
    local primaryFrame = _G.Hekili.DisplayPool and _G.Hekili.DisplayPool["Primary"]
    if not primaryFrame or not primaryFrame.Recommendations then
        return nil
    end
    
    local recommendations = {}
    
    -- 获取前 2 个推荐
    for i = 1, 2 do
        local rec = primaryFrame.Recommendations[i]
        if rec and rec.actionID then
            -- 获取技能冷却时间
            local start, duration = GetSpellCooldown(rec.actionID)
            local cdRemains = 0
            if start > 0 and duration > 0 then
                cdRemains = math.max(0, (start + duration) - GetTime())
            end
            
            -- 获取快捷键 (如果有)
            local keybind = rec.keybind or ""
            
            table.insert(recommendations, {
                actionID = rec.actionID,
                cooldown = math.floor(cdRemains * 1000),  -- 转为毫秒
                keybind = keybind  -- 快捷键字符串
            })
        end
    end
    
    return recommendations
end

--- 测试 Hekili 集成功能,打印推荐技能和 CD
function DataToColor:TestHekili()
    DataToColor:Print("=== Hekili 集成测试 (自动模式) ===")
    
    -- 检查 Hekili 是否加载
    if not _G.Hekili then
        DataToColor:Print("|cffff0000错误: Hekili 插件未加载|r")
        return
    end
    
    DataToColor:Print("|cff00ff00✓ Hekili 已加载|r (版本: " .. tostring(_G.Hekili.Version or "未知") .. ")")
    
    -- 检查模式
    local mode = _G.Hekili.DB.profile.toggles.mode.value
    DataToColor:Print("当前模式: |cffffff00" .. tostring(mode) .. "|r")
    
    -- 获取推荐
    local recs = self:GetHekiliRecommendations()
    
    if not recs then
        DataToColor:Print("|cffff0000未获取到推荐|r")
        if mode ~= "automatic" then
            DataToColor:Print("  原因: Hekili 未处于自动模式")
            DataToColor:Print("  提示: 切换到自动模式后重试")
        else
            DataToColor:Print("  提示: 进入战斗或确保 Hekili Primary 队列有推荐")
        end
        DataToColor:Print("=== 测试完成 ===")
        return
    end
    
    DataToColor:Print("|cff00ff00✓ 获取到 " .. #recs .. " 个推荐技能|r")
    
    for i, rec in ipairs(recs) do
        local cdSec = rec.cooldown / 1000
        DataToColor:Print(string.format("  [%d] ID:|cffffff00%d|r  CD:|cffff00ff%.1fs|r (%dms)", 
            i, rec.actionID, cdSec, rec.cooldown))
    end
    
    DataToColor:Print("=== 测试完成 ===")
end

-- 注册斜杠命令
DataToColor:RegisterChatCommand('testhekili', 'TestHekili')

DataToColor:Print("Hekili 集成模块已加载 (使用 /testhekili 测试)")
