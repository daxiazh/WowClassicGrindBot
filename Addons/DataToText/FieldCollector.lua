----------------------------------------------------------------------------
-- FieldCollector.lua
-- 数据收集器模块 - 收集 108 个游戏数据字段
--
-- 注意:
-- - Lua 5.1 兼容（无 0x 字面量，使用 math.mod）
-- - 每个字段为 24 bits (0-16777215)
-- - 按 DataToColor 的字段索引顺序排列
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextFieldCollector = {}
local FC = DataToTextFieldCollector

----------------------------------------------------------------------------
-- WoW 1.12 兼容性：访问 pfUI 环境中的 UnitCastingInfo
----------------------------------------------------------------------------
local function GetUnitCastingInfo(unit)
    -- 尝试全局函数（TBC+）
    if _G.UnitCastingInfo then
        return _G.UnitCastingInfo(unit)
    end
    
    -- 尝试 pfUI 环境中的函数
    if pfUI and pfUI.env and pfUI.env.UnitCastingInfo then
        return pfUI.env.UnitCastingInfo(unit)
    end
    
    -- 不支持
    return nil
end

-- 常量定义
local FIELD_COUNT = 108
local MAX_24BIT = 16777215  -- 2^24 - 1

----------------------------------------------------------------------------
-- 辅助函数
----------------------------------------------------------------------------

-- 限制值在 24 位范围内
local function Clamp24Bit(value)
    if not value or value < 0 then
        return 0
    end
    if value > MAX_24BIT then
        return MAX_24BIT
    end
    return math.floor(value)
end

-- Float 转整数 (x10 精度)
local function FloatToInt(value, scale)
    scale = scale or 10
    if not value then
        return 0
    end
    return math.floor(value * scale)
end

-- 编码名称前3个字符
local function EncodeName3Chars(name, offset)
    if not name or name == "" then
        return 0
    end

    local len = string.len(name)
    local result = 0

    for i = 1, 3 do
        local index = offset + i - 1
        if index <= len then
            local byte = string.byte(name, index)
            result = result + bit.lshift(byte, (i - 1) * 8)
        end
    end

    return result
end

-- 获取背包信息
local function GetBagInfo()
    local totalSlots = 0
    local freeSlots = 0
    for bag = 0, 4 do
        local slots = GetContainerNumSlots(bag) or 0
        totalSlots = totalSlots + slots
        for slot = 1, slots do
            if not GetContainerItemLink(bag, slot) then
                freeSlots = freeSlots + 1
            end
        end
    end
    return freeSlots, totalSlots
end

-- 获取玩家朝向（模拟，WoW 1.12 无此 API）
local function GetPlayerFacing()
    -- WoW 1.12 vanilla 无 GetPlayerFacing API
    -- 返回 0 作为占位符
    return 0
end

-- 获取地图 ID（模拟）
local function GetMapUIId()
    -- WoW 1.12 使用 GetCurrentMapZone 和 GetCurrentMapContinent
    -- 简化实现：返回 0
    return 0
end

-- 获取尸体位置（模拟）
local function GetCorpsePosition()
    -- WoW 1.12 vanilla 无直接 API
    -- 返回 (0, 0) 作为占位符
    return 0, 0
end

-- 获取目标 GUID（WoW 1.12 兼容版本）
--
-- 设计说明:
-- 1. WoW 1.12 的 GUID 是完整的十六进制字符串（如 "0xF130000F0FA275ED4"，17位十六进制）
-- 2. 每个字段只能存储 24 bits (3 bytes)，最大值 16777215 (0xFFFFFF，6位十六进制)
-- 3. 直接转换完整 GUID 会超出 Lua 数值精度和字段容量限制
-- 4. 解决方案: 只取最后 6 位十六进制 (= 24 bits)，足够在当前视野内区分不同单位
--
-- 对比 DataToColor (TBC+):
-- - TBC+ GUID 格式: "Creature-0-4488-530-222-19350-000005C0D70" (分段格式)
-- - DataToColor 策略: 提取 NPC ID + spawn 时间戳，计算 24-bit 哈希值
--   (spawnEpochOffset + spawnIndex + npcId) % 0x1000000
-- - 本实现: 直接取完整 GUID 的低 24 bits，达到相同效果
--
-- 为什么只取低 24 bits:
-- - 数据传输限制: DataToColor 使用 RGB 像素 (3字节)，DataToText 使用网格编码 (3字节)
-- - 实际需求: 机器人只需在当前战斗/视野内区分目标，不需要全局唯一 ID
-- - 碰撞可接受: 不同区域/时间的相同 NPC 碰撞不影响机器人决策
--
-- 注意: 这不是全局唯一 ID，只保证短时间内的区分度
local function GetTargetGUID()
    -- WoW 1.12: UnitExists 返回 (exists, guid_hex_string)
    local exists, guid = UnitExists("target")
    if not exists or not guid then
        return 0
    end
    
    if type(guid) == "string" then
        -- WoW 1.12 GUID 格式: "0xF130000F0FA275ED4" (十六进制字符串)
        -- 移除 "0x" 前缀
        local hexStr = string.gsub(guid, "^0[xX]", "")
        
        -- GUID 通常是 17 位十六进制，只取最后 6 位（24 bits）
        -- 原因: tonumber() 对超长十六进制字符串可能精度丢失，且字段容量限制为 24 bits
        -- 例: "F130000F0FA275ED4" -> "75ED4" (最后6位)
        if string.len(hexStr) > 6 then
            hexStr = string.sub(hexStr, -6)  -- 取最后6位十六进制
        end
        
        local numGuid = tonumber(hexStr, 16)
        if numGuid then
            return numGuid  -- 范围: 0 ~ 16777215 (0x000000 ~ 0xFFFFFF)
        end
    elseif type(guid) == "number" then
        -- 数字类型（罕见情况），取模确保在 24-bit 范围内
        return math.mod(guid, 16777216)
    end
    
    return 0
end

-- 获取 Buff 数量（WoW 1.12 使用 UnitBuff）
local function GetBuffCount(unit)
    local count = 0
    for i = 1, 40 do
        local name = UnitBuff(unit, i)
        if name then
            count = count + 1
        else
            break
        end
    end
    return count
end

-- 获取 Debuff 数量（WoW 1.12 使用 UnitDebuff）
local function GetDebuffCount(unit)
    local count = 0
    for i = 1, 40 do
        local name = UnitDebuff(unit, i)
        if name then
            count = count + 1
        else
            break
        end
    end
    return count
end

----------------------------------------------------------------------------
-- 字段收集函数
----------------------------------------------------------------------------

-- 收集所有 108 个字段
function FC.CollectAllFields()
    local fields = {}

    -- 初始化所有字段为 0
    for i = 0, FIELD_COUNT - 1 do
        fields[i] = 0
    end

    -- === 基础信息 (0-9) ===

    -- 0: 初始化标志 (0 = 正常模式)
    fields[0] = 0

    -- 1-2: 玩家坐标 (float * 10)
    -- WoW 1.12 vanilla 无 GetPlayerMapPosition，使用占位符
    fields[1] = 0
    fields[2] = 0

    -- 3: 玩家朝向
    fields[3] = GetPlayerFacing()

    -- 4: 地图 ID
    fields[4] = GetMapUIId()

    -- 5: 玩家等级
    fields[5] = UnitLevel("player") or 0

    -- 6-7: 尸体坐标
    local corpseX, corpseY = GetCorpsePosition()
    fields[6] = corpseX
    fields[7] = corpseY

    -- 8-9: 布尔标志位 (待实现)
    fields[8] = 0  -- Bits1
    fields[9] = 0  -- Bits2

    -- === 玩家属性 (10-15) ===

    fields[10] = UnitHealthMax("player") or 0
    fields[11] = UnitHealth("player") or 0

    -- 12-13: 能量值 (法力/怒气/能量)
    local powerType = UnitPowerType("player")
    fields[12] = UnitManaMax("player") or 0
    fields[13] = UnitMana("player") or 0

    -- 14-15: 法力值（对于非法力职业，这里存储相同数据）
    fields[14] = UnitManaMax("player") or 0
    fields[15] = UnitMana("player") or 0

    -- === 目标信息 (16-19, 43, 56-59) ===

    if UnitExists("target") then
        local targetName = UnitName("target") or ""

        -- 16-17: 目标名称编码（前3字符 + 后3字符）
        fields[16] = EncodeName3Chars(targetName, 1)
        fields[17] = EncodeName3Chars(targetName, 4)

        -- 18-19: 目标生命值
        fields[18] = UnitHealthMax("target") or 0
        fields[19] = UnitHealth("target") or 0

        -- 43: 目标等级 + 分类
        local level = UnitLevel("target") or 0
        local classification = UnitClassification("target") or "normal"
        local classCode = 0
        if classification == "elite" then
            classCode = 1
        elseif classification == "rare" then
            classCode = 2
        elseif classification == "rareelite" then
            classCode = 3
        elseif classification == "worldboss" then
            classCode = 4
        end
        fields[43] = level + bit.lshift(classCode, 16)

        -- 56: 目标 NPC ID（从 GUID 提取，简化实现）
        fields[56] = 0  -- 需要解析 GUID

        -- 57: 目标 GUID
        fields[57] = GetTargetGUID()

        -- 58: 目标正在施法的法术 ID
        -- WoW 1.12 限制: pfUI libcast 仅返回法术名称，无法获取法术ID
        -- pfUI 返回值: cast, nameSubtext, text, texture, startTime, endTime, isTradeSkill (无spellId)
        -- TBC+ 返回值: cast, nameSubtext, text, texture, startTime, endTime, isTradeSkill, castID, notInterruptible, spellId
        local cast = GetUnitCastingInfo("target")
        -- WoW 1.12 无法获取法术ID，始终返回0
        fields[58] = 0

        -- 59: 鼠标悬停目标 + 目标的目标
        fields[59] = 0  -- 待实现
    end

    -- === 背包系统 (20-24, 44-45) ===

    -- 20: 背包信息
    local freeSlots, totalSlots = GetBagInfo()
    fields[20] = freeSlots + bit.lshift(totalSlots, 8)

    -- 21-24: 背包物品（简化实现，需要队列轮询）
    fields[21] = 0
    fields[22] = 0
    fields[23] = 0
    fields[24] = 0

    -- 44-45: 金钱
    local money = GetMoney() or 0
    fields[44] = math.mod(money, 1000000)  -- 铜币部分
    fields[45] = math.floor(money / 1000000)  -- 金币部分

    -- === 动作条状态 (25-37) ===

    -- 25-29: 动作条按钮当前状态（简化实现）
    for i = 25, 29 do
        fields[i] = 0
    end

    -- 30-34: 动作条按钮可用性
    for i = 30, 34 do
        fields[i] = 0
    end

    -- 35-37: 动作条消耗和冷却
    fields[35] = 0
    fields[36] = 0
    fields[37] = 0

    -- === 宠物信息 (38-39, 68-69) ===

    if UnitExists("pet") then
        fields[38] = UnitHealthMax("pet") or 0
        fields[39] = UnitHealth("pet") or 0
        fields[68] = 0  -- 宠物 GUID（待实现）
        fields[69] = 0  -- 宠物目标 GUID
    else
        fields[38] = 0
        fields[39] = 0
        fields[68] = 0
        fields[69] = 0
    end

    -- === Buff/Debuff 系统 (41-42, 55, 79-84, 91-93, 104-105) ===

    -- 41-42: Buff/Debuff 掩码（待实现）
    fields[41] = 0
    fields[42] = 0

    -- 55: Buff/Debuff 计数
    local playerBuffCount = GetBuffCount("player")
    local playerDebuffCount = GetDebuffCount("player")
    local targetBuffCount = UnitExists("target") and GetBuffCount("target") or 0
    local targetDebuffCount = UnitExists("target") and GetDebuffCount("target") or 0

    fields[55] = playerDebuffCount + bit.lshift(playerBuffCount, 8) +
                 bit.lshift(targetDebuffCount, 16) + bit.lshift(targetBuffCount, 24)

    -- 79-84: Buff/Debuff 详情（简化实现）
    fields[79] = 0
    fields[80] = 0
    fields[81] = 0
    fields[82] = 0
    fields[83] = 0
    fields[84] = 0

    -- 91-93: 焦点 Buff（WoW 1.12 无焦点系统）
    fields[91] = 0
    fields[92] = 0
    fields[93] = 0

    -- 104-105: 玩家 Debuff
    fields[104] = 0
    fields[105] = 0

    -- === 技能和施法 (40, 46-54, 60-63, 70-76, 94-95) ===

    -- 40: 法术距离检测
    fields[40] = 0

    -- 46: 种族 ID + 职业 ID + 客户端版本
    local _, race = UnitRace("player")
    local _, class = UnitClass("player")
    fields[46] = 0  -- 需要种族/职业 ID 映射

    -- 47-54: 各种游戏状态
    fields[47] = 0  -- UI 错误消息时间戳
    fields[48] = 0  -- 变形形态 ID
    fields[49] = 0  -- 距离范围

    -- 50-51: 经验值
    fields[50] = UnitXP and UnitXP("player") or 0
    fields[51] = UnitXPMax and UnitXPMax("player") or 0

    fields[52] = 0  -- 最后的 UI 错误消息
    fields[53] = 0  -- 玩家正在施法的法术 ID
    fields[54] = 0  -- 装备耐久度 + 连击点

    -- 60-63: 施法和攻击时间
    fields[60] = 0
    fields[61] = 0
    fields[62] = 0
    fields[63] = 0

    -- 70-76: 施法和冷却信息
    for i = 70, 76 do
        fields[i] = 0
    end

    -- 94-95: GCD
    fields[94] = 0
    fields[95] = 0

    -- === 战斗日志 (64-67) ===

    fields[64] = 0  -- 造成伤害队列
    fields[65] = 0  -- 受到伤害队列
    fields[66] = 0  -- 怪物死亡队列
    fields[67] = 0  -- Miss 类型队列

    -- === 交互和对话 (73, 97-99, 101-103) ===

    fields[73] = 0  -- Gossip 对话队列
    fields[97] = 0  -- 拾取物品
    fields[98] = 0  -- 聊天消息
    fields[99] = 0  -- 聊天消息元数据
    fields[101] = 0  -- SoftInteract GUID
    fields[102] = 0  -- SoftInteract NPC ID
    fields[103] = 0  -- SoftInteract 类型

    -- === 鼠标悬停 (85-87) ===

    if UnitExists("mouseover") then
        fields[85] = UnitLevel("mouseover") or 0
        fields[86] = 0  -- NPC ID
        fields[87] = 0  -- GUID
    else
        fields[85] = 0
        fields[86] = 0
        fields[87] = 0
    end

    -- === 其他信息 (74, 88-90, 96, 100, 106-107) ===

    fields[74] = 0  -- 自定义触发器
    fields[88] = 0  -- 远程伤害

    -- 89-90: 焦点生命值（WoW 1.12 无焦点系统）
    fields[89] = 0
    fields[90] = 0

    fields[96] = 0  -- SpellQueueWindow + 网络延迟
    fields[100] = 0  -- 布尔标志位3

    -- 106: GlobalTime 帧计数器（模拟 DataToColor 的行为）
    -- 每次 UpdateDisplay() 递增，溢出后重置为 4
    if DataToText_GetGlobalTime then
        fields[106] = DataToText_GetGlobalTime()
    else
        fields[106] = 0  -- 回退方案（如果函数不存在）
    end

    -- 107: 元数据/验证标志
    fields[107] = 1  -- 标记数据有效

    -- === 焦点目标 (77-78, WoW 1.12 无焦点系统) ===

    fields[77] = 0
    fields[78] = 0

    -- 限制所有值在 24 位范围内
    for i = 0, FIELD_COUNT - 1 do
        fields[i] = Clamp24Bit(fields[i])
    end

    return fields
end

-- 获取字段数量
function FC.GetFieldCount()
    return FIELD_COUNT
end

-- 获取字段名称（调试用）
function FC.GetFieldName(index)
    local names = {
        [0] = "InitFlag",
        [1] = "PlayerX",
        [2] = "PlayerY",
        [3] = "PlayerFacing",
        [4] = "MapID",
        [5] = "PlayerLevel",
        [6] = "CorpseX",
        [7] = "CorpseY",
        [8] = "Bits1",
        [9] = "Bits2",
        [10] = "PlayerMaxHP",
        [11] = "PlayerHP",
        [12] = "PlayerMaxPower",
        [13] = "PlayerPower",
        [14] = "PlayerMaxMana",
        [15] = "PlayerMana",
        [16] = "TargetName1",
        [17] = "TargetName2",
        [18] = "TargetMaxHP",
        [19] = "TargetHP",
        [20] = "BagInfo",
        -- ... 其他字段名称
    }
    return names[index] or "Field" .. index
end
