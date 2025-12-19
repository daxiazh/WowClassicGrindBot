----------------------------------------------------------------------------
--  HekiliIntegration.lua
--  Hekili 插件集成模块 - 获取技能推荐数据供 DataToColor 编码使用
----------------------------------------------------------------------------

local Load = select(2, ...)
local DataToColor = unpack(Load)

--- 获取 Hekili Primary 队列前2个推荐(仅自动模式)
-- 用于 DataToColor 编码,供 C# 端识别当前推荐的技能及可用性
-- @return table|nil 自动模式下返回技能列表,否则返回 nil
--   格式: { {actionID=123, usable=true, keybind="1"}, {actionID=456, usable=false, keybind="2"} }
--   actionID: 技能 ID
--   usable: 是否可用 (Hekili 的 Button.unusable 取反, 包含能量/距离/条件/CD/GCD等所有检查)
--   keybind: 快捷键字符串
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
        local btn = primaryFrame.Buttons[i]
        
        if rec and rec.actionID and btn then
            local keybind = rec.keybind or ""
            
            -- 检查技能是否可用
            -- 说明: 直接使用 Hekili 的 exact_time（最佳释放时机）
            -- Hekili 已综合考虑: 法力/能量/怒气、CD、GCD、施法中、连击点数、能量管理、优先级等
            local usable = true
            local now = GetTime()
            
            -- 1. 检查 Hekili 推荐的释放时机
            -- exact_time: Hekili 计算的最佳释放时间点
            -- 如果 exact_time > now，说明 Hekili 认为现在不应该释放
            if rec.exact_time then
                local delay = rec.exact_time - now
                if delay > 0.05 then  -- 留 50ms 容差
                    usable = false
                end
            end
            
            -- 2. 二次检查：法力/能量/怒气是否足够（防止 Hekili 数据延迟）
            if usable then
                local ability = _G.Hekili.Class.abilities[rec.actionID]
                if ability then
                    local spellName = ability.actualName or ability.name
                    if spellName then
                        local isUsable, notEnoughPower = IsUsableSpell(spellName)
                        if notEnoughPower then
                            usable = false
                        end
                    end
                end
            end
            
            table.insert(recommendations, {
                actionID = rec.actionID,
                usable = usable,  -- true=可用, false=不可用
                keybind = keybind
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
        local usableText = rec.usable and "|cff00ff00可用|r" or "|cffff0000不可用|r"
        DataToColor:Print(string.format("  [%d] ID:|cffffff00%d|r  状态:%s  快捷键:|cff00ffff%s|r", 
            i, rec.actionID, usableText, rec.keybind ~= "" and rec.keybind or "无"))
    end
    
    DataToColor:Print("=== 测试完成 ===")
end

-- 注册斜杠命令
DataToColor:RegisterChatCommand('testhekili', 'TestHekili')

DataToColor:Print("Hekili 集成模块已加载 (使用 /testhekili 测试)")
