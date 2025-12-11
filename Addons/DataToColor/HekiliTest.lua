----------------------------------------------------------------------------
--  HekiliTest.lua
--  测试 DataToColor 是否能访问 Hekili 插件的推荐技能
----------------------------------------------------------------------------

local Load = select(2, ...)
local DataToColor = unpack(Load)

function DataToColor:TestHekiliAccess()
    DataToColor:Print("=== 开始测试 Hekili 访问 ===")
    
    -- 1. 检查 Hekili 全局变量是否存在
    if not _G.Hekili then
        DataToColor:Print("|cffff0000错误: Hekili 插件未加载或全局变量不存在|r")
        return false
    end
    
    DataToColor:Print("|cff00ff00✓ Hekili 全局变量存在|r")
    DataToColor:Print("  版本: " .. tostring(_G.Hekili.Version or "未知"))
    
    -- 2. 尝试直接从 Hekili 对象访问
    if _G.Hekili.displays then
        DataToColor:Print("|cff00ff00✓ 找到 Hekili.displays|r")
    end
    
    -- 3. 尝试访问推荐队列 (通过全局变量查找)
    local found = false
    
    -- 方法 1: 遍历全局变量找 Hekili 相关的队列数据
    for key, value in pairs(_G) do
        if type(key) == "string" and key:match("^Hekili") and type(value) == "table" then
            if value.queue or value.Recommendations then
                DataToColor:Print("|cff00ff00✓ 找到可能的推荐数据: " .. key .. "|r")
                found = true
            end
        end
    end
    
    -- 方法 2: 检查 Hekili UI 显示框架
    for i = 1, 10 do
        local displayName = "HekiliDisplay" .. (i == 1 and "Primary" or i)
        local frame = _G[displayName]
        
        if frame and frame.Recommendations then
            DataToColor:Print("|cff00ff00✓ 找到 Display: " .. displayName .. "|r")
            
            -- 尝试读取第一个推荐
            local rec = frame.Recommendations[1]
            if rec then
                DataToColor:Print("  推荐技能:")
                DataToColor:Print("    名称: |cffff00ff" .. tostring(rec.actionName or "未知") .. "|r")
                DataToColor:Print("    ID: |cffffff00" .. tostring(rec.actionID or 0) .. "|r")
                DataToColor:Print("    图标: " .. tostring(rec.texture or "无"))
                found = true
                break
            else
                DataToColor:Print("  (当前无推荐)")
            end
        end
    end
    
    if not found then
        DataToColor:Print("|cffff0000注意: 未找到当前推荐数据|r")
        DataToColor:Print("  可能原因:")
        DataToColor:Print("  - Hekili 界面未显示")
        DataToColor:Print("  - 当前无战斗推荐")
        DataToColor:Print("  - 需要进入战斗状态")
    end
    
    DataToColor:Print("=== 测试完成 ===")
    return found
end

-- 注册斜杠命令
DataToColor:RegisterChatCommand('testhekili', 'TestHekiliAccess')

DataToColor:Print("Hekili测试模块已加载。使用 /testhekili 命令进行测试")
