----------------------------------------------------------------------------
--  HekiliIntegration.lua
--  Hekili 插件集成模块 - 获取技能推荐数据供 DataToColor 编码使用
----------------------------------------------------------------------------

local Load = select(2, ...)
local DataToColor = unpack(Load)

--- 获取 Hekili 所有队列的前两个推荐技能
-- 用于 DataToColor 编码,供 C# 端识别当前推荐的技能
-- @return table 包含5个队列的推荐技能数据,格式:
--   {
--     Primary = { {actionID, actionName, texture}, {actionID, actionName, texture} },
--     AOE = { ... },
--     Cooldowns = { ... },
--     Defensives = { ... },
--     Interrupts = { ... }
--   }
--   如果 Hekili 未加载或无推荐,对应队列返回空表 {}
function DataToColor:GetHekiliRecommendations()
    local recommendations = {
        Primary = {},
        AOE = {},
        Cooldowns = {},
        Defensives = {},
        Interrupts = {}
    }
    
    -- 检查 Hekili 是否加载
    if not _G.Hekili then
        return recommendations
    end
    
    -- 队列名称列表(对应 Hekili 的5个显示框架)
    local queueNames = { "Primary", "AOE", "Cooldowns", "Defensives", "Interrupts" }
    
    -- 遍历每个队列
    for _, queueName in ipairs(queueNames) do
        local displayName = "HekiliDisplay" .. queueName
        local frame = _G[displayName]
        
        -- 检查框架是否存在且有推荐数据
        if frame and frame.Recommendations then
            -- 获取前两个推荐
            for i = 1, 2 do
                local rec = frame.Recommendations[i]
                if rec and rec.actionID then
                    table.insert(recommendations[queueName], {
                        actionID = rec.actionID or 0,
                        actionName = rec.actionName or "",
                        texture = rec.texture or 0
                    })
                end
            end
        end
    end
    
    return recommendations
end

--- 测试 Hekili 集成功能,打印所有队列的推荐技能
function DataToColor:TestHekili()
    DataToColor:Print("=== Hekili 集成测试 ===")
    
    -- 检查 Hekili 是否加载
    if not _G.Hekili then
        DataToColor:Print("|cffff0000错误: Hekili 插件未加载|r")
        return
    end
    
    DataToColor:Print("|cff00ff00✓ Hekili 已加载|r (版本: " .. tostring(_G.Hekili.Version or "未知") .. ")")
    
    -- 获取推荐
    local recs = self:GetHekiliRecommendations()
    local queueNames = { "Primary", "AOE", "Cooldowns", "Defensives", "Interrupts" }
    local hasAnyRec = false
    
    for _, queueName in ipairs(queueNames) do
        local queue = recs[queueName]
        if queue and #queue > 0 then
            hasAnyRec = true
            DataToColor:Print("|cff00ff00" .. queueName .. " 队列:|r")
            for i, rec in ipairs(queue) do
                DataToColor:Print("  [" .. i .. "] ID:|cffffff00" .. rec.actionID .. "|r  名称:|cffff00ff" .. rec.actionName .. "|r  图标:" .. rec.texture)
            end
        else
            DataToColor:Print("|cff808080" .. queueName .. ": (无推荐)|r")
        end
    end
    
    if not hasAnyRec then
        DataToColor:Print("|cffff0000所有队列均无推荐|r")
        DataToColor:Print("  提示: 进入战斗或确保 Hekili 界面显示")
    end
    
    DataToColor:Print("=== 测试完成 ===")
end

-- 注册斜杠命令
DataToColor:RegisterChatCommand('testhekili', 'TestHekili')

DataToColor:Print("Hekili 集成模块已加载 (使用 /testhekili 测试)")
